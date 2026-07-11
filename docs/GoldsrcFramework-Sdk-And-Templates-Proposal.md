# GoldsrcFramework SDK and Project Templates Proposal

## Summary

This proposal describes how GoldsrcFramework should evolve from a repository-oriented demo project into a reusable .NET SDK and project-template based development experience for GoldSrc mod developers.

The desired outcome is that a mod developer can create a new project from a template, configure the Half-Life installation directory once, press F5 in Visual Studio, and have the project build, deploy, launch, and debug with minimal manual setup.

This document focuses on product and engineering requirements, package layout, project-template behavior, and migration strategy. It intentionally avoids low-level implementation details such as exact hostfxr calls, loader internals, or final NuGet publishing automation.

## Background

GoldsrcFramework currently contains framework libraries, engine bindings, build tools, loader experiments, and a demo project in one repository. The Demo project is useful for framework development, but the current shape is still repository-centric:

- The Demo project directly references framework source projects.
- The Demo project manually imports MSBuild targets from `src/targets`.
- Post-build behavior is specific to the repository layout.
- Local game installation paths are configured through local props files and ad hoc project settings.
- A new mod project cannot yet be created as a clean external consumer project.

This is acceptable for early framework development, but it is not the right long-term developer experience for mod authors. The build and debug workflow should become a first-class part of GoldsrcFramework itself.

## Goals

The SDK and template work should achieve the following goals:

1. Provide a standard .NET project experience for GoldSrc mod development.
2. Move GoldsrcFramework-specific build behavior into a dedicated SDK package.
3. Provide project templates for creating new mod projects.
4. Support both framework-development mode and external-mod-development mode.
5. Allow Visual Studio F5 debugging after minimal configuration.
6. Keep machine-specific Half-Life paths out of source control.
7. Make deployment to a GoldSrc or Xash3D mod directory predictable and diagnosable.
8. Prepare the project for future NuGet-based distribution.


## Target Developer Experience

For an external mod developer, the desired workflow should eventually look like this:

```powershell
dotnet new install GoldsrcFramework.Templates
dotnet new goldsrcmod -n MyMod
cd MyMod
```

Then the developer configures the Half-Life installation directory using a local file, environment variable, Visual Studio property page, or command-line property.

The generated project should be small and recognizable:

```xml
<Project Sdk="GoldsrcFramework.Sdk/1.0.0">
  <PropertyGroup>
    <GoldsrcModName>mymod</GoldsrcModName>
  </PropertyGroup>
</Project>
```

In Visual Studio, pressing F5 should:

1. Build the mod project.
2. Generate required loader/export artifacts.
3. Deploy outputs to the configured mod directory.
4. Launch `hl.exe` or a configured compatible engine executable.
5. Attach the managed debugger where possible.

## Two Usage Modes

The SDK must support two distinct modes.

### Source Development Mode

This mode is used inside the GoldsrcFramework repository by framework developers.

In this mode, mod projects such as `GoldsrcFramework.Demo` should reference framework source projects directly:

```text
GoldsrcFramework.Demo
  -> GoldsrcFramework
  -> GoldsrcFramework.Engine
  -> GoldsrcFramework.Ecs
```

This allows changes to framework code to be immediately visible to the Demo project. It is the right mode for framework development, debugging, and regression testing.

The SDK should enable this mode when it detects that it is running inside the source repository, or when explicitly configured:

```xml
<GoldsrcFrameworkUseSourceProjectReferences>true</GoldsrcFrameworkUseSourceProjectReferences>
```

### Package Consumption Mode

This mode is used by external mod projects.

In this mode, the SDK must not reference local framework source projects. It should consume released framework packages or assemblies:

```xml
<GoldsrcFrameworkUseSourceProjectReferences>false</GoldsrcFrameworkUseSourceProjectReferences>
```

The SDK can then provide references using one of two packaging strategies:

1. Reference separate NuGet packages such as `GoldsrcFramework`, `GoldsrcFramework.Engine`, and `GoldsrcFramework.Ecs`.
2. Include framework assemblies and tools inside the SDK package itself.

The recommended long-term approach is separate packages, because it keeps SDK behavior, runtime assemblies, and tools versioned clearly.

## Package Strategy

The preferred long-term package layout is:

```text
GoldsrcFramework
  Runtime/framework APIs used by mod projects.

GoldsrcFramework.Engine
  Generated and hand-written GoldSrc engine bindings.

GoldsrcFramework.Ecs
  Optional ECS layer.

GoldsrcFramework.BuildTool
  Entity export generation and build-time tooling.

GoldsrcFramework.Sdk
  MSBuild SDK behavior: references, generation, deployment, launch, diagnostics.

GoldsrcFramework.Templates
  dotnet new templates for mod projects.
```

This split keeps responsibilities clear:

- `GoldsrcFramework.Sdk` controls build behavior.
- `GoldsrcFramework.Templates` controls project creation.
- Runtime packages provide APIs.
- Build tool packages provide executable tooling used by MSBuild targets.

For early development, it is acceptable for `GoldsrcFramework.Sdk` to use source project references inside the repository. Before external release, package consumption mode must be implemented and tested.

## SDK Source Layout

SDK-owned MSBuild files should physically live under the SDK project, not in a repository-level `src/targets` folder.

Recommended layout:

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
      README.md
```

The `Sdk` folder is the MSBuild SDK entry point. The `build` folder contains the main SDK props and targets. The `targets` folder contains individual build features. The `tools` folder can later hold packaged tools or tool metadata.

The solution may still have a `targets` solution folder for convenience, but the files should be linked from `GoldsrcFramework.Sdk/targets`, not stored as an independent top-level implementation.

## SDK Responsibilities

The SDK should own the following behavior.

### Project Defaults

The SDK should provide sensible defaults for mod projects:

```xml
<TargetFramework>net10.0</TargetFramework>
<RuntimeIdentifier>win-x86</RuntimeIdentifier>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
```

These defaults should be overridable. For example:

```xml
<GoldsrcFrameworkSetAllowUnsafeBlocks>false</GoldsrcFrameworkSetAllowUnsafeBlocks>
```

### References

In source development mode, the SDK should add `ProjectReference` items to local framework projects.

In package consumption mode, it should add references to framework packages or package-provided assemblies.

External mod projects should not need to know the internal project layout of the GoldsrcFramework repository.

### Entity Export Generation

The SDK should run the build tool that scans framework or mod assemblies and generates the required entity export code.

The current `GenerateEntityExports` target should become an SDK target with clear inputs and outputs, so incremental builds work predictably.

Required behavior:

- Build or locate the generation tool.
- Locate the input assembly.
- Generate C# or C++ entity export artifacts.
- Emit clear errors when required assemblies are missing.
- Avoid regeneration when inputs are unchanged.

### Loader Build

The SDK should support at least one loader strategy:

- Native AOT .NET loader.
- Traditional C++ loader, if still required as a compatibility option.

The selected loader should be controlled by a property:

```xml
<GoldsrcFrameworkLoaderKind>NetLoader</GoldsrcFrameworkLoaderKind>
```

Possible values:

```text
NetLoader
CppLoader
None
```

The loader output should be written to the final project output directory, using `$(TargetDir)` or another fully resolved output property. Avoid early-evaluated relative paths such as plain `client.dll`.

### Deployment

Deployment should be a named SDK target, not a Demo-specific post-build event.

Recommended target:

```powershell
dotnet build /t:Deploy
```

The default build may optionally deploy in Debug mode, but this should be configurable:

```xml
<GoldsrcFrameworkDeployOnBuild>true</GoldsrcFrameworkDeployOnBuild>
```

Deployment should handle:

- Creating the mod directory.
- Copying managed assemblies.
- Copying loader outputs.
- Copying `.deps.json`, `.runtimeconfig.json`, and `.pdb` files.
- Copying content assets.
- Generating or copying `liblist.gam`.
- Reporting copied files.
- Treating copy failures as build errors.

Deployment should be incremental where possible.

### Launch and F5 Debugging

The SDK should provide launch configuration data and/or targets that support running the mod:

```powershell
dotnet run
dotnet build /t:Launch
```

Required properties:

```xml
<HalfLifeInstallDir>...</HalfLifeInstallDir>
<GoldsrcEngineExe>hl.exe</GoldsrcEngineExe>
<GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
<GoldsrcLaunchArguments>-game mymod -dev -console</GoldsrcLaunchArguments>
```

The SDK should not require machine-specific paths to be committed. Local configuration can come from:

- `Directory.Build.props.user`
- `*.csproj.user`
- environment variables
- command-line properties
- Visual Studio launch profiles

### Diagnostics

GoldsrcFramework projects fail in confusing ways when paths, loaders, or runtime files are wrong. The SDK should provide dedicated diagnostics.

Examples:

- Half-Life install directory is missing.
- `hl.exe` or configured engine executable does not exist.
- Mod directory cannot be created.
- Loader output is missing.
- `GoldsrcFramework.dll` is missing from output.
- BuildTool cannot be found.
- Required Visual Studio C++ tools are missing when using `CppLoader`.
- Unsupported runtime identifier is selected.

Diagnostics should prefer explicit MSBuild errors over later runtime crashes.

## Project Template Package

The template package should be separate from the SDK package:

```text
GoldsrcFramework.Templates
```

It should provide at least one template:

```powershell
dotnet new goldsrcmod
```

Optional future templates:

```powershell
dotnet new goldsrcmod-empty
dotnet new goldsrcmod-client
dotnet new goldsrcmod-server
dotnet new goldsrcmod-full
```

## Template Output

A new mod project should look like this:

```text
MyMod/
  MyMod.csproj
  Program.cs or MyModStartup.cs
  MyServerExports.cs
  MyClientExports.cs
  modSettings.json
  liblist.gam
  Properties/
    launchSettings.json
  content/
    maps/
    models/
    sounds/
    sprites/
