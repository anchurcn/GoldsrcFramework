using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GoldsrcFramework.Engine.Native;

namespace GoldsrcFramework
{
    internal unsafe static class ServerMain
    {
        // 接口版本常量
        private const int INTERFACE_VERSION = 140;
        private const int NEW_DLL_FUNCTIONS_VERSION = 1;

        // 静态变量存储服务端实例和引擎函数
        private static IServerExportFuncs s_server = null!;
        public static enginefuncs_s* s_engineFuncs = null;
        public static globalvars_t* s_globalVars = null;

        /// <summary>
        /// Initialize server with game assembly (called from FrameworkInterop)
        /// </summary>
        public static void Initialize(Assembly? gameAssembly)
        {
            if (_inited) return;
            _inited = true;

            try
            {
                if (gameAssembly != null)
                {
                    // Find type implementing IServerExportFuncs
                    var serverType = gameAssembly.GetTypes()
                        .FirstOrDefault(x => x.GetInterface(nameof(IServerExportFuncs)) == typeof(IServerExportFuncs));

                    if (serverType != null)
                    {
                        s_server = (IServerExportFuncs)Activator.CreateInstance(serverType)!;
                        return;
                    }
                }

                // Fallback to framework default implementation
                s_server = new FrameworkServerExports();
            }
            catch (Exception ex)
            {
                // Error fallback to framework default
                s_server = new FrameworkServerExports();
                System.Diagnostics.Debug.WriteLine($"Server initialization error: {ex.Message}");
            }
        }

        /// <summary>
        /// 引擎调用此函数来传递引擎函数指针和全局变量
        /// </summary>
        /// <param name="pengfuncsFromEngine">引擎函数指针</param>
        /// <param name="pGlobals">全局变量指针</param>
        public static void GiveFnptrsToDll(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals)
        {
            // 保存引擎函数指针和全局变量
            s_engineFuncs = pengfuncsFromEngine;
            s_globalVars = pGlobals;

            // Server instance should be initialized by FrameworkInterop before this call
        }

        /// <summary>
        /// 获取实体API函数表 (版本1)
        /// </summary>
        /// <param name="pFunctionTable">函数表指针</param>
        /// <param name="interfaceVersion">接口版本</param>
        /// <returns>成功返回1，失败返回0</returns>
        public static int GetEntityAPI(ServerExportFuncs* pFunctionTable, int interfaceVersion)
        {
            if (pFunctionTable == null || interfaceVersion != INTERFACE_VERSION)
            {
                return 0;
            }

            // 填充函数表
            FillServerExportFuncs(pFunctionTable);
            return 1;
        }

        /// <summary>
        /// 获取实体API函数表 (版本2)
        /// </summary>
        /// <param name="pFunctionTable">函数表指针</param>
        /// <param name="interfaceVersion">接口版本指针</param>
        /// <returns>成功返回1，失败返回0</returns>
        public static int GetEntityAPI2(ServerExportFuncs* pFunctionTable, int* interfaceVersion)
        {
            if (pFunctionTable == null || *interfaceVersion != INTERFACE_VERSION)
            {
                // 告诉引擎我们支持的版本
                *interfaceVersion = INTERFACE_VERSION;
                return 0;
            }

            // 填充函数表
            FillServerExportFuncs(pFunctionTable);
            return 1;
        }

        /// <summary>
        /// 获取新的DLL函数表
        /// </summary>
        /// <param name="pFunctionTable">新函数表指针</param>
        /// <param name="interfaceVersion">接口版本指针</param>
        /// <returns>成功返回1，失败返回0</returns>
        public static int GetNewDLLFunctions(ServerNewExportFuncs* pFunctionTable, int* interfaceVersion)
        {
            if (pFunctionTable == null || *interfaceVersion != NEW_DLL_FUNCTIONS_VERSION)
            {
                // 告诉引擎我们支持的版本
                *interfaceVersion = NEW_DLL_FUNCTIONS_VERSION;
                return 0;
            }

            // 填充新函数表
            FillServerNewExportFuncs(pFunctionTable);
            return 1;
        }

        private static bool _inited = false;

