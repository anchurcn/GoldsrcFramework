# ModPostBuild.targets

这个 MSBuild targets 文件实现了 GoldsrcFramework Mod 项目构建后的完整流程，用于生成和编译 loader.dll。

## 功能概述

ModPostBuild.targets 实现了以下构建后处理步骤：

1. **生成 entity_exports.cpp**: 使用 GoldsrcFramework.BuildTool 从 GoldsrcFramework.dll 中提取实体列表并生成 C++ 导出代码
2. **复制源文件**: 将 loader.cpp 和 dotnet_hosting.props 复制到 LoaderSourceDir
3. **编译 loader.dll**: 使用 MSVC 工具链编译生成 x86 架构的 loader.dll

## 使用方法

### 在 Mod 项目中集成

在你的 Mod 项目的 `.csproj` 文件中添加以下内容：

```xml
<PropertyGroup>
  <!-- 定义 LoaderSourceDir（可选，有默认值） -->
  <LoaderSourceDir>$(IntermediateOutputPath)LoaderSource\</LoaderSourceDir>
</PropertyGroup>

<!-- 导入 ModPostBuild.targets -->
<Import Project="..\ModPostBuild.targets" />
```

### 目录结构要求

确保你的项目结构如下：

```
src/
├── YourMod/
│   └── YourMod.csproj          # 你的 Mod 项目
├── ModPostBuild.targets        # 这个 targets 文件
├── GoldsrcFramework.BuildTool/ # BuildTool 项目
└── GoldsrcFramework.Loader/    # Loader 源码
    ├── loader.cpp
    └── dotnet_hosting.props
```

## 配置属性

### 可配置的属性

- `LoaderSourceDir`: 源文件复制和编译的目录（默认：`$(IntermediateOutputPath)LoaderSource\`）

### 自动检测的属性

- `BuildToolProjectPath`: BuildTool 项目路径
- `BuildToolExePath`: BuildTool 可执行文件路径
- `VCInstallDir`: Visual Studio C++ 工具链安装目录
- `VCToolsVersion`: MSVC 工具版本
- `AppHostDir`: .NET Host 库目录（从 dotnet_hosting.props 读取）

## 生成的文件

构建成功后，会在以下位置生成文件：

- `$(LoaderSourceDir)entity_exports.cpp`: 生成的实体导出代码
- `$(LoaderSourceDir)loader.cpp`: 复制的 loader 源码
- `$(LoaderSourceDir)dotnet_hosting.props`: 复制的 .NET hosting 配置
- `$(OutDir)loader.dll`: 编译生成的 loader 动态库（x86）
- `$(OutDir)nethost.dll`: 复制的 .NET host 库

## 构建目标

### GenerateEntityExports
- **触发时机**: Build 之后
- **功能**: 调用 BuildTool 生成 entity_exports.cpp
- **输入**: GoldsrcFramework.dll
- **输出**: entity_exports.cpp

### CopyLoaderSource
- **触发时机**: GenerateEntityExports 之后
- **功能**: 复制 loader.cpp 和 dotnet_hosting.props
- **输入**: 源文件
- **输出**: 复制到 LoaderSourceDir 的文件

### CompileLoaderDll
- **触发时机**: CopyLoaderSource 之后
- **功能**: 使用 MSVC 编译 loader.dll
- **输入**: loader.cpp, entity_exports.cpp
- **输出**: loader.dll (x86)

### ModPostBuild
- **触发时机**: Build 之后
- **功能**: 协调整个构建流程
- **依赖**: GenerateEntityExports, CopyLoaderSource, CompileLoaderDll

## 系统要求

- .NET 8.0 SDK
- Visual Studio 2022 (Community/Professional/Enterprise)
- MSVC v143 工具集
- Windows SDK

## 故障排除

### 常见错误

1. **"MSVC compiler not found"**
   - 确保安装了 Visual Studio 2022 和 C++ 工具集
   - 检查 VCInstallDir 路径是否正确

2. **"AppHostDir not defined"**
   - 确保 dotnet_hosting.props 文件存在且配置正确
   - 检查 AppHostDir 路径指向正确的 .NET Host 库目录

3. **"GoldsrcFramework.dll not found"**
   - 确保 GoldsrcFramework 项目已正确构建
   - 检查输出目录中是否存在 GoldsrcFramework.dll

### 调试技巧

1. 使用 MSBuild 详细输出查看构建过程：
   ```bash
   dotnet build -v detailed
   ```

2. 检查生成的中间文件：
   ```bash
   # 查看 LoaderSourceDir 中的文件
   ls $(IntermediateOutputPath)LoaderSource/
   ```

## 自定义和扩展

### 禁用特定步骤

如果只需要生成 entity_exports.cpp 而不编译 loader.dll，可以设置：

```xml
<PropertyGroup>
  <SkipLoaderCompilation>true</SkipLoaderCompilation>
</PropertyGroup>
```

### 自定义编译选项

可以通过重写属性来自定义编译选项：

```xml
<PropertyGroup>
  <CompileFlags>/c /EHsc /nologo /MD /DWIN32 /D_WINDOWS /DCUSTOM_DEFINE</CompileFlags>
</PropertyGroup>
```

## 示例输出

成功构建时的输出示例：

```
Building GoldsrcFramework.BuildTool...
Generating entity_exports.cpp using BuildTool...
Found 249 entities
Copying loader source files to obj\Debug\net8.0\LoaderSource\...
Compiling loader.dll using MSVC toolchain (x86)...
ModPostBuild completed successfully!
Generated files:
  - obj\Debug\net8.0\LoaderSource\entity_exports.cpp
  - bin\Debug\net8.0\loader.dll
```
