#include<string>
#include<nethost.h>
#include<coreclr_delegates.h>
#include<hostfxr.h>
#include<Windows.h>

using string_t = std::basic_string<char_t>;
typedef void (__cdecl *fn_F)(void* pv);
typedef int (__cdecl *fn_Test)(void* pIntValue);

// Globals to hold hostfxr exports
hostfxr_initialize_for_runtime_config_fn init_fptr;
hostfxr_get_runtime_delegate_fn get_delegate_fptr;
hostfxr_close_fn close_fptr;

// Forward declarations
bool load_hostfxr();
load_assembly_and_get_function_pointer_fn get_dotnet_load_assembly(const char_t* assembly);

extern "C" void __declspec(dllexport) F(void* pv)
{
	HMODULE hm = GetModuleHandle(NULL);
	char_t buf[MAX_PATH];
	GetModuleFileName(hm, buf, MAX_PATH);

	string_t root_path = buf;
	auto pos = root_path.find_last_of(L"\\");
	root_path = root_path.substr(0, pos + 1);

	//
	// STEP 1: Load HostFxr and get exported hosting functions
	//
	if (!load_hostfxr())
	{
		return;
	}

	//
	// STEP 2: Initialize and start the .NET Core runtime
	//
	const string_t config_path = root_path + L"GoldsrcFramework.runtimeconfig.json";
	load_assembly_and_get_function_pointer_fn load_assembly_and_get_function_pointer = nullptr;
	load_assembly_and_get_function_pointer = get_dotnet_load_assembly(config_path.c_str());

	//
	// STEP 3: Load managed assembly and get function pointer to a managed method
	//
	const string_t dotnetlib_path = root_path + L"GoldsrcFramework.dll";
	const char_t* dotnet_type = L"GoldsrcFramework.F, GoldsrcFramework";
	const char_t* dotnet_type_method = L"F";


	// Function pointer to managed delegate with non-default signature
	fn_F pfn_F = nullptr;
	int rc = load_assembly_and_get_function_pointer(
		dotnetlib_path.c_str(),
		dotnet_type,
		dotnet_type_method /*method_name*/,
		UNMANAGEDCALLERSONLY_METHOD,
		nullptr,
		(void**)&pfn_F);

	pfn_F(pv);
}


/********************************************************************************************
 * Function used to load and activate .NET Core
 ********************************************************************************************/


 // Forward declarations
void* load_library(const char_t*);
void* get_export(void*, const char*);


void* load_library(const char_t* path)
{
	HMODULE h = ::LoadLibraryW(path);
	return (void*)h;
}
void* get_export(void* h, const char* name)
{
	void* f = ::GetProcAddress((HMODULE)h, name);
	return f;
}



// Using the nethost library, discover the location of hostfxr and get exports
bool load_hostfxr()
{
	// Pre-allocate a large buffer for the path to hostfxr
	char_t buffer[MAX_PATH];
	size_t buffer_size = sizeof(buffer) / sizeof(char_t);
	int rc = get_hostfxr_path(buffer, &buffer_size, nullptr);
	if (rc != 0)
		return false;

	// Load hostfxr and get desired exports
	void* lib = load_library(buffer);
	init_fptr = (hostfxr_initialize_for_runtime_config_fn)get_export(lib, "hostfxr_initialize_for_runtime_config");
	get_delegate_fptr = (hostfxr_get_runtime_delegate_fn)get_export(lib, "hostfxr_get_runtime_delegate");
	close_fptr = (hostfxr_close_fn)get_export(lib, "hostfxr_close");

	return (init_fptr && get_delegate_fptr && close_fptr);
}

// Load and initialize .NET Core and get desired function pointer for scenario
load_assembly_and_get_function_pointer_fn get_dotnet_load_assembly(const char_t* config_path)
{
	// Load .NET Core
	void* load_assembly_and_get_function_pointer = nullptr;
	hostfxr_handle cxt = nullptr;
	int rc = init_fptr(config_path, nullptr, &cxt);
	if (rc != 0 || cxt == nullptr)
	{
		close_fptr(cxt);
		return nullptr;
	}

	// Get the load assembly function pointer
	rc = get_delegate_fptr(
		cxt,
		hdt_load_assembly_and_get_function_pointer,
		&load_assembly_and_get_function_pointer);
	if (rc != 0 || load_assembly_and_get_function_pointer == nullptr)

		close_fptr(cxt);
	return (load_assembly_and_get_function_pointer_fn)load_assembly_and_get_function_pointer;
}





extern "C" int __declspec(dllexport) Test(void* pIntValue)
{
	HMODULE hm = GetModuleHandle(NULL);
	char_t buf[MAX_PATH];
	GetModuleFileName(hm, buf, MAX_PATH);
	
	string_t root_path = buf;
	auto pos = root_path.find_last_of(L"\\");
	root_path = root_path.substr(0, pos + 1);

	//
	// STEP 1: Load HostFxr and get exported hosting functions
	//
	if (!load_hostfxr())
	{
		return 0;
	}

	//
	// STEP 2: Initialize and start the .NET Core runtime
	//
	const string_t config_path = root_path + L"GoldsrcFramework.runtimeconfig.json";
	load_assembly_and_get_function_pointer_fn load_assembly_and_get_function_pointer = nullptr;
	load_assembly_and_get_function_pointer = get_dotnet_load_assembly(config_path.c_str());

	//
	// STEP 3: Load managed assembly and get function pointer to a managed method
	//
	const string_t dotnetlib_path = root_path + L"GoldsrcFramework.dll";
	const char_t* dotnet_type = L"GoldsrcFramework.HostingTest, GoldsrcFramework";
	const char_t* dotnet_type_method = L"Test";


	// Function pointer to managed delegate with non-default signature
	fn_Test pfn_Test = nullptr;
	int rc = load_assembly_and_get_function_pointer(
		dotnetlib_path.c_str(),
		dotnet_type,
		dotnet_type_method /*method_name*/,
		UNMANAGEDCALLERSONLY_METHOD,
		nullptr,
		(void**)&pfn_Test);

	return pfn_Test(pIntValue);
}