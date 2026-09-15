using System;
using GoldsrcFramework.LinearMath;
using NativeInterop;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Interface that combines DLL_FUNCTIONS and NEW_DLL_FUNCTIONS
/// </summary>
public unsafe interface IServerExportFuncs
{
    // DLL_FUNCTIONS
    void GameInit();
    int Spawn(edict_t* pent);
    void Think(edict_t* pent);
    void Use(edict_t* pentUsed, edict_t* pentOther);
    void Touch(edict_t* pentTouched, edict_t* pentOther);
    void Blocked(edict_t* pentBlocked, edict_t* pentOther);
    void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd);
    void Save(edict_t* pent, SAVERESTOREDATA* pSaveData);
    int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity);
    void SetAbsBox(edict_t* pent);

    void SaveWriteFields(SAVERESTOREDATA* pSaveData, NCharPtr pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount);
    void SaveReadFields(SAVERESTOREDATA* pSaveData, NCharPtr pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount);

    void SaveGlobalState(SAVERESTOREDATA* pSaveData);
    void RestoreGlobalState(SAVERESTOREDATA* pSaveData);
    void ResetGlobalState();

    qboolean ClientConnect(edict_t* pEntity, NCharPtr pszName, NCharPtr pszAddress, NCharPtr szRejectReason);

    void ClientDisconnect(edict_t* pEntity);
    void ClientKill(edict_t* pEntity);
    void ClientPutInServer(edict_t* pEntity);
    void ClientCommand(edict_t* pEntity);
    void ClientUserInfoChanged(edict_t* pEntity, NCharPtr infobuffer);

    void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax);
    void ServerDeactivate();

    void PlayerPreThink(edict_t* pEntity);
    void PlayerPostThink(edict_t* pEntity);

    void StartFrame();
    void ParmsNewLevel();
    void ParmsChangeLevel();

    NCharPtr GetGameDescription();

    void PlayerCustomization(edict_t* pEntity, customization_t* pCustom);

    void SpectatorConnect(edict_t* pEntity);
    void SpectatorDisconnect(edict_t* pEntity);
    void SpectatorThink(edict_t* pEntity);

    void Sys_Error(NCharPtr error_string);

    void PM_Move(playermove_t* ppmove, qboolean server);
    void PM_Init(playermove_t* ppmove);
    NChar PM_FindTextureType(NCharPtr name);
    void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas);
    void UpdateClientData(edict_t* ent, int sendweapons, clientdata_t* cd);
    int AddToFullPack(entity_state_t* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet);
    void CreateBaseline(int player, int eindex, entity_state_t* baseline, edict_t* entity, int playermodelindex, Vector3* player_mins, Vector3* player_maxs);
    void RegisterEncoders();
    int GetWeaponData(edict_t* player, weapon_data_t* info);

    void CmdStart(edict_t* player, usercmd_t* cmd, uint random_seed);
    void CmdEnd(edict_t* player);

    int ConnectionlessPacket(netadr_t* net_from, NCharPtr args, NCharPtr response_buffer, int* response_buffer_size);

    int GetHullBounds(int hullnumber, float* mins, float* maxs);

    void CreateInstancedBaselines();

    int InconsistentFile(edict_t* player, NCharPtr filename, NCharPtr disconnect_message);

    int AllowLagCompensation();

    // NEW_DLL_FUNCTIONS
    void OnFreeEntPrivateData(edict_t* pEnt);
    void GameShutdown();
    int ShouldCollide(edict_t* pentTouched, edict_t* pentOther);
    void CvarValue(edict_t* pEnt, NCharPtr value);
    void CvarValue2(edict_t* pEnt, int requestID, NCharPtr cvarName, NCharPtr value);
}
