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
/// Worldspawn (entity 0) is handled specially: created once on first frame and never PVS-managed.
/// </summary>
public sealed class ClientSceneManagementSystem : SceneEntityLifecycleSystem
{
    private readonly GoldsrcClientGame game;
    private readonly Dictionary<int, Entity> activeEntities = [];
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
    /// Entity 0 (worldspawn) is ignored — it is created once and never PVS-managed.
    /// </summary>
    public void MarkEntityVisible(int entindex)
    {
        if (entindex == 0)
            return; // worldspawn handled separately
        thisFrameVisible.Add(entindex);
    }

    protected override void ProcessEntityLifecycle(GameTime gameTime)
    {
        // Create worldspawn once on first frame (after map data is available).
        if (!hasWorldspawn)
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
    }

    private Entity CreateEntity(int entindex)
    {
        var entity = new Entity($"Entity@{entindex}");
        entity.Components.Add(new GoldsrcTransformLinkComponent());

        var behavior = new HalfLifeBehavior
        {
            ContentManager = ContentManager
        };
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
            const string worldspawnKey = "*1"; // worldspawn is always model index 1
            if (ContentManager.IsExist(worldspawnKey))
            {
                var prefab = ContentManager.Load<Prefab>(worldspawnKey);
                if (prefab is not null)
                {
                    var bones = prefab.Instantiate();
                    foreach (var bone in bones)
                        bone.Transform.Parent = entity.Transform;
                }
            }
        }

        return entity;
    }
}
