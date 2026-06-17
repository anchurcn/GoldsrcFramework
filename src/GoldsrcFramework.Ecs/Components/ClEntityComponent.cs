using GoldsrcFramework.Engine.Native;
using Stride.Engine;

namespace GoldsrcFramework.Ecs.Components;

/// <summary>
/// 持有 GoldSrc 客户端实体指针的基础组件。
/// 当前约定：cl_entity_t* 在组件存活期间始终有效。
/// </summary>
public unsafe sealed class ClEntityComponent : EntityComponent
{
    public ClEntityComponent()
    {
    }

    public ClEntityComponent(cl_entity_t* nativeEntity)
    {
        NativeEntity = nativeEntity;
    }

    public cl_entity_t* NativeEntity { get; set; }

    public bool HasNativeEntity => NativeEntity != null;

    public int Index => NativeEntity != null ? NativeEntity->index : -1;
}
