# Legacy-Based Framework 架构文档

## 概述

GoldsrcFramework 现在采用基于 Legacy 的架构，所有 Framework 的导出实现都基于 `LegacyServerInterop`，确保与原版 hl.dll 的完全兼容性。

## 架构层次

```
┌─────────────────────────────────────┐
│          用户自定义实现              │
│    (继承 FrameworkServerExports)    │
├─────────────────────────────────────┤
│       FrameworkServerExports       │
│      (基于 LegacyServerInterop)     │
├─────────────────────────────────────┤
│       LegacyServerInterop          │
│        (静态转发方法)               │
├─────────────────────────────────────┤
│         原版 hl.dll                │
│      (unmanaged 实现)              │
└─────────────────────────────────────┘
```

## 核心组件

### 1. LegacyServerInterop
- 提供与原版 hl.dll 的直接交互
- 所有 `IServerExportFuncs` 方法的静态转发实现
- 自动异常处理和初始化检查

### 2. FrameworkServerExports
- 基于 `LegacyServerInterop` 的虚拟方法实现
- 所有方法都转发到 `LegacyServerInterop`
- 可以被继承和重写

### 3. ServerMain
- 使用 `FrameworkServerExports` 作为默认实现
- 支持通过配置加载自定义实现

## 使用模式

### 1. 直接使用 LegacyServerInterop

```csharp
// 初始化
LegacyServerInterop.Initialize(pengfuncsFromEngine, pGlobals);

// 直接调用
LegacyServerInterop.GameInit();
LegacyServerInterop.Spawn(pEntity);
```

### 2. 使用 FrameworkServerExports

```csharp
var framework = new FrameworkServerExports();
framework.GameInit();  // 内部调用 LegacyServerInterop.GameInit()
```

### 3. 自定义实现

```csharp
public class CustomServer : FrameworkServerExports
{
    public override void GameInit()
    {
        Console.WriteLine("自定义初始化");
        base.GameInit();  // 调用 Legacy 实现
        Console.WriteLine("初始化完成");
    }
}
```

## 开发模式

### 1. 完全 Legacy 模式
直接使用原版 hl.dll 的所有功能，无任何修改。

### 2. 增强模式
在原版功能基础上添加额外的自定义逻辑。

```csharp
public override qboolean ClientConnect(edict_t* pEntity, sbyte* name, sbyte* address, sbyte* reason)
{
    // 自定义验证
    if (!ValidateClient(name))
        return new qboolean { Value = 0 };
    
    // 调用原版逻辑
    return base.ClientConnect(pEntity, name, address, reason);
}
```

### 3. 混合模式
根据条件选择使用原版或自定义实现。

```csharp
public override void Think(edict_t* pent)
{
    if (IsCustomEntity(pent))
        CustomThink(pent);
    else
        base.Think(pent);  // 使用 Legacy
}
```

### 4. 渐进迁移模式
逐步将功能从 Legacy 迁移到自定义实现。

```csharp
public override int Spawn(edict_t* pent)
{
    if (ShouldUseNewSpawnLogic(pent))
        return NewSpawnImplementation(pent);
    else
        return base.Spawn(pent);  // 暂时使用 Legacy
}
```

## 优势

### 1. 完全兼容性
- 与原版 hl.dll 100% 兼容
- 保证所有原版功能正常工作
- 无需担心行为差异

### 2. 灵活性
- 可以选择性地重写特定功能
- 支持多种开发模式
- 易于调试和测试

### 3. 渐进式开发
- 可以从完全 Legacy 开始
- 逐步添加自定义功能
- 降低开发风险

### 4. 性能
- 直接调用原版实现，性能最优
- 最小的额外开销
- 保持原版的执行效率

## 初始化流程

```csharp
// 1. ServerMain 接收引擎调用
ServerMain.GiveFnptrsToDll(pengfuncs, globals);

// 2. ServerMain 初始化 LegacyServerInterop
LegacyServerInterop.Initialize(pengfuncs, globals);

// 3. ServerMain 创建服务端实例
s_server = new FrameworkServerExports(); // 或自定义实现

// 4. 所有后续调用都通过这个架构流转
```

## 最佳实践

### 1. 初始化顺序
始终先初始化 `LegacyServerInterop`，再创建其他实例。

### 2. 错误处理
利用 `LegacyServerInterop` 的自动异常处理机制。

### 3. 性能考虑
对于性能敏感的方法，考虑直接使用 `LegacyServerInterop`。

### 4. 调试
可以在自定义实现中添加日志，同时保持 Legacy 行为。

## 示例代码

完整的使用示例请参考：
- `LegacyBasedExample.cs` - 基本使用模式
- `ServerMain.cs` - 集成示例
- `FrameworkServerExports.cs` - 实现参考

## 注意事项

1. **初始化依赖**: `FrameworkServerExports` 依赖 `LegacyServerInterop` 的初始化
2. **异常处理**: 未初始化时会抛出 `InvalidOperationException`
3. **线程安全**: 当前实现不是线程安全的
4. **内存管理**: 由 `LegacyServerInterop` 自动管理

这个架构提供了最大的灵活性和兼容性，是 GoldsrcFramework 的核心设计。
