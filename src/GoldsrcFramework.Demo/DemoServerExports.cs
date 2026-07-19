using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using System.Diagnostics;

namespace GoldsrcFramework.Demo;

/// <summary>
/// Minimal server-side ball experiment implemented directly on engine edicts.
/// No CBaseEntity instances or legacy private data are used by this implementation.
/// </summary>
public unsafe class DemoServerExports : FrameworkServerExports
{
    private const int MaxPlayers = 32;
    private const float BallSpeed = 300.0f;
    private const int ObserverChaseFree = 2;

    private const int InJump = 1 << 1;
    private const int InForward = 1 << 3;
    private const int InBack = 1 << 4;
    private const int InUse = 1 << 5;
    private const int InMoveLeft = 1 << 9;
    private const int InMoveRight = 1 << 10;

    private const int MoveTypeNone = 0;
    private const int SolidNot = 0;
    private const int SolidBBox = 2;
    private const int EffectNoInterp = 32;
    private const int EffectNoDraw = 128;
    private const int FlagFrozen = 1 << 12;

    private readonly nint[] balls = new nint[MaxPlayers];
    private readonly usercmd_t[] commands = new usercmd_t[MaxPlayers];
    private readonly bool[] hasCommand = new bool[MaxPlayers];
    private readonly float[] nextDiagnosticTime = new float[MaxPlayers];

