using GoldsrcFramework.Content;
using GoldsrcFramework.Ecs;
using GoldsrcFramework.Graphics;
using GoldsrcFramework.LinearMath;
using GoldsrcFramework.Rendering;
using NativeInterop;
using System.Text;


namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of client export functions based on LegacyClientInterop
/// </summary>
public unsafe class FrameworkClientExports : IClientExportFuncs
{
    private const int MaxLevelNameLength = 260;

    private GoldsrcClientGame? clientGame;

    /// <summary>
    /// True when <see cref="HUD_VidInit"/> announced a (potential) map change and the
    /// framework is still waiting for <see cref="HUD_ProcessPlayerState"/> to confirm it.
    /// </summary>
    private bool isNewMapPending;

    /// <summary>
    /// Name of the currently loaded level (e.g. "maps/crossfire.bsp"), as reported by the
    /// engine when the last <see cref="HUD_NewMap"/> callback fired. Null before the first map load.
    /// </summary>
    public string? CurrentLevelName { get; private set; }

    /// <summary>
    /// Raised when a new map has finished loading (see <see cref="HUD_NewMap"/>).
    /// The string argument is the level name, e.g. "maps/crossfire.bsp".
    /// Exceptions thrown by subscribers are caught and logged; they never propagate
    /// back into the engine callback.
    /// </summary>
    public event Action<string>? NewMapLoaded;

    // IClientExportFuncs implementation - all based on LegacyClientInterop
    public virtual int Initialize(ClientEngineFuncs* pEnginefuncs, int iVersion)
    {
        EngineApi.ClientApiInit(pEnginefuncs);
        return LegacyClientInterop.Initialize(pEnginefuncs, iVersion);
    }

    public virtual void HUD_Init()
    {
        LegacyClientInterop.HUD_Init();

        var game = new GoldsrcClientGame();
        game.SceneManagement.ContentManager = new GoldsrcContentManager();
        clientGame = game;
        RegisterDebugDrawCvar();
    }

    public virtual int HUD_VidInit()
    {
        int result = LegacyClientInterop.HUD_VidInit();
        NewMapBegin(true);
        return result;
    }

    public virtual int HUD_Redraw(float flTime, int intermission)
    {
        EngineApi.DrawStringCenter("GoldsrcFrameworkDemo");
        return LegacyClientInterop.HUD_Redraw(flTime, intermission);
    }

    public virtual int HUD_UpdateClientData(client_data_t* cdata, float flTime)
    {
        return LegacyClientInterop.HUD_UpdateClientData(cdata, flTime);
    }

    public virtual void HUD_Reset()
    {
        LegacyClientInterop.HUD_Reset();
    }

    public virtual void HUD_PlayerMove(playermove_t* ppmove, qboolean server)
    {
        LegacyClientInterop.HUD_PlayerMove(ppmove, server.Value);
    }

    public virtual void HUD_PlayerMoveInit(playermove_t* ppmove)
    {
        LegacyClientInterop.HUD_PlayerMoveInit(ppmove);
    }

    public virtual NChar HUD_PlayerMoveTexture(NChar* name)
    {
        sbyte result = LegacyClientInterop.HUD_PlayerMoveTexture((sbyte*)name);
        return new NChar((byte)result);
    }

    public virtual void IN_ActivateMouse()
    {
        LegacyClientInterop.IN_ActivateMouse();
    }

    public virtual void IN_DeactivateMouse()
    {
        LegacyClientInterop.IN_DeactivateMouse();
    }

    public virtual void IN_MouseEvent(int mstate)
    {
        LegacyClientInterop.IN_MouseEvent(mstate);
    }

    public virtual void IN_ClearStates()
    {
        LegacyClientInterop.IN_ClearStates();
    }

    public virtual void IN_Accumulate()
    {
        LegacyClientInterop.IN_Accumulate();
    }

    public virtual void CL_CreateMove(float frametime, usercmd_t* cmd, int active)
    {
        LegacyClientInterop.CL_CreateMove(frametime, cmd, active);
    }

    public virtual int CL_IsThirdPerson()
    {
        return LegacyClientInterop.CL_IsThirdPerson();
    }

    public virtual void CL_GetCameraOffsets(Vector3* ofs)
    {
        LegacyClientInterop.CL_GetCameraOffsets(ofs);
    }

