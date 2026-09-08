# GoldSrc Client Game Loop (Xash3D FWGS Reference)

This document is a continuous pseudo-code description of the GoldSrc client
side. It follows `external/xash3d-fwgs` and the callback ABI declared in
`external/xash3d-fwgs/engine/cdll_exp.h`. The names in the pseudo-code are the
names seen by the engine; an individual client DLL may use different internal
function names.

The ownership rule is:

```text
Engine owns the frame, networking, entity snapshots, prediction scheduling,
render-list construction, audio mixing, and the lifetime of engine memory.

client.dll owns HUD state, client effects, input policy, movement rules, weapon
prediction, camera policy, and optional custom render/audio behavior.
```

`cldll_func_t` contains 48 callback pointers. The engine normally obtains them
through one export (`GetClientAPI` in modern SDKs, `F` for secured DLLs). Older
DLLs export the same callbacks individually. The table order is ABI-significant.

## 1. Complete entry and load sequence

```cpp
Main()
{
    Host_MainLoop()
    {
        Host_InputFrame();
        Host_ClientBegin();
        Host_ClientFrame();
    }
}

LoadClientDLL(path)
{
    if (client DLL is already loaded)
        CL_UnloadProgs();

    Allocate client static pool and client entity pool;
    Start VGUI before loading the DLL when external VGUI support is used;
    Load the shared library with COM_LoadLibrary(path);

    Clear all callback slots;

    if (export GetClientAPI exists)
        GetClientAPI(&clgame.dllFuncs);
    else if (export F exists)
        CL_GetSecuredClientAPI(F);      // secured DLL ABI; unwrap pointers
    else
        Resolve each legacy export by its string name;

    Validate required callbacks:
        Initialize, HUD_Init, HUD_VidInit, HUD_Redraw,
        HUD_UpdateClientData, HUD_Reset, and required movement/input slots;
    if (a required callback is missing)
    {
        Unload the library;
        return false;
    }

    Resolve optional SDK 2.3/Xash3D/FWGS callbacks;
    Initialize(&gEngfuncs, CLDLL_INTERFACE_VERSION);
    if (return value == 0)
    {
        Unload the library;
        return false;
    }

    Register client cvars and commands;
    Initialize client CD/audio playlist, titles, particles, view beams,
        temporary-entity pools, and local edicts;
    R_InitRenderAPI();                 // calls HUD_GetRenderInterface if present
    Mobile_Init();                     // touch/mobility extension
    CL_InitClientMove();               // initializes pm_shared and hull bounds
    HUD_Init();
    CL_InitStudioAPI();                // gives renderer engine_studio_api_t;
                                       // then calls HUD_GetStudioModelInterface
    S_InitSoundAPI();                  // calls HUD_GetSoundInterface if present

    Set host_clientloaded = 1;
    return true;
}
```

### Initialization details

```cpp
Initialize(cl_enginefunc_t* engine, int version)
{
    if (version is unsupported)
        return 0;

    Store engine function table;       // sprites, cvars, commands, tracing,
                                       // sound, events, TriAPI, EfxAPI, etc.
    Create permanent client singletons and register client cvars/commands;
    return 1;
}

HUD_Init()
{
    Create permanent HUD state;
    Register user-message hooks through pfnHookUserMsg;
    Register input buttons and initialize client-only systems;
}

CL_InitClientMove()
{
    Pmove_Init();
    Build the shared playermove_t structure and install engine helpers
        (PM_PointContents, PM_PlayerTrace, PM_TraceLine, random functions, sound,
         event playback, model queries, and optional ClipMoveToEntity);

    for (hull = 0; hull < MAX_MAP_HULLS; ++hull)
    {
        HUD_GetHullBounds(hull, mins[hull], maxs[hull]);
        // The DLL supplies player hull extents. A non-zero return means bounds
        // were written; they are copied into pmove->player_mins/maxs.
    }

    HUD_PlayerMoveInit(pmove);
    // One-time client initialization of the playermove state.
}
```

