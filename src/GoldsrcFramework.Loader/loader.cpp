#include <string>
#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>
#include <Windows.h>
#include <memory>
#include <mutex>

using string_t = std::basic_string<char_t>;

// Forward declarations
namespace DotNetLoader {
    class RuntimeManager;
    enum class CallResult {
        Success,
        HostFxrLoadFailed,
        RuntimeInitFailed,
        FunctionLoadFailed,
        CallFailed
    };
}

// Global runtime manager instance
static std::unique_ptr<DotNetLoader::RuntimeManager> g_runtimeManager;
static std::once_flag g_initFlag;

// Forward declaration for entity private data allocation (defined in entity_exports.cpp)
extern "C" void InitializePrivateDataAllocators();

namespace DotNetLoader {
    /// <summary>
    /// Manages .NET runtime initialization and function calls
    /// Implements RAII pattern for proper resource management
    /// </summary>
    class RuntimeManager {
    private:
        // HostFxr function pointers
        hostfxr_initialize_for_runtime_config_fn m_initFptr = nullptr;
        hostfxr_get_runtime_delegate_fn m_getDelegateFptr = nullptr;
        hostfxr_close_fn m_closeFptr = nullptr;

        // Runtime state
        hostfxr_handle m_runtimeContext = nullptr;
        load_assembly_and_get_function_pointer_fn m_loadAssemblyFptr = nullptr;
        HMODULE m_hostFxrLib = nullptr;

        // Paths
        string_t m_rootPath;
        string_t m_configPath;
        string_t m_frameworkDllPath;

        bool m_initialized = false;
        mutable std::mutex m_mutex;

    public:
        RuntimeManager() {
            InitializePaths();
        }

        ~RuntimeManager() {
            Cleanup();
        }

        // Non-copyable, non-movable
        RuntimeManager(const RuntimeManager&) = delete;
        RuntimeManager& operator=(const RuntimeManager&) = delete;
        RuntimeManager(RuntimeManager&&) = delete;
        RuntimeManager& operator=(RuntimeManager&&) = delete;

        /// <summary>
        /// Initialize the .NET runtime (thread-safe)
        /// </summary>
        CallResult Initialize() {
            std::lock_guard<std::mutex> lock(m_mutex);

            if (m_initialized) {
                return CallResult::Success;
            }

            // Step 1: Load HostFxr
            if (!LoadHostFxr()) {
                return CallResult::HostFxrLoadFailed;
            }

            // Step 2: Initialize .NET runtime
            if (!InitializeRuntime()) {
                return CallResult::RuntimeInitFailed;
            }

            m_initialized = true;
            return CallResult::Success;
        }

        /// <summary>
        /// Call a managed method through FrameworkInterop
        /// </summary>
        template<typename TFunc>
        CallResult CallFrameworkMethod(const char_t* methodName, TFunc& functionPtr) {
            if (!EnsureInitialized()) {
                return CallResult::RuntimeInitFailed;
            }

            const char_t* dotnetType = L"GoldsrcFramework.FrameworkInterop, GoldsrcFramework";

            int rc = m_loadAssemblyFptr(
                m_frameworkDllPath.c_str(),
                dotnetType,
                methodName,
                UNMANAGEDCALLERSONLY_METHOD,
                nullptr,
                (void**)&functionPtr);

            return (rc == 0 && functionPtr != nullptr) ? CallResult::Success : CallResult::FunctionLoadFailed;
        }

        /// <summary>
        /// Call a test method (for testing purposes only)
        /// </summary>
        template<typename TFunc>
        CallResult CallTestMethod(const char_t* typeName, const char_t* methodName, TFunc& functionPtr) {
            if (!EnsureInitialized()) {
                return CallResult::RuntimeInitFailed;
            }

            int rc = m_loadAssemblyFptr(
                m_frameworkDllPath.c_str(),
                typeName,
                methodName,
                UNMANAGEDCALLERSONLY_METHOD,
                nullptr,
                (void**)&functionPtr);

            return (rc == 0 && functionPtr != nullptr) ? CallResult::Success : CallResult::FunctionLoadFailed;
        }

    private:
        void InitializePaths() {
            // Get module path
            HMODULE hm = GetModuleHandle(NULL);
            char_t buf[MAX_PATH];
            GetModuleFileName(hm, buf, MAX_PATH);

            m_rootPath = buf;
            auto pos = m_rootPath.find_last_of(L"\\");
            m_rootPath = m_rootPath.substr(0, pos + 1);

            m_configPath = m_rootPath + L"GoldsrcFramework.runtimeconfig.json";
            m_frameworkDllPath = m_rootPath + L"GoldsrcFramework.dll";
        }

