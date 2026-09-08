using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Links a root Stride entity to a GoldSrc entity and declares pose ownership.
/// </summary>
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
