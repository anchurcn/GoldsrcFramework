using GoldsrcFramework.Engine;
using System.Diagnostics;


namespace GoldsrcFramework.Demo
{
    public unsafe class LegacyClientFuncs : GoldsrcFramework.Engine.IClientExportFuncs
    {
        EngineApi* _pengfuncs = null;
        public void HUD_PostRunCmd(void* from, void* to, void* cmd, int runfuncs, long time, int random_seed)
        {//Debugger.Break();
            LegacyClientInterop.HUD_PostRunCmd(from, to, cmd, runfuncs, time, random_seed);
        }
        public  int Initialize(void* pEnginefuncs, int iVersion)
        {//Debugger.Break();
            _pengfuncs = (EngineApi*)pEnginefuncs;
            return LegacyClientInterop.Initialize(pEnginefuncs, iVersion);
        }
        public  int HUD_VidInit()
        {//Debugger.Break();
            return LegacyClientInterop.HUD_VidInit();
        }
        public  void HUD_Init()
        {//Debugger.Break();
            LegacyClientInterop.HUD_Init();
        }
        public  int HUD_Redraw(int flTime, int intermission)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_Redraw(flTime, intermission);
        }
        public  int HUD_UpdateClientData(void* cdata, int flTime)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_UpdateClientData(cdata, flTime);
        }
        public  void HUD_Reset()
        {//Debugger.Break();
            LegacyClientInterop.HUD_Reset();
        }
        public  void HUD_PlayerMove(void* ppmove, int server)
        {//Debugger.Break();
            LegacyClientInterop.HUD_PlayerMove(ppmove, server);
        }
        public  void HUD_PlayerMoveInit(void* ppmove)
        {//Debugger.Break();
            LegacyClientInterop.HUD_PlayerMoveInit(ppmove);
        }
        public  byte HUD_PlayerMoveTexture(void* name)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_PlayerMoveTexture(name);
        }
        public  int HUD_ConnectionlessPacket(void* net_from, void* args, void* response_buffer, void* response_buffer_size)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
        }
        public  int HUD_GetHullBounds(int hullnumber, void* mins, void* maxs)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_GetHullBounds(hullnumber, mins, maxs);
        }
        public  void HUD_Frame(long time)
        {//Debugger.Break();
            _pengfuncs->pfnConsolePrint(_pengfuncs->pfnGetGameDirectory());
            LegacyClientInterop.HUD_Frame(time);
        }
        public  void HUD_VoiceStatus(int entindex, int bTalking)
        {//Debugger.Break();
            LegacyClientInterop.HUD_VoiceStatus(entindex, bTalking);
        }
        public  void HUD_DirectorMessage(int iSize, void* pbuf)
        {//Debugger.Break();
            LegacyClientInterop.HUD_DirectorMessage(iSize, pbuf);
        }
        public  void HUD_ChatInputPosition(void* x, void* y)
        {//Debugger.Break();
            LegacyClientInterop.HUD_ChatInputPosition(x, y);
        }
        public  void Demo_ReadBuffer(int size, void* buffer)
        {//Debugger.Break();
            LegacyClientInterop.Demo_ReadBuffer(size, buffer);
        }
        public  int HUD_AddEntity(int type, void* ent, void* modelname)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_AddEntity(type, ent, modelname);
        }
        public  void HUD_CreateEntities()
        {//Debugger.Break();
            LegacyClientInterop.HUD_CreateEntities();
        }
        public  void HUD_StudioEvent(void* @event, void* entity)
        {//Debugger.Break();
            LegacyClientInterop.HUD_StudioEvent(@event, entity);
        }
        public  void HUD_TxferLocalOverrides(void* state, void* client)
        {//Debugger.Break();
            LegacyClientInterop.HUD_TxferLocalOverrides(state, client);
        }
        public  void HUD_ProcessPlayerState(void* dst, void* src)
        {//Debugger.Break();
            LegacyClientInterop.HUD_ProcessPlayerState(dst, src);
        }
        public  void HUD_TxferPredictionData(void* ps, void* pps, void* pcd, void* ppcd, void* wd, void* pwd)
        {//Debugger.Break();
            LegacyClientInterop.HUD_TxferPredictionData(ps, pps, pcd, ppcd, wd, pwd);
        }
        public  void HUD_TempEntUpdate(long frametime, long client_time, long cl_gravity, void* ppTempEntFree, void* ppTempEntActive, void* Callback_AddVisibleEntity, void* Callback_TempEntPlaySound)
        {//Debugger.Break();
            LegacyClientInterop.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);
        }
        public  void* HUD_GetUserEntity(int index)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_GetUserEntity(index);
        }
        public  void CAM_Think()
        {//Debugger.Break();
            LegacyClientInterop.CAM_Think();
        }
        public  int CL_IsThirdPerson()
        {//Debugger.Break();
            return LegacyClientInterop.CL_IsThirdPerson();
        }
        public  void CL_CameraOffset(void* ofs)
        {//Debugger.Break();
            LegacyClientInterop.CL_CameraOffset(ofs);
        }
        public  void* KB_Find(void* name)
        {//Debugger.Break();
            return LegacyClientInterop.KB_Find(name);
        }
        public  void CL_CreateMove(int frametime, void* cmd, int active)
        {//Debugger.Break();
            LegacyClientInterop.CL_CreateMove(frametime, cmd, active);
        }
        public  void HUD_Shutdown()
        {//Debugger.Break();
            LegacyClientInterop.HUD_Shutdown();
        }
        public  int HUD_Key_Event(int eventcode, int keynum, void* pszCurrentBinding)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_Key_Event(eventcode, keynum, pszCurrentBinding);
        }
        public  void IN_ActivateMouse()
        {//Debugger.Break();
            LegacyClientInterop.IN_ActivateMouse();
        }
        public  void IN_DeactivateMouse()
        {//Debugger.Break();
            LegacyClientInterop.IN_DeactivateMouse();
        }
        public  void IN_MouseEvent(int mstate)
        {//Debugger.Break();
            LegacyClientInterop.IN_MouseEvent(mstate);
        }
        public  void IN_Accumulate()
        {//Debugger.Break();
            LegacyClientInterop.IN_Accumulate();
        }
        public  void IN_ClearStates()
        {//Debugger.Break();
            LegacyClientInterop.IN_ClearStates();
        }
        public  void HUD_DrawNormalTriangles()
        {//Debugger.Break();
            LegacyClientInterop.HUD_DrawNormalTriangles();
        }
        public  void HUD_DrawTransparentTriangles()
        {//Debugger.Break();
            LegacyClientInterop.HUD_DrawTransparentTriangles();
        }
        public  void V_CalcRefdef(void* pparams)
        {//Debugger.Break();
            LegacyClientInterop.V_CalcRefdef(pparams);
        }
        public  int HUD_GetStudioModelInterface(int version, void* ppinterface, void* pstudio)
        {//Debugger.Break();
            return LegacyClientInterop.HUD_GetStudioModelInterface(version, ppinterface, pstudio);
        }

    }
}
