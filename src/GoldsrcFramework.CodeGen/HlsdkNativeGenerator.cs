using CppAst;
using System.Text;

namespace GoldsrcFramework.CodeGen;

internal static class HlsdkNativeGenerator
{
    static readonly string[] RootStructs = ["cldll_func_t", "cl_enginefuncs_s", "cl_enginefunc_t", "enginefuncs_s", "DLL_FUNCTIONS", "NEW_DLL_FUNCTIONS"];

    static int Main(string[] args)
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var hlsdk = args.FirstOrDefault(a => a.StartsWith("--hlsdk=", StringComparison.OrdinalIgnoreCase))?.Substring(8)
            ?? Path.Combine(repo, "external", "hlsdk");
        var output = args.FirstOrDefault(a => a.StartsWith("--out=", StringComparison.OrdinalIgnoreCase))?.Substring(6)
            ?? Path.Combine(repo, "src", "GoldsrcFramework.Engine", "Native", "Generated", "HlsdkNative.generated.cs");

        var options = CreateOptions(hlsdk);
        var clientProject = VcxProjectInfo.Load(Path.Combine(hlsdk, "projects", "vs2019", "hl_cdll.vcxproj"), "Release", "Win32");
        foreach (var includeDir in clientProject.IncludeDirectories) AddIfExists(options.IncludeFolders, includeDir);
        foreach (var define in clientProject.Defines) if (!options.Defines.Contains(define)) options.Defines.Add(define);

        var abiHeaders = new[]
        {
            Path.Combine(hlsdk, "cl_dll", "cl_dll.h"),
            Path.Combine(hlsdk, "engine", "cdll_int.h"),
            Path.Combine(hlsdk, "engine", "APIProxy.h"),
            Path.Combine(hlsdk, "cl_dll", "kbutton.h"),

            Path.Combine(hlsdk, "engine", "eiface.h"),
            Path.Combine(hlsdk, "cl_dll", "Exports.h"),
            Path.Combine(repo, "src", "GoldsrcFramework.CodeGen", "HlsdkSupplementalTypes.h.txt"),
        };
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

