using System.Globalization;
using CppAst;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// Resolves the integer value of a C macro well enough to build a C# enum out of it.
///
/// Understands the subset of C constant expressions the HLSDK headers actually use to
/// declare flag families: integer literals (decimal, hexadecimal, octal, binary, with
/// u/U/l/L/z/Z suffixes), references to other macros, parentheses, the shift and bitwise-or
/// operators, and the negating/complementing unary operators. That covers
/// <c>#define FL_FLY (1 &lt;&lt; 0)</c>, <c>#define BOUNCE_GLASS BREAK_GLASS</c> and
/// <c>#define DMG_GIB_CORPSE (DMG_CRUSH | DMG_FALL | ...)</c> as well as plain values.
///
/// Anything outside that subset - a cast, a floating point value, an arithmetic or logical
/// operator, a function-like macro, a token that cannot be resolved - is reported instead of
/// being guessed at: a wrong enum value is worse than a missing one.
/// </summary>
internal sealed class MacroValueEvaluator
{
    const int MaxDepth = 16;

    readonly Dictionary<string, CppMacro> _macros = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _conflicts = new(StringComparer.Ordinal);
    readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    public MacroValueEvaluator(IEnumerable<CppMacro> macros)
    {
        foreach (var macro in macros)
        {
            if (!_macros.TryAdd(macro.Name, macro))
            {
                if (_macros[macro.Name].Value == macro.Value) continue;

                // Historically redefined macros (or a header redefining another header's
                // macro) would silently pick one of the two values. Refuse instead.
                _conflicts.TryAdd(macro.Name, macro.Value);
            }
        }
    }

    /// <summary>Macros redefined with a different value; rules that reference them cannot be resolved.</summary>
    public IReadOnlyDictionary<string, string> Conflicts => _conflicts;

    /// <summary>
    /// Tries to resolve a macro to a C# integer literal. Returns null (with
    /// <paramref name="reason"/> set) when the value cannot be resolved confidently.
    /// </summary>
    public string? TryEvaluate(CppMacro macro, out string reason)
    {
        var value = TryEvaluateTokens(macro.Tokens, macro, 0, out reason);
        return value is null ? null : CSharpLiteral(value.Value);
    }

    long? TryEvaluateTokens(List<CppToken> tokens, CppMacro macro, int depth, out string reason)
    {
        reason = string.Empty;
        var significant = tokens.Where(t => t.Kind != CppTokenKind.Comment).ToList();
        if (significant.Count == 0)
        {
            reason = "empty macro value";
            return null;
        }

        var expression = new IntegerExpression(this, significant, macro, depth);
        var value = expression.Evaluate();
        if (value is null) reason = expression.Reason;
        return value;
    }

    /// <summary>Resolves a macro used as a sub-expression, guarding against cycles and redefinitions.</summary>
    long? ResolveMacroReference(string name, int depth, out string reason)
    {
        reason = string.Empty;
        if (depth >= MaxDepth)
        {
            reason = $"macro reference chain is deeper than {MaxDepth} (possible cycle)";
            return null;
        }

        if (!_macros.TryGetValue(name, out var referenced))
        {
            reason = $"\"{name}\" is not a macro visible to the generator";
            return null;
        }

        if (_conflicts.TryGetValue(name, out var conflicting))
        {
            reason = $"\"{name}\" is redefined with a different value (also \"{conflicting}\")";
            return null;
        }

        if (!_resolving.Add(name))
        {
            reason = $"macro \"{name}\" refers to itself";
            return null;
        }

        try
        {
            var value = TryEvaluateTokens(referenced.Tokens, referenced, depth + 1, out var nestedReason);
            if (value is null)
            {
                reason = $"\"{name}\": {nestedReason}";
                return null;
            }

            return value;
        }
        finally
        {
            _resolving.Remove(name);
        }
    }

