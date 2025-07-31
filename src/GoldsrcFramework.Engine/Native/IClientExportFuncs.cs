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
    int Initialize(cl_enginefunc_t* pEnginefuncs, int iVersion);

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
    void HUD_PlayerMove(playermove_s* ppmove, int server);

    /// <summary>
    /// Initialize player movement
    /// </summary>
    void HUD_PlayerMoveInit(playermove_s* ppmove);

    /// <summary>
    /// Get texture type for player movement
    /// </summary>
    sbyte HUD_PlayerMoveTexture(sbyte* name);

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
    void CL_CreateMove(float frametime, usercmd_s* cmd, int active);

    /// <summary>
    /// Check if in third person view
    /// </summary>
    int CL_IsThirdPerson();

    /// <summary>
    /// Get camera offsets
    /// </summary>
    void CL_GetCameraOffsets(Vector3f* ofs);

    /// <summary>
    /// Find key button
    /// </summary>
    kbutton_s* KB_Find(sbyte* name);

    /// <summary>
    /// Camera think function
    /// </summary>
    void CAM_Think();

    /// <summary>
    /// Calculate reference parameters
    /// </summary>
    void V_CalcRefdef(ref_params_s* pparams);

    /// <summary>
    /// Add entity to render list
    /// </summary>
    int HUD_AddEntity(int type, cl_entity_s* ent, sbyte* modelname);

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
    void HUD_StudioEvent(mstudioevent_s* @event, cl_entity_s* entity);

    /// <summary>
    /// Post run command processing
    /// </summary>
    void HUD_PostRunCmd(local_state_s* from, local_state_s* to, usercmd_s* cmd, int runfuncs, double time, uint random_seed);

    /// <summary>
    /// Shutdown client DLL
    /// </summary>
    void HUD_Shutdown();

    /// <summary>
    /// Transfer local overrides
    /// </summary>
    void HUD_TxferLocalOverrides(entity_state_s* state, clientdata_s* client);

    /// <summary>
    /// Process player state
    /// </summary>
    void HUD_ProcessPlayerState(entity_state_s* dst, entity_state_s* src);

    /// <summary>
    /// Transfer prediction data
    /// </summary>
    void HUD_TxferPredictionData(entity_state_s* ps, entity_state_s* pps, clientdata_s* pcd, clientdata_s* ppcd, weapon_data_s* wd, weapon_data_s* pwd);

    /// <summary>
    /// Read demo buffer
    /// </summary>
    void Demo_ReadBuffer(int size, byte* buffer);

    /// <summary>
    /// Handle connectionless packet
    /// </summary>
    int HUD_ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size);

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
    int HUD_Key_Event(int eventcode, int keynum, sbyte* pszCurrentBinding);

    /// <summary>
    /// Update temporary entities
    /// </summary>
    void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, tempent_s** ppTempEntFree, tempent_s** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_s*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<tempent_s*, float, void> Callback_TempEntPlaySound);

    /// <summary>
    /// Get user entity
    /// </summary>
    cl_entity_s* HUD_GetUserEntity(int index);

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
    int HUD_GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio);

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
