# Legacy-Based Client Framework 架构文档

## 概述

GoldsrcFramework 的客户端现在也采用基于 Legacy 的架构，所有 Client Framework 的导出实现都基于 `LegacyClientInterop`，确保与原版 client.dll 的完全兼容性。

## 架构层次

```
┌─────────────────────────────────────┐
│          用户自定义实现              │
│    (继承 FrameworkClientExports)    │
├─────────────────────────────────────┤
│       FrameworkClientExports       │
│      (基于 LegacyClientInterop)     │
├─────────────────────────────────────┤
│       LegacyClientInterop          │
│        (静态转发方法)               │
├─────────────────────────────────────┤
│         原版 client.dll            │
│      (unmanaged 实现)              │
└─────────────────────────────────────┘
```

## 核心组件

### 1. LegacyClientInterop
- 提供与原版 client.dll 的直接交互
- 所有 `IClientExportFuncs` 方法的静态转发实现
- 自动异常处理和初始化检查

### 2. FrameworkClientExports
- 基于 `LegacyClientInterop` 的虚拟方法实现
- 所有方法都转发到 `LegacyClientInterop`
- 可以被继承和重写

### 3. ClientMain
- 使用 `FrameworkClientExports` 作为默认实现
- 支持通过配置加载自定义实现

## 主要功能分类

### HUD 相关
- `HUD_Init()` - HUD 初始化
- `HUD_VidInit()` - 视频模式初始化
- `HUD_Redraw()` - HUD 重绘
- `HUD_Reset()` - HUD 重置
- `HUD_Frame()` - 帧处理

### 输入处理
- `IN_ActivateMouse()` - 激活鼠标
- `IN_DeactivateMouse()` - 停用鼠标
- `IN_MouseEvent()` - 鼠标事件
- `IN_ClearStates()` - 清除输入状态
- `IN_Accumulate()` - 累积输入

### 玩家移动
- `HUD_PlayerMove()` - 玩家移动处理
- `HUD_PlayerMoveInit()` - 移动初始化
- `CL_CreateMove()` - 创建移动命令

### 渲染相关
- `HUD_DrawNormalTriangles()` - 绘制普通三角形
- `HUD_DrawTransparentTriangles()` - 绘制透明三角形
- `HUD_AddEntity()` - 添加实体到渲染列表
- `V_CalcRefdef()` - 计算视图参数

### 摄像机和视图
- `CAM_Think()` - 摄像机思考
- `CL_IsThirdPerson()` - 检查第三人称视角
- `CL_GetCameraOffsets()` - 获取摄像机偏移

## 使用模式

### 1. 直接使用 LegacyClientInterop

```csharp
// 初始化
LegacyClientInterop.InitializeClient(pEnginefuncs, iVersion);

// 直接调用
LegacyClientInterop.HUD_Init();
LegacyClientInterop.HUD_VidInit();
```

### 2. 使用 FrameworkClientExports

```csharp
var framework = new FrameworkClientExports();
framework.HUD_Init();  // 内部调用 LegacyClientInterop.HUD_Init()
```

### 3. 自定义实现

```csharp
public class CustomClient : FrameworkClientExports
{
    public override void HUD_Init()
    {
        Console.WriteLine("自定义 HUD 初始化");
        base.HUD_Init();  // 调用 Legacy 实现
        Console.WriteLine("HUD 初始化完成");
    }
}
```

## 开发模式

### 1. 完全 Legacy 模式
直接使用原版 client.dll 的所有功能，无任何修改。

### 2. HUD 增强模式
在原版 HUD 基础上添加额外的界面元素。

```csharp
public override int HUD_Redraw(float flTime, int intermission)
{
    // 先调用原版 HUD 绘制
    int result = base.HUD_Redraw(flTime, intermission);
    
    // 添加自定义 HUD 元素
    DrawCustomHUD();
    
    return result;
}
```

### 3. 输入增强模式
在原版输入处理基础上添加自定义功能。

```csharp
public override int HUD_Key_Event(int eventcode, int keynum, sbyte* binding)
{
    // 处理自定义按键
    if (HandleCustomKeys(eventcode, keynum))
        return 1;
    
    // 交给原版处理
    return base.HUD_Key_Event(eventcode, keynum, binding);
}
```

### 4. 混合渲染模式
根据条件选择使用原版或自定义渲染。

```csharp
public override int HUD_Redraw(float flTime, int intermission)
{
    if (UseCustomRendering())
        return CustomRender(flTime, intermission);
    else
        return base.HUD_Redraw(flTime, intermission);
}
```

## 优势

### 1. 完全兼容性
- 与原版 client.dll 100% 兼容
- 保证所有原版客户端功能正常工作
- 无需担心 HUD 或输入行为差异

### 2. 灵活的自定义
- 可以选择性地重写 HUD 功能
- 支持输入处理增强
- 易于添加新的界面元素

### 3. 渐进式开发
- 可以从完全 Legacy 开始
- 逐步添加自定义 HUD 和功能
- 降低客户端开发风险

### 4. 调试友好
- 保持原版行为便于问题定位
- 可以轻松切换 Legacy/自定义模式
- 支持混合调试

## 初始化流程

```csharp
// 1. ClientMain 接收引擎调用
ClientMain.Initialize(pEnginefuncs, iVersion);

// 2. ClientMain 初始化 LegacyClientInterop
LegacyClientInterop.InitializeClient(pEnginefuncs, iVersion);

// 3. ClientMain 创建客户端实例
s_client = new FrameworkClientExports(); // 或自定义实现

// 4. 所有后续调用都通过这个架构流转
```

## 最佳实践

### 1. 初始化顺序
始终先初始化 `LegacyClientInterop`，再创建其他实例。

### 2. HUD 开发
对于 HUD 修改，建议先调用原版实现，再添加自定义元素。

### 3. 输入处理
对于输入增强，建议先处理自定义逻辑，再交给原版处理。

### 4. 性能考虑
对于性能敏感的渲染代码，考虑直接使用 `LegacyClientInterop`。

## 示例代码

完整的使用示例请参考：
- `LegacyClientExample.cs` - 基本使用模式
- `ClientMain.cs` - 集成示例
- `FrameworkClientExports.cs` - 实现参考

## 注意事项

1. **初始化依赖**: `FrameworkClientExports` 依赖 `LegacyClientInterop` 的初始化
2. **异常处理**: 未初始化时会抛出 `InvalidOperationException`
3. **线程安全**: 当前实现不是线程安全的
4. **内存管理**: 由 `LegacyClientInterop` 自动管理

这个架构为客户端开发提供了最大的灵活性和兼容性，是 GoldsrcFramework 客户端的核心设计。