        bool LoadHostFxr() {
            // Get hostfxr path
            char_t buffer[MAX_PATH];
            size_t buffer_size = sizeof(buffer) / sizeof(char_t);
            int rc = get_hostfxr_path(buffer, &buffer_size, nullptr);
            if (rc != 0) {
                return false;
            }

            // Load hostfxr library
            m_hostFxrLib = ::LoadLibraryW(buffer);
            if (!m_hostFxrLib) {
                return false;
            }

            // Get function pointers
            m_initFptr = (hostfxr_initialize_for_runtime_config_fn)
                ::GetProcAddress(m_hostFxrLib, "hostfxr_initialize_for_runtime_config");
            m_getDelegateFptr = (hostfxr_get_runtime_delegate_fn)
                ::GetProcAddress(m_hostFxrLib, "hostfxr_get_runtime_delegate");
            m_closeFptr = (hostfxr_close_fn)
                ::GetProcAddress(m_hostFxrLib, "hostfxr_close");

            return (m_initFptr && m_getDelegateFptr && m_closeFptr);
        }

        bool InitializeRuntime() {
            // Initialize .NET runtime
            int rc = m_initFptr(m_configPath.c_str(), nullptr, &m_runtimeContext);
            if (rc != 0 || m_runtimeContext == nullptr) {
                return false;
            }

            // Get load assembly function pointer
            void* loadAssemblyPtr = nullptr;
            rc = m_getDelegateFptr(
                m_runtimeContext,
                hdt_load_assembly_and_get_function_pointer,
                &loadAssemblyPtr);

            if (rc != 0 || loadAssemblyPtr == nullptr) {
                return false;
            }

            m_loadAssemblyFptr = (load_assembly_and_get_function_pointer_fn)loadAssemblyPtr;
            return true;
        }

        bool EnsureInitialized() {
            if (m_initialized) {
                return true;
            }
            return Initialize() == CallResult::Success;
        }

        void Cleanup() {
            if (m_runtimeContext) {
                if (m_closeFptr) {
                    m_closeFptr(m_runtimeContext);
                }
                m_runtimeContext = nullptr;
            }

            if (m_hostFxrLib) {
                ::FreeLibrary(m_hostFxrLib);
                m_hostFxrLib = nullptr;
            }

            m_initFptr = nullptr;
            m_getDelegateFptr = nullptr;
            m_closeFptr = nullptr;
            m_loadAssemblyFptr = nullptr;
            m_initialized = false;
        }
    };

    /// <summary>
    /// Get or create the global runtime manager instance (thread-safe)
    /// </summary>
    RuntimeManager& GetRuntimeManager() {
        std::call_once(g_initFlag, []() {
            g_runtimeManager = std::make_unique<RuntimeManager>();
        });
        return *g_runtimeManager;
    }

    /// <summary>
    /// Convert CallResult to human-readable string for debugging
    /// </summary>
    const char* CallResultToString(CallResult result) {
        switch (result) {
            case CallResult::Success: return "Success";
            case CallResult::HostFxrLoadFailed: return "HostFxr load failed";
            case CallResult::RuntimeInitFailed: return "Runtime initialization failed";
            case CallResult::FunctionLoadFailed: return "Function load failed";
            case CallResult::CallFailed: return "Function call failed";
            default: return "Unknown error";
        }
    }
}

// =============================================================================
// TEST INTERFACES (for testing purposes only)
// =============================================================================

typedef void (__cdecl *fn_F)(void* pv);
typedef int (__cdecl *fn_Test)(void* pIntValue);

extern "C" void __declspec(dllexport) F(void* pv)
{
    auto& runtime = DotNetLoader::GetRuntimeManager();

    fn_F pfn_F = nullptr;
    auto result = runtime.CallTestMethod(
        L"GoldsrcFramework.F, GoldsrcFramework",
        L"F",
        pfn_F);

    if (result == DotNetLoader::CallResult::Success && pfn_F != nullptr) {
        pfn_F(pv);
    }
    // Note: In production, you might want to log errors or handle them appropriately
}

extern "C" int __declspec(dllexport) Test(void* pIntValue)
{
    auto& runtime = DotNetLoader::GetRuntimeManager();

    fn_Test pfn_Test = nullptr;
    auto result = runtime.CallTestMethod(
        L"GoldsrcFramework.HostingTest, GoldsrcFramework",
        L"Test",
        pfn_Test);

    if (result == DotNetLoader::CallResult::Success && pfn_Test != nullptr) {
        return pfn_Test(pIntValue);
    }

    return 0; // Return 0 on failure
}

// =============================================================================
// PRODUCTION INTERFACES (through FrameworkInterop)
// =============================================================================

