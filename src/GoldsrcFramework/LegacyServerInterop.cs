using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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

        // 供 legacy DLL 使用的引擎函数表副本，替换掉 FunctionFromName/NameForFunction
        private static ServerEngineFuncs* _patchedEngineFuncs = null;
        private static IntPtr _legacyServerModule = IntPtr.Zero;
        private static readonly object _legacyModuleLock = new();
        private static readonly Dictionary<string, nuint> _legacyNameToFunction = new(StringComparer.Ordinal);
        private static readonly Dictionary<nuint, IntPtr> _legacyFunctionToName = new();
        private static readonly Dictionary<string, IntPtr> _utf8StringCache = new(StringComparer.Ordinal);

        // 声明原版 hl.dll 的导出函数
        /// <summary>
        /// 引擎调用此函数来传递引擎函数指针和全局变量
        /// </summary>
        /// <param name="pengfuncsFromEngine">引擎函数指针</param>
        /// <param name="pGlobals">全局变量指针</param>
        [DllImport(LegacyServerDll, CallingConvention = CallingConvention.StdCall)]
        private static extern void GiveFnptrsToDll(ServerEngineFuncs* pengfuncsFromEngine, globalvars_t* pGlobals);

        /// <summary>
        /// 获取实体API函数表 (版本2)
        /// </summary>
        [DllImport(LegacyServerDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetEntityAPI2(ServerExportFuncs* pFunctionTable, int* interfaceVersion);

        /// <summary>
        /// 获取新的DLL函数表
        /// </summary>
        [DllImport(LegacyServerDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetNewDLLFunctions(ServerNewExportFuncs* pFunctionTable, int* interfaceVersion);

        public static void Initialize(ServerEngineFuncs* pengfuncsFromEngine, globalvars_t* pGlobals)
        {
            if (LegacyServerApiPtr is not null)
                return;

            EnsureLegacyModuleLoaded();
            var patchedEngineFuncs = GetPatchedEngineFuncs(pengfuncsFromEngine);

            // 分配函数表内存
            LegacyServerApiPtr = (ServerExportFuncs*)Marshal.AllocHGlobal(sizeof(ServerExportFuncs));
            LegacyServerNewApiPtr = (ServerNewExportFuncs*)Marshal.AllocHGlobal(sizeof(ServerNewExportFuncs));

            // 清零内存
            new Span<byte>(LegacyServerApiPtr, sizeof(ServerExportFuncs)).Clear();
            new Span<byte>(LegacyServerNewApiPtr, sizeof(ServerNewExportFuncs)).Clear();

            // 获取函数表
            GiveFnptrsToDll(patchedEngineFuncs, pGlobals);

            int version = 140;
            var res = GetEntityAPI2(LegacyServerApiPtr, &version);
            version = 1;
            var res2 = GetNewDLLFunctions(LegacyServerNewApiPtr, &version);

            Debug.WriteLine($"[LegacyServerInterop] GetEntityAPI2={res}, GetNewDLLFunctions={res2}");

            if (LegacyServerNewApiPtr != null)
            {
                // Log if null entrys found in the new API.
                if (LegacyServerNewApiPtr->OnFreeEntPrivateData == null) Debug.WriteLine("Warning: New API OnFreeEntPrivateData is null!");
                if (LegacyServerNewApiPtr->GameDLLShutdown == null) Debug.WriteLine("Warning: New API GameDLLShutdown is null!");
                if (LegacyServerNewApiPtr->ShouldCollide == null) Debug.WriteLine("Warning: New API ShouldCollide is null!");
                if (LegacyServerNewApiPtr->CvarValue == null) Debug.WriteLine("Warning: New API CvarValue is null!");
                if (LegacyServerNewApiPtr->CvarValue2 == null) Debug.WriteLine("Warning: New API CvarValue2 is null!");
            }
        }


        private static void EnsureLegacyModuleLoaded()
        {
            if (_legacyServerModule != IntPtr.Zero)
                return;

            lock (_legacyModuleLock)
            {
                if (_legacyServerModule != IntPtr.Zero)
                    return;

                _legacyServerModule = NativeLibrary.Load(LegacyServerDll);
                BuildLegacyExportMaps(_legacyServerModule);
            }
        }

        private static ServerEngineFuncs* GetPatchedEngineFuncs(ServerEngineFuncs* source)
        {
            if (_patchedEngineFuncs != null)
                return _patchedEngineFuncs;

            _patchedEngineFuncs = (ServerEngineFuncs*)Marshal.AllocHGlobal(sizeof(ServerEngineFuncs));
            Buffer.MemoryCopy(source, _patchedEngineFuncs, sizeof(ServerEngineFuncs), sizeof(ServerEngineFuncs));
            _patchedEngineFuncs->FunctionFromName = &LegacyFunctionFromName;
            _patchedEngineFuncs->NameForFunction = &LegacyNameForFunction;
            return _patchedEngineFuncs;
        }

        private static void BuildLegacyExportMaps(IntPtr module)
        {
            byte* basePtr = (byte*)module;
            if (*(ushort*)basePtr != 0x5A4D)
                throw new InvalidDataException("Invalid MZ header in libserver.dll.");

            int peOffset = *(int*)(basePtr + 0x3C);
            byte* ntHeaders = basePtr + peOffset;
            if (*(uint*)ntHeaders != 0x00004550)
                throw new InvalidDataException("Invalid PE signature in libserver.dll.");

            ushort sizeOfOptionalHeader = *(ushort*)(ntHeaders + 20);
            byte* optionalHeader = ntHeaders + 24;
            ushort magic = *(ushort*)optionalHeader;
            int dataDirectoryOffset = magic == 0x20B ? 112 : 96;
            if (sizeOfOptionalHeader < dataDirectoryOffset + 8)
                throw new InvalidDataException("Optional header too small in libserver.dll.");

            uint exportRva = *(uint*)(optionalHeader + dataDirectoryOffset);
            uint exportSize = *(uint*)(optionalHeader + dataDirectoryOffset + 4);
            if (exportRva == 0 || exportSize == 0)
                return;

            byte* exportDirectory = basePtr + exportRva;
            uint numberOfNames = *(uint*)(exportDirectory + 24);
            uint addressOfFunctions = *(uint*)(exportDirectory + 28);
            uint addressOfNames = *(uint*)(exportDirectory + 32);
            uint addressOfOrdinals = *(uint*)(exportDirectory + 36);

            for (uint i = 0; i < numberOfNames; i++)
            {
                uint nameRva = *(uint*)(basePtr + addressOfNames + i * 4);
                if (nameRva == 0)
                    continue;

                string? rawName = Marshal.PtrToStringUTF8((IntPtr)(basePtr + nameRva));
                if (string.IsNullOrEmpty(rawName))
                    continue;

                ushort ordinal = *(ushort*)(basePtr + addressOfOrdinals + i * 2);
                uint functionRva = *(uint*)(basePtr + addressOfFunctions + ordinal * 4);
                if (functionRva == 0)
                    continue;

                // forwarded exports live inside the export directory; they do not point at code
                if (functionRva >= exportRva && functionRva < exportRva + exportSize)
                    continue;

                string normalizedName = NormalizeFunctionName(rawName);
                nuint functionAddress = (nuint)(basePtr + functionRva);

                if (!_legacyNameToFunction.ContainsKey(normalizedName))
                    _legacyNameToFunction.Add(normalizedName, functionAddress);

                if (!_legacyFunctionToName.ContainsKey(functionAddress))
                    _legacyFunctionToName.Add(functionAddress, GetCachedUtf8String(normalizedName));
            }
        }

        private static string NormalizeFunctionName(string name)
        {
            if (name.Length > 0 && name[0] == '?')
            {
                int end = name.IndexOf("@@", StringComparison.Ordinal);
                if (end > 0)
                    return name[1..end];
            }

            return name;
        }

        private static IntPtr GetCachedUtf8String(string text)
        {
            if (_utf8StringCache.TryGetValue(text, out var cached))
                return cached;

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            IntPtr memory = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            Marshal.WriteByte(memory, bytes.Length, 0);
            _utf8StringCache[text] = memory;
            return memory;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static uint LegacyFunctionFromName(NChar* pName)
        {
            if (pName == null)
                return 0;

            string name = Marshal.PtrToStringUTF8((IntPtr)pName) ?? string.Empty;
            if (name.Length == 0)
                return 0;

            EnsureLegacyModuleLoaded();

            string normalized = NormalizeFunctionName(name);
            if (_legacyNameToFunction.TryGetValue(normalized, out var address))
                return unchecked((uint)address);

            if (NativeLibrary.TryGetExport(_legacyServerModule, normalized, out var exact))
                return unchecked((uint)exact);

            if (!ReferenceEquals(normalized, name) && NativeLibrary.TryGetExport(_legacyServerModule, name, out exact))
                return unchecked((uint)exact);

            Debug.WriteLine($"[LegacyServerInterop] Can't find proc: {name}");
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static NChar* LegacyNameForFunction(uint function)
        {
            EnsureLegacyModuleLoaded();

            nuint address = function;
            if (_legacyFunctionToName.TryGetValue(address, out var namePtr))
                return (NChar*)namePtr;

            Debug.WriteLine($"[LegacyServerInterop] Can't find address: 0x{function:X8}");
            return null;
        }

        // DLL_FUNCTIONS 静态转发方法
        public static void GameInit() => LegacyServerApiPtr->GameDLLInit();

        public static int Spawn(edict_t* pent) => LegacyServerApiPtr->DispatchSpawn(pent);

        public static void Think(edict_t* pent) => LegacyServerApiPtr->DispatchThink(pent);

        public static void Use(edict_t* pentUsed, edict_t* pentOther) => LegacyServerApiPtr->DispatchUse(pentUsed, pentOther);

        public static void Touch(edict_t* pentTouched, edict_t* pentOther) => LegacyServerApiPtr->DispatchTouch(pentTouched, pentOther);

        public static void Blocked(edict_t* pentBlocked, edict_t* pentOther) => LegacyServerApiPtr->DispatchBlocked(pentBlocked, pentOther);

        public static void KeyValue(edict_t* pentKeyvalue, KeyValueData* pkvd) => LegacyServerApiPtr->DispatchKeyValue(pentKeyvalue, pkvd);

        public static void Save(edict_t* pent, SAVERESTOREDATA* pSaveData) => LegacyServerApiPtr->DispatchSave(pent, pSaveData);

        public static int Restore(edict_t* pent, SAVERESTOREDATA* pSaveData, int globalEntity) => LegacyServerApiPtr->DispatchRestore(pent, pSaveData, globalEntity);

        public static void SetAbsBox(edict_t* pent) => LegacyServerApiPtr->DispatchObjectCollsionBox(pent);

        public static void SaveWriteFields(SAVERESTOREDATA* pSaveData, NChar* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => LegacyServerApiPtr->SaveWriteFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        public static void SaveReadFields(SAVERESTOREDATA* pSaveData, NChar* pname, void* pBaseData, TYPEDESCRIPTION* pFields, int fieldCount)
            => LegacyServerApiPtr->SaveReadFields(pSaveData, pname, pBaseData, pFields, fieldCount);

        public static void SaveGlobalState(SAVERESTOREDATA* pSaveData) => LegacyServerApiPtr->SaveGlobalState(pSaveData);

        public static void RestoreGlobalState(SAVERESTOREDATA* pSaveData) => LegacyServerApiPtr->RestoreGlobalState(pSaveData);

        public static void ResetGlobalState() => LegacyServerApiPtr->ResetGlobalState();

        public static qboolean ClientConnect(edict_t* pEntity, NChar* pszName, NChar* pszAddress, NChar* szRejectReason)
            => LegacyServerApiPtr->ClientConnect(pEntity, pszName, pszAddress, szRejectReason);

        public static void ClientDisconnect(edict_t* pEntity) => LegacyServerApiPtr->ClientDisconnect(pEntity);

        public static void ClientKill(edict_t* pEntity) => LegacyServerApiPtr->ClientKill(pEntity);

        public static void ClientPutInServer(edict_t* pEntity) => LegacyServerApiPtr->ClientPutInServer(pEntity);

        public static void ClientCommand(edict_t* pEntity) => LegacyServerApiPtr->ClientCommand(pEntity);

        public static void ClientUserInfoChanged(edict_t* pEntity, NChar* infobuffer) => LegacyServerApiPtr->ClientUserInfoChanged(pEntity, infobuffer);

        public static void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax) => LegacyServerApiPtr->ServerActivate(pEdictList, edictCount, clientMax);

        public static void ServerDeactivate() => LegacyServerApiPtr->ServerDeactivate();

        public static void PlayerPreThink(edict_t* pEntity) => LegacyServerApiPtr->PlayerPreThink(pEntity);

        public static void PlayerPostThink(edict_t* pEntity) => LegacyServerApiPtr->PlayerPostThink(pEntity);

        public static void StartFrame() => LegacyServerApiPtr->StartFrame();

        public static void ParmsNewLevel() => LegacyServerApiPtr->ParmsNewLevel();

        public static void ParmsChangeLevel() => LegacyServerApiPtr->ParmsChangeLevel();

        public static NChar* GetGameDescription() => LegacyServerApiPtr->GetGameDescription();

        public static void PlayerCustomization(edict_t* pEntity, customization_t* pCust) => LegacyServerApiPtr->PlayerCustomization(pEntity, pCust);

        public static void SpectatorConnect(edict_t* pEntity) => LegacyServerApiPtr->SpectatorConnect(pEntity);

        public static void SpectatorDisconnect(edict_t* pEntity) => LegacyServerApiPtr->SpectatorDisconnect(pEntity);

        public static void SpectatorThink(edict_t* pEntity) => LegacyServerApiPtr->SpectatorThink(pEntity);

        public static void Sys_Error(NChar* error_string) => LegacyServerApiPtr->Sys_Error(error_string);

        public static void PM_Move(playermove_t* ppmove, qboolean server) => LegacyServerApiPtr->PM_Move(ppmove, server);

        public static void PM_Init(playermove_t* ppmove) => LegacyServerApiPtr->PM_Init(ppmove);

        public static NChar PM_FindTextureType(NChar* name) => LegacyServerApiPtr->PM_FindTextureType(name);

        public static void SetupVisibility(edict_t* pViewEntity, edict_t* pClient, byte** pvs, byte** pas)
            => LegacyServerApiPtr->SetupVisibility(pViewEntity, pClient, pvs, pas);

        public static void UpdateClientData(edict_t* ent, int sendweapons, clientdata_t* cd)
            => LegacyServerApiPtr->UpdateClientData(ent, sendweapons, cd);

        public static int AddToFullPack(entity_state_t* state, int e, edict_t* ent, edict_t* host, int hostflags, int player, byte* pSet)
            => LegacyServerApiPtr->AddToFullPack(state, e, ent, host, hostflags, player, pSet);

        public static void CreateBaseline(int player, int eindex, entity_state_t* baseline, edict_t* entity, int playermodelindex, Vector3* player_mins, Vector3* player_maxs)
            => LegacyServerApiPtr->CreateBaseline(player, eindex, baseline, entity, playermodelindex, player_mins, player_maxs);

        public static void RegisterEncoders() => LegacyServerApiPtr->RegisterEncoders();

        public static int GetWeaponData(edict_t* player, weapon_data_t* info) => LegacyServerApiPtr->GetWeaponData(player, info);

        public static void CmdStart(edict_t* player, usercmd_t* cmd, uint random_seed) => LegacyServerApiPtr->CmdStart(player, cmd, random_seed);

        public static void CmdEnd(edict_t* player) => LegacyServerApiPtr->CmdEnd(player);

        public static int ConnectionlessPacket(netadr_t* net_from, NChar* args, NChar* response_buffer, int* response_buffer_size)
            => LegacyServerApiPtr->ConnectionlessPacket(net_from, args, response_buffer, response_buffer_size);

        public static int GetHullBounds(int hullnumber, float* mins, float* maxs) => LegacyServerApiPtr->GetHullBounds(hullnumber, mins, maxs);

        public static void CreateInstancedBaselines() => LegacyServerApiPtr->CreateInstancedBaselines();

        public static int InconsistentFile(edict_t* player, NChar* filename, NChar* disconnect_message)
            => LegacyServerApiPtr->InconsistentFile(player, filename, disconnect_message);

        public static int AllowLagCompensation() => LegacyServerApiPtr->AllowLagCompensation();

        // NEW_DLL_FUNCTIONS 静态转发方法
        public static void OnFreeEntPrivateData(edict_t* pEnt)
        {
            if (LegacyServerNewApiPtr != null && LegacyServerNewApiPtr->OnFreeEntPrivateData != null)
                LegacyServerNewApiPtr->OnFreeEntPrivateData(pEnt);
        }

        public static void GameShutdown()
        {
            if (LegacyServerNewApiPtr != null && LegacyServerNewApiPtr->GameDLLShutdown != null)
                LegacyServerNewApiPtr->GameDLLShutdown();
        }

        public static int ShouldCollide(edict_t* pentTouched, edict_t* pentOther)
        {
            if (LegacyServerNewApiPtr != null && LegacyServerNewApiPtr->ShouldCollide != null)
                return LegacyServerNewApiPtr->ShouldCollide(pentTouched, pentOther);

            return 1;
        }

        public static void CvarValue(edict_t* pEnt, NChar* value)
        {
            if (LegacyServerNewApiPtr != null && LegacyServerNewApiPtr->CvarValue != null)
                LegacyServerNewApiPtr->CvarValue(pEnt, value);
        }

        public static void CvarValue2(edict_t* pEnt, int requestID, NChar* cvarName, NChar* value)
        {
            if (LegacyServerNewApiPtr != null && LegacyServerNewApiPtr->CvarValue2 != null)
                LegacyServerNewApiPtr->CvarValue2(pEnt, requestID, cvarName, value);
        }
    }
}
