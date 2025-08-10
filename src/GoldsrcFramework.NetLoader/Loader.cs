using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Mxrx.NetHost;
using Rxmxnx.PInvoke;

[assembly: DisableRuntimeMarshalling]

namespace GoldsrcFramework.NetLoader
{
    public static partial class NetHost
    {
        public static string GetHostFxrPath()
        {
            if (OperatingSystem.IsWindows())
            {
                char[] buffer = new char[1024];
                nint bufferSize = buffer.Length;
                int rc = get_hostfxr_path_windows(buffer, ref bufferSize, IntPtr.Zero);
                if (rc == 0)
                {
                    return new string(buffer.AsSpan().Slice(0, (int)bufferSize - 1));
                }
                else
                {
                    throw new Exception($"get_hostfxr_path failed: {rc}");
                }
            }
            else
            {
                byte[] buffer = new byte[1024];
                nint bufferSize = buffer.Length;
                int rc = get_hostfxr_path_non_windows(buffer, ref bufferSize, IntPtr.Zero);
                if (rc == 0)
                {
                    return Encoding.UTF8.GetString(buffer.AsSpan().Slice(0, (int)bufferSize - 1));
                }
                else
                {
                    throw new Exception($"get_hostfxr_path failed: {rc}");
                }
            }
        }

        [LibraryImport("*", EntryPoint = "get_hostfxr_path")]
        private static partial int get_hostfxr_path_windows(char[] buffer, ref nint buffer_size, IntPtr parameters);

        [LibraryImport("*", EntryPoint = "get_hostfxr_path")]
        private static partial int get_hostfxr_path_non_windows(byte[] buffer, ref nint buffer_size, IntPtr parameters);
    }
    /// <summary>
    /// Native AOT loader implementation using Mxrx.NetHost.Fxr
    /// This replaces the C++ loader.cpp functionality
    /// </summary>
    public static partial class Loader
    {
        private static FrameworkResolver s_hostFxr = null!;
        private static HostContext s_hostContext = null!;
        private static string s_frameworkPath = string.Empty;

        /// <summary>
        /// Initialize the .NET host using Mxrx.NetHost.Fxr
        /// </summary>
        private static void InitializeNetHost()
        {
            if (s_hostContext is not null)
            {
                return;
            }

            try
            {
                // Get the current module directory
                string moduleDir = AppContext.BaseDirectory;
                string gsfPath = Path.Combine(moduleDir, "GoldsrcFramework.dll");
                string runtimeConfigPath = Path.Combine(moduleDir, "GoldsrcFramework.runtimeconfig.json");
                s_frameworkPath = gsfPath;

                Debug.WriteLine($"[Debug] Trying to load GoldsrcFramework from: {gsfPath}");

                if (!File.Exists(gsfPath) || !File.Exists(runtimeConfigPath))
                {
                    Debug.WriteLine($"[Error] GoldsrcFramework.dll or runtimeconfig.json not found in {moduleDir}");
                    throw new FileNotFoundException("GoldsrcFramework.dll or runtimeconfig.json not found.");
                }

                // Find hostfxr library
                string hostfxrPath = NetHost.GetHostFxrPath();
                if (string.IsNullOrEmpty(hostfxrPath))
                {
                    Debug.WriteLine("Hostfxr path is empty or null.");
                    throw new Exception("Hostfxr path is empty or null.");
                }

                // Load the framework resolver
                s_hostFxr = FrameworkResolver.GetActiveOrLoad(hostfxrPath, out var _);

                // Create initialization parameters
                InitializationParameters initParams = InitializationParameters.CreateBuilder()
                    .WithRuntimeConfigPath(runtimeConfigPath)
                    .Build();

                // Initialize the host context
                s_hostContext = s_hostFxr.Initialize(initParams);
            }
            catch
            {
                Debug.WriteLine("Failed to initialize .NET host.");
                throw new Exception("Failed to initialize .NET host.");
            }
        }

        private static void EnsureInitialized()
        {
            InitializeNetHost();
        }

