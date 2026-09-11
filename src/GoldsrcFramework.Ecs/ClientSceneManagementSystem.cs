using GoldsrcFramework.Ecs.Components;
using GoldsrcFramework.Engine.Native;
using Stride.Core;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Manages client-side entity visibility based on GoldSrc PVS (Potentially Visible Set).
/// Entities are marked visible each frame via <see cref="MarkEntityVisible"/> (called from
/// <c>HUD_AddEntity</c>), and the system settles adds/removes during <see cref="ProcessEntityLifecycle"/>.
/// <see cref="IEnterExitCallable.OnEnter"/> / <see cref="IEnterExitCallable.OnExit"/> are called
/// automatically by the base <see cref="SceneEntityLifecycleSystem"/> via <see cref="EntityManager"/> events.
/// Worldspawn (entity 0) is handled specially: created once per map and never PVS-managed.
/// </summary>
/// <remarks>
/// Brush model entities (doors, platforms, <c>func_wall</c>, ...) reach this system like any other
/// entity: the engine calls the client's <c>HUD_AddEntity</c> for every <c>ENTITY_NORMAL</c> entity,
/// brush models included, before it draws them. Their physics key is derived from the model index.
/// </remarks>
public sealed unsafe class ClientSceneManagementSystem : SceneEntityLifecycleSystem
{
    private const string WorldspawnKey = "*1"; // worldspawn is always model index 1

    private readonly GoldsrcClientGame game;
    private readonly Dictionary<int, Entity> activeEntities = [];
    private readonly Dictionary<int, VisibleEntity> visibleState = [];
    private HashSet<int> thisFrameVisible = [];
    private HashSet<int> lastFrameVisible = [];
    private readonly List<Entity> pendingAdd = [];
    private readonly List<Entity> pendingRemove = [];
    private bool hasWorldspawn;

    public ClientSceneManagementSystem(IServiceRegistry services, GoldsrcClientGame game, GoldsrcSceneSystem sceneSystem)
        : base(services, sceneSystem)
    {
        this.game = game ?? throw new ArgumentNullException(nameof(game));
    }

    /// <summary>Content manager injected into <see cref="HalfLifeBehavior"/> for physics prefab loading.</summary>
    public IContentManager? ContentManager { get; set; }

    /// <summary>Returns the entity associated with an entindex, or null if none exists.</summary>
    public Entity? GetEntity(int entindex)
    {
        activeEntities.TryGetValue(entindex, out var entity);
        return entity;
    }

    /// <summary>
    /// Marks an entity as visible this frame. Called from <c>HUD_AddEntity</c>.
    /// Entity 0 (worldspawn) is ignored — it is created once per map and never PVS-managed.
    /// </summary>
    /// <param name="nativeEntity">
    /// The native client entity. Its address is stable for the lifetime of the client, and it is only
    /// read while the Stride entity for it is being created.
    /// </param>
    /// <param name="isPlayer">True for <c>ET_PLAYER</c> entities, which resolve their model by name.</param>
    public void MarkEntityVisible(cl_entity_t* nativeEntity, bool isPlayer)
    {
        if (nativeEntity == null)
            return;

        int entindex = nativeEntity->index;
        if (entindex <= 0)
            return; // worldspawn handled separately

        thisFrameVisible.Add(entindex);
        visibleState[entindex] = new VisibleEntity(nativeEntity, isPlayer);
    }

    /// <summary>
    /// Drops all per-map state: the worldspawn bookkeeping and every entity this system knows about.
    /// Call on map change, before the scene is rebuilt.
    /// </summary>
    /// <remarks>
    /// <see cref="GoldsrcClientGame.Reset"/> removes the entities from the scene, which already runs
    /// <see cref="IEnterExitCallable.OnExit"/> for them. What it cannot do is forget that worldspawn
    /// existed — without this the new map would never get a static world.
    /// </remarks>
    public void ResetMapState()
    {
        hasWorldspawn = false;
        activeEntities.Clear();
        visibleState.Clear();
        thisFrameVisible.Clear();
        lastFrameVisible.Clear();
        pendingAdd.Clear();
        pendingRemove.Clear();
    }

