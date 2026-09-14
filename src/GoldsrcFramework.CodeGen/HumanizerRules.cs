using CppAst;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Sections of the unified humanizer rules file. Each section is a table of
/// "pattern = value" rows; see humanizer.rules for the authoritative documentation.
/// </summary>
internal enum HumanizerSection
{
    /// <summary>File-level output settings: namespace, usings, default file template.</summary>
    Defaults,

    /// <summary>C type (builtin or typedef) to C# type substitution. Applies to both layers.</summary>
    TypeAliases,

    /// <summary>Declared symbol rename in the humanized layer.</summary>
    Types,

    /// <summary>Target file for a humanized type, relative to the Native directory.</summary>
    Files,

    /// <summary>Struct field / function table entry rename.</summary>
    Fields,

    /// <summary>Struct field type override, optionally guarded by the raw C shape.</summary>
    FieldTypes,

    /// <summary>Enum member rename.</summary>
    EnumMembers,

    /// <summary>Function pointer parameter rename.</summary>
    Parameters,

    /// <summary>Function pointer parameter type override.</summary>
    ParameterTypes,

    /// <summary>Emission strategy override for a type.</summary>
    Emit,
}

/// <summary>Emission strategies selectable from the <see cref="HumanizerSection.Emit"/> section.</summary>
internal static class HumanizerEmitStrategies
{
    /// <summary>Emit the type as a plain struct (the default).</summary>
    public const string Default = "struct";

    /// <summary>Do not emit the type in the humanized layer; the raw ABI type is used instead.</summary>
    public const string Skip = "skip";

    /// <summary>Hand-tuned layout for mstudioanimvalue_t (anonymous union of num/value).</summary>
    public const string UnionAnimValue = "union-anim-value";

    public static readonly IReadOnlyList<string> All = [Default, Skip, UnionAnimValue];
}

/// <summary>A single row of the humanizer rules file.</summary>
internal sealed class HumanizerRule
{
    public required HumanizerSection Section { get; init; }
    public required string Pattern { get; init; }
    public required string Value { get; init; }

    /// <summary>Optional "if &lt;raw shape&gt;" guard (only used by <see cref="HumanizerSection.FieldTypes"/>).</summary>
    public string? Condition { get; init; }

    /// <summary>
    /// True when the rule's comment carries the "unused-ok" marker: the rule is known not to be
    /// reached yet (e.g. a mapping kept for xash3d-only headers) and must not be reported as
    /// unmatched, so that report keeps pointing at real typos and stale entries.
    /// </summary>
    public bool AllowUnused { get; init; }

    public required int Line { get; init; }

    /// <summary>How often the rule matched during generation.</summary>
    public int HitCount { get; private set; }

    /// <summary>The symbol paths the rule matched, in match order.</summary>
    public List<string> MatchedKeys { get; } = [];

    public bool HasWildcards => Pattern.Contains('*') || Pattern.Contains('?');

    /// <summary>Patterns with more literal characters are considered more specific and win over broader ones.</summary>
    public int Specificity { get; init; }

    public string Location => $"humanizer.rules:{Line}";

    internal void RecordHit(string key)
    {
        HitCount++;
        MatchedKeys.Add(key);
    }
}

/// <summary>
/// The unified humanizer rule set: every manual adjustment made to generated symbols
/// is expressed here (renames, placement, type overrides, emission strategy), so there
/// is a single place to review and maintain them.
///
/// Lookup rules:
///   * candidate paths are tried in the order the caller supplies them (canonical
///     raw names first, humanized names as an ergonomic fallback);
///   * an exact pattern always wins over a wildcard pattern;
///   * among wildcard patterns the one with the most literal characters wins;
///     ties are broken by the order in the file.
/// </summary>
internal sealed class HumanizerRules
{
    readonly Dictionary<HumanizerSection, Dictionary<string, HumanizerRule>> _exact = [];
    readonly Dictionary<HumanizerSection, List<HumanizerRule>> _patterns = [];
    readonly List<HumanizerRule> _all = [];
    readonly List<string> _problems = [];

    public string SourcePath { get; }

    public IReadOnlyList<HumanizerRule> AllRules => _all;

    /// <summary>Structural problems found while parsing (unknown section, unknown strategy, ...).</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>Rules that never matched anything during generation (typos or stale entries).</summary>
    public IEnumerable<HumanizerRule> UnusedRules => _all.Where(r => r.HitCount == 0 && r.Section != HumanizerSection.Defaults && !r.AllowUnused);

