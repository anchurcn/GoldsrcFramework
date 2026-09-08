using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Minimal synchronous script for the GoldSrc-hosted Stride runtime.
/// </summary>
[AllowMultipleComponents]
public abstract class ScriptComponentBase : EntityComponent
{
    public virtual void Start()
    {
    }

    public virtual void Update(GameTime gameTime)
    {
    }
}
