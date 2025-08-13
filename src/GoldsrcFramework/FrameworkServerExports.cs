using System;
using System.Text;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of server export functions with virtual methods for framework
/// </summary>
public unsafe class FrameworkServerExports : IServerExportFuncs
{
    // DLL_FUNCTIONS implementation - all based on LegacyServerInterop
    public virtual void GameInit()
    {
        LegacyServerInterop.GameInit();
    }

    public virtual int Spawn(edict_t* pent)
    {
        var msgbuf = Encoding.UTF8.GetBytes("hello spawn from framework");
        
        fixed (byte* pDst = msgbuf)
        {
            sbyte* p = (sbyte*) pDst;
        }
        return LegacyServerInterop.Spawn(pent);
    }

    public virtual void Think(edict_t* pent)
    {
        LegacyServerInterop.Think(pent);
    }

    public virtual void Use(edict_t* pentUsed, edict_t* pentOther)
    {
        LegacyServerInterop.Use(pentUsed, pentOther);
    }

    public virtual void Touch(edict_t* pentTouched, edict_t* pentOther)
    {
        LegacyServerInterop.Touch(pentTouched, pentOther);
    }

    public virtual void Blocked(edict_t* pentBlocked, edict_t* pentOther)
    {
        LegacyServerInterop.Blocked(pentBlocked, pentOther);
    }

