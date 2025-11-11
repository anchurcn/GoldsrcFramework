using GoldsrcFramework.Entity;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace GoldsrcFramework
{
    /// <summary>
    /// Framework interop layer - unified entry point for all native calls
    /// </summary>
    public unsafe class FrameworkInterop
    {
        // Framework initialization state
        private static bool _frameworkInitialized = false;
        private static readonly object _initLock = new object();

        // Initialization state
        private static bool _serverInitialized = false;
        private static bool _clientInitialized = false;

        #region Entity System

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static IntPtr GetPrivateDataAllocator(IntPtr pszEntityClassName)
        {
            if (pszEntityClassName == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(pszEntityClassName));
            }
            var entityClassName = Marshal.PtrToStringUTF8(pszEntityClassName)!;
            return EntityContext.GetLegacyEntityPrivateDataAllocator(entityClassName);
        }

        #endregion

        #region Server Entry Points

        /// <summary>
        /// Server entry point: GiveFnptrsToDll
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void GiveFnptrsToDll(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals)
        {
            EnsureFrameworkInitialized();
            EnsureServerInitialized();
            ServerMain.GiveFnptrsToDll(pengfuncsFromEngine, pGlobals);
        }

        /// <summary>
        /// Server entry point: GetEntityAPI
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int GetEntityAPI(ServerExportFuncs* pFunctionTable, int interfaceVersion)
        {
            EnsureFrameworkInitialized();
            EnsureServerInitialized();
            return ServerMain.GetEntityAPI(pFunctionTable, interfaceVersion);
        }

        /// <summary>
        /// Server entry point: GetEntityAPI2
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int GetEntityAPI2(ServerExportFuncs* pFunctionTable, int* interfaceVersion)
        {
            EnsureFrameworkInitialized();
            EnsureServerInitialized();
            return ServerMain.GetEntityAPI2(pFunctionTable, interfaceVersion);
        }

        /// <summary>
        /// Server entry point: GetNewDLLFunctions
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int GetNewDLLFunctions(ServerNewExportFuncs* pFunctionTable, int* interfaceVersion)
        {
            EnsureFrameworkInitialized();
            EnsureServerInitialized();
            return ServerMain.GetNewDLLFunctions(pFunctionTable, interfaceVersion);
        }

        #endregion

        #region Client Entry Points

        /// <summary>
        /// Client entry point: F function
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void F(ClientExportFuncs* pv)
        {
            EnsureFrameworkInitialized();
            EnsureClientInitialized();
            ClientMain.F(pv);
        }

        #endregion

        #region Framework Initialization

        /// <summary>
        /// Ensure framework is initialized (thread-safe)
        /// </summary>
        private static void EnsureFrameworkInitialized()
        {
            if (_frameworkInitialized) return;

            lock (_initLock)
            {
                if (_frameworkInitialized) return;

                // Set up environment variables
                var pathEnvVar = Environment.GetEnvironmentVariable("Path");
                var frameworkDir = Path.GetDirectoryName(typeof(FrameworkInterop).Assembly.Location);
                pathEnvVar += ";" + frameworkDir;
                Environment.SetEnvironmentVariable("Path", pathEnvVar);

                // Initialize DI container
                ServiceContainer.Initialize();

                _frameworkInitialized = true;
            }
        }

        /// <summary>
        /// Ensure server is initialized with game assembly
        /// </summary>
        private static void EnsureServerInitialized()
        {
            if (_serverInitialized) return;

            lock (_initLock)
            {
                if (_serverInitialized) return;

                // Initialize ServerMain using DI container
                ServerMain.Initialize();
                _serverInitialized = true;
            }
        }

        /// <summary>
        /// Ensure client is initialized with game assembly
        /// </summary>
        private static void EnsureClientInitialized()
        {
            if (_clientInitialized) return;

            lock (_initLock)
            {
                if (_clientInitialized) return;

                // Initialize ClientMain using DI container
                ClientMain.Initialize();
                _clientInitialized = true;
            }
        }

        #endregion
    }
}
