using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Demo;


public static unsafe class NativeExceptionHandler
{
    public const uint MS_VC_EXCEPTION = 0x406D1388;

    // 与 Windows API 完全匹配的结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct EXCEPTION_RECORD
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public EXCEPTION_RECORD* ExceptionRecord;
        public void* ExceptionAddress;
        public uint NumberParameters;
        public nint* ExceptionInformation; // 使用 nint 确保指针大小正确
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EXCEPTION_POINTERS
    {
        public EXCEPTION_RECORD* ExceptionRecord;
        public nint ContextRecord;
    }

    // 使用精确的 Windows API 签名
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, int>
        SetUnhandledExceptionFilter(delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, int> lpTopLevelExceptionFilter);

    private static delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, int> _oldFilter;

    public static bool Install()
    {
        try
        {
            _oldFilter = SetUnhandledExceptionFilter(&CustomExceptionFilter);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Remove()
    {
        SetUnhandledExceptionFilter(_oldFilter);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CustomExceptionFilter(EXCEPTION_POINTERS* pExceptionPointers)
    {
        if (pExceptionPointers == null)
            return 1;

        EXCEPTION_RECORD* pExceptionRecord = pExceptionPointers->ExceptionRecord;
        if (pExceptionRecord == null)
            return 1;

        // 直接内存访问检查异常代码
        if (pExceptionRecord->ExceptionCode == MS_VC_EXCEPTION)
        {
            // 对于线程名称异常，我们静默处理并继续执行
            return -1; // EXCEPTION_CONTINUE_EXECUTION
        }

        // 对于其他异常，调用链中的下一个过滤器
        if (_oldFilter != null)
            return _oldFilter(pExceptionPointers);

        return 1; // EXCEPTION_EXECUTE_HANDLER
    }
}


public static unsafe class RobustExceptionHandler
{
    public const uint TARGET_EXCEPTION = 0x406D1388;

    [StructLayout(LayoutKind.Sequential)]
    public struct EXCEPTION_POINTERS
    {
        public EXCEPTION_RECORD* ExceptionRecord;
        public void* ContextRecord;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EXCEPTION_RECORD
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public EXCEPTION_RECORD* NextExceptionRecord;
        public void* ExceptionAddress;
        public uint NumberParameters;
        public void* ExceptionInformation;
    }

    // 两种异常处理 API
    [DllImport("kernel32.dll")]
    private static extern nint AddVectoredExceptionHandler(uint first,
        delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, nint> handler);

    [DllImport("kernel32.dll")]
    private static extern uint RemoveVectoredExceptionHandler(nint handle);

    [DllImport("kernel32.dll")]
    private static extern delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, int>
        SetUnhandledExceptionFilter(delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, int> filter);

    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    private static nint _vectoredHandler;
    private static delegate* unmanaged[Stdcall]<EXCEPTION_POINTERS*, int> _unhandledFilter;
    private static bool _installed = false;

    public static void Install()
    {
        if (_installed) return;

        try
        {
            // 安装向量化异常处理（在调试器之前调用）
            _vectoredHandler = AddVectoredExceptionHandler(1, &VectoredHandler);

            // 同时安装未处理异常过滤器
            _unhandledFilter = SetUnhandledExceptionFilter(&UnhandledFilter);

            _installed = true;

            Console.WriteLine($"异常处理器已安装 - 调试器: {IsDebuggerPresent()}");
            Console.WriteLine($"向量化处理器: 0x{_vectoredHandler:X}, 未处理过滤器: 0x{(nint)_unhandledFilter:X}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"安装异常处理器失败: {ex.Message}");
        }
    }

    public static void Uninstall()
    {
        if (!_installed) return;

        if (_vectoredHandler != 0)
        {
            RemoveVectoredExceptionHandler(_vectoredHandler);
            _vectoredHandler = 0;
        }

        if (_unhandledFilter != null)
        {
            SetUnhandledExceptionFilter(_unhandledFilter);
            _unhandledFilter = null;
        }

        _installed = false;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint VectoredHandler(EXCEPTION_POINTERS* exceptionInfo)
    {
        return HandleException(exceptionInfo) ? (nint)(0-1) : (nint)0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int UnhandledFilter(EXCEPTION_POINTERS* exceptionInfo)
    {
        return HandleException(exceptionInfo) ? -1 : 1;
    }

    private static bool HandleException(EXCEPTION_POINTERS* exceptionInfo)
    {
        if (exceptionInfo == null || exceptionInfo->ExceptionRecord == null)
            return false;

        var record = exceptionInfo->ExceptionRecord;

        // 记录所有捕获的异常用于调试
        if (record->ExceptionCode != TARGET_EXCEPTION)
        {
            Console.WriteLine($"其他异常: 0x{record->ExceptionCode:X8}");
            return false;
        }

        Console.WriteLine($"成功拦截目标异常: 0x{TARGET_EXCEPTION:X8}");
        return true;
    }
}