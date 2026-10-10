using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Links a root Stride entity to a GoldSrc entity and declares pose ownership.
/// </summary>
/// <remarks>
/// <see cref="GoldsrcTransformSyncProcessor"/> is declared here as the component's default processor, so
/// the scene's entity manager creates it on demand and keeps its set of links in step - no matter when a
/// link is attached, including to an entity that is already in the scene.
/// </remarks>
[DefaultEntityComponentProcessor(typeof(GoldsrcTransformSyncProcessor), ExecutionMode = ExecutionMode.Runtime)]
public sealed class GoldsrcTransformLinkComponent : EntityComponent
{
    public GoldsrcTransformLinkComponent()
        : this(NullTransformBinding.Instance, TransformAuthority.None)
    {
    }

    public GoldsrcTransformLinkComponent(
        IGoldsrcTransformBinding binding,
        TransformAuthority authority)
    {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Authority = authority;
    }

    [DataMemberIgnore]
    public IGoldsrcTransformBinding Binding { get; }

    public TransformAuthority Authority { get; set; }

    internal bool HasLastAngles { get; set; }

    internal Stride.Core.Mathematics.Vector3 LastAngles { get; set; }

    private sealed class NullTransformBinding : IGoldsrcTransformBinding
    {
        public static NullTransformBinding Instance { get; } = new();

        public bool TryRead(out Vector3 origin, out Vector3 angles)
        {
            origin = default;
            angles = default;
            return false;
        }

        public bool TryWrite(in Vector3 origin, in Vector3 angles) => false;
    }
}
