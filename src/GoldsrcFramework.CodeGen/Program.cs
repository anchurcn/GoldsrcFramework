using CppAst;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Entry point: loads codegen.json, parses the HLSDK headers with the toolchain
/// located on this machine, runs the raw ABI + humanizer generators, and writes,
/// verifies or previews the output depending on the selected mode.
///
/// Usage: GoldsrcFramework.CodeGen [--config=&lt;path&gt;] [--hlsdk=&lt;path&gt;] [--out=&lt;path&gt;]
///                                 [--rules=&lt;path&gt;] [--list-symbols[=&lt;filter&gt;]] [--dry-run | --check]
///   --config=       Path to a codegen.json (default: the one next to the generator sources).
///   --hlsdk=        Path to the Half-Life SDK checkout (overrides the config).
///   --out=          Main output file path (overrides the config).
///   --rules=        Path to humanizer.rules (overrides the config).
///   --list-symbols  Print every symbol the humanizer can adjust, then exit.
///   --dry-run       Parse and generate, but only report which files would change.
///   --check         Parse and generate, compare with the files on disk, exit 1 if out of date.
/// </summary>
internal static class Program
{
    static int Main(string[] args)
    {
        var dryRun = HasFlag(args, "--dry-run");
        var check = HasFlag(args, "--check");
        if (dryRun && check)
        {
            Console.Error.WriteLine("Options --dry-run and --check are mutually exclusive.");
            return 1;
        }

        var repo = FindRepoRoot();
        var configPath = OptionValue(args, "--config=") ?? Path.Combine(repo, "src", "GoldsrcFramework.CodeGen", "codegen.json");
        var config = File.Exists(configPath) ? CodeGenConfig.Load(configPath) : new CodeGenConfig();
        if (!File.Exists(configPath)) Console.Error.WriteLine($"Warning: config file not found ({configPath}); using built-in defaults.");

        var hlsdk = OptionValue(args, "--hlsdk=") ?? CodeGenConfig.ResolvePath(repo, repo, config.Hlsdk);
        var output = OptionValue(args, "--out=") ?? CodeGenConfig.ResolvePath(repo, repo, config.Output);
        var rulesPath = OptionValue(args, "--rules=") ?? CodeGenConfig.ResolvePath(repo, repo, config.HumanizerRules);

        if (!Directory.Exists(hlsdk))
        {
            Console.Error.WriteLine($"HLSDK directory not found: {hlsdk}");
            Console.Error.WriteLine("Pass the path to a Half-Life SDK checkout via --hlsdk=<path> or the \"hlsdk\" entry in codegen.json.");
            return 1;
        }

        var rules = HumanizerRules.Load(rulesPath);
        foreach (var problem in rules.Problems) Console.Error.WriteLine($"Warning: {problem}");
        if (!File.Exists(rulesPath)) Console.Error.WriteLine("Pass the rules file path via --rules=<path> or the \"humanizerRules\" entry in codegen.json.");

        var options = ToolchainLocator.CreateOptions(hlsdk, config).ConfigureForWindowsMsvc();
        var clientProject = VcxProjectInfo.Load(Path.Combine(hlsdk, config.VcxProject), config.VcxConfiguration, config.VcxPlatform);
        foreach (var includeDir in clientProject.IncludeDirectories) AddIfExists(options.IncludeFolders, includeDir);
        foreach (var define in clientProject.Defines) if (!options.Defines.Contains(define)) options.Defines.Add(define);

        var abiHeaders = config.AbiHeaders.Select(h => CodeGenConfig.ResolvePath(repo, hlsdk, h));
        var files = clientProject.CompileFiles
            .Where(f => Path.GetFileName(f).Equals("cdll_int.cpp", StringComparison.OrdinalIgnoreCase))
            .Concat(clientProject.HeaderFiles.Where(IsClientAbiHeader))
            .Concat(abiHeaders)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var compilation = CppParser.ParseFiles(files, options);
        foreach (var message in compilation.Diagnostics.Messages) Console.WriteLine(message);
        if (compilation.HasErrors) return 1;

        var gen = new RawAbiGenerator(compilation, rules, config.RootStructs, config.ManagedNativeApiRootTypes);

        var nativeDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output)!, ".."));
        var engineDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output)!, "..", ".."));

        var outputs = new List<(string Path, string Content)>
        {
            (output, gen.Generate())
        };
        var humanizer = new HumanizerGenerator(gen, compilation);
        foreach (var (relativePath, content) in humanizer.PlanHumanizerFiles())
            outputs.Add((Path.Combine(nativeDir, CppAstHelpers.NormalizeRelativePath(relativePath)), content));

        // Structural rule problems (and anything the macro-enum pass could not resolve) make
        // the generated code wrong rather than merely ugly, so they fail the run.
        foreach (var problem in humanizer.MacroEnumProblems) Console.Error.WriteLine($"Warning (macroEnums): {problem}");
        if (rules.Problems.Count > 0) return 1;

        ReportRules(rules);

        var symbolFilter = OptionValue(args, "--list-symbols=");
        if (HasFlag(args, "--list-symbols") || symbolFilter is not null)
        {
            PrintSymbols(humanizer.Catalog, symbolFilter);
            return 0;
        }

        outputs.Add((Path.Combine(engineDir, "InlineArrayUnmanaged.Generated.cs"), RawAbiGenerator.CreateInlineArrayDefinitions(engineDir, gen.RequiredInlineArraySizes)));
        outputs.Add((Path.ChangeExtension(output, ".manifest.txt"), gen.CreateManifest(output, hlsdk)));

        if (dryRun || check)
        {
            var outOfDate = 0;
            foreach (var (path, content) in outputs)
            {
                var expected = OutputWriter.NormalizeNewLines(content);
                if (File.Exists(path) && File.ReadAllText(path) == expected) continue;
                outOfDate++;
                Console.WriteLine(File.Exists(path) ? $"Out of date: {path}" : $"Missing: {path}");
            }
            Console.WriteLine(outOfDate == 0
                ? $"Generated code is up to date ({outputs.Count} files)."
                : $"{outOfDate} of {outputs.Count} generated files are missing or out of date.");
            if (dryRun) return 0;
            Console.WriteLine(outOfDate == 0 ? "Check passed." : "Check failed: run the generator to refresh the generated code.");
            return outOfDate == 0 ? 0 : 1;
        }

        HumanizerGenerator.DeletePreviousHumanizerFiles(nativeDir);
        foreach (var (path, content) in outputs) OutputWriter.WriteGeneratedFile(path, content);

        Console.WriteLine($"Generated {output}");
        Console.WriteLine($"Manifest {Path.ChangeExtension(output, ".manifest.txt")}");
        Console.WriteLine($"Types: {gen.GeneratedTypeCount}");

        return 0;
    }

    /// <summary>
    /// Reports rule hygiene: rules that never matched anything are either typos or stale
    /// entries, and both are invisible without this check.
    /// </summary>
    static void ReportRules(HumanizerRules rules)
    {
        Console.WriteLine($"Humanizer rules: {rules.AllRules.Count} loaded from {rules.SourcePath}, {rules.AllRules.Count(r => r.HitCount > 0)} matched");
        var unused = rules.UnusedRules.ToList();
        if (unused.Count == 0) return;

        Console.Error.WriteLine($"Warning: {unused.Count} humanizer rule(s) matched no symbol - fix the path or delete the rule:");
        foreach (var rule in unused.OrderBy(r => r.Line))
            Console.Error.WriteLine($"  {rule.Location}  [{HumanizerRules.SectionName(rule.Section)}]  {rule.Pattern}");
    }

    static void PrintSymbols(HumanizerSymbolCatalog catalog, string? filter)
    {
        foreach (var symbol in catalog.Symbols
            .Where(s => filter is null || s.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Kind, StringComparer.Ordinal).ThenBy(s => s.Path, StringComparer.Ordinal))
        {
            var name = symbol.EmittedName == symbol.RawName ? symbol.RawName : $"{symbol.RawName} => {symbol.EmittedName}";
            Console.WriteLine($"{symbol.Kind,-14} {symbol.Path,-60} {name}");
        }
    }

    /// <summary>
    /// Walks up from the executable location to find the repository root.
    /// Prefers the directory containing .git (works even when solution files live
    /// in a subdirectory); falls back to the directory containing the solution file.
    /// </summary>
    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? solutionDir = null;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
            solutionDir ??= File.Exists(Path.Combine(dir.FullName, "GoldsrcFramework.sln")) ? dir : null;
            dir = dir.Parent!;
        }
        if (solutionDir is not null) return solutionDir.FullName;
        // Legacy fallback for unusual output locations.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
    }

    static string? OptionValue(string[] args, string prefix) => args
        .FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];

    static bool HasFlag(string[] args, string flag) => args.Contains(flag, StringComparer.OrdinalIgnoreCase);

    static void AddIfExists(ICollection<string> list, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) list.Add(path);
    }

    static bool IsClientAbiHeader(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/common/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/engine/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/pm_shared/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/public/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/cl_dll/cl_dll.h", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/cl_dll/kbutton.h", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/cl_dll/Exports.h", StringComparison.OrdinalIgnoreCase);
    }
}
