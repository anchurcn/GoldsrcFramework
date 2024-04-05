#include<stdio.h>
#include<assert.h>
#include<Windows.h>

// The better project name is GoldsrcFramework.Loading.Test
// Because this project is not just test the loader.

typedef int (*fn_Test)(void* pIntValue);

int main(int argc, char** argv)
{

	HMODULE hModule = LoadLibrary(L"gsfloader.dll");
	if (hModule == NULL)
	{
		printf("Cannot load gsfloader.dll.");
		return 1;
	}

	fn_Test pfn = (fn_Test)GetProcAddress(hModule, "Test");
	if (pfn == NULL)
	{
		printf("Cannot find Test function.");
		return 2;
	}

	int value = 0xAABBFFDD;
	int retval = pfn(&value);
	assert(retval == value);

	//value = 0x00AABBCC;
	//retval = pfn(&value);
	//assert(retval == value);

	printf("Test passed.");

	return 0;
}