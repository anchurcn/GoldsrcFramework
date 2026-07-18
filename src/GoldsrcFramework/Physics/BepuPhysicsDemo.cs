using System.Reflection;
using System.Runtime.CompilerServices;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.BepuPhysics.Systems;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

using SVector3 = Stride.Core.Mathematics.Vector3;
using SQuaternion = Stride.Core.Mathematics.Quaternion;
using SEntity = Stride.Engine.Entity;

namespace GoldsrcFramework.Physics;

/// <summary>
/// BEPU physics demo using Stride's ECS-driven physics pipeline.
/// 
/// Architecture:
/// - Entity + BodyComponent + BoxCollider — the "Stride way" to define physics objects
/// - CollidableProcessor handles entity → simulation attachment (ReAttach → TryAttach → AttachInner)
/// - BepuSimulation.Update() handles physics stepping + automatic Transform sync
/// - No manual body creation, no manual sync, no reflection for BodyReference
/// 
/// The only "non-standard" parts are:
/// - Minimal ServiceRegistry (no Game/Scene/EntityManager needed)
/// - Dummy ShapeCacheSystem (we don't use AppendModel debug rendering)
/// - Reflection to call internal methods (CollidableProcessor.OnSystemAdd, OnEntityComponentAdding,
///   BepuSimulation.Update)
/// </summary>
public class BepuPhysicsDemo
{
    private BepuSimulation _bepuSimulation = null!;
    private ServiceRegistry _serviceRegistry = null!;
    private CollidableProcessor _collidableProcessor = null!;
    private SEntity _boxEntity = null!;
    private BodyComponent _bodyComponent = null!;

    private readonly SVector3 _boxHalfSize = new(10f, 10f, 10f);
    private readonly SVector3 _initialPosition = new(0f, 0f, 200f);

    // Cached reflection delegates for per-frame calls
    private static readonly Action<BepuSimulation, TimeSpan> s_bepuSimulationUpdate;
    private static readonly Action<CollidableProcessor> s_processorOnSystemAdd;
    private static readonly Action<CollidableProcessor, Stride.Engine.Entity, CollidableComponent, CollidableComponent> s_processorOnEntityComponentAdding;
    private static readonly Action<EntityProcessor, IServiceRegistry> s_setProcessorServices;

    public bool Initialized { get; private set; }

    /// <summary>
    /// The Stride entity representing the physics box.
    /// Has BodyComponent + BoxCollider attached, fully managed by the ECS physics pipeline.
    /// </summary>
    public SEntity BoxEntity => _boxEntity;

    /// <summary>
    /// The BodyComponent attached to the entity.
    /// After initialization, BodyReference is properly set by AttachInner.
    /// </summary>
    public BodyComponent BodyComponent => _bodyComponent;

    /// <summary>
    /// Current world-space position of the box (synced automatically from physics).
    /// </summary>
    public SVector3 BoxPosition => _boxEntity.Transform.Position;

    /// <summary>
    /// Current world-space rotation of the box (synced automatically from physics).
    /// </summary>
    public SQuaternion BoxRotation => _boxEntity.Transform.Rotation;

