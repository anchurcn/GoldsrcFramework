using GoldsrcFramework.Ecs.Diagnostics;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.Graphics;
using NativeInterop;
using Stride.Engine;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// The Stride side of the client export functions: every place <see cref="FrameworkClientExports"/>
/// has to touch the ECS runtime is one method here, named after the export function that triggers it.
/// </summary>
/// <remarks>
/// <para>
/// The export layer owns the engine callbacks and forwards to the legacy client DLL. Everything that
/// concerns the runtime - the Stride scene, the console variables and command the runtime offers, the
/// engine state a callback needs - belongs to the game and is reached through these methods only. The
/// export no longer walks <c>game.SceneSystem</c> / <c>game.SceneManagement</c> into a system, and
/// what crosses the seam is an intent (<c>HUD_AddEntity</c>, <c>HUD_TempEntUpdate</c>, ...) rather
/// than a system call.
/// </para>
/// <para>
/// The method names are deliberately the export function names (<c>HUD_Init</c>,
/// <c>HUD_AddEntity</c>, ...) rather than the names of the systems they drive. The name answers "what
/// does the client tell the runtime at this point", which is what makes the handoffs readable in one
/// place; naming them after the systems would only move the coupling into the name.
/// </para>
/// <para>
/// One callback is composed the other way round. The library runs first everywhere else, but a call
/// that has to be bracketed - <c>CL_CreateMove</c>, because the mouse only becomes a turn of the view
/// inside it - can only be bracketed by its caller: there the runtime makes the library call itself,
/// and the export is left with nothing but the handoff. What comes with it is still the same seam -
/// an intent and the engine state the runtime looks up on its own.
/// </para>
/// <para>
/// This is the client half of the host. The server will grow its own set of equivalently named methods
/// over the same runtime; the systems themselves stay shared.
/// </para>
/// </remarks>
public sealed partial class GoldsrcClientGame
{
    /// <summary>Console command that spawns a ragdoll, for looking at the ragdoll rendering.</summary>
    private const string RagdollCommandName = "gsf_ragdoll";

    /// <summary>Model the ragdoll command spawns.</summary>
    private const string RagdollModelPath = "models/scientist.mdl";

    /// <summary>
    /// The game the console command acts on.
    /// </summary>
    /// <remarks>
    /// The engine invokes console commands through function pointers and passes no user data, so a
    /// handler - which has to be static to be callable that way - can only reach its game from here.
    /// Set while the client is being initialised, which is also when the command is registered.
    /// </remarks>
    private static GoldsrcClientGame? commandTarget;

    /// <summary>
    /// The Stride side of <c>HUD_Init</c>: brings up everything the runtime needs before the first
    /// frame. The runtime itself (registry, systems, content service) is already up by the time the
    /// game exists.
    /// </summary>
    /// <remarks>
    /// The console surface the runtime owns is declared here too, one line per variable: it is the
    /// interface a player sees (<c>gsf_debugdraw</c>, <c>gsf_physdtest</c>, <c>phys_corpsestay</c>,
    /// <c>gsf_ragdoll</c>), so it reads best next to the features that answer to it.
    /// </remarks>
    public unsafe void HUD_Init()
    {
        // The client console surface: physics debug wireframe on, its lines depth tested, and how
        // long a corpse stays before it is released.
        EngineApi.PClient->RegisterVariable("gsf_debugdraw"u8.GetNCharPtr(), "1"u8.GetNCharPtr(), 0);
        EngineApi.PClient->RegisterVariable("gsf_physdtest"u8.GetNCharPtr(), "1"u8.GetNCharPtr(), 0);
        EngineApi.PClient->RegisterVariable("phys_corpsestay"u8.GetNCharPtr(), "30"u8.GetNCharPtr(), 0);

        EngineApi.PClient->AddCommand("gsf_ragdoll"u8.GetNCharPtr(), &SpawnRagdollCommand);
        commandTarget = this;

        // RagdollHelper is what the studio renderer asks about corpses - whether an entity is one, and
        // what pose it carries - so it is handed the game that owns the scene the corpses are found
        // in: the renderer is called straight from the engine's draw loop, which has no registry.
        RagdollHelper.AttachGame(this);

        // The physics gun attaches itself to the local player's entity when that entity appears;
        // this only hands it the game.
        PhysicsGunScript.Initialize(this);
    }

