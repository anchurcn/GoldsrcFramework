using CppAst;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Builds the <see cref="CppParserOptions"/> for parsing the HLSDK headers, including
/// discovery of the MSVC and Windows SDK system include directories on this machine.
/// </summary>
internal static class ToolchainLocator
{
    public static CppParserOptions CreateOptions(string hlsdk, CodeGenConfig config)
    {
        var options = new CppParserOptions { AutoSquashTypedef = false, ParseMacros = true, ParseSystemIncludes = false, TargetCpu = CppTargetCpu.X86 };
        foreach (var dir in config.IncludeDirs) options.IncludeFolders.Add(Path.Combine(hlsdk, dir));
        options.Defines.AddRange(config.Defines);
        options.AdditionalArguments.AddRange(config.ParserArguments);

        var msvcInclude = FindMsvcInclude();
        if (msvcInclude is null)
            Console.Error.WriteLine("Warning: could not locate the MSVC include directory (vswhere / Visual Studio 2022). Parsing may fail for standard headers.");
        AddIfExists(options.SystemIncludeFolders, msvcInclude);

        var kitsRoot = @"C:\Program Files (x86)\Windows Kits\10\Include";
        foreach (var kit in new[] { "ucrt", "shared", "um" })
            AddIfExists(options.SystemIncludeFolders, LatestDir(kitsRoot, kit));

        return options;
    }

    /// <summary>
    /// Locates the newest MSVC "include" directory. Prefers vswhere (works with any
    /// Visual Studio 2017+ edition and channel); falls back to scanning the common
    /// Visual Studio 2022 installation paths.
    /// </summary>
    public static string? FindMsvcInclude()
    {
        var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (File.Exists(vswhere))
        {
            var installationPath = RunVswhere(vswhere);
            if (!string.IsNullOrWhiteSpace(installationPath))
            {
                var include = LatestDir(Path.Combine(installationPath, "VC", "Tools", "MSVC"), "include");
                if (include is not null) return include;
            }
        }

        foreach (var edition in new[] { "Community", "Professional", "Enterprise", "BuildTools" })
        {
            var include = LatestDir($@"C:\Program Files\Microsoft Visual Studio\2022\{edition}\VC\Tools\MSVC", "include");
            if (include is not null) return include;
        }

        return null;
    }

    static string? RunVswhere(string vswhere)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = vswhere,
                Arguments = "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            return null;
        }
    }

    static string? LatestDir(string root, string child)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.GetDirectories(root).OrderByDescending(Path.GetFileName).Select(d => Path.Combine(d, child)).FirstOrDefault(Directory.Exists);
    }

    static void AddIfExists(ICollection<string> list, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) list.Add(path);
    }
}
