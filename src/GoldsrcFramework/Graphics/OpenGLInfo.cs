using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GoldsrcFramework.Graphics;

/// <summary>
/// OpenGL 上下文信息查询工具
/// </summary>
public static unsafe class OpenGLInfo
{
    #region OpenGL Constants

    private const uint GL_VENDOR = 0x1F00;
    private const uint GL_RENDERER = 0x1F01;
    private const uint GL_VERSION = 0x1F02;
    private const uint GL_EXTENSIONS = 0x1F03;
    private const uint GL_SHADING_LANGUAGE_VERSION = 0x8B8C;
    
    private const uint GL_MAJOR_VERSION = 0x821B;
    private const uint GL_MINOR_VERSION = 0x821C;
    private const uint GL_CONTEXT_FLAGS = 0x821E;
    private const uint GL_CONTEXT_PROFILE_MASK = 0x9126;
    
    // Context flags
    private const uint GL_CONTEXT_FLAG_FORWARD_COMPATIBLE_BIT = 0x00000001;
    private const uint GL_CONTEXT_FLAG_DEBUG_BIT = 0x00000002;
    private const uint GL_CONTEXT_FLAG_ROBUST_ACCESS_BIT = 0x00000004;
    
    // Context profile mask
    private const uint GL_CONTEXT_CORE_PROFILE_BIT = 0x00000001;
    private const uint GL_CONTEXT_COMPATIBILITY_PROFILE_BIT = 0x00000002;

    #endregion

    #region OpenGL P/Invoke

    [DllImport("opengl32.dll", EntryPoint = "glGetString", CallingConvention = CallingConvention.Winapi)]
    private static extern byte* glGetString(uint name);

    [DllImport("opengl32.dll", EntryPoint = "glGetIntegerv", CallingConvention = CallingConvention.Winapi)]
    private static extern void glGetIntegerv(uint pname, int* data);

    [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentContext", CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr wglGetCurrentContext();

    [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentDC", CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr wglGetCurrentDC();

    #endregion

    #region Helper Methods

    /// <summary>
    /// 将 OpenGL 返回的字符串指针转换为 C# 字符串
    /// </summary>
    private static string? GetGLString(uint name)
    {
        try
        {
            byte* ptr = glGetString(name);
            if (ptr == null)
                return null;

            // 查找字符串长度
            int length = 0;
            while (ptr[length] != 0)
                length++;

            // 转换为字符串
            return Encoding.UTF8.GetString(ptr, length);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取 OpenGL 整数参数
    /// </summary>
    private static int GetGLInteger(uint pname)
    {
        try
        {
            int value = 0;
            glGetIntegerv(pname, &value);
            return value;
        }
        catch
        {
            return -1;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 打印当前 OpenGL 上下文信息
    /// </summary>
    public static void PrintContextInfo(ILogger? logger = null)
    {
        try
        {
            // 获取当前上下文
            IntPtr hglrc = wglGetCurrentContext();
            IntPtr hdc = wglGetCurrentDC();

            if (hglrc == IntPtr.Zero)
            {
                logger?.LogWarning("No OpenGL context is current!");
                Console.WriteLine("⚠️ No OpenGL context is current!");
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           OpenGL Context Information                           ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            // 上下文句柄
            Console.WriteLine($"║ Context Handle (HGLRC): 0x{hglrc:X16}                    ║");
            Console.WriteLine($"║ Device Context (HDC):   0x{hdc:X16}                    ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            // 基本信息
            string? vendor = GetGLString(GL_VENDOR);
            string? renderer = GetGLString(GL_RENDERER);
            string? version = GetGLString(GL_VERSION);
            string? glslVersion = GetGLString(GL_SHADING_LANGUAGE_VERSION);

            Console.WriteLine($"║ Vendor:   {vendor?.PadRight(52) ?? "Unknown".PadRight(52)} ║");
            Console.WriteLine($"║ Renderer: {renderer?.PadRight(52) ?? "Unknown".PadRight(52)} ║");
            Console.WriteLine($"║ Version:  {version?.PadRight(52) ?? "Unknown".PadRight(52)} ║");
            Console.WriteLine($"║ GLSL:     {glslVersion?.PadRight(52) ?? "Unknown".PadRight(52)} ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            // OpenGL 3.0+ 版本信息
            int majorVersion = GetGLInteger(GL_MAJOR_VERSION);
            int minorVersion = GetGLInteger(GL_MINOR_VERSION);

            if (majorVersion > 0)
            {
                Console.WriteLine($"║ OpenGL Version: {majorVersion}.{minorVersion}                                        ║");
                
                // 上下文标志
                int contextFlags = GetGLInteger(GL_CONTEXT_FLAGS);
                Console.WriteLine($"║ Context Flags: 0x{contextFlags:X8}                                  ║");
                
                if ((contextFlags & GL_CONTEXT_FLAG_FORWARD_COMPATIBLE_BIT) != 0)
                    Console.WriteLine("║   - Forward Compatible                                         ║");
                if ((contextFlags & GL_CONTEXT_FLAG_DEBUG_BIT) != 0)
                    Console.WriteLine("║   - Debug Context                                              ║");
                if ((contextFlags & GL_CONTEXT_FLAG_ROBUST_ACCESS_BIT) != 0)
                    Console.WriteLine("║   - Robust Access                                              ║");

                // 上下文配置文件
                int profileMask = GetGLInteger(GL_CONTEXT_PROFILE_MASK);
                Console.WriteLine($"║ Profile Mask: 0x{profileMask:X8}                                   ║");
                
                if ((profileMask & GL_CONTEXT_CORE_PROFILE_BIT) != 0)
                    Console.WriteLine("║   - Core Profile                                               ║");
                if ((profileMask & GL_CONTEXT_COMPATIBILITY_PROFILE_BIT) != 0)
                    Console.WriteLine("║   - Compatibility Profile                                      ║");
            }

            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            // 记录到日志
            logger?.LogInformation("OpenGL Context Info - Vendor: {Vendor}, Renderer: {Renderer}, Version: {Version}, GLSL: {GLSL}",
                vendor, renderer, version, glslVersion);
            logger?.LogInformation("OpenGL Context - HGLRC: 0x{HGLRC:X}, HDC: 0x{HDC:X}, Version: {Major}.{Minor}",
                hglrc, hdc, majorVersion, minorVersion);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to query OpenGL context information");
            Console.WriteLine($"❌ Error querying OpenGL context: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取 OpenGL 扩展列表（仅适用于 OpenGL 3.0 之前的版本）
    /// </summary>
    public static string? GetExtensions()
    {
        return GetGLString(GL_EXTENSIONS);
    }

    /// <summary>
    /// 检查是否有当前 OpenGL 上下文
    /// </summary>
    public static bool HasCurrentContext()
    {
        try
        {
            return wglGetCurrentContext() != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取当前 OpenGL 上下文句柄
    /// </summary>
    public static IntPtr GetCurrentContext()
    {
        try
        {
            return wglGetCurrentContext();
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 获取当前设备上下文句柄
    /// </summary>
    public static IntPtr GetCurrentDC()
    {
        try
        {
            return wglGetCurrentDC();
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    #endregion
}