        var mappings = LoadMappings(Path.Combine(AppContext.BaseDirectory, "CustomTypeMapping.txt"));
        var humanizerTypeMappings = LoadMappings(Path.Combine(repo, "src", "GoldsrcFramework.CodeGen", "HumanizerTypeMapping.txt"));
        var humanizerVectorFields = LoadSet(Path.Combine(repo, "src", "GoldsrcFramework.CodeGen", "HumanizerVectorFields.txt"));
        var gen = new Generator(compilation, mappings, humanizerTypeMappings, humanizerVectorFields);
        var text = gen.Generate();
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        WriteGeneratedFile(output, text);
        gen.GenerateHumanizerFiles(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output)!, "..")));

        var engineDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output)!, "..", ".."));
        var inlineArrayOutput = Path.Combine(engineDir, "InlineArrayUnmanaged.Generated.cs");
        WriteGeneratedFile(inlineArrayOutput, CreateInlineArrayDefinitions(engineDir, gen.RequiredInlineArraySizes));

        var manifest = gen.CreateManifest(output, hlsdk);
        WriteGeneratedFile(Path.ChangeExtension(output, ".manifest.txt"), manifest);
        Console.WriteLine($"Generated {output}");
        Console.WriteLine($"Generated {inlineArrayOutput}");

        Console.WriteLine($"Manifest {Path.ChangeExtension(output, ".manifest.txt")}");
        Console.WriteLine($"Types: {gen.GeneratedTypeCount}");

        return 0;
    }

    static void WriteGeneratedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, NormalizeNewLines(content), Encoding.UTF8);
    }

    static string NormalizeNewLines(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        return Environment.NewLine == "\n" ? normalized : normalized.Replace("\n", Environment.NewLine);
    }


    static CppParserOptions CreateOptions(string hlsdk)
    {
        var options = new CppParserOptions { AutoSquashTypedef = false, ParseMacros = true, ParseSystemIncludes = false, TargetCpu = CppTargetCpu.X86 };
        foreach (var dir in new[] { "dlls", "cl_dll", "cl_dll/particleman", "public", "common", "pm_shared", "engine", "utils/vgui/include", "game_shared", "external" })
            options.IncludeFolders.Add(Path.Combine(hlsdk, dir));
        options.Defines.AddRange(["WIN32", "_WINDOWS", "CLIENT_DLL", "CLIENT_WEAPONS", "HL_DLL", "_WINDLL", "_CRT_SECURE_NO_WARNINGS", "__PRFCHWINTRIN_H", "DLLEXPORT=", "EXPORT="]);
        options.AdditionalArguments.AddRange(["-std=c++17", "-fms-extensions"]);
        AddIfExists(options.SystemIncludeFolders, LatestDir(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC", "include"));
        AddIfExists(options.SystemIncludeFolders, LatestDir(@"C:\Program Files (x86)\Windows Kits\10\Include", "ucrt"));
        AddIfExists(options.SystemIncludeFolders, LatestDir(@"C:\Program Files (x86)\Windows Kits\10\Include", "shared"));
        AddIfExists(options.SystemIncludeFolders, LatestDir(@"C:\Program Files (x86)\Windows Kits\10\Include", "um"));

        return options;
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


    static string CreateInlineArrayDefinitions(string engineDir, IReadOnlyCollection<int> requiredSizes)
    {
        var existingSizes = Directory.GetFiles(engineDir, "InlineArrayUnmanaged*.cs")
            .Where(f => !Path.GetFileName(f).Equals("InlineArrayUnmanaged.Generated.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(File.ReadAllLines)
            .Select(line => System.Text.RegularExpressions.Regex.Match(line, @"\[InlineArray\((\d+)\)\]"))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

        var missingSizes = requiredSizes.Where(size => !existingSizes.Contains(size)).Order().ToList();
        var sb = new StringBuilder("// <auto-generated/>\nusing System.Runtime.CompilerServices;\n\nnamespace NativeInterop;\n\n");
        foreach (var size in missingSizes)
        {
            sb.Append("[InlineArray(").Append(size).AppendLine(")] ");
            sb.Append("public unsafe struct InlineArray").Append(size).AppendLine("<T> : IInlineArrayUnmanaged<T> where T : unmanaged");
            sb.AppendLine("{");
            sb.AppendLine("    public T Element0;");
            sb.AppendLine("}\n");
        }

        return sb.ToString();
    }


    static Dictionary<string, string> LoadMappings(string path) => File.Exists(path)
        ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length >= 2).ToDictionary(p => p[0], p => p[1])
        : [];
    static HashSet<string> LoadSet(string path) => File.Exists(path)
        ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);




    sealed class VcxProjectInfo
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

    sealed class Generator(CppCompilation compilation, Dictionary<string, string> mappings, Dictionary<string, string> humanizerTypeMappings, HashSet<string> humanizerVectorFields)
    {
        readonly Queue<CppType> _queue = new();

        readonly Dictionary<string, CppTypedef> _typedefs = compilation.Typedefs.ToDictionary(t => t.Name, t => t);
        readonly Dictionary<string, CppClass> _classes = compilation.Classes.Where(c => !string.IsNullOrWhiteSpace(c.Name)).GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.FirstOrDefault(c => c.IsDefinition) ?? g.First());
        readonly Dictionary<string, CppEnum> _enums = compilation.Enums.Where(e => !string.IsNullOrWhiteSpace(e.Name)).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First());
        readonly Dictionary<string, string> _typeAliases = compilation.Typedefs
            .Select(t => (Target: TypedefTargetName(t), Alias: t.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x.Target))
            .GroupBy(x => x.Target!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Alias, StringComparer.OrdinalIgnoreCase);



        readonly HashSet<string> _seen = [];
        readonly HashSet<int> _inlineArraySizes = [];

        readonly List<CppType> _ordered = [];
        readonly Dictionary<string, string> _pointerWrappers = [];
        readonly HashSet<string> _opaqueTypes = [];
        readonly Dictionary<string, string[]> _sourceLineCache = new(StringComparer.OrdinalIgnoreCase);


        public IReadOnlyCollection<int> RequiredInlineArraySizes => _inlineArraySizes;


        public int GeneratedTypeCount => _ordered.Count;

        public string Generate()
        {
            foreach (var c in compilation.Classes.Where(c => RootStructs.Contains(c.Name))) Enqueue(c);

            foreach (var f in compilation.Functions.Where(f => Path.GetFileName(f.SourceFile).Equals("Exports.h", StringComparison.OrdinalIgnoreCase))) { Enqueue(f.ReturnType); foreach (var p in f.Parameters) Enqueue(p.Type); }
            while (_queue.TryDequeue(out var type)) Visit(type);
            var sb = new StringBuilder("// <auto-generated/>\nusing System;\nusing System.Runtime.InteropServices;\nusing GoldsrcFramework.LinearMath;\nusing NativeInterop;\n\nnamespace GoldsrcFramework.Engine.Native.Generated;\n\n");
            foreach (var type in _ordered) EmitType(sb, type);
            foreach (var wrapper in _pointerWrappers.OrderBy(x => x.Key)) sb.Append("[StructLayout(LayoutKind.Sequential)]\npublic unsafe struct ").Append(wrapper.Key).Append(" { public ").Append(wrapper.Value).AppendLine(" Value; }\n");
            foreach (var opaque in _opaqueTypes.Order()) sb.Append("[StructLayout(LayoutKind.Sequential)]\npublic unsafe struct ").Append(opaque).AppendLine(" { }\n");

            return sb.ToString();
        }

        public void GenerateHumanizerFiles(string nativeDir)
        {
            Directory.CreateDirectory(nativeDir);
            foreach (var pair in humanizerTypeMappings.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            {
                var cls = ResolveClass(pair.Key);
                if (cls is null || !cls.IsDefinition) continue;
                var rawName = CsTypeName(cls, false);
                var filePath = Path.Combine(nativeDir, pair.Value + ".cs");
                var sb = new StringBuilder("// <auto-generated/>\nusing System;\nusing System.Runtime.InteropServices;\nusing GoldsrcFramework.LinearMath;\nusing NativeInterop;\nusing GoldsrcFramework.Engine.Native.Generated;\n\nnamespace GoldsrcFramework.Engine.Native;\n\n");
                EmitHumanizedClass(sb, cls, pair.Value, rawName, renameFields: true);
                WriteGeneratedFile(filePath, sb.ToString());
                Console.WriteLine($"Generated {filePath}");
            }
        }

        CppClass? ResolveClass(string name) => _classes.Values.FirstOrDefault(c => string.Equals(CsTypeName(c, false), name, StringComparison.OrdinalIgnoreCase) || string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        void EmitHumanizedClass(StringBuilder sb, CppClass c, string humanizedName, string rawName, bool renameFields)
        {
            EmitHumanizerXmlDocumentation(sb, c, "");
            sb.AppendLine("[StructLayout(LayoutKind.Sequential)]").Append("public unsafe struct ").AppendLine(humanizedName).AppendLine("{");
            foreach (var f in c.Fields)
            {
                EmitHumanizerXmlDocumentation(sb, f, "    ");
                sb.Append("    public ").Append(HumanizedFieldType(rawName, f)).Append(' ').Append(Safe(renameFields ? HumanizedFieldName(f.Name) : f.Name)).AppendLine(";");
            }
            sb.AppendLine("}\n");
        }

        void EmitHumanizerXmlDocumentation(StringBuilder sb, CppElement element, string indent)
        {
            var summaryLines = GetCommentLines(element).ToList();
            var remarkLines = new List<string>();
            var original = GetOriginalDeclarationLine(element);
            if (!string.IsNullOrWhiteSpace(original)) remarkLines.Add("Original: " + original);
            if (element is CppField field && Strip(field.Type) is CppTypedef typedef && IsFunctionPointer(typedef.ElementType))
            {
                var aliasOriginal = GetOriginalDeclarationLine(typedef);
                if (!string.IsNullOrWhiteSpace(aliasOriginal)) remarkLines.Add("Alias: " + aliasOriginal);
            }

            if (summaryLines.Count == 0 && remarkLines.Count == 0) return;
            if (summaryLines.Count > 0)
            {
                sb.Append(indent).AppendLine("/// <summary>");
                foreach (var line in summaryLines) sb.Append(indent).Append("/// ").AppendLine(XmlEscape(line));
                sb.Append(indent).AppendLine("/// </summary>");
            }
            if (remarkLines.Count > 0)
            {
                sb.Append(indent).AppendLine("/// <remarks>");
                foreach (var line in remarkLines) sb.Append(indent).Append("/// ").AppendLine(XmlEscape(line));
                sb.Append(indent).AppendLine("/// </remarks>");
            }
        }

        static string XmlEscape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");


        string HumanizedFieldName(string name)
        {
            foreach (var prefix in new[] { "pfnEngSrc_pfn", "pfnEngSrc_", "pfn" })
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
                    return name[prefix.Length..];
            }
            return name;
        }

        string HumanizedFieldType(string rawTypeName, CppField field)
        {
            if (IsConfiguredVectorField(rawTypeName, field) && IsFloatArray3(field.Type)) return "Vector3";
            if (Strip(field.Type) is CppArrayType array) return HumanizedArrayFieldType(array);
            return HumanizedCsTypeName(field.Type, true);
        }

        string HumanizedArrayFieldType(CppArrayType array)
        {
            var element = Strip(array.ElementType)!;
            if (element is CppArrayType nested) return $"InlineArray{array.Size}<" + HumanizedArrayFieldType(nested) + ">";
            if (element is CppPointerType pointer)
            {
                var pointerType = HumanizedCsTypeName(pointer, true);
                var wrapperName = PointerWrapperName(pointerType);
                return $"InlineArray{array.Size}<{wrapperName}>";
            }
            return $"InlineArray{array.Size}<" + HumanizedCsTypeName(element, false) + ">";
        }

        bool IsConfiguredVectorField(string rawTypeName, CppField field) => humanizerVectorFields.Contains(rawTypeName + "." + field.Name);

        static bool IsFloatArray3(CppType type)
        {
            type = Strip(type)!;
            return type is CppArrayType { Size: 3, ElementType: CppPrimitiveType p } && p.Kind == CppPrimitiveKind.Float;
        }

        string HumanizedCsTypeName(CppType type, bool field)
        {
            var rawName = CsTypeName(type, field);
            return humanizerTypeMappings.TryGetValue(rawName, out var mapped) ? mapped : rawName;
        }


        public string CreateManifest(string output, string hlsdk)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HLSDK={hlsdk}");
            sb.AppendLine($"Output={output}");
            sb.AppendLine($"GeneratedTypeCount={GeneratedTypeCount}");
            sb.AppendLine("Roots=" + string.Join(",", RootStructs));
            sb.AppendLine("Mappings=" + string.Join(",", mappings.Select(x => x.Key + "=>" + x.Value)));
            sb.AppendLine("PointerWrappers=" + string.Join(",", _pointerWrappers.Select(x => x.Key + "=>" + x.Value)));

            sb.AppendLine("Types=" + string.Join(",", _ordered.Select(t => NativeName(t) ?? CsTypeName(t, false))));
            sb.AppendLine("InlineArraySizes=" + string.Join(",", _inlineArraySizes.Order()));

            sb.AppendLine("OpaqueTypes=" + string.Join(",", _opaqueTypes.Order()));


            return sb.ToString();
        }


        void Enqueue(CppType? type)
        {
            type = Strip(type);
            if (type is CppUnexposedType u)
            {
                if (_typedefs.TryGetValue(u.Name, out var td)) type = td;
                else if (_classes.TryGetValue(u.Name, out var cls)) type = cls;
                else if (_enums.TryGetValue(u.Name, out var en)) type = en;
            }

            if (type is null || type is CppPrimitiveType || type is CppPointerType || type is CppFunctionType || type is CppArrayType) return;
            var name = NativeName(type);
            if ((name is not null && mappings.ContainsKey(name)) || !_seen.Add(CsTypeName(type, false))) return;
            _queue.Enqueue(type);
        }


        void Visit(CppType type)
        {
            type = Strip(type)!;
            if (type is CppUnexposedType ux)
            {
                if (_typedefs.TryGetValue(ux.Name, out var uxTypedef)) { Visit(uxTypedef); return; }
                if (_classes.TryGetValue(ux.Name, out var cls)) { Visit(cls); return; }
                if (_enums.TryGetValue(ux.Name, out var en)) { Visit(en); return; }
            }

            if (type is CppTypedef alias && alias.ElementType is CppClass or CppEnum or CppUnexposedType)
            {
                var target = Strip(alias.ElementType)!;
                if (target is CppUnexposedType aliasUx)
                {
                    if (_classes.TryGetValue(aliasUx.Name, out var cls)) target = cls;
                    else if (_enums.TryGetValue(aliasUx.Name, out var en)) target = en;
                }
                _ordered.Add(target);
                if (target is CppClass aliasClass) foreach (var f in aliasClass.Fields) Collect(f.Type);
                return;
            }

            if (type is CppTypedef td && IsFunctionPointer(td.ElementType)) { Visit(UnwrapFunctionPointer(td.ElementType)!); return; }
            _ordered.Add(type);
            if (type is CppTypedef t) Enqueue(t.ElementType);
            else if (type is CppClass c) foreach (var f in c.Fields) Collect(f.Type);
        }

        void Collect(CppType? type)
        {
            type = Strip(type);
            switch (type)
            {
                case null or CppPrimitiveType: return;
                case CppPointerType p: Collect(p.ElementType); return;
                case CppArrayType a: Collect(a.ElementType); return;
                case CppFunctionType f: Collect(f.ReturnType); foreach (var p in f.Parameters) Collect(p.Type); return;
                case CppTypedef td when IsFunctionPointer(td.ElementType): Collect(UnwrapFunctionPointer(td.ElementType)); return;
                case CppUnexposedType u when _typedefs.TryGetValue(u.Name, out var resolved): Collect(resolved); return;
                case CppUnexposedType u when _classes.TryGetValue(u.Name, out var resolvedClass): Collect(resolvedClass); return;
                case CppUnexposedType u when _enums.TryGetValue(u.Name, out var resolvedEnum): Collect(resolvedEnum); return;

                default: Enqueue(type); return;
            }
        }

        void EmitType(StringBuilder sb, CppType type)
        {
            type = Strip(type)!;
            if (type is CppTypedef td && td.ElementType is CppPrimitiveType)
            {
                EmitDeclarationComments(sb, td, "");
                sb.AppendLine("[StructLayout(LayoutKind.Sequential)]").Append("public struct ").Append(td.Name).Append(" { public ").Append(CsTypeName(td.ElementType, false)).AppendLine(" Value; }\n");
                return;
            }
            if (type is CppEnum e)
            {
                EmitDeclarationComments(sb, e, "");
                sb.Append("public enum ").AppendLine(CsTypeName(e, false)).AppendLine("{");
                foreach (var item in e.Items) sb.Append("    ").Append(item.Name).Append(" = ").Append(item.Value).AppendLine(",");
                sb.AppendLine("}\n");
                return;
            }
            if (type is not CppClass c || !c.IsDefinition) return;
            EmitDeclarationComments(sb, c, "");
            sb.AppendLine("[StructLayout(LayoutKind.Sequential)]").Append("public unsafe struct ").AppendLine(CsTypeName(c, false)).AppendLine("{");
            foreach (var f in c.Fields)
            {
                EmitDeclarationComments(sb, f, "    ");
                sb.Append("    public ").Append(FieldType(f.Type)).Append(' ').Append(Safe(f.Name)).AppendLine(";");
            }
            sb.AppendLine("}\n");
        }

        void EmitDeclarationComments(StringBuilder sb, CppElement element, string indent)
        {
            foreach (var line in GetCommentLines(element)) sb.Append(indent).Append("// ").AppendLine(line);
            var original = GetOriginalDeclarationLine(element);
            if (!string.IsNullOrWhiteSpace(original)) sb.Append(indent).Append("// Original: ").AppendLine(original);
        }

        IEnumerable<string> GetCommentLines(CppElement element)
        {
            if (element is not ICppDeclaration declaration || declaration.Comment is null) yield break;
            var text = declaration.Comment.ToString().Replace("\r", string.Empty).Trim();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var cleaned = line.Trim().TrimStart('/', '*').Trim();
                if (!string.IsNullOrWhiteSpace(cleaned)) yield return cleaned;
            }
        }

        string? GetOriginalDeclarationLine(CppElement element)
        {
            var span = element.Span;
            if (string.IsNullOrWhiteSpace(span.Start.File) || span.Start.Line <= 0) return null;
            if (!File.Exists(span.Start.File)) return null;
            if (!_sourceLineCache.TryGetValue(span.Start.File, out var lines))
            {
                lines = File.ReadAllLines(span.Start.File);
                _sourceLineCache[span.Start.File] = lines;
            }
            var start = Math.Max(1, span.Start.Line);
            var end = span.End.Line > 0 ? Math.Min(span.End.Line, lines.Length) : start;
            if (start > lines.Length) return null;

            var selected = lines.Skip(start - 1).Take(Math.Max(1, end - start + 1))
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
            if (selected.Count == 0) return null;

            var original = string.Join(" ", selected);
            return original.Length <= 240 ? original : original[..237] + "...";
        }


        string FieldType(CppType type) => type is CppArrayType a ? ArrayType(a) : CsTypeName(type, true);

        string ArrayType(CppArrayType a)
        {
            _inlineArraySizes.Add(a.Size);
            var element = Strip(a.ElementType)!;
            if (element is CppArrayType nested) return $"InlineArray{a.Size}<{ArrayType(nested)}>";
            if (element is CppPointerType)
            {
                var pointerType = CsTypeName(element, true);
                var wrapperName = PointerWrapperName(pointerType);
                _pointerWrappers.TryAdd(wrapperName, pointerType);
                return $"InlineArray{a.Size}<{wrapperName}>";
            }
            return $"InlineArray{a.Size}<{CsTypeName(element, false)}>";
        }

        string CsTypeName(CppType type, bool field)
        {
            type = Strip(type)!;
            if (type is CppTypedef td) return mappings.TryGetValue(td.Name, out var m) ? m : IsFunctionPointer(td.ElementType) ? CsTypeName(UnwrapFunctionPointer(td.ElementType)!, field) : td.ElementType is CppClass or CppEnum or CppUnexposedType ? CsTypeName(td.ElementType, field) : td.Name;
            if (type is CppPointerType p) return IsFunctionPointer(p.ElementType) ? CsTypeName(p.ElementType, field) : CsTypeName(p.ElementType, field) + "*";
            if (type is CppArrayType a) return CsTypeName(a.ElementType, field) + "*";
            if (type is CppFunctionType fn) return "delegate* unmanaged[Cdecl]<" + string.Join(", ", fn.Parameters.Select(p => CsTypeName(p.Type, field)).Append(CsTypeName(fn.ReturnType, field))) + ">";
            if (type is CppEnum e)
            {
                if (string.IsNullOrWhiteSpace(e.Name)) return "int";
                if (mappings.TryGetValue(e.Name, out var em)) return em;
                return _typeAliases.TryGetValue(e.Name, out var alias) ? alias : e.Name;
            }
            if (type is CppUnexposedType u && _typedefs.TryGetValue(u.Name, out var resolved)) return CsTypeName(resolved, field);
            if (type is CppUnexposedType u3 && _classes.TryGetValue(u3.Name, out var resolvedClass)) return CsTypeName(resolvedClass, field);
            if (type is CppUnexposedType u4 && _enums.TryGetValue(u4.Name, out var resolvedEnum)) return CsTypeName(resolvedEnum, field);
            if (type is CppUnexposedType u2 && mappings.TryGetValue(u2.Name, out var um)) return um;
            if (type is CppUnexposedType u5) { _opaqueTypes.Add(u5.Name); return u5.Name; }
            if (type is CppClass c)
            {
                var className = string.IsNullOrWhiteSpace(c.Name) ? "__Anonymous" + Math.Abs(c.GetHashCode()) : mappings.TryGetValue(c.Name, out var cm) ? cm : _typeAliases.TryGetValue(c.Name, out var alias) ? alias : c.Name;
                if (!c.IsDefinition) _opaqueTypes.Add(className);
                return className;
            }
            var displayName = type.GetDisplayName();
            if (type is CppPrimitiveType ptype)
            {
                var primitiveName = Primitive(ptype.Kind);
                return mappings.TryGetValue(displayName, out var dm) ? dm : mappings.TryGetValue(primitiveName, out var pm) ? pm : primitiveName;
            }
            return mappings.TryGetValue(displayName, out var mappedDisplayName) ? mappedDisplayName : displayName;
        }
        static bool IsFunctionPointer(CppType? type) => UnwrapFunctionPointer(type) is not null;
        static CppFunctionType? UnwrapFunctionPointer(CppType? type)
        {
            type = Strip(type);
            return type switch { CppFunctionType f => f, CppPointerType p => UnwrapFunctionPointer(p.ElementType), CppQualifiedType q => UnwrapFunctionPointer(q.ElementType), _ => null };
        }

        static string? TypedefTargetName(CppTypedef typedef)
        {
            var target = Strip(typedef.ElementType);
            return target switch
            {
                CppClass c when !string.IsNullOrWhiteSpace(c.Name) => c.Name,
                CppEnum e when !string.IsNullOrWhiteSpace(e.Name) => e.Name,
                CppUnexposedType u when !string.IsNullOrWhiteSpace(u.Name) => u.Name,
                _ => null
            };
        }


        static string? NativeName(CppType? type) => Strip(type) switch
        {
            CppTypedef t => t.Name,
            CppClass c => c.Name,
            CppEnum e => e.Name,
            CppUnexposedType u => u.Name,
            _ => null
        };

        static CppType? Strip(CppType? type) => type is CppQualifiedType q ? Strip(q.ElementType) : type;
        static string Primitive(CppPrimitiveKind k) => k switch { CppPrimitiveKind.Void => "void", CppPrimitiveKind.Bool => "Bool1Byte", CppPrimitiveKind.Char => "sbyte", CppPrimitiveKind.UnsignedChar => "byte", CppPrimitiveKind.Short => "short", CppPrimitiveKind.UnsignedShort => "ushort", CppPrimitiveKind.Int => "int", CppPrimitiveKind.UnsignedInt => "uint", CppPrimitiveKind.Long or CppPrimitiveKind.LongLong => "long", CppPrimitiveKind.UnsignedLong or CppPrimitiveKind.UnsignedLongLong => "ulong", CppPrimitiveKind.Float => "float", CppPrimitiveKind.Double => "double", _ => "IntPtr" };
        static string PointerWrapperName(string pointerType) => pointerType.Replace("*", "_ptr").Replace("::", "_").Replace(".", "_");

        static string Safe(string name) => name is "event" or "string" or "params" or "base" ? "@" + name : string.IsNullOrWhiteSpace(name) ? "_" : name;
    }
}