    public virtual void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd)
    {
        LegacyServerInterop.KeyValue(pentKeyvalue, pkvd);
    }

    public virtual void Save(edict_t* pent, SAVERESTOREDATA* pSaveData)
    {
        LegacyServerInterop.Save(pent, pSaveData);
    }

    public virtual int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity)
    {
        return LegacyServerInterop.Restore(pent, pSaveData, globalEntity);
    }

    public virtual void SetAbsBox(edict_t* pent)
    {
        LegacyServerInterop.SetAbsBox(pent);
    }

    public virtual void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
    {
        LegacyServerInterop.SaveWriteFields(pSaveData, pname, pBaseData, pFields, fieldCount);
    }

    public virtual void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
    {
        LegacyServerInterop.SaveReadFields(pSaveData, pname, pBaseData, pFields, fieldCount);
    }

    public virtual void SaveGlobalState(SAVERESTOREDATA* pSaveData)
    {
        LegacyServerInterop.SaveGlobalState(pSaveData);
    }

    public virtual void RestoreGlobalState(SAVERESTOREDATA* pSaveData)
    {
        LegacyServerInterop.RestoreGlobalState(pSaveData);
    }

    public virtual void ResetGlobalState()
    {
        LegacyServerInterop.ResetGlobalState();
    }

    public virtual qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
    {
        return LegacyServerInterop.ClientConnect(pEntity, pszName, pszAddress, szRejectReason);
    }

    public virtual void ClientDisconnect(edict_t* pEntity)
    {
        LegacyServerInterop.ClientDisconnect(pEntity);
    }

    public virtual void ClientKill(edict_t* pEntity)
    {
        LegacyServerInterop.ClientKill(pEntity);
    }

    public virtual void ClientPutInServer(edict_t* pEntity)
    {
        LegacyServerInterop.ClientPutInServer(pEntity);
    }

    public virtual void ClientCommand(edict_t* pEntity)
    {
        LegacyServerInterop.ClientCommand(pEntity);
    }

    public virtual void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer)
    {
        LegacyServerInterop.ClientUserInfoChanged(pEntity, infobuffer);
    }

    public virtual void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax)
    {
        LegacyServerInterop.ServerActivate(pEdictList, edictCount, clientMax);
    }

    public virtual void ServerDeactivate()
    {
        LegacyServerInterop.ServerDeactivate();
    }

    public virtual void PlayerPreThink(edict_t* pEntity)
    {
        LegacyServerInterop.PlayerPreThink(pEntity);
    }

    public virtual void PlayerPostThink(edict_t* pEntity)
    {
        LegacyServerInterop.PlayerPostThink(pEntity);
    }

    public virtual void StartFrame()
    {
        LegacyServerInterop.StartFrame();
    }

    public virtual void ParmsNewLevel()
    {
        LegacyServerInterop.ParmsNewLevel();
    }

    public virtual void ParmsChangeLevel()
    {
        LegacyServerInterop.ParmsChangeLevel();
    }

    public virtual sbyte* GetGameDescription()
    {
        return LegacyServerInterop.GetGameDescription();
    }

    public virtual void PlayerCustomization(edict_t* pEntity, customization_t* pCustom)
    {
        LegacyServerInterop.PlayerCustomization(pEntity, pCustom);
    }

    public virtual void SpectatorConnect(edict_t* pEntity)
    {
        LegacyServerInterop.SpectatorConnect(pEntity);
    }

    public virtual void SpectatorDisconnect(edict_t* pEntity)
    {
        LegacyServerInterop.SpectatorDisconnect(pEntity);
    }

    public virtual void SpectatorThink(edict_t* pEntity)
    {
        LegacyServerInterop.SpectatorThink(pEntity);
    }

    public virtual void Sys_Error(sbyte* error_string)
    {
        LegacyServerInterop.Sys_Error(error_string);
    }

    public virtual void PM_Move(playermove_s* ppmove, qboolean server)
    {
        LegacyServerInterop.PM_Move(ppmove, server);
    }

    public virtual void PM_Init(playermove_s* ppmove)
    {
        LegacyServerInterop.PM_Init(ppmove);
    }

    public virtual sbyte PM_FindTextureType(sbyte* name)
    {
        return LegacyServerInterop.PM_FindTextureType(name);
    }

    public virtual void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas)
    {
        LegacyServerInterop.SetupVisibility(pViewEntity, pClient, pvs, pas);
    }

    public virtual void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd)
    {
        LegacyServerInterop.UpdateClientData(ent, sendweapons, cd);
    }

    public virtual int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
    {
        return LegacyServerInterop.AddToFullPack(state, e, ent, host, hostflags, player, pSet);
    }

    public virtual void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3f* player_mins, Vector3f* player_maxs)
    {
        LegacyServerInterop.CreateBaseline(player, eindex, baseline, entity, playermodelindex, player_mins, player_maxs);
    }

    public virtual void RegisterEncoders()
    {
        LegacyServerInterop.RegisterEncoders();
    }

    public virtual int GetWeaponData(edict_t* player, weapon_data_s* info)
    {
        return LegacyServerInterop.GetWeaponData(player, info);
    }

    public virtual void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed)
    {
        LegacyServerInterop.CmdStart(player, cmd, random_seed);
    }

    public virtual void CmdEnd(edict_t* player)
    {
        LegacyServerInterop.CmdEnd(player);
    }

    public virtual int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
    {
        return LegacyServerInterop.ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);
    }

    public virtual int GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        return LegacyServerInterop.GetHullBounds(hullnumber, mins, maxs);
    }

    public virtual void CreateInstancedBaselines()
    {
        LegacyServerInterop.CreateInstancedBaselines();
    }

    public virtual int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message)
    {
        return LegacyServerInterop.InconsistentFile(player, filename, disconnect_message);
    }

    public virtual int AllowLagCompensation()
    {
        return LegacyServerInterop.AllowLagCompensation();
    }

    // NEW_DLL_FUNCTIONS implementation - all based on LegacyServerInterop
    public virtual void OnFreeEntPrivateData(edict_t* pEnt)
    {
        LegacyServerInterop.OnFreeEntPrivateData(pEnt);
    }

    public virtual void GameShutdown()
    {
        LegacyServerInterop.GameShutdown();
    }

    public virtual int ShouldCollide(edict_t* pentTouched, edict_t* pentOther)
    {
        return LegacyServerInterop.ShouldCollide(pentTouched, pentOther);
    }

    public virtual void CvarValue(edict_t* pEnt, sbyte* value)
    {
        LegacyServerInterop.CvarValue(pEnt, value);
    }

    public virtual void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value)
    {
        LegacyServerInterop.CvarValue2(pEnt, requestID, cvarName, value);
    }
}
