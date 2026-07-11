# NuGet Packaging

GoldsrcFramework is published as three NuGet packages that share the same semantic version:

- `GoldsrcFramework`: runtime assemblies for GoldSrc mod projects.
- `GoldsrcFramework.Sdk`: MSBuild SDK assets, targets, loader source, and build-time tools.
- `GoldsrcFramework.Templates`: `dotnet new` templates for creating mod projects.

## Local Build

Restore the local tools and run the package pipeline:

```powershell
dotnet tool restore
dotnet cake --target=Pack
```

Run the template smoke test:

```powershell
dotnet cake --target=TemplateSmokeTest
```

The smoke test installs the local template package, generates a `goldsrcmod` project, restores it from `artifacts/packages`, and builds it.

## Versioning

The repository uses MinVer with `v`-prefixed semantic version tags.

Examples:

```text
v0.1.0
v0.2.0-preview.1
v1.0.0
```

When no version tag is present, MinVer produces a preview version based on commit height.

## Generated Project Shape

The template creates projects in this shape:

```xml
<Project Sdk="GoldsrcFramework.Sdk/0.1.0">
  <PropertyGroup>
    <GoldsrcModDirectoryName>mymod</GoldsrcModDirectoryName>
    <GoldsrcModDisplayName>My GoldSrc Mod</GoldsrcModDisplayName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="GoldsrcFramework" Version="0.1.0" />
  </ItemGroup>
</Project>
```

Use `--frameworkVersion` when testing local preview packages:

```powershell
dotnet new goldsrcmod -n MyMod --frameworkVersion 0.1.0-preview.1
```

## GitHub Actions

The build workflow restores tools, builds the managed package projects, creates all three packages, and runs the template smoke test. Pushes to `main` or `master` also publish preview packages to GitHub Packages.

The release workflow runs on published GitHub releases and pushes packages to nuget.org using the `NUGET_API_KEY` repository secret.
