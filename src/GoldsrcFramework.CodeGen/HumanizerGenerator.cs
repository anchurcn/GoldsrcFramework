using CppAst;
using System.Text;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Generates the humanized (friendly-named) P/Invoke layer on top of the raw ABI graph
/// produced by <see cref="RawAbiGenerator"/>. Writes one file per source header folder,
/// a file map report, a symbol catalog, and a manifest used to clean up stale files on
/// the next run.
///
/// The generator contains no per-symbol special cases: everything that used to be a
/// hardcoded adjustment (name prefixes, file placement, field types, special layouts)
/// is expressed as a rule in humanizer.rules and applied through <see cref="HumanizerRules"/>.
/// </summary>
internal sealed class HumanizerGenerator
{
    const string PointerWrapperPath = "Core/PointerWrappers.cs";

    readonly RawAbiGenerator _generator;
    readonly HumanizerRules _rules;

    /// <summary>Every symbol the humanizer emitted, with the rule that produced it.</summary>
    public HumanizerSymbolCatalog Catalog { get; } = new();

    public HumanizerGenerator(RawAbiGenerator generator)
    {
        _generator = generator;
        _rules = generator.Rules;
    }

    /// <summary>
    /// Plans all humanizer output files (relative to the Native directory) without
    /// touching the file system, so callers can write, dry-run or verify them.
    /// </summary>
    public IReadOnlyList<(string RelativePath, string Content)> PlanHumanizerFiles()
    {
        var fileMap = new List<(string RawType, string HumanizedType, string RelativePath, string Reason, string SourceHeader)>();
        var writtenFiles = new List<string>();
        var files = new Dictionary<string, (StringBuilder Builder, HashSet<string> Emitted)>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in HumanizerTypes())
        {
            if (!TryGetHumanizerInfo(type, out var info)) continue;
            var file = GetOrCreateHumanizerFile(files, info.RelativePath);
            if (!file.Emitted.Add(info.HumanizedName)) continue;
            EmitHumanizedType(file.Builder, type, info);
            fileMap.Add((info.RawName, info.HumanizedName, info.RelativePath, info.Reason, info.SourceHeader));
            Catalog.Add(KindOf(type), info.RawName, info.RawName, info.HumanizedName, info.TypeRuleSource, info.RelativePath);
        }

        if (_generator.PointerWrappers.Count > 0)
        {
            var file = GetOrCreateHumanizerFile(files, PointerWrapperPath);
            foreach (var wrapper in _generator.PointerWrappers.OrderBy(x => x.Key))
            {
                if (!file.Emitted.Add(wrapper.Key)) continue;
                file.Builder.Append("[StructLayout(LayoutKind.Sequential)]\npublic unsafe struct ")
                    .Append(wrapper.Key).Append(" { public ").Append(wrapper.Value).AppendLine(" Value; }\n");
                fileMap.Add((wrapper.Key, wrapper.Key, PointerWrapperPath, "generated-pointer-wrapper", string.Empty));
                Catalog.Add("PointerWrapper", wrapper.Key, wrapper.Value, wrapper.Key, "generated", PointerWrapperPath);
            }
        }

        var outputs = new List<(string RelativePath, string Content)>();
        foreach (var file in files.OrderBy(x => x.Key))
        {
            outputs.Add((file.Key, file.Value.Builder.ToString()));
            writtenFiles.Add(file.Key.Replace('\\', '/'));
        }

