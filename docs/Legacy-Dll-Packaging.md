# Legacy DLL Packaging and Deployment

## Goal

GoldsrcFramework currently depends on legacy HLSDK client and server DLLs for a large part of its behavior. Until the framework can stand alone, the NuGet SDK should provide a reliable way to ship and deploy those legacy DLLs together with GoldsrcFramework-based mods.

The SDK should support two base-game variants at first:

- `hl`: legacy Half-Life DLLs built from `external/hlsdk`.
- `bare`: minimal legacy DLLs built from `external/bare`.

The selected variant is controlled by the mod project property:

```xml
<BaseGame>hl</BaseGame>
```

`hl` is the default base game.

## Package Layout

Legacy DLLs should be packaged as SDK-controlled tool assets, not as NuGet runtime native assets.

Do not use this layout:

```text
runtimes/win-x86/native/hl/libclient.dll
runtimes/win-x86/native/bare/libclient.dll
```

.NET flattens native runtime asset directories when copying them to the output folder, so multiple base-game variants with the same file names can conflict.

Use this layout in `GoldsrcFramework.Sdk` instead:

```text
tools/GoldsrcFramework.LegacyDlls/
  win-x86/
    hl/
      libclient.dll
      libserver.dll
    bare/
      libclient.dll
      libserver.dll
```

These files are deployment inputs owned by the GoldsrcFramework SDK targets.

## Source Build Layout

Source builds should produce the same logical layout under `artifacts`:

```text
artifacts/legacy/
  win-x86/
    hl/
      libclient.dll
      libserver.dll
    bare/
      libclient.dll
      libserver.dll
```

`build-hl.bat` remains the user-facing double-click entry point for building the Half-Life legacy DLLs. It may internally call a shared script or target, but the top-level workflow should stay simple:

```text
build-hl.bat
```

Future shared implementation can expose base-game-specific entry points such as:

```text
build-legacy.bat hl
build-legacy.bat bare
```

but `build-hl.bat` should remain available for users who only need the default Half-Life DLLs.

## Bare SDK Submodule

Add the bare HLSDK variant as a submodule:

```powershell
git submodule add https://github.com/anchurcn/ball_game.git external/bare
```

The `bare` project should build the same pair of renamed DLLs:

```text
libclient.dll
libserver.dll
```

## MSBuild Properties

The public project property is:

```xml
<BaseGame>hl</BaseGame>
```

SDK targets should normalize it into a GoldsrcFramework-specific internal property:

```xml
<GoldsrcFrameworkBaseGame Condition="'$(GoldsrcFrameworkBaseGame)' == ''">$(BaseGame)</GoldsrcFrameworkBaseGame>
<GoldsrcFrameworkBaseGame Condition="'$(GoldsrcFrameworkBaseGame)' == ''">hl</GoldsrcFrameworkBaseGame>
```

This keeps the user-facing project file concise while leaving room for future SDK-specific configuration.

## Asset Resolution

The SDK should resolve legacy DLLs from different roots depending on the build mode.

In source mode:

```text
artifacts/legacy/win-x86/$(GoldsrcFrameworkBaseGame)/
```

In package mode:

```text
$(GoldsrcFrameworkSdkRoot)tools/GoldsrcFramework.LegacyDlls/win-x86/$(GoldsrcFrameworkBaseGame)/
```

The resolved directory must contain:

```text
libclient.dll
libserver.dll
```

If either file is missing, the SDK should fail with a clear diagnostic that includes the selected base game and the expected path.

## Deployment

Legacy DLL copying should be implemented as separate SDK targets, independent from NetLoader generation:

```text
ResolveGoldsrcFrameworkLegacyDlls
CopyGoldsrcFrameworkLegacyDlls
```

The copy behavior should be:

```text
libclient.dll -> $(TargetDir)/libclient.dll
libserver.dll -> $(TargetDir)/libserver.dll
```

The output root is important because the generated NetLoader `client.dll` resolves the legacy DLLs from its load directory.

The destination directories remain configurable:

```xml
<GoldsrcFrameworkClientDllDeployDir>...</GoldsrcFrameworkClientDllDeployDir>
<GoldsrcFrameworkServerDllDeployDir>...</GoldsrcFrameworkServerDllDeployDir>
```

Copying the final output directory into a local game or engine install is a separate deploy concern. The SDK should keep that in a dedicated deploy target:

```text
DeployToHlModDir.targets
```

When `HlModDir` is set, the deploy target should copy `$(TargetDir)` into `$(HlModDir)` with robocopy. In NetLoader mode, this deploy step must run after `BuildNetLoader`, so the generated `client.dll` and the legacy DLLs copied into `$(TargetDir)` are deployed together.

## Project Examples

Demo should explicitly select Half-Life:

```xml
<PropertyGroup>
  <BaseGame>hl</BaseGame>
</PropertyGroup>
```

A package consumer can omit `BaseGame` and receive the same default:

```xml
<Project Sdk="GoldsrcFramework.Sdk/0.1.0">
  <PropertyGroup>
    <GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
  </PropertyGroup>
</Project>
```

A consumer that wants the bare legacy DLLs can opt in:

```xml
<Project Sdk="GoldsrcFramework.Sdk/0.1.0">
  <PropertyGroup>
    <GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
    <BaseGame>bare</BaseGame>
  </PropertyGroup>
</Project>
```

## Implementation Order

1. Add `external/bare` as a Git submodule.
2. Update `build-hl.bat` to place renamed HL DLLs under `artifacts/legacy/win-x86/hl/`.
3. Add a build path for `bare` that outputs to `artifacts/legacy/win-x86/bare/`.
4. Pack `artifacts/legacy/win-x86/**` into `GoldsrcFramework.Sdk` under `tools/GoldsrcFramework.LegacyDlls/win-x86/`.
5. Add SDK targets to resolve and copy the selected legacy DLLs.
6. Add diagnostics for unsupported `BaseGame` values and missing DLLs.
7. Update Demo and template projects to document the default `hl` behavior.
