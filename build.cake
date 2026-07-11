var target = Argument("Target", "Default");
var configuration =
    HasArgument("Configuration") ? Argument<string>("Configuration") :
    EnvironmentVariable("Configuration", "Release");

var solution = File("./src/GoldsrcFramework.sln");
var artifactsDirectory = Directory("./artifacts/packages");
var smokeDirectory = Directory("./artifacts/template-smoke");
var buildProjects = new[]
{
    "./src/GoldsrcFramework/GoldsrcFramework.csproj",
    "./src/GoldsrcFramework.BuildTool/GoldsrcFramework.BuildTool.csproj",
    "./src/GoldsrcFramework.Sdk/GoldsrcFramework.Sdk.csproj",
    "./src/GoldsrcFramework.Templates/GoldsrcFramework.Templates.csproj",
};
var packageProjects = new[]
{
    "./src/GoldsrcFramework/GoldsrcFramework.csproj",
    "./src/GoldsrcFramework.Sdk/GoldsrcFramework.Sdk.csproj",
    "./src/GoldsrcFramework.Templates/GoldsrcFramework.Templates.csproj",
};

void RunDotNet(string arguments)
{
    var exitCode = StartProcess(
        "dotnet",
        new ProcessSettings()
        {
            Arguments = arguments,
        });

    if (exitCode != 0)
    {
        throw new Exception($"dotnet {arguments} failed with exit code {exitCode}.");
    }
}

Task("Clean")
    .Description("Cleans package and smoke-test artifacts.")
    .Does(() =>
    {
        CleanDirectory(artifactsDirectory);
        CleanDirectory(smokeDirectory);
    });

Task("Restore")
    .Description("Restores NuGet packages.")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        foreach (var project in buildProjects)
        {
            DotNetRestore(project);
        }
    });

Task("Build")
    .Description("Builds the solution.")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        foreach (var project in buildProjects)
        {
            DotNetBuild(
                project,
                new DotNetBuildSettings()
                {
                    Configuration = configuration,
                    NoRestore = true,
                });
        }
    });

Task("Test")
    .Description("Runs test projects when any are present.")
    .Does(() =>
    {
        var testProjects = GetFiles("./src/**/*.csproj")
            .Where(project => project.GetFilenameWithoutExtension().ToString().EndsWith(".Test"));

        foreach (var project in testProjects)
        {
            DotNetTest(
                project.ToString(),
                new DotNetTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = true,
                    NoRestore = true,
                    ResultsDirectory = Directory("./artifacts/test-results"),
                });
        }
    });

Task("Pack")
    .Description("Creates GoldsrcFramework NuGet packages.")
    .IsDependentOn("Build")
    .Does(() =>
    {
        foreach (var project in packageProjects)
        {
            var includeSymbols = !project.EndsWith("GoldsrcFramework.Templates.csproj");
            DotNetPack(
                project,
                new DotNetPackSettings()
                {
                    Configuration = configuration,
                    IncludeSymbols = includeSymbols,
                    MSBuildSettings = new DotNetMSBuildSettings()
                    {
                        ContinuousIntegrationBuild = !BuildSystem.IsLocalBuild,
                    },
                    NoBuild = true,
                    NoRestore = true,
                    OutputDirectory = artifactsDirectory,
                });
        }
    });

Task("TemplateSmokeTest")
    .Description("Installs the local template package and builds a generated mod project.")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        CleanDirectory(smokeDirectory);

        var templatePackage = GetFiles("./artifacts/packages/GoldsrcFramework.Templates.*.nupkg").Single();
        var frameworkPackage = GetFiles("./artifacts/packages/GoldsrcFramework.*.nupkg")
            .Where(package => !package.GetFilename().ToString().StartsWith("GoldsrcFramework.Sdk.") &&
                              !package.GetFilename().ToString().StartsWith("GoldsrcFramework.Templates."))
            .Single();
        var frameworkVersion = frameworkPackage
            .GetFilenameWithoutExtension()
            .ToString()
            .Substring("GoldsrcFramework.".Length);
        var smokeProjectDirectory = smokeDirectory + Directory("SmokeMod");

        RunDotNet($"new install \"{templatePackage}\" --force");

        RunDotNet($"new goldsrcmod -n SmokeMod -o \"{smokeProjectDirectory}\" --frameworkVersion \"{frameworkVersion}\"");

        var nugetConfigPath = smokeDirectory + File("NuGet.config");
        System.IO.File.WriteAllText(
            MakeAbsolute(nugetConfigPath).FullPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<configuration>\n" +
            "  <packageSources>\n" +
            "    <clear />\n" +
            $"    <add key=\"local\" value=\"{MakeAbsolute(artifactsDirectory).FullPath}\" />\n" +
            "    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" />\n" +
            "  </packageSources>\n" +
            "</configuration>\n");

        DotNetRestore(
            smokeProjectDirectory,
            new DotNetRestoreSettings()
            {
                ConfigFile = nugetConfigPath,
            });

        DotNetBuild(
            smokeProjectDirectory,
            new DotNetBuildSettings()
            {
                Configuration = configuration,
                NoRestore = true,
            });
    });

Task("Default")
    .Description("Builds, tests, packs, and smoke-tests the template package.")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("TemplateSmokeTest");

RunTarget(target);
