using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// The Stride side of the studio model renderer: what the renderer asks the runtime while the engine
/// is drawing an entity.
/// </summary>
/// <remarks>
/// <para>
/// A physics-driven renderer draws a pose that was produced somewhere else, and it runs inside the
/// engine's own draw loop - there is no service registry there to resolve a scene from, and the
/// renderer has no business knowing what a ragdoll is. What it asks is therefore answered here, where
/// the scene is.
/// </para>
/// <para>
/// There is one question - which pose source, if any, this client entity is - and everything the
/// renderer asks follows from its answer. Two kinds of entity answer to it, and they are found in
/// different places: a ragdoll corpse is reached through the ragdoll link its client entity carries,
/// a server entity through the scene. What each of them supplies is its own, behind
/// <see cref="IPhysicsPoseSource"/>; turning it into a pose is not.
/// </para>
/// </remarks>
public sealed partial class GoldsrcClientGame
{
    /// <summary>
    /// Whether <paramref name="entity"/> is drawn from a physics pose rather than from its animation.
    /// </summary>
    /// <remarks>
    /// An entity a corpse has replaced is not one of them: its skeleton has been handed to the corpse
    /// and disabled, so it drops out for want of a source rather than through a rule of its own.
    /// </remarks>
    public unsafe bool IsPhysAnimating(cl_entity_t* entity) => ResolveSource(entity) is { Physics: not null };

    /// <summary>
    /// Whether <paramref name="entity"/> has been replaced by a corpse and must not be drawn any more.
    /// </summary>
    /// <remarks>
    /// The engine would otherwise draw the entity's death animation standing next to its body. The
    /// state is read from the scene, so nothing has to be cleared when an entity respawns.
    /// </remarks>
    public unsafe bool IsReplacedByCorpse(cl_entity_t* entity) => RagdollHelper.IsReplacedByCorpse(entity);

    /// <summary>
    /// Fills the renderer's own buffers with the physics pose of <paramref name="entity"/>.
    /// </summary>
    /// <param name="entity">The client entity the engine is asking to draw.</param>
    /// <param name="modelRoot">The renderer's rotation matrix, to receive the model root pose.</param>
    /// <param name="bones">The renderer's bone buffer, to receive the pose in studio bone order.</param>
    /// <param name="boneCapacity">How many matrices <paramref name="bones"/> can hold.</param>
    /// <returns>
    /// True when a pose was written. False when the entity has none to give, which leaves the caller
    /// to draw it the ordinary way rather than blank it out.
    /// </returns>
    /// <remarks>
    /// The one place a pose is produced, whichever kind of entity it belongs to: the source names a
    /// skeleton and the pose its bones without a rigid body hold, and the controller turns the two
    /// into the full pose. Writing into the renderer's own buffers is the point - several kilobytes of
    /// bone matrices per entity, per frame, are not going to be allocated and copied for the privilege.
    /// </remarks>
    public unsafe bool TryFillPhysicsPose(cl_entity_t* entity, Matrix3x4* modelRoot, Matrix3x4* bones, int boneCapacity)
    {
        if (modelRoot == null || bones == null || boneCapacity <= 0)
            return false;

        var source = ResolveSource(entity);
        if (source?.Physics is not { } physics)
            return false;

        var basePose = source.BasePose;
        if (basePose.IsEmpty)
            return false;

        var count = Math.Min(basePose.Length, boneCapacity);
        physics.SetupBones(new Span<Matrix3x4>(bones, count), basePose[..count]);
        *modelRoot = source.ModelRoot;

        return true;
    }

    /// <summary>
    /// The physics pose source behind a client entity, or null when it has none.
    /// </summary>
    /// <remarks>
    /// The one place that decides which entities are drawn from physics, and the only one that has to
    /// know how either kind is found. Everything above it asks through this answer alone.
    /// </remarks>
    private unsafe IPhysicsPoseSource? ResolveSource(cl_entity_t* entity)
    {
        if (entity == null)
            return null;

        // The first person view model is a client entity of its own that borrows the local player's
        // index and origin, and it is drawn through the same entry as every other model. It is not a
        // body: it holds a weapon sequence of its own, it has no skeleton of its own, and looking it
        // up by index would find the player's skeleton instead - so it is named and refused here,
        // before the index is trusted.
        if (entity == EngineApi.PStudio->GetViewEntity())
            return null;

        // A corpse carries its skeleton through the handle its client entity holds.
        if (RagdollHelper.TryGetBehavior(entity, out var ragdoll) && ragdoll is not null)
            return ragdoll;

        return BehaviorOf(entity);
    }

    /// <summary>
    /// The behavior behind a client entity, or null when the engine keeps it outside its entity list.
    /// </summary>
    /// <remarks>
    /// A temp entity the client owns - a corpse, for one - has no index to look up, so it has no
    /// counterpart here; the ECS only tracks the entities the engine's server owns.
    /// </remarks>
    private unsafe HalfLifeBehavior? BehaviorOf(cl_entity_t* entity)
        => entity == null || entity->index <= 0
            ? null
            : SceneManagement.GetEntity(entity->index)?.Get<HalfLifeBehavior>();
}