## 2. Video initialization and map transition

There is no native `ClientDLL_MapLoaded` export. Xash3D calls `HUD_VidInit` for
startup, map changes, and video-mode changes; the first later player-state
transfer is the practical point at which a new map is confirmed.

```cpp
VideoOrMapInit()
{
    SCR_VidInit()
    {
        Reset draw state and screen dimensions;
        Restart VGUI and invalidate cached HUD sprite textures;
        HUD_VidInit();
        // Reload video-dependent sprites, fonts, textures, and HUD resources.
        Notify console/touch UI about the new screen size;
    }
}

ServerDataArrived()
{
    // CL_ParseServerData calls HUD_VidInit directly after a new server-data
    // message. This covers initial connection and a level transition, and is
    // especially important when the game directory/client resources changed.
    HUD_VidInit();
    newMapPending = true;
}

ResetClientSession()
{
    HUD_Reset();
    // This is a separate reset callback. FWGS uses it for demo/session reset
    // paths; it is not automatically paired with every HUD_VidInit call.
}

ConfirmMapAfterPlayerState()
{
    if (newMapPending)
    {
        level = pfnGetLevelName();
        if (level is not empty)
        {
            NewMap(level);              // framework-level derived callback
            newMapPending = false;
        }
        else
        {
            pfnClientCmd("disconnect\\n");
            Con_Printf("map name is unavailable");
        }
    }
}
```

## 3. Input phase

Input is event-driven. It can happen between frames, while a frame is being
prepared, or while the window changes focus.

```cpp
Host_InputFrame()
{
    IN_Commands()
    {
        Poll OS/SDL, joystick, gyro, touch, and raw mouse deltas;

        if (client exposes the FWGS look/move extensions)
        {
            IN_ClientLookEvent(yawDelta, pitchDelta);
            IN_ClientMoveEvent(forward, side);
            // Extensions receive normalized input before usercmd assembly.
        }

        Check mouse activation state;
    }

    IN_MouseMove();                    // updates VGUI and menu cursor
}

WindowFocusChanged(active)
{
    if (active)
    {
        IN_ActivateMouse();
        // Client callback enables its mouse/key handling.
    }
    else
    {
        IN_DeactivateMouse();
        // Client callback releases relative mouse mode and input grabs.
    }
}

MouseButtonEvent(buttonState)
{
    if (key destination == game && mouse is active)
    {
        IN_MouseEvent(buttonState);
        // The client interprets button bits; it may call engine Key_Event.
    }
    else
        Key_Event(mouseKey, down);
}

KeyEvent(down, key, binding)
{
    if (HUD_Key_Event(down, key, binding) != 0)
        return;                         // client consumed the key
    Execute normal engine binding;
}

ClearInputStates()
{
    IN_ClearStates();                   // releases client-side + engine buttons
}

AccumulateInputForRender()
{
    IN_Accumulate();                    // client consumes accumulated deltas
}
```

`CL_IsThirdPerson()` is queried while deciding whether the local player is
rendered and while setting renderer parameters. `CL_CameraOffset(float* ofs)`
is an ABI-compatible legacy slot and is intentionally unused by Xash3D FWGS.
`KB_Find(name)` is queried by the engine/UI for client key-button structures such
as `in_mlook`, `in_jlook`, and `in_graph`.

## 4. Per-frame loop

The two functions below are the real Xash3D split. `Host_ClientBegin` runs before
remote packet processing; `Host_ClientFrame` runs the rest of the frame.

