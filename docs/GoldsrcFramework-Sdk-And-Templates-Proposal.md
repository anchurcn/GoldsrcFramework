# GoldsrcFramework SDK and Templates Proposal

## Goal

GoldsrcFramework should provide an SDK-style development experience for GoldSrc mod projects.

The SDK owns build-time behavior that is specific to GoldsrcFramework: project defaults, entity export generation, loader build, deployment, launch support, and diagnostics. Mod projects should stay small and focused on game code.

The preferred external consumption model is one primary NuGet package:

```text
GoldsrcFramework
```

This package should contain the runtime framework assemblies, engine bindings, SDK build assets, loader assets, and prebuilt build tools required by the SDK pipeline.

Project templates may be distributed separately:

```text
GoldsrcFramework.Templates
```

## Target Project Experience

An external mod project should be small:

```xml
<Project Sdk="GoldsrcFramework.Sdk/1.0.0">

  <PropertyGroup>
    <GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
    <GoldsrcModDisplayName>My Mod</GoldsrcModDisplayName>
  </PropertyGroup>

</Project>
```

The `GoldsrcFramework.Sdk` package should add the matching `GoldsrcFramework` runtime package reference, so consumers do not need to write the package reference by hand.

The Demo project is different from external consumers. It should use the same SDK pipeline, but keep source `ProjectReference` items for fast framework development:

```xml
<Import Project="..\GoldsrcFramework.Sdk\build\GoldsrcFramework.Sdk.props" />

<ItemGroup>
  <ProjectReference Include="..\GoldsrcFramework\GoldsrcFramework.csproj" />
  <ProjectReference Include="..\GoldsrcFramework.Engine\GoldsrcFramework.Engine.csproj" />
  <ProjectReference Include="..\GoldsrcFramework.Ecs\GoldsrcFramework.Ecs.csproj" />
</ItemGroup>

<Import Project="..\GoldsrcFramework.Sdk\build\GoldsrcFramework.Sdk.targets" />
```

## SDK Source Layout

SDK-owned MSBuild files should live under the SDK project:

```text
src/
  GoldsrcFramework.Sdk/
    GoldsrcFramework.Sdk.csproj

    Sdk/
      Sdk.props
      Sdk.targets

    build/
      GoldsrcFramework.Sdk.props
      GoldsrcFramework.Sdk.targets

    targets/
      ModBuild.targets
      NetLoaderBuild.targets
      CppLoaderBuild.targets
      Deploy.targets
      Launch.targets
      Diagnostics.targets

    tools/
      GoldsrcFramework.BuildTool/
        GoldsrcFramework.BuildTool.dll
        GoldsrcFramework.BuildTool.deps.json
        GoldsrcFramework.BuildTool.runtimeconfig.json
```

The `targets` directory contains feature-specific MSBuild logic. The `tools` directory contains packaged build-time tools used by external consumers.

## SDK Activation

`GoldsrcFramework.Sdk.props` should set:

```xml
<UsingGoldsrcFrameworkSdk>true</UsingGoldsrcFrameworkSdk>
```

Targets should use this marker when applying GoldsrcFramework-specific behavior.

The SDK should also expose a mode property:

```xml
<GoldsrcFrameworkUseSourceProjectReferences>true</GoldsrcFrameworkUseSourceProjectReferences>
```

Recommended behavior:

- Demo/source mode sets `GoldsrcFrameworkUseSourceProjectReferences=true`.
- Package mode sets `GoldsrcFrameworkUseSourceProjectReferences=false`.

This property controls whether framework assets come from source projects or from the `GoldsrcFramework` package.

## Source Mode vs Package Mode

The SDK must support two dependency-resolution modes.

In source mode:

- Framework assemblies come from `ProjectReference` outputs.
- `GoldsrcFramework.BuildTool` is built from source.
- Loader source/assets may come from repository projects.
- This mode is used by `GoldsrcFramework.Demo`.

In package mode:

- Framework assemblies come from the `GoldsrcFramework` package.
- `GoldsrcFramework.BuildTool` comes from prebuilt packaged files.
- Loader assets come from packaged SDK assets.
- Repository-relative source paths must not be required.

## SDK Responsibilities

The SDK should provide default project settings:

```xml
<TargetFramework>net10.0</TargetFramework>
<RuntimeIdentifier>win-x86</RuntimeIdentifier>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
<DisableFastUpToDateCheck>true</DisableFastUpToDateCheck>
```

These defaults should be overridable by consuming projects.

The SDK should own these build features:

1. Entity export generation.
2. BuildTool resolution for source mode and package mode.
3. NetLoader build.
4. Optional CppLoader build.
5. Deployment to a configured mod directory.
6. Launch/F5 support.
7. Diagnostics for missing paths, missing tools, missing loader outputs, and unsupported configurations.

The selected loader should be controlled by:

```xml
<GoldsrcFrameworkLoaderKind>NetLoader</GoldsrcFrameworkLoaderKind>
```

Supported values:

```text
NetLoader
CppLoader
None
```

Deployment should be available as an explicit target:

```powershell
dotnet build /t:Deploy
```

Deploy-on-build can be enabled with:

```xml
<GoldsrcFrameworkDeployOnBuild>true</GoldsrcFrameworkDeployOnBuild>
```

## Local Configuration

Machine-specific paths should not be committed.

The SDK should support local configuration through files, environment variables, or command-line properties:

```xml
<HalfLifeInstallDir>G:\Games\Half-Life</HalfLifeInstallDir>
<GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
<GoldsrcEngineExe>hl.exe</GoldsrcEngineExe>
<GoldsrcLaunchArguments>-game mymod -dev -console</GoldsrcLaunchArguments>
```

Recommended environment-variable fallbacks:

```text
HALFLIFE_INSTALL_DIR
GOLDSRCFRAMEWORK_HALFLIFE_DIR
```

Build should still work without a Half-Life path. Deploy and launch targets should fail with clear diagnostics when required local paths are missing.

## Template Package

`GoldsrcFramework.Templates` should provide:

```powershell
dotnet new goldsrcmod
```

The generated project should reference only:

```xml
<Project Sdk="GoldsrcFramework.Sdk/...">
  <PropertyGroup>
    <GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
  </PropertyGroup>
</Project>
```

The template should not generate repository-specific `ProjectReference` items.

Recommended template parameters:

```text
--mod-name
--display-name
--target-framework
--loader
--client
--server
--ecs
--legacy-interop
```

## Success Criteria

1. `GoldsrcFramework.Demo` uses SDK props and targets while keeping source project references.
2. External projects can build with only a `PackageReference` to `GoldsrcFramework`.
3. Package mode uses packaged BuildTool binaries instead of source builds.
4. SDK targets can generate entity exports and build the selected loader.
5. SDK targets can deploy to a configured mod directory.
6. Visual Studio F5 can launch the configured engine with the mod loaded.
7. Missing local configuration produces clear MSBuild diagnostics.
