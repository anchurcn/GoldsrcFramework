using GoldsrcFramework.Ecs;
using GoldsrcFramework.Engine.Native;
using Stride.Core.Mathematics;

namespace GoldsrcFramework.Physics;

internal sealed unsafe class ClEntityTransformBinding : IGoldsrcTransformBinding
{
    private cl_entity_t* clEntity;

    public ClEntityTransformBinding(cl_entity_t* clEntity)
    {
        this.clEntity = clEntity;
    }

    public bool TryRead(out Vector3 origin, out Vector3 angles)
    {
        if (clEntity == null)
        {
            origin = default;
            angles = default;
            return false;
        }

        origin = ToStride(clEntity->origin);
        angles = ToStride(clEntity->angles);
        return true;
    }

    public bool TryWrite(in Vector3 origin, in Vector3 angles)
    {
        if (clEntity == null)
            return false;

        clEntity->origin = ToGoldsrc(origin);
        clEntity->angles = ToGoldsrc(angles);
        return true;
    }

    public void Invalidate()
    {
        clEntity = null;
    }

    private static Vector3 ToStride(GoldsrcFramework.LinearMath.Vector3 value)
    {
        return new Vector3(value.X, value.Y, value.Z);
    }

    private static GoldsrcFramework.LinearMath.Vector3 ToGoldsrc(Vector3 value)
    {
        return new GoldsrcFramework.LinearMath.Vector3(value.X, value.Y, value.Z);
    }
}