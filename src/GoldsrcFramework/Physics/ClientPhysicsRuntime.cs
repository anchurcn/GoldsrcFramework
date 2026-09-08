using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GoldsrcFramework.Ecs;
using GoldsrcFramework.Engine.Native;
using NativeInterop;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Core.Mathematics;
using Stride.Engine;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;
using StrideEntity = Stride.Engine.Entity;

namespace GoldsrcFramework.Physics;

/// <summary>
/// Connects GoldSrc temporary entities to Stride's built-in Bepu physics system.
/// </summary>
[Obsolete("ClientPhysicsRuntime is deprecated. Use GoldsrcClientGame directly for the GoldSrc-Stride bridge.")]
internal sealed unsafe class ClientPhysicsRuntime : IDisposable
{
    private const string CreateBoxCommand = "gsf_phys_box";
    private const string GrenadeModel = "models/w_grenade.mdl";
    private const int TempEntityPersist = 0x0000_2000;
    private const int TempEntityNoModel = 0x0004_0000;
    private const float LifetimeSeconds = 30.0f;
    private static ClientPhysicsRuntime? current;

    private readonly Dictionary<nint, PhysicsTempEntity> tempEntities = [];
    private GoldsrcClientGame? game;
    private BepuConfiguration? physics;
    private bool disposed;

    public ClientSceneManagementSystem? SceneManagement => game?.SceneManagement;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (game is not null)
            return;

