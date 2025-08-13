using GoldsrcFramework.NetLoader.Native;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

[assembly: DisableRuntimeMarshalling]

namespace GoldsrcFramework.NetLoader.Native
{
    public struct char_t { }

    public enum hostfxr_delegate_type
    {
        hdt_com_activation,
        hdt_load_in_memory_assembly,
        hdt_winrt_activation,
        hdt_com_register,
        hdt_com_unregister,
        hdt_load_assembly_and_get_function_pointer,
        hdt_get_function_pointer,
        hdt_load_assembly,
        hdt_load_assembly_bytes,
    }

    public struct hostfxr_initialize_parameters
    {
        public nuint size;
        public unsafe char_t* host_path;
        public unsafe char_t* dotnet_root;
    }

    public struct hostfxr_dotnet_environment_sdk_info
    {
        public nuint size;
        public unsafe char_t* version;
        public unsafe char_t* path;
    }

    public struct hostfxr_dotnet_environment_framework_info
    {
        public nuint size;
        public unsafe char_t* name;
        public unsafe char_t* version;
        public unsafe char_t* path;
    }

    public struct hostfxr_dotnet_environment_info
    {
        public nuint size;
        public unsafe char_t* hostfxr_version;
        public unsafe char_t* hostfxr_commit_hash;
        public nuint sdk_count;
        public unsafe hostfxr_dotnet_environment_sdk_info* sdks;
        public nuint framework_count;
        public unsafe hostfxr_dotnet_environment_framework_info* frameworks;
    }

    public struct hostfxr_framework_result
    {
        public nuint size;
        public unsafe char_t* name;
        public unsafe char_t* requested_version;
        public unsafe char_t* resolved_version;
        public unsafe char_t* resolved_path;
    }

    public struct hostfxr_resolve_frameworks_result
    {
        public nuint size;
        public nuint resolved_count;
        public unsafe hostfxr_framework_result* resolved_frameworks;
        public nuint unresolved_count;
        public unsafe hostfxr_framework_result* unresolved_frameworks;
    }
    public class hostfxr
    {
        // Function pointer types
        public unsafe delegate* unmanaged[Cdecl]<int, char_t**, int> hostfxr_main_fn;
        public unsafe delegate* unmanaged[Cdecl]<int, char_t**, char_t*, char_t*, char_t*, int> hostfxr_main_startupinfo_fn;
        public unsafe delegate* unmanaged[Cdecl]<int, char_t**, char_t*, char_t*, char_t*, long, int> hostfxr_main_bundle_startupinfo_fn;
        public unsafe delegate* unmanaged[Cdecl]<char_t*, void> hostfxr_error_writer_fn;
        //public unsafe delegate* unmanaged[Cdecl]<hostfxr_error_writer_fn, hostfxr_error_writer_fn> hostfxr_set_error_writer_fn;
        public unsafe delegate* unmanaged[Cdecl]<int, char_t**, hostfxr_initialize_parameters*, void**, int> hostfxr_initialize_for_dotnet_command_line_fn;
        public unsafe delegate* unmanaged[Cdecl]<char_t*, hostfxr_initialize_parameters*, void**, int> hostfxr_initialize_for_runtime_config_fn;
        public unsafe delegate* unmanaged[Cdecl]<void*, char_t*, char_t**, int> hostfxr_get_runtime_property_value_fn;
        public unsafe delegate* unmanaged[Cdecl]<void*, char_t*, char_t*, int> hostfxr_set_runtime_property_value_fn;
        public unsafe delegate* unmanaged[Cdecl]<void*, nuint*, char_t**, char_t**, int> hostfxr_get_runtime_properties_fn;
        public unsafe delegate* unmanaged[Cdecl]<void*, int> hostfxr_run_app_fn;
        public unsafe delegate* unmanaged[Cdecl]<void*, hostfxr_delegate_type, void**, int> hostfxr_get_runtime_delegate_fn;
        public unsafe delegate* unmanaged[Cdecl]<void*, int> hostfxr_close_fn;

