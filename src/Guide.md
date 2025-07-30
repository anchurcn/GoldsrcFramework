# Generate Definations in C#
从 HLSDK 的 C/CPP 代码中生成金源引擎与Mod dll 交互的函数和类型定义。
首先列出这些函数，然后递归的从函数签名中收集依赖的类型，将这些类型

# GoldsrcFramework 项目结构规划

## 整体架构设计

参考 Stride3D 和 Unity 的设计模式，GoldsrcFramework 采用分层架构：

```
GoldsrcFramework/
├── Core/                    # 核心基础设施 - 暂时不用
├── Engine/                  # 引擎接口层 - 基本上都是从 HLSDK 生成的比较贴合引擎的类型定义
├── Graphics/                # 图形渲染
├── Audio/                   # 音频系统
├── Input/                   # 输入处理
├── Physics/                 # 物理系统
├── Scripting/               # 脚本系统
├── Assets/                  # 资源管理
└── Tools/                   # 开发工具 - 目前仅有 CodeGen
```

## 命名空间规划

### 1. 核心命名空间
```csharp
GoldsrcFramework.Core
├── .Annotations            # 特性和标注
├── .Collections            # 集合类型
├── .Diagnostics            # 诊断和日志
├── .IO                     # 文件和流操作
├── .Mathematics            # 数学库
├── .Memory                 # 内存管理
├── .Serialization          # 序列化
└── .Threading              # 线程和异步
```

### 2. 引擎接口层
```csharp
GoldsrcFramework.Engine
├── .Native                 # P/Invoke 绑定和原生结构体
│   ├── .Client.cs            # 客户端 DLL 接口
│   ├── .Server.cs            # 服务端 DLL 接口
│   ├── .Engine.cs            # 引擎接口
│   └── .Common.cs            # 通用定义
├── .Graphics              # 图形 API 封装
├── .Audio                 # 音频 API 封装
├── .Input                 # 输入 API 封装
└── .Annotations               # 特性和标注

## CodeGen 生成目标

### 1. 原生接口绑定 (GoldsrcFramework.Engine.Native)

#### 客户端接口
```csharp
namespace GoldsrcFramework.Engine.Native.Client
{
    // 从 cl_dll/cdll_int.h 生成
    public static class ClientDll
    {
        [DllImport("client.dll")]
        public static extern int Initialize(IntPtr pEnginefuncs, int iVersion);
        
        [DllImport("client.dll")]
        public static extern void HUD_Init();
        // ... 其他导出函数
    }
    
    // 从 engine/cdll_int.h 生成
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct cl_enginefuncs_t
    {
        public delegate* unmanaged[Cdecl]<int, void> pfnSPR_Load;
        public delegate* unmanaged[Cdecl]<int, int, int, int, void> pfnSPR_Set;
        // ... 其他函数指针
    }
}
```

#### 服务端接口
```csharp
namespace GoldsrcFramework.Engine.Native.Server
{
    // 从 dlls/enginecallback.h 生成
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct enginefuncs_t
    {
        public delegate* unmanaged[Cdecl]<int, int> pfnPrecacheModel;
        public delegate* unmanaged[Cdecl]<int, int> pfnPrecacheSound;
        // ... 其他函数指针
    }
    
    // 从 dlls/extdll.h 生成
    public static class ServerDll
    {
        [DllImport("hl.dll")]
        public static extern void GameDLLInit();
        // ... 其他导出函数
    }
}
```

#### 通用定义
```csharp
namespace GoldsrcFramework.Engine.Native.Common
{
    // 从 common/mathlib.h 生成
    [StructLayout(LayoutKind.Sequential)]
    public struct vec3_t
    {
        public float x, y, z;
    }
    
    // 从 common/const.h 生成
    public static class Constants
    {
        public const int MAX_PLAYERS = 32;
        public const int MAX_WEAPONS = 32;
        // ... 其他常量
    }
    
    // 从 common/triangleapi.h 生成
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct triangleapi_t
    {
        public delegate* unmanaged[Cdecl]<int, void> RenderMode;
        // ... 其他函数指针
    }
}
```

### 2. 高级封装 (GoldsrcFramework.Engine)

```csharp
namespace GoldsrcFramework.Engine.Graphics
{
    public class Renderer
    {
        private readonly triangleapi_t* _triangleApi;
        
        public void SetRenderMode(RenderMode mode) 
        {
            _triangleApi->RenderMode((int)mode);
        }
    }
    
    public enum RenderMode
    {
        Normal = 0,
        Additive = 1,
        // ...
    }
}

namespace GoldsrcFramework.Engine.Audio
{
    public class AudioEngine
    {
        public void PlaySound(string soundName, Vector3 position) { }
        public void SetListenerPosition(Vector3 position) { }
    }
}
```

### 3. 游戏框架 (GoldsrcFramework.Game)

```csharp
namespace GoldsrcFramework.Game.Entities
{
    public abstract class BaseEntity
    {
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public string ClassName { get; protected set; }
        
        public virtual void Spawn() { }
        public virtual void Think() { }
        public virtual void Touch(BaseEntity other) { }
    }
    
    public class PlayerEntity : BaseEntity
    {
        public string Name { get; set; }
        public int Health { get; set; }
        public int Armor { get; set; }
    }
}

namespace GoldsrcFramework.Game.Components
{
    public interface IComponent
    {
        BaseEntity Owner { get; }
        void Update(float deltaTime);
    }
    
    public class HealthComponent : IComponent
    {
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public BaseEntity Owner { get; private set; }
        
        public void TakeDamage(int amount) { }
        public void Update(float deltaTime) { }
    }
}
```

## CodeGen 配置

### 类型映射规则
```csharp
// 在 CustomTypeMapping.txt 中定义
vec3_t -> Vector3
edict_t* -> EntityHandle
BOOL -> bool
byte -> byte
int -> int
float -> float
char* -> string (with marshaling)
void* -> IntPtr
```

### 生成规则
1. **结构体**: 保持原始内存布局，添加 `[StructLayout(LayoutKind.Sequential)]`
2. **函数指针**: 转换为 `delegate* unmanaged[Cdecl]`
3. **常量**: 生成为 `const` 字段
4. **枚举**: 保持原始值，添加类型安全
5. **回调函数**: 生成对应的委托类型

### 文件组织
```
GoldsrcFramework.Engine/
├── Native/
│   ├── Client/
│   │   ├── ClientDll.cs          # 客户端导出函数
│   │   ├── ClientEngineFuncs.cs  # 引擎回调函数
│   │   └── ClientStructures.cs   # 客户端结构体
│   ├── Server/
│   │   ├── ServerDll.cs          # 服务端导出函数
│   │   ├── ServerEngineFuncs.cs  # 引擎回调函数
│   │   └── ServerStructures.cs   # 服务端结构体
│   └── Common/
│       ├── Constants.cs          # 常量定义
│       ├── Enums.cs             # 枚举定义
│       ├── Structures.cs        # 通用结构体
│       └── Types.cs             # 基础类型定义
├── Graphics/
├── Audio/
├── Input/
└── Network/
```

这个结构既保持了与原始 HLSDK 的对应关系，又提供了现代化的 C# 开发体验。