    protected override void ProcessEntityLifecycle(GameTime gameTime)
    {
        // Create worldspawn once the map actually exposes a brush world model. With a content manager
        // configured but no map loaded yet, retry next frame instead of caching "*1" as missing.
        if (!hasWorldspawn && (ContentManager is null || ContentManager.IsExist(WorldspawnKey)))
        {
            var worldspawn = CreateWorldspawnEntity();
            activeEntities[0] = worldspawn;
            game.Add(worldspawn);
            hasWorldspawn = true;
        }

        // Collect entities that entered this frame (visible now, not visible last frame)
        foreach (var entindex in thisFrameVisible)
        {
            if (lastFrameVisible.Contains(entindex))
                continue;

            if (activeEntities.TryGetValue(entindex, out var existingEntity))
            {
                // Entity was previously created but left PVS and came back.
                // Re-add to scene. EntityAdded event fires → OnEnter called by base class.
                pendingAdd.Add(existingEntity);
            }
            else
            {
                // First time seeing this entity. Create a new Stride entity.
                var entity = CreateEntity(entindex);
                activeEntities[entindex] = entity;
                pendingAdd.Add(entity);
            }
        }

        // Collect entities that left this frame
        foreach (var entindex in lastFrameVisible)
        {
            if (thisFrameVisible.Contains(entindex))
                continue;

            if (activeEntities.TryGetValue(entindex, out var entity))
                pendingRemove.Add(entity);
        }

        // Apply removals first (EntityRemoved → OnExit), then adds (EntityAdded → OnEnter)
        foreach (var entity in pendingRemove)
            game.Remove(entity);

        foreach (var entity in pendingAdd)
            game.Add(entity);

        pendingAdd.Clear();
        pendingRemove.Clear();

        // Swap for next frame
        (lastFrameVisible, thisFrameVisible) = (thisFrameVisible, lastFrameVisible);
        thisFrameVisible.Clear();
        visibleState.Clear();
    }

    private Entity CreateEntity(int entindex)
    {
        visibleState.TryGetValue(entindex, out var visible);

        var entity = new Entity(DescribeEntity(entindex, visible.NativeEntity));

        if (visible.NativeEntity is not null)
        {
            entity.Components.Add(new GoldsrcTransformLinkComponent(
                new ClEntityTransformBinding(visible.NativeEntity),
                TransformAuthority.Goldsrc));
        }
        else
        {
            entity.Components.Add(new GoldsrcTransformLinkComponent());
        }

        var behavior = new HalfLifeBehavior
        {
            ContentManager = ContentManager,
            IsPlayer = visible.IsPlayer,
            ModelKey = GetPhysicsModelKey(visible.NativeEntity),
        };

        if (visible.NativeEntity is not null)
            entity.Components.Add(new ClEntityComponent(visible.NativeEntity));

        entity.Components.Add(behavior);

        return entity;
    }

    private Entity CreateWorldspawnEntity()
    {
        var entity = new Entity("Entity@0(\"worldspawn\")");
        entity.Components.Add(new GoldsrcTransformLinkComponent());

        // worldspawn uses StaticComponent + MeshCollider, no PhysicsController/HalfLifeBehavior.
        // The physics prefab is loaded by ContentManager and instantiated here.
        if (ContentManager is not null)
        {
            var prefab = ContentManager.Load<Prefab>(WorldspawnKey);
            if (prefab is not null)
            {
                foreach (var bone in prefab.Instantiate())
                    bone.Transform.Parent = entity.Transform;
            }
        }

        return entity;
    }

    /// <summary>
    /// Physics resource key of an entity, or null when this part of the framework cannot provide
    /// one. Brush models are keyed by model index; studio models are resolved by name elsewhere.
    /// </summary>
    private static string? GetPhysicsModelKey(cl_entity_t* nativeEntity)
    {
        if (nativeEntity == null || nativeEntity->model == null)
            return null;

        if (nativeEntity->model->type != modtype_t.mod_brush)
            return null;

        int modelIndex = nativeEntity->curstate.modelindex;
        return modelIndex >= 1 ? $"*{modelIndex}" : null;
    }

    private static string DescribeEntity(int entindex, cl_entity_t* nativeEntity)
    {
        var modelName = ReadModelName(nativeEntity);
        return modelName.Length == 0 ? $"Entity@{entindex}" : $"Entity@{entindex}(\"{modelName}\")";
    }

    private static string ReadModelName(cl_entity_t* nativeEntity)
    {
        if (nativeEntity == null || nativeEntity->model == null)
            return string.Empty;

        const int MaxModelNameLength = 64;
        Span<char> buffer = stackalloc char[MaxModelNameLength];
        int length = 0;

        while (length < MaxModelNameLength && (byte)nativeEntity->model->name[length] != 0)
        {
            buffer[length] = (char)(byte)nativeEntity->model->name[length];
            length++;
        }

        return new string(buffer[..length]);
    }

    /// <summary>Native entity seen this frame plus the metadata needed to build its Stride entity.</summary>
    private readonly struct VisibleEntity
    {
        public VisibleEntity(cl_entity_t* nativeEntity, bool isPlayer)
        {
            NativeEntity = nativeEntity;
            IsPlayer = isPlayer;
        }

        public cl_entity_t* NativeEntity { get; }

        public bool IsPlayer { get; }
    }
}
