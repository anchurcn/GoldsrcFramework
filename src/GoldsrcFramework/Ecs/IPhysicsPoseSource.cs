using GoldsrcFramework.LinearMath;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// A client entity that carries a physics pose the studio renderer can draw instead of the entity's
/// animation.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds of entity answer to this, and they are peers here on purpose. A ragdoll corpse owns a
/// physics skeleton of its own and is drawn from it; a server entity is animated as usual and
/// additionally drives a kinematic skeleton, whose simulated bones - the addon bones - follow the
/// simulation rather than the animation. Which of the two a client entity is, is decided entirely by
/// resolving it - see <c>GoldsrcClientGame.ResolveSource</c> - so the renderer never has to know.
/// </para>
/// <para>
/// A source only names its data, though; the one place that turns it into a pose is
/// <c>GoldsrcClientGame.TryFillPhysicsPose</c>. That is why everything here is a plain read, and why
/// the two kinds have nothing left to differ in but which pose and which controller they name.
/// </para>
/// <para>
/// Both questions are asked from inside the engine's draw loop, where there is no service registry
/// and nothing to allocate. That is why the pose is exposed as a span over the source's own storage
/// rather than returned as an array: several kilobytes of bone matrices per entity, per frame, are
/// not going to be copied for the privilege.
/// </para>
/// </remarks>
internal interface IPhysicsPoseSource
{
    /// <summary>
    /// The controller holding the skeleton this pose is read from, or null when there is nothing to
    /// read right now - which is also how a source says "draw me from my animation".
    /// </summary>
    /// <remarks>
    /// Null covers a skeleton that is not loaded, one that is no longer enabled, and an entity the
    /// animation is not driving this frame.
    /// </remarks>
    PhysicsController? Physics { get; }

    /// <summary>
    /// The pose the bones without a rigid body hold this frame - the <c>basePose</c> of
    /// <see cref="PhysicsController.SetupBones"/> - or an empty span when there is none.
    /// </summary>
    ReadOnlySpan<Matrix3x4> BasePose { get; }

    /// <summary>
    /// The model root to draw the pose under: where the model's own origin sits in the world.
    /// </summary>
    Matrix3x4 ModelRoot { get; }
}
