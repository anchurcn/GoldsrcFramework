using GoldsrcFramework.Configuration;
using GoldsrcFramework.DependencyInjection;
using GoldsrcFramework.Graphics;
using GoldsrcFramework.LinearMath;
using GoldsrcFramework.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NativeInterop;
using System.Text;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of client export functions based on LegacyClientInterop
/// </summary>
public unsafe class FrameworkClientExports : IClientExportFuncs
{
    // IClientExportFuncs implementation - all based on LegacyClientInterop
    public virtual int Initialize(ClientEngineFuncs* pEnginefuncs, int iVersion)
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
        EngineApi.DrawStringCenter("GoldsrcFrameworkDemo");
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

    public virtual void HUD_PlayerMove(playermove_t* ppmove, qboolean server)
    {
        LegacyClientInterop.HUD_PlayerMove(ppmove, server.Value);
    }

    public virtual void HUD_PlayerMoveInit(playermove_t* ppmove)
    {
        LegacyClientInterop.HUD_PlayerMoveInit(ppmove);
    }

    public virtual NChar HUD_PlayerMoveTexture(NChar* name)
    {
        sbyte result = LegacyClientInterop.HUD_PlayerMoveTexture((sbyte*)name);
        return new NChar((byte)result);
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

    public virtual void CL_CreateMove(float frametime, usercmd_t* cmd, int active)
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

    public virtual kbutton_t* KB_Find(NChar* name)
    {
        return LegacyClientInterop.KB_Find((sbyte*)name);
    }

    public virtual void CAM_Think()
    {
        LegacyClientInterop.CAM_Think();
    }

    public virtual void V_CalcRefdef(ref_params_t* pparams)
    {
        LegacyClientInterop.V_CalcRefdef(pparams);
    }

    public virtual int HUD_AddEntity(int type, cl_entity_t* ent, NChar* modelname)
    {
        return LegacyClientInterop.HUD_AddEntity(type, ent, (sbyte*)modelname);
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

    public virtual void HUD_StudioEvent(mstudioevent_t* @event, cl_entity_t* entity)
    {
        LegacyClientInterop.HUD_StudioEvent(@event, entity);
    }

    public virtual void HUD_PostRunCmd(local_state_t* from, local_state_t* to, usercmd_t* cmd, int runfuncs, double time, uint random_seed)
    {
        LegacyClientInterop.HUD_PostRunCmd(from, to, cmd, runfuncs, time, random_seed);
    }

    public virtual void HUD_Shutdown()
    {
        LegacyClientInterop.HUD_Shutdown();
    }

    public virtual void HUD_TxferLocalOverrides(entity_state_t* state, clientdata_t* client)
    {
        LegacyClientInterop.HUD_TxferLocalOverrides(state, client);
    }

    public virtual void HUD_ProcessPlayerState(entity_state_t* dst, entity_state_t* src)
    {
        LegacyClientInterop.HUD_ProcessPlayerState(dst, src);
    }

    public virtual void HUD_TxferPredictionData(entity_state_t* ps, entity_state_t* pps, clientdata_t* pcd, clientdata_t* ppcd, weapon_data_t* wd, weapon_data_t* pwd)
    {
        LegacyClientInterop.HUD_TxferPredictionData(ps, pps, pcd, ppcd, wd, pwd);
    }

    public virtual void Demo_ReadBuffer(int size, byte* buffer)
    {
        LegacyClientInterop.Demo_ReadBuffer(size, buffer);
    }

    public virtual int HUD_ConnectionlessPacket(netadr_t* net_from, NChar* args, NChar* response_buffer, int* response_buffer_size)
    {
        return LegacyClientInterop.HUD_ConnectionlessPacket(net_from, (sbyte*)args, (sbyte*)response_buffer, response_buffer_size);
    }

    public virtual int HUD_GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        return LegacyClientInterop.HUD_GetHullBounds(hullnumber, mins, maxs);
    }

    public virtual void HUD_Frame(double time)
    {
        LegacyClientInterop.HUD_Frame(time);
    }

    public virtual int HUD_Key_Event(int eventcode, int keynum, NChar* pszCurrentBinding)
    {
        return LegacyClientInterop.HUD_Key_Event(eventcode, keynum, pszCurrentBinding);
    }

    public virtual void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, TEMPENTITY** ppTempEntFree, TEMPENTITY** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_t*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<TEMPENTITY*, float, void> Callback_TempEntPlaySound)
    {
        LegacyClientInterop.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);
    }

    public virtual cl_entity_t* HUD_GetUserEntity(int index)
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

    public virtual int HUD_GetStudioModelInterface(int version, r_studio_interface_t** ppinterface, engine_studio_api_t* pstudio)
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

