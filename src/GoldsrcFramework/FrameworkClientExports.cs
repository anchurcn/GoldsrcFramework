using System.Runtime.CompilerServices;
using GoldsrcFramework.Configuration;
using GoldsrcFramework.DependencyInjection;
using GoldsrcFramework.Graphics;
using GoldsrcFramework.LinearMath;
using GoldsrcFramework.Physics;
using GoldsrcFramework.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NativeInterop;
using System.Text;

using SVector3 = Stride.Core.Mathematics.Vector3;
using SQuaternion = Stride.Core.Mathematics.Quaternion;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of client export functions based on LegacyClientInterop
/// </summary>
public unsafe class FrameworkClientExports : IClientExportFuncs
{
    private BepuPhysicsDemo? _physicsDemo;
    private double _physicsFrameTime;
    private bool _physicsDemoInitialized;

    // IClientExportFuncs implementation - all based on LegacyClientInterop
    public virtual int Initialize(ClientEngineFuncs* pEnginefuncs, int iVersion)
    {
        EngineApi.ClientApiInit(pEnginefuncs);
        return LegacyClientInterop.Initialize(pEnginefuncs, iVersion);
    }

    public virtual void HUD_Init()
    {
        LegacyClientInterop.HUD_Init();
        InitPhysicsDemo();
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
        DrawPhysicsDemo();
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
        UpdatePhysicsDemo(time);
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

    #region Physics Demo

    private void InitPhysicsDemo()
    {
        try
        {
            _physicsDemo = new BepuPhysicsDemo();
            _physicsDemo.Initialize();
            _physicsFrameTime = 0;
            _physicsDemoInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BepuPhysicsDemo] Init failed: {ex.Message}");
        }
    }

    private void UpdatePhysicsDemo(double time)
    {
        if (!_physicsDemoInitialized || _physicsDemo == null)
            return;

        try
        {
            float deltaTime;
            if (_physicsFrameTime > 0)
            {
                deltaTime = (float)(time - _physicsFrameTime);
            }
            else
            {
                deltaTime = 1f / 60f;
            }
            _physicsFrameTime = time;

            _physicsDemo.Update(deltaTime);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BepuPhysicsDemo] Update failed: {ex.Message}");
        }
    }

    private void DrawPhysicsDemo()
    {
        if (!_physicsDemoInitialized || _physicsDemo == null)
            return;

        var triApi = EngineApi.PClient->pTriAPI;
        if (triApi == null)
            return;

        try
        {
            var pos = _physicsDemo.BoxPosition;
            var rot = _physicsDemo.BoxRotation;

            // Convert Stride types to System.Numerics for math operations
            var nPos = Unsafe.As<SVector3, System.Numerics.Vector3>(ref pos);
            var nRot = Unsafe.As<SQuaternion, System.Numerics.Quaternion>(ref rot);

            // Draw a wireframe box using the triangle API
            float half = 10f;

            // Calculate 8 corners of the box in local space
            Span<System.Numerics.Vector3> corners = stackalloc System.Numerics.Vector3[8];
            corners[0] = new System.Numerics.Vector3(-half, -half, -half);
            corners[1] = new System.Numerics.Vector3( half, -half, -half);
            corners[2] = new System.Numerics.Vector3( half,  half, -half);
            corners[3] = new System.Numerics.Vector3(-half,  half, -half);
            corners[4] = new System.Numerics.Vector3(-half, -half,  half);
            corners[5] = new System.Numerics.Vector3( half, -half,  half);
            corners[6] = new System.Numerics.Vector3( half,  half,  half);
            corners[7] = new System.Numerics.Vector3(-half,  half,  half);

            // Transform corners by rotation and position
            for (int i = 0; i < 8; i++)
            {
                corners[i] = System.Numerics.Vector3.Transform(corners[i], nRot) + nPos;
            }

            // 12 edges of the box
            Span<(int, int)> edges = stackalloc (int, int)[12];
            edges[0] = (0, 1); edges[1] = (1, 2); edges[2] = (2, 3); edges[3] = (3, 0); // bottom face
            edges[4] = (4, 5); edges[5] = (5, 6); edges[6] = (6, 7); edges[7] = (7, 4); // top face
            edges[8] = (0, 4); edges[9] = (1, 5); edges[10] = (2, 6); edges[11] = (3, 7); // verticals

            triApi->RenderMode(5); // kRenderTransAdd
            triApi->Color4f(0f, 1f, 0f, 1f); // green
            triApi->Brightness(1f);
            triApi->CullFace(TRICULLSTYLE.TRI_NONE);

            triApi->Begin(4); // LINES
            for (int i = 0; i < 12; i++)
            {
                var (a, b) = edges[i];
                triApi->Vertex3f(corners[a].X, corners[a].Y, corners[a].Z);
                triApi->Vertex3f(corners[b].X, corners[b].Y, corners[b].Z);
            }
            triApi->End();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BepuPhysicsDemo] Draw failed: {ex.Message}");
        }
    }

    #endregion
}