        // New delegates from .NET 5+
        public unsafe delegate* unmanaged[Cdecl]<hostfxr_dotnet_environment_info*, void*, void> hostfxr_get_dotnet_environment_info_result_fn;
        //public unsafe delegate* unmanaged[Cdecl]<char_t*, void*, hostfxr_get_dotnet_environment_info_result_fn, void*, int> hostfxr_get_dotnet_environment_info_fn;
        public unsafe delegate* unmanaged[Cdecl]<hostfxr_resolve_frameworks_result*, void*, void> hostfxr_resolve_frameworks_result_fn;
        //public unsafe delegate* unmanaged[Cdecl]<char_t*, hostfxr_initialize_parameters*, hostfxr_resolve_frameworks_result_fn, void*, int> hostfxr_resolve_frameworks_for_runtime_config_fn;
    }
}

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
        //static unsafe void Main()
        //{
        //    int a = 2;
        //    IntPtr p = (IntPtr)(&a);
        //    var b= Test(p);
        //}
        private static HostFrameworkResolver s_hostFxr = null!;
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
                moduleDir = Path.Combine(moduleDir, "gsfdemo","cl_dlls");
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

                s_hostFxr = HostFrameworkResolver.Create(hostfxrPath);
                s_hostContext = s_hostFxr.InitializeForRuntimeConfig(runtimeConfigPath);
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
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop, GoldsrcFramework", "F");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for F");
                throw new InvalidOperationException("Function pointer for F is null.");
            }
            pfn(pv);
        }

        private static IntPtr GetGoldsrcFrameworkFunctionPointer(string typeName, string methodName)
        {
            if (s_hostContext is null)
            {
                throw new InvalidOperationException("Host context is not initialized.");
            }
            IntPtr pfn = s_hostContext.LoadAssemblyAndGetUnmanagedCallerOnlyFunctionPointer(s_frameworkPath, typeName, methodName);
            if (pfn == IntPtr.Zero)
            {
                Debug.WriteLine($"Function pointer for {typeName}.{methodName} is null.");
            }
            return pfn;
        }

        [UnmanagedCallersOnly(EntryPoint = "Test", CallConvs = new[] { typeof(CallConvCdecl) })]
        public unsafe static int Test(IntPtr pIntValue)
        {
            EnsureInitialized();
            delegate* unmanaged[Cdecl]<IntPtr, int> pfn = (delegate* unmanaged[Cdecl]<IntPtr, int>)
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.HostingTest, GoldsrcFramework", "Test");
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
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop, GoldsrcFramework", "GiveFnptrsToDll");
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
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop, GoldsrcFramework", "GetEntityAPI");
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
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop, GoldsrcFramework", "GetEntityAPI2");
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
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop, GoldsrcFramework", "GetNewDLLFunctions");
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
                GetGoldsrcFrameworkFunctionPointer("GoldsrcFramework.FrameworkInterop, GoldsrcFramework", "GetPrivateDataAllocator");
            if (pfn == null)
            {
                Debug.WriteLine("Unable to get function pointer for GetPrivateDataAllocator");
                return IntPtr.Zero;
            }
            return pfn(pszEntityClassName);
        }

    }
}

namespace GoldsrcFramework.NetLoader.Native
{
    // TODO, UTF8 on non-Windows.

    public unsafe class HostFrameworkResolver
    {
        private readonly delegate* unmanaged[Cdecl]<int, char_t**, hostfxr_initialize_parameters*, void**, int> _initializeForDotnetCommandLine;
        private readonly delegate* unmanaged[Cdecl]<char_t*, hostfxr_initialize_parameters*, void**, int> _initializeForRuntimeConfig;
        private readonly delegate* unmanaged[Cdecl]<void*, char_t*, char_t**, int> _getRuntimePropertyValue;
        private readonly delegate* unmanaged[Cdecl]<void*, char_t*, char_t*, int> _setRuntimePropertyValue;
        private readonly delegate* unmanaged[Cdecl]<void*, hostfxr_delegate_type, void**, int> _getRuntimeDelegate;
        private readonly delegate* unmanaged[Cdecl]<void*, int> _closeHost;

        private HostFrameworkResolver(
            delegate* unmanaged[Cdecl]<int, char_t**, hostfxr_initialize_parameters*, void**, int> initializeForDotnetCommandLine,
            delegate* unmanaged[Cdecl]<char_t*, hostfxr_initialize_parameters*, void**, int> initializeForRuntimeConfig,
            delegate* unmanaged[Cdecl]<void*, char_t*, char_t**, int> getRuntimePropertyValue,
            delegate* unmanaged[Cdecl]<void*, char_t*, char_t*, int> setRuntimePropertyValue,
            delegate* unmanaged[Cdecl]<void*, hostfxr_delegate_type, void**, int> getRuntimeDelegate,
            delegate* unmanaged[Cdecl]<void*, int> closeHost)
        {
            _initializeForDotnetCommandLine = initializeForDotnetCommandLine;
            _initializeForRuntimeConfig = initializeForRuntimeConfig;
            _getRuntimePropertyValue = getRuntimePropertyValue;
            _setRuntimePropertyValue = setRuntimePropertyValue;
            _getRuntimeDelegate = getRuntimeDelegate;
            _closeHost = closeHost;
        }