```cpp
Host_ClientBegin()
{
    Cbuf_Execute();                    // execute queued console commands
    if (!cls.initialized)
        return;

    CL_CheckClientState();              // complete sign-on/connect state machine
    CL_CheckLogoChanged();
    HUD_UpdateClientData(&clientData, cl.time);
    // Input-independent local origin, viewangles, weapon bits, and FOV are sent
    // to the DLL. If it returns non-zero, modified viewangles/FOV are copied back.

    if (SV_Active())
        CL_SendCommand();              // local server: send intentions now

    SteamBroker_Frame();
}

Host_ClientFrame()
{
    if (!cls.initialized)
        return;

    if (remote server)
        CL_SendCommand();              // remote server: send after prior input

    HUD_Frame(host.frametime);
    // Once per engine frame. Poll client timing/cvars and perform lightweight
    // client bookkeeping; it is not a replacement for HUD_Redraw.

    CL_SetLastUpdate();
    CL_ReadPackets();                  // parse server snapshots and user messages
    CL_RedoPrediction();               // replay unacknowledged commands
    Voice_Idle(host.frametime);

    CL_EmitEntities();                 // interpolation, filtering, temp entities,
                                       // client events, and camera-side simulation
    CL_CheckForResend();
    while (CL_RequestMissingResources())
        ;                               // download/precache resources

    CL_MoveThirdpersonCamera();        // calls CAM_Think()
    CL_MoveSpectatorCamera();
    VID_CheckChanges();
    SCR_UpdateScreen();                // 3D view + HUD + client triangles
    SND_UpdateSound();
    SCR_RunCinematic();
    CL_AdjustClock();
}
```

### User command creation and prediction

```cpp
CL_SendCommand()
{
    if (client is not connected or cinematic)
        return;

    BuildSolidPhysEntLists();          // world + brush/studio collision entities
    BuildPredictedPlayerList();

    cmd = next outgoing usercmd;
    active = signon == SIGNONS && !paused && !demoPlayback;
    Platform_PreCreateMove();
    CL_CreateMove(host.frametime, cmd, active);
    // Client sets viewangles, forward/sidemove/upmove, buttons, impulse,
    // weapon selection, and mod-specific command fields.

    if (client did not provide CL_ClientLookEvent)
        IN_EngineAppendMove(host.frametime, cmd, active);
        // Engine appends mouse/joystick movement and clamps pitch.

    Set command msec and lightlevel;
    Restore frozen/background/overview viewangles when required;
    CL_PredictMovement(false);
}

CL_PredictMovement(replay)
{
    CL_SetUpPlayerPrediction(dopred = true, includeLocalClient = true);
    CL_SetSolidPlayers(localPlayer);

    for each unacknowledged usercmd
    {
        CL_RunUsercmd(fromState, toState, cmd, runfuncs, &time, randomSeed)
        {
            if (cmd.msec > 50)
            {
                Split command into two smaller commands;
                Run both halves recursively;
            }
            else
            {
                Setup pmove state from local_state_t;
                HUD_PlayerMove(pmove, server = false);
                // Shared movement: friction, acceleration, collision, ladders,
                // water, hull tracing, ducking, and movement result.
                Copy pmove result back to predicted local_state_t;

                HUD_PostRunCmd(from, to, cmd, runfuncs, time, randomSeed);
                // Weapon/item prediction, animation, sounds, events, and custom
                // client state are updated after movement.
            }
        }
    }
}
```

When a server snapshot acknowledges commands, the parser calls
`HUD_TxferPredictionData` to merge authoritative and predicted clientdata,
player state, and weapon data. `HUD_ProcessPlayerState` is called once per
received player state; for the local player it is also the map-confirmation hook.

```cpp
ProcessServerPlayerState(state)
{
    if (state is local player)
        HUD_TxferLocalOverrides(state, &frame.clientdata);

    HUD_ProcessPlayerState(destination, state);
    ConfirmMapAfterPlayerState();

    if (state is local player)
        CheckPredictionErrorAgainstAcknowledgedOrigin();
}

TransferAcknowledgedPrediction(frame)
{
    HUD_TxferPredictionData(authoritativePlayerState,
                            predictedPlayerState,
                            authoritativeClientData,
                            predictedClientData,
                            authoritativeWeaponData,
                            predictedWeaponData);
}
```

## 5. Network, demo, and asynchronous callbacks

