using System;
using System.Text;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of server export functions with virtual methods for framework
/// </summary>
public unsafe class FrameworkServerExports : IServerExportFuncs
{
    private void Log(string methodName)
    {
        Console.WriteLine($"[FrameworkServerExports] Calling {methodName}");
    }

    // DLL_FUNCTIONS implementation - all based on LegacyServerInterop
    public virtual void GameInit()
    {
        Log(nameof(GameInit));
        LegacyServerInterop.GameInit();
    }

    public virtual int Spawn(edict_t* pent)
    {
        Log(nameof(Spawn));
        var msgbuf = Encoding.UTF8.GetBytes("hello spawn from framework");

        fixed (byte* pDst = msgbuf)
        {
            sbyte* p = (sbyte*)pDst;
        }
        return LegacyServerInterop.Spawn(pent);
    }

    public virtual void Think(edict_t* pent)
    {
        Log(nameof(Think));
        LegacyServerInterop.Think(pent);
    }

    public virtual void Use(edict_t* pentUsed, edict_t* pentOther)
    {
        Log(nameof(Use));
        LegacyServerInterop.Use(pentUsed, pentOther);
    }

    public virtual void Touch(edict_t* pentTouched, edict_t* pentOther)
    {
        Log(nameof(Touch));
        LegacyServerInterop.Touch(pentTouched, pentOther);
    }

    public virtual void Blocked(edict_t* pentBlocked, edict_t* pentOther)
    {
        Log(nameof(Blocked));
        LegacyServerInterop.Blocked(pentBlocked, pentOther);
    }

