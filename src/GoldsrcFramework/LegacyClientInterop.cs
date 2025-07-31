using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework
{
    internal unsafe static class LegacyClientInterop
    {
        const string LegacyClientDll = "libclient.dll";
        [DllImport(LegacyClientDll,CallingConvention = CallingConvention.Cdecl)]
        internal extern static void FF(void* pv);

        [DllImport(LegacyClientDll,CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_PostRunCmd(void* from, void* to, void* cmd, int runfuncs, long time, int random_seed);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int Initialize(void* pEnginefuncs, int iVersion);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_VidInit();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_Init();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_Redraw(int flTime, int intermission);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_UpdateClientData(void* cdata, int flTime);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_Reset();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_PlayerMove(void* ppmove, int server);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_PlayerMoveInit(void* ppmove);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static byte HUD_PlayerMoveTexture(void* name);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_ConnectionlessPacket(void* net_from, void* args, void* response_buffer, void* response_buffer_size);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_GetHullBounds(int hullnumber, void* mins, void* maxs);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_Frame(long time);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_VoiceStatus(int entindex, int bTalking);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_DirectorMessage(int iSize, void* pbuf);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_ChatInputPosition(void* x, void* y);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void Demo_ReadBuffer(int size, void* buffer);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_AddEntity(int type, void* ent, void* modelname);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_CreateEntities();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_StudioEvent(void* @event, void* entity);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_TxferLocalOverrides(void* state, void* client);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_ProcessPlayerState(void* dst, void* src);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_TxferPredictionData(void* ps, void* pps, void* pcd, void* ppcd, void* wd, void* pwd);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_TempEntUpdate(long frametime, long client_time, long cl_gravity, void* ppTempEntFree, void* ppTempEntActive, void* Callback_AddVisibleEntity, void* Callback_TempEntPlaySound);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void* HUD_GetUserEntity(int index);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void CAM_Think();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int CL_IsThirdPerson();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void CL_CameraOffset(void* ofs);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void* KB_Find(void* name);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void CL_CreateMove(int frametime, void* cmd, int active);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_Shutdown();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_Key_Event(int eventcode, int keynum, void* pszCurrentBinding);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void IN_ActivateMouse();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void IN_DeactivateMouse();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void IN_MouseEvent(int mstate);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void IN_Accumulate();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void IN_ClearStates();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_DrawNormalTriangles();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void HUD_DrawTransparentTriangles();
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void V_CalcRefdef(void* pparams);
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static int HUD_GetStudioModelInterface(int version, void* ppinterface, void* pstudio);

    }
}
