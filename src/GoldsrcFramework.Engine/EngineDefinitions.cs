using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework.Engine
{
    /// <summary>
    /// Client dll implement this abstract class for client game logic.
    /// </summary>
    public unsafe interface ClientFuncs
    {
        public void HUD_PostRunCmd(void* from, void* to, void* cmd, int runfuncs, long time, int random_seed);
        public int Initialize(void* pEnginefuncs, int iVersion);
        public int HUD_VidInit();
        public void HUD_Init();
        public int HUD_Redraw(int flTime, int intermission);
        public int HUD_UpdateClientData(void* cdata, int flTime);
        public void HUD_Reset();
        public void HUD_PlayerMove(void* ppmove, int server);
        public void HUD_PlayerMoveInit(void* ppmove);
        public byte HUD_PlayerMoveTexture(void* name);
        public int HUD_ConnectionlessPacket(void* net_from, void* args, void* response_buffer, void* response_buffer_size);
        public int HUD_GetHullBounds(int hullnumber, void* mins, void* maxs);
        public void HUD_Frame(long time);
        public void HUD_VoiceStatus(int entindex, int bTalking);
        public void HUD_DirectorMessage(int iSize, void* pbuf);
        public void HUD_ChatInputPosition(void* x, void* y);
        public void Demo_ReadBuffer(int size, void* buffer);
        public int HUD_AddEntity(int type, void* ent, void* modelname);
        public void HUD_CreateEntities();
        public void HUD_StudioEvent(void* @event, void* entity);
        public void HUD_TxferLocalOverrides(void* state, void* client);
        public void HUD_ProcessPlayerState(void* dst, void* src);
        public void HUD_TxferPredictionData(void* ps, void* pps, void* pcd, void* ppcd, void* wd, void* pwd);
        public void HUD_TempEntUpdate(long frametime, long client_time, long cl_gravity, void* ppTempEntFree, void* ppTempEntActive, void* Callback_AddVisibleEntity, void* Callback_TempEntPlaySound);
        public void* HUD_GetUserEntity(int index);
        public void CAM_Think();
        public int CL_IsThirdPerson();
        public void CL_CameraOffset(void* ofs);
        public void* KB_Find(void* name);
        public void CL_CreateMove(int frametime, void* cmd, int active);
        public void HUD_Shutdown();
        public int HUD_Key_Event(int eventcode, int keynum, void* pszCurrentBinding);
        public void IN_ActivateMouse();
        public void IN_DeactivateMouse();
        public void IN_MouseEvent(int mstate);
        public void IN_Accumulate();
        public void IN_ClearStates();
        public void HUD_DrawNormalTriangles();
        public void HUD_DrawTransparentTriangles();
        public void V_CalcRefdef(void* pparams);
        public int HUD_GetStudioModelInterface(int version, void* ppinterface, void* pstudio);

    }
}
