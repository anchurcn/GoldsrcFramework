# GoldsrcFramework 快速参考

本文档提供 GoldsrcFramework 的快速参考，包括关键 API、常用模式和代码示例。

## 目录

1. [核心接口](#核心接口)
2. [关键类和方法](#关键类和方法)
3. [常用代码模式](#常用代码模式)
4. [调试技巧](#调试技巧)
5. [常见问题](#常见问题)

---

## 核心接口

### IServerExportFuncs

服务端导出函数接口，游戏服务端逻辑需要实现此接口。

```csharp
public unsafe interface IServerExportFuncs
{
    // 游戏初始化
    void GameInit();
    
    // 实体生成
    int Spawn(edict_t* pent);
    
    // 客户端连接
    qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason);
    
    // 客户端断开
    void ClientDisconnect(edict_t* pEntity);
    
    // 服务器激活
    void ServerActivate(edict_t* pEdictList, int edictCount, int clientMax);
    
    // 每帧调用
    void StartFrame();
    
    // 玩家思考前
    void PlayerPreThink(edict_t* pEntity);
    
    // 玩家思考后
    void PlayerPostThink(edict_t* pEntity);
    
    // ... 更多方法
}
```

### IClientExportFuncs

客户端导出函数接口，游戏客户端逻辑需要实现此接口。

```csharp
public unsafe interface IClientExportFuncs
{
    // 初始化
    int Initialize(cl_enginefunc_t* pEnginefuncs, int iVersion);
    
    // HUD 初始化
    void HUD_Init();
    
    // 视频初始化
    int HUD_VidInit();
    
    // HUD 重绘
    int HUD_Redraw(float time, int intermission);
    
    // 更新客户端数据
    int HUD_UpdateClientData(client_data_t* pcldata, float flTime);
    
    // 每帧调用
    void HUD_Frame(double time);
    
    // ... 更多方法
}
```

---

## 关键类和方法

### FrameworkInterop

统一的原生调用入口点。

```csharp
public unsafe class FrameworkInterop
{
    // 服务端入口
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void GiveFnptrsToDll(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals);
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int GetEntityAPI2(ServerExportFuncs* pFunctionTable, int* interfaceVersion);
    
    // 客户端入口
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void F(ClientExportFuncs* pv);
    
    // 实体系统
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static IntPtr GetPrivateDataAllocator(IntPtr pszEntityClassName);
}
```

### ServerMain

服务端管理类。

```csharp
public unsafe class ServerMain
{
    // 引擎函数指针 (用于调用引擎功能)
    public static enginefuncs_s* EngineApis { get; private set; }
    
    // 全局变量
    public static globalvars_t* Globals { get; private set; }
    
    // 初始化
    public static void Initialize(Assembly? gameAssembly);
    
    // 接收引擎函数指针
    public static void GiveFnptrsToDll(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals);
}
```

### ClientMain

客户端管理类。

```csharp
public unsafe class ClientMain
{
    // 引擎函数指针
    public static cl_enginefunc_t* EngineApis { get; private set; }
    
    // 初始化
    public static void Initialize(Assembly? gameAssembly);
}
```

### LegacyServerInterop

与原版服务端 DLL 互操作。

```csharp
public unsafe static class LegacyServerInterop
{
    // 初始化原版 DLL
    public static void Initialize(enginefuncs_s* pengfuncsFromEngine, globalvars_t* pGlobals);
    
    // 转发函数 (调用原版 DLL)
    public static void GameInit();
    public static int Spawn(edict_t* pent);
    public static qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason);
    // ... 更多转发函数
}
```

---

## 常用代码模式

### 1. 创建自定义服务端实现

```csharp
using GoldsrcFramework.Engine.Native;

public unsafe class MyServerExports : FrameworkServerExports
{
    public override void GameInit()
    {
        Console.WriteLine("My custom game initialization!");
        
        // 可选: 调用原版逻辑
        base.GameInit();
    }
    
    public override int Spawn(edict_t* pent)
    {
        Console.WriteLine($"Spawning entity at {(IntPtr)pent:X}");
        
        // 自定义逻辑
        // ...
        
        // 调用原版逻辑
        return base.Spawn(pent);
    }
    
    public override qboolean ClientConnect(edict_t* pEntity, sbyte* pszName, sbyte* pszAddress, sbyte* szRejectReason)
    {
        // 将 sbyte* 转换为 string
        string playerName = Marshal.PtrToStringUTF8((IntPtr)pszName) ?? "Unknown";
        string address = Marshal.PtrToStringUTF8((IntPtr)pszAddress) ?? "Unknown";
        
        Console.WriteLine($"Player {playerName} connecting from {address}");
        
        // 可以拒绝连接
        // Marshal.Copy(Encoding.UTF8.GetBytes("Server is full"), 0, (IntPtr)szRejectReason, ...);
        // return 0; // 拒绝
        
        return base.ClientConnect(pEntity, pszName, pszAddress, szRejectReason);
    }
}
```

### 2. 调用引擎函数

```csharp
public unsafe class MyServerExports : FrameworkServerExports
{
    public override void StartFrame()
    {
        // 获取引擎函数指针
        var engineFuncs = ServerMain.EngineApis;
        
        // 调用引擎函数: 打印消息到服务器控制台
        var message = "Hello from C#!\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        fixed (byte* pMsg = bytes)
        {
            engineFuncs->pfnServerPrint((sbyte*)pMsg);
        }
        
        // 预缓存模型
        var modelName = "models/player.mdl";
        var modelBytes = Encoding.UTF8.GetBytes(modelName + "\0");
        fixed (byte* pModel = modelBytes)
        {
            int modelIndex = engineFuncs->pfnPrecacheModel((sbyte*)pModel);
        }
        
        base.StartFrame();
    }
}
```

### 3. 访问全局变量

```csharp
public unsafe class MyServerExports : FrameworkServerExports
{
    public override void StartFrame()
    {
        var globals = ServerMain.Globals;
        
        // 获取当前时间
        float currentTime = globals->time;
        
        // 获取地图名称
        string mapName = Marshal.PtrToStringUTF8((IntPtr)globals->mapname) ?? "unknown";
        
        // 获取最大客户端数
        int maxClients = globals->maxClients;
        
        Console.WriteLine($"Time: {currentTime}, Map: {mapName}, MaxClients: {maxClients}");
        
        base.StartFrame();
    }
}
```

### 4. 操作实体 (edict_t)

```csharp
public unsafe class MyServerExports : FrameworkServerExports
{
    public override int Spawn(edict_t* pent)
    {
        // 访问实体变量
        var pev = pent->v;
        
        // 读取实体位置
        Vector3f origin = pev->origin;
        Console.WriteLine($"Entity origin: ({origin.X}, {origin.Y}, {origin.Z})");
        
        // 修改实体属性
        pev->health = 100.0f;
        pev->max_health = 100.0f;
        
        // 设置模型
        var modelName = "models/player.mdl";
        var modelBytes = Encoding.UTF8.GetBytes(modelName + "\0");
        fixed (byte* pModel = modelBytes)
        {
            ServerMain.EngineApis->pfnSetModel(pent, (sbyte*)pModel);
        }
        
        return base.Spawn(pent);
    }
}
```

### 5. 创建自定义客户端实现

```csharp
using GoldsrcFramework.Engine.Native;

public unsafe class MyClientExports : FrameworkClientExports
{
    public override void HUD_Init()
    {
        Console.WriteLine("Custom HUD initialization!");
        base.HUD_Init();
    }
    
    public override int HUD_Redraw(float time, int intermission)
    {
        // 自定义 HUD 绘制
        var engineFuncs = ClientMain.EngineApis;
        
        // 绘制文本示例
        // engineFuncs->pfnDrawString(x, y, "Hello World", r, g, b);
        
        return base.HUD_Redraw(time, intermission);
    }
}
```

---

## 调试技巧

### 1. 启用控制台输出

```csharp
public override void GameInit()
{
    Console.WriteLine("Game initialized!");
    System.Diagnostics.Debug.WriteLine("Debug output");
}
```

### 2. 附加 Visual Studio 调试器

1. 启动游戏
2. Visual Studio → Debug → Attach to Process
3. 选择 `hl.exe` 或 `xash3d.exe`
4. 设置断点

### 3. 使用条件断点

```csharp
public override int Spawn(edict_t* pent)
{
    // 在特定条件下中断
    if (pent->v->classname != null)
    {
        string className = Marshal.PtrToStringUTF8((IntPtr)pent->v->classname) ?? "";
        if (className == "player")
        {
            System.Diagnostics.Debugger.Break(); // 触发断点
        }
    }
    return base.Spawn(pent);
}
```

### 4. 日志记录

```csharp
public class Logger
{
    private static StreamWriter? _logFile;
    
    public static void Initialize()
    {
        _logFile = new StreamWriter("gsf_debug.log", append: true);
        _logFile.AutoFlush = true;
    }
    
    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        _logFile?.WriteLine($"[{timestamp}] {message}");
        Console.WriteLine(message);
    }
}
```

---

## 常见问题

### Q: 如何判断是否在使用自定义实现？

```csharp
public override void GameInit()
{
    Console.WriteLine($"Using implementation: {this.GetType().Name}");
    base.GameInit();
}
```

### Q: 如何安全地转换字符串指针？

```csharp
public static string SafePtrToString(sbyte* ptr)
{
    if (ptr == null) return string.Empty;
    return Marshal.PtrToStringUTF8((IntPtr)ptr) ?? string.Empty;
}
```

### Q: 如何创建字符串并传递给引擎？

```csharp
public static void PrintToServer(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message + "\0");
    fixed (byte* pMsg = bytes)
    {
        ServerMain.EngineApis->pfnServerPrint((sbyte*)pMsg);
    }
}
```

### Q: 如何遍历所有实体？

```csharp
public void IterateAllEntities()
{
    var globals = ServerMain.Globals;
    int maxEntities = globals->maxEntities;
    
    for (int i = 0; i < maxEntities; i++)
    {
        edict_t* pEdict = ServerMain.EngineApis->pfnPEntityOfEntIndex(i);
        if (pEdict != null && !pEdict->free)
        {
            // 处理实体
            string className = SafePtrToString(pEdict->v->classname);
            Console.WriteLine($"Entity {i}: {className}");
        }
    }
}
```

### Q: 如何处理异常？

```csharp
public override int Spawn(edict_t* pent)
{
    try
    {
        // 自定义逻辑
        return CustomSpawn(pent);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in Spawn: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        
        // 回退到原版逻辑
        return base.Spawn(pent);
    }
}
```

---

## 相关文档

- [Architecture.md](./Architecture.md) - 详细架构文档
- [Architecture-Diagrams.md](./Architecture-Diagrams.md) - 架构图集
- [Example.cs](../src/GoldsrcFramework/Example.cs) - 完整示例代码

