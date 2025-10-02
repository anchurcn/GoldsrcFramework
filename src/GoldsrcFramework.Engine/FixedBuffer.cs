using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NativeInterop;

/// <summary>
/// Marker interface for structs that can be used as fixed-size buffer's backing storage.
/// </summary>
public interface IFixedBufferHolder
{

}

/// <summary>
/// A fixed-size buffer that can hold a specified number of elements of type TElement.
/// Total size is determined by the TLength struct which must implement IFixedBufferHolder.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FixedBuffer<TElement, TLength> where TLength : unmanaged, IFixedBufferHolder where TElement : unmanaged
{
    public TLength Data;
    public unsafe ref TElement this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index < 0 || index >= sizeof(TLength) / sizeof(TElement))
                throw new IndexOutOfRangeException();

            fixed (TLength* p = &Data)
            {
                TElement* pElement = (TElement*)p;
                return ref pElement[index];
            }
        }
    }
}

public struct DemoUsageOfFixedBuffer
{
    public int A;
    // public Matrix4x4 FixedBuffer[64]; // Compilation error.

    public unsafe struct BufferFloat4x4x64 : IFixedBufferHolder
    {
        public fixed float M[4 * 4 * 64]; // 64 Matrix4x4 (4x4 floats each)
    }
    public FixedBuffer<Matrix4x4, BufferFloat4x4x64> FixedBuffer;

    public void Test()
    {
        ref var element = ref FixedBuffer[10]; // Access the 10th Matrix4x4 in the buffer.
        element = Matrix4x4.Identity; // Set it to identity matrix.
        var result = element + element;
        FixedBuffer[0] = result; // Store the result back.
    }
}

#region Built-in buffer holders
// From 64 bytes (16 floats) up to 4096 bytes (1024 floats)
public unsafe struct BufferByte64 : IFixedBufferHolder { public fixed byte M[64]; }
public unsafe struct BufferByte128 : IFixedBufferHolder { public fixed byte M[128]; }
public unsafe struct BufferByte256 : IFixedBufferHolder { public fixed byte M[256]; }
public unsafe struct BufferByte512 : IFixedBufferHolder { public fixed byte M[512]; }
public unsafe struct BufferByte1024 : IFixedBufferHolder { public fixed byte M[1024]; }
public unsafe struct BufferByte2048 : IFixedBufferHolder { public fixed byte M[2048]; }
public unsafe struct BufferByte4096 : IFixedBufferHolder { public fixed byte M[4096]; }
#endregion