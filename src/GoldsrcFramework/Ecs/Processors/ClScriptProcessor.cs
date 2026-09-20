using GoldsrcFramework.Ecs.Scripting;
using Stride.Engine;
using Stride.Games;


namespace GoldsrcFramework.Ecs.Processors;

/// <summary>
/// 驱动 ClScript.Start / Update 的轻量 Processor。
/// </summary>
public sealed class ClScriptProcessor : EntityProcessor<ClScript>
{
    private readonly HashSet<ClScript> startedScripts = [];

    public override void Update(GameTime time)
    {
        foreach (var script in ComponentDatas.Keys)
        {
            if (startedScripts.Add(script))
            {
                script.Start();
            }

            script.Update(time);
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, ClScript component, ClScript data)
    {
        startedScripts.Remove(component);
    }
}
