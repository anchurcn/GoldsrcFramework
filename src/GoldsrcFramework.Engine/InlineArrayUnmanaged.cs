namespace NativeInterop;

public static class InlineArrayUnmanagedHelper
{
    public static unsafe Span<T> GetSpan<T>(ref IInlineArrayUnmanaged<T> inlineArray) where T : unmanaged
    {
        fixed (IInlineArrayUnmanaged<T>* p = &inlineArray)
        {
            return new Span<T>(p, sizeof(IInlineArrayUnmanaged<T>) / sizeof(T));
        }
    }

    public static unsafe T* GetPointer<T>(ref IInlineArrayUnmanaged<T> inlineArray) where T : unmanaged
    {
        fixed (IInlineArrayUnmanaged<T>* p = &inlineArray)
        {
            return (T*)p;
        }
    }
}

public interface IInlineArrayUnmanaged<T> where T : unmanaged
{

}