        [UnmanagedCallersOnly(EntryPoint = "F", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static void F(IntPtr pv)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, void> pfn = (delegate* unmanaged[Cdecl]<IntPtr, void>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop", "F");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for F");
                throw new InvalidOperationException("Function pointer for F is null.");
            }
            pfn(pv);
        }

        private static IntPtr GetGoldsrcFrameworkFunctionPointer(string typeName, string methodName)
        {
            var builder = NetFunctionInfo.CreateBuilder()
                .WithAssemblyPathPath(s_frameworkPath)
                .WithUnmanagedCallerOnly(true);

            if (OperatingSystem.IsWindows())
            {
                builder = builder.WithTypeName($"{typeName}\0")
                                 .WithMethodName($"{methodName}\0");
            }
            else
            {
                builder = builder.WithTypeName(Encoding.UTF8.GetBytes($"{typeName}"))
                                 .WithMethodName(Encoding.UTF8.GetBytes(methodName));
            }
            NetFunctionInfo functionInfo = builder.Build();
            return s_hostContext.GetFunctionPointer(functionInfo);
        }

        [UnmanagedCallersOnly(EntryPoint = "Test", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static int Test(IntPtr pIntValue)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, int> pfn = (delegate* unmanaged[Cdecl]<IntPtr, int>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.HostingTest", "Test");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for Test");
                throw new InvalidOperationException("Function pointer for Test is null.");
            }
            return pfn(pIntValue);
        }

        [UnmanagedCallersOnly(EntryPoint = "Test2", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static int Test2(IntPtr pIntValue)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, int> pfn = (delegate* unmanaged[Cdecl]<IntPtr, int>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.HostingTest", "Test");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for Test2");
                throw new InvalidOperationException("Function pointer for Test2 is null.");
            }
            return pfn(pIntValue);
        }

        [UnmanagedCallersOnly(EntryPoint = "GiveFnptrsToDll", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static void GiveFnptrsToDll(IntPtr pengfuncsFromEngine, IntPtr pGlobals)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> pfn = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop", "GiveFnptrsToDll");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for GiveFnptrsToDll");
                throw new InvalidOperationException("Function pointer for GiveFnptrsToDll is null.");
            }
            pfn(pengfuncsFromEngine, pGlobals);

            // Initialize private data allocators after calling the framework function
            EntityExportsDemo.InitializePrivateDataAllocators();
        }

        [UnmanagedCallersOnly(EntryPoint = "GetEntityAPI", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static int GetEntityAPI(IntPtr pFunctionTable, int interfaceVersion)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, int, int> pfn = (delegate* unmanaged[Cdecl]<IntPtr, int, int>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop", "GetEntityAPI");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for GetEntityAPI");
                return 0;
            }
            return pfn(pFunctionTable, interfaceVersion);
        }

        [UnmanagedCallersOnly(EntryPoint = "GetEntityAPI2", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static int GetEntityAPI2(IntPtr pFunctionTable, IntPtr interfaceVersion)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> pfn = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop", "GetEntityAPI2");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for GetEntityAPI2");
                return 0;
            }
            return pfn(pFunctionTable, interfaceVersion);
        }

        [UnmanagedCallersOnly(EntryPoint = "GetNewDLLFunctions", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static int GetNewDLLFunctions(IntPtr pFunctionTable, IntPtr interfaceVersion)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> pfn = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop", "GetNewDLLFunctions");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for GetNewDLLFunctions");
                return 0;
            }
            return pfn(pFunctionTable, interfaceVersion);
        }

        /// <summary>
        /// Internal method to get private data allocator - used by EntityExportsDemo
        /// </summary>
        internal unsafe static IntPtr GetPrivateDataAllocator(IntPtr pszEntityClassName)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr> pfn = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop", "GetPrivateDataAllocator");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for GetPrivateDataAllocator");
                return IntPtr.Zero;
            }
            return pfn(pszEntityClassName);
        }

    }
}
