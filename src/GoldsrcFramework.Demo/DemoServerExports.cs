using System.Text;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;

namespace GoldsrcFramework.Demo;

/// <summary>
/// Minimal server-side ball experiment implemented directly on engine edicts.
/// No CBaseEntity instances or legacy private data are used by this implementation.
/// </summary>
public unsafe class DemoServerExports : FrameworkServerExports
{
    private const int MaxPlayers = 32;
    private const float BallSpeed = 300.0f;
    private const float CameraDistance = 128.0f;
    private const float CameraTargetHeight = 8.0f;

    private const int InForward = 1 << 3;
    private const int InBack = 1 << 4;
    private const int InMoveLeft = 1 << 9;
    private const int InMoveRight = 1 << 10;

    private const int MoveTypeFly = 5;
    private const int MoveTypeNoClip = 8;
    private const int SolidNot = 0;
    private const int SolidBBox = 2;
    private const int EffectNoInterp = 32;
    private const int EffectNoDraw = 128;
    private const int FlagFrozen = 1 << 12;

    private readonly nint[] balls = new nint[MaxPlayers];
    private readonly nint[] cameras = new nint[MaxPlayers];

    public override void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax)
    {
        base.ServerActivate(pEdictList, edictCount, clientMax);
        Array.Clear(balls);
        Array.Clear(cameras);
    }

    public override void ServerDeactivate()
    {
        Array.Clear(balls);
        Array.Clear(cameras);
        base.ServerDeactivate();
    }

    public override void ClientPutInServer(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0)
            return;

        RemoveCamera(playerIndex, player);
        RemoveBall(playerIndex);

        var engine = EngineApi.PServer;
        var ball = engine->CreateEntity();
        if (ball == null)
            return;

        ball->v.classname = AllocString("ball_entity");
        ball->v.solid = SolidNot;
        ball->v.movetype = MoveTypeFly;
        ball->v.takedamage = 0;
        ball->v.effects = EffectNoInterp;
        ball->v.rendermode = 0;
        ball->v.renderamt = 255;
        ball->v.renderfx = 0;

        SetModel(ball, "models/w_grenade.mdl");
        SetSize(ball, new Vector3(-16, -16, -16), new Vector3(16, 16, 16));

        var origin = new Vector3(
            engine->RandomFloat(-200, 200),
            engine->RandomFloat(-200, 200),
            32);
        SetOrigin(ball, origin);
        ball->v.velocity = default;
        balls[playerIndex] = (nint)ball;

        var camera = engine->CreateEntity();
        if (camera == null)
        {
            RemoveBall(playerIndex);
            return;
        }

        camera->v.classname = AllocString("ball_camera");
        camera->v.solid = SolidNot;
        camera->v.movetype = MoveTypeNoClip;
        camera->v.effects = EffectNoDraw;
        cameras[playerIndex] = (nint)camera;

        player->v.health = 100;
        player->v.deadflag = 0;
        player->v.solid = SolidBBox;
        player->v.movetype = MoveTypeFly;
        player->v.effects |= EffectNoDraw;
        player->v.takedamage = 0;
        player->v.flags &= ~FlagFrozen;
        player->v.iuser1 = 0;
        player->v.iuser2 = 0;
        player->v.view_ofs = default;
        player->v.fixangle = 0;
        player->v.maxspeed = BallSpeed;
        player->v.gravity = 0;
        SetSize(player, new Vector3(-16, -16, -16), new Vector3(16, 16, 16));
        SetOrigin(player, origin);
        UpdateCamera(playerIndex, player);
        engine->SetView(player, camera);
    }

    public override void ClientDisconnect(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex >= 0)
        {
            RemoveCamera(playerIndex, player);
            RemoveBall(playerIndex);
        }

        base.ClientDisconnect(player);
    }

    public override void PlayerPreThink(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0)
            return;

        if (!HasActiveBallAndCamera(playerIndex))
            return;

        var yaw = player->v.v_angle.Y * (MathF.PI / 180.0f);
        var forward = new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0);
        var right = new Vector3(MathF.Sin(yaw), -MathF.Cos(yaw), 0);
        var direction = default(Vector3);
        var buttons = player->v.button;

        if ((buttons & InForward) != 0) direction += forward;
        if ((buttons & InBack) != 0) direction -= forward;
        if ((buttons & InMoveRight) != 0) direction += right;
        if ((buttons & InMoveLeft) != 0) direction -= right;

        var lengthSquared = direction.X * direction.X + direction.Y * direction.Y;
        if (lengthSquared > 0)
            direction *= 1.0f / MathF.Sqrt(lengthSquared);

        var velocity = direction * BallSpeed;

        player->v.velocity = velocity;
        player->v.iuser1 = 0;
        player->v.iuser2 = 0;

        if (lengthSquared > 0)
            ((edict_t*)balls[playerIndex])->v.angles.Y = MathF.Atan2(direction.Y, direction.X) * (180.0f / MathF.PI);
    }

    public override void PlayerPostThink(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0 || !HasActiveBallAndCamera(playerIndex))
            return;

        var ball = (edict_t*)balls[playerIndex];
        ball->v.velocity = player->v.velocity;
        SetOrigin(ball, player->v.origin);
        UpdateCamera(playerIndex, player);
    }

    private static int GetPlayerIndex(edict_t* player)
    {
        if (player == null || EngineApi.PServer == null)
            return -1;

        var index = EngineApi.PServer->IndexOfEdict(player) - 1;
        return index >= 0 && index < MaxPlayers ? index : -1;
    }

    private void RemoveBall(int playerIndex)
    {
        var ball = (edict_t*)balls[playerIndex];
        balls[playerIndex] = 0;
        if (ball != null && ball->free.Value == 0)
            EngineApi.PServer->RemoveEntity(ball);
    }

    private void RemoveCamera(int playerIndex, edict_t* player)
    {
        var camera = (edict_t*)cameras[playerIndex];
        cameras[playerIndex] = 0;
        if (camera == null || camera->free.Value != 0)
            return;

        if (player != null && player->free.Value == 0)
            EngineApi.PServer->SetView(player, player);

        EngineApi.PServer->RemoveEntity(camera);
    }

    private bool HasActiveBallAndCamera(int playerIndex)
    {
        var ball = (edict_t*)balls[playerIndex];
        var camera = (edict_t*)cameras[playerIndex];
        return ball != null && ball->free.Value == 0 &&
               camera != null && camera->free.Value == 0;
    }

    private void UpdateCamera(int playerIndex, edict_t* player)
    {
        var camera = (edict_t*)cameras[playerIndex];
        if (camera == null || camera->free.Value != 0)
            return;

        var pitch = player->v.v_angle.X * (MathF.PI / 180.0f);
        var yaw = player->v.v_angle.Y * (MathF.PI / 180.0f);
        var cosPitch = MathF.Cos(pitch);
        var forward = new Vector3(
            cosPitch * MathF.Cos(yaw),
            cosPitch * MathF.Sin(yaw),
            -MathF.Sin(pitch));

        var target = player->v.origin + new Vector3(0, 0, CameraTargetHeight);
        var desired = target - forward * CameraDistance;
        var trace = default(TraceResult);
        EngineApi.PServer->TraceLine((float*)&target, (float*)&desired, 1, player, &trace);

        var cameraOrigin = trace.flFraction < 1.0f
            ? trace.vecEndPos + trace.vecPlaneNormal * 4.0f
            : desired;

        camera->v.angles = player->v.v_angle;
        camera->v.v_angle = player->v.v_angle;
        SetOrigin(camera, cameraOrigin);
    }

    private static string_t AllocString(string value)
    {
        Span<byte> buffer = stackalloc byte[64];
        WriteNullTerminatedAscii(value, buffer);
        fixed (byte* pointer = buffer)
            return new string_t { Value = (uint)EngineApi.PServer->AllocString((NChar*)pointer) };
    }

    private static void SetModel(edict_t* entity, string model)
    {
        Span<byte> buffer = stackalloc byte[64];
        WriteNullTerminatedAscii(model, buffer);
        fixed (byte* pointer = buffer)
            EngineApi.PServer->SetModel(entity, (NChar*)pointer);
    }

    private static void SetSize(edict_t* entity, Vector3 mins, Vector3 maxs)
    {
        EngineApi.PServer->SetSize(entity, (float*)&mins, (float*)&maxs);
    }

    private static void SetOrigin(edict_t* entity, Vector3 origin)
    {
        EngineApi.PServer->SetOrigin(entity, (float*)&origin);
    }

    private static void WriteNullTerminatedAscii(string value, Span<byte> buffer)
    {
        buffer.Clear();
        var written = Encoding.ASCII.GetBytes(value, buffer);
        if (written < buffer.Length)
            buffer[written] = 0;
    }
}