        outputs.Add(("Generated/HlsdkNative.filemap.generated.md", CreateFileMapReport(fileMap)));
        outputs.Add(("Generated/HlsdkNative.humanizer.files.txt", string.Join(Environment.NewLine, writtenFiles.Order(StringComparer.OrdinalIgnoreCase)) + Environment.NewLine));
        outputs.Add(("Generated/HlsdkNative.humanizer.symbols.md", Catalog.ToMarkdown(_rules.UnusedRules, _rules.AllRules.Count)));
        return outputs;
    }

    static string KindOf(CppType type) => CppAstHelpers.Strip(type) switch
    {
        CppEnum => "Enum",
        CppTypedef => "Typedef",
        _ => "Struct",
    };

    static string HumanizerFilesManifestPath(string nativeDir) => Path.Combine(nativeDir, "Generated", "HlsdkNative.humanizer.files.txt");

    /// <summary>
    /// Deletes files listed in the humanizer manifest so a subsequent write does not
    /// leave files behind for types that are no longer generated.
    /// </summary>
    public static void DeletePreviousHumanizerFiles(string nativeDir)
    {
        var manifest = HumanizerFilesManifestPath(nativeDir);
        if (!File.Exists(manifest))
        {
            DeleteBootstrapHumanizerFiles(nativeDir);
            return;
        }

        foreach (var line in File.ReadAllLines(manifest))
        {
            var relative = line.Trim();
            if (string.IsNullOrWhiteSpace(relative)) continue;
            var fullPath = Path.GetFullPath(Path.Combine(nativeDir, CppAstHelpers.NormalizeRelativePath(relative)));
            if (!fullPath.StartsWith(Path.GetFullPath(nativeDir), StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }

    static void DeleteBootstrapHumanizerFiles(string nativeDir)
    {
        // One-time cleanup for files generated before the manifest existed.
        foreach (var directory in new[] { "Client", "Core", "Entity", "Input", "Misc", "Model", "PlayerMove", "Resource", "SaveRestore", "Studio", "Text" })
        {
            var fullPath = Path.Combine(nativeDir, directory);
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
        }

        foreach (var fileName in new[] { "ClientEngineFuncs.cs", "ClientExportFuncs.cs", "ServerEngineFuncs.cs", "ServerExportFuncs.cs", "ServerNewExportFuncs.cs" })
        {
            var fullPath = Path.Combine(nativeDir, fileName);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }

    (StringBuilder Builder, HashSet<string> Emitted) GetOrCreateHumanizerFile(Dictionary<string, (StringBuilder Builder, HashSet<string> Emitted)> files, string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/');
        if (files.TryGetValue(relativePath, out var file)) return file;
        file = (new StringBuilder(FileHeader()), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        files[relativePath] = file;
        return file;
    }

    /// <summary>File header built from the [defaults] section of the rules file.</summary>
    string FileHeader()
    {
        var ns = _rules.Default("namespace") ?? "GoldsrcFramework.Engine.Native";
        var usings = (_rules.Default("usings") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (usings.Length == 0) usings = ["System", "System.Runtime.InteropServices", "GoldsrcFramework.LinearMath", "NativeInterop"];

        var sb = new StringBuilder("// <auto-generated/>\n");
        foreach (var use in usings) sb.Append("using ").Append(use).Append(";\n");
        sb.Append("\nnamespace ").Append(ns).Append(";\n\n");
        return sb.ToString();
    }

    /// <summary>Everything needed to emit one humanized type.</summary>
    readonly struct HumanizerTypeInfo
    {
        public required string RawName { get; init; }
        public required string HumanizedName { get; init; }
        public required string RelativePath { get; init; }
        public required string Reason { get; init; }
        public required string SourceHeader { get; init; }
        public required string EmitStrategy { get; init; }
        public required string TypeRuleSource { get; init; }
    }

    bool TryGetHumanizerInfo(CppType type, out HumanizerTypeInfo info)
    {
        type = CppAstHelpers.Strip(type)!;
        info = default!;
        var sourceHeader = (type as CppElement)?.SourceFile ?? string.Empty;

        // Primitive typedef wrappers (HSPRITE, qboolean, string_t, ...)
        if (type is CppTypedef td && CppAstHelpers.Strip(td.ElementType) is CppPrimitiveType)
        {
            if (ShouldEmitHumanizedPrimitiveTypedef(td))
                info = CreateInfo(td.Name, null, sourceHeader);
            return info.RawName is not null;
        }

        if (type is CppEnum e)
        {
            info = CreateInfo(_generator.Resolver.Name(e, false), e.Name, sourceHeader);
            return info.RawName is not null;
        }

        if (type is CppClass c && c.IsDefinition)
        {
            var candidate = CreateInfo(_generator.Resolver.Name(c, false), c.Name, sourceHeader);
            if (candidate.RawName is null) return false;

            // Types with anonymous union members cannot be expressed as a plain struct; they
            // need an explicit emission strategy in [emit] (e.g. union-anim-value) to be emitted.
            if (HasUnsupportedAnonymousField(c) && candidate.EmitStrategy != HumanizerEmitStrategies.UnionAnimValue)
                return false;

            info = candidate;
            return true;
        }

        return false;
    }

    HumanizerTypeInfo CreateInfo(string rawName, string? nativeName, string sourceHeader)
    {
        var renameRule = _rules.Match(HumanizerSection.Types, rawName, nativeName);
        var humanizedName = renameRule is null ? rawName : HumanizerRules.ExpandTemplate(renameRule.Value, rawName);
        var strategy = _rules.Lookup(HumanizerSection.Emit, rawName, rawName, nativeName) ?? HumanizerEmitStrategies.Default;

        return new HumanizerTypeInfo
        {
            RawName = strategy == HumanizerEmitStrategies.Skip ? string.Empty : rawName,
            HumanizedName = humanizedName,
            RelativePath = ResolveHumanizerRelativePath(rawName, nativeName, humanizedName, sourceHeader, out var reason),
            Reason = reason,
            SourceHeader = sourceHeader,
            EmitStrategy = strategy,
            TypeRuleSource = renameRule?.Location ?? "default",
        };
    }

    IEnumerable<CppType> HumanizerTypes()
    {
        // Humanizer should expose the ABI dependency graph rooted at the engine/client
        // function tables and exported API entry points. It must not scan every parsed
        // declaration just because it exists in the translation unit.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in _generator.Ordered)
        {
            var key = HumanizerTypeKey(type);
            if (key is not null && seen.Add(key)) yield return type;
        }
    }

    string? HumanizerTypeKey(CppType type)
    {
        type = CppAstHelpers.Strip(type)!;
        return type switch
        {
            CppTypedef td => td.Name,
            CppClass c when c.IsDefinition => _generator.Resolver.Name(c, false),
            CppEnum e => _generator.Resolver.Name(e, false),
            _ => null,
        };
    }

    bool ShouldEmitHumanizedPrimitiveTypedef(CppTypedef typedef)
    {
        var name = typedef.Name;
        if (_generator.Resolver.IsSubstituted(name)) return false;
        if (CppAstHelpers.CSharpKeywords.Contains(name)) return false;
        return true;
    }

    static bool HasUnsupportedAnonymousField(CppClass c)
    {
        return c.Fields.Any(f => CsTypeNameStatic(CppAstHelpers.Strip(f.Type)!).StartsWith("__Anonymous", StringComparison.Ordinal));
    }

    static string CsTypeNameStatic(CppType type)
    {
        type = CppAstHelpers.Strip(type)!;
        return type switch
        {
            CppClass c => c.Name,
            CppEnum e => e.Name,
            CppTypedef td => td.Name,
            CppUnexposedType u => u.Name,
            CppArrayType a => CsTypeNameStatic(a.ElementType),
            CppPointerType p => CsTypeNameStatic(p.ElementType),
            _ => string.Empty,
        };
    }

    string ResolveHumanizerRelativePath(string rawName, string? nativeName, string humanizedName, string sourceHeader, out string reason)
    {
        if (_rules.Lookup(HumanizerSection.Files, humanizedName, rawName, nativeName) is { } explicitPath)
        {
            reason = "explicit-humanizer-file";
            return explicitPath.Replace('\\', '/');
        }

        reason = "fallback-header-folder";
        var template = _rules.Default("file") ?? "{header}/{name}.cs";
        var expanded = template
            .Replace("{header}", HeaderFolderName(sourceHeader), StringComparison.Ordinal)
            .Replace("{name}", CppAstHelpers.SafeFileName(humanizedName), StringComparison.Ordinal);
        return expanded.Replace('\\', '/');
    }

    static string HeaderFolderName(string? sourceHeader)
    {
        if (string.IsNullOrWhiteSpace(sourceHeader)) return "Misc";

        var normalized = sourceHeader.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(normalized);
        if (fileName.EndsWith(".h", StringComparison.OrdinalIgnoreCase)) fileName = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(fileName) ? "Misc" : CppAstHelpers.SafeFileName(fileName);
    }

    void EmitHumanizedType(StringBuilder sb, CppType type, HumanizerTypeInfo info)
    {
        type = CppAstHelpers.Strip(type)!;
        if (info.EmitStrategy == HumanizerEmitStrategies.UnionAnimValue && type is CppClass unionClass)
        {
            EmitMStudioAnimValue(sb, unionClass, info.HumanizedName);
            return;
        }
        if (type is CppTypedef td && CppAstHelpers.Strip(td.ElementType) is CppPrimitiveType) { EmitHumanizedPrimitiveTypedef(sb, td, info.HumanizedName); return; }
        if (type is CppEnum e) { EmitHumanizedEnum(sb, e, info); return; }
        if (type is CppClass c && c.IsDefinition) EmitHumanizedClass(sb, c, info);
    }

    static string CreateFileMapReport(IEnumerable<(string RawType, string HumanizedType, string RelativePath, string Reason, string SourceHeader)> rows)
    {
        var sb = new StringBuilder("# HLSDK Native Humanizer File Map\n\n| Raw Type | Humanized Type | Target | Reason | Source Header |\n|---|---|---|---|---|\n");
        foreach (var row in rows.OrderBy(r => r.RelativePath).ThenBy(r => r.RawType))
        {
            sb.Append("| `").Append(row.RawType).Append("` | `").Append(row.HumanizedType).Append("` | `").Append(row.RelativePath.Replace('\\', '/')).Append("` | ").Append(row.Reason).Append(" | `").Append(row.SourceHeader.Replace('\\', '/')).AppendLine("` |");
        }
        return sb.ToString();
    }

    void EmitHumanizedClass(StringBuilder sb, CppClass c, HumanizerTypeInfo info)
    {
        EmitHumanizerXmlDocumentation(sb, c, "");
        sb.AppendLine("[StructLayout(LayoutKind.Sequential)]").Append("public unsafe struct ").AppendLine(info.HumanizedName).AppendLine("{");
        foreach (var f in c.Fields)
        {
            var fieldName = HumanizedFieldName(info, f);
            var fieldType = HumanizedFieldType(info, f, fieldName, out var parameterDocs);
            EmitHumanizerXmlDocumentation(sb, f, "    ", parameterDocs);
            sb.Append("    public ").Append(fieldType).Append(' ').Append(CppAstHelpers.Safe(fieldName)).AppendLine(";");
        }
        sb.AppendLine("}\n");
    }

    void EmitMStudioAnimValue(StringBuilder sb, CppClass c, string humanizedName)
    {
        EmitHumanizerXmlDocumentation(sb, c, "");
        sb.AppendLine("[StructLayout(LayoutKind.Sequential)]")
          .Append("public struct ").Append(humanizedName).AppendLine("_num_t")
          .AppendLine("{")
          .AppendLine("    public byte valid;")
          .AppendLine("    public byte total;")
          .AppendLine("}\n");

        sb.AppendLine("[StructLayout(LayoutKind.Explicit)]")
          .Append("public struct ").AppendLine(humanizedName)
          .AppendLine("{")
          .Append("    [FieldOffset(0)] public ").Append(humanizedName).AppendLine("_num_t num;")
          .AppendLine("    [FieldOffset(0)] public short value;")
          .AppendLine("}\n");
    }

    void EmitHumanizedPrimitiveTypedef(StringBuilder sb, CppTypedef typedef, string humanizedName)
    {
        EmitHumanizerXmlDocumentation(sb, typedef, "");
        sb.AppendLine("[StructLayout(LayoutKind.Sequential)]")
          .Append("public struct ").Append(humanizedName).Append(" { public ")
          .Append(_generator.Resolver.Name(typedef.ElementType, false)).AppendLine(" Value; }\n");
    }

    void EmitHumanizedEnum(StringBuilder sb, CppEnum e, HumanizerTypeInfo info)
    {
        EmitHumanizerXmlDocumentation(sb, e, "");
        sb.Append("public enum ").AppendLine(info.HumanizedName).AppendLine("{");
        foreach (var item in e.Items)
        {
            var rule = _rules.Match(HumanizerSection.EnumMembers, HumanizerRules.RawPath(info.RawName, item.Name), HumanizerRules.HumanizedPath(info.HumanizedName, item.Name));
            var name = rule is null ? item.Name : HumanizerRules.ExpandTemplate(rule.Value, item.Name);
            Catalog.Add("EnumMember", HumanizerRules.RawPath(info.RawName, item.Name), item.Name, name, rule?.Location ?? "default", info.RelativePath);
            sb.Append("    ").Append(name).Append(" = ").Append(item.Value).AppendLine(",");
        }
        sb.AppendLine("}\n");
    }

    void EmitHumanizerXmlDocumentation(StringBuilder sb, CppElement element, string indent, IReadOnlyList<string>? parameterDocs = null)
    {
        var summaryLines = _generator.GetCommentLines(element).ToList();
        var remarkLines = new List<string>();
        var original = _generator.GetOriginalDeclarationLine(element);
        if (!string.IsNullOrWhiteSpace(original)) remarkLines.Add("Original: " + original);
        if (element is CppField field && TryGetFunctionPointer(field.Type, out _, out var typedef, out _) && typedef is not null)
        {
            var aliasOriginal = _generator.GetOriginalDeclarationLine(typedef);
            if (!string.IsNullOrWhiteSpace(aliasOriginal)) remarkLines.Add("Alias: " + aliasOriginal);
        }
        if (parameterDocs is { Count: > 0 }) remarkLines.Add("Parameters: " + string.Join(", ", parameterDocs));

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

    /// <summary>[fields] lookup: raw type name first (canonical), humanized name as a convenience.</summary>
    string HumanizedFieldName(HumanizerTypeInfo info, CppField field)
    {
        var rule = _rules.Match(HumanizerSection.Fields, HumanizerRules.RawPath(info.RawName, field.Name), HumanizerRules.HumanizedPath(info.HumanizedName, field.Name));
        var name = rule is null ? field.Name : HumanizerRules.ExpandTemplate(rule.Value, field.Name);
        if (string.IsNullOrWhiteSpace(name)) name = field.Name;
        Catalog.Add("Field", HumanizerRules.RawPath(info.RawName, field.Name), field.Name, name, rule?.Location ?? "default", info.RelativePath);
        return name;
    }

    string HumanizedFieldType(HumanizerTypeInfo info, CppField field, string fieldName, out IReadOnlyList<string>? parameterDocs)
    {
        parameterDocs = null;
        var shape = CppAstHelpers.RawShape(field.Type);
        var overrideRule = _rules.Match(HumanizerSection.FieldTypes, HumanizerRules.RawPath(info.RawName, field.Name), HumanizerRules.HumanizedPath(info.HumanizedName, field.Name));
        if (overrideRule is not null && HumanizerRules.ConditionMatches(overrideRule.Condition, shape))
        {
            var overridden = HumanizerRules.ExpandTemplate(overrideRule.Value, field.Name);
            Catalog.Add("FieldType", HumanizerRules.RawPath(info.RawName, field.Name), shape, overridden, overrideRule.Location, info.RelativePath);
            return overridden;
        }

        if (TryGetFunctionPointer(field.Type, out var function, out var typedef, out _))
        {
            var signature = HumanizedFunctionPointerSignature(info, field, fieldName, function, typedef, out parameterDocs);
            if (signature is not null) return signature;
        }

        if (CppAstHelpers.Strip(field.Type) is CppArrayType array) return HumanizedArrayFieldType(info, field, array);
        return _generator.Resolver.HumanizedName(field.Type, true);
    }

    /// <summary>
    /// Builds the humanized function pointer signature, applying [paramTypes] overrides and
    /// collecting the parameter names documented by [params].
    ///
    /// Returns null when no parameter rule applies: the shared type resolver output is then
    /// used, which keeps untouched signatures identical to the raw ABI shape.
    ///
    /// Note: C# does not accept parameter names inside a delegate*&lt;...&gt; type, so parameter
    /// renames surface in the generated XML documentation instead of in the signature.
    /// </summary>
    string? HumanizedFunctionPointerSignature(HumanizerTypeInfo info, CppField field, string fieldName, CppFunctionType function, CppTypedef? typedef, out IReadOnlyList<string>? parameterDocs)
    {
        var prefixes = new List<string?>
        {
            HumanizerRules.RawPath(info.RawName, field.Name),
            HumanizerRules.HumanizedPath(info.HumanizedName, field.Name),
            HumanizerRules.HumanizedPath(info.HumanizedName, fieldName),
            typedef?.Name,
        };

        var overrides = new Dictionary<int, string>();
        var parameterNames = new List<string>(function.Parameters.Count);
        var anyNameRule = false;

        for (var i = 0; i < function.Parameters.Count; i++)
        {
            var parameter = function.Parameters[i];
            var keys = ParameterKeys(prefixes, parameter.Name, i);
            var subject = string.IsNullOrWhiteSpace(parameter.Name) ? $"#{i}" : parameter.Name;

            var typeRule = _rules.Match(HumanizerSection.ParameterTypes, keys);
            var parameterType = typeRule is null
                ? _generator.Resolver.HumanizedName(parameter.Type, true)
                : HumanizerRules.ExpandTemplate(typeRule.Value, subject);
            if (typeRule is not null) overrides[i] = parameterType;
            Catalog.Add("Parameter", keys[0]!, $"{CppAstHelpers.RawShape(parameter.Type)} {parameter.Name}".Trim(), parameterType, typeRule?.Location ?? "default", info.RelativePath);

            var nameRule = _rules.Match(HumanizerSection.Parameters, keys);
            if (nameRule is not null) anyNameRule = true;
            var parameterName = nameRule is null ? parameter.Name : HumanizerRules.ExpandTemplate(nameRule.Value, subject);
            parameterNames.Add(string.IsNullOrWhiteSpace(parameterName) ? parameterType : $"{parameterType} {parameterName}");
        }

        parameterDocs = anyNameRule ? parameterNames : null;
        if (overrides.Count == 0) return null;

        var parts = new List<string>(function.Parameters.Count + 1);
        for (var i = 0; i < function.Parameters.Count; i++)
            parts.Add(overrides.TryGetValue(i, out var overridden) ? overridden : _generator.Resolver.HumanizedName(function.Parameters[i].Type, true));
        parts.Add(_generator.Resolver.HumanizedName(function.ReturnType, true));
        return "delegate* unmanaged[Cdecl]<" + string.Join(", ", parts) + ">";
    }

    static string?[] ParameterKeys(List<string?> prefixes, string? parameterName, int index)
    {
        var keys = new List<string?>(prefixes.Count * 2);
        foreach (var prefix in prefixes)
        {
            if (string.IsNullOrEmpty(prefix)) continue;
            if (!string.IsNullOrWhiteSpace(parameterName)) keys.Add($"{prefix}.{parameterName}");
            keys.Add($"{prefix}.#{index}");
        }
        return [.. keys];
    }

    /// <summary>Unwraps the function type behind a function pointer field (through its typedef).</summary>
    static bool TryGetFunctionPointer(CppType type, out CppFunctionType function, out CppTypedef? typedef, out CppType target)
    {
        type = CppAstHelpers.Strip(type)!;
        typedef = null;
        if (type is CppTypedef t)
        {
            typedef = t;
            type = CppAstHelpers.Strip(t.ElementType)!;
        }

        function = CppAstHelpers.UnwrapFunctionPointer(type)!;
        target = type;
        return function is not null;
    }

    string HumanizedArrayFieldType(HumanizerTypeInfo info, CppField field, CppArrayType array)
    {
        var element = CppAstHelpers.Strip(array.ElementType)!;
        if (element is CppArrayType nested) return $"InlineArray{array.Size}<" + HumanizedArrayFieldType(info, field, nested) + ">";
        if (element is CppPointerType pointer)
        {
            var pointerType = _generator.Resolver.HumanizedName(pointer, true);
            var wrapperName = CppAstHelpers.PointerWrapperName(pointerType);
            return $"InlineArray{array.Size}<{wrapperName}>";
        }
        return $"InlineArray{array.Size}<" + _generator.Resolver.HumanizedName(element, false) + ">";
    }
}