        public static HostFrameworkResolver Create(string hostfxrPath)
        {
            IntPtr lib = NativeLibrary.Load(hostfxrPath);
            try
            {
                var initializeForDotnetCommandLine = (delegate* unmanaged[Cdecl]<int, char_t**, hostfxr_initialize_parameters*, void**, int>)NativeLibrary.GetExport(lib, "hostfxr_initialize_for_dotnet_command_line");
                var initializeForRuntimeConfig = (delegate* unmanaged[Cdecl]<char_t*, hostfxr_initialize_parameters*, void**, int>)NativeLibrary.GetExport(lib, "hostfxr_initialize_for_runtime_config");
                var getRuntimePropertyValue = (delegate* unmanaged[Cdecl]<void*, char_t*, char_t**, int>)NativeLibrary.GetExport(lib, "hostfxr_get_runtime_property_value");
                var setRuntimePropertyValue = (delegate* unmanaged[Cdecl]<void*, char_t*, char_t*, int>)NativeLibrary.GetExport(lib, "hostfxr_set_runtime_property_value");
                var getRuntimeDelegate = (delegate* unmanaged[Cdecl]<void*, hostfxr_delegate_type, void**, int>)NativeLibrary.GetExport(lib, "hostfxr_get_runtime_delegate");
                var closeHost = (delegate* unmanaged[Cdecl]<void*, int>)NativeLibrary.GetExport(lib, "hostfxr_close");
                return new HostFrameworkResolver(
                    initializeForDotnetCommandLine,
                    initializeForRuntimeConfig,
                    getRuntimePropertyValue,
                    setRuntimePropertyValue,
                    getRuntimeDelegate,
                    closeHost);
            }
            catch
            {
                NativeLibrary.Free(lib);
                throw;
            }
        }

        //public int InitializeForDotnetCommandLine(string[] args, string? hostPath = null, string? dotnetRoot = null)
        //{
        //    fixed (char* argsPtr = string.Join('\0', args))
        //    {
        //        char_t** argv = (char_t**)Marshal.AllocHGlobal(args.Length * IntPtr.Size);
        //        try
        //        {
        //            for (int i = 0; i < args.Length; i++)
        //            {
        //                argv[i] = (char_t*)Marshal.StringToHGlobalUni(args[i]);
        //            }

        //            var parameters = new hostfxr_initialize_parameters
        //            {
        //                size = (nuint)Marshal.SizeOf<hostfxr_initialize_parameters>(),
        //                host_path = hostPath != null ? (char_t*)Marshal.StringToHGlobalUni(hostPath) : null,
        //                dotnet_root = dotnetRoot != null ? (char_t*)Marshal.StringToHGlobalUni(dotnetRoot) : null
        //            };

        //            void* host_context_handle;
        //            return _initializeForDotnetCommandLine(args.Length, argv, &parameters, &host_context_handle);
        //        }
        //        finally
        //        {
        //            for (int i = 0; i < args.Length; i++)
        //            {
        //                Marshal.FreeHGlobal((IntPtr)argv[i]);
        //            }
        //            Marshal.FreeHGlobal((IntPtr)argv);
        //            if (hostPath != null)
        //                Marshal.FreeHGlobal((IntPtr)parameters.host_path);
        //            if (dotnetRoot != null)
        //                Marshal.FreeHGlobal((IntPtr)parameters.dotnet_root);
        //        }
        //    }
        //}