    static BepuPhysicsDemo()
    {
        // --- Cache reflection delegates (one-time cost) ---

        // BepuSimulation.Update(TimeSpan) — internal, handles Timestep + SyncActiveTransformsWithPhysics
        var updateMethod = typeof(BepuSimulation).GetMethod("Update",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        s_bepuSimulationUpdate = (sim, time) => updateMethod.Invoke(sim, [time]);

        // CollidableProcessor.OnSystemAdd() — protected, wires up BepuConfiguration & ShapeCache from services
        var onSystemAddMethod = typeof(CollidableProcessor).GetMethod("OnSystemAdd",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        s_processorOnSystemAdd = proc => onSystemAddMethod.Invoke(proc, null);

        // CollidableProcessor.OnEntityComponentAdding(Entity, CollidableComponent, CollidableComponent) — protected
        var onEntityAddingMethod = typeof(CollidableProcessor).GetMethod("OnEntityComponentAdding",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        s_processorOnEntityComponentAdding = (proc, entity, comp, data) =>
            onEntityAddingMethod.Invoke(proc, [entity, comp, data]);

        // EntityProcessor.Services — public get, internal set
        var servicesProp = typeof(EntityProcessor).GetProperty("Services",
            BindingFlags.Instance | BindingFlags.Public)!;
        s_setProcessorServices = (proc, svc) => servicesProp.SetValue(proc, svc);
    }

    /// <summary>
    /// Initialize the physics pipeline and create the box entity via Stride ECS.
    /// </summary>
    public void Initialize()
    {
        if (Initialized)
            return;

        // 1. Create BepuSimulation (creates its own ThreadDispatcher, BufferPool, Simulation internally)
        _bepuSimulation = new BepuSimulation
        {
            PoseGravity = new SVector3(0, 0, -800f) // Match Goldsrc gravity
        };

        // 2. Create BepuConfiguration with our simulation
        var bepuConfig = new BepuConfiguration
        {
            BepuSimulations = [_bepuSimulation]
        };

        // 3. Create ServiceRegistry and register required services
        _serviceRegistry = new ServiceRegistry();
        _serviceRegistry.AddService(bepuConfig);

        // Dummy ShapeCacheSystem: we never call AppendModel (debug rendering),
        // and BoxCollider.AddToCompoundBuilder doesn't use ShapeCache at all.
        // Using GetUninitializedObject avoids the Graphics-dependent constructor.
        // ShapeCacheSystem is internal, so we must use reflection to get its type.
        var shapeCacheType = typeof(BepuSimulation).Assembly.GetType("Stride.BepuPhysics.Systems.ShapeCacheSystem")!;
        var dummyShapeCache = RuntimeHelpers.GetUninitializedObject(shapeCacheType);

        // Register via reflection: _serviceRegistry.AddService<T>(service)
        var addServiceMethod = typeof(ServiceRegistry).GetMethod("AddService")!.MakeGenericMethod(shapeCacheType);
        addServiceMethod.Invoke(_serviceRegistry, [dummyShapeCache]);

        // 4. Create CollidableProcessor and initialize it (OnSystemAdd wires up services)
        _collidableProcessor = new CollidableProcessor();
        s_setProcessorServices(_collidableProcessor, _serviceRegistry);
        s_processorOnSystemAdd(_collidableProcessor);

        // 5. Create entity with BodyComponent + BoxCollider — the Stride way
        _boxEntity = new SEntity("PhysicsBox");

        var compoundCollider = new CompoundCollider();
        compoundCollider.Colliders.Add(new BoxCollider
        {
            Size = _boxHalfSize * 2f // BoxCollider.Size is full extent
        });

        _bodyComponent = new BodyComponent
        {
            Collider = compoundCollider
        };
        _boxEntity.Components.Add(_bodyComponent);

        // 6. Set initial transform before attachment (ReAttach reads WorldMatrix)
        _boxEntity.Transform.Position = _initialPosition;
        _boxEntity.Transform.Rotation = SQuaternion.Identity;

        // 7. Attach entity to simulation via CollidableProcessor
        //    This triggers: ReAttach → Collider.TryAttach (creates BEPU shape) → AttachInner (creates BEPU body, sets BodyReference)
        s_processorOnEntityComponentAdding(_collidableProcessor, _boxEntity, _bodyComponent, _bodyComponent);

        Initialized = true;
    }

    /// <summary>
    /// Update the physics simulation. Call once per frame from HUD_Frame.
    /// BepuSimulation.Update handles:
    ///   1. Time accumulation / fixed timestep
    ///   2. Simulation.Timestep()
    ///   3. SyncActiveTransformsWithPhysics() — automatically syncs entity Transforms!
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
    public void Update(float deltaTime)
    {
        if (!Initialized)
            return;

        // Clamp delta to avoid physics explosion
        if (deltaTime <= 0 || deltaTime > 0.1f)
            deltaTime = 1f / 60f;

        s_bepuSimulationUpdate(_bepuSimulation, TimeSpan.FromSeconds(deltaTime));
    }

    /// <summary>
    /// Reset the box to its initial position and zero velocity.
    /// Uses BodyComponent.Teleport which properly handles both physics body and entity transform.
    /// </summary>
    public void Reset()
    {
        if (!Initialized)
            return;

        _bodyComponent.Teleport(_initialPosition, SQuaternion.Identity);
    }

    /// <summary>
    /// Release physics resources.
    /// </summary>
    public void Dispose()
    {
        if (!Initialized)
            return;

        _bepuSimulation.Dispose();
        Initialized = false;
    }
}