```cpp
ReadPackets()
{
    for each incoming packet
    {
        if (packet is connectionless)
        {
            if (HUD_ConnectionlessPacket(from, args, replyBuffer, &replySize))
                Send client-generated reply;
            else
                Process standard engine connectionless command;
        }
        else
        {
            Parse server snapshot;
            for each entity state
                ProcessServerPlayerState(state when it is a player);
            Parse user messages;
            if (message is director/HLTV)
                HUD_DirectorMessage(size, buffer);
        }
    }
}

ReadDemoBuffer(size, buffer)
{
    Demo_ReadBuffer(size, buffer);
    // Client consumes demo-specific commands/events. The engine owns demo I/O.
}

StudioEvent(event, entity)
{
    // Renderer forwards animation events (muzzle flash, footsteps, custom
    // events) at the point a studio model is evaluated.
    HUD_StudioEvent(event, entity);
}

VoicePacketDecoded(entindex, samples, pcm)
{
    if (HUD_VoiceStatus exists)
        HUD_VoiceStatus(entindex, talking = true/false);

    if (Voice_StartChannel exists && Voice_StartChannel(samples, pcm, entindex))
        return;                         // client supplied custom voice playback
    Mix voice through the engine sound system;
}

ChatLayoutQuery()
{
    x = defaultChatX; y = defaultChatY;
    if (HUD_ChatInputPosition exists)
        HUD_ChatInputPosition(&x, &y);
    Draw chat input at (x, y);
}
```

## 6. Entity list, temporary entities, and client events

```cpp
CL_EmitEntities()
{
    if (paused || state != ca_active || !validsequence)
        return;

    Update lightstyles, dynamic lights, and interpolation fraction;
    R_ClearScene();

    for each visible player entity
        AddEntityToClientScene(ET_PLAYER, entity);

    for each visible packet entity
        AddEntityToClientScene(ET_NORMAL or ET_BEAM, entity);

    HUD_CreateEntities();
    // Client creates beams, dlights, view effects, custom sprites, and other
    // client-only cl_entity_t instances.

    HUD_TempEntUpdate(frameTime, cl.time, gravity,
                      &freeTempEnts, &activeTempEnts,
                      Callback_AddVisibleEntity,
                      Callback_TempEntPlaySound);
    // Client advances TEMPENTITY lifetimes, positions, collision/bounce,
    // animation and sound. Callback_AddVisibleEntity adds a temp entity only
    // when it is in the current PVS; Callback_TempEntPlaySound emits its sound.

    CL_FireEvents();                    // invokes HUD_StudioEvent as appropriate
}

AddEntityToClientScene(type, entity)
{
    if (!entity.model)
        return;

    if (HUD_AddEntity(type, entity, entity.model.name) == 0)
        return;                         // client culls it and may suppress effects

    if (local player && !CL_IsThirdPerson())
        return;                         // first-person local body is not drawn

    R_AddEntity(entity, type);
}

GetUserEntity(index)
{
    return HUD_GetUserEntity(index);
    // Used by beam/effect code for entities allocated through the client DLL.
}
```

## 7. View and rendering sequence

```cpp
SCR_UpdateScreen()
{
    if (!V_PreRender())
        return;                         // loading plaque, disabled screen, etc.

    V_RenderView()
    {
        Setup viewport and ref_params_t;
        Setup view model and renderer 2D/3D state;

        do
        {
            V_CalcRefdef(&refParams);
            // Client computes origin, viewangles, FOV, punch/bob, viewmodel,
            // third-person/overview flags, and nextView for monitor cameras.
            Render world and model entities;
        }
        while (refParams.nextView);

        HUD_DrawNormalTriangles();
        // Opaque/world-space TriAPI overlays after normal world drawing.
        HUD_DrawTransparentTriangles();
        // Alpha/transparent TriAPI overlays after transparent world passes.
    }

    HUD_Redraw(cl.time, intermission);
    // Draw 2D HUD, sprites, text, crosshair, scoreboard, and client overlays.
    // Return value is used by legacy clients to indicate redraw handling.

    Draw engine console, menus, loading/progress overlays, and VGUI;
    V_PostRender();
}
```