    public override void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax)
    {
        base.ServerActivate(pEdictList, edictCount, clientMax);
        Array.Clear(balls);
        Array.Clear(commands);
        Array.Clear(hasCommand);
        Array.Clear(nextDiagnosticTime);
    }

    public override void ServerDeactivate()
    {
        Array.Clear(balls);
        Array.Clear(hasCommand);
        base.ServerDeactivate();
    }

    public override void ClientPutInServer(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0)
            return;

        RemoveBall(playerIndex);

        var engine = EngineApi.PServer;
        var ball = engine->CreateEntity();
        if (ball == null)
            return;

        ball->v.classname = AllocString("ball_entity");
        ball->v.solid = SolidBBox;
        ball->v.movetype = MoveTypeNone;
        ball->v.takedamage = 0;
        ball->v.effects = EffectNoInterp;
        ball->v.rendermode = 0;
        ball->v.renderamt = 255;
        ball->v.renderfx = 0;

        SetModel(ball, "models/w_grenade.mdl");
        SetSize(ball, new Vector3(-16), new Vector3(16));

        var origin = FindSpawnOrigin();
        SetOrigin(ball, origin);
        ball->v.velocity = default;
        balls[playerIndex] = (nint)ball;

        player->v.health = 100;
        player->v.deadflag = 0;
        player->v.solid = SolidNot;
        player->v.movetype = MoveTypeNone;
        player->v.effects |= EffectNoDraw;
        player->v.takedamage = 0;
        player->v.flags &= ~FlagFrozen;
        player->v.iuser1 = ObserverChaseFree;
        player->v.iuser2 = engine->IndexOfEdict(ball);
        player->v.view_ofs = default;
        player->v.fixangle = 0;
        player->v.maxspeed = BallSpeed;
        player->v.gravity = 0;
        SetOrigin(player, origin);
        engine->SetView(player, ball);
    }

    public override void ClientDisconnect(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex >= 0)
        {
            RemoveBall(playerIndex);
        }

        base.ClientDisconnect(player);
    }

    public override void KeyValue(edict_t* entity, KeyValueData* data)
    {
        if (entity == null || data == null)
            return;

        base.KeyValue(entity, data);
        var key = Marshal.PtrToStringUTF8((nint)data->szKeyName);
        var value = Marshal.PtrToStringUTF8((nint)data->szValue);
        if (key == null || value == null)
            return;

        switch (key)
        {
            case "origin" when TryParseVector(value, out var origin):
                entity->v.origin = origin;
                data->fHandled = 1;
                break;
            case "angles" when TryParseVector(value, out var angles):
                entity->v.angles = angles;
                data->fHandled = 1;
                break;
            case "angle" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw):
                entity->v.angles = yaw switch
                {
                    -1 => new Vector3(-90, 0, 0),
                    -2 => new Vector3(90, 0, 0),
                    _ => new Vector3(0, yaw, 0)
                };
                data->fHandled = 1;
                break;
        }
    }

    public override void PlayerPreThink(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0)
            return;

        if (!HasActiveBall(playerIndex))
            return;

        var command = commands[playerIndex];
        var yaw = command.viewangles.Y * (MathF.PI / 180.0f);
        var forward = new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0);
        var right = new Vector3(MathF.Sin(yaw), -MathF.Cos(yaw), 0);
        var direction = default(Vector3);
        var buttons = hasCommand[playerIndex] ? command.buttons : player->v.button;

        if ((buttons & InForward) != 0) direction += forward;
        if ((buttons & InBack) != 0) direction -= forward;
        if ((buttons & InMoveRight) != 0) direction += right;
        if ((buttons & InMoveLeft) != 0) direction -= right;
        if ((buttons & InJump) != 0) direction.Z += 1;
        if ((buttons & InUse) != 0) direction.Z -= 1;

        var lengthSquared = direction.X * direction.X +
                            direction.Y * direction.Y +
                            direction.Z * direction.Z;
        if (lengthSquared > 0)
            direction *= 1.0f / MathF.Sqrt(lengthSquared);

        var velocity = direction * BallSpeed;
        var frameTime = command.msec > 0
            ? command.msec * 0.001f
            : Math.Max(EngineApi.PGlobals->frametime, 0.001f);
        var ball = (edict_t*)balls[playerIndex];
        var start = ball->v.origin;
        var end = start + velocity * frameTime;
        var trace = MoveWithCollision(ball, start, end);

        player->v.velocity = velocity;
        player->v.v_angle = command.viewangles;
        player->v.iuser1 = ObserverChaseFree;
        player->v.iuser2 = EngineApi.PServer->IndexOfEdict(ball);
        ball->v.velocity = velocity;
        SetOrigin(ball, trace.vecEndPos);

        if (lengthSquared > 0)
            ball->v.angles.Y = MathF.Atan2(direction.Y, direction.X) * (180.0f / MathF.PI);

        WriteMovementDiagnostic(playerIndex, ball, command, trace);
    }

    public override void CmdStart(edict_t* player, usercmd_t* command, uint randomSeed)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0 || command == null)
            return;

        commands[playerIndex] = *command;
        hasCommand[playerIndex] = true;
        player->v.button = command->buttons;
        player->v.impulse = command->impulse;
        player->v.v_angle = command->viewangles;
    }

    public override void PlayerPostThink(edict_t* player)
    {
        var playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0 || !HasActiveBall(playerIndex))
            return;
    }

    public override void UpdateClientData(edict_t* player, int sendWeapons, clientdata_t* clientData)
    {
        if (player == null || clientData == null)
            return;

        var entity = &player->v;
        *clientData = default;
        clientData->origin = entity->origin;
        clientData->velocity = entity->velocity;
        clientData->punchangle = entity->punchangle;
        clientData->flags = entity->flags;
        clientData->waterlevel = entity->waterlevel;
        clientData->watertype = entity->watertype;
        clientData->view_ofs = entity->view_ofs;
        clientData->health = entity->health;
        clientData->bInDuck = entity->bInDuck;
        clientData->weapons = entity->weapons;
        clientData->flTimeStepSound = entity->flTimeStepSound;
        clientData->flDuckTime = entity->flDuckTime;
        clientData->flSwimTime = entity->flSwimTime;
        clientData->waterjumptime = (int)entity->teleport_time;
        clientData->maxspeed = entity->maxspeed;
        clientData->fov = entity->fov;
        clientData->weaponanim = entity->weaponanim;
        clientData->pushmsec = entity->pushmsec;
        clientData->deadflag = entity->deadflag;
        clientData->iuser1 = entity->iuser1;
        clientData->iuser2 = entity->iuser2;
        clientData->iuser3 = entity->iuser3;
        clientData->iuser4 = entity->iuser4;
        clientData->fuser1 = entity->fuser1;
        clientData->fuser2 = entity->fuser2;
        clientData->fuser3 = entity->fuser3;
        clientData->fuser4 = entity->fuser4;
        clientData->vuser1 = entity->vuser1;
        clientData->vuser2 = entity->vuser2;
        clientData->vuser3 = entity->vuser3;
        clientData->vuser4 = entity->vuser4;
    }

    public override int AddToFullPack(entity_state_t* state, int entityIndex, edict_t* entity, edict_t* host,
        int hostFlags, int isPlayer, byte* visibilitySet)
    {
        var result = base.AddToFullPack(state, entityIndex, entity, host, hostFlags, isPlayer, visibilitySet);
        if (entity != host)
            return result;

        if (result == 0)
        {
            *state = default;
            state->entityType = 0;
            state->number = entityIndex;
            state->origin = entity->v.origin;
            state->angles = entity->v.angles;
            state->velocity = entity->v.velocity;
            state->modelindex = entity->v.modelindex;
            state->solid = (short)entity->v.solid;
            state->effects = entity->v.effects;
            state->movetype = entity->v.movetype;
            state->mins = entity->v.mins;
            state->maxs = entity->v.maxs;
        }

        state->iuser1 = entity->v.iuser1;
        state->iuser2 = entity->v.iuser2;
        state->iuser3 = entity->v.iuser3;
        state->iuser4 = entity->v.iuser4;
        state->fuser1 = entity->v.fuser1;
        state->fuser2 = entity->v.fuser2;
        state->fuser3 = entity->v.fuser3;
        state->fuser4 = entity->v.fuser4;
        state->vuser1 = entity->v.vuser1;
        state->vuser2 = entity->v.vuser2;
        state->vuser3 = entity->v.vuser3;
        state->vuser4 = entity->v.vuser4;
        return 1;
    }

    private static int GetPlayerIndex(edict_t* player)
    {
        if (player == null || EngineApi.PServer == null)
            return -1;

        var index = EngineApi.PServer->IndexOfEdict(player) - 1;
        return index >= 0 && index < MaxPlayers ? index : -1;
    }

    private static Vector3 FindSpawnOrigin()
    {
        var spawn = FindEntityByClassName("info_player_start");
        if (spawn == null)
            spawn = FindEntityByClassName("info_player_deathmatch");
        if (spawn != null)
            return spawn->v.origin + new Vector3(0, 0, 1);

        return new Vector3(0, 0, 64);
    }

    private static bool TryParseVector(string value, out Vector3 vector)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            vector = new Vector3(x, y, z);
            return true;
        }

        vector = default;
        return false;
    }

    private static edict_t* FindEntityByClassName(string className)
    {
        Span<byte> fieldBuffer = stackalloc byte[16];
        Span<byte> valueBuffer = stackalloc byte[64];
        WriteNullTerminatedAscii("classname", fieldBuffer);
        WriteNullTerminatedAscii(className, valueBuffer);
        fixed (byte* field = fieldBuffer)
        fixed (byte* value = valueBuffer)
            return EngineApi.PServer->FindEntityByString(null, (NChar*)field, (NChar*)value);
    }

    private static TraceResult MoveWithCollision(edict_t* player, Vector3 start, Vector3 end)
    {
        var trace = default(TraceResult);
        EngineApi.PServer->TraceMonsterHull(player, (float*)&start, (float*)&end, 1, player, &trace);
        if (trace.flFraction >= 1.0f || trace.fAllSolid != 0)
            return trace;

        var remaining = end - trace.vecEndPos;
        var intoWall = remaining.X * trace.vecPlaneNormal.X +
                       remaining.Y * trace.vecPlaneNormal.Y +
                       remaining.Z * trace.vecPlaneNormal.Z;
        var slide = remaining - trace.vecPlaneNormal * intoWall;
        if (slide.X * slide.X + slide.Y * slide.Y + slide.Z * slide.Z < 0.0001f)
            return trace;

        var slideStart = trace.vecEndPos;
        var slideEnd = slideStart + slide;
        var slideTrace = default(TraceResult);
        EngineApi.PServer->TraceMonsterHull(player, (float*)&slideStart, (float*)&slideEnd, 1, player, &slideTrace);
        return slideTrace;
    }

    private void WriteMovementDiagnostic(int playerIndex, edict_t* player, usercmd_t command, TraceResult trace)
    {
        var now = EngineApi.PGlobals->time;
        if (now < nextDiagnosticTime[playerIndex])
            return;

        nextDiagnosticTime[playerIndex] = now + 1.0f;
        var origin = player->v.origin;
        var message = string.Create(CultureInfo.InvariantCulture,
            $"[BallGame] p={playerIndex + 1} buttons=0x{command.buttons:X4} " +
            $"fwd={command.forwardmove:F0} side={command.sidemove:F0} msec={command.msec} " +
            $"origin=({origin.X:F1},{origin.Y:F1},{origin.Z:F1}) " +
            $"trace={trace.flFraction:F2} solid={trace.fAllSolid}/{trace.fStartSolid}\n");
        Debug.WriteLine (message);
    }

    private static void AlertConsole(string message)
    {
        Span<byte> buffer = stackalloc byte[384];
        WriteNullTerminatedAscii(message, buffer);
        fixed (byte* pointer = buffer)
            EngineApi.PServer->AlertMessage(ALERT_TYPE.at_console, (NChar*)pointer);
    }

    private void RemoveBall(int playerIndex)
    {
        var ball = (edict_t*)balls[playerIndex];
        balls[playerIndex] = 0;
        if (ball != null && ball->free.Value == 0)
            EngineApi.PServer->RemoveEntity(ball);
    }

    private bool HasActiveBall(int playerIndex)
    {
        var ball = (edict_t*)balls[playerIndex];
        return ball != null && ball->free.Value == 0;
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
