using GoldsrcFramework.Ecs;
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
        clientGame = game;

        // Everything from here on - the console variables and the ragdoll command included - is the
        // runtime's own setup, so it belongs to the game rather than to this layer.
        game.HUD_Init();
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

    public virtual NChar HUD_PlayerMoveTexture(NCharPtr name)
    {
        return new NChar(LegacyClientInterop.HUD_PlayerMoveTexture(name));
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
        clientGame?.CL_CreateMove(cmd, active);
    }

    public virtual int CL_IsThirdPerson()
    {
        return LegacyClientInterop.CL_IsThirdPerson();
    }

    public virtual void CL_GetCameraOffsets(Vector3* ofs)
    {
        LegacyClientInterop.CL_GetCameraOffsets(ofs);
    }

    public virtual kbutton_t* KB_Find(NCharPtr name)
    {
        return LegacyClientInterop.KB_Find(name);
    }

    public virtual void CAM_Think()
    {
        LegacyClientInterop.CAM_Think();
    }

    public virtual void V_CalcRefdef(ref_params_t* pparams)
    {
        LegacyClientInterop.V_CalcRefdef(pparams);
    }

    public virtual int HUD_AddEntity(EntityType type, cl_entity_t* ent, NCharPtr modelname)
    {
        var result = LegacyClientInterop.HUD_AddEntity((int)type, ent, modelname);

        // The engine calls this for brush model entities too, so doors, platforms and func_wall come
        // through here as well; the game decides which of them it tracks.
        clientGame?.HUD_AddEntity(type, ent);

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

        clientGame?.HUD_DrawTransparentTriangles();
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
        clientGame?.HUD_Shutdown();
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

    public virtual int HUD_ConnectionlessPacket(netadr_t* net_from, NCharPtr args, NCharPtr response_buffer, int* response_buffer_size)
    {
        return LegacyClientInterop.HUD_ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
    }

    public virtual int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        return LegacyClientInterop.HUD_GetHullBounds(hullnumber, mins, maxs);
    }

    public virtual void HUD_Frame(double time)
    {
        LegacyClientInterop.HUD_Frame(time);
    }

    public virtual int HUD_Key_Event(int eventcode, int keynum, NCharPtr pszCurrentBinding)
    {
        return LegacyClientInterop.HUD_Key_Event(eventcode, keynum, pszCurrentBinding);
    }

    public virtual void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, TEMPENTITY** ppTempEntFree, TEMPENTITY** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_t*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<TEMPENTITY*, float, void> Callback_TempEntPlaySound)
    {
        LegacyClientInterop.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);

        if (clientGame is not { } game)
            return;

        game.HUD_TempEntUpdate(frametime, client_time, cl_gravity);
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

        // The renderer asks the runtime about the entities it draws, and the engine calls this before
        // anything else exists to resolve the game from - so it is handed over. HUD_Init has already
        // run by this point, which is what makes the game non-null here.
        return StudioModelRenderer.GetStudioModelInterface(version, ppinterface, pstudio, clientGame!);
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
        clientGame?.HUD_NewMap();

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
            // Should not happen: the engine is in a map but reports no level name. Bail out instead
            // of running with stale per-map state. Con_Printf is printf-style, so the message must
            // not contain a '%'.
            EngineApi.PClient->ClientCmd("disconnect\n"u8.GetNCharPtr());
            EngineApi.PClient->Con_Printf("GoldsrcFramework: couldn't get map name from level name!\n"u8.GetNCharPtr());
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

        NCharPtr levelName = EngineApi.PClient->GetLevelName();
        if (levelName.IsNull)
            return false;

        // The engine hands back a NUL-terminated name; copy it out without its terminator,
        // capped by both the scan bound and the caller's buffer.
        ReadOnlySpan<byte> name = levelName.AsByteSpan(MaxLevelNameLength);
        length = Math.Min(name.Length, buffer.Length);
        name[..length].CopyTo(buffer);

        return length > 0;
    }

}

