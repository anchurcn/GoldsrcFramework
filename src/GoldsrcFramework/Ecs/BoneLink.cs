using Stride.Core;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Transient component attached to a physics bone entity, describing the mapping between this
/// simulated bone and the corresponding studio bone of the model.
/// <see cref="PhysicsController.LoadSkeleton"/> reads this component once to build its internal
/// index; afterwards it is no longer accessed.
/// </summary>
/// <remarks>
/// It still carries <see cref="DataContractAttribute"/> because it lives inside a physics
/// <see cref="Prefab"/>: <c>Prefab.Instantiate()</c> deep-clones the bone entities through Stride's
/// serialization, which refuses to copy a component type without a serializer.
/// </remarks>
[DataContract]
public sealed class BoneLink : EntityComponent
{
    /// <summary>
    /// Index of the corresponding studio bone. Only a subset of the studio bones - the simulated
    /// ones - carry a rigid body and therefore a physics bone entity. Brush models have a single
    /// implicit root bone, always index 0.
    /// </summary>
    public int StudioBoneIndex { get; set; }

    /// <summary>
    /// Whether this is a jiggle / addon bone that is driven by physics rather than animation.
    /// Maps to gsphysics <c>UserRigidbodyType::Addon</c>.
    /// </summary>
    public bool IsAddon { get; set; }
}
