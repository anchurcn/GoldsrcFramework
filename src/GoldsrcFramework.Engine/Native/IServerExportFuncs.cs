using System;
using GoldsrcFramework.LinearMath;

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

    void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount);
    void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount);

    void SaveGlobalState(SAVERESTOREDATA* pSaveData);
    void RestoreGlobalState(SAVERESTOREDATA* pSaveData);
    void ResetGlobalState();

    qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason);

    void ClientDisconnect(edict_t* pEntity);
    void ClientKill(edict_t* pEntity);
    void ClientPutInServer(edict_t* pEntity);
    void ClientCommand(edict_t* pEntity);
    void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer);

    void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax);
    void ServerDeactivate();

    void PlayerPreThink(edict_t* pEntity);
    void PlayerPostThink(edict_t* pEntity);

    void StartFrame();
    void ParmsNewLevel();
    void ParmsChangeLevel();

    sbyte* GetGameDescription();

    void PlayerCustomization(edict_t* pEntity, customization_t* pCustom);

    void SpectatorConnect(edict_t* pEntity);
    void SpectatorDisconnect(edict_t* pEntity);
    void SpectatorThink(edict_t* pEntity);

    void Sys_Error(sbyte* error_string);

    void PM_Move(playermove_s* ppmove, qboolean server);
    void PM_Init(playermove_s* ppmove);
    sbyte PM_FindTextureType(sbyte* name);
    void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas);
    void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd);
    int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet);
    void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3* player_mins, Vector3* player_maxs);
    void RegisterEncoders();
    int GetWeaponData(edict_t* player, weapon_data_s* info);

    void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed);
    void CmdEnd(edict_t* player);

    int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size);

    int GetHullBounds(int hullnumber, float* mins, float* maxs);

    void CreateInstancedBaselines();

    int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message);

    int AllowLagCompensation();

    // NEW_DLL_FUNCTIONS
    void OnFreeEntPrivateData(edict_t* pEnt);
    void GameShutdown();
    int ShouldCollide(edict_t* pentTouched, edict_t* pentOther);
    void CvarValue(edict_t* pEnt, sbyte* value);
    void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value);
}
