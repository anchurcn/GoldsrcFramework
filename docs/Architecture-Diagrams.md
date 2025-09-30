# GoldsrcFramework 架构图集

本文档包含 GoldsrcFramework 的所有架构图和流程图。这些图表使用 Mermaid 格式绘制，可以在支持 Mermaid 的 Markdown 查看器中查看。

## 目录

1. [整体架构图](#1-整体架构图)
2. [服务端初始化流程](#2-服务端初始化流程)
3. [运行时函数调用流程](#3-运行时函数调用流程)
4. [实体私有数据分配流程](#4-实体私有数据分配流程)
5. [类型系统和接口层次结构](#5-类型系统和接口层次结构)
6. [构建和部署流程](#6-构建和部署流程)
7. [数据流和内存管理](#7-数据流和内存管理)

---

## 1. 整体架构图

展示 GoldsrcFramework 的整体架构，包括各个层次和组件之间的关系。

```mermaid
graph TB
    subgraph "GoldSrc Engine"
        Engine[GoldSrc Engine<br/>hl_win32.exe]
    end
    
    subgraph "Native Layer (C++)"
        Loader[GoldsrcFramework.Loader<br/>client.dll / hl.dll]
        HostFxr[.NET Host FXR<br/>Runtime Initialization]
        EntityExports[entity_exports.cpp<br/>Entity Export Functions]
    end
    
    subgraph "Framework Layer (C#)"
        FrameworkInterop[FrameworkInterop<br/>Unified Entry Point]
        ServerMain[ServerMain<br/>Server Management]
        ClientMain[ClientMain<br/>Client Management]
        LegacyServer[LegacyServerInterop<br/>Legacy DLL Bridge]
        LegacyClient[LegacyClientInterop<br/>Legacy DLL Bridge]
        FrameworkServer[FrameworkServerExports<br/>Default Implementation]
        FrameworkClient[FrameworkClientExports<br/>Default Implementation]
        EntityContext[EntityContext<br/>Entity Management]
    end
    
    subgraph "Engine Interface Layer (C#)"
        IServerExport[IServerExportFuncs<br/>Interface]
        IClientExport[IClientExportFuncs<br/>Interface]
        EngineTypes[EngineTypes<br/>Type Definitions]
        ServerEngineFuncs[ServerEngineFuncs<br/>enginefuncs_t]
        ClientEngineFuncs[ClientEngineFuncs<br/>cl_enginefunc_t]
    end
    
    subgraph "Game Layer (C#)"
        GameServer[GameServerExports<br/>Custom Implementation]
        GameClient[GameClientExports<br/>Custom Implementation]
    end
    
    subgraph "Legacy DLLs"
        LibServer[libserver.dll<br/>Original HL Server]
        LibClient[libclient.dll<br/>Original HL Client]
    end
    
    Engine -->|Load DLL| Loader
    Loader -->|Initialize| HostFxr
    HostFxr -->|Load Assembly| FrameworkInterop
    Loader -->|Call Exports| FrameworkInterop
    
    FrameworkInterop -->|Server Init| ServerMain
    FrameworkInterop -->|Client Init| ClientMain
    FrameworkInterop -->|Entity Alloc| EntityContext
    
    ServerMain -->|Load Game| GameServer
    ServerMain -->|Fallback| FrameworkServer
    ClientMain -->|Load Game| GameClient
    ClientMain -->|Fallback| FrameworkClient
    
    GameServer -.->|Implements| IServerExport
    GameClient -.->|Implements| IClientExport
    FrameworkServer -.->|Implements| IServerExport
    FrameworkClient -.->|Implements| IClientExport
    
    FrameworkServer -->|Forward Calls| LegacyServer
    FrameworkClient -->|Forward Calls| LegacyClient
    
    LegacyServer -->|P/Invoke| LibServer
    LegacyClient -->|P/Invoke| LibClient
    EntityContext -->|Load Entities| LibServer
    
    IServerExport -.->|Uses| EngineTypes
    IClientExport -.->|Uses| EngineTypes
    ServerMain -->|Receives| ServerEngineFuncs
    ClientMain -->|Receives| ClientEngineFuncs
    
    EntityExports -->|Generated| Loader
    
    style Engine fill:#ff9999
    style Loader fill:#ffcc99
    style FrameworkInterop fill:#99ccff
    style GameServer fill:#99ff99
    style GameClient fill:#99ff99
    style LibServer fill:#cccccc
    style LibClient fill:#cccccc
```

**关键组件说明**:
- **红色**: GoldSrc 引擎
- **橙色**: 原生 C++ 加载器层
- **蓝色**: C# 框架层
- **绿色**: 游戏逻辑层
- **灰色**: 原版 Half-Life DLL

---

## 2. 服务端初始化流程

展示服务端从引擎加载到完全初始化的完整流程。

```mermaid
sequenceDiagram
    participant Engine as GoldSrc Engine
    participant Loader as Loader (C++)
    participant HostFxr as .NET Runtime
    participant FwInterop as FrameworkInterop
    participant ServerMain as ServerMain
    participant LegacyInterop as LegacyServerInterop
    participant LibServer as libserver.dll
    participant GameAsm as Game Assembly
    
    Engine->>Loader: LoadLibrary("hl.dll")
    activate Loader
    
    Note over Loader: First call triggers initialization
    
    Engine->>Loader: GiveFnptrsToDll(enginefuncs, globals)
    Loader->>Loader: init_load_assembly_and_get_function_pointer()
    Loader->>HostFxr: load_hostfxr()
    activate HostFxr
    HostFxr-->>Loader: hostfxr loaded
    
    Loader->>HostFxr: get_dotnet_load_assembly(config)
    HostFxr-->>Loader: load_assembly_fn
    
    Loader->>HostFxr: load_assembly_and_get_function_pointer<br/>("GoldsrcFramework.dll", "FrameworkInterop", "GiveFnptrsToDll")
    HostFxr->>FwInterop: Load Assembly
    activate FwInterop
    HostFxr-->>Loader: function pointer
    
    Loader->>FwInterop: GiveFnptrsToDll(enginefuncs, globals)
    FwInterop->>FwInterop: EnsureFrameworkInitialized()
    Note over FwInterop: Setup environment variables
    
    FwInterop->>FwInterop: EnsureServerInitialized()
    FwInterop->>GameAsm: Load "GoldsrcFramework.Demo.dll"
    activate GameAsm
    FwInterop->>GameAsm: Find type implementing IServerExportFuncs
    alt Game Implementation Found
        GameAsm-->>FwInterop: GameServerExports type
        FwInterop->>GameAsm: Activator.CreateInstance()
    else No Implementation
        FwInterop->>FwInterop: Use FrameworkServerExports
    end
    
    FwInterop->>ServerMain: Initialize(gameAssembly)
    activate ServerMain
    ServerMain->>ServerMain: Store IServerExportFuncs instance
    
    FwInterop->>ServerMain: GiveFnptrsToDll(enginefuncs, globals)
    ServerMain->>LegacyInterop: Initialize(enginefuncs, globals)
    activate LegacyInterop
    
    LegacyInterop->>LibServer: LoadLibrary("libserver.dll")
    activate LibServer
    LegacyInterop->>LibServer: GiveFnptrsToDll(enginefuncs, globals)
    LegacyInterop->>LibServer: GetEntityAPI2(&pFunctionTable, &version)
    LibServer-->>LegacyInterop: Function table filled
    LegacyInterop->>LibServer: GetNewDLLFunctions(&pNewFunctionTable, &version)
    LibServer-->>LegacyInterop: New function table filled
    
    LegacyInterop-->>ServerMain: Initialized
    ServerMain->>ServerMain: Store enginefuncs pointer
    ServerMain-->>FwInterop: Success
    FwInterop-->>Loader: Success
    Loader-->>Engine: Success
    
    Note over Engine,LibServer: Initialization Complete
    
    Engine->>Loader: GetEntityAPI2(&pFunctionTable, &version)
    Loader->>HostFxr: Get function pointer for GetEntityAPI2
    HostFxr-->>Loader: function pointer
    Loader->>FwInterop: GetEntityAPI2(&pFunctionTable, &version)
    FwInterop->>ServerMain: GetEntityAPI2(&pFunctionTable, &version)
    ServerMain->>ServerMain: Fill function table with<br/>IServerExportFuncs methods
    ServerMain-->>FwInterop: Success (1)
    FwInterop-->>Loader: Success (1)
    Loader-->>Engine: Success (1)
    
    deactivate LibServer
    deactivate LegacyInterop
    deactivate ServerMain
    deactivate GameAsm
    deactivate FwInterop
    deactivate HostFxr
    deactivate Loader
```

**关键步骤**:
1. 引擎加载 hl.dll (实际是 Loader)
2. Loader 初始化 .NET 运行时
3. 加载 GoldsrcFramework.dll
4. 加载游戏程序集 (GoldsrcFramework.Demo.dll)
5. 初始化 LegacyServerInterop 并加载 libserver.dll
6. 填充函数表返回给引擎

---

## 3. 运行时函数调用流程

展示运行时引擎调用游戏代码的流程 (以 Spawn 函数为例)。

```mermaid
sequenceDiagram
    participant Engine as GoldSrc Engine
    participant FuncTable as ServerExportFuncs<br/>Function Table
    participant GameExports as GameServerExports<br/>(or FrameworkServerExports)
    participant LegacyInterop as LegacyServerInterop
    participant LibServer as libserver.dll
    participant EngineFunc as Engine Functions<br/>(enginefuncs_t)
    
    Note over Engine: Entity needs to spawn
    
    Engine->>FuncTable: pFunctionTable->Spawn(edict)
    Note over FuncTable: Function pointer points to<br/>managed method
    
    FuncTable->>GameExports: Spawn(edict_t* pent)
    activate GameExports
    
    alt Custom Implementation
        Note over GameExports: Execute custom logic
        GameExports->>GameExports: Custom spawn code
        
        opt Call Engine Function
            GameExports->>EngineFunc: ServerMain.EngineApis->pfnServerPrint("message")
            EngineFunc->>Engine: ServerPrint("message")
            Engine-->>EngineFunc: Success
            EngineFunc-->>GameExports: Success
        end
        
        opt Forward to Legacy
            GameExports->>LegacyInterop: LegacyServerInterop.Spawn(pent)
            activate LegacyInterop
            LegacyInterop->>LibServer: Call function pointer
            activate LibServer
            LibServer->>LibServer: Original HL spawn logic
            
            opt Legacy calls engine
                LibServer->>Engine: enginefuncs->pfnCreateEntity()
                Engine-->>LibServer: New entity
            end
            
            LibServer-->>LegacyInterop: Return result
            deactivate LibServer
            LegacyInterop-->>GameExports: Return result
            deactivate LegacyInterop
        end
        
        GameExports-->>FuncTable: Return result
    else Framework Default
        Note over GameExports: FrameworkServerExports
        GameExports->>LegacyInterop: LegacyServerInterop.Spawn(pent)
        activate LegacyInterop
        LegacyInterop->>LibServer: Direct forward
        activate LibServer
        LibServer-->>LegacyInterop: Result
        deactivate LibServer
        LegacyInterop-->>GameExports: Result
        deactivate LegacyInterop
        GameExports-->>FuncTable: Return result
    end
    
    deactivate GameExports
    FuncTable-->>Engine: Return result
    
    Note over Engine: Spawn complete
```

**调用路径**:
- **自定义实现**: Engine → FuncTable → GameExports → [Custom Logic] → [Optional: LegacyInterop → libserver.dll]
- **框架默认**: Engine → FuncTable → FrameworkServerExports → LegacyInterop → libserver.dll

---

## 4. 实体私有数据分配流程

展示实体私有数据的分配机制。

请参考 Architecture.md 文档中的详细说明和 Mermaid 图表。

---

## 5. 类型系统和接口层次结构

展示框架中的类型系统、接口和类之间的关系。

请参考 Architecture.md 文档中的类图。

---

## 6. 构建和部署流程

展示从源代码到最终部署的完整构建流程。

请参考 Architecture.md 文档中的构建流程图。

---

## 7. 数据流和内存管理

展示不同内存空间之间的数据流动和指针关系。

请参考 Architecture.md 文档中的数据流图。

---

## 图表使用说明

### 在线查看

这些 Mermaid 图表可以在以下环境中查看:
- GitHub (原生支持 Mermaid)
- Visual Studio Code (安装 Mermaid 插件)
- Mermaid Live Editor: https://mermaid.live/

### 导出图片

可以使用以下工具将 Mermaid 图表导出为图片:
- Mermaid CLI: `mmdc -i input.md -o output.png`
- Mermaid Live Editor: 在线编辑并导出

### 修改图表

所有图表都使用 Mermaid 语法编写，可以直接编辑 Markdown 文件中的图表代码。

---

## 相关文档

- [Architecture.md](./Architecture.md) - 详细架构文档
- [README.md](../README.md) - 项目说明
- [Guide.md](../src/Guide.md) - 开发指南

