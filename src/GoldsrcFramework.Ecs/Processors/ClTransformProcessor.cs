using GoldsrcFramework.Ecs.Components;
using Stride.Engine;
using Stride.Games;


namespace GoldsrcFramework.Ecs.Processors;

/// <summary>
/// GoldSrc Transform 桥接 Processor。
/// 默认不做任何变换逻辑，只提供按帧遍历 ClTransformComponent 的钩子。
/// 游戏逻辑可继承或订阅 Tick 编写 origin/angles 回写逻辑。
/// </summary>
public class ClTransformProcessor : EntityProcessor<ClTransformComponent>
{
    public event Action<ClTransformComponent, GameTime>? Tick;

    public override void Update(GameTime time)
    {
        if (Tick is null)
        {
            return;
        }

        foreach (var transform in ComponentDatas.Keys)
        {
            Tick(transform, time);
        }
    }
}
