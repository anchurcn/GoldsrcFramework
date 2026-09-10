using Stride.Core;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Starts and updates synchronous GoldSrc scripts.
/// </summary>
/// <remarks>
/// Runs after <see cref="GoldsrcSceneSystem"/> (-100) and before the Bepu
/// <c>PhysicsGameSystem</c> (-49) so that whatever scripts write into the physics
/// skeleton takes effect in the same physics step.
/// </remarks>
public sealed class GoldsrcScriptSystem : GameSystemBase
{
    /// <summary>Runs after <see cref="GoldsrcSceneSystem"/> and before the physics step.</summary>
    public const int DefaultUpdateOrder = -90;

    private readonly HashSet<ScriptComponentBase> scripts = [];
    private readonly HashSet<ScriptComponentBase> startedScripts = [];
    private readonly HashSet<ScriptComponentBase> pendingAdditions = [];
    private readonly HashSet<ScriptComponentBase> pendingRemovals = [];
    private bool updating;

    public GoldsrcScriptSystem(IServiceRegistry services)
        : base(services)
    {
        Enabled = true;
        UpdateOrder = DefaultUpdateOrder;
    }

    internal void Add(ScriptComponentBase script)
    {
        if (updating)
        {
            pendingRemovals.Remove(script);
            pendingAdditions.Add(script);
            return;
        }

        scripts.Add(script);
    }

    internal void Remove(ScriptComponentBase script)
    {
        if (updating)
        {
            pendingAdditions.Remove(script);
            pendingRemovals.Add(script);
            return;
        }

        scripts.Remove(script);
        startedScripts.Remove(script);
    }

    public override void Update(GameTime gameTime)
    {
        updating = true;
        try
        {
            foreach (var script in scripts)
            {
                if (startedScripts.Add(script))
                    script.Start();

                script.Update(gameTime);
            }
        }
        finally
        {
            updating = false;
            ApplyPendingChanges();
        }
    }

    private void ApplyPendingChanges()
    {
        foreach (var script in pendingRemovals)
        {
            scripts.Remove(script);
            startedScripts.Remove(script);
        }

        foreach (var script in pendingAdditions)
            scripts.Add(script);

        pendingRemovals.Clear();
        pendingAdditions.Clear();
    }
}