        public HostContext InitializeForRuntimeConfig(string runtimeConfigPath, string? hostPath = null, string? dotnetRoot = null)
        {
            var parameters = new hostfxr_initialize_parameters
            {
                size = (nuint)sizeof(hostfxr_initialize_parameters),
                host_path = hostPath != null ? (char_t*)Marshal.StringToHGlobalUni(hostPath) : null,
                dotnet_root = dotnetRoot != null ? (char_t*)Marshal.StringToHGlobalUni(dotnetRoot) : null
            };

            CoreClrDelegates* pDelegates = (CoreClrDelegates*)Marshal.AllocHGlobal(sizeof(CoreClrDelegates));
            try
            {
                void* host_context_handle;
                fixed (char* configPath = runtimeConfigPath)
                {
                    var rc = _initializeForRuntimeConfig((char_t*)configPath, &parameters, &host_context_handle);
                    if (rc != 0)
                        throw new InvalidOperationException($"Failed to initialize host for runtime config: {rc}");
                    if (host_context_handle == null)
                        throw new InvalidOperationException("Host context handle is null after initialization.");

                    void* pfn = null;
                    rc = _getRuntimeDelegate(host_context_handle,
                        hostfxr_delegate_type.hdt_load_assembly_and_get_function_pointer,
                        &pfn);
                    pDelegates->load_assembly_and_get_function_pointer_fn =
                        (delegate* unmanaged[Stdcall]<char_t*, char_t*, char_t*, char_t*, void*, void**, int>)pfn;
                    if (rc != 0 || pDelegates->load_assembly_and_get_function_pointer_fn == null)
                    {
                        _closeHost(host_context_handle);
                        throw new InvalidOperationException($"Failed to get load_assembly_and_get_function_pointer delegate: {rc}");
                    }
                    else
                    {
                        return new HostContext(*pDelegates);
                    }
                }
            }
            finally
            {
                if (hostPath != null)
                    Marshal.FreeHGlobal((IntPtr)parameters.host_path);
                if (dotnetRoot != null)
                    Marshal.FreeHGlobal((IntPtr)parameters.dotnet_root);
                Marshal.FreeHGlobal((IntPtr)pDelegates);
            }
        }

        public string? GetRuntimePropertyValue(void* hostContextHandle, string name)
        {
            char_t* value;
            fixed (char* namePtr = name)
            {
                int result = _getRuntimePropertyValue(hostContextHandle, (char_t*)namePtr, &value);
                if (result != 0)
                    return null;

                return Marshal.PtrToStringUni((IntPtr)value);
            }
        }

        public int SetRuntimePropertyValue(void* hostContextHandle, string name, string value)
        {
            fixed (char* namePtr = name)
            fixed (char* valuePtr = value)
            {
                return _setRuntimePropertyValue(hostContextHandle, (char_t*)namePtr, (char_t*)valuePtr);
            }
        }

        public int GetRuntimeDelegate(void* hostContextHandle, hostfxr_delegate_type type, out IntPtr @delegate)
        {
            void* delegatePtr;
            int result = _getRuntimeDelegate(hostContextHandle, type, &delegatePtr);
            @delegate = (IntPtr)delegatePtr;
            return result;
        }

        public int CloseHost(void* hostContextHandle)
        {
            return _closeHost(hostContextHandle);
        }
    }

    public unsafe class HostContext
    {
        private readonly CoreClrDelegates coreClrDelegates;

        public HostContext(CoreClrDelegates coreClrDelegates)
        {
            this.coreClrDelegates = coreClrDelegates;
        }

        public IntPtr LoadAssemblyAndGetUnmanagedCallerOnlyFunctionPointer(string assemblyPath, string typeName, string methodName)
        {
            fixed (char* assemblyPathPtr = assemblyPath)
            fixed (char* typeNamePtr = typeName)
            fixed (char* methodNamePtr = methodName)
            {
                void* functionPtr;
                int rc = coreClrDelegates.load_assembly_and_get_function_pointer_fn(
                    (char_t*)assemblyPathPtr,
                    (char_t*)typeNamePtr,
                    (char_t*)methodNamePtr,
                    (char_t*)-1,
                    null,
                    &functionPtr);
                if (rc != 0)
                    throw new InvalidOperationException($"Failed to load assembly and get function pointer: {rc}");
                return (IntPtr)functionPtr;
            }
        }
    }
}
namespace GoldsrcFramework.NetLoader.Native
{
    public unsafe struct CoreClrDelegates
    {
        public static readonly IntPtr UNMANAGEDCALLERSONLY_METHOD = (IntPtr)(-1);

        // Native function pointer types
        public delegate* unmanaged[Stdcall]<char_t*, char_t*, char_t*, char_t*, void*, void**, int> load_assembly_and_get_function_pointer_fn;
        public delegate* unmanaged[Stdcall]<void*, int, int> component_entry_point_fn;
        public delegate* unmanaged[Stdcall]<char_t*, char_t*, char_t*, void*, void*, void**, int> get_function_pointer_fn;
        public delegate* unmanaged[Stdcall]<char_t*, void*, void*, int> load_assembly_fn;
        public delegate* unmanaged[Stdcall]<void*, nuint, void*, nuint, void*, void*, int> load_assembly_bytes_fn;
    }
}

