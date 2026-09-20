using System.Reflection;
using GoldsrcFramework.Ecs.Processors;

using Stride.Core;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// GoldSrc 客户端使用的轻量 Stride ECS 管理器。
/// 只手动注册 GoldSrc 侧 Processor，不启用 Scene/Game/渲染管线。
/// </summary>
public sealed class ClientEntityManager : EntityManager
{
    private static readonly MethodInfo InternalAddEntityMethod =
        typeof(EntityManager).GetMethod("InternalAddEntity", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(EntityManager).FullName, "InternalAddEntity");

    public ClientEntityManager(IServiceRegistry registry, bool registerDefaultProcessors = true)
        : base(registry)
    {
        ExecutionMode = Stride.Engine.Design.ExecutionMode.Runtime;

        if (registerDefaultProcessors)
        {
            RegisterGoldsrcProcessors();
        }
    }

    public static ClientEntityManager CreateDefault(bool registerDefaultProcessors = true)
    {
        return new ClientEntityManager(new ServiceRegistry(), registerDefaultProcessors);
    }

    public void RegisterGoldsrcProcessors()
    {
        Processors.Add(new ClScriptProcessor());
        Processors.Add(new ClTransformProcessor());
        Processors.Add(new ClModelProcessor());
    }

    /// <summary>
    /// Stride.Engine.EntityManager 的 Add 是 internal；这里通过反射打开入口。
    /// 暂不做生命周期和线程防护，调用方按 GoldSrc 主线程 tick 使用。
    /// </summary>
    public void AddEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        InternalAddEntityMethod.Invoke(this, [entity]);
    }

    public void RemoveEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Remove(entity);
    }
}