`HUD_GetRenderInterface(version, render_api, callback)` is called during
`R_InitRenderAPI`, before normal frames. If accepted, the client can provide
custom render callbacks and access the Xash3D renderer interface. The standard
TriAPI path still reaches `HUD_DrawNormalTriangles` and
`HUD_DrawTransparentTriangles` through the renderer bridge.

## 8. Movement and collision extension callbacks

```cpp
ClipMoveToEntity(physent, start, mins, maxs, end, trace)
{
    if (HUD_ClipMoveToEntity exists)
        HUD_ClipMoveToEntity(physent, start, mins, maxs, end, trace);
    else
        trace.allsolid = false;         // no custom hit; engine fallback behavior
}

PlayerMoveTexture(name)
{
    material = HUD_PlayerMoveTexture(name);
    // Called by movement/material code when a client DLL wants to remap a
    // surface texture to a movement material (ladder, ice, slime, etc.).
    return material;
}
```

`HUD_GetHullBounds` is called at movement initialization (and can be called
again when a movement implementation is rebuilt). `HUD_PlayerMove` is called
for every predicted or spectator user command. `HUD_PostRunCmd` runs directly
after each command, including replayed commands, and must guard side effects
with its `runfuncs` argument.

## 9. Shutdown and unload

```cpp
UnloadClientDLL()
{
    if (!client DLL loaded)
        return;

    Free client edicts, temp entities, view beams, particles, and remaps;
    Remove client cvars and commands linked to CMD_CLIENTDLL/FCVAR_CLIENTDLL;

    HUD_Shutdown();
    // Release HUD singletons, user-message hooks, custom render/audio state,
    // and resources allocated by HUD_Init/Initialize.

    Shutdown VGUI and custom interfaces;
    Set host_clientloaded = 0;
    Free the shared library;
    Free client memory pools;
    Clear clgame.dllFuncs and all client state;
}
```

`HUD_Reset` is not shutdown: it is a separate reusable reset callback used by
session/demo reset paths. `HUD_VidInit` is not map-only: it can be called by a
video restart or display change, and FWGS also calls it when new server-data is
received during connection/level setup.

## 10. Export coverage checklist

The following is the complete `cldll_func_t` callback set from `cdll_exp.h`.
The left column is the legacy export name used by `cl_game.c`; the right column
is the function-table member used in the loop above.

