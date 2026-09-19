using System.Security.Cryptography;
using System.Text;
using GoldsrcFramework.Engine.Native;
using NativeInterop;

namespace GoldsrcFramework.Content.Gpd;

/// <summary>
/// Engine-side access to legacy <c>.gpd</c> files and to the model checksum they are bound to.
/// </summary>
/// <remarks>
/// <para>
/// Files are read through GoldSrc <c>COM_LoadFile</c> so mod search paths, archives and fallback
/// game directories behave exactly like they do for any other engine asset. The bytes are copied
/// into managed memory and the engine buffer is always released, including when the load has to be
/// rejected for being too large.
/// </para>
/// <para>
/// <b>Checksum</b>: the legacy <c>checksum</c> attribute is the SHA-1 of the <b>studio model
/// header</b> only - <c>sizeof(studiohdr_t)</c> bytes at the start of the <c>.mdl</c>, not the whole
/// file. This mirrors gsphysics <c>CalcHdrChecksum</c> so existing files keep working. It is a
/// version mark, not a security feature.
/// </para>
/// </remarks>
internal static class GpdFile
{
    /// <summary>
    /// Size of <c>studiohdr_t</c> as compiled into the engine, i.e. how many leading bytes of a
    /// <c>.mdl</c> the legacy checksum covers. Verified against the shipped models: hashing exactly
    /// 244 bytes of <c>scientist.mdl</c> reproduces the checksum of its <c>.gpd</c>.
    /// </summary>
    public const int StudioHeaderSize = 244;

    private const int MaxPathLength = 128;
    private const int MaxFileBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Engine load mode used by gsphysics for <c>.gpd</c> files: allocate from the cache, do not
    /// keep the file handle open.
    /// </summary>
    private const int LoadFileMode = 5;

    /// <summary>
    /// Reads a physics data file into managed memory. Returns false when the file is absent, empty,
    /// too large, or when the engine file API is unavailable.
    /// </summary>
    public static unsafe bool TryRead(string path, out byte[] bytes)
    {
        bytes = [];

        var engine = EngineApi.PClient;
        if (engine == null || engine->COM_LoadFile == null || engine->COM_FreeFile == null)
            return false;
        if (string.IsNullOrEmpty(path) || path.Length >= MaxPathLength)
            return false;

        Span<byte> pathBuffer = stackalloc byte[MaxPathLength];
        int length = Encoding.ASCII.GetBytes(path, pathBuffer);
        pathBuffer[length] = 0;

        byte* data = null;
        fixed (byte* pathPointer = pathBuffer)
        {
            int size = 0;
            data = engine->COM_LoadFile(NCharPtr.From(pathPointer), LoadFileMode, &size);

            try
            {
                if (data == null || size <= 0 || size > MaxFileBytes)
                    return false;

                bytes = new ReadOnlySpan<byte>(data, size).ToArray();
                return true;
            }
            finally
            {
                // The engine handed the buffer over; it is ours to release whatever we decided to do
                // with the content.
                if (data != null)
                    engine->COM_FreeFile(data);
            }
        }
    }

    /// <summary>
    /// Computes the legacy checksum of a studio model header: the SHA-1 of its first
    /// <see cref="StudioHeaderSize"/> bytes, as an upper case hexadecimal string.
    /// </summary>
    public static unsafe bool TryComputeChecksum(studiohdr_t* header, out string checksum)
    {
        checksum = string.Empty;

        if (header == null)
            return false;

        // The C# declaration of studiohdr_t must cover at least the header the engine hashed, or the
        // digest would silently be taken over a shorter prefix.
        if (sizeof(studiohdr_t) < StudioHeaderSize)
            return false;

        var digest = SHA1.HashData(new ReadOnlySpan<byte>(header, StudioHeaderSize));
        checksum = Convert.ToHexString(digest);
        return true;
    }

    /// <summary>
    /// Compares a legacy <c>checksum</c> attribute with a freshly computed one. Case insensitive
    /// because the legacy format is inconsistent about case across tools.
    /// </summary>
    public static bool ChecksumMatches(string expected, string actual)
        => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
