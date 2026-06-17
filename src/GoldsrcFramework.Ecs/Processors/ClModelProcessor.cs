using GoldsrcFramework.Ecs.Components;
using Stride.Engine;
using Stride.Games;


namespace GoldsrcFramework.Ecs.Processors;

/// <summary>
/// GoldSrc 模型状态桥接 Processor。
/// 默认不改写模型状态，保留 Tick 钩子给业务侧扩展。
/// </summary>
public class ClModelProcessor : EntityProcessor<ClModelComponent>
{
    public event Action<ClModelComponent, GameTime>? Tick;

    public override void Update(GameTime time)
    {
        if (Tick is null)
        {
            return;
        }

        foreach (var model in ComponentDatas.Keys)
        {
            Tick(model, time);
        }
    }
}
