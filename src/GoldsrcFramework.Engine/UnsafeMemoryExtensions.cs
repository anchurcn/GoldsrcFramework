using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NativeInterop;

public static class UnsafeMemoryExtensions
{
    // Get pointer to the first element of a Span<T>
    public static unsafe T* GetPointerUnsafe<T>(this ReadOnlySpan<T> span) where T : unmanaged
    {
        if (span.IsEmpty)
            return null;
        fixed (T* ptr = &span[0])
        {
            return ptr;
        }
    }

    public static unsafe NChar* GetNCharPointerUnsafe(this ReadOnlySpan<byte> span)
        => (NChar*)span.GetPointerUnsafe();
}