    /// <summary>
    /// The Stride side of <c>CL_CreateMove</c>: builds the command with the client library, then keeps
    /// what the library read out of the player's input - the buttons, and the movement the mouse asked
    /// for.
    /// </summary>
    /// <param name="frametime">Frame time in seconds, as the engine reports it.</param>
    /// <param name="cmd">The command the library fills in.</param>
    /// <param name="active">
    /// Zero while a menu is up, while a demo plays and before sign-on. The library leaves the command
    /// untouched then, so it says nothing about the buttons and the previous reading stands.
    /// </param>
    /// <remarks>
    /// <para>
    /// This is the one export the runtime does not simply follow. The library's own <c>CreateMove</c> is
    /// where the mouse becomes a turn of the view and nowhere else, so the call is bracketed - the view
    /// angles are read on each side of it - and the difference is what the look was about to spend.
    /// Nothing else can measure it: it is not state that can be looked up, and the library recentres the
    /// pointer as it goes. What comes back has already been through the player's sensitivity, mouse
    /// filtering and pitch clamp, which is what lets a control spend the same movement the crosshair
    /// would have taken.
    /// </para>
    /// <para>
    /// While the runtime has claimed the mouse (<see cref="ClientInput.MouseCaptured"/>) the turn is
    /// given back: the angles the frame is drawn with, and the ones the server is told about, return to
    /// where they were, and the movement is left for the runtime. The claim is read before the library
    /// runs - it is the previous tick's word, because a command is built ahead of the frame it belongs to
    /// - and the pitch clamp is widened for that same call (<see cref="PitchClamp"/>), which is the one
    /// thing the library would otherwise take away from a turn.
    /// </para>
    /// </remarks>
    public unsafe void CL_CreateMove(float frametime, usercmd_t* cmd, int active)
    {
        var engine = EngineApi.PClient;
        var measurable = active != 0
            && engine != null
            && engine->GetViewAngles != null
            && engine->SetViewAngles != null;

        var captured = measurable && Input.MouseCaptured;

        var before = default(GoldsrcVector3);
        if (measurable)
            engine->GetViewAngles((float*)&before);

        var clamp = captured ? PitchClamp.Widen(engine) : default;

        LegacyClientInterop.CL_CreateMove(frametime, cmd, active);

        clamp.Restore();

        if (active == 0)
            return;

        Input.Observe(cmd->buttons);

        if (measurable)
        {
            var after = default(GoldsrcVector3);
            engine->GetViewAngles((float*)&after);

            Input.ObserveMouseMove(after.X - before.X, after.Y - before.Y);
        }

        if (!captured)
            return;

        // The runtime asked for the mouse, so the turn the library just made is given back: the view
        // the frame is drawn with and the angles the server is told about both return to where they
        // were, and the movement stays with the runtime.
        engine->SetViewAngles((float*)&before);
        cmd->viewangles = before;
    }

    /// <summary>
    /// The view's pitch clamp, widened for the length of one call and put back afterwards.
    /// </summary>
    /// <remarks>
    /// The library clamps the view's pitch to <c>cl_pitchup</c>/<c>cl_pitchdown</c> (89 degrees either
    /// way) as it applies the mouse, to keep a player's aim sane. While the mouse is captured the aim
    /// does not move - the angles are given back where they started - so the clamp has nothing to
    /// protect and one thing to take away: it would swallow the vertical half of a turn made while
    /// looking down at the floor, and the body would stop answering in that direction. Both variables
    /// are restored the moment the call returns, so nothing outside can observe the widening.
    /// </remarks>
    private readonly unsafe struct PitchClamp
    {
        /// <summary>Pitch, in degrees, the view may reach while the runtime has the mouse.</summary>
        private const float Wide = 3600f;

        private readonly cvar_t* down;
        private readonly cvar_t* up;
        private readonly float savedDown;
        private readonly float savedUp;

        private PitchClamp(cvar_t* down, cvar_t* up, float savedDown, float savedUp)
        {
            this.down = down;
            this.up = up;
            this.savedDown = savedDown;
            this.savedUp = savedUp;
        }

        /// <summary>Widens the view's pitch clamp, remembering the values it had.</summary>
        public static PitchClamp Widen(ClientEngineFuncs* engine)
        {
            var down = LookupCvar(engine, "cl_pitchdown"u8);
            var up = LookupCvar(engine, "cl_pitchup"u8);

            var savedDown = down == null ? 0f : down->value;
            var savedUp = up == null ? 0f : up->value;

            if (down != null)
                down->value = Wide;

            if (up != null)
                up->value = Wide;

            return new PitchClamp(down, up, savedDown, savedUp);
        }

        /// <summary>Puts back the values the clamp had.</summary>
        public void Restore()
        {
            if (down != null)
                down->value = savedDown;

            if (up != null)
                up->value = savedUp;
        }
    }

