using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using GoldsrcFramework.Engine;

namespace GoldsrcFramework
{
    internal unsafe static class ClientDllExportsInternal
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct clfuncs
        {
            public delegate* unmanaged[Cdecl]<void*, int, int> Initialize;
            public delegate* unmanaged[Cdecl]<void> HUD_Init;
            public delegate* unmanaged[Cdecl]<int> HUD_VidInit;
            public delegate* unmanaged[Cdecl]<int, int, int> HUD_Redraw;
            public delegate* unmanaged[Cdecl]<void*, int, int> HUD_UpdateClientData;
            public delegate* unmanaged[Cdecl]<void> HUD_Reset;
            public delegate* unmanaged[Cdecl]<void*, int, void> HUD_PlayerMove;
            public delegate* unmanaged[Cdecl]<void*, void> HUD_PlayerMoveInit;
            public delegate* unmanaged[Cdecl]<void*, byte> HUD_PlayerMoveTexture;
            public delegate* unmanaged[Cdecl]<void> IN_ActivateMouse;
            public delegate* unmanaged[Cdecl]<void> IN_DeactivateMouse;
            public delegate* unmanaged[Cdecl]<int, void> IN_MouseEvent;
            public delegate* unmanaged[Cdecl]<void> IN_ClearStates;
            public delegate* unmanaged[Cdecl]<void> IN_Accumulate;
            public delegate* unmanaged[Cdecl]<int, void*, int, void> CL_CreateMove;
            public delegate* unmanaged[Cdecl]<int> CL_IsThirdPerson;
            public delegate* unmanaged[Cdecl]<void*, void> CL_CameraOffset;
            public delegate* unmanaged[Cdecl]<void*, void*> KB_Find;
            public delegate* unmanaged[Cdecl]<void> CAM_Think;
            public delegate* unmanaged[Cdecl]<void*, void> V_CalcRefdef;
            public delegate* unmanaged[Cdecl]<int, void*, void*, int> HUD_AddEntity;
            public delegate* unmanaged[Cdecl]<void> HUD_CreateEntities;
            public delegate* unmanaged[Cdecl]<void> HUD_DrawNormalTriangles;
            public delegate* unmanaged[Cdecl]<void> HUD_DrawTransparentTriangles;
            public delegate* unmanaged[Cdecl]<void*, void*, void> HUD_StudioEvent;
            public delegate* unmanaged[Cdecl]<void*, void*, void*, int, long, int, void> HUD_PostRunCmd;
            public delegate* unmanaged[Cdecl]<void> HUD_Shutdown;
            public delegate* unmanaged[Cdecl]<void*, void*, void> HUD_TxferLocalOverrides;
            public delegate* unmanaged[Cdecl]<void*, void*, void> HUD_ProcessPlayerState;
            public delegate* unmanaged[Cdecl]<void*, void*, void*, void*, void*, void*, void> HUD_TxferPredictionData;
            public delegate* unmanaged[Cdecl]<int, void*, void> Demo_ReadBuffer;
            public delegate* unmanaged[Cdecl]<void*, void*, void*, void*, int> HUD_ConnectionlessPacket;
            public delegate* unmanaged[Cdecl]<int, void*, void*, int> HUD_GetHullBounds;
            public delegate* unmanaged[Cdecl]<long, void> HUD_Frame;
            public delegate* unmanaged[Cdecl]<int, int, void*, int> HUD_Key_Event;
            public delegate* unmanaged[Cdecl]<long, long, long, void*, void*, void*, void*, void> HUD_TempEntUpdate;
            public delegate* unmanaged[Cdecl]<int, void*> HUD_GetUserEntity;
            public delegate* unmanaged[Cdecl]<int, int, void> HUD_VoiceStatus;
            public delegate* unmanaged[Cdecl]<int, void*, void> HUD_DirectorMessage;
            public delegate* unmanaged[Cdecl]<int, void*, void*, int> HUD_GetStudioModelInterface;
            public delegate* unmanaged[Cdecl]<void*, void*, void> HUD_ChatInputPosition;

        }