// Function pointer types for FrameworkInterop methods
typedef void (__cdecl *fn_GiveFnptrsToDll)(void* pengfuncsFromEngine, void* pGlobals);
typedef int (__cdecl *fn_GetEntityAPI)(void* pFunctionTable, int interfaceVersion);
typedef int (__cdecl *fn_GetEntityAPI2)(void* pFunctionTable, int* interfaceVersion);
typedef int (__cdecl *fn_GetNewDLLFunctions)(void* pFunctionTable, int* interfaceVersion);
typedef void* (__cdecl *fn_GetPrivateDataAllocator)(void* pszEntityClassName);

/// <summary>
/// Template helper to return appropriate default values for different types (C++14 compatible)
/// </summary>
template<typename T>
typename std::enable_if<std::is_void<T>::value, T>::type DefaultReturnValue() {
    return;
}

template<typename T>
typename std::enable_if<std::is_integral<T>::value && !std::is_void<T>::value, T>::type DefaultReturnValue() {
    return 0;
}

template<typename T>
typename std::enable_if<!std::is_integral<T>::value && !std::is_void<T>::value, T>::type DefaultReturnValue() {
    return nullptr;
}

/// <summary>
/// Template helper for calling FrameworkInterop methods with error handling
/// </summary>
template<typename TFunc, typename... Args>
auto CallFrameworkInteropMethod(const char_t* methodName, Args&&... args) -> decltype(std::declval<TFunc>()(args...)) {
    auto& runtime = DotNetLoader::GetRuntimeManager();

    TFunc functionPtr = nullptr;
    auto result = runtime.CallFrameworkMethod(methodName, functionPtr);

    if (result == DotNetLoader::CallResult::Success && functionPtr != nullptr) {
        return functionPtr(std::forward<Args>(args)...);
    }

    // Return appropriate default value based on return type
    using ReturnType = decltype(functionPtr(args...));
    return DefaultReturnValue<ReturnType>();
}


// Server-side exports
extern "C" {
    // Standard Half-Life server exports
    __declspec(dllexport) void GiveFnptrsToDll(void* pengfuncsFromEngine, void* pGlobals);
    __declspec(dllexport) int GetEntityAPI(void* pFunctionTable, int interfaceVersion);
    __declspec(dllexport) int GetEntityAPI2(void* pFunctionTable, int* interfaceVersion);
    __declspec(dllexport) int GetNewDLLFunctions(void* pFunctionTable, int* interfaceVersion);
}

void* GetPrivateDataAllocator(const char* const pszEntityClassName)
{
    return CallFrameworkInteropMethod<fn_GetPrivateDataAllocator>(
        L"GetPrivateDataAllocator",
        (void*)pszEntityClassName);
}

void GiveFnptrsToDll(void* pengfuncsFromEngine, void* pGlobals)
{
    CallFrameworkInteropMethod<fn_GiveFnptrsToDll>(
        L"GiveFnptrsToDll",
        pengfuncsFromEngine,
        pGlobals);

    // Initialize private data allocators after framework initialization
    InitializePrivateDataAllocators();
}

int GetEntityAPI(void* pFunctionTable, int interfaceVersion)
{
    return CallFrameworkInteropMethod<fn_GetEntityAPI>(
        L"GetEntityAPI",
        pFunctionTable,
        interfaceVersion);
}

int GetEntityAPI2(void* pFunctionTable, int* interfaceVersion)
{
    return CallFrameworkInteropMethod<fn_GetEntityAPI2>(
        L"GetEntityAPI2",
        pFunctionTable,
        interfaceVersion);
}

int GetNewDLLFunctions(void* pFunctionTable, int* interfaceVersion)
{
    return CallFrameworkInteropMethod<fn_GetNewDLLFunctions>(
        L"GetNewDLLFunctions",
        pFunctionTable,
        interfaceVersion);
}







// =============================================================================
// END OF REFACTORED LOADER
// =============================================================================
//
// Summary of improvements:
// 1. Eliminated code duplication - all .NET runtime initialization is now
//    centralized in the RuntimeManager class
// 2. Added proper error handling with CallResult enum
// 3. Implemented RAII pattern for resource management
// 4. Made the code thread-safe with std::once_flag and std::mutex
// 5. Separated test interfaces from production interfaces
// 6. All production calls now go through FrameworkInterop for consistency
// 7. Added comprehensive documentation and comments
// 8. Improved maintainability and readability
//
// The new architecture:
// - RuntimeManager: Handles all .NET runtime lifecycle management
// - Test interfaces: F() and Test() for testing purposes only
// - Production interfaces: All server exports go through FrameworkInterop
// - Template helpers: Reduce boilerplate code for method calls
// =============================================================================










