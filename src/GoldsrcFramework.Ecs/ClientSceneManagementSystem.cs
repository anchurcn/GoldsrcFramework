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
/// </summary>
public sealed class ClientSceneManagementSystem : SceneEntityLifecycleSystem
{
    private readonly GoldsrcClientGame game;
    private readonly Dictionary<int, Entity> activeEntities = [];
    private HashSet<int> thisFrameVisible = [];
    private HashSet<int> lastFrameVisible = [];
    private readonly List<Entity> pendingAdd = [];
    private readonly List<Entity> pendingRemove = [];

    public ClientSceneManagementSystem(IServiceRegistry services, GoldsrcClientGame game, GoldsrcSceneSystem sceneSystem)
        : base(services, sceneSystem)
    {
        this.game = game ?? throw new ArgumentNullException(nameof(game));
    }

    /// <summary>
    /// Returns the entity associated with an entindex, or null if none exists.
    /// </summary>
    public Entity? GetEntity(int entindex)
    {
        activeEntities.TryGetValue(entindex, out var entity);
        return entity;
    }

    /// <summary>
    /// Marks an entity as visible this frame. Called from <c>HUD_AddEntity</c>.
    /// </summary>
    public void MarkEntityVisible(int entindex)
    {
        thisFrameVisible.Add(entindex);
    }

    protected override void ProcessEntityLifecycle(GameTime gameTime)
    {
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

        // Collect entities that left this frame (visible last frame, not visible now)
        foreach (var entindex in lastFrameVisible)
        {
            if (thisFrameVisible.Contains(entindex))
                continue;

            if (activeEntities.TryGetValue(entindex, out var entity))
                pendingRemove.Add(entity);
        }

        // Apply removals first (EntityRemoved event → OnExit), then adds (EntityAdded event → OnEnter)
        foreach (var entity in pendingRemove)
            game.Remove(entity);

        foreach (var entity in pendingAdd)
            game.Add(entity);

        pendingAdd.Clear();
        pendingRemove.Clear();

        // Swap for next frame: lastFrameVisible ← thisFrameVisible, thisFrameVisible ← cleared
        (lastFrameVisible, thisFrameVisible) = (thisFrameVisible, lastFrameVisible);
        thisFrameVisible.Clear();
    }

    private static Entity CreateEntity(int entindex)
    {
        var entity = new Entity($"Entity@{entindex}");
        entity.Components.Add(new GoldsrcTransformLinkComponent());
        entity.Components.Add(new HalfLifeBehavior());
        return entity;
    }
}