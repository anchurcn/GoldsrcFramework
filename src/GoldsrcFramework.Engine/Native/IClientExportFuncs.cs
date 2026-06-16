using GoldsrcFramework.LinearMath;
using NativeInterop;
using System;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Interface for client export functions - collection of client export funcs declared in an interface
/// </summary>
public unsafe interface IClientExportFuncs
{
    /// <summary>
    /// Initialize the client DLL
    /// </summary>
    int Initialize(ClientEngineFuncs* pEnginefuncs, int iVersion);

    /// <summary>
    /// Initialize HUD elements
    /// </summary>
    void HUD_Init();

    /// <summary>
    /// Initialize video mode dependent HUD elements
    /// </summary>
    int HUD_VidInit();

    /// <summary>
    /// Redraw HUD elements
    /// </summary>
    int HUD_Redraw(float flTime, int intermission);

    /// <summary>
    /// Update client data
    /// </summary>
    int HUD_UpdateClientData(client_data_t* cdata, float flTime);

    /// <summary>
    /// Reset HUD state
    /// </summary>
    void HUD_Reset();

    /// <summary>
    /// Handle player movement
    /// </summary>
    void HUD_PlayerMove(playermove_t* ppmove, qboolean server);

    /// <summary>
    /// Initialize player movement
    /// </summary>
    void HUD_PlayerMoveInit(playermove_t* ppmove);

    /// <summary>
    /// Get texture type for player movement
    /// </summary>
    NChar HUD_PlayerMoveTexture(NChar* name);

    /// <summary>
    /// Activate mouse input
    /// </summary>
    void IN_ActivateMouse();

    /// <summary>
    /// Deactivate mouse input
    /// </summary>
    void IN_DeactivateMouse();

    /// <summary>
    /// Handle mouse events
    /// </summary>
    void IN_MouseEvent(int mstate);

    /// <summary>
    /// Clear input states
    /// </summary>
    void IN_ClearStates();

    /// <summary>
    /// Accumulate input
    /// </summary>
    void IN_Accumulate();

    /// <summary>
    /// Create movement command
    /// </summary>
    void CL_CreateMove(float frametime, usercmd_t* cmd, int active);

    /// <summary>
    /// Check if in third person view
    /// </summary>
    int CL_IsThirdPerson();

    /// <summary>
    /// Get camera offsets
    /// </summary>
    void CL_GetCameraOffsets(Vector3* ofs);

    /// <summary>
    /// Find key button
    /// </summary>
    kbutton_t* KB_Find(NChar* name);

    /// <summary>
    /// Camera think function
    /// </summary>
    void CAM_Think();

    /// <summary>
    /// Calculate reference parameters
    /// </summary>
    void V_CalcRefdef(ref_params_t* pparams);

    /// <summary>
    /// Add entity to render list
    /// </summary>
    int HUD_AddEntity(int type, cl_entity_t* ent, NChar* modelname);

    /// <summary>
    /// Create entities
    /// </summary>
    void HUD_CreateEntities();

    /// <summary>
    /// Draw normal triangles
    /// </summary>
    void HUD_DrawNormalTriangles();

    /// <summary>
    /// Draw transparent triangles
    /// </summary>
    void HUD_DrawTransparentTriangles();

    /// <summary>
    /// Handle studio events
    /// </summary>
    void HUD_StudioEvent(mstudioevent_t* @event, cl_entity_t* entity);

    /// <summary>
    /// Post run command processing
    /// </summary>
    void HUD_PostRunCmd(local_state_t* from, local_state_t* to, usercmd_t* cmd, int runfuncs, double time, uint random_seed);

    /// <summary>
    /// Shutdown client DLL
    /// </summary>
    void HUD_Shutdown();

    /// <summary>
    /// Transfer local overrides
    /// </summary>
    void HUD_TxferLocalOverrides(entity_state_t* state, clientdata_t* client);

    /// <summary>
    /// Process player state
    /// </summary>
    void HUD_ProcessPlayerState(entity_state_t* dst, entity_state_t* src);

    /// <summary>
    /// Transfer prediction data
    /// </summary>
    void HUD_TxferPredictionData(entity_state_t* ps, entity_state_t* pps, clientdata_t* pcd, clientdata_t* ppcd, weapon_data_t* wd, weapon_data_t* pwd);

    /// <summary>
    /// Read demo buffer
    /// </summary>
    void Demo_ReadBuffer(int size, byte* buffer);

    /// <summary>
    /// Handle connectionless packet
    /// </summary>
    int HUD_ConnectionlessPacket(netadr_t* net_from, NChar* args, NChar* response_buffer, int* response_buffer_size);

    /// <summary>
    /// Get hull bounds
    /// </summary>
    int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs);

    /// <summary>
    /// Frame processing
    /// </summary>
    void HUD_Frame(double time);

    /// <summary>
    /// Handle key events
    /// </summary>
    int HUD_Key_Event(int eventcode, int keynum, NChar* pszCurrentBinding);

    /// <summary>
    /// Update temporary entities
    /// </summary>
    void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, TEMPENTITY** ppTempEntFree, TEMPENTITY** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_t*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<TEMPENTITY*, float, void> Callback_TempEntPlaySound);

    /// <summary>
    /// Get user entity
    /// </summary>
    cl_entity_t* HUD_GetUserEntity(int index);

    /// <summary>
    /// Handle voice status
    /// </summary>
    void HUD_VoiceStatus(int entindex, qboolean bTalking);

    /// <summary>
    /// Handle director message
    /// </summary>
    void HUD_DirectorMessage(int iSize, void* pbuf);

    /// <summary>
    /// Get studio model interface
    /// </summary>
    int HUD_GetStudioModelInterface(int version, r_studio_interface_t** ppinterface, engine_studio_api_t* pstudio);

    /// <summary>
    /// Get chat input position
    /// </summary>
    void HUD_ChatInputPosition(int* x, int* y);

    /// <summary>
    /// Get player team
    /// </summary>
    int HUD_GetPlayerTeam(int iplayer);

    /// <summary>
    /// Client factory function
    /// </summary>
    void* ClientFactory();
}
