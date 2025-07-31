using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Client export functions structure that matches cldll_func_t layout
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClientExportFuncs
{
    public delegate* unmanaged[Cdecl]<cl_enginefunc_t*, int, int> Initialize;
    public delegate* unmanaged[Cdecl]<void> HUD_Init;
    public delegate* unmanaged[Cdecl]<int> HUD_VidInit;
    public delegate* unmanaged[Cdecl]<float, int, int> HUD_Redraw;
    public delegate* unmanaged[Cdecl]<client_data_t*, float, int> HUD_UpdateClientData;
    public delegate* unmanaged[Cdecl]<void> HUD_Reset;
    public delegate* unmanaged[Cdecl]<playermove_s*, int, void> HUD_PlayerMove;
    public delegate* unmanaged[Cdecl]<playermove_s*, void> HUD_PlayerMoveInit;
    public delegate* unmanaged[Cdecl]<sbyte*, sbyte> HUD_PlayerMoveTexture;
    public delegate* unmanaged[Cdecl]<void> IN_ActivateMouse;
    public delegate* unmanaged[Cdecl]<void> IN_DeactivateMouse;
    public delegate* unmanaged[Cdecl]<int, void> IN_MouseEvent;
    public delegate* unmanaged[Cdecl]<void> IN_ClearStates;
    public delegate* unmanaged[Cdecl]<void> IN_Accumulate;
    public delegate* unmanaged[Cdecl]<float, usercmd_s*, int, void> CL_CreateMove;
    public delegate* unmanaged[Cdecl]<int> CL_IsThirdPerson;
    public delegate* unmanaged[Cdecl]<Vector3f*, void> CL_GetCameraOffsets;
    public delegate* unmanaged[Cdecl]<sbyte*, kbutton_s*> KB_Find;
    public delegate* unmanaged[Cdecl]<void> CAM_Think;
    public delegate* unmanaged[Cdecl]<ref_params_s*, void> V_CalcRefdef;
    public delegate* unmanaged[Cdecl]<int, cl_entity_s*, sbyte*, int> HUD_AddEntity;
    public delegate* unmanaged[Cdecl]<void> HUD_CreateEntities;
    public delegate* unmanaged[Cdecl]<void> HUD_DrawNormalTriangles;
    public delegate* unmanaged[Cdecl]<void> HUD_DrawTransparentTriangles;
    public delegate* unmanaged[Cdecl]<mstudioevent_s*, cl_entity_s*, void> HUD_StudioEvent;
    public delegate* unmanaged[Cdecl]<local_state_s*, local_state_s*, usercmd_s*, int, double, uint, void> HUD_PostRunCmd;
    public delegate* unmanaged[Cdecl]<void> HUD_Shutdown;
    public delegate* unmanaged[Cdecl]<entity_state_s*, clientdata_s*, void> HUD_TxferLocalOverrides;
    public delegate* unmanaged[Cdecl]<entity_state_s*, entity_state_s*, void> HUD_ProcessPlayerState;
    public delegate* unmanaged[Cdecl]<entity_state_s*, entity_state_s*, clientdata_s*, clientdata_s*, weapon_data_s*, weapon_data_s*, void> HUD_TxferPredictionData;
    public delegate* unmanaged[Cdecl]<int, byte*, void> Demo_ReadBuffer;
    public delegate* unmanaged[Cdecl]<netadr_s*, sbyte*, sbyte*, int*, int> HUD_ConnectionlessPacket;
    public delegate* unmanaged[Cdecl]<int, float*, float*, int> HUD_GetHullBounds;
    public delegate* unmanaged[Cdecl]<double, void> HUD_Frame;
    public delegate* unmanaged[Cdecl]<int, int, sbyte*, int> HUD_Key_Event;
    public delegate* unmanaged[Cdecl]<double, double, double, tempent_s**, tempent_s**, delegate* unmanaged[Cdecl]<cl_entity_s*, int>, delegate* unmanaged[Cdecl]<tempent_s*, float, void>, void> HUD_TempEntUpdate;
    public delegate* unmanaged[Cdecl]<int, cl_entity_s*> HUD_GetUserEntity;
    public delegate* unmanaged[Cdecl]<int, qboolean, void> HUD_VoiceStatus;
    public delegate* unmanaged[Cdecl]<int, void*, void> HUD_DirectorMessage;
    public delegate* unmanaged[Cdecl]<int, r_studio_interface_s**, engine_studio_api_s*, int> HUD_GetStudioModelInterface;
    public delegate* unmanaged[Cdecl]<int*, int*, void> HUD_ChatInputPosition;
    public delegate* unmanaged[Cdecl]<int, int> HUD_GetPlayerTeam;
    public delegate* unmanaged[Cdecl]<void*> ClientFactory;
}
