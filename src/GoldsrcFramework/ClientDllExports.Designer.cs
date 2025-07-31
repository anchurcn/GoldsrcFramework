using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using GoldsrcFramework.Engine;
using GoldsrcFramework.Engine.Native;

namespace GoldsrcFramework
{
    internal unsafe static class ClientExportsInterop
    {
        static IClientExportFuncs s_client = null!;

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="pv"></param>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void F(ClientExportFuncs* pv)
        {
            var pathEnvVar = Environment.GetEnvironmentVariable("Path");
            var cldllDir = Path.GetDirectoryName(typeof(ClientExportsInterop).Assembly.Location);
            pathEnvVar += ";" + cldllDir;
            Environment.SetEnvironmentVariable("Path", pathEnvVar);

            var modSettings = File.ReadAllText(Path.Combine(cldllDir, "modsettings.json"));
            var modSettingsObj = JsonSerializer.Deserialize<Dictionary<string, string>>(modSettings);
            var clientDllName = modSettingsObj["GameClientAssembly"];
            var clientAssemblyPath = Path.Combine(cldllDir, clientDllName);
            var clientAssembly = AssemblyLoadContext.GetLoadContext(typeof(ClientExportsInterop).Assembly).LoadFromAssemblyPath(clientAssemblyPath);

            // Find the first type in the assembly where implements ClientFuncs interface.
            var t = clientAssembly.GetTypes().FirstOrDefault(x => x.GetInterface(nameof(IClientExportFuncs)) == typeof(IClientExportFuncs));
            
            if (t is null)
            {
                s_client = new FrameworkClientExports();
            }
            else
            {
                var o = Activator.CreateInstance(t);
                s_client = (IClientExportFuncs)o;
            }

            ClientExportFuncs v = new ClientExportFuncs();

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
            v.CL_GetCameraOffsets = &CL_GetCameraOffset;
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
            v.HUD_GetPlayerTeam = &HUD_GetPlayerTeam;
            v.ClientFactory = &ClientFactory;

            *pv = v;
            System.Diagnostics.Debug.WriteLine("done");
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int Initialize(cl_enginefunc_t* pEnginefuncs, int iVersion)
        {
            return s_client.Initialize(pEnginefuncs, iVersion);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Init()
        {
            s_client.HUD_Init();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_VidInit()
        {
            return s_client.HUD_VidInit();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_Redraw(float flTime, int intermission)
        {
            return s_client.HUD_Redraw(flTime, intermission);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_UpdateClientData(client_data_t* cdata, float flTime)
        {
            return s_client.HUD_UpdateClientData(cdata, flTime);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Reset()
        {
            s_client.HUD_Reset();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_PlayerMove(playermove_s* ppmove, int server)
        {
            s_client.HUD_PlayerMove(ppmove, server);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_PlayerMoveInit(playermove_s* ppmove)
        {
            s_client.HUD_PlayerMoveInit(ppmove);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static sbyte HUD_PlayerMoveTexture(sbyte* name)
        {
            return s_client.HUD_PlayerMoveTexture(name);
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
        static void IN_ClearStates()
        {
            s_client.IN_ClearStates();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void IN_Accumulate()
        {
            s_client.IN_Accumulate();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CL_CreateMove(float frametime, usercmd_s* cmd, int active)
        {
            s_client.CL_CreateMove(frametime, cmd, active);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int CL_IsThirdPerson()
        {
            return s_client.CL_IsThirdPerson();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CL_GetCameraOffset(Vector3f* ofs)
        {
            s_client.CL_GetCameraOffsets(ofs);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static kbutton_s* KB_Find(sbyte* name)
        {
            return s_client.KB_Find(name);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CAM_Think()
        {
            s_client.CAM_Think();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void V_CalcRefdef(ref_params_s* pparams)
        {
            s_client.V_CalcRefdef(pparams);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_AddEntity(int type, cl_entity_s* ent, sbyte* modelname)
        {
            return s_client.HUD_AddEntity(type, ent, modelname);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_CreateEntities()
        {
            s_client.HUD_CreateEntities();
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
        static void HUD_StudioEvent(mstudioevent_s* @event, cl_entity_s* entity)
        {
            s_client.HUD_StudioEvent(@event, entity);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_PostRunCmd(local_state_s* from, local_state_s* to, usercmd_s* cmd, int runfuncs, double time, uint random_seed)
        {
            s_client.HUD_PostRunCmd(from, to, cmd, runfuncs, time, random_seed);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Shutdown()
        {
            s_client.HUD_Shutdown();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_TxferLocalOverrides(entity_state_s* state, clientdata_s* client)
        {
            s_client.HUD_TxferLocalOverrides(state, client);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_ProcessPlayerState(entity_state_s* dst, entity_state_s* src)
        {
            s_client.HUD_ProcessPlayerState(dst, src);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_TxferPredictionData(entity_state_s* ps, entity_state_s* pps, clientdata_s* pcd, clientdata_s* ppcd, weapon_data_s* wd, weapon_data_s* pwd)
        {
            s_client.HUD_TxferPredictionData(ps, pps, pcd, ppcd, wd, pwd);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Demo_ReadBuffer(int size, byte* buffer)
        {
            s_client.Demo_ReadBuffer(size, buffer);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
        {
            return s_client.HUD_ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs)
        {
            return s_client.HUD_GetHullBounds(hullnumber, mins, maxs);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_Frame(double time)
        {
            s_client.HUD_Frame(time);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_Key_Event(int down, int keynum, sbyte* pszCurrentBinding)
        {
            return s_client.HUD_Key_Event(down, keynum, pszCurrentBinding);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, tempent_s** ppTempEntFree, tempent_s** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_s*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<tempent_s*, float, void> Callback_TempEntPlaySound)
        {
            s_client.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static cl_entity_s* HUD_GetUserEntity(int index)
        {
            return s_client.HUD_GetUserEntity(index);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_VoiceStatus(int entindex, qboolean bTalking)
        {
            s_client.HUD_VoiceStatus(entindex, bTalking);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_DirectorMessage(int iSize, void* pbuf)
        {
            s_client.HUD_DirectorMessage(iSize, pbuf);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio)
        {
            return s_client.HUD_GetStudioModelInterface(version, ppinterface, pstudio);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void HUD_ChatInputPosition(int* x, int* y)
        {
            s_client.HUD_ChatInputPosition(x, y);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int HUD_GetPlayerTeam(int iplayer)
        {
            return s_client.HUD_GetPlayerTeam(iplayer);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void* ClientFactory()
        {
            return s_client.ClientFactory();
        }

    }
}
