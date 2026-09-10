using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Transient component attached to a physics bone entity, describing the mapping
/// between this physics bone and the corresponding GoldSrc animation bone.
/// <see cref="PhysicsController.LoadSkeleton"/> reads this component once to build
/// its internal index; afterwards it is no longer accessed.
/// </summary>
public sealed class BoneLink : EntityComponent
{
    /// <summary>
    /// The index of the corresponding GoldSrc studio model bone.
    /// Brush models always use 0.
    /// </summary>
    public int BoneIndex { get; set; }

    /// <summary>
    /// Whether this is a jiggle / addon bone that is driven by physics rather than animation.
    /// Maps to gsphysics <c>UserRigidbodyType::Addon</c>.
    /// </summary>
    public bool IsAddon { get; set; }
}
