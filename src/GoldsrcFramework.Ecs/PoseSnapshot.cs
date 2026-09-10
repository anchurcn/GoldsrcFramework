using GoldsrcFramework.LinearMath;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// An immutable snapshot of a studio skeleton pose, stored as parent-relative transforms indexed by
/// studio bone index.
/// </summary>
/// <remarks>
/// Each entry is the bone's transform relative to its <b>anchor</b>: the nearest ancestor that is a
/// simulated bone (or, when there is none, the skeleton's pivot bone). Storing anchor-relative
/// transforms lets a snapshot be replayed on another skeleton instance - a ragdoll, for example -
/// without any knowledge of the bone hierarchy ordering: every anchor is a simulated bone, so its
/// world transform is always available from the physics simulation.
/// <para>
/// Entries of simulated bones are <see cref="Matrix3x4.Identity"/> and are never read, because a
/// simulated bone's world transform always comes from the simulation.
/// </para>
/// <para>
/// The typical use is carrying the pose of a character that just died over to its ragdoll, so that
/// bones without a rigid body (fingers, for instance) keep the pose they had at the moment of death
/// instead of snapping back to a bind pose.
/// </para>
/// </remarks>
public sealed class PoseSnapshot
{
    private readonly Matrix3x4[] localTransforms;

    internal PoseSnapshot(Matrix3x4[] localTransforms)
    {
        this.localTransforms = localTransforms;
    }

    /// <summary>Number of studio bones in the snapshot.</summary>
    public int BoneCount => localTransforms.Length;

    /// <summary>Anchor-relative transforms indexed by studio bone index.</summary>
    public ReadOnlySpan<Matrix3x4> LocalTransforms => localTransforms;

    internal Matrix3x4[] LocalTransformsArray => localTransforms;
}
