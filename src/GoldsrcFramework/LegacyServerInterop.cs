using GoldsrcFramework.Engine.Native;
using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework
{
    /// <summary>
    /// 与原版 unmanaged hl.dll 交互的互操作类
    /// </summary>
    public unsafe static class LegacyServerInterop
    {
        private const string LegacyServerDll = "libserver.dll";

        // 存储从原版 DLL 获取的函数表
        private static ServerExportFuncs* Api = null;
        private static ServerNewExportFuncs* NewApi = null;

        private static ServerExportFuncs* s_api
        {
            get
            {
                if (Api is null)
                {
                    throw new InvalidOperationException();
                }
                return Api;
            }
        }

        private static ServerNewExportFuncs* s_newapi
        {
            get
            {
                if (NewApi is null)
                {
                    throw new InvalidOperationException();
                }
                return NewApi;
            }
        }

        // 声明原版 hl.dll 的导出函数
        /// <summary>
        /// 引擎调用此函数来传递引擎函数指针和全局变量
        /// </summary>
        /// <param name="pengfuncsFromEngine">引擎函数指针</param>
        /// <param name="pGlobals">全局变量指针</param>
        [DllImport(LegacyServerDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void GiveFnptrsToDll(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals);

        /// <summary>
        /// 获取实体API函数表 (版本2)
        /// </summary>
        /// <param name="pFunctionTable">函数表指针</param>
        /// <param name="interfaceVersion">接口版本指针</param>
        /// <returns>成功返回1，失败返回0</returns>
        [DllImport(LegacyServerDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetEntityAPI2(ServerExportFuncs* pFunctionTable, int* interfaceVersion);

        /// <summary>
        /// 获取新的DLL函数表
        /// </summary>
        /// <param name="pFunctionTable">新函数表指针</param>
        /// <param name="interfaceVersion">接口版本指针</param>
        /// <returns>成功返回1，失败返回0</returns>
        [DllImport(LegacyServerDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetNewDLLFunctions(ServerNewExportFuncs* pFunctionTable, int* interfaceVersion);

        public static void Initialize(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals)
        {
            if (Api is not null)
                return;

            // 分配函数表内存
            Api = (ServerExportFuncs*)Marshal.AllocHGlobal(sizeof(ServerExportFuncs));
            NewApi = (ServerNewExportFuncs*)Marshal.AllocHGlobal(sizeof(ServerNewExportFuncs));

            // 清零内存
            new Span<byte>(Api, sizeof(ServerExportFuncs)).Clear();
            new Span<byte>(NewApi, sizeof(ServerNewExportFuncs)).Clear();

            // 获取函数表
            int version = 140;
            GetEntityAPI2(Api, &version);
            version = 1;
            GetNewDLLFunctions(NewApi, &version);
            GiveFnptrsToDll(pengfuncsFromEngine, pGlobals);
        }

        // DLL_FUNCTIONS 静态转发方法
        public static void GameInit() => s_api->GameInit();

        public static int Spawn(edict_t* pent) => s_api->Spawn(pent);

        public static void Think(edict_t* pent) => s_api->Think(pent);

        public static void Use(edict_t* pentUsed, edict_t* pentOther) => s_api->Use(pentUsed, pentOther);

        public static void Touch(edict_t* pentTouched, edict_t* pentOther) => s_api->Touch(pentTouched, pentOther);

        public static void Blocked(edict_t* pentBlocked, edict_t* pentOther) => s_api->Blocked(pentBlocked, pentOther);

        public static void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd) => s_api->KeyValue(pentKeyvalue, pkvd);

        public static void Save(edict_t* pent, SAVERESTOREDATA* pSaveData) => s_api->Save(pent, pSaveData);

        public static int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity) => s_api->Restore(pent, pSaveData, globalEntity);

        public static void SetAbsBox(edict_t* pent) => s_api->SetAbsBox(pent);

        public static void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => s_api->SaveWriteFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        public static void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => s_api->SaveReadFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        public static void SaveGlobalState(SAVERESTOREDATA* pSaveData) => s_api->SaveGlobalState(pSaveData);

        public static void RestoreGlobalState(SAVERESTOREDATA* pSaveData) => s_api->RestoreGlobalState(pSaveData);

        public static void ResetGlobalState() => s_api->ResetGlobalState();

        public static qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
            => s_api->ClientConnect(pEntity, pszName, pszAddress, szRejectReason);

        public static void ClientDisconnect(edict_t* pEntity) => s_api->ClientDisconnect(pEntity);

        public static void ClientKill(edict_t* pEntity) => s_api->ClientKill(pEntity);

        public static void ClientPutInServer(edict_t* pEntity) => s_api->ClientPutInServer(pEntity);

        public static void ClientCommand(edict_t* pEntity) => s_api->ClientCommand(pEntity);

        public static void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer) => s_api->ClientUserInfoChanged(pEntity, infobuffer);

        public static void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax) => s_api->ServerActivate(pEdictList, edictCount, clientMax);

        public static void ServerDeactivate() => s_api->ServerDeactivate();

        public static void PlayerPreThink(edict_t* pEntity) => s_api->PlayerPreThink(pEntity);

        public static void PlayerPostThink(edict_t* pEntity) => s_api->PlayerPostThink(pEntity);

        public static void StartFrame() => s_api->StartFrame();

        public static void ParmsNewLevel() => s_api->ParmsNewLevel();

        public static void ParmsChangeLevel() => s_api->ParmsChangeLevel();

        public static sbyte* GetGameDescription() => s_api->GetGameDescription();

        public static void PlayerCustomization(edict_t* pEntity, customization_t* pCust) => s_api->PlayerCustomization(pEntity, pCust);

        public static void SpectatorConnect(edict_t* pEntity) => s_api->SpectatorConnect(pEntity);

        public static void SpectatorDisconnect(edict_t* pEntity) => s_api->SpectatorDisconnect(pEntity);

        public static void SpectatorThink(edict_t* pEntity) => s_api->SpectatorThink(pEntity);

        public static void Sys_Error(sbyte* error_string) => s_api->Sys_Error(error_string);

        public static void PM_Move(playermove_s* ppmove, qboolean server) => s_api->PM_Move(ppmove, server);

        public static void PM_Init(playermove_s* ppmove) => s_api->PM_Init(ppmove);

        public static sbyte PM_FindTextureType(sbyte* name) => s_api->PM_FindTextureType(name);

        public static void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas)
            => s_api->SetupVisibility(pViewEntity, pClient, pvs, pas);

        public static void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd)
            => s_api->UpdateClientData(ent, sendweapons, cd);

        public static int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
            => s_api->AddToFullPack(state, e, ent, host, hostflags, player, pSet);

        public static void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3f* player_mins, Vector3f* player_maxs)
            => s_api->CreateBaseline(player, eindex, baseline, entity, playermodelindex, player_mins, player_maxs);

        public static void RegisterEncoders() => s_api->RegisterEncoders();

        public static int GetWeaponData(edict_t* player, weapon_data_s* info) => s_api->GetWeaponData(player, info);

        public static void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed) => s_api->CmdStart(player, cmd, random_seed);

        public static void CmdEnd(edict_t* player) => s_api->CmdEnd(player);

        public static int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
            => s_api->ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);

        public static int GetHullBounds(int hullnumber, float* mins, float* maxs) => s_api->GetHullBounds(hullnumber, mins, maxs);

        public static void CreateInstancedBaselines() => s_api->CreateInstancedBaselines();

        public static int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message)
            => s_api->InconsistentFile(player, filename, disconnect_message);

        public static int AllowLagCompensation() => s_api->AllowLagCompensation();

        // NEW_DLL_FUNCTIONS 静态转发方法
        public static void OnFreeEntPrivateData(edict_t* pEnt) => s_newapi->OnFreeEntPrivateData(pEnt);

        public static void GameShutdown() => s_newapi->GameShutdown();

        public static int ShouldCollide(edict_t* pentTouched, edict_t* pentOther) => s_newapi->ShouldCollide(pentTouched, pentOther);

        public static void CvarValue(edict_t* pEnt, sbyte* value) => s_newapi->CvarValue(pEnt, value);

        public static void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value)
            => s_newapi->CvarValue2(pEnt, requestID, cvarName, value);
    }
}