    /// <summary>Parses a C integer literal (0x, 0b, 0 prefix and u/l suffixes) into a long.</summary>
    static bool TryParseIntegerLiteral(string text, out long value, out string error)
    {
        value = 0;
        error = string.Empty;
        var span = text.AsSpan().Trim();
        if (span.IsEmpty)
        {
            error = "empty literal";
            return false;
        }

        // Suffixes (u/U, l/L, ll/LL, and C++23 z/Z) carry no value information for us.
        var end = span.Length;
        while (end > 0 && span[end - 1] is 'u' or 'U' or 'l' or 'L' or 'z' or 'Z') end--;
        span = span[..end];
        if (span.IsEmpty)
        {
            error = $"literal \"{text}\" has no digits";
            return false;
        }

        var numberBase = 10;
        if (span.Length > 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X')) { numberBase = 16; span = span[2..]; }
        else if (span.Length > 2 && span[0] == '0' && (span[1] == 'b' || span[1] == 'B')) { numberBase = 2; span = span[2..]; }
        else if (span.Length > 1 && span[0] == '0') numberBase = 8;

        try
        {
            value = Convert.ToInt64(span.ToString(), numberBase);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            error = $"literal \"{text}\" is not a supported integer";
            return false;
        }
    }

    /// <summary>
    /// Formats an evaluated value as a literal that is valid as an <c>int</c>-backed enum member.
    ///
    /// Values that fit the signed 32-bit range are written as decimals. Values that only fit an
    /// unsigned 32-bit range (the C headers use that range for the top flag bits, e.g.
    /// <c>FL_DORMANT</c> and <c>FENTTABLE_PLAYER</c>) become <c>unchecked((int)0x...)</c>: a bare
    /// hexadecimal literal of that magnitude has type <c>uint</c> and does not convert to
    /// <c>int</c> implicitly, so the enum would not compile. Anything wider is left as
    /// hexadecimal and needs a wider underlying type.
    /// </summary>
    static string CSharpLiteral(long value)
    {
        if (value is >= int.MinValue and <= int.MaxValue)
            return value.ToString(CultureInfo.InvariantCulture);

        if (value is >= 0 and <= uint.MaxValue)
            return "unchecked((int)0x" + ((uint)value).ToString("X8", CultureInfo.InvariantCulture) + ")";

        return "0x" + unchecked((ulong)value).ToString("X16", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Recursive-descent parser for the supported subset of C constant expressions, over the
    /// tokens of one macro body. Precedence, lowest first: <c>|</c>, <c>&lt;&lt;</c>, unary
    /// <c>- + ~</c>, primary (literal, macro reference, parenthesised expression).
    /// </summary>
    sealed class IntegerExpression
    {
        readonly MacroValueEvaluator _owner;
        readonly List<CppToken> _tokens;
        readonly CppMacro _macro;
        readonly int _depth;
        int _index;

        public IntegerExpression(MacroValueEvaluator owner, List<CppToken> tokens, CppMacro macro, int depth)
        {
            _owner = owner;
            _tokens = tokens;
            _macro = macro;
            _depth = depth;
        }

        /// <summary>Why evaluation failed; only meaningful when <see cref="Evaluate"/> returned null.</summary>
        public string Reason { get; private set; } = string.Empty;

        public long? Evaluate()
        {
            var value = ParseBitwiseOr();
            if (value is null) return null;

            if (_index != _tokens.Count)
            {
                Reason = $"\"{_macro.Value}\" is not a supported constant expression (unexpected \"{_tokens[_index].Text}\")";
                return null;
            }

            return value;
        }

        long? ParseBitwiseOr()
        {
            var left = ParseShift();
            while (left is not null && TryTakePunctuation("|"))
            {
                var right = ParseShift();
                if (right is null) return null;
                left |= right.Value;
            }

            return left;
        }

        long? ParseShift()
        {
            var left = ParseUnary();
            while (left is not null && TryTakeShift())
            {
                var right = ParseUnary();
                if (right is null) return null;
                if (right.Value is < 0 or > 63)
                {
                    Reason = $"shift amount {right.Value} in \"{_macro.Value}\" is out of range";
                    return null;
                }

                left = left.Value << (int)right.Value;
            }

            return left;
        }

        long? ParseUnary()
        {
            if (TryTakePunctuation("-"))
            {
                var negated = ParseUnary();
                return negated is null ? null : -negated.Value;
            }

            if (TryTakePunctuation("+")) return ParseUnary();

            if (TryTakePunctuation("~"))
            {
                // Masks are written for 32-bit ints ("DMG_TIMEBASED (~(0xff003fff))"), so the
                // complement is taken at 32 bits rather than at long width.
                var complement = ParseUnary();
                return complement is null ? null : ~(long)(int)complement.Value;
            }

            return ParsePrimary();
        }

        long? ParsePrimary()
        {
            if (TryTakePunctuation("("))
            {
                var inner = ParseBitwiseOr();
                if (inner is null) return null;
                if (!TryTakePunctuation(")"))
                {
                    Reason = $"unbalanced parentheses in \"{_macro.Value}\"";
                    return null;
                }

                return inner;
            }

            if (_index >= _tokens.Count)
            {
                Reason = $"expression \"{_macro.Value}\" ends unexpectedly";
                return null;
            }

            var token = _tokens[_index];
            if (token.Kind == CppTokenKind.Literal)
            {
                _index++;
                if (!TryParseIntegerLiteral(token.Text, out var literal, out var error))
                {
                    Reason = error;
                    return null;
                }

                return literal;
            }

            if (token.Kind is CppTokenKind.Identifier or CppTokenKind.Keyword)
            {
                _index++;
                var value = _owner.ResolveMacroReference(token.Text, _depth, out var reason);
                if (value is null) Reason = reason;
                return value;
            }

            Reason = $"unsupported token \"{token.Text}\" in \"{_macro.Value}\"";
            return null;
        }

        bool TryTakePunctuation(string text)
        {
            if (_index < _tokens.Count && _tokens[_index].Kind == CppTokenKind.Punctuation && _tokens[_index].Text == text)
            {
                _index++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Consumes <c>&lt;&lt;</c>. Accepts either a single two-character token or two adjacent
        /// <c>&lt;</c> tokens, because how the parser splits the operator is an implementation detail.
        /// </summary>
        bool TryTakeShift()
        {
            if (_index >= _tokens.Count || _tokens[_index].Kind != CppTokenKind.Punctuation) return false;

            if (_tokens[_index].Text == "<<")
            {
                _index++;
                return true;
            }

            if (_tokens[_index].Text == "<"
                && _index + 1 < _tokens.Count
                && _tokens[_index + 1].Kind == CppTokenKind.Punctuation
                && _tokens[_index + 1].Text == "<")
            {
                _index += 2;
                return true;
            }

            return false;
        }
    }
}
