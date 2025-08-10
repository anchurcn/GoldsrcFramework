using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mxrx.NetHost;
using Rxmxnx.PInvoke;

namespace GoldsrcFramework.NetLoader
{
    /// <summary>
    /// Native AOT loader implementation using Mxrx.NetHost.Fxr
    /// This replaces the C++ loader.cpp functionality
    /// </summary>
    public static class Loader
    {
        private static bool s_isInitialized = false;
        private static FrameworkResolver? s_frameworkResolver = null;
        private static HostContext? s_hostContext = null;

        /// <summary>
        /// Initialize the .NET host using Mxrx.NetHost.Fxr
        /// </summary>
        private static bool InitializeNetHost()
        {
            if (s_isInitialized && s_hostContext != null)
                return true;

            try
            {
                // Get the current module directory
                string moduleDir = GetModuleDirectory();
                string frameworkPath = Path.Combine(moduleDir, "GoldsrcFramework.dll");
                string runtimeConfigPath = Path.Combine(moduleDir, "GoldsrcFramework.runtimeconfig.json");

                if (!File.Exists(frameworkPath) || !File.Exists(runtimeConfigPath))
                    return false;

                // Find hostfxr library
                string hostfxrPath = FindHostfxrLibrary();
                if (string.IsNullOrEmpty(hostfxrPath))
                    return false;

                // Load the framework resolver
                s_frameworkResolver = FrameworkResolver.LoadResolver(hostfxrPath);

                // Create initialization parameters
                InitializationParameters initParams = InitializationParameters.CreateBuilder()
                    .WithRuntimeConfigPath(runtimeConfigPath)
                    .Build();

                // Initialize the host context
                s_hostContext = s_frameworkResolver.Initialize(initParams);

                s_isInitialized = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Find the hostfxr library path
        /// </summary>
        private static string FindHostfxrLibrary()
        {
            try
            {
                // Try common locations for hostfxr
                string[] possiblePaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "host", "fxr"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "host", "fxr"),
                    @"C:\Program Files\dotnet\host\fxr",
                    @"C:\Program Files (x86)\dotnet\host\fxr"
                };

                foreach (string basePath in possiblePaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        // Find the latest version directory
                        var versionDirs = Directory.GetDirectories(basePath)
                            .Select(d => new DirectoryInfo(d))
                            .OrderByDescending(d => d.Name)
                            .ToArray();

                        foreach (var versionDir in versionDirs)
                        {
                            string hostfxrPath = Path.Combine(versionDir.FullName, "hostfxr.dll");
                            if (File.Exists(hostfxrPath))
                                return hostfxrPath;
                        }
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the directory containing the current module
        /// </summary>
        private static string GetModuleDirectory()
        {
            // Get the current executable path
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                // Fallback to current directory
                return Directory.GetCurrentDirectory();
            }

            return Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Main entry point - equivalent to F() function in loader.cpp
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "F", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static void F(IntPtr pv)
        {
            try
            {
                // Initialize .NET host if not already done
                if (!InitializeNetHost())
                    return;

                // Get function pointer for FrameworkInterop.F
                NetFunctionInfo fFunctionInfo = CreateFunctionInfo("GoldsrcFramework.FrameworkInterop", "F");
                IntPtr fPtr = s_hostContext!.GetFunctionPointer(fFunctionInfo);

                if (fPtr != IntPtr.Zero)
                {
                    // Call the managed F method via function pointer
                    var fDelegate = fPtr.GetUnsafeDelegate<Action<IntPtr>>();
                    fDelegate?.Invoke(pv);
                }
            }
            catch
            {
                // Silently handle errors - similar to C++ version
            }
        }

        /// <summary>
        /// Create NetFunctionInfo for calling managed methods
        /// </summary>
        private static NetFunctionInfo CreateFunctionInfo(string typeName, string methodName)
        {
            string moduleDir = GetModuleDirectory();
            string frameworkPath = Path.Combine(moduleDir, "GoldsrcFramework.dll");

            NetFunctionInfo.Builder builder = NetFunctionInfo.CreateBuilder()
                .WithAssemblyPathPath(frameworkPath);

            if (OperatingSystem.IsWindows())
            {
                builder = builder.WithTypeName($"{typeName}, GoldsrcFramework\0")
                               .WithMethodName($"{methodName}\0");
            }
            else
            {
                string typeNameUtf8 = $"{typeName}, GoldsrcFramework";
                string methodNameUtf8 = methodName;
                builder = builder.WithTypeName(System.Text.Encoding.UTF8.GetBytes(typeNameUtf8))
                               .WithMethodName(System.Text.Encoding.UTF8.GetBytes(methodNameUtf8));
            }

            return builder.Build();
        }

        /// <summary>
        /// Test function - equivalent to Test() function in loader.cpp
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "Test", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static int Test(IntPtr pIntValue)
        {
            try
            {
                // Initialize .NET host if not already done
                if (!InitializeNetHost())
                    return -1;

                // Get function pointer for FrameworkInterop.Test
                NetFunctionInfo testFunctionInfo = CreateFunctionInfo("GoldsrcFramework.FrameworkInterop", "Test");
                IntPtr testPtr = s_hostContext!.GetFunctionPointer(testFunctionInfo);

                if (testPtr != IntPtr.Zero)
                {
                    // Call the managed Test method via function pointer
                    var testDelegate = testPtr.GetUnsafeDelegate<Func<IntPtr, int>>();
                    return testDelegate?.Invoke(pIntValue) ?? -1;
                }

                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// GetPrivateDataAllocator function - equivalent to the one in loader.cpp
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "GetPrivateDataAllocator", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static IntPtr GetPrivateDataAllocator(IntPtr pszEntityClassName)
        {
            try
            {
                // Initialize .NET host if not already done
                if (!InitializeNetHost())
                    return IntPtr.Zero;

                // Get function pointer for FrameworkInterop.GetPrivateDataAllocator
                NetFunctionInfo allocatorFunctionInfo = CreateFunctionInfo("GoldsrcFramework.FrameworkInterop", "GetPrivateDataAllocator");
                IntPtr allocatorPtr = s_hostContext!.GetFunctionPointer(allocatorFunctionInfo);

                if (allocatorPtr != IntPtr.Zero)
                {
                    // Call the managed GetPrivateDataAllocator method via function pointer
                    var allocatorDelegate = allocatorPtr.GetUnsafeDelegate<Func<IntPtr, IntPtr>>();
                    return allocatorDelegate?.Invoke(pszEntityClassName) ?? IntPtr.Zero;
                }

                return IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Cleanup resources when the loader is unloaded
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "DllMain", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        public static bool DllMain(IntPtr hModule, uint dwReason, IntPtr lpReserved)
        {
            const uint DLL_PROCESS_DETACH = 0;

            if (dwReason == DLL_PROCESS_DETACH)
            {
                // Cleanup when DLL is unloaded
                try
                {
                    s_hostContext?.Dispose();
                    s_frameworkResolver?.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
                finally
                {
                    s_hostContext = null;
                    s_frameworkResolver = null;
                    s_isInitialized = false;
                }
            }

            return true;
        }
    }
}
