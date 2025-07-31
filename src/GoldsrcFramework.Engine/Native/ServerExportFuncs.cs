using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Server export functions structure that matches DLL_FUNCTIONS layout
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ServerExportFuncs
{
    public delegate* unmanaged[Cdecl]<void> GameInit;
    public delegate* unmanaged[Cdecl]<edict_t*, int> Spawn;
    public delegate* unmanaged[Cdecl]<edict_t*, void> Think;
    public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, void> Use;
    public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, void> Touch;
    public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, void> Blocked;
    public delegate* unmanaged[Cdecl]<edict_t*, KeyValueData*, void> KeyValue;
    public delegate* unmanaged[Cdecl]<edict_t*, SAVERESTOREDATA*, void> Save;
    public delegate* unmanaged[Cdecl]<edict_t*, SAVERESTOREDATA*, int, int> Restore;
    public delegate* unmanaged[Cdecl]<edict_t*, void> SetAbsBox;

    public delegate* unmanaged[Cdecl]<SAVERESTOREDATA*, sbyte*, void*, TYPEDESCRIPTION*, int, void> SaveWriteFields;
    public delegate* unmanaged[Cdecl]<SAVERESTOREDATA*, sbyte*, void*, TYPEDESCRIPTION*, int, void> SaveReadFields;

    public delegate* unmanaged[Cdecl]<SAVERESTOREDATA*, void> SaveGlobalState;
    public delegate* unmanaged[Cdecl]<SAVERESTOREDATA*, void> RestoreGlobalState;
    public delegate* unmanaged[Cdecl]<void> ResetGlobalState;

    public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, sbyte*, sbyte*, qboolean> ClientConnect;

    public delegate* unmanaged[Cdecl]<edict_t*, void> ClientDisconnect;
    public delegate* unmanaged[Cdecl]<edict_t*, void> ClientKill;
    public delegate* unmanaged[Cdecl]<edict_t*, void> ClientPutInServer;
    public delegate* unmanaged[Cdecl]<edict_t*, void> ClientCommand;
    public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, void> ClientUserInfoChanged;

    public delegate* unmanaged[Cdecl]<edict_t*, int, int, void> ServerActivate;
    public delegate* unmanaged[Cdecl]<void> ServerDeactivate;

    public delegate* unmanaged[Cdecl]<edict_t*, void> PlayerPreThink;
    public delegate* unmanaged[Cdecl]<edict_t*, void> PlayerPostThink;

    public delegate* unmanaged[Cdecl]<void> StartFrame;
    public delegate* unmanaged[Cdecl]<void> ParmsNewLevel;
    public delegate* unmanaged[Cdecl]<void> ParmsChangeLevel;

    public delegate* unmanaged[Cdecl]<sbyte*> GetGameDescription;

    public delegate* unmanaged[Cdecl]<edict_t*, customization_t*, void> PlayerCustomization;

    public delegate* unmanaged[Cdecl]<edict_t*, void> SpectatorConnect;
    public delegate* unmanaged[Cdecl]<edict_t*, void> SpectatorDisconnect;
    public delegate* unmanaged[Cdecl]<edict_t*, void> SpectatorThink;

    public delegate* unmanaged[Cdecl]<sbyte*, void> Sys_Error;

    public delegate* unmanaged[Cdecl]<playermove_s*, qboolean, void> PM_Move;
    public delegate* unmanaged[Cdecl]<playermove_s*, void> PM_Init;
    public delegate* unmanaged[Cdecl]<sbyte*, sbyte> PM_FindTextureType;
    public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, byte**, byte**, void> SetupVisibility;
    public delegate* unmanaged[Cdecl]<edict_t*, int, clientdata_s*, void> UpdateClientData;
    public delegate* unmanaged[Cdecl]<entity_state_s*, int, edict_t*, edict_t*, int, int, byte*, int> AddToFullPack;
    public delegate* unmanaged[Cdecl]<int, int, entity_state_s*, edict_t*, int, Vector3f*, Vector3f*, void> CreateBaseline;
    public delegate* unmanaged[Cdecl]<void> RegisterEncoders;
    public delegate* unmanaged[Cdecl]<edict_t*, weapon_data_s*, int> GetWeaponData;

    public delegate* unmanaged[Cdecl]<edict_t*, usercmd_s*, uint, void> CmdStart;
    public delegate* unmanaged[Cdecl]<edict_t*, void> CmdEnd;

    public delegate* unmanaged[Cdecl]<netadr_s*, sbyte*, sbyte*, int*, int> ConnectionlessPacket;

    public delegate* unmanaged[Cdecl]<int, float*, float*, int> GetHullBounds;

    public delegate* unmanaged[Cdecl]<void> CreateInstancedBaselines;

    public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, sbyte*, int> InconsistentFile;

    public delegate* unmanaged[Cdecl]<int> AllowLagCompensation;
}
