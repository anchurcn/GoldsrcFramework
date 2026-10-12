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
/// renderer has no business knowing what a ragdoll is. These three questions are therefore answered
/// here, where the scene is: is this entity drawn from physics, what pose does it carry, and has a
/// corpse replaced it.
/// </para>
/// <para>
/// Two kinds of entity animate physically, and they carry their pose in different places. A ragdoll
/// corpse is a client entity of its own whose physics skeleton holds the pose it is drawn from; a
/// server entity is animated as usual and additionally drives a kinematic skeleton, whose simulated
/// bones - the addon bones - follow the simulation rather than the animation. The first is reached
/// through the ragdoll link and the second through the scene, and both end in the renderer's own
/// buffers.
/// </para>
/// </remarks>
public sealed partial class GoldsrcClientGame
{
    /// <summary>
    /// Whether <paramref name="entity"/> is drawn from a physics pose rather than from its animation.
    /// </summary>
    /// <remarks>
    /// An entity a corpse has replaced is not one of them: its skeleton has been handed to the corpse
    /// and disabled, so it drops out through the <see cref="PhysicsController.IsEnabled"/> check rather
    /// than through a rule of its own.
    /// </remarks>
    public unsafe bool IsPhysAnimating(cl_entity_t* entity)
    {
        if (entity == null)
            return false;

        // The first person view model is a client entity of its own that borrows the local player's
        // index and origin, and it is drawn through the same entry as every other model. It is not a
        // body: it holds a weapon sequence of its own, it has no skeleton of its own, and looking it
        // up by index would find the player's skeleton instead - so it is named and refused here,
        // before the index is trusted.
        if (entity == EngineApi.PStudio->GetViewEntity())
            return false;

        // A ragdoll corpse carries the pose on a physics skeleton of its own, reached through the
        // handle its client entity holds.
        if (RagdollHelper.IsRagdoll(entity))
            return true;

        // Everything else is a server entity whose skeleton is loaded and live.
        return PhysicsOf(entity) is { IsNullSkeleton: false, IsEnabled: true };
    }

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
    /// Writing into the renderer's buffers is the point: several kilobytes of bone matrices per
    /// entity, per frame, are not going to be allocated and copied for the privilege.
    /// </remarks>
    public unsafe bool TryFillPhysicsPose(cl_entity_t* entity, Matrix3x4* modelRoot, Matrix3x4* bones, int boneCapacity)
    {
        if (entity == null || modelRoot == null || bones == null || boneCapacity <= 0)
            return false;

        if (RagdollHelper.TryGetBehavior(entity, out _))
        {
            if (!RagdollHelper.TryFillPhysicsPose(entity, modelRoot, bones, boneCapacity))
                return false;
        }
        else
        {
            var behavior = BehaviorOf(entity);
            var physics = behavior?.PhysicsController;
            if (physics is not { IsEnabled: true, IsNullSkeleton: false })
                return false;

            // The animation pose is the base the physics pose is read back on top of: the simulated
            // bones overwrite it, and every bone without a rigid body keeps the pose it has relative
            // to its anchor in this very pose, so it follows the anchor the simulation moved. It is
            // the pose the script stage handed the skeleton this frame.
            var animated = behavior!.StudioPose;
            if (animated.IsEmpty)
                return false;

            var count = Math.Min(animated.Length, boneCapacity);
            animated[..count].CopyTo(new Span<Matrix3x4>(bones, count));

            // The simulated bones - addon bones among them - are authoritative in the simulation and
            // overwrite what was just copied.
            physics.SetupBones(new Span<Matrix3x4>(bones, count), animated[..count]);
        }

        // The bones are already in world space, so the model root carries no rotation of its own -
        // only where the entity is.
        *modelRoot = Matrix3x4.Identity;
        modelRoot->Origin = entity->curstate.origin;
        return true;
    }

    /// <summary>The physics controller of a server entity, or null when it has no ECS counterpart.</summary>
    private unsafe PhysicsController? PhysicsOf(cl_entity_t* entity) => BehaviorOf(entity)?.PhysicsController;

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
