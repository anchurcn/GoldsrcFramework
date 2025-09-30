# GoldsrcFramework 文档

欢迎来到 GoldsrcFramework 文档中心！本目录包含了框架的完整技术文档。

## 📚 文档索引

### 核心文档

#### [Architecture.md](./Architecture.md)
**GoldsrcFramework 架构文档** - 最重要的技术文档

详细介绍了 GoldsrcFramework 的整体架构设计，包括：
- 核心设计理念
- 项目结构和层次划分
- 各个组件的职责和交互
- 初始化流程详解
- 运行时调用机制
- 实体系统设计
- 关键技术点说明
- 完整的函数调用链

**适合**: 想要深入了解框架内部工作原理的开发者

---

#### [Architecture-Diagrams.md](./Architecture-Diagrams.md)
**架构图集** - 可视化架构文档

包含 7 个 Mermaid 图表，直观展示：
1. 整体架构图 - 组件关系
2. 服务端初始化流程 - 时序图
3. 运行时函数调用流程 - 时序图
4. 实体私有数据分配流程 - 时序图
5. 类型系统和接口层次结构 - 类图
6. 构建和部署流程 - 流程图
7. 数据流和内存管理 - 数据流图

**适合**: 喜欢通过图表理解系统的开发者

---

#### [Quick-Reference.md](./Quick-Reference.md)
**快速参考手册** - 实用开发指南

提供快速查阅的参考信息：
- 核心接口定义 (IServerExportFuncs, IClientExportFuncs)
- 关键类和方法说明
- 常用代码模式和示例
- 调试技巧
- 常见问题解答

**适合**: 正在开发游戏逻辑的开发者

---

### 遗留文档

#### [LegacyBasedArchitecture.md](./LegacyBasedArchitecture.md)
早期的架构设计文档，描述了基于原版 DLL 的架构方案。

#### [LegacyClientArchitecture.md](./LegacyClientArchitecture.md)
客户端架构的早期设计文档。

---

## 🚀 快速开始

### 新手入门路径

1. **了解项目** → 阅读 [主 README](../README.md)
2. **理解架构** → 阅读 [Architecture.md](./Architecture.md) 的"概述"和"架构层次"部分
3. **查看图表** → 浏览 [Architecture-Diagrams.md](./Architecture-Diagrams.md) 中的整体架构图
4. **动手实践** → 参考 [Quick-Reference.md](./Quick-Reference.md) 中的代码示例
5. **深入学习** → 完整阅读 [Architecture.md](./Architecture.md)

### 开发者路径

如果你想要：

- **开发游戏逻辑** → 直接查看 [Quick-Reference.md](./Quick-Reference.md)
- **理解框架原理** → 阅读 [Architecture.md](./Architecture.md)
- **修改框架代码** → 阅读 [Architecture.md](./Architecture.md) + [Architecture-Diagrams.md](./Architecture-Diagrams.md)
- **调试问题** → 查看 [Quick-Reference.md](./Quick-Reference.md) 的"调试技巧"部分

---

## 📖 文档结构说明

### Architecture.md 结构

```
Architecture.md
├── 概述
├── 核心设计理念
├── 项目结构
├── 架构层次
│   ├── 原生层 (Native Layer)
│   ├── 引擎接口层 (Engine Interface Layer)
│   ├── 框架层 (Framework Layer)
│   └── 游戏层 (Game Layer)
├── 实体系统
├── 代码生成
├── 构建流程
├── 初始化流程
│   ├── 服务端初始化
│   └── 客户端初始化
├── 运行时调用流程
├── 关键技术点
├── 扩展点
├── 性能考虑
├── 已知限制
├── 未来规划
├── 关键函数调用链 (详细)
└── 参考资料
```

### Architecture-Diagrams.md 结构

```
Architecture-Diagrams.md
├── 1. 整体架构图 (组件关系)
├── 2. 服务端初始化流程 (时序图)
├── 3. 运行时函数调用流程 (时序图)
├── 4. 实体私有数据分配流程 (时序图)
├── 5. 类型系统和接口层次结构 (类图)
├── 6. 构建和部署流程 (流程图)
└── 7. 数据流和内存管理 (数据流图)
```

