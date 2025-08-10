#include <stdio.h>
#include <windows.h>

// Function pointers for the loader functions
typedef void (*F_func)(void* pv);
typedef int (*Test_func)(int* pIntValue);
typedef void* (*GetPrivateDataAllocator_func)(const char* pszEntityClassName);

int main() {
    printf("Testing NetLoader (client.dll)...\n");

    // Load the NetLoader DLL
    HMODULE hLoader = LoadLibraryA("client.dll");
    if (!hLoader) {
        printf("Failed to load client.dll. Error: %lu\n", GetLastError());
        return 1;
    }

    printf("Successfully loaded client.dll\n");

    // Get function pointers
    F_func F = (F_func)GetProcAddress(hLoader, "F");
    Test_func Test = (Test_func)GetProcAddress(hLoader, "Test");
    GetPrivateDataAllocator_func GetPrivateDataAllocator = 
        (GetPrivateDataAllocator_func)GetProcAddress(hLoader, "GetPrivateDataAllocator");

    if (!F) {
        printf("Failed to get F function\n");
        FreeLibrary(hLoader);
        return 1;
    }

    if (!Test) {
        printf("Failed to get Test function\n");
        FreeLibrary(hLoader);
        return 1;
    }

    if (!GetPrivateDataAllocator) {
        printf("Failed to get GetPrivateDataAllocator function\n");
        FreeLibrary(hLoader);
        return 1;
    }

    printf("All functions found successfully\n");

    // Test the F function
    printf("Testing F function...\n");
    F(NULL);
    printf("F function called successfully\n");

    // Test the Test function
    printf("Testing Test function...\n");
    int testValue = 42;
    int result = Test(&testValue);
    printf("Test function returned: %d\n", result);

    // Test the GetPrivateDataAllocator function
    printf("Testing GetPrivateDataAllocator function...\n");
    void* allocator = GetPrivateDataAllocator("test_entity");
    printf("GetPrivateDataAllocator returned: %p\n", allocator);

    // Cleanup
    FreeLibrary(hLoader);
    printf("NetLoader test completed successfully!\n");
    return 0;
}