        game = new GoldsrcClientGame();
        current = this;
        RegisterCommands();
    }

    public void Tick(double clientTime, double frameTime, double gravity)
    {
        if (game is null || physics is null)
            return;

        foreach (var simulation in physics.BepuSimulations)
            simulation.PoseGravity = new Vector3(0, 0, -(float)gravity);

        var elapsed = double.IsFinite(frameTime) && frameTime > 0
            ? TimeSpan.FromSeconds(Math.Min(frameTime, 0.1))
            : TimeSpan.Zero;

        game.Tick(TimeSpan.FromSeconds(clientTime), elapsed);
    }

    public void Reset()
    {
        foreach (var item in tempEntities.Values.ToArray())
            Destroy(item, clientTime: 0, hideTempEntity: false);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (ReferenceEquals(current, this))
            current = null;

        // GoldSrc owns TEMPENTITY memory. During shutdown the engine may already
        // have released the active/free lists, so cleanup must not dereference
        // cached TEMPENTITY pointers. The binding is invalidated below before
        // the managed Stride scene is disposed.
        foreach (var item in tempEntities.Values.ToArray())
            Destroy(item, clientTime: 0, hideTempEntity: false);

        game?.Dispose();
        game = null;
        physics = null;
    }

    private void RegisterCommands()
    {
        var engine = EngineApi.PClient;
        if (engine == null || engine->AddCommand == null)
            return;

        Span<byte> command = stackalloc byte[64];
        WriteNullTerminatedAscii(CreateBoxCommand, command);
        fixed (byte* commandPointer = command)
            engine->AddCommand((NChar*)commandPointer, &CreatePhysicsBoxCommand);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CreatePhysicsBoxCommand()
    {
        try
        {
            current?.CreatePhysicsBox();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPhysics] Create failed: {exception}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void UpdatePhysicsTempEntity(TEMPENTITY* tempEntity, float frameTime, float clientTime)
    {
        try
        {
            current?.UpdateTempEntityLifetime(tempEntity, clientTime);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPhysics] TempEnt update failed: {exception}");
        }
    }

    private void CreatePhysicsBox()
    {
        if (game is null)
            return;

        var engine = EngineApi.PClient;
        if (engine == null || engine->pEfxAPI == null || engine->CL_LoadModel == null)
            return;

        var model = LoadModel(engine, GrenadeModel, out var modelIndex);
        if (model == null)
        {
            EngineApi.DrawStringCenter($"Unable to load {GrenadeModel}");
            return;
        }

        GetSpawnPose(engine, out var spawnOrigin, out var forward);
        var tempEntity = engine->pEfxAPI->CL_TentEntAllocCustom(
            (float*)&spawnOrigin,
            model,
            0,
            &UpdatePhysicsTempEntity);
        if (tempEntity == null)
        {
            EngineApi.DrawStringCenter("Unable to allocate physics TempEnt");
            return;
        }

        var clientTime = engine->GetClientTime != null ? engine->GetClientTime() : 0;
        tempEntity->flags |= TempEntityPersist;
        tempEntity->die = float.MaxValue;
        tempEntity->entity.model = model;
        tempEntity->entity.origin = spawnOrigin;
        tempEntity->entity.angles = default;
        tempEntity->entity.curstate.modelindex = modelIndex;
        tempEntity->entity.curstate.sequence = 0;
        tempEntity->entity.curstate.frame = 0;

        var binding = new ClEntityTransformBinding(&tempEntity->entity);
        var (bodyEntity, body) = CreateBodyEntity(binding, spawnOrigin);
        var groundEntity = CreateTestGround(spawnOrigin);
        var groundAdded = false;

        try
        {
            game.Add(groundEntity);
            groundAdded = true;
            game.Add(bodyEntity);

            // BodyComponent velocity setters require the body to be attached to a simulation.
            body.LinearVelocity = ToStride(forward) * 180.0f + Vector3.UnitZ * 80.0f;
            body.AngularVelocity = new Vector3(2.5f, 5.0f, 1.5f);

            tempEntities.Add(
                (nint)tempEntity,
                new PhysicsTempEntity(
                    tempEntity,
                    binding,
                    bodyEntity,
                    groundEntity,
                    clientTime + LifetimeSeconds));
        }
        catch
        {
            game.Remove(bodyEntity);
            if (groundAdded)
                game.Remove(groundEntity);

            binding.Invalidate();
            tempEntity->flags = TempEntityNoModel;
            tempEntity->die = clientTime;
            throw;
        }

        EngineApi.DrawStringCenter($"Created Stride physics sphere ({CreateBoxCommand})");
    }

    private static (StrideEntity Entity, BodyComponent Body) CreateBodyEntity(
        ClEntityTransformBinding binding,
        GoldsrcVector3 spawnOrigin)
    {
        var collider = new CompoundCollider();
        collider.Colliders.Add(new SphereCollider { Radius = 4.0f });

        var body = new BodyComponent
        {
            Collider = collider,
            InterpolationMode = InterpolationMode.Interpolated,
        };

        var entity = new StrideEntity("client_physics_grenade");
        entity.Transform.Position = ToStride(spawnOrigin);
        entity.Components.Add(body);
        entity.Components.Add(new GoldsrcTransformLinkComponent(binding, TransformAuthority.Stride));
        return (entity, body);
    }

    private static StrideEntity CreateTestGround(GoldsrcVector3 spawnOrigin)
    {
        var collider = new CompoundCollider();
        collider.Colliders.Add(new BoxCollider { Size = new Vector3(9000, 9000, 8) });

        var entity = new StrideEntity("client_physics_test_ground");
        entity.Transform.Position = new Vector3(spawnOrigin.X, spawnOrigin.Y, spawnOrigin.Z - 48.0f);
        entity.Components.Add(new StaticComponent { Collider = collider });
        return entity;
    }

    private void UpdateTempEntityLifetime(TEMPENTITY* tempEntity, float clientTime)
    {
        if (!tempEntities.TryGetValue((nint)tempEntity, out var item))
            return;

        if (clientTime >= item.ExpiresAt)
            Destroy(item, clientTime, hideTempEntity: true);
    }

    private void Destroy(PhysicsTempEntity item, float clientTime, bool hideTempEntity)
    {
        if (!tempEntities.Remove((nint)item.TempEntity))
            return;

        // Invalidate the native binding before removing the Stride entities. The
        // removal can synchronously notify processors, and none of them should
        // be able to observe a native pointer that is being retired.
        item.Binding.Invalidate();

        if (game is not null)
        {
            game.Remove(item.BodyEntity);
            game.Remove(item.GroundEntity);
        }

        if (!hideTempEntity || item.TempEntity == null)
            return;

        item.TempEntity->die = clientTime;
        item.TempEntity->flags = TempEntityNoModel;
    }

    private static model_t* LoadModel(ClientEngineFuncs* engine, string path, out int modelIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[128];
        WriteNullTerminatedAscii(path, pathBuffer);
        var loadedModelIndex = 0;
        fixed (byte* pathPointer = pathBuffer)
        {
            var model = engine->CL_LoadModel((NChar*)pathPointer, &loadedModelIndex);
            modelIndex = loadedModelIndex;
            return model;
        }
    }

    private static void GetSpawnPose(
        ClientEngineFuncs* engine,
        out GoldsrcVector3 origin,
        out GoldsrcVector3 forward)
    {
        var localPlayer = engine->GetLocalPlayer != null ? engine->GetLocalPlayer() : null;
        origin = localPlayer != null ? localPlayer->origin : new GoldsrcVector3(0, 0, 64);

        var viewAngles = default(GoldsrcVector3);
        var calculatedForward = new GoldsrcVector3(1, 0, 0);
        if (engine->GetViewAngles != null && engine->AngleVectors != null)
        {
            engine->GetViewAngles((float*)&viewAngles);
            engine->AngleVectors((float*)&viewAngles, (float*)&calculatedForward, null, null);
        }

        forward = calculatedForward;
        origin += forward * 48.0f;
        origin.Z += 24.0f;
    }

    private static Vector3 ToStride(GoldsrcVector3 value)
    {
        return new Vector3(value.X, value.Y, value.Z);
    }

    private static void WriteNullTerminatedAscii(string value, Span<byte> buffer)
    {
        buffer.Clear();
        var written = Encoding.ASCII.GetBytes(value, buffer);
        if (written >= buffer.Length)
            throw new ArgumentException("The native string buffer is too small.", nameof(value));

        buffer[written] = 0;
    }

    private sealed class PhysicsTempEntity(
        TEMPENTITY* tempEntity,
        ClEntityTransformBinding binding,
        StrideEntity bodyEntity,
        StrideEntity groundEntity,
        float expiresAt)
    {
        public TEMPENTITY* TempEntity { get; } = tempEntity;
        public ClEntityTransformBinding Binding { get; } = binding;
        public StrideEntity BodyEntity { get; } = bodyEntity;
        public StrideEntity GroundEntity { get; } = groundEntity;
        public float ExpiresAt { get; } = expiresAt;
    }
}