    public virtual void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd)
    {
        Log(nameof(KeyValue));
        LegacyServerInterop.KeyValue(pentKeyvalue, pkvd);
    }

    public virtual void Save(edict_t* pent, SAVERESTOREDATA* pSaveData)
    {
        Log(nameof(Save));
        LegacyServerInterop.Save(pent, pSaveData);
    }

    public virtual int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity)
    {
        Log(nameof(Restore));
        return LegacyServerInterop.Restore(pent, pSaveData, globalEntity);
    }

    public virtual void SetAbsBox(edict_t* pent)
    {
        Log(nameof(SetAbsBox));
        LegacyServerInterop.SetAbsBox(pent);
    }

    public virtual void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
    {
        Log(nameof(SaveWriteFields));
        LegacyServerInterop.SaveWriteFields(pSaveData, pname, pBaseData, pFields, fieldCount);
    }

    public virtual void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
    {
        Log(nameof(SaveReadFields));
        LegacyServerInterop.SaveReadFields(pSaveData, pname, pBaseData, pFields, fieldCount);
    }

    public virtual void SaveGlobalState(SAVERESTOREDATA* pSaveData)
    {
        Log(nameof(SaveGlobalState));
        LegacyServerInterop.SaveGlobalState(pSaveData);
    }

    public virtual void RestoreGlobalState(SAVERESTOREDATA* pSaveData)
    {
        Log(nameof(RestoreGlobalState));
        LegacyServerInterop.RestoreGlobalState(pSaveData);
    }

    public virtual void ResetGlobalState()
    {
        Log(nameof(ResetGlobalState));
        LegacyServerInterop.ResetGlobalState();
    }

    public virtual qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
    {
        Log(nameof(ClientConnect));
        return LegacyServerInterop.ClientConnect(pEntity, pszName, pszAddress, szRejectReason);
    }

    public virtual void ClientDisconnect(edict_t* pEntity)
    {
        Log(nameof(ClientDisconnect));
        LegacyServerInterop.ClientDisconnect(pEntity);
    }

    public virtual void ClientKill(edict_t* pEntity)
    {
        Log(nameof(ClientKill));
        LegacyServerInterop.ClientKill(pEntity);
    }

    public virtual void ClientPutInServer(edict_t* pEntity)
    {
        Log(nameof(ClientPutInServer));
        LegacyServerInterop.ClientPutInServer(pEntity);
    }

    public virtual void ClientCommand(edict_t* pEntity)
    {
        Log(nameof(ClientCommand));
        LegacyServerInterop.ClientCommand(pEntity);
    }

    public virtual void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer)
    {
        Log(nameof(ClientUserInfoChanged));
        LegacyServerInterop.ClientUserInfoChanged(pEntity, infobuffer);
    }

    public virtual void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax)
    {
        Log(nameof(ServerActivate));
        LegacyServerInterop.ServerActivate(pEdictList, edictCount, clientMax);
    }

    public virtual void ServerDeactivate()
    {
        Log(nameof(ServerDeactivate));
        LegacyServerInterop.ServerDeactivate();
    }

    public virtual void PlayerPreThink(edict_t* pEntity)
    {
        Log(nameof(PlayerPreThink));
        LegacyServerInterop.PlayerPreThink(pEntity);
    }

    public virtual void PlayerPostThink(edict_t* pEntity)
    {
        Log(nameof(PlayerPostThink));
        LegacyServerInterop.PlayerPostThink(pEntity);
    }

    public virtual void StartFrame()
    {
        Log(nameof(StartFrame));
        LegacyServerInterop.StartFrame();
    }

    public virtual void ParmsNewLevel()
    {
        Log(nameof(ParmsNewLevel));
        LegacyServerInterop.ParmsNewLevel();
    }

    public virtual void ParmsChangeLevel()
    {
        Log(nameof(ParmsChangeLevel));
        LegacyServerInterop.ParmsChangeLevel();
    }

    public virtual sbyte* GetGameDescription()
    {
        Log(nameof(GetGameDescription));
        return LegacyServerInterop.GetGameDescription();
    }

    public virtual void PlayerCustomization(edict_t* pEntity, customization_t* pCustom)
    {
        Log(nameof(PlayerCustomization));
        LegacyServerInterop.PlayerCustomization(pEntity, pCustom);
    }

    public virtual void SpectatorConnect(edict_t* pEntity)
    {
        Log(nameof(SpectatorConnect));
        LegacyServerInterop.SpectatorConnect(pEntity);
    }

    public virtual void SpectatorDisconnect(edict_t* pEntity)
    {
        Log(nameof(SpectatorDisconnect));
        LegacyServerInterop.SpectatorDisconnect(pEntity);
    }

    public virtual void SpectatorThink(edict_t* pEntity)
    {
        Log(nameof(SpectatorThink));
        LegacyServerInterop.SpectatorThink(pEntity);
    }

    public virtual void Sys_Error(sbyte* error_string)
    {
        Log(nameof(Sys_Error));
        LegacyServerInterop.Sys_Error(error_string);
    }

    public virtual void PM_Move(playermove_s* ppmove, qboolean server)
    {
        Log(nameof(PM_Move));
        LegacyServerInterop.PM_Move(ppmove, server);
    }

    public virtual void PM_Init(playermove_s* ppmove)
    {
        Log(nameof(PM_Init));
        LegacyServerInterop.PM_Init(ppmove);
    }

    public virtual sbyte PM_FindTextureType(sbyte* name)
    {
        Log(nameof(PM_FindTextureType));
        return LegacyServerInterop.PM_FindTextureType(name);
    }

    public virtual void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas)
    {
        Log(nameof(SetupVisibility));
        LegacyServerInterop.SetupVisibility(pViewEntity, pClient, pvs, pas);
    }

    public virtual void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd)
    {
        Log(nameof(UpdateClientData));
        LegacyServerInterop.UpdateClientData(ent, sendweapons, cd);
    }

    public virtual int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
    {
        Log(nameof(AddToFullPack));
        return LegacyServerInterop.AddToFullPack(state, e, ent, host, hostflags, player, pSet);
    }

    public virtual void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3f* player_mins, Vector3f* player_maxs)
    {
        Log(nameof(CreateBaseline));
        LegacyServerInterop.CreateBaseline(player, eindex, baseline, entity, playermodelindex, player_mins, player_maxs);
    }

    public virtual void RegisterEncoders()
    {
        Log(nameof(RegisterEncoders));
        LegacyServerInterop.RegisterEncoders();
    }

    public virtual int GetWeaponData(edict_t* player, weapon_data_s* info)
    {
        Log(nameof(GetWeaponData));
        return LegacyServerInterop.GetWeaponData(player, info);
    }

    public virtual void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed)
    {
        Log(nameof(CmdStart));
        LegacyServerInterop.CmdStart(player, cmd, random_seed);
    }

    public virtual void CmdEnd(edict_t* player)
    {
        Log(nameof(CmdEnd));
        LegacyServerInterop.CmdEnd(player);
    }

    public virtual int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
    {
        Log(nameof(ConnectionlessPacket));
        return LegacyServerInterop.ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
    }

    public virtual int GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        Log(nameof(GetHullBounds));
        return LegacyServerInterop.GetHullBounds(hullnumber, mins, maxs);
    }

    public virtual void CreateInstancedBaselines()
    {
        Log(nameof(CreateInstancedBaselines));
        LegacyServerInterop.CreateInstancedBaselines();
    }

    public virtual int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message)
    {
        Log(nameof(InconsistentFile));
        return LegacyServerInterop.InconsistentFile(player, filename, disconnect_message);
    }

    public virtual int AllowLagCompensation()
    {
        Log(nameof(AllowLagCompensation));
        return LegacyServerInterop.AllowLagCompensation();
    }

    // NEW_DLL_FUNCTIONS implementation - all based on LegacyServerInterop
    public virtual void OnFreeEntPrivateData(edict_t* pEnt)
    {
        Log(nameof(OnFreeEntPrivateData));
        LegacyServerInterop.OnFreeEntPrivateData(pEnt);
    }

    public virtual void GameShutdown()
    {
        Log(nameof(GameShutdown));
        LegacyServerInterop.GameShutdown();
    }

    public virtual int ShouldCollide(edict_t* pentTouched, edict_t* pentOther)
    {
        Log(nameof(ShouldCollide));
        return LegacyServerInterop.ShouldCollide(pentTouched, pentOther);
    }

    public virtual void CvarValue(edict_t* pEnt, sbyte* value)
    {
        Log(nameof(CvarValue));
        LegacyServerInterop.CvarValue(pEnt, value);
    }

    public virtual void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value)
    {
        Log(nameof(CvarValue2));
        LegacyServerInterop.CvarValue2(pEnt, requestID, cvarName, value);
    }
}