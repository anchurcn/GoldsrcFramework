using Stride.Engine;

namespace GoldsrcFramework.Ecs;

internal sealed class GoldsrcScriptProcessor : EntityProcessor<ScriptComponentBase>
{
    private GoldsrcScriptSystem scriptSystem = null!;

    public GoldsrcScriptProcessor()
    {
        Order = -100_000;
    }

    protected override void OnSystemAdd()
    {
        scriptSystem = Services.GetService<GoldsrcScriptSystem>()
            ?? throw new InvalidOperationException("GoldsrcScriptSystem is not registered.");
    }

    protected override void OnEntityComponentAdding(
        Entity entity,
        ScriptComponentBase component,
        ScriptComponentBase data)
    {
        scriptSystem.Add(component);
    }

    protected override void OnEntityComponentRemoved(
        Entity entity,
        ScriptComponentBase component,
        ScriptComponentBase data)
    {
        scriptSystem.Remove(component);
    }
}