        /// <summary>
        /// 填充服务端导出函数表
        /// </summary>
        /// <param name="pFunctionTable">函数表指针</param>
        private static void FillServerExportFuncs(ServerExportFuncs* pFunctionTable)
        {
            pFunctionTable->GameInit = &GameInit;
            pFunctionTable->Spawn = &Spawn;
            pFunctionTable->Think = &Think;
            pFunctionTable->Use = &Use;
            pFunctionTable->Touch = &Touch;
            pFunctionTable->Blocked = &Blocked;
            pFunctionTable->KeyValue = &KeyValue;
            pFunctionTable->Save = &Save;
            pFunctionTable->Restore = &Restore;
            pFunctionTable->SetAbsBox = &SetAbsBox;
            pFunctionTable->SaveWriteFields = &SaveWriteFields;
            pFunctionTable->SaveReadFields = &SaveReadFields;
            pFunctionTable->SaveGlobalState = &SaveGlobalState;
            pFunctionTable->RestoreGlobalState = &RestoreGlobalState;
            pFunctionTable->ResetGlobalState = &ResetGlobalState;
            pFunctionTable->ClientConnect = &ClientConnect;
            pFunctionTable->ClientDisconnect = &ClientDisconnect;
            pFunctionTable->ClientKill = &ClientKill;
            pFunctionTable->ClientPutInServer = &ClientPutInServer;
            pFunctionTable->ClientCommand = &ClientCommand;
            pFunctionTable->ClientUserInfoChanged = &ClientUserInfoChanged;
            pFunctionTable->ServerActivate = &ServerActivate;
            pFunctionTable->ServerDeactivate = &ServerDeactivate;
            pFunctionTable->PlayerPreThink = &PlayerPreThink;
            pFunctionTable->PlayerPostThink = &PlayerPostThink;
            pFunctionTable->StartFrame = &StartFrame;
            pFunctionTable->ParmsNewLevel = &ParmsNewLevel;
            pFunctionTable->ParmsChangeLevel = &ParmsChangeLevel;
            pFunctionTable->GetGameDescription = &GetGameDescription;
            pFunctionTable->PlayerCustomization = &PlayerCustomization;
            pFunctionTable->SpectatorConnect = &SpectatorConnect;
            pFunctionTable->SpectatorDisconnect = &SpectatorDisconnect;
            pFunctionTable->SpectatorThink = &SpectatorThink;
            pFunctionTable->Sys_Error = &Sys_Error;
            pFunctionTable->PM_Move = &PM_Move;
            pFunctionTable->PM_Init = &PM_Init;
            pFunctionTable->PM_FindTextureType = &PM_FindTextureType;
            pFunctionTable->SetupVisibility = &SetupVisibility;
            pFunctionTable->UpdateClientData = &UpdateClientData;
            pFunctionTable->AddToFullPack = &AddToFullPack;
            pFunctionTable->CreateBaseline = &CreateBaseline;
            pFunctionTable->RegisterEncoders = &RegisterEncoders;
            pFunctionTable->GetWeaponData = &GetWeaponData;
            pFunctionTable->CmdStart = &CmdStart;
            pFunctionTable->CmdEnd = &CmdEnd;
            pFunctionTable->ConnectionlessPacket = &ConnectionlessPacket;
            pFunctionTable->GetHullBounds = &GetHullBounds;
            pFunctionTable->CreateInstancedBaselines = &CreateInstancedBaselines;
            pFunctionTable->InconsistentFile = &InconsistentFile;
            pFunctionTable->AllowLagCompensation = &AllowLagCompensation;
        }

        /// <summary>
        /// 填充新服务端导出函数表
        /// </summary>
        /// <param name="pFunctionTable">新函数表指针</param>
        private static void FillServerNewExportFuncs(ServerNewExportFuncs* pFunctionTable)
        {
            pFunctionTable->OnFreeEntPrivateData = &OnFreeEntPrivateData;
            pFunctionTable->GameShutdown = &GameShutdown;
            pFunctionTable->ShouldCollide = &ShouldCollide;
            pFunctionTable->CvarValue = &CvarValue;
            pFunctionTable->CvarValue2 = &CvarValue2;
        }

