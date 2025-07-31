using System;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Framework implementation of server export functions with virtual methods for framework
/// </summary>
public unsafe class FrameworkServerExports : IServerExportFuncs
{
    // DLL_FUNCTIONS implementation
    public virtual void GameInit()
    {
        // Default game initialization
    }

    public virtual int Spawn(edict_t* pent)
    {
        // Default spawn
        return 0;
    }

    public virtual void Think(edict_t* pent)
    {
        // Default think
    }

    public virtual void Use(edict_t* pentUsed, edict_t* pentOther)
    {
        // Default use
    }

    public virtual void Touch(edict_t* pentTouched, edict_t* pentOther)
    {
        // Default touch
    }

    public virtual void Blocked(edict_t* pentBlocked, edict_t* pentOther)
    {
        // Default blocked
    }

    public virtual void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd)
    {
        // Default key value
    }

    public virtual void Save(edict_t* pent, SAVERESTOREDATA* pSaveData)
    {
        // Default save
    }

    public virtual int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity)
    {
        // Default restore
        return 0;
    }

    public virtual void SetAbsBox(edict_t* pent)
    {
        // Default set abs box
    }

    public virtual void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
    {
        // Default save write fields
    }

    public virtual void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
    {
        // Default save read fields
    }

    public virtual void SaveGlobalState(SAVERESTOREDATA* pSaveData)
    {
        // Default save global state
    }

    public virtual void RestoreGlobalState(SAVERESTOREDATA* pSaveData)
    {
        // Default restore global state
    }

    public virtual void ResetGlobalState()
    {
        // Default reset global state
    }

    public virtual qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
    {
        // Default client connect - allow connection
        return new qboolean { Value = 1 };
    }

    public virtual void ClientDisconnect(edict_t* pEntity)
    {
        // Default client disconnect
    }

    public virtual void ClientKill(edict_t* pEntity)
    {
        // Default client kill
    }

    public virtual void ClientPutInServer(edict_t* pEntity)
    {
        // Default client put in server
    }

    public virtual void ClientCommand(edict_t* pEntity)
    {
        // Default client command
    }

    public virtual void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer)
    {
        // Default client user info changed
    }

    public virtual void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax)
    {
        // Default server activate
    }

    public virtual void ServerDeactivate()
    {
        // Default server deactivate
    }

    public virtual void PlayerPreThink(edict_t* pEntity)
    {
        // Default player pre think
    }

    public virtual void PlayerPostThink(edict_t* pEntity)
    {
        // Default player post think
    }

    public virtual void StartFrame()
    {
        // Default start frame
    }

    public virtual void ParmsNewLevel()
    {
        // Default parms new level
    }

    public virtual void ParmsChangeLevel()
    {
        // Default parms change level
    }

    public virtual sbyte* GetGameDescription()
    {
        // Default game description
        return null;
    }

    public virtual void PlayerCustomization(edict_t* pEntity, customization_t* pCustom)
    {
        // Default player customization
    }

    public virtual void SpectatorConnect(edict_t* pEntity)
    {
        // Default spectator connect
    }

    public virtual void SpectatorDisconnect(edict_t* pEntity)
    {
        // Default spectator disconnect
    }

    public virtual void SpectatorThink(edict_t* pEntity)
    {
        // Default spectator think
    }

    public virtual void Sys_Error(sbyte* error_string)
    {
        // Default sys error
    }

    public virtual void PM_Move(playermove_s* ppmove, qboolean server)
    {
        // Default PM move
    }

    public virtual void PM_Init(playermove_s* ppmove)
    {
        // Default PM init
    }

    public virtual sbyte PM_FindTextureType(sbyte* name)
    {
        // Default texture type
        return (sbyte)'C'; // Concrete
    }

    public virtual void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas)
    {
        // Default setup visibility
    }

    public virtual void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd)
    {
        // Default update client data
    }

    public virtual int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
    {
        // Default add to full pack
        return 0;
    }

    public virtual void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3f* player_mins, Vector3f* player_maxs)
    {
        // Default create baseline
    }

    public virtual void RegisterEncoders()
    {
        // Default register encoders
    }

    public virtual int GetWeaponData(edict_t* player, weapon_data_s* info)
    {
        // Default get weapon data
        return 0;
    }

    public virtual void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed)
    {
        // Default cmd start
    }

    public virtual void CmdEnd(edict_t* player)
    {
        // Default cmd end
    }

    public virtual int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
    {
        // Default connectionless packet
        return 0;
    }

    public virtual int GetHullBounds(int hullnumber, float* mins, float* maxs)
    {
        // Default get hull bounds
        return 0;
    }

    public virtual void CreateInstancedBaselines()
    {
        // Default create instanced baselines
    }

    public virtual int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message)
    {
        // Default inconsistent file - allow
        return 0;
    }

    public virtual int AllowLagCompensation()
    {
        // Default allow lag compensation
        return 0;
    }

    // NEW_DLL_FUNCTIONS implementation
    public virtual void OnFreeEntPrivateData(edict_t* pEnt)
    {
        // Default on free ent private data
    }

    public virtual void GameShutdown()
    {
        // Default game shutdown
    }

    public virtual int ShouldCollide(edict_t* pentTouched, edict_t* pentOther)
    {
        // Default should collide - yes
        return 1;
    }

    public virtual void CvarValue(edict_t* pEnt, sbyte* value)
    {
        // Default cvar value
    }

    public virtual void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value)
    {
        // Default cvar value 2
    }
}