        static ClientFuncs s_client = null!;
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void F(clfuncs* pv)
        {
            var pathEnvVar = Environment.GetEnvironmentVariable("Path");
            var cldllDir = Path.GetDirectoryName(typeof(clfuncs).Assembly.Location);
            pathEnvVar += ";" + cldllDir;
            Environment.SetEnvironmentVariable("Path", pathEnvVar);

            // TODO: Get <GameClient.dll> name from modsettings.json.
            var clientAssemblyPath = Path.Combine(cldllDir, "GoldsrcFramework.Demo.dll");
            var clientAssembly = AssemblyLoadContext.GetLoadContext(typeof(clfuncs).Assembly).LoadFromAssemblyPath(clientAssemblyPath);

            // Find the first type in the assembly where implements ClientFuncs interface.
            var t = clientAssembly.GetTypes().FirstOrDefault(x => x.GetInterface(nameof(ClientFuncs)) == typeof(ClientFuncs));
            
            var o = Activator.CreateInstance(t);
            s_client = o as ClientFuncs;

            var cl = (ClientFuncs)o;

            clfuncs v = new clfuncs();

            v.Initialize = &Initialize;
            v.HUD_Init = &HUD_Init;
            v.HUD_VidInit = &HUD_VidInit;
            v.HUD_Redraw = &HUD_Redraw;
            v.HUD_UpdateClientData = &HUD_UpdateClientData;
            v.HUD_Reset = &HUD_Reset;
            v.HUD_PlayerMove = &HUD_PlayerMove;
            v.HUD_PlayerMoveInit = &HUD_PlayerMoveInit;
            v.HUD_PlayerMoveTexture = &HUD_PlayerMoveTexture;
            v.IN_ActivateMouse = &IN_ActivateMouse;
            v.IN_DeactivateMouse = &IN_DeactivateMouse;
            v.IN_MouseEvent = &IN_MouseEvent;
            v.IN_ClearStates = &IN_ClearStates;
            v.IN_Accumulate = &IN_Accumulate;
            v.CL_CreateMove = &CL_CreateMove;
            v.CL_IsThirdPerson = &CL_IsThirdPerson;
            v.CL_CameraOffset = &CL_CameraOffset;
            v.KB_Find = &KB_Find;
            v.CAM_Think = &CAM_Think;
            v.V_CalcRefdef = &V_CalcRefdef;
            v.HUD_AddEntity = &HUD_AddEntity;
            v.HUD_CreateEntities = &HUD_CreateEntities;
            v.HUD_DrawNormalTriangles = &HUD_DrawNormalTriangles;
            v.HUD_DrawTransparentTriangles = &HUD_DrawTransparentTriangles;
            v.HUD_StudioEvent = &HUD_StudioEvent;
            v.HUD_PostRunCmd = &HUD_PostRunCmd;
            v.HUD_Shutdown = &HUD_Shutdown;
            v.HUD_TxferLocalOverrides = &HUD_TxferLocalOverrides;
            v.HUD_ProcessPlayerState = &HUD_ProcessPlayerState;
            v.HUD_TxferPredictionData = &HUD_TxferPredictionData;
            v.Demo_ReadBuffer = &Demo_ReadBuffer;
            v.HUD_ConnectionlessPacket = &HUD_ConnectionlessPacket;
            v.HUD_GetHullBounds = &HUD_GetHullBounds;
            v.HUD_Frame = &HUD_Frame;
            v.HUD_Key_Event = &HUD_Key_Event;
            v.HUD_TempEntUpdate = &HUD_TempEntUpdate;
            v.HUD_GetUserEntity = &HUD_GetUserEntity;
            v.HUD_VoiceStatus = &HUD_VoiceStatus;
            v.HUD_DirectorMessage = &HUD_DirectorMessage;
            v.HUD_GetStudioModelInterface = &HUD_GetStudioModelInterface;
            v.HUD_ChatInputPosition = &HUD_ChatInputPosition;

            *pv = v;
            System.Diagnostics.Debug.WriteLine("done");
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_PostRunCmd(void* from, void* to, void* cmd, int runfuncs, long time, int random_seed)
        {
            s_client.HUD_PostRunCmd(from, to, cmd, runfuncs, time, random_seed);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int Initialize(void* pEnginefuncs, int iVersion)
        {
            var e = Environment.CommandLine;
            return s_client.Initialize(pEnginefuncs, iVersion);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_VidInit()
        {
            return s_client.HUD_VidInit();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Init()
        {
            s_client.HUD_Init();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_Redraw(int flTime, int intermission)
        {
            return s_client.HUD_Redraw(flTime, intermission);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_UpdateClientData(void* cdata, int flTime)
        {
            return s_client.HUD_UpdateClientData(cdata, flTime);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Reset()
        {
            s_client.HUD_Reset();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_PlayerMove(void* ppmove, int server)
        {
            s_client.HUD_PlayerMove(ppmove, server);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_PlayerMoveInit(void* ppmove)
        {
            s_client.HUD_PlayerMoveInit(ppmove);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static byte HUD_PlayerMoveTexture(void* name)
        {
            return s_client.HUD_PlayerMoveTexture(name);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_ConnectionlessPacket(void* net_from, void* args, void* response_buffer, void* response_buffer_size)
        {
            return s_client.HUD_ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_GetHullBounds(int hullnumber, void* mins, void* maxs)
        {
            return s_client.HUD_GetHullBounds(hullnumber, mins, maxs);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Frame(long time)
        {
            s_client.HUD_Frame(time);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_VoiceStatus(int entindex, int bTalking)
        {
            s_client.HUD_VoiceStatus(entindex, bTalking);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_DirectorMessage(int iSize, void* pbuf)
        {
            s_client.HUD_DirectorMessage(iSize, pbuf);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_ChatInputPosition(void* x, void* y)
        {
            s_client.HUD_ChatInputPosition(x, y);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Demo_ReadBuffer(int size, void* buffer)
        {
            s_client.Demo_ReadBuffer(size, buffer);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_AddEntity(int type, void* ent, void* modelname)
        {
            return s_client.HUD_AddEntity(type, ent, modelname);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_CreateEntities()
        {
            s_client.HUD_CreateEntities();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_StudioEvent(void* @event, void* entity)
        {
            s_client.HUD_StudioEvent(@event, entity);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_TxferLocalOverrides(void* state, void* client)
        {
            s_client.HUD_TxferLocalOverrides(state, client);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_ProcessPlayerState(void* dst, void* src)
        {
            s_client.HUD_ProcessPlayerState(dst, src);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_TxferPredictionData(void* ps, void* pps, void* pcd, void* ppcd, void* wd, void* pwd)
        {
            s_client.HUD_TxferPredictionData(ps, pps, pcd, ppcd, wd, pwd);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_TempEntUpdate(long frametime, long client_time, long cl_gravity, void* ppTempEntFree, void* ppTempEntActive, void* Callback_AddVisibleEntity, void* Callback_TempEntPlaySound)
        {
            s_client.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void* HUD_GetUserEntity(int index)
        {
            return s_client.HUD_GetUserEntity(index);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CAM_Think()
        {
            s_client.CAM_Think();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int CL_IsThirdPerson()
        {
            return s_client.CL_IsThirdPerson();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CL_CameraOffset(void* ofs)
        {
            s_client.CL_CameraOffset(ofs);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void* KB_Find(void* name)
        {
            return s_client.KB_Find(name);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CL_CreateMove(int frametime, void* cmd, int active)
        {
            s_client.CL_CreateMove(frametime, cmd, active);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Shutdown()
        {
            s_client.HUD_Shutdown();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_Key_Event(int eventcode, int keynum, void* pszCurrentBinding)
        {
            return s_client.HUD_Key_Event(eventcode, keynum, pszCurrentBinding);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void IN_ActivateMouse()
        {
            s_client.IN_ActivateMouse();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void IN_DeactivateMouse()
        {
            s_client.IN_DeactivateMouse();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void IN_MouseEvent(int mstate)
        {
            s_client.IN_MouseEvent(mstate);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void IN_Accumulate()
        {
            s_client.IN_Accumulate();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void IN_ClearStates()
        {
            s_client.IN_ClearStates();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_DrawNormalTriangles()
        {
            s_client.HUD_DrawNormalTriangles();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_DrawTransparentTriangles()
        {
            s_client.HUD_DrawTransparentTriangles();
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void V_CalcRefdef(void* pparams)
        {
            s_client.V_CalcRefdef(pparams);
        }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_GetStudioModelInterface(int version, void* ppinterface, void* pstudio)
        {
            return s_client.HUD_GetStudioModelInterface(version, ppinterface, pstudio);
        }

    }
}