    HumanizerRules(string sourcePath) => SourcePath = sourcePath;

    public static HumanizerRules Empty { get; } = new("(no rules file)");

    public static HumanizerRules Load(string path)
    {
        var rules = new HumanizerRules(path);
        if (!File.Exists(path))
        {
            rules._problems.Add($"Rules file not found: {path}");
            return rules;
        }

        var section = HumanizerSection.Types;
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//")) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1].Trim();
                if (!TryParseSection(name, out section))
                    rules._problems.Add($"humanizer.rules:{lineNumber}: unknown section [{name}]");
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                rules._problems.Add($"humanizer.rules:{lineNumber}: expected \"pattern = value\" (found \"{line}\")");
                continue;
            }

            var pattern = line[..separator].Trim();
            var (rawValue, comment) = SplitInlineComment(line[(separator + 1)..]);
            var value = rawValue.Trim();
            if (pattern.Length == 0 || value.Length == 0)
            {
                rules._problems.Add($"humanizer.rules:{lineNumber}: empty pattern or value");
                continue;
            }

            string? condition = null;
            if (section == HumanizerSection.FieldTypes)
            {
                var guardIndex = value.IndexOf(" if ", StringComparison.OrdinalIgnoreCase);
                if (guardIndex > 0)
                {
                    condition = value[(guardIndex + 4)..].Trim();
                    value = value[..guardIndex].Trim();
                }
            }