    public virtual kbutton_t* KB_Find(NChar* name)
    {
        return LegacyClientInterop.KB_Find((sbyte*)name);
    }

    public virtual void CAM_Think()
    {
        LegacyClientInterop.CAM_Think();
    }

    public virtual void V_CalcRefdef(ref_params_t* pparams)
    {
        LegacyClientInterop.V_CalcRefdef(pparams);
    }

    public virtual int HUD_AddEntity(int type, cl_entity_t* ent, NChar* modelname)
    {
        var result = LegacyClientInterop.HUD_AddEntity(type, ent, (sbyte*)modelname);

        // Mark this entity as visible for the ClientSceneManagementSystem.
        // Only normal entities (ET_NORMAL) and players are tracked. The engine calls this for brush
        // model entities too, so doors, platforms and func_wall come through here as well.
        if (type == 0 || type == 1) // ET_NORMAL = 0, ET_PLAYER = 1
        {
            clientGame?.SceneManagement.MarkEntityVisible(ent, type == 1);
        }

        return result;
    }

    public virtual void HUD_CreateEntities()
    {
        LegacyClientInterop.HUD_CreateEntities();
    }

    public virtual void HUD_DrawNormalTriangles()
    {
        LegacyClientInterop.HUD_DrawNormalTriangles();
    }

    public virtual void HUD_DrawTransparentTriangles()
    {
        LegacyClientInterop.HUD_DrawTransparentTriangles();

        if (IsDebugDrawEnabled() && clientGame?.SceneSystem.PhysicsDebug is { } debug)
            TriApiLineDraw.DrawLineVertices(debug.Commands.LineVertices);
    }

    public virtual void HUD_StudioEvent(mstudioevent_t* @event, cl_entity_t* entity)
    {
        LegacyClientInterop.HUD_StudioEvent(@event, entity);
    }

    public virtual void HUD_PostRunCmd(local_state_t* from, local_state_t* to, usercmd_t* cmd, int runfuncs, double time, uint random_seed)
    {
        LegacyClientInterop.HUD_PostRunCmd(from, to, cmd, runfuncs, time, random_seed);
    }

    public virtual void HUD_Shutdown()
    {
        isNewMapPending = false;
        CurrentLevelName = null;
        clientGame?.Dispose();
        LegacyClientInterop.HUD_Shutdown();
    }

    public virtual void HUD_TxferLocalOverrides(entity_state_t* state, clientdata_t* client)
    {
        LegacyClientInterop.HUD_TxferLocalOverrides(state, client);
    }

    public virtual void HUD_ProcessPlayerState(entity_state_t* dst, entity_state_t* src)
    {
        LegacyClientInterop.HUD_ProcessPlayerState(dst, src);
        NewMapBegin(false);
    }

    public virtual void HUD_TxferPredictionData(entity_state_t* ps, entity_state_t* pps, clientdata_t* pcd, clientdata_t* ppcd, weapon_data_t* wd, weapon_data_t* pwd)
    {
        LegacyClientInterop.HUD_TxferPredictionData(ps, pps, pcd, ppcd, wd, pwd);
    }

    public virtual void Demo_ReadBuffer(int size, byte* buffer)
    {
        LegacyClientInterop.Demo_ReadBuffer(size, buffer);
    }

    public virtual int HUD_ConnectionlessPacket(netadr_t* net_from, NChar* args, NChar* response_buffer, int* response_buffer_size)
    {
        return LegacyClientInterop.HUD_ConnectionlessPacket(net_from, (sbyte*)args, (sbyte*)response_buffer, response_buffer_size);
    }

