using Stride.Core.Mathematics;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Adapts a native GoldSrc entity transform to the shared Stride synchronization module.
/// </summary>
public interface IGoldsrcTransformBinding
{
    bool TryRead(out Vector3 origin, out Vector3 angles);

    bool TryWrite(in Vector3 origin, in Vector3 angles);
}