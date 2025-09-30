# GoldsrcFramework 架构文档

## 概述

GoldsrcFramework 是一个使用 C# (.NET 8) 重写 HLSDK 的代码库，支持使用 C# 开发 GoldSrc 引擎模组。该框架通过 .NET 托管互操作层，将原生 GoldSrc 引擎与 C# 托管代码连接起来。

## 核心设计理念

1. **分层架构**：清晰分离原生层、互操作层、框架层和游戏层
2. **向后兼容**：支持调用原版 Half-Life DLL 的功能
3. **可扩展性**：允许开发者通过继承接口自定义游戏逻辑
4. **代码生成**：从 HLSDK 自动生成 C# 类型定义

## 项目结构

```
GoldsrcFramework/
├── GoldsrcFramework.Loader       # C++ 原生加载器 (client.dll/hl.dll)
├── GoldsrcFramework.Engine       # 引擎类型定义和接口
├── GoldsrcFramework              # 核心框架实现
├── GoldsrcFramework.Demo         # 示例游戏实现
├── GoldsrcFramework.CodeGen      # 代码生成工具
├── GoldsrcFramework.BuildTool    # 构建工具
├── GoldsrcFramework.Math         # 数学库
└── GoldsrcFramework.NetLoader    # .NET AOT 加载器 (实验性)
```

## 架构层次

### 1. 原生层 (Native Layer)

**组件**: `GoldsrcFramework.Loader` (C++)

**职责**:
- 作为 GoldSrc 引擎直接加载的 DLL (client.dll/hl.dll)
- 初始化 .NET Core 运行时 (通过 hostfxr)
- 导出引擎期望的标准函数
- 将引擎调用转发到托管代码

**关键文件**:
- `loader.cpp`: .NET 运行时初始化和函数转发
- `entity_exports_demo.cpp`: 实体导出函数示例

### 2. 引擎接口层 (Engine Interface Layer)

**组件**: `GoldsrcFramework.Engine`

**职责**:
- 定义引擎与 MOD DLL 交互的所有类型和接口
- 从 HLSDK 生成的 C# 类型定义
- 提供类型安全的 P/Invoke 绑定

**关键类型**:
- `EngineTypes.cs`: 基础类型 (Vector3f, edict_t, entvars_t 等)
- `IServerExportFuncs`: 服务端导出函数接口
- `IClientExportFuncs`: 客户端导出函数接口
- `ServerEngineFuncs`: 服务端引擎函数 (enginefuncs_t)
- `ClientEngineFuncs`: 客户端引擎函数 (cl_enginefunc_t)

### 3. 框架层 (Framework Layer)

**组件**: `GoldsrcFramework`

**职责**:
- 提供统一的互操作入口点
- 管理游戏程序集加载
- 实现默认的框架行为
- 与原版 DLL 互操作

**关键类**:

#### FrameworkInterop
统一的原生调用入口点，使用 `[UnmanagedCallersOnly]` 导出函数。

**关键函数**:
- `GiveFnptrsToDll()`: 接收引擎函数指针
- `GetEntityAPI()` / `GetEntityAPI2()`: 返回服务端函数表
- `GetNewDLLFunctions()`: 返回新版服务端函数表
- `F()`: 客户端函数表初始化
- `GetPrivateDataAllocator()`: 实体私有数据分配器

#### ServerMain / ClientMain
管理服务端/客户端的初始化和函数分发。

**关键函数**:
- `Initialize(Assembly)`: 加载游戏程序集
- `GiveFnptrsToDll()`: 初始化引擎函数指针
- `GetEntityAPI()`: 填充函数表

#### LegacyServerInterop / LegacyClientInterop
与原版 Half-Life DLL (libserver.dll/libclient.dll) 互操作。

**关键函数**:
- `Initialize()`: 加载原版 DLL 并获取函数表
- 各种转发函数: `Spawn()`, `GameInit()`, `ClientConnect()` 等

#### FrameworkServerExports / FrameworkClientExports
框架默认实现，转发调用到原版 DLL。

### 4. 游戏层 (Game Layer)

**组件**: `GoldsrcFramework.Demo` 或用户自定义程序集

**职责**:
- 实现 `IServerExportFuncs` 和/或 `IClientExportFuncs`
- 自定义游戏逻辑
- 可选择性覆盖框架默认行为

## 实体系统

### 实体私有数据分配

GoldSrc 引擎通过导出的实体函数来分配实体私有数据。

**流程**:
1. 引擎调用导出的实体函数 (如 `monster_barney`)
2. 原生加载器转发到 `GetPrivateDataAllocator()`
3. `EntityContext.GetLegacyEntityPrivateDataAllocator()` 从 libserver.dll 获取函数指针
4. 返回函数指针给原生层
5. 原生层缓存并调用该函数指针

**关键组件**:
- `EntityContext`: 管理实体列表和分配器
- `entity_exports_demo.cpp`: 生成的实体导出函数
- `InitializePrivateDataAllocators()`: 初始化函数指针表

## 代码生成

### GoldsrcFramework.CodeGen

从 HLSDK 的 C/C++ 代码生成 C# 类型定义。

**生成内容**:
- 结构体定义 (struct)
- 函数指针类型 (typedef)
- 枚举 (enum)
- 常量 (const/define)

**类型映射**:
- `vec3_t` → `Vector3f`
- `edict_t*` → `edict_t*`
- `qboolean` → `qboolean` (int)
- 函数指针 → `delegate* unmanaged[Cdecl]<...>`

## 构建流程

### 标准构建步骤

1. **编译 GoldsrcFramework.Engine**: 生成引擎类型定义
2. **编译 GoldsrcFramework**: 生成核心框架
3. **编译游戏程序集** (如 GoldsrcFramework.Demo)
4. **编译 GoldsrcFramework.Loader**: 生成原生 DLL
5. **复制文件到 mod 目录**:
   - `client.dll` → `cl_dlls/client.dll`
   - `hl.dll` → `dlls/hl.dll`
   - 原版 DLL 重命名为 `libclient.dll` / `libserver.dll`
   - 所有 .NET 程序集和运行时文件

### 实体导出生成

构建工具扫描游戏程序集，生成 `entity_exports.cpp`，包含所有实体的导出函数。

## 初始化流程

### 服务端初始化

1. **引擎加载** `hl.dll` (实际是 GoldsrcFramework.Loader)
2. **Loader 初始化** .NET Core 运行时
3. **引擎调用** `GiveFnptrsToDll(enginefuncs, globals)`
4. **Loader 转发**到 `FrameworkInterop.GiveFnptrsToDll()`
5. **FrameworkInterop** 调用 `EnsureFrameworkInitialized()`
6. **FrameworkInterop** 调用 `EnsureServerInitialized()`
   - 尝试加载 `GoldsrcFramework.Demo.dll`
   - 查找实现 `IServerExportFuncs` 的类型
   - 如果找到，实例化；否则使用 `FrameworkServerExports`
7. **ServerMain** 初始化 `LegacyServerInterop`
   - 加载 `libserver.dll`
   - 调用原版 `GiveFnptrsToDll()`
   - 获取原版函数表
8. **引擎调用** `GetEntityAPI2(pFunctionTable, version)`
9. **ServerMain** 填充函数表，指向 `IServerExportFuncs` 实例的方法

### 客户端初始化

流程类似服务端，但使用客户端相关的接口和函数。

## 运行时调用流程

### 示例: 实体生成 (Spawn)

```
GoldSrc Engine
    ↓ 调用 pFunctionTable->Spawn(edict)
ServerExportFuncs 函数表
    ↓ 指向
GameServerExports.Spawn() 或 FrameworkServerExports.Spawn()
    ↓ 可选择调用
LegacyServerInterop.Spawn()
    ↓ P/Invoke 调用
libserver.dll (原版 Half-Life)
    ↓ 返回结果
    ↑
```

### 示例: 引擎函数调用

```
GameServerExports.ClientConnect()
    ↓ 需要调用引擎函数
ServerMain.EngineApis->pfnServerPrint()
    ↓ 函数指针调用
GoldSrc Engine
```

## 关键技术点

### 1. UnmanagedCallersOnly

使用 .NET 5+ 的 `[UnmanagedCallersOnly]` 特性，允许原生代码直接调用托管函数，无需 COM 互操作。

```csharp
[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
public static void GiveFnptrsToDll(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals)
{
    // 可以被原生代码直接调用
}
```

### 2. 函数指针类型

使用 C# 9+ 的函数指针语法表示原生函数指针。

```csharp
public unsafe struct enginefuncs_s
{
    public delegate* unmanaged[Cdecl]<int, sbyte*, void> pfnServerPrint;
    public delegate* unmanaged[Cdecl]<edict_t*, void> pfnRemoveEntity;
    // ...
}
```

### 3. 程序集动态加载

框架在运行时加载游戏程序集，通过反射查找实现接口的类型。

```csharp
var serverType = gameAssembly.GetTypes()
    .FirstOrDefault(x => x.GetInterface(nameof(IServerExportFuncs)) == typeof(IServerExportFuncs));
```

### 4. 双向互操作

- **引擎 → 框架**: 通过导出函数和函数表
- **框架 → 引擎**: 通过引擎函数指针 (enginefuncs_t)
- **框架 → 原版 DLL**: 通过 P/Invoke 和函数表

## 扩展点

### 自定义服务端逻辑

```csharp
public unsafe class MyServerExports : FrameworkServerExports
{
    public override int Spawn(edict_t* pent)
    {
        // 自定义逻辑
        Console.WriteLine("Custom spawn logic");
        
        // 可选择调用基类 (转发到原版)
        return base.Spawn(pent);
    }
}
```

### 自定义客户端逻辑

```csharp
public unsafe class MyClientExports : FrameworkClientExports
{
    public override void HUD_Init()
    {
        // 自定义 HUD 初始化
        base.HUD_Init();
    }
}
```

## 性能考虑

1. **函数指针调用**: 直接调用，无装箱/拆箱开销
2. **结构体传递**: 使用指针传递，避免复制
3. **热重载支持**: .NET 8 支持 Edit and Continue
4. **AOT 编译**: 实验性的 NetLoader 支持 Native AOT

## 已知限制

1. **仅支持 Windows**: 当前实现依赖 Windows API
2. **需要原版 DLL**: 当前依赖 libserver.dll/libclient.dll
3. **Xash3D 兼容性**: 在 Xash3D 上可运行，原版 GoldSrc 引擎崩溃 (WIP)

## 未来规划

1. **完全托管实现**: 移除对原版 DLL 的依赖
2. **跨平台支持**: Linux 和 macOS 支持
3. **更好的工具链**: 集成 Stride3D 等现代引擎工具
4. **性能优化**: Native AOT 编译
5. **完善文档**: 更多教程和示例

## 关键函数调用链

### 服务端关键函数

#### GiveFnptrsToDll
```
GoldSrc Engine
  → Loader::GiveFnptrsToDll(enginefuncs_s*, globalvars_t*)
    → load_assembly_and_get_function_pointer("GoldsrcFramework.dll", "FrameworkInterop", "GiveFnptrsToDll")
      → FrameworkInterop::GiveFnptrsToDll(enginefuncs_s*, globalvars_t*)
        → EnsureFrameworkInitialized()
        → EnsureServerInitialized()
          → Assembly.LoadFrom("GoldsrcFramework.Demo.dll")
          → ServerMain::Initialize(Assembly)
        → ServerMain::GiveFnptrsToDll(enginefuncs_s*, globalvars_t*)
          → LegacyServerInterop::Initialize(enginefuncs_s*, globalvars_t*)
            → NativeLibrary.Load("libserver.dll")
            → P/Invoke: GiveFnptrsToDll(enginefuncs_s*, globalvars_t*)
            → P/Invoke: GetEntityAPI2(ServerExportFuncs*, int*)
            → P/Invoke: GetNewDLLFunctions(ServerNewExportFuncs*, int*)
```

#### GetEntityAPI2
```
GoldSrc Engine
  → Loader::GetEntityAPI2(ServerExportFuncs*, int*)
    → load_assembly_and_get_function_pointer("GoldsrcFramework.dll", "FrameworkInterop", "GetEntityAPI2")
      → FrameworkInterop::GetEntityAPI2(ServerExportFuncs*, int*)
        → ServerMain::GetEntityAPI2(ServerExportFuncs*, int*)
          → ServerMain::FillFunctionTable(ServerExportFuncs*)
            → pFunctionTable->GameInit = &IServerExportFuncs::GameInit
            → pFunctionTable->Spawn = &IServerExportFuncs::Spawn
            → pFunctionTable->ClientConnect = &IServerExportFuncs::ClientConnect
            → ... (填充所有函数指针)
```

#### Spawn (运行时调用)
```
GoldSrc Engine
  → pFunctionTable->Spawn(edict_t*)
    → GameServerExports::Spawn(edict_t*) 或 FrameworkServerExports::Spawn(edict_t*)
      → [可选] 自定义逻辑
      → [可选] LegacyServerInterop::Spawn(edict_t*)
        → LegacyServerApiPtr->Spawn(edict_t*)
          → libserver.dll::Spawn(edict_t*)
```

#### 引擎函数调用 (从游戏代码调用引擎)
```
GameServerExports::SomeMethod()
  → ServerMain.EngineApis->pfnServerPrint("message")
    → GoldSrc Engine::ServerPrint("message")
```

### 客户端关键函数

#### F (客户端初始化)
```
GoldSrc Engine
  → Loader::F(ClientExportFuncs*)
    → load_assembly_and_get_function_pointer("GoldsrcFramework.dll", "FrameworkInterop", "F")
      → FrameworkInterop::F(ClientExportFuncs*)
        → EnsureFrameworkInitialized()
        → EnsureClientInitialized()
          → Assembly.LoadFrom("GoldsrcFramework.Demo.dll")
          → ClientMain::Initialize(Assembly)
        → ClientMain::F(ClientExportFuncs*)
          → ClientMain::FillFunctionTable(ClientExportFuncs*)
            → pFunctionTable->Initialize = &IClientExportFuncs::Initialize
            → pFunctionTable->HUD_Init = &IClientExportFuncs::HUD_Init
            → ... (填充所有函数指针)
```

#### HUD_Init (运行时调用)
```
GoldSrc Engine
  → pFunctionTable->HUD_Init()
    → GameClientExports::HUD_Init() 或 FrameworkClientExports::HUD_Init()
      → [可选] 自定义逻辑
      → [可选] LegacyClientInterop::HUD_Init()
        → P/Invoke: libclient.dll::HUD_Init()
```

### 实体系统关键函数

#### GetPrivateDataAllocator
```
Loader::InitializePrivateDataAllocators()
  → load_assembly_and_get_function_pointer("GoldsrcFramework.dll", "FrameworkInterop", "GetPrivateDataAllocator")
    → FrameworkInterop::GetPrivateDataAllocator(IntPtr pszEntityClassName)
      → EntityContext::GetLegacyEntityPrivateDataAllocator(string entityClassName)
        → NativeLibrary.Load("libserver.dll")
        → NativeLibrary.TryGetExport(handle, entityClassName, out address)
        → return address (或 ErrorAllocator)
```

#### 实体导出函数调用
```
GoldSrc Engine
  → Loader::monster_barney(entvars_t*)
    → entity_exports.cpp::monster_barney(entvars_t*)
      → g_allocFuncs.monster_barney(entvars_t*)
        → libserver.dll::monster_barney(entvars_t*)
          → GetClassPtr((CBarney*)pev)
            → 分配 CBarney 私有数据
            → 存储到 edict->pvPrivateData
```

## .NET 运行时加载关键函数

### Loader 初始化
```
Loader::init_load_assembly_and_get_function_pointer()
  → load_hostfxr()
    → get_hostfxr_path(buffer, &buffer_size, nullptr)
    → LoadLibrary(hostfxr_path)
    → GetProcAddress("hostfxr_initialize_for_runtime_config")
    → GetProcAddress("hostfxr_get_runtime_delegate")
    → GetProcAddress("hostfxr_close")
  → get_dotnet_load_assembly(config_path)
    → hostfxr_initialize_for_runtime_config(config_path, nullptr, &context)
    → hostfxr_get_runtime_delegate(context, hdt_load_assembly_and_get_function_pointer, &delegate)
    → return delegate
```

### 程序集加载
```
g_load_assembly_and_get_function_pointer(
    L"GoldsrcFramework.dll",
    L"GoldsrcFramework.FrameworkInterop, GoldsrcFramework",
    L"GiveFnptrsToDll",
    UNMANAGEDCALLERSONLY_METHOD,
    nullptr,
    (void**)&pfn_GiveFnptrsToDll
)
  → .NET Runtime 加载程序集
  → 查找类型和方法
  → 返回 UnmanagedCallersOnly 函数指针
```

## 参考资料

- [HLSDK](https://github.com/ValveSoftware/halflife)
- [.NET Hosting](https://docs.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting)
- [UnmanagedCallersOnly](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute)
- [Function Pointers in C#](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/function-pointers)

