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
/// <para>
/// The mouse is kept here too, in the two forms the buttons cannot express: the movement it asked for
/// (<see cref="ConsumeMouseMove"/>), which the library has already turned into a view change, and the
/// claim that the movement belongs to the runtime rather than to the view
/// (<see cref="MouseCaptured"/>) - which is what lets a control aim something other than the
/// crosshair.
/// </para>
/// </remarks>
public sealed class ClientInput
{
    /// <summary>The engine's key number for the wheel scrolled up, from <c>keydefs.h</c> (<c>K_MWHEELUP</c>).</summary>
    private const int WheelUp = 240;

    /// <summary>The engine's key number for the wheel scrolled down, from <c>keydefs.h</c> (<c>K_MWHEELDOWN</c>).</summary>
    private const int WheelDown = 239;

    private ushort buttons;
    private int wheelSteps;
    private float mousePitch;
    private float mouseYaw;

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

    /// <summary>The use button, <c>+use</c> - the E key on a stock config.</summary>
    public bool UseHeld => IsHeld(InputButtons.USE);

    /// <summary>
    /// Whether the mouse belongs to the runtime rather than to the view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While this is set, the turn the client library makes to the view inside <c>CL_CreateMove</c> is
    /// undone before the frame is drawn: the view - and the angles the server is told about - stay
    /// where they were, and the movement is left for the runtime to spend
    /// (<see cref="ConsumeMouseMove"/>). Nothing is switched off at the source, so the mouse never
    /// stops being read, and no sensitivity, filtering or pitch clamp has to be reproduced to get the
    /// movement back.
    /// </para>
    /// <para>
    /// It describes a hold rather than a tap, so it stays set for as long as the hold lasts: the owner
    /// states it every frame it wants it and drops it in the frame it does not, rather than setting it
    /// once. Nothing else may leave it set - a claim left standing is a player who cannot look around.
    /// </para>
    /// <para>
    /// What is read when the next command is built is the previous tick's word, because the command is
    /// built ahead of the frame it belongs to: the mouse is taken one frame after the hold starts, and
    /// given back one frame after it ends.
    /// </para>
    /// </remarks>
    public bool MouseCaptured { get; set; }

    /// <summary>
    /// Records what the client library did to the view from the mouse when it built a command.
    /// </summary>
    /// <remarks>
    /// See <see cref="ConsumeMouseMove"/> for what the numbers are. Summed rather than replaced: a
    /// frame builds several commands and the movement is shared across them.
    /// </remarks>
    internal void ObserveMouseMove(float pitch, float yaw)
    {
        mousePitch += pitch;
        mouseYaw += yaw;
    }

    /// <summary>
    /// Reads the mouse movement seen since the last call, in view degrees, and clears it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the movement the engine's own look was about to apply, measured where it happens: the
    /// runtime reads the view angles on both sides of the library's <c>CL_CreateMove</c> and hands the
    /// difference here. Sensitivity, mouse filtering and the pitch clamp have already been applied,
    /// so a control that spends it on something else turns at the speed the player's own settings say.
    /// </para>
    /// <para>
    /// In the engine's own axes: an increase in pitch means the view went down, one in yaw means it
    /// turned anticlockwise seen from above.
    /// </para>
    /// <para>
    /// A frame builds several commands, and only the first of them finds a mouse displacement - the
    /// library recentres the pointer - so what is summed over a frame is that frame's movement rather
    /// than a multiple of it. Callers drain it every frame whether or not they have a use for it, so
    /// that a sweep made between two uses is forgotten instead of banked up.
    /// </para>
    /// </remarks>
    internal void ConsumeMouseMove(out float pitch, out float yaw)
    {
        pitch = mousePitch;
        yaw = mouseYaw;

        mousePitch = 0f;
        mouseYaw = 0f;
    }

    /// <summary>Records the buttons of a command the library has just built.</summary>
    internal void Observe(ushort commandButtons) => buttons = commandButtons;

    /// <summary>
    /// Records a key the engine reported to <c>HUD_Key_Event</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wheel is the one control no command can carry: <c>usercmd_t</c> has no field for it, and
    /// the library turns it into the <c>invnext</c>/<c>invprev</c> commands before any command
    /// exists. <c>HUD_Key_Event</c> is the only place the engine names it, and it names it by key
    /// number.
    /// </para>
    /// <para>
    /// A notch is counted rather than held, because a wheel is an edge by nature: what the caller
    /// wants to know is how far it was turned, not whether it is still down. Read the count with
    /// <see cref="ConsumeWheel"/>, which clears it.
    /// </para>
    /// <para>
    /// The answer says whether the key was a wheel notch, which is the caller's cue to keep it away
    /// from the engine: <c>HUD_Key_Event</c> answering "handled" is what stops the engine from running
    /// the binding the notch would otherwise fire. Nothing here decides that - the caller does, since
    /// only the caller knows whether it has a use for the wheel at that moment.
    /// </para>
    /// </remarks>
    /// <param name="down">Non-zero for a press, zero for a release.</param>
    /// <param name="keynum">The engine's key number.</param>
    /// <returns>True when the key was a wheel notch, so the caller may want to keep it.</returns>
    internal bool ObserveKeyEvent(int down, int keynum)
    {
        if (down == 0)
            return false;

        switch (keynum)
        {
            case WheelUp:
                wheelSteps++;
                return true;

            case WheelDown:
                wheelSteps--;
                return true;

            default:
                return false;
        }
    }

    /// <summary>Reads the wheel notches seen since the last call and clears the count.</summary>
    internal int ConsumeWheel()
    {
        var steps = wheelSteps;
        wheelSteps = 0;
        return steps;
    }
}
