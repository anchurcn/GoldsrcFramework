using System.Runtime.CompilerServices;

namespace NativeInterop;

[InlineArray(256)]
public unsafe struct InlineArray256<T> : IInlineArrayUnmanaged<T> where T : unmanaged
{
    public T Element0;
}

[InlineArray(512)]
public unsafe struct InlineArray512<T> : IInlineArrayUnmanaged<T> where T : unmanaged
{
    public T Element0;
}

[InlineArray(1024)]
public unsafe struct InlineArray1024<T> : IInlineArrayUnmanaged<T> where T : unmanaged
{
    public T Element0;
}

[InlineArray(2048)]
public unsafe struct InlineArray2048<T> : IInlineArrayUnmanaged<T> where T : unmanaged
{
    public T Element0;
}

