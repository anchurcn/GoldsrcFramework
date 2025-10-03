using System.Text;
using GoldsrcFramework.LinearMath;
using GoldsrcFramework.Rendering;
using NativeInterop;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of client export functions based on LegacyClientInterop
/// </summary>
public unsafe class FrameworkClientExports : IClientExportFuncs
{
    // IClientExportFuncs implementation - all based on LegacyClientInterop
    public virtual int Initialize(cl_enginefunc_t* pEnginefuncs, int iVersion)
    {
        EngineApi.ClientApiInit(pEnginefuncs);
        return LegacyClientInterop.Initialize(pEnginefuncs, iVersion);
    }

    public virtual void HUD_Init()
    {
        LegacyClientInterop.HUD_Init();
    }

    public virtual int HUD_VidInit()
    {
        return LegacyClientInterop.HUD_VidInit();
    }

    public virtual int HUD_Redraw(float flTime, int intermission)
    {
        Span<byte> msg = stackalloc byte[32];
        Encoding.UTF8.GetBytes("GoldsrcFrameworkDemo", msg);
        fixed (byte* ptr = msg)
        {
            EngineApi.PClient->CenterPrint((NChar*)ptr);
        }
        return LegacyClientInterop.HUD_Redraw(flTime, intermission);
    }

    public virtual int HUD_UpdateClientData(client_data_t* cdata, float flTime)
    {
        return LegacyClientInterop.HUD_UpdateClientData(cdata, flTime);
    }

    public virtual void HUD_Reset()
    {
        LegacyClientInterop.HUD_Reset();
    }

    public virtual void HUD_PlayerMove(playermove_s* ppmove, int server)
    {
        LegacyClientInterop.HUD_PlayerMove(ppmove, server);
    }

    public virtual void HUD_PlayerMoveInit(playermove_s* ppmove)
    {
        LegacyClientInterop.HUD_PlayerMoveInit(ppmove);
    }

    public virtual sbyte HUD_PlayerMoveTexture(sbyte* name)
    {
        return LegacyClientInterop.HUD_PlayerMoveTexture(name);
    }

    public virtual void IN_ActivateMouse()
    {
        LegacyClientInterop.IN_ActivateMouse();
    }

    public virtual void IN_DeactivateMouse()
    {
        LegacyClientInterop.IN_DeactivateMouse();
    }

    public virtual void IN_MouseEvent(int mstate)
    {
        LegacyClientInterop.IN_MouseEvent(mstate);
    }

    public virtual void IN_ClearStates()
    {
        LegacyClientInterop.IN_ClearStates();
    }

    public virtual void IN_Accumulate()
    {
        LegacyClientInterop.IN_Accumulate();
    }

    public virtual void CL_CreateMove(float frametime, usercmd_s* cmd, int active)
    {
        LegacyClientInterop.CL_CreateMove(frametime, cmd, active);
    }

    public virtual int CL_IsThirdPerson()
    {
        return LegacyClientInterop.CL_IsThirdPerson();
    }

    public virtual void CL_GetCameraOffsets(Vector3* ofs)
    {
        LegacyClientInterop.CL_GetCameraOffsets(ofs);
    }

    public virtual kbutton_s* KB_Find(sbyte* name)
    {
        return LegacyClientInterop.KB_Find(name);
    }

    public virtual void CAM_Think()
    {
        LegacyClientInterop.CAM_Think();
    }

    public virtual void V_CalcRefdef(ref_params_s* pparams)
    {
        LegacyClientInterop.V_CalcRefdef(pparams);
    }

    public virtual int HUD_AddEntity(int type, cl_entity_s* ent, sbyte* modelname)
    {
        return LegacyClientInterop.HUD_AddEntity(type, ent, modelname);
    }

    public virtual void HUD_CreateEntities()
    {
        LegacyClientInterop.HUD_CreateEntities();
    }

    public virtual void HUD_DrawNormalTriangles()
    {
        LegacyClientInterop.HUD_DrawNormalTriangles();
    }

    public virtual void HUD_DrawTransparentTriangles()
    {
        LegacyClientInterop.HUD_DrawTransparentTriangles();
    }

    public virtual void HUD_StudioEvent(mstudioevent_s* @event, cl_entity_s* entity)
    {
        LegacyClientInterop.HUD_StudioEvent(@event, entity);
    }

    public virtual void HUD_PostRunCmd(local_state_s* from, local_state_s* to, usercmd_s* cmd, int runfuncs, double time, uint random_seed)
    {
        LegacyClientInterop.HUD_PostRunCmd(from, to, cmd, runfuncs, time, random_seed);
    }

    public virtual void HUD_Shutdown()
    {
        LegacyClientInterop.HUD_Shutdown();
    }

    public virtual void HUD_TxferLocalOverrides(entity_state_s* state, clientdata_s* client)
    {
        LegacyClientInterop.HUD_TxferLocalOverrides(state, client);
    }

    public virtual void HUD_ProcessPlayerState(entity_state_s* dst, entity_state_s* src)
    {
        LegacyClientInterop.HUD_ProcessPlayerState(dst, src);
    }

    public virtual void HUD_TxferPredictionData(entity_state_s* ps, entity_state_s* pps, clientdata_s* pcd, clientdata_s* ppcd, weapon_data_s* wd, weapon_data_s* pwd)
    {
        LegacyClientInterop.HUD_TxferPredictionData(ps, pps, pcd, ppcd, wd, pwd);
    }

    public virtual void Demo_ReadBuffer(int size, byte* buffer)
    {
        LegacyClientInterop.Demo_ReadBuffer(size, buffer);
    }

    public virtual int HUD_ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
    {
        return LegacyClientInterop.HUD_ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
    }

    public virtual int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        return LegacyClientInterop.HUD_GetHullBounds(hullnumber, mins, maxs);
    }

    public virtual void HUD_Frame(double time)
    {
        LegacyClientInterop.HUD_Frame(time);
    }

    public virtual int HUD_Key_Event(int eventcode, int keynum, sbyte* pszCurrentBinding)
    {
        return LegacyClientInterop.HUD_Key_Event(eventcode, keynum, pszCurrentBinding);
    }

    public virtual void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, tempent_s** ppTempEntFree, tempent_s** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_s*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<tempent_s*, float, void> Callback_TempEntPlaySound)
    {
        LegacyClientInterop.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);
    }

    public virtual cl_entity_s* HUD_GetUserEntity(int index)
    {
        return LegacyClientInterop.HUD_GetUserEntity(index);
    }

    public virtual void HUD_VoiceStatus(int entindex, qboolean bTalking)
    {
        LegacyClientInterop.HUD_VoiceStatus(entindex, bTalking);
    }

    public virtual void HUD_DirectorMessage(int iSize, void* pbuf)
    {
        LegacyClientInterop.HUD_DirectorMessage(iSize, pbuf);
    }

    public virtual int HUD_GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio)
    {
        //return LegacyClientInterop.HUD_GetStudioModelInterface(version, ppinterface, pstudio);
        return StudioModelRenderer.GetStudioModelInterface(version, ppinterface, pstudio);
    }

    public virtual void HUD_ChatInputPosition(int* x, int* y)
    {
        LegacyClientInterop.HUD_ChatInputPosition(x, y);
    }

    public virtual int HUD_GetPlayerTeam(int iplayer)
    {
        return LegacyClientInterop.HUD_GetPlayerTeam(iplayer);
    }

    public virtual void* ClientFactory()
    {
        return LegacyClientInterop.ClientFactory();
    }
}
