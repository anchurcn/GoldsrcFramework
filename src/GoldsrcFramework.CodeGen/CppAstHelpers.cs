using CppAst;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Shared CppAst helper utilities used by both the raw ABI generator and the humanizer generator.
/// </summary>
internal static class CppAstHelpers
{
    /// <summary>Identifier keywords of C# that must not be emitted as identifiers.</summary>
    public static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
        "float", "double", "decimal", "char", "string", "object", "void"
    };

    /// <summary>
    /// Reserved C# keywords that need escaping when a C++ name is emitted as a C#
    /// identifier. Used by <see cref="Safe"/> for members and by
    /// <see cref="SafeParameterName"/> for function pointer parameters.
    /// </summary>
    public static readonly HashSet<string> CSharpReservedKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    public static CppType? Strip(CppType? type) => type is CppQualifiedType q ? Strip(q.ElementType) : type;

    public static bool IsFunctionPointer(CppType? type) => UnwrapFunctionPointer(type) is not null;

    public static CppFunctionType? UnwrapFunctionPointer(CppType? type)
    {
        type = Strip(type);
        return type switch
        {
            CppFunctionType f => f,
            CppPointerType p => UnwrapFunctionPointer(p.ElementType),
            CppQualifiedType q => UnwrapFunctionPointer(q.ElementType),
            _ => null
        };
    }

    public static string? NativeName(CppType? type) => Strip(type) switch
    {
        CppTypedef t => t.Name,
        CppClass c => c.Name,
        CppEnum e => e.Name,
        CppUnexposedType u => u.Name,
        _ => null
    };

    public static string? TypedefTargetName(CppTypedef typedef)
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

    public static string Primitive(CppPrimitiveKind k) => k switch
    {
        CppPrimitiveKind.Void => "void",
        CppPrimitiveKind.Bool => "Bool1Byte",
        CppPrimitiveKind.Char => "sbyte",
        CppPrimitiveKind.UnsignedChar => "byte",
        CppPrimitiveKind.Short => "short",
        CppPrimitiveKind.UnsignedShort => "ushort",
        CppPrimitiveKind.Int => "int",
        CppPrimitiveKind.UnsignedInt => "uint",
        CppPrimitiveKind.Long or CppPrimitiveKind.LongLong => "long",
        CppPrimitiveKind.UnsignedLong or CppPrimitiveKind.UnsignedLongLong => "ulong",
        CppPrimitiveKind.Float => "float",
        CppPrimitiveKind.Double => "double",
        _ => "IntPtr"
    };

    public static string PointerWrapperName(string pointerType) => pointerType.Replace("*", "_ptr").Replace("::", "_").Replace(".", "_");

    public static string Safe(string name) => string.IsNullOrWhiteSpace(name)
        ? "_"
        : CSharpReservedKeywords.Contains(name) ? "@" + name : name;

    /// <summary>
    /// The raw C shape of a type as used by the rules file guards, e.g. "float[3]",
    /// "char*", "int", "vec3_t".
    /// </summary>
    public static string RawShape(CppType type)
    {
        type = Strip(type)!;
        return type switch
        {
            CppArrayType a => RawShape(a.ElementType) + "[" + a.Size + "]",
            CppPointerType p => RawShape(p.ElementType) + "*",
            CppPrimitiveType prim => RawPrimitiveName(prim.Kind),
            CppTypedef t => t.Name,
            CppClass c => c.Name,
            CppEnum e => e.Name,
            CppUnexposedType u => u.Name,
            _ => type.GetDisplayName(),
        };
    }

    static string RawPrimitiveName(CppPrimitiveKind kind) => kind switch
    {
        CppPrimitiveKind.Void => "void",
        CppPrimitiveKind.Bool => "bool",
        CppPrimitiveKind.Char => "char",
        CppPrimitiveKind.UnsignedChar => "unsigned char",
        CppPrimitiveKind.Short => "short",
        CppPrimitiveKind.UnsignedShort => "unsigned short",
        CppPrimitiveKind.Int => "int",
        CppPrimitiveKind.UnsignedInt => "unsigned int",
        CppPrimitiveKind.Long => "long",
        CppPrimitiveKind.UnsignedLong => "unsigned long",
        CppPrimitiveKind.LongLong => "long long",
        CppPrimitiveKind.UnsignedLongLong => "unsigned long long",
        CppPrimitiveKind.Float => "float",
        CppPrimitiveKind.Double => "double",
        _ => "int",
    };

    public static string SafeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value;
    }

    public static string NormalizeRelativePath(string path) => path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>
    /// Anonymous C++ structs/unions get a stable name derived from their source location
    /// (file + line) instead of GetHashCode(), which is randomized per process and made
    /// every regeneration produce spurious diffs.
    /// </summary>
    public static string AnonymousClassName(CppClass c)
    {
        var file = c.Span.Start.File;
        if (!string.IsNullOrWhiteSpace(file) && c.Span.Start.Line > 0)
        {
            var stem = Path.GetFileNameWithoutExtension(file).Replace('.', '_');
            return $"__Anonymous_{stem}_{c.Span.Start.Line}";
        }
        return "__Anonymous";
    }
}
