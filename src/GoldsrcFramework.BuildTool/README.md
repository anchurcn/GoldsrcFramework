# GoldsrcFramework.BuildTool

GoldsrcFramework.BuildTool 是一个命令行工具，用于生成 `entity_exports.cpp` 文件。该工具通过调用 `EntityContext.GetEntityList` 方法获取实体列表，然后根据 Loader 项目的 `entity_exports_demo.cpp` 样式生成相应的 C++ 代码。

## 功能

- 动态加载 GoldsrcFramework.dll
- 调用 EntityContext.GetEntityList 方法获取实体列表
- 生成符合 entity_exports_demo.cpp 格式的 C++ 代码
- 支持命令行参数传入 DLL 路径和输出路径

## 使用方法

### 基本语法

```bash
# 使用默认设置（推荐）
GoldsrcFramework.BuildTool.exe

# 指定输出文件
GoldsrcFramework.BuildTool.exe [output-path]

# 指定 DLL 路径和输出文件
GoldsrcFramework.BuildTool.exe <dll-path> <output-path>
```

### 参数说明

- `dll-path`: GoldsrcFramework.dll 的路径（可选，默认为当前目录的 GoldsrcFramework.dll）
- `output-path`: 生成的 entity_exports.cpp 文件的目标路径（可选，默认为 entity_exports.cpp）

### 使用示例

```bash
# 最简单的使用方式 - 使用默认设置
GoldsrcFramework.BuildTool.exe
# 从当前目录的 GoldsrcFramework.dll 生成 entity_exports.cpp

# 指定输出文件名
GoldsrcFramework.BuildTool.exe my_entity_exports.cpp
# 从当前目录的 GoldsrcFramework.dll 生成 my_entity_exports.cpp

# 指定 DLL 路径和输出文件
GoldsrcFramework.BuildTool.exe "C:\MyMod\bin\GoldsrcFramework.dll" "C:\MyMod\src\entity_exports.cpp"

# 在构建脚本中使用（推荐方式）
dotnet run --project src/GoldsrcFramework.BuildTool/GoldsrcFramework.BuildTool.csproj
```

## 生成的文件格式

生成的 `entity_exports.cpp` 文件包含以下内容：

1. **类型定义**: 定义了 `entvars_t` 和 `PrivateDataAllocatorFunc` 类型
2. **函数表结构**: `PrivateDataAllocFuncs` 结构体，包含所有实体的函数指针
3. **全局实例**: `g_allocFuncs` 全局变量
4. **初始化函数**: `InitializePrivateDataAllocators()` 函数，用于设置函数指针
5. **导出函数**: 每个实体对应的 `extern "C" __declspec(dllexport)` 函数

## 在 MSBuild 中使用

该工具设计用于在 `.targets` 文件中使用，可以作为构建过程的一部分自动生成 entity_exports.cpp 文件。

示例 MSBuild 任务：

```xml
<!-- 简单方式：使用默认设置 -->
<Target Name="GenerateEntityExports" BeforeTargets="Build">
  <Exec Command="$(OutputPath)GoldsrcFramework.BuildTool.exe" WorkingDirectory="$(OutputPath)" />
</Target>

<!-- 指定输出路径 -->
<Target Name="GenerateEntityExports" BeforeTargets="Build">
  <Exec Command="$(OutputPath)GoldsrcFramework.BuildTool.exe $(ProjectDir)entity_exports.cpp" WorkingDirectory="$(OutputPath)" />
</Target>

<!-- 完全指定路径（兼容旧版本） -->
<Target Name="GenerateEntityExports" BeforeTargets="Build">
  <Exec Command="$(OutputPath)GoldsrcFramework.BuildTool.exe $(OutputPath)GoldsrcFramework.dll $(ProjectDir)entity_exports.cpp" />
</Target>
```

## 错误处理

工具包含完整的错误处理：

- 检查命令行参数数量
- 验证 DLL 文件是否存在
- 验证程序集加载是否成功
- 验证 EntityContext 类型和 GetEntityList 方法是否存在
- 处理文件写入错误

## 构建要求

- .NET 8.0
- 对 GoldsrcFramework 项目的引用

## 输出示例

运行成功时的输出：

```bash
# 使用默认设置
$ GoldsrcFramework.BuildTool.exe
Loading assembly from: GoldsrcFramework.dll
Output file: entity_exports.cpp
Found 249 entities
Entity exports generated successfully!

# 指定输出文件
$ GoldsrcFramework.BuildTool.exe my_exports.cpp
Loading assembly from: GoldsrcFramework.dll
Output file: my_exports.cpp
Found 249 entities
Entity exports generated successfully!
```
