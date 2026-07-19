using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoldsrcFramework;

using System.Text;
using GoldsrcFramework.Ecs;
using GoldsrcFramework.Ecs.Components;
using GoldsrcFramework.Ecs.Scripting;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Demo;

public unsafe class DemoClientExports : FrameworkClientExports
{
    private const string CreateScientistCommand = "gf_scientist_temp";
    private const string ScientistModel = "models/scientist.mdl";
    private const float BallCameraDistance = 128.0f;
    private const float BallCameraTargetHeight = 8.0f;
    private const int PlayerMoveWorldOnly = 1 << 3;
    private static DemoClientExports? current;
    private ClientEntityManager? entityManager;
    private double lastFrameTime;

    public override void HUD_Init()
    {
        base.HUD_Init();
        current = this;
        entityManager = ClientEntityManager.CreateDefault();
        RegisterCreateScientistCommand();
        EngineApi.DrawStringCenter($"Command registered: {CreateScientistCommand}");
    }

    public override void HUD_Frame(double time)
    {
        base.HUD_Frame(time);
    }

    public override void V_CalcRefdef(ref_params_t* parameters)
    {
        var orbitAngles = parameters->cmd != null
            ? parameters->cmd->viewangles
            : new Vector3(
                parameters->cl_viewangles[0],
                parameters->cl_viewangles[1],
                parameters->cl_viewangles[2]);

        base.V_CalcRefdef(parameters);

        var engine = EngineApi.PClient;
        var localPlayer = engine != null && engine->GetLocalPlayer != null
            ? engine->GetLocalPlayer()
            : null;
        var ballIndex = localPlayer != null ? localPlayer->curstate.iuser2 : 0;
        var ball = ballIndex > 0 && engine->GetEntityByIndex != null
            ? engine->GetEntityByIndex(ballIndex)
            : null;
        if (ball == null)
            return;

        var pitch = orbitAngles.X * (MathF.PI / 180.0f);
        var yaw = orbitAngles.Y * (MathF.PI / 180.0f);
        var cosPitch = MathF.Cos(pitch);
        var forward = new Vector3(
            cosPitch * MathF.Cos(yaw),
            cosPitch * MathF.Sin(yaw),
            -MathF.Sin(pitch));

        var target = ball->origin + new Vector3(0, 0, BallCameraTargetHeight);
        var desired = target - forward * BallCameraDistance;
        var cameraOrigin = TraceCamera(target, desired);

        parameters->vieworg = cameraOrigin;
        parameters->viewangles = orbitAngles;

        var right = default(Vector3);
        var up = default(Vector3);
        engine->AngleVectors((float*)&orbitAngles, (float*)&forward, (float*)&right, (float*)&up);
        parameters->forward = forward;
        parameters->right = right;
        parameters->up = up;
    }

    public override unsafe void HUD_TempEntUpdate(double frametime, double client_time, double cl_gravity, TEMPENTITY** ppTempEntFree, TEMPENTITY** ppTempEntActive, delegate* unmanaged[Cdecl]<cl_entity_t*, int> Callback_AddVisibleEntity, delegate* unmanaged[Cdecl]<TEMPENTITY*, float, void> Callback_TempEntPlaySound)
    {
        base.HUD_TempEntUpdate(frametime, client_time, cl_gravity, ppTempEntFree, ppTempEntActive, Callback_AddVisibleEntity, Callback_TempEntPlaySound);
        entityManager?.Update(new GameTime(TimeSpan.FromSeconds(client_time), TimeSpan.FromSeconds(frametime)));
    }

    public override void HUD_Shutdown()
    {
        entityManager = null;
        current = null;
        base.HUD_Shutdown();
    }

    private void RegisterCreateScientistCommand()
    {
        if (EngineApi.PClient == null || EngineApi.PClient->AddCommand == null)
            return;

        Span<byte> cmd = stackalloc byte[64];
        WriteNullTerminatedAscii(CreateScientistCommand, cmd);
        fixed (byte* cmdPtr = cmd)
        {
            EngineApi.PClient->AddCommand((NChar*)cmdPtr, &CreateScientistTempEntityCommand);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CreateScientistTempEntityCommand()
    {
        current?.CreateScientistTempEntity();
    }

    private void CreateScientistTempEntity()
    {
        if (entityManager == null || EngineApi.PClient == null || EngineApi.PClient->pEfxAPI == null)
            return;

        int modelIndex = 0;
        model_t* model;
        {
            var plr = EngineApi.PClient->GetLocalPlayer();
            model = plr->model;
            modelIndex = plr->curstate.modelindex;
        }

        if (model == null)
        {
            EngineApi.DrawStringCenter($"Load failed: {ScientistModel}");
            return;
        }

        var origin = GetSpawnOrigin();
        Vector3* originPtr = &origin;
        TEMPENTITY* temp = EngineApi.PClient->pEfxAPI->CL_TempEntAlloc((float*)originPtr, model);

        if (temp == null)
        {
            EngineApi.DrawStringCenter("CL_TempEntAlloc failed");
            return;
        }

        temp->die = EngineApi.PClient->GetClientTime() + 60.0f;
        temp->entity.model = model;
        temp->entity.origin = origin;
        temp->entity.angles = new Vector3(0, 0, 0);
        temp->entity.curstate.modelindex = modelIndex;
        temp->entity.curstate.sequence = 1;
        temp->entity.curstate.frame = 0;
        temp->entity.curstate.body = 0;

        var transform = new ClTransformComponent(&temp->entity);
        var modelComponent = new ClModelComponent(&temp->entity);
        var entity = new Stride.Engine.Entity("ecs_temp_scientist");
        entity.Components.Add(new ClEntityComponent(&temp->entity));
        entity.Components.Add(transform);
        entity.Components.Add(modelComponent);
        entity.Components.Add(new SpinTempEntityScript(transform, 180f));
        entityManager.AddEntity(entity);

        EngineApi.DrawStringCenter("ECS scientist temp entity created");
    }

    private static Vector3 GetSpawnOrigin()
    {
        var local = EngineApi.PClient->GetLocalPlayer != null ? EngineApi.PClient->GetLocalPlayer() : null;
        if (local == null)
            return new Vector3(0, 0, 32);

        var origin = local->origin;
        origin.X += 64;
        origin.Z += 8;
        return origin;
    }

    private static Vector3 TraceCamera(Vector3 start, Vector3 end)
    {
        var eventApi = EngineApi.PClient->pEventAPI;
        if (eventApi == null || eventApi->EV_SetTraceHull == null || eventApi->EV_PlayerTrace == null)
            return end;

        eventApi->EV_SetTraceHull(2);
        var trace = default(pmtrace_t);
        eventApi->EV_PlayerTrace((float*)&start, (float*)&end, PlayerMoveWorldOnly, -1, &trace);
        return trace.fraction < 1.0f
            ? trace.endpos + trace.plane.normal * 4.0f
            : end;
    }

    private static void WriteNullTerminatedAscii(string value, Span<byte> buffer)
    {
        buffer.Clear();
        var written = Encoding.ASCII.GetBytes(value, buffer);
        if (written < buffer.Length)
            buffer[written] = 0;
    }
}


