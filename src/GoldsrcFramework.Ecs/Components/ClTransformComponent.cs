using GoldsrcFramework.Engine.Native;
using Stride.Engine;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace GoldsrcFramework.Ecs.Components;

/// <summary>
/// GoldSrc 风格 Transform：属性直接穿透读写 cl_entity_t.origin / angles。
/// 不使用 Stride 自带 TransformComponent。
/// </summary>
public unsafe sealed class ClTransformComponent : EntityComponent
{
    public ClTransformComponent()
    {
    }

    public ClTransformComponent(cl_entity_t* nativeEntity)
    {
        NativeEntity = nativeEntity;
    }

    public cl_entity_t* NativeEntity { get; set; }

    public GoldsrcVector3 Origin
    {
        get => NativeEntity->origin;
        set => NativeEntity->origin = value;
    }

    public GoldsrcVector3 Angles
    {
        get => NativeEntity->angles;
        set => NativeEntity->angles = value;
    }

    public StrideVector3 StrideOrigin
    {
        get => ToStride(Origin);
        set => Origin = ToGoldsrc(value);
    }

    public StrideVector3 StrideAngles
    {
        get => ToStride(Angles);
        set => Angles = ToGoldsrc(value);
    }

    private static StrideVector3 ToStride(GoldsrcVector3 value)
    {
        return new StrideVector3(value.X, value.Y, value.Z);
    }

    private static GoldsrcVector3 ToGoldsrc(StrideVector3 value)
    {
        return new GoldsrcVector3(value.X, value.Y, value.Z);
    }
}
