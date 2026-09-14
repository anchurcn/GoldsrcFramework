using CppAst;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Resolves CppAst types to C# type names.
/// The two resolution modes of the former duplicate implementations are unified here:
/// <see cref="Name"/> produces raw ABI names, <see cref="HumanizedName"/> additionally
/// consults the [types] renaming rules before the [typeAliases] substitution rules.
/// </summary>
internal sealed class TypeNameResolver
{
    readonly CppCompilation _compilation;
    readonly HumanizerRules _rules;
    readonly HashSet<string> _opaqueTypes;

    readonly Dictionary<string, CppTypedef> _typedefs;
    readonly Dictionary<string, CppClass> _classes;
    readonly Dictionary<string, CppEnum> _enums;
    readonly Dictionary<string, string> _typeAliases;

    public TypeNameResolver(CppCompilation compilation, HumanizerRules rules, HashSet<string> opaqueTypes)
    {
        _compilation = compilation;
        _rules = rules;
        _opaqueTypes = opaqueTypes;

        _typedefs = compilation.Typedefs.ToDictionary(t => t.Name, t => t);
        _classes = compilation.Classes.Where(c => !string.IsNullOrWhiteSpace(c.Name)).GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.FirstOrDefault(c => c.IsDefinition) ?? g.First());
        _enums = compilation.Enums.Where(e => !string.IsNullOrWhiteSpace(e.Name)).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First());
        _typeAliases = compilation.Typedefs
            .Select(t => (Target: CppAstHelpers.TypedefTargetName(t), Alias: t.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x.Target))
            .GroupBy(x => x.Target!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Alias, StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, CppTypedef> Typedefs => _typedefs;
    public Dictionary<string, CppClass> Classes => _classes;
    public Dictionary<string, CppEnum> Enums => _enums;

    public CppCompilation Compilation => _compilation;

    /// <summary>Raw ABI type name (used for HlsdkNative.generated.cs).</summary>
    public string Name(CppType type, bool field) => Resolve(type, field, humanized: false);

    /// <summary>Humanized type name (used for the friendly-named layer).</summary>
    public string HumanizedName(CppType type, bool field) => Resolve(type, field, humanized: true);

    /// <summary>Typedef alias that names a class/enum type, if one exists.</summary>
    public string? TypeAlias(string name) => _typeAliases.TryGetValue(name, out var alias) ? alias : null;

    /// <summary>[typeAliases] substitution for a C type name, or null.</summary>
    public string? Substitution(string name) => _rules.Lookup(HumanizerSection.TypeAliases, name, name);

    /// <summary>Whether the type name is substituted by a [typeAliases] rule (and therefore not emitted).</summary>
    public bool IsSubstituted(string? name) => !string.IsNullOrEmpty(name) && _rules.Match(HumanizerSection.TypeAliases, name) is not null;

    /// <summary>[types] rename for a declared symbol name, or null.</summary>
    public string? Renamed(string name, string? nativeName = null) => _rules.Lookup(HumanizerSection.Types, name, name, nativeName);

    string Resolve(CppType type, bool field, bool humanized)
    {
        type = CppAstHelpers.Strip(type)!;

        // Handle typedef
        if (type is CppTypedef td)
        {
            if (humanized && Renamed(td.Name) is { } tdMapped)
                return tdMapped;
            if (Substitution(td.Name) is { } m)
                return m;
            if (CppAstHelpers.IsFunctionPointer(td.ElementType))
                return Resolve(CppAstHelpers.UnwrapFunctionPointer(td.ElementType)!, field, humanized);
            if (td.ElementType is CppClass or CppEnum or CppUnexposedType)
                return Resolve(td.ElementType, field, humanized);
            return td.Name;
        }

        // Handle pointer type - recursively resolve element type
        if (type is CppPointerType p)
        {
            if (CppAstHelpers.IsFunctionPointer(p.ElementType))
                return Resolve(p.ElementType, field, humanized);
            return Resolve(p.ElementType, field, humanized) + "*";
        }

        // Handle array type
        if (type is CppArrayType a)
            return Resolve(a.ElementType, field, humanized) + "*";

        // Handle function type - recursively resolve parameters and return type
        if (type is CppFunctionType fn)
            return "delegate* unmanaged[Cdecl]<" + string.Join(", ", fn.Parameters.Select(p => Resolve(p.Type, field, humanized)).Append(Resolve(fn.ReturnType, field, humanized))) + ">";

        // Handle enum
        if (type is CppEnum e)
        {
            if (string.IsNullOrWhiteSpace(e.Name)) return "int";
            if (humanized && Renamed(e.Name) is { } eMapped) return eMapped;
            if (Substitution(e.Name) is { } em) return em;
            return TypeAlias(e.Name) ?? e.Name;
        }

        // Handle CppUnexposedType
        if (type is CppUnexposedType u && _typedefs.TryGetValue(u.Name, out var resolved))
            return Resolve(resolved, field, humanized);
        if (type is CppUnexposedType u3 && _classes.TryGetValue(u3.Name, out var resolvedClass))
            return Resolve(resolvedClass, field, humanized);
        if (type is CppUnexposedType u4 && _enums.TryGetValue(u4.Name, out var resolvedEnum))
            return Resolve(resolvedEnum, field, humanized);
        if (type is CppUnexposedType u2)
        {
            if (humanized && Renamed(u2.Name) is { } uMapped) return uMapped;
            if (Substitution(u2.Name) is { } um) return um;
            _opaqueTypes.Add(u2.Name);
            return u2.Name;
        }

        // Handle CppClass
        if (type is CppClass c)
        {
            var className = string.IsNullOrWhiteSpace(c.Name) ? CppAstHelpers.AnonymousClassName(c) : c.Name;

            if (humanized)
            {
                // First check if the class name itself is renamed by a [types] rule
                if (Renamed(className) is { } cMapped) return cMapped;

                // Then check if there's a typedef alias for this class
                var aliasName = TypeAlias(className) ?? className;

                if (aliasName != className && Renamed(aliasName) is { } aliasMapped) return aliasMapped;
                if (Substitution(className) is { } cm) return cm;
                if (aliasName != className && Substitution(aliasName) is { } am) return am;

                if (!c.IsDefinition) _opaqueTypes.Add(aliasName);
                return aliasName;
            }

            var resolvedName = Substitution(c.Name) ?? TypeAlias(c.Name) ?? className;
            if (!c.IsDefinition) _opaqueTypes.Add(resolvedName);
            return resolvedName;
        }

        // Handle primitive types
        var displayName = type.GetDisplayName();
        if (type is CppPrimitiveType ptype)
        {
            var primitiveName = CppAstHelpers.Primitive(ptype.Kind);
            return Substitution(displayName) ?? Substitution(primitiveName) ?? primitiveName;
        }

        return Substitution(displayName) ?? displayName;
    }
}