```

The generated `.csproj` should be small:

```xml
<Project Sdk="GoldsrcFramework.Sdk/1.0.0">

  <PropertyGroup>
    <GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
    <GoldsrcModDisplayName>My Mod</GoldsrcModDisplayName>
  </PropertyGroup>

</Project>
```

The template should not generate repository-specific references such as:

```xml
<ProjectReference Include="..\GoldsrcFramework\GoldsrcFramework.csproj" />
```

Those are only appropriate for source development mode inside the GoldsrcFramework repository.

## Template Parameters

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

Example:

```powershell
dotnet new goldsrcmod -n MyMod --mod-name mymod --display-name "My Mod" --loader NetLoader --ecs true
```

Defaults:

```text
target-framework: net10.0
loader: NetLoader
client: true
server: true
ecs: false
legacy-interop: true
```

## Local Configuration

Generated projects should not hard-code a developer's Half-Life path.

Recommended local file:

```xml
<!-- Directory.Build.props.user -->
<Project>
  <PropertyGroup>
    <HalfLifeInstallDir>G:\Games\Half-Life</HalfLifeInstallDir>
  </PropertyGroup>
</Project>
```

This file should be ignored by Git.

The SDK should also support environment-variable fallback:

```text
HALFLIFE_INSTALL_DIR
GOLDSRCFRAMEWORK_HALFLIFE_DIR
```

If no install directory is configured, build should still be possible, but deploy and launch targets should emit a clear message or error depending on the target.

## Versioning

Before 1.0.0, SDK and template versions can move together:

```text
GoldsrcFramework.Sdk 0.x
GoldsrcFramework.Templates 0.x
GoldsrcFramework 0.x
```

For 1.0.0, the recommended rule is:

- A template version chooses an SDK version.
- The SDK version chooses compatible framework package versions.
- Mod projects may pin the SDK version explicitly.

Example:

```xml
<Project Sdk="GoldsrcFramework.Sdk/1.0.0">
</Project>
```

This keeps old mods buildable even after the framework evolves.

## Migration Plan

### Phase 1: Move Build Logic Into SDK Project

Move files from:

```text
src/targets/
```

to:

```text
src/GoldsrcFramework.Sdk/targets/
```

Add SDK entry files:

```text
src/GoldsrcFramework.Sdk/Sdk/Sdk.props
src/GoldsrcFramework.Sdk/Sdk/Sdk.targets
src/GoldsrcFramework.Sdk/build/GoldsrcFramework.Sdk.props
src/GoldsrcFramework.Sdk/build/GoldsrcFramework.Sdk.targets
```

Make `GoldsrcFramework.Demo` consume the SDK behavior from the SDK project.

### Phase 2: Split Source Mode and Package Mode

Add explicit mode support:

```xml
<GoldsrcFrameworkUseSourceProjectReferences>true</GoldsrcFrameworkUseSourceProjectReferences>
```

In source mode, use `ProjectReference`.

In package mode, use package references or package-provided assemblies.

### Phase 3: Add Deployment and Launch Targets

Promote Demo-specific copy logic into SDK targets:

```text
Deploy.targets
Launch.targets
Diagnostics.targets
```

Make Demo use the same deployment behavior that external projects will use.

### Phase 4: Create Template Package

Add:

```text
src/GoldsrcFramework.Templates/
```

Create the first template:

```text
goldsrcmod
```

Use the template to generate a test project and build it outside the source tree.

### Phase 5: Test External Consumption

Create a local NuGet feed and test:

```powershell
dotnet pack
dotnet new install GoldsrcFramework.Templates
dotnet new goldsrcmod -n TestMod
dotnet build
dotnet build /t:Deploy
```

This test must run without source project references.

### Phase 6: Prepare 1.0.0 Experience

Before 1.0.0:

- Finalize package names.
- Finalize template names.
- Document required Visual Studio workloads.
- Document local Half-Life configuration.
- Document F5 debugging.
- Add SDK diagnostics for common failures.
- Add sample generated project tests.

## Open Questions

1. Should `GoldsrcFramework.Sdk` include runtime assemblies directly, or depend on separate packages?
2. Should deployment happen automatically after every Debug build, or only through `/t:Deploy`?
3. Should the first template support both client and server code, or start client-only?
4. Should Xash3D be treated as a first-class launch target?
5. How much of F5 launch setup can be solved with `launchSettings.json` versus custom MSBuild targets?
6. Should the SDK generate `liblist.gam`, validate an existing file, or both?
7. Should template projects default to ECS support or keep ECS opt-in?

## Recommended Decisions

Recommended initial decisions:

1. Keep the project name `GoldsrcFramework` until at least 1.0.0.
2. Move SDK-owned targets into `GoldsrcFramework.Sdk`.
3. Keep templates in a separate `GoldsrcFramework.Templates` package.
4. Support source mode and package mode explicitly.
5. Make `NetLoader` the default loader.
6. Make deployment an explicit target first, then optionally add deploy-on-build.
7. Keep generated project files small and let the SDK own complexity.

## Success Criteria

This work is successful when the following are true:

1. `GoldsrcFramework.Demo` builds through `GoldsrcFramework.Sdk` behavior.
2. A generated external mod project builds without referencing repository source projects.
3. The generated project can deploy to a configured Half-Life mod directory.
4. Visual Studio F5 can launch the configured engine with the mod loaded.
5. Missing local configuration produces clear errors or guidance.
6. SDK packaging can be tested through a local NuGet feed.
7. The template output is small enough that mod developers mostly see their own game code, not build-system machinery.

