using Stride.Core;
using Stride.Engine;
using Stride.Games;


namespace GoldsrcFramework.Ecs.Scripting;

/// <summary>
/// GoldSrc 客户端轻量脚本基类，用于替代 Stride ScriptComponent / SyncScript。
/// </summary>
public abstract class ClScript : EntityComponent
{
    public IServiceRegistry? Services => Entity?.EntityManager?.Services;

    public virtual void Start()
    {
    }

    public virtual void Update(GameTime time)
    {
    }
}