    /// <summary>Looks a console variable up by name, or answers null when there is no such variable.</summary>
    private static unsafe cvar_t* LookupCvar(ClientEngineFuncs* engine, ReadOnlySpan<byte> name)
        => engine->GetCvarPointer == null ? null : engine->GetCvarPointer(name.GetNCharPtr());

    /// <summary>
    /// The Stride side of <c>HUD_Key_Event</c>: latches the keys a command cannot carry, which today
    /// is only the mouse wheel (see <see cref="ClientInput"/>), and answers whether the runtime wants
    /// the key for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wheel is latched either way - the answer is about the engine's binding, not about the
    /// reading. Answering true is what keeps <c>invnext</c>/<c>invprev</c> from being run, and the one
    /// thing the runtime asks for is a wheel that the physics gun has a use for: while it holds a body
    /// the notch is its distance control, and the weapon switch would take the crowbar away and end
    /// the hold with it. Everything else, and the wheel at every other moment, goes back untouched.
    /// </para>
    /// <para>
    /// The answer is based on the last frame's grip, which is what the engine's input pump can see -
    /// there is no reading of the buttons that is more recent. A notch scrolled in the same frame the
    /// grip starts is the one case where the two disagree, and it is not worth a second source of
    /// truth to close.
    /// </para>
    /// </remarks>
    /// <param name="eventcode">Non-zero for a press, zero for a release.</param>
    /// <param name="keynum">The engine's key number.</param>
    /// <returns>True when the engine must not run the key's binding.</returns>
    public bool HUD_Key_Event(int eventcode, int keynum)
    {
        var wheel = Input.ObserveKeyEvent(eventcode, keynum);

        return wheel && PhysicsGunScript.IsHolding;
    }

    /// <summary>
    /// The Stride side of <c>HUD_NewMap</c>: drops everything scoped to the map that just ended - its
    /// content resources, the scene bookkeeping and every entity - then warms the new map's brush
    /// prefabs so the first visible brush entity does not pay for triangulation.
    /// </summary>
    public void HUD_NewMap()
    {
        Reset();
        SceneManagement.ContentManager?.PreloadBrushModels();
    }

    /// <summary>
    /// The Stride side of <c>HUD_TempEntUpdate</c>: the frame tick. Mirrors the client gravity, lets
    /// the scene management system know the world model is present, then advances the runtime.
    /// </summary>
    /// <param name="frametime">Frame time in seconds, as the engine reports it.</param>
    /// <param name="clientTime">Client clock in seconds; the runtime's total time.</param>
    /// <param name="clGravity">Client gravity, mirrored into every Bepu simulation.</param>
    public unsafe void HUD_TempEntUpdate(double frametime, double clientTime, double clGravity)
    {
        SyncGravity(clGravity);

        // The world model (index 0) is what makes the map's static geometry appear. It only exists
        // once a map is loaded, so it is looked up per frame rather than cached.
        var worldSpawn = EngineApi.PClient->GetEntityByIndex(0);
        if (worldSpawn != null)
            SceneManagement.MarkWorldSpawnVisible(worldSpawn);

        // Cap the step: a stall (a map load, a hitching disk) must not become one giant physics step.
        Tick(TimeSpan.FromSeconds(clientTime), TimeSpan.FromSeconds(Math.Min(frametime, 0.1)));
    }