            rules.Add(new HumanizerRule
            {
                Section = section,
                Pattern = pattern,
                Value = value,
                Condition = condition,
                Line = lineNumber,
                Specificity = pattern.Count(c => c != '*' && c != '?'),
                AllowUnused = comment?.Contains("unused-ok", StringComparison.OrdinalIgnoreCase) == true,
            });
        }

        rules.Validate();
        return rules;
    }

    void Add(HumanizerRule rule)
    {
        _all.Add(rule);
        if (!rule.HasWildcards)
        {
            var table = _exact.TryGetValue(rule.Section, out var existing) ? existing : _exact[rule.Section] = new Dictionary<string, HumanizerRule>(StringComparer.Ordinal);
            if (!table.TryAdd(rule.Pattern, rule))
                _problems.Add($"{rule.Location}: duplicate pattern \"{rule.Pattern}\" for [{SectionName(rule.Section)}] (first declared on line {table[rule.Pattern].Line})");
            return;
        }

        var patterns = _patterns.TryGetValue(rule.Section, out var list) ? list : _patterns[rule.Section] = [];
        patterns.Add(rule);
    }

    void Validate()
    {
        if (_patterns.TryGetValue(HumanizerSection.Emit, out var emitPatterns) && emitPatterns.Count > 0)
            _problems.Add("[emit] does not support wildcard patterns; emission strategies must be assigned per type.");

        foreach (var rule in _all.Where(r => r.Section == HumanizerSection.Emit))
        {
            if (!HumanizerEmitStrategies.All.Contains(rule.Value, StringComparer.Ordinal))
                _problems.Add($"{rule.Location}: unknown emit strategy \"{rule.Value}\" (expected one of: {string.Join(", ", HumanizerEmitStrategies.All)})");
        }

        foreach (var (section, list) in _patterns)
            list.Sort((a, b) => b.Specificity != a.Specificity ? b.Specificity - a.Specificity : a.Line - b.Line);
    }

    /// <summary>Rules of one section in file order.</summary>
    public IEnumerable<HumanizerRule> RulesOf(HumanizerSection section) => _all.Where(r => r.Section == section);

    /// <summary>Finds the rule matching the first candidate key that has one, or null.</summary>
    public HumanizerRule? Match(HumanizerSection section, params string?[] keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            if (_exact.TryGetValue(section, out var table) && table.TryGetValue(key, out var exact))
            {
                exact.RecordHit(key);
                return exact;
            }
        }

        if (!_patterns.TryGetValue(section, out var patterns)) return null;
        foreach (var pattern in patterns)
        {
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key) || !IsMatch(pattern.Pattern, key)) continue;
                pattern.RecordHit(key);
                return pattern;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the renamed/overridden value for a symbol: matches a rule and expands
    /// the <c>{name}</c>/<c>{strip:P}</c>/<c>{rstrip:S}</c> placeholders against
    /// <paramref name="subject"/>. Returns null when no rule applies.
    /// </summary>
    public string? Lookup(HumanizerSection section, string subject, params string?[] keys)
    {
        var rule = Match(section, keys);
        return rule is null ? null : ExpandTemplate(rule.Value, subject);
    }

    /// <summary>Resolves a [defaults] entry, ignoring unused-rule accounting.</summary>
    public string? Default(string key) => _exact.TryGetValue(HumanizerSection.Defaults, out var table) && table.TryGetValue(key, out var rule)
        ? rule.Value
        : null;

    /// <summary>Checks a raw shape guard such as "if float[3]".</summary>
    public static bool ConditionMatches(string? condition, string? rawShape)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        if (string.IsNullOrWhiteSpace(rawShape)) return false;
        return IsMatch(condition, rawShape);
    }

    public static string SectionName(HumanizerSection section) => section switch
    {
        HumanizerSection.Defaults => "defaults",
        HumanizerSection.TypeAliases => "typeAliases",
        HumanizerSection.Types => "types",
        HumanizerSection.Files => "files",
        HumanizerSection.Fields => "fields",
        HumanizerSection.FieldTypes => "fieldTypes",
        HumanizerSection.EnumMembers => "enumMembers",
        HumanizerSection.Parameters => "params",
        HumanizerSection.ParameterTypes => "paramTypes",
        HumanizerSection.Emit => "emit",
        _ => section.ToString(),
    };

    static bool TryParseSection(string name, out HumanizerSection section)
    {
        foreach (var candidate in Enum.GetValues<HumanizerSection>())
        {
            if (string.Equals(SectionName(candidate), name, StringComparison.OrdinalIgnoreCase))
            {
                section = candidate;
                return true;
            }
        }

        section = HumanizerSection.Types;
        return false;
    }

    /// <summary>Splits a value from its trailing "# comment", when present.</summary>
    static (string Value, string? Comment) SplitInlineComment(string line)
    {
        var index = line.IndexOf(" #", StringComparison.Ordinal);
        return index < 0 ? (line, null) : (line[..index], line[(index + 1)..]);
    }

    /// <summary>Expands {name}, {strip:PREFIX} and {rstrip:SUFFIX} placeholders.</summary>
    public static string ExpandTemplate(string template, string subject)
    {
        if (!template.Contains('{')) return template;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c != '{')
            {
                result.Append(c);
                continue;
            }

            var end = template.IndexOf('}', i + 1);
            if (end < 0)
            {
                result.Append(template[i..]);
                break;
            }

            var token = template[(i + 1)..end];
            i = end;
            result.Append(token switch
            {
                "name" => subject,
                _ when token.StartsWith("strip:", StringComparison.Ordinal) => SubjectWithoutPrefix(subject, token[6..]),
                _ when token.StartsWith("rstrip:", StringComparison.Ordinal) => SubjectWithoutSuffix(subject, token[7..]),
                _ => "{" + token + "}",
            });
        }

        return result.ToString();
    }

    static string SubjectWithoutPrefix(string subject, string prefix) =>
        prefix.Length > 0 && subject.StartsWith(prefix, StringComparison.Ordinal) && subject.Length > prefix.Length
            ? subject[prefix.Length..]
            : subject;

    static string SubjectWithoutSuffix(string subject, string suffix) =>
        suffix.Length > 0 && subject.EndsWith(suffix, StringComparison.Ordinal) && subject.Length > suffix.Length
            ? subject[..^suffix.Length]
            : subject;

    /// <summary>Wildcard matcher supporting '*' and '?'.</summary>
    public static bool IsMatch(string pattern, string value)
    {
        int p = 0, v = 0, starPattern = -1, starValue = 0;
        while (v < value.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == value[v]))
            {
                p++;
                v++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                starPattern = p++;
                starValue = v;
            }
            else if (starPattern >= 0)
            {
                p = starPattern + 1;
                v = ++starValue;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }

    /// <summary>
    /// Canonical raw path of a struct member: "&lt;rawType&gt;.&lt;member&gt;".
    /// Raw type names are the stable keys of the rules file: renaming a humanized type
    /// never invalidates its member rules.
    /// </summary>
    public static string RawPath(string? rawTypeName, string member) => $"{rawTypeName}.{member}";

    /// <summary>Humanized path of a struct member, accepted as an ergonomic alternative to <see cref="RawPath"/>.</summary>
    public static string HumanizedPath(string? humanizedTypeName, string member) => $"{humanizedTypeName}.{member}";
}
