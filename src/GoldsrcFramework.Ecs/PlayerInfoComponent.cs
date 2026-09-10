using NativeInterop;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Holds player-entity information. Data is mocked for now; later it will be
/// sourced from native <c>player_info_t</c>.
/// </summary>
public sealed class PlayerInfoComponent : EntityComponent
{
    /// <summary>
    /// The player's model name (e.g. "gordon"). Used for model validation
    /// because player models are loaded by name, not by model index.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
}