    /// <summary>
    /// The Stride side of <c>HUD_AddEntity</c>: marks the entity the engine is about to draw as visible
    /// this frame, so the scene management system creates - or re-adds - its ECS counterpart.
    /// </summary>
    /// <param name="type">The entity class the engine reported.</param>
    /// <param name="entity">The native client entity.</param>
    /// <remarks>
    /// Brush model entities (doors, platforms, <c>func_wall</c>, ...) arrive as
    /// <see cref="EntityType.NORMAL"/> too: the engine calls the export for every normal entity, brush
    /// models included, before it draws them. Their physics key is derived from the model index, so no
    /// special case is needed here.
    /// </remarks>
    public unsafe void HUD_AddEntity(EntityType type, cl_entity_t* entity)
    {
        if (entity is null)
            return;

        switch (type)
        {
            case EntityType.NORMAL:
                SceneManagement.MarkEntityVisible(entity, false, null);
                break;

            case EntityType.PLAYER:
                SceneManagement.MarkEntityVisible(entity, true, EngineApi.PStudio->PlayerInfo(entity->index - 1));

                // Cheap and idempotent, and the one place that is guaranteed to run while the local
                // player's entity exists: it is what tells the physics gun it can move in.
                PhysicsGunScript.EnsureAttached(SceneManagement, entity);
                break;
        }
    }

    /// <summary>
    /// The Stride side of <c>HUD_DrawTransparentTriangles</c>: flushes the physics debug wireframe and
    /// the physics gun overlay. Both are drawn from the engine's transparent pass because that is the
    /// only client hook that runs after the world is on screen.
    /// </summary>
    public unsafe void HUD_DrawTransparentTriangles()
    {
        // Depth testing keeps the lines behind world geometry; gsf_physdtest=0 draws them on top.
        var depthTest = IsCvarOn("gsf_physdtest"u8, whenMissing: true);

        if (IsCvarOn("gsf_debugdraw"u8) && SceneSystem.PhysicsDebug is { } debug)
            TriApiLineDraw.DrawLineVertices(debug.Commands.LineVertices, depthTest);

        // The gun's own overlay, drawn in the same pass so the two can be compared directly.
        PhysicsGunScript.DrawOverlay(depthTest);
    }

    /// <summary>
    /// The Stride side of <c>HUD_Shutdown</c>: releases the ECS runtime. Mirror of
    /// <see cref="HUD_Init"/>.
    /// </summary>
    public void HUD_Shutdown() => Dispose();

    /// <summary>
    /// Spawns a ragdoll at <paramref name="origin"/> and returns its entity, or null when the model
    /// could not be loaded.
    /// </summary>
    public Entity? SpawnRagdoll(string modelName, GoldsrcVector3 origin)
        => RagdollHelper.CreateRagdoll(Services, modelName, origin);

    /// <summary>
    /// Returns true when the named console variable is non-zero. Looks the variable up by name every
    /// call - cheap, and it never caches a pointer that a <c>cvar_set</c> could outlive.
    /// </summary>
    /// <param name="name">Variable name, as a UTF-8 literal.</param>
    /// <param name="whenMissing">Value to report when the variable is not registered.</param>
    private static unsafe bool IsCvarOn(ReadOnlySpan<byte> name, bool whenMissing = false)
    {
        var cvar = EngineApi.PClient->GetCvarPointer(name.GetNCharPtr());
        return cvar == null ? whenMissing : cvar->value != 0f;
    }

    /// <summary>Spawns the body the <c>gsf_ragdoll</c> command asks for.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void SpawnRagdollCommand()
    {
        try
        {
            if (commandTarget is not { } game)
                return;

            var entity = game.SpawnRagdoll(RagdollModelPath, GetRagdollSpawnPose());

            EngineApi.DrawStringCenter(entity is null
                ? $"{RagdollCommandName}: cannot load {RagdollModelPath}"
                : $"{RagdollCommandName}: spawned {RagdollModelPath}");

            System.Diagnostics.Debug.WriteLine($"[gsf] {RagdollCommandName} spawned {RagdollModelPath}");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"[gsf] {RagdollCommandName} failed: {exception}");
        }
    }

    /// <summary>
    /// Where a spawned body goes: a little in front of the local player and off the ground, so that it
    /// lands in view instead of inside the player or at the world origin.
    /// </summary>
    private static unsafe GoldsrcVector3 GetRagdollSpawnPose()
    {
        var engine = EngineApi.PClient;

        var origin = new GoldsrcVector3(0, 0, 64);
        var localPlayer = engine->GetLocalPlayer();
        if (localPlayer != null)
            origin = localPlayer->origin;

        var viewAngles = default(GoldsrcVector3);
        var forward = new GoldsrcVector3(1, 0, 0);
        engine->GetViewAngles((float*)&viewAngles);
        engine->AngleVectors((float*)&viewAngles, (float*)&forward, null, null);

        origin += forward * 48.0f;
        origin.Z += 24.0f;
        return origin;
    }
}
