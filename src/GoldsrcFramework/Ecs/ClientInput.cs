using GoldsrcFramework.Engine.Native;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// What the player is holding, as the engine's own command builder reported it.
/// </summary>
/// <remarks>
/// <para>
/// The bits come from the <c>usercmd_t</c> the legacy client library fills in inside
/// <c>CL_CreateMove</c>, and from nowhere else. Nothing else can be used: the <c>kbutton_t</c> the
/// library keeps for <c>+attack</c> is private to it, and the table <c>KB_Find</c> walks only ever
/// holds the three toggles the library registers for its own bookkeeping (<c>in_graph</c>,
/// <c>in_mlook</c>, <c>in_jlook</c>) - never <c>+attack</c>. Asking <c>KB_Find</c> for a game button
/// therefore always answers null, which is not the same thing as "the key is unbound".
/// </para>
/// <para>
/// Reading the library's command is also the reading the game itself acts on, so every binding, alias
/// and config that reaches the library has already been resolved and reduced to button bits -
/// including the case of two keys bound to the same <c>+command</c>.
/// </para>
/// <para>
/// One frame produces several commands and the library consumes its own edge bits on each of them, so
/// what is kept here is a level. Hold it against the previous frame to get a press or a release.
/// </para>
/// </remarks>
public sealed class ClientInput
{
    private ushort buttons;

    /// <summary>
    /// The buttons of the most recent command, as <see cref="InputButtons"/> flags. Zero until the
    /// engine has asked for a command - which is also what a client that is not in a game reports,
    /// because none is built then.
    /// </summary>
    public InputButtons Buttons => (InputButtons)buttons;

    /// <summary>Whether every bit of <paramref name="button"/> was down in the most recent command.</summary>
    public bool IsHeld(InputButtons button) => ((InputButtons)buttons & button) == button;

    /// <summary>The primary attack button, <c>+attack</c>.</summary>
    public bool AttackHeld => IsHeld(InputButtons.ATTACK);

    /// <summary>The secondary attack button, <c>+attack2</c>.</summary>
    public bool Attack2Held => IsHeld(InputButtons.ATTACK2);

    /// <summary>Records the buttons of a command the library has just built.</summary>
    internal void Observe(ushort commandButtons) => buttons = commandButtons;
}
