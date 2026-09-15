using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NativeInterop;

/// <summary>
/// A blittable, pointer-sized wrapper around a native <c>char*</c> (C++ <c>char</c>, one byte).
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="NCharPtr"/> in <c>delegate* unmanaged[...]</c> parameter/return types and in
/// native struct field declarations to make the intent of "this is a native C string" explicit,
/// instead of using <c>byte*</c> which is ambiguous with genuine byte buffers (which GoldSrc has many of).
/// </para>
/// <para>
/// At the ABI level <see cref="NCharPtr"/> is a single pointer-sized struct, so a native
/// <c>char*</c> maps to it with identical calling-convention behavior.
/// </para>
/// <para>
/// <b>Lifetime</b>: an <see cref="NCharPtr"/> only holds a raw pointer; its validity is entirely
/// the caller's responsibility. Constructing from a managed <c>byte[]</c>/<c>string</c> without
/// pinning, or from a <c>Span&lt;byte&gt;</c> backed by movable managed memory, yields a pointer
/// that may dangle across GC compaction. Prefer <c>stackalloc</c>-backed spans, <c>fixed</c>
/// buffers, native allocations, or <c>"..."u8</c> literals (static data segment, pointer stable
/// for the process lifetime).
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct NCharPtr : IEquatable<NCharPtr>
{
    private readonly byte* _ptr;

    private NCharPtr(byte* ptr) => _ptr = ptr;

    /// <summary>Null pointer sentinel.</summary>
    public static NCharPtr Null => default;

    /// <summary><c>true</c> when the pointer is null.</summary>
    public bool IsNull => _ptr == null;

    // ----- Construction -----
    // Wrapping a raw byte* is explicit: that is the path taken when the source is a managed
    // buffer whose lifetime the caller has to think about. Wrapping a raw NChar* is implicit:
    // it just reinterprets a pointer that is already native, so it adds no lifetime risk and
    // keeps the call sites that migrate from NChar* readable.

    /// <summary>Wrap a raw <c>byte*</c>. Lifetime is the caller's responsibility.</summary>
    public static NCharPtr From(byte* ptr) => new(ptr);

    /// <summary>Wrap a raw <c>NChar*</c>. Lifetime is the caller's responsibility.</summary>
    public static NCharPtr From(NChar* ptr) => new((byte*)ptr);

    /// <summary>
    /// Implicit wrap of a raw <c>NChar*</c>, so a native <c>char*</c> can be passed wherever an
    /// <see cref="NCharPtr"/> is expected without a <c>From</c> on every argument.
    /// </summary>
    public static implicit operator NCharPtr(NChar* ptr) => new((byte*)ptr);

    /// <summary>Wrap a pointer carried as a native integer (<see cref="nint"/>, alias for <see cref="IntPtr"/>).</summary>
    public static NCharPtr From(nint value) => new((byte*)value);

    // ----- Unwrap (implicit; identity at the ABI level) -----
    // NCharPtr is a single pointer-sized struct, so unwrapping to byte*/NChar* is an identity
    // operation rather than a real conversion. Implicit keeps call sites ergonomic when feeding
    // unmigrated delegate*<NChar*> signatures or BCL Encoding APIs that take byte*.

    public static implicit operator byte*(NCharPtr p) => p._ptr;
    public static implicit operator NChar*(NCharPtr p) => (NChar*)p._ptr;
    public static implicit operator nint(NCharPtr p) => (nint)p._ptr;

    // ----- Reading (NUL-terminated native string) -----

    /// <summary>
    /// Length in bytes up to the first NUL byte, capped by <paramref name="maxLen"/>.
    /// </summary>
    public int GetLength(int maxLen)
    {
        byte* p = _ptr;
        if (p == null) return 0;
        int i = 0;
        while (i < maxLen && p[i] != 0) i++;
        return i;
    }

    /// <summary>
    /// Decode the NUL-terminated native string as UTF-8. Scans up to <paramref name="maxLen"/>
    /// bytes (the caller must know the buffer bound; <c>"..."u8</c> literals are NOT NUL-terminated).
    /// </summary>
    public string GetString(int maxLen)
    {
        int len = GetLength(maxLen);
        return len == 0 ? string.Empty : Encoding.UTF8.GetString(_ptr, len);
    }

    /// <summary>
    /// View the NUL-terminated buffer as a <see cref="ReadOnlySpan{T}"/> of <c>byte</c>, up to
    /// (but excluding) the first NUL within <paramref name="maxLen"/>.
    /// </summary>
    public ReadOnlySpan<byte> AsByteSpan(int maxLen)
    {
        int len = GetLength(maxLen);
        return len == 0 ? default : new ReadOnlySpan<byte>(_ptr, len);
    }

    /// <summary>
    /// View the buffer as a span of <see cref="NChar"/> of exactly <paramref name="length"/>
    /// elements (no NUL scanning). Useful for fixed-size native char arrays.
    /// </summary>
    public ReadOnlySpan<NChar> AsNCharSpan(int length)
        => length <= 0 ? default : new ReadOnlySpan<NChar>((NChar*)_ptr, length);

    /// <summary>Raw pointer value as hex, for debugging. Does NOT walk memory.</summary>
    public override string ToString() => $"NCharPtr@0x{(nint)_ptr:X}";

    // ----- Writing (caller-owned buffer) -----

    /// <summary>
    /// Encode <paramref name="text"/> as UTF-8 plus a trailing NUL byte into
    /// <paramref name="buffer"/> and return an <see cref="NCharPtr"/> into it.
    /// </summary>
    /// <remarks>
    /// <paramref name="buffer"/> must be backed by stackalloc or pinned/unmanaged memory; the
    /// returned pointer is only valid while that memory stays alive (for managed buffers, until
    /// the GC moves them). Consume the result immediately, typically as an argument to a native call.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small.</exception>
    public static NCharPtr FromUtf8(ReadOnlySpan<char> text, Span<byte> buffer)
    {
        int byteCount = Encoding.UTF8.GetByteCount(text);
        if (buffer.Length < byteCount + 1)
            throw new ArgumentException(
                $"Buffer too small: need {byteCount + 1} bytes, have {buffer.Length}.", nameof(buffer));

        Encoding.UTF8.GetBytes(text, buffer);
        buffer[byteCount] = 0;
        fixed (byte* p = buffer)
            return new NCharPtr(p);
    }

    /// <inheritdoc cref="FromUtf8(ReadOnlySpan{char}, Span{byte})"/>
    public static NCharPtr FromUtf8(string text, Span<byte> buffer)
        => FromUtf8(text.AsSpan(), buffer);

    // ----- Equality -----

    public bool Equals(NCharPtr other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is NCharPtr other && Equals(other);
    public override int GetHashCode() => ((nint)_ptr).GetHashCode();
    public static bool operator ==(NCharPtr a, NCharPtr b) => a._ptr == b._ptr;
    public static bool operator !=(NCharPtr a, NCharPtr b) => a._ptr != b._ptr;
}

/// <summary>Extensions for obtaining an <see cref="NCharPtr"/> from common native-buffer sources.</summary>
public static unsafe class NCharPtrExtensions
{
    /// <summary>
    /// Get an <see cref="NCharPtr"/> to the first byte of the span. Safe for <c>"..."u8</c>
    /// literals (static data segment) and for <c>stackalloc</c>/<c>fixed</c> buffers the caller
    /// controls. Unsafe for spans backed by movable managed memory unless separately pinned.
    /// </summary>
    public static NCharPtr GetNCharPtr(this ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty) return NCharPtr.Null;
        fixed (byte* p = span)
            return NCharPtr.From(p);
    }

    /// <summary>
    /// Get an <see cref="NCharPtr"/> to the start of an <see cref="InlineArray64{NChar}"/> field.
    /// Lifetime is the containing struct's.
    /// </summary>
    public static NCharPtr GetNCharPtr(this ref InlineArray64<NChar> array)
    {
        fixed (NChar* p = &array.Element0)
            return NCharPtr.From(p);
    }

    /// <summary>
    /// Get an <see cref="NCharPtr"/> to the start of an <see cref="InlineArray32{NChar}"/> field.
    /// Lifetime is the containing struct's.
    /// </summary>
    public static NCharPtr GetNCharPtr(this ref InlineArray32<NChar> array)
    {
        fixed (NChar* p = &array.Element0)
            return NCharPtr.From(p);
    }
}
