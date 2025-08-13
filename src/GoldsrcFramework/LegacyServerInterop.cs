using GoldsrcFramework.Engine.Native;
using System;
using System.Diagnostics;
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
        private static ServerExportFuncs* LegacyServerApiPtr = null;
        private static ServerNewExportFuncs* LegacyServerNewApiPtr = null;

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
            if (LegacyServerApiPtr is not null)
                return;

            // 分配函数表内存
            LegacyServerApiPtr = (ServerExportFuncs*)Marshal.AllocHGlobal(sizeof(ServerExportFuncs));
            LegacyServerNewApiPtr = (ServerNewExportFuncs*)Marshal.AllocHGlobal(sizeof(ServerNewExportFuncs));

            // 清零内存
            var span = new Span<IntPtr>(LegacyServerApiPtr, sizeof(ServerExportFuncs) / sizeof(IntPtr));
            var spannew = new Span<IntPtr>(LegacyServerNewApiPtr, sizeof(ServerNewExportFuncs) / sizeof(IntPtr));

            // 获取函数表
            // TODO validation and logging
            GiveFnptrsToDll(pengfuncsFromEngine, pGlobals);
            int version = 140;
            var res = GetEntityAPI2(LegacyServerApiPtr, &version);
            version = 1;
            var res2 = GetNewDLLFunctions(LegacyServerNewApiPtr, &version);
            if (LegacyServerNewApiPtr != null)
            {
                // Log if null entrys found in the new API.
                if(LegacyServerNewApiPtr->OnFreeEntPrivateData == null) Debug.WriteLine("Warning: New API OnFreeEntPrivateData is null!");
                if(LegacyServerNewApiPtr->GameShutdown == null) Debug.WriteLine("Warning: New API GameShutdown is null!");
                if(LegacyServerNewApiPtr->ShouldCollide == null) Debug.WriteLine("Warning: New API ShouldCollide is null!");
                if(LegacyServerNewApiPtr->CvarValue == null) Debug.WriteLine("Warning: New API CvarValue is null!");
                if(LegacyServerNewApiPtr->CvarValue2 == null) Debug.WriteLine("Warning: New API CvarValue2 is null!");
            }
        }

        // DLL_FUNCTIONS 静态转发方法
        public static void GameInit() => LegacyServerApiPtr->GameInit();

        public static int Spawn(edict_t* pent) => LegacyServerApiPtr->Spawn(pent);

        public static void Think(edict_t* pent) => LegacyServerApiPtr->Think(pent);

        public static void Use(edict_t* pentUsed, edict_t* pentOther) => LegacyServerApiPtr->Use(pentUsed, pentOther);

        public static void Touch(edict_t* pentTouched, edict_t* pentOther) => LegacyServerApiPtr->Touch(pentTouched, pentOther);

        public static void Blocked(edict_t* pentBlocked, edict_t* pentOther) => LegacyServerApiPtr->Blocked(pentBlocked, pentOther);

        public static void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd) => LegacyServerApiPtr->KeyValue(pentKeyvalue, pkvd);

        public static void Save(edict_t* pent, SAVERESTOREDATA* pSaveData) => LegacyServerApiPtr->Save(pent, pSaveData);

        public static int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity) => LegacyServerApiPtr->Restore(pent, pSaveData, globalEntity);

        public static void SetAbsBox(edict_t* pent) => LegacyServerApiPtr->SetAbsBox(pent);

        public static void SaveWriteFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => LegacyServerApiPtr->SaveWriteFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        public static void SaveReadFields(SAVERESTOREDATA* pSaveData, sbyte* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => LegacyServerApiPtr->SaveReadFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        public static void SaveGlobalState(SAVERESTOREDATA* pSaveData) => LegacyServerApiPtr->SaveGlobalState(pSaveData);

        public static void RestoreGlobalState(SAVERESTOREDATA* pSaveData) => LegacyServerApiPtr->RestoreGlobalState(pSaveData);

        public static void ResetGlobalState() => LegacyServerApiPtr->ResetGlobalState();

        public static qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
            => LegacyServerApiPtr->ClientConnect(pEntity, pszName, pszAddress, szRejectReason);

        public static void ClientDisconnect(edict_t* pEntity) => LegacyServerApiPtr->ClientDisconnect(pEntity);

        public static void ClientKill(edict_t* pEntity) => LegacyServerApiPtr->ClientKill(pEntity);

        public static void ClientPutInServer(edict_t* pEntity) => LegacyServerApiPtr->ClientPutInServer(pEntity);

        public static void ClientCommand(edict_t* pEntity) => LegacyServerApiPtr->ClientCommand(pEntity);

        public static void ClientUserInfoChanged(edict_t* pEntity, sbyte* infobuffer) => LegacyServerApiPtr->ClientUserInfoChanged(pEntity, infobuffer);

        public static void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax) => LegacyServerApiPtr->ServerActivate(pEdictList, edictCount, clientMax);

        public static void ServerDeactivate() => LegacyServerApiPtr->ServerDeactivate();

        public static void PlayerPreThink(edict_t* pEntity) => LegacyServerApiPtr->PlayerPreThink(pEntity);

        public static void PlayerPostThink(edict_t* pEntity) => LegacyServerApiPtr->PlayerPostThink(pEntity);

        public static void StartFrame() => LegacyServerApiPtr->StartFrame();

        public static void ParmsNewLevel() => LegacyServerApiPtr->ParmsNewLevel();

        public static void ParmsChangeLevel() => LegacyServerApiPtr->ParmsChangeLevel();

        public static sbyte* GetGameDescription() => LegacyServerApiPtr->GetGameDescription();

        public static void PlayerCustomization(edict_t* pEntity, customization_t* pCust) => LegacyServerApiPtr->PlayerCustomization(pEntity, pCust);

        public static void SpectatorConnect(edict_t* pEntity) => LegacyServerApiPtr->SpectatorConnect(pEntity);

        public static void SpectatorDisconnect(edict_t* pEntity) => LegacyServerApiPtr->SpectatorDisconnect(pEntity);

        public static void SpectatorThink(edict_t* pEntity) => LegacyServerApiPtr->SpectatorThink(pEntity);

        public static void Sys_Error(sbyte* error_string) => LegacyServerApiPtr->Sys_Error(error_string);

        public static void PM_Move(playermove_s* ppmove, qboolean server) => LegacyServerApiPtr->PM_Move(ppmove, server);

        public static void PM_Init(playermove_s* ppmove) => LegacyServerApiPtr->PM_Init(ppmove);

        public static sbyte PM_FindTextureType(sbyte* name) => LegacyServerApiPtr->PM_FindTextureType(name);

        public static void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas)
            => LegacyServerApiPtr->SetupVisibility(pViewEntity, pClient, pvs, pas);

        public static void UpdateClientData(edict_t* ent, int sendweapons, clientdata_s* cd)
            => LegacyServerApiPtr->UpdateClientData(ent, sendweapons, cd);

        public static int AddToFullPack(entity_state_s* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
            => LegacyServerApiPtr->AddToFullPack(state, e, ent, host, hostflags, player, pSet);

        public static void CreateBaseline(int player, int eindex, entity_state_s* baseline, edict_t* entity, int playermodelindex, Vector3f* player_mins, Vector3f* player_maxs)
            => LegacyServerApiPtr->CreateBaseline(player, eindex, baseline, entity, playermodelindex, player_mins, player_maxs);

        public static void RegisterEncoders() => LegacyServerApiPtr->RegisterEncoders();

        public static int GetWeaponData(edict_t* player, weapon_data_s* info) => LegacyServerApiPtr->GetWeaponData(player, info);

        public static void CmdStart(edict_t* player, usercmd_s* cmd, uint random_seed) => LegacyServerApiPtr->CmdStart(player, cmd, random_seed);

        public static void CmdEnd(edict_t* player) => LegacyServerApiPtr->CmdEnd(player);

        public static int ConnectionlessPacket(netadr_s* net_from, sbyte* args, sbyte* response_buffer, int* response_buffer_size)
            => LegacyServerApiPtr->ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);

        public static int GetHullBounds(int hullnumber, float* mins, float* maxs) => LegacyServerApiPtr->GetHullBounds(hullnumber, mins, maxs);

        public static void CreateInstancedBaselines() => LegacyServerApiPtr->CreateInstancedBaselines();

        public static int InconsistentFile(edict_t* player, sbyte* filename, sbyte* disconnect_message)
            => LegacyServerApiPtr->InconsistentFile(player, filename, disconnect_message);

        public static int AllowLagCompensation() => LegacyServerApiPtr->AllowLagCompensation();

        // NEW_DLL_FUNCTIONS 静态转发方法
        public static void OnFreeEntPrivateData(edict_t* pEnt) => LegacyServerNewApiPtr->OnFreeEntPrivateData(pEnt);

        public static void GameShutdown() {}

        public static int ShouldCollide(edict_t* pentTouched, edict_t* pentOther) { return default; }

        public static void CvarValue(edict_t* pEnt, sbyte* value) {}

        public static void CvarValue2(edict_t* pEnt, int requestID, sbyte* cvarName, sbyte* value)
        {

        }
    }
}