### Quick-Reference.md 结构

```
Quick-Reference.md
├── 核心接口
│   ├── IServerExportFuncs
│   └── IClientExportFuncs
├── 关键类和方法
│   ├── FrameworkInterop
│   ├── ServerMain / ClientMain
│   └── LegacyServerInterop / LegacyClientInterop
├── 常用代码模式
│   ├── 创建自定义服务端实现
│   ├── 调用引擎函数
│   ├── 访问全局变量
│   ├── 操作实体
│   └── 创建自定义客户端实现
├── 调试技巧
│   ├── 启用控制台输出
│   ├── 附加调试器
│   ├── 条件断点
│   └── 日志记录
└── 常见问题
```

---

## 🔍 关键概念速查

### 四层架构

1. **原生层** (C++) - GoldsrcFramework.Loader
   - 加载 .NET 运行时
   - 导出引擎期望的函数
   
2. **引擎接口层** (C#) - GoldsrcFramework.Engine
   - 类型定义 (edict_t, entvars_t, Vector3f 等)
   - 接口定义 (IServerExportFuncs, IClientExportFuncs)
   
3. **框架层** (C#) - GoldsrcFramework
   - FrameworkInterop: 统一入口点
   - ServerMain/ClientMain: 管理类
   - LegacyInterop: 原版 DLL 桥接
   - FrameworkExports: 默认实现
   
4. **游戏层** (C#) - GoldsrcFramework.Demo 或自定义
   - 实现 IServerExportFuncs/IClientExportFuncs
   - 自定义游戏逻辑

### 关键流程

- **初始化**: Engine → Loader → .NET Runtime → FrameworkInterop → ServerMain/ClientMain → Game Assembly
- **运行时调用**: Engine → Function Table → Game Exports → [Optional: Legacy DLL]
- **引擎调用**: Game Code → enginefuncs_t → Engine
- **实体分配**: Engine → Entity Export → GetPrivateDataAllocator → Legacy DLL

### 核心技术

- **UnmanagedCallersOnly**: 允许原生代码直接调用托管函数
- **函数指针**: `delegate* unmanaged[Cdecl]<...>` 表示原生函数指针
- **动态加载**: 运行时加载游戏程序集
- **双向互操作**: 框架 ↔ 引擎, 框架 ↔ 原版 DLL

---

## 💡 使用建议

### 阅读顺序建议

**快速了解** (30 分钟):
1. 主 README.md
2. Architecture.md 的"概述"和"架构层次"
3. Architecture-Diagrams.md 的整体架构图

**深入理解** (2-3 小时):
1. Architecture.md 完整阅读
2. Architecture-Diagrams.md 所有图表
3. Quick-Reference.md 的代码示例

**实战开发** (边做边学):
1. Quick-Reference.md 作为手册
2. 参考 Example.cs 示例代码
3. 遇到问题查阅 Architecture.md

### 文档维护

这些文档应该随着代码的演进而更新。如果你发现：
- 文档与代码不一致
- 缺少重要信息
- 有错误或不清楚的地方

请提交 Issue 或 Pull Request！

---

## 🔗 相关资源

### 项目内资源

- [主 README](../README.md) - 项目介绍和使用说明
- [Guide.md](../src/Guide.md) - 项目结构规划
- [Example.cs](../src/GoldsrcFramework/Example.cs) - 完整示例代码
- [Engine README](../src/GoldsrcFramework.Engine/README.md) - 引擎接口层说明

### 外部资源

- [HLSDK](https://github.com/ValveSoftware/halflife) - Half-Life SDK
- [.NET Hosting](https://docs.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting) - .NET 托管文档
- [UnmanagedCallersOnly](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute) - 互操作特性
- [Function Pointers](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/function-pointers) - C# 函数指针

---

## 📝 文档版本

- **创建日期**: 2025-09-30
- **框架版本**: WIP (Work In Progress)
- **文档状态**: 初始版本

---

## 🤝 贡献

欢迎对文档进行改进！如果你想贡献：

1. Fork 项目
2. 创建文档分支
3. 修改或添加文档
4. 提交 Pull Request

文档贡献同样重要！

---

**Happy Coding with GoldsrcFramework! 🎮**

