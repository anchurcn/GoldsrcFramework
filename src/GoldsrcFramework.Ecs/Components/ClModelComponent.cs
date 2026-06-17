using GoldsrcFramework.Engine.Native;
using Stride.Engine;

namespace GoldsrcFramework.Ecs.Components;

/// <summary>
/// GoldSrc 模型状态组件，直接读写 cl_entity_t.model 和 curstate 模型动画字段。
/// </summary>
public unsafe sealed class ClModelComponent : EntityComponent
{
    public ClModelComponent()
    {
    }

    public ClModelComponent(cl_entity_t* nativeEntity)
    {
        NativeEntity = nativeEntity;
    }

    public cl_entity_t* NativeEntity { get; set; }

    public model_t* Model
    {
        get => NativeEntity->model;
        set => NativeEntity->model = value;
    }

    public int ModelIndex
    {
        get => NativeEntity->curstate.modelindex;
        set => NativeEntity->curstate.modelindex = value;
    }

    public int Sequence
    {
        get => NativeEntity->curstate.sequence;
        set => NativeEntity->curstate.sequence = value;
    }

    public float Frame
    {
        get => NativeEntity->curstate.frame;
        set => NativeEntity->curstate.frame = value;
    }

    public int Body
    {
        get => NativeEntity->curstate.body;
        set => NativeEntity->curstate.body = value;
    }
}
