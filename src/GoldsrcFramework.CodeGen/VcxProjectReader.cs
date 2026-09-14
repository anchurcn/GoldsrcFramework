namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Reads include directories, defines and source/header file lists from a Visual
/// Studio C++ project file (.vcxproj) for one configuration|platform combination.
/// </summary>
internal sealed class VcxProjectInfo
{
    public required IReadOnlyList<string> CompileFiles { get; init; }
    public required IReadOnlyList<string> HeaderFiles { get; init; }

    public required IReadOnlyList<string> IncludeDirectories { get; init; }
    public required IReadOnlyList<string> Defines { get; init; }

    public static VcxProjectInfo Load(string projectPath, string configuration, string platform)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var doc = System.Xml.Linq.XDocument.Load(projectPath);
        var ns = doc.Root!.Name.Namespace;
        var conditionToken = $"'$(Configuration)|$(Platform)'=='{configuration}|{platform}'";

        var clCompile = doc.Descendants(ns + "ItemDefinitionGroup")
            .Where(e => ((string?)e.Attribute("Condition"))?.Contains(conditionToken, StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(e => e.Elements(ns + "ClCompile"))
            .FirstOrDefault();

        var includeDirs = SplitMsBuildList(clCompile?.Element(ns + "AdditionalIncludeDirectories")?.Value)
            .Where(v => !v.StartsWith("%(", StringComparison.Ordinal))
            .Select(v => NormalizeProjectPath(projectDir, v))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defines = SplitMsBuildList(clCompile?.Element(ns + "PreprocessorDefinitions")?.Value)
            .Where(v => !v.StartsWith("%(", StringComparison.Ordinal))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compileFiles = doc.Descendants(ns + "ClCompile")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => NormalizeProjectPath(projectDir, v!))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var headerFiles = doc.Descendants(ns + "ClInclude")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => NormalizeProjectPath(projectDir, v!))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();


        return new VcxProjectInfo { CompileFiles = compileFiles, HeaderFiles = headerFiles, IncludeDirectories = includeDirs, Defines = defines };
    }


    static IEnumerable<string> SplitMsBuildList(string? value) => (value ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static string NormalizeProjectPath(string projectDir, string value)
    {
        value = value.Replace("$(ProjectDir)", projectDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            .Replace("$(SolutionDir)", Path.GetFullPath(Path.Combine(projectDir, ".")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            .Replace("$(Configuration)", "Release", StringComparison.OrdinalIgnoreCase)
            .Replace("$(Platform)", "Win32", StringComparison.OrdinalIgnoreCase);
        return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(projectDir, value));
    }
}
