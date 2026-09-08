using Stride.Core;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Base class for game systems that manage entity lifecycle. Subscribes to
/// <see cref="EntityManager.EntityAdded"/> / <see cref="EntityManager.EntityRemoved"/>
/// events on the <see cref="GoldsrcSceneSystem.SceneInstance"/> and automatically
/// calls <see cref="IEnterExitCallable.OnEnter"/> / <see cref="IEnterExitCallable.OnExit"/>
/// on entity components that implement the interface.
/// </summary>
public abstract class SceneEntityLifecycleSystem : GameSystemBase
{
    private readonly GoldsrcSceneSystem sceneSystem;

    protected SceneEntityLifecycleSystem(IServiceRegistry registry, GoldsrcSceneSystem sceneSystem)
        : base(registry)
    {
        this.sceneSystem = sceneSystem ?? throw new ArgumentNullException(nameof(sceneSystem));
        Enabled = true;
        UpdateOrder = -1000;
    }

    public override void Initialize()
    {
        base.Initialize();
        sceneSystem.SceneInstance.EntityAdded += OnEntityAdded;
        sceneSystem.SceneInstance.EntityRemoved += OnEntityRemoved;
    }

    public override void Update(GameTime gameTime)
    {
        ProcessEntityLifecycle(gameTime);
    }

    protected abstract void ProcessEntityLifecycle(GameTime gameTime);

    private void OnEntityAdded(object? sender, Entity entity)
    {
        foreach (var component in entity.Components)
        {
            if (component is IEnterExitCallable callable)
                callable.OnEnter();
        }
    }

    private void OnEntityRemoved(object? sender, Entity entity)
    {
        foreach (var component in entity.Components)
        {
            if (component is IEnterExitCallable callable)
                callable.OnExit();
        }
    }
}