using System.Reflection;
using Stride.BepuPhysics;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Hosts Stride game systems inside a GoldSrc client or server module.
/// GoldSrc owns the outer loop; this class owns the Stride runtime lifetime.
/// </summary>
public sealed class GoldsrcClientGame : IDisposable
{
    private static readonly Action<GameTime, TimeSpan, TimeSpan, bool> UpdateGameTime =
        CreateGameTimeUpdater();

    private readonly List<IDisposable> ownedServices = [];
    private bool disposed;
    private double? lastGravity;

    public GoldsrcClientGame()
    {
        Services = new ServiceRegistry();
        GameSystems = new GameSystemCollection(Services);
        Services.AddService<IGameSystemCollection>(GameSystems);
        Services.ServiceAdded += OnServiceAdded;
        Services.ServiceRemoved += OnServiceRemoved;

        // Register the script systems before GoldsrcSceneSystem: its constructor adds
        // the script processors, whose OnSystemAdd resolves these services synchronously.
        ScriptSystem = new GoldsrcScriptSystem(Services);
        Services.AddService(ScriptSystem);

        LateUpdateSystem = new LateUpdateScriptSystem(Services);
        Services.AddService(LateUpdateSystem);

        SceneSystem = new GoldsrcSceneSystem(Services);
        Services.AddService(SceneSystem);

        SceneManagement = new ClientSceneManagementSystem(Services, this, SceneSystem);
        Services.AddService(SceneManagement);

        transformSync = new GoldsrcTransformSyncSystem(Services);

        Time = new GameTime();

        GameSystems.Add(SceneManagement);
        GameSystems.Add(transformSync);
        GameSystems.Add(SceneSystem);
        GameSystems.Add(ScriptSystem);
        GameSystems.Add(LateUpdateSystem);
        GameSystems.Initialize();
    }

    public ServiceRegistry Services { get; }

    public GameSystemCollection GameSystems { get; }

    public ClientSceneManagementSystem SceneManagement { get; }

    public GoldsrcSceneSystem SceneSystem { get; }

    public GoldsrcScriptSystem ScriptSystem { get; }

    public LateUpdateScriptSystem LateUpdateSystem { get; }

    private readonly GoldsrcTransformSyncSystem transformSync;

    public GameTime Time { get; }

    public Scene RootScene => SceneSystem.RootScene;

    public SceneInstance SceneInstance => SceneSystem.SceneInstance;

    /// <summary>
    /// Syncs GoldSrc gravity to all Bepu simulations. Only writes when the value changes.
    /// </summary>
    public void SyncGravity(double gravity)
    {
        if (gravity == lastGravity)
            return;

        if (Services.GetService<BepuConfiguration>() is not { } physics)
            return; // Don't cache — retry next frame once physics is ready

        lastGravity = gravity;
        foreach (var simulation in physics.BepuSimulations)
            simulation.PoseGravity = new Vector3(0, 0, -(float)gravity);
    }

    /// <summary>
    /// Clears all entities from the root scene. Called on map change / reset.
    /// </summary>
    public void Reset()
    {
        var entities = RootScene.Entities.ToArray();
        foreach (var entity in entities)
            RootScene.Entities.Remove(entity);
    }

    public void Tick(TimeSpan totalTime, TimeSpan elapsedTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (elapsedTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsedTime));

        UpdateGameTime(Time, totalTime, elapsedTime, true);
        GameSystems.Update(Time);
        GameSystems.Draw(Time);
    }

    public void Add(Entity entity)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(entity);
        RootScene.Entities.Add(entity);
    }

    public bool Remove(Entity entity)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(entity);
        return RootScene.Entities.Remove(entity);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        GameSystems.Dispose();

        var physics = Services.GetService<BepuConfiguration>();
        if (physics is not null)
        {
            foreach (var simulation in physics.BepuSimulations)
                simulation.Dispose();
        }

        for (var index = ownedServices.Count - 1; index >= 0; index--)
            ownedServices[index].Dispose();

        ownedServices.Clear();
        Services.ServiceAdded -= OnServiceAdded;
        Services.ServiceRemoved -= OnServiceRemoved;
    }

    private static Action<GameTime, TimeSpan, TimeSpan, bool> CreateGameTimeUpdater()
    {
        var update = typeof(GameTime).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(TimeSpan), typeof(TimeSpan), typeof(bool)],
            modifiers: null)
            ?? throw new MissingMethodException(typeof(GameTime).FullName, "Update");

        return update.CreateDelegate<Action<GameTime, TimeSpan, TimeSpan, bool>>();
    }

    private void OnServiceAdded(object? sender, ServiceEventArgs eventArgs)
    {
        if (eventArgs.Instance is IDisposable disposable &&
            eventArgs.Instance is not IGameSystemBase &&
            !ReferenceEquals(eventArgs.Instance, GameSystems))
        {
            ownedServices.Add(disposable);
        }
    }

    private void OnServiceRemoved(object? sender, ServiceEventArgs eventArgs)
    {
        if (eventArgs.Instance is IDisposable disposable)
            ownedServices.Remove(disposable);
    }
}