    public virtual int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        return LegacyClientInterop.HUD_GetHullBounds(hullnumber, mins, maxs);
    }

    public virtual void HUD_Frame(double time)
    {
        LegacyClientInterop.HUD_Frame(time);
    }

    public virtual int HUD_Key_Event(int eventcode, int keynum, NChar* pszCurrentBinding)
    {
        return LegacyClientInterop.HUD_Key_Event(eventcode, keynum, pszCurrentBinding);
    }

    public virtual void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, TEMPENTITY** ppTempEntFree, TEMPENTITY** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_t*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<TEMPENTITY*, float, void> Callback_TempEntPlaySound)
    {
        LegacyClientInterop.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);

        if (clientGame is not { } game)
            return;

        game.SyncGravity(cl_gravity);
        game.Tick(TimeSpan.FromSeconds(client_time), TimeSpan.FromSeconds(Math.Min(frametime, 0.1)));

    }

    public virtual cl_entity_t* HUD_GetUserEntity(int index)
    {
        return LegacyClientInterop.HUD_GetUserEntity(index);
    }

    public virtual void HUD_VoiceStatus(int entindex, qboolean bTalking)
    {
        LegacyClientInterop.HUD_VoiceStatus(entindex, bTalking);
    }

    public virtual void HUD_DirectorMessage(int iSize, void* pbuf)
    {
        LegacyClientInterop.HUD_DirectorMessage(iSize, pbuf);
    }

    public virtual int HUD_GetStudioModelInterface(int version, r_studio_interface_t** ppinterface, engine_studio_api_t* pstudio)
    {
        //return LegacyClientInterop.HUD_GetStudioModelInterface(version, ppinterface, pstudio);
        return StudioModelRenderer.GetStudioModelInterface(version, ppinterface, pstudio);
    }

    public virtual void HUD_ChatInputPosition(int* x, int* y)
    {
        LegacyClientInterop.HUD_ChatInputPosition(x, y);
    }

    public virtual int HUD_GetPlayerTeam(int iplayer)
    {
        return LegacyClientInterop.HUD_GetPlayerTeam(iplayer);
    }

    public virtual void* ClientFactory()
    {
        return LegacyClientInterop.ClientFactory();
    }

    /// <summary>
    /// Constructed lifecycle callback: a new map has finished loading and is ready to use.
    /// </summary>
    /// <remarks>
    /// The engine has no native "map loaded" callback for client DLLs, so the framework
    /// derives one from two engine callbacks (two-phase handshake):
    /// <list type="number">
    /// <item><see cref="HUD_VidInit"/> is called right after a level change, but at that point
    /// client state is not fully set up yet - it only marks the map change as pending.</item>
    /// <item>The next <see cref="HUD_ProcessPlayerState"/> (called early every frame once the
    /// local player state is available) confirms the transition. Only then does the framework
    /// validate <c>pfnGetLevelName()</c> and fire this callback.</item>
    /// </list>
    /// Override this method (or subscribe to <see cref="NewMapLoaded"/>) to rebuild per-map
    /// state such as physics scenes, particle caches or level-specific resources.
    /// Note that <see cref="HUD_VidInit"/> is also invoked on video mode changes, so this
    /// callback may fire again without an actual level change - keep handlers idempotent.
    /// </remarks>
    /// <param name="levelName">Name of the loaded level, e.g. "maps/crossfire.bsp".</param>
    public void HUD_NewMap(string levelName)
    {
        CurrentLevelName = levelName;

        // Drop the previous map's physics resources and entities, then warm the new map's brush
        // prefabs so the first visible brush entity does not pay for triangulation.
        clientGame?.Reset();
        clientGame?.SceneManagement.ContentManager?.PreloadBrushModels();

        if (NewMapLoaded is null)
            return;

        foreach (Action<string> handler in NewMapLoaded.GetInvocationList())
        {
            try
            {
                handler(levelName);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"[NewMapLoaded] subscriber failed: {exception}");
            }
        }
    }

    /// <summary>
    /// Two-phase map change detection. Called with <c>true</c> from <see cref="HUD_VidInit"/>
    /// ("a map change may have just happened") and with <c>false</c> from
    /// <see cref="HUD_ProcessPlayerState"/> every frame ("client state is now available").
    /// </summary>
    private void NewMapBegin(bool isNewMap)
    {
        if (isNewMap)
        {
            isNewMapPending = true;
            return;
        }

        if (!isNewMapPending)
            return;

        isNewMapPending = false;

        Span<byte> buffer = stackalloc byte[MaxLevelNameLength];
        if (TryReadLevelName(buffer, out int length))
        {
            CurrentLevelName = Encoding.ASCII.GetString(buffer[..length]);
            HUD_NewMap(CurrentLevelName);
        }
        else
        {
            // Should not happen: the engine is in a map but reports no level name.
            // Bail out instead of running with stale per-map state.
            ExecuteClientCommand("disconnect\n");
            PrintConsole("GoldsrcFramework: couldn't get map name from level name!\n");
        }
    }

    /// <summary>
    /// Reads the null-terminated level name from <c>pfnGetLevelName()</c> into
    /// <paramref name="buffer"/> without allocating a managed string.
    /// Returns false when unavailable or empty.
    /// </summary>
    private static bool TryReadLevelName(Span<byte> buffer, out int length)
    {
        length = 0;
        ClientEngineFuncs* engine = EngineApi.PClient;
        if (engine == null || engine->GetLevelName == null)
            return false;

        NChar* levelName = engine->GetLevelName();
        if (levelName == null)
            return false;

        while (length < buffer.Length && length < MaxLevelNameLength && (byte)levelName[length] != 0)
        {
            buffer[length] = (byte)levelName[length];
            length++;
        }

        return length > 0;
    }

    /// <summary>
    /// Registers the <c>gsf_debugdraw</c> console variable (default "1") if it is
    /// not already registered. Idempotent across re-init. Reading the value is done
    /// per-frame via <see cref="IsDebugDrawEnabled"/> so a stale cached pointer is
    /// never used.
    /// </summary>
    private static void RegisterDebugDrawCvar()
    {
        ClientEngineFuncs* engine = EngineApi.PClient;
        if (engine == null || engine->RegisterVariable == null || engine->GetCvarPointer == null)
            return;

        Span<byte> name = stackalloc byte[32];
        WriteAscii("gsf_debugdraw", name);

        fixed (byte* namePointer = name)
        {
            if (engine->GetCvarPointer((NChar*)namePointer) != null)
                return; // already registered (e.g. on re-init)

            Span<byte> value = stackalloc byte[8];
            WriteAscii("1", value);
            fixed (byte* valuePointer = value)
                engine->RegisterVariable((NChar*)namePointer, (NChar*)valuePointer, 0);
        }
    }

    /// <summary>
    /// Returns true when the <c>gsf_debugdraw</c> cvar is non-zero. Looks the cvar
    /// up by name each call; cheap and never caches a pointer.
    /// </summary>
    private static bool IsDebugDrawEnabled()
    {
        ClientEngineFuncs* engine = EngineApi.PClient;
        if (engine == null || engine->GetCvarPointer == null)
            return false;

        Span<byte> name = stackalloc byte[32];
        WriteAscii("gsf_debugdraw", name);

        fixed (byte* namePointer = name)
        {
            cvar_t* cvar = engine->GetCvarPointer((NChar*)namePointer);
            return cvar != null && cvar->value != 0f;
        }
    }

    /// <summary>
    /// Writes <paramref name="value"/> as null-terminated ASCII into
    /// <paramref name="buffer"/>. Throws if the buffer is too small.
    /// </summary>
    private static void WriteAscii(string value, Span<byte> buffer)
    {
        buffer.Clear();
        int written = Encoding.ASCII.GetBytes(value, buffer);
        if (written >= buffer.Length)
            throw new ArgumentException("The native string buffer is too small.", nameof(value));

        buffer[written] = 0;
    }

    /// <summary>
    /// Executes a console command on the client (e.g. "disconnect\n").
    /// </summary>
    private static void ExecuteClientCommand(string command)
    {
        ClientEngineFuncs* engine = EngineApi.PClient;
        if (engine == null || engine->ClientCmd == null)
            return;

        if (command.Length >= 128)
            command = command[..127];

        Span<byte> buffer = stackalloc byte[128];
        int length = Encoding.ASCII.GetBytes(command, buffer);
        buffer[length] = 0;

        fixed (byte* pointer = buffer)
            engine->ClientCmd((NChar*)pointer);
    }

    /// <summary>
    /// Prints a message to the client console via <c>Con_Printf</c>.
    /// The message must not contain printf format specifiers.
    /// </summary>
    private static void PrintConsole(string message)
    {
        ClientEngineFuncs* engine = EngineApi.PClient;
        if (engine == null || engine->Con_Printf == null)
            return;

        if (message.Length >= 512)
            message = message[..511];

        Span<byte> buffer = stackalloc byte[512];
        int length = Encoding.ASCII.GetBytes(message, buffer);
        buffer[length] = 0;

        fixed (byte* pointer = buffer)
            engine->Con_Printf((NChar*)pointer);
    }

}