| DLL export | `cldll_func_t` member | Main call site/phase |
|---|---|---|
| `Initialize` | `pfnInitialize` | DLL load |
| `HUD_Init` | `pfnInit` | post-load initialization |
| `HUD_VidInit` | `pfnVidInit` | video/map initialization |
| `HUD_Redraw` | `pfnRedraw` | every rendered frame |
| `HUD_UpdateClientData` | `pfnUpdateClientData` | `Host_ClientBegin` |
| `HUD_Reset` | `pfnReset` | map/reconnect/demo reset |
| `HUD_PlayerMove` | `pfnPlayerMove` | each predicted command |
| `HUD_PlayerMoveInit` | `pfnPlayerMoveInit` | movement initialization |
| `HUD_PlayerMoveTexture` | `pfnPlayerMoveTexture` | movement material lookup |
| `IN_ActivateMouse` | `IN_ActivateMouse` | window/input activation |
| `IN_DeactivateMouse` | `IN_DeactivateMouse` | window/input deactivation |
| `IN_MouseEvent` | `IN_MouseEvent` | mouse button state |
| `IN_ClearStates` | `IN_ClearStates` | input reset |
| `IN_Accumulate` | `IN_Accumulate` | input accumulation |
| `CL_CreateMove` | `CL_CreateMove` | usercmd construction |
| `CL_IsThirdPerson` | `CL_IsThirdPerson` | entity/view filtering |
| `CL_CameraOffset` | `CL_CameraOffset` | legacy slot; unused by FWGS |
| `KB_Find` | `KB_Find` | key-button lookup |
| `CAM_Think` | `CAM_Think` | third-person camera step |
| `V_CalcRefdef` | `pfnCalcRefdef` | view/refdef calculation |
| `HUD_AddEntity` | `pfnAddEntity` | each candidate visible entity |
| `HUD_CreateEntities` | `pfnCreateEntities` | client-only entities |
| `HUD_DrawNormalTriangles` | `pfnDrawNormalTriangles` | 3D opaque overlays |
| `HUD_DrawTransparentTriangles` | `pfnDrawTransparentTriangles` | 3D transparent overlays |
| `HUD_StudioEvent` | `pfnStudioEvent` | studio animation events |
| `HUD_PostRunCmd` | `pfnPostRunCmd` | after each movement command |
| `HUD_Shutdown` | `pfnShutdown` | DLL unload |
| `HUD_TxferLocalOverrides` | `pfnTxferLocalOverrides` | local snapshot transfer |
| `HUD_ProcessPlayerState` | `pfnProcessPlayerState` | player snapshot processing |
| `HUD_TxferPredictionData` | `pfnTxferPredictionData` | acknowledged prediction merge |
| `Demo_ReadBuffer` | `pfnDemo_ReadBuffer` | demo packet playback |
| `HUD_ConnectionlessPacket` | `pfnConnectionlessPacket` | out-of-band packet |
| `HUD_GetHullBounds` | `pfnGetHullBounds` | pmove hull setup |
| `HUD_Frame` | `pfnFrame` | once per engine frame |
| `HUD_Key_Event` | `pfnKey_Event` | key press/release |
| `HUD_TempEntUpdate` | `pfnTempEntUpdate` | temp-entity simulation |
| `HUD_GetUserEntity` | `pfnGetUserEntity` | client beam/effect lookup |
| `HUD_VoiceStatus` | `pfnVoiceStatus` | voice talker state |
| `HUD_DirectorMessage` | `pfnDirectorMessage` | HLTV/director message |
| `HUD_GetStudioModelInterface` | `pfnGetStudioModelInterface` | studio renderer hook |
| `HUD_ChatInputPosition` | `pfnChatInputPosition` | chat layout query |
| `HUD_GetRenderInterface` | `pfnGetRenderInterface` | Xash3D render extension |
| `HUD_ClipMoveToEntity` | `pfnClipMoveToEntity` | Xash3D custom collision |
| `IN_ClientTouchEvent` | `pfnTouchEvent` | FWGS touch event |
| `IN_ClientMoveEvent` | `pfnMoveEvent` | FWGS mobile movement |
| `IN_ClientLookEvent` | `pfnLookEvent` | FWGS mobile look |
| `HUD_GetSoundInterface` | `pfnGetSoundInterface` | FWGS sound extension |
| `Voice_StartChannel` | `pfnVoice_StartChannel` | custom voice PCM playback |

The single export used to fill this table is `GetClientAPI` (or secured `F`).
Those are loader exports rather than entries in `cldll_func_t`; they are still
part of the client DLL contract and are included in `LoadClientDLL` above.

## 11. Source map

```text
engine/cdll_exp.h                         callback ABI and ordering
engine/client/dll_int/cl_game.c            DLL loading, validation, init, unload
engine/client/cl_main.c                    Host_ClientBegin/Frame, commands, packets
engine/client/cl_frame.c                   snapshots, interpolation, entity emission
engine/client/dll_int/cl_pmove.c           prediction and movement callbacks
engine/client/cl_view.c                    V_CalcRefdef and view rendering
engine/client/cl_tent.c                    HUD_TempEntUpdate bridge
engine/client/input/input.c                mouse, movement, touch/look extensions
engine/client/input/in_keys.c              HUD_Key_Event and IN_ClearStates
engine/client/parse/cl_parse.c             prediction transfer/director messages
engine/client/dll_int/cl_render.c          HUD_GetRenderInterface
engine/client/sound/s_main.c               HUD_GetSoundInterface
engine/client/sound/voice.c                HUD_VoiceStatus/Voice_StartChannel
```