        // ========== DLL_FUNCTIONS 实现 ==========

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void GameInit() => s_server.GameInit();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int Spawn(edict_t* pent) => s_server.Spawn(pent);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Think(edict_t* pent) => s_server.Think(pent);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Use(edict_t* pentUsed, edict_t* pentOther) => s_server.Use(pentUsed, pentOther);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Touch(edict_t* pentTouched, edict_t* pentOther) => s_server.Touch(pentTouched, pentOther);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Blocked(edict_t* pentBlocked, edict_t* pentOther) => s_server.Blocked(pentBlocked, pentOther);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd) => s_server.KeyValue(pentKeyvalue, pkvd);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Save(edict_t* pent, SAVERESTOREDATA* pSaveData) => s_server.Save(pent, pSaveData);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity) => s_server.Restore(pent, pSaveData, globalEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SetAbsBox(edict_t* pent) => s_server.SetAbsBox(pent);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => s_server.SaveWriteFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => s_server.SaveReadFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SaveGlobalState(SAVERESTOREDATA* pSaveData) => s_server.SaveGlobalState(pSaveData);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RestoreGlobalState(SAVERESTOREDATA* pSaveData) => s_server.RestoreGlobalState(pSaveData);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ResetGlobalState() => s_server.ResetGlobalState();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
            => s_server.ClientConnect(pEntity, pszName, pszAddress, szRejectReason);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ClientDisconnect(edict_t* pEntity) => s_server.ClientDisconnect(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ClientKill(edict_t* pEntity) => s_server.ClientKill(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ClientPutInServer(edict_t* pEntity) => s_server.ClientPutInServer(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ClientCommand(edict_t* pEntity) => s_server.ClientCommand(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer) => s_server.ClientUserInfoChanged(pEntity, infobuffer);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax) => s_server.ServerActivate(pEdictList, edictCount, clientMax);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ServerDeactivate() => s_server.ServerDeactivate();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void PlayerPreThink(edict_t* pEntity) => s_server.PlayerPreThink(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void PlayerPostThink(edict_t* pEntity) => s_server.PlayerPostThink(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void StartFrame() => s_server.StartFrame();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ParmsNewLevel() => s_server.ParmsNewLevel();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ParmsChangeLevel() => s_server.ParmsChangeLevel();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static sbyte* GetGameDescription() => s_server.GetGameDescription();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void PlayerCustomization(edict_t* pEntity, customization_t* pCust) => s_server.PlayerCustomization(pEntity, pCust);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SpectatorConnect(edict_t* pEntity) => s_server.SpectatorConnect(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SpectatorDisconnect(edict_t* pEntity) => s_server.SpectatorDisconnect(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SpectatorThink(edict_t* pEntity) => s_server.SpectatorThink(pEntity);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Sys_Error(sbyte* error_string) => s_server.Sys_Error(error_string);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void PM_Move(playermove_s* ppmove, qboolean server) => s_server.PM_Move(ppmove, server);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void PM_Init(playermove_s* ppmove) => s_server.PM_Init(ppmove);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static sbyte PM_FindTextureType(sbyte* name) => s_server.PM_FindTextureType(name);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas) => s_server.SetupVisibility(pViewEntity, pClient, pvs, pas);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd) => s_server.UpdateClientData(ent, sendweapons, cd);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
            => s_server.AddToFullPack(state, e, ent, host, hostflags, player, pSet);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3f* player_mins, Vector3f* player_maxs)
            => s_server.CreateBaseline(player, eindex, baseline, entity, playermodelindex, player_mins, player_maxs);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RegisterEncoders() => s_server.RegisterEncoders();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int GetWeaponData(edict_t* player, weapon_data_s* info) => s_server.GetWeaponData(player, info);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed) => s_server.CmdStart(player, cmd, random_seed);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CmdEnd(edict_t* player) => s_server.CmdEnd(player);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
            => s_server.ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int GetHullBounds(int hullnumber, float* mins, float* maxs) => s_server.GetHullBounds(hullnumber, mins, maxs);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CreateInstancedBaselines() => s_server.CreateInstancedBaselines();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message)
            => s_server.InconsistentFile(player, filename, disconnect_message);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int AllowLagCompensation() => s_server.AllowLagCompensation();

        // ========== NEW_DLL_FUNCTIONS 实现 ==========

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void OnFreeEntPrivateData(edict_t* pEnt) => s_server.OnFreeEntPrivateData(pEnt);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void GameShutdown() => s_server.GameShutdown();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int ShouldCollide(edict_t* pentTouched, edict_t* pentOther) => s_server.ShouldCollide(pentTouched, pentOther);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CvarValue(edict_t* pEnt, sbyte* value) => s_server.CvarValue(pEnt, value);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value) => s_server.CvarValue2(pEnt, requestID, cvarName, value);
    }
}
