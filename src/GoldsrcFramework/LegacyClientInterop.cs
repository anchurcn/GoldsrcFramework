using GoldsrcFramework.Engine.Native;
using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework
{
    /// <summary>
    /// 与原版 unmanaged client.dll 交互的互操作类
    /// </summary>
    public unsafe static class LegacyClientInterop
    {
        private const string LegacyClientDll = "libclient.dll";

        // 声明原版 client.dll 的导出函数，函数签名与 IClientExportFuncs 对齐
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Initialize(cl_enginefunc_t* pEnginefuncs, int iVersion);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_Init();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_VidInit();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_Redraw(float flTime, int intermission);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_UpdateClientData(client_data_t* cdata, float flTime);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_Reset();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_PlayerMove(playermove_s* ppmove, int server);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_PlayerMoveInit(playermove_s* ppmove);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte HUD_PlayerMoveTexture(sbyte* name);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IN_ActivateMouse();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IN_DeactivateMouse();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IN_MouseEvent(int mstate);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IN_ClearStates();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IN_Accumulate();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CL_CreateMove(float frametime, usercmd_s* cmd, int active);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CL_IsThirdPerson();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CL_GetCameraOffsets(Vector3f* ofs);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern kbutton_s* KB_Find(sbyte* name);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CAM_Think();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void V_CalcRefdef(ref_params_s* pparams);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_AddEntity(int type, cl_entity_s* ent, sbyte* modelname);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_CreateEntities();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_DrawNormalTriangles();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_DrawTransparentTriangles();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_StudioEvent(mstudioevent_s* @event, cl_entity_s* entity);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_PostRunCmd(local_state_s* from, local_state_s* to, usercmd_s* cmd, int runfuncs, double time, uint random_seed);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_Shutdown();

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_TxferLocalOverrides(entity_state_s* state, clientdata_s* client);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_ProcessPlayerState(entity_state_s* dst, entity_state_s* src);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_TxferPredictionData(entity_state_s* ps, entity_state_s* pps, clientdata_s* pcd, clientdata_s* ppcd, weapon_data_s* wd, weapon_data_s* pwd);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Demo_ReadBuffer(int size, byte* buffer);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_Frame(double time);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_Key_Event(int eventcode, int keynum, sbyte* pszCurrentBinding);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, tempent_s** ppTempEntFree, tempent_s** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_s*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<tempent_s*, float, void> Callback_TempEntPlaySound);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern cl_entity_s* HUD_GetUserEntity(int index);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_VoiceStatus(int entindex, qboolean bTalking);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_DirectorMessage(int iSize, void* pbuf);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void HUD_ChatInputPosition(int* x, int* y);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HUD_GetPlayerTeam(int iplayer);

        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void* ClientFactory();
    }
}
