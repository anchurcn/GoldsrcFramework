using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Graphics;

public static unsafe class ShaderTriangleRenderer
{
    [DllImport("opengl32.dll", EntryPoint = "wglGetProcAddress", CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr wglGetProcAddress(string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, EntryPoint = "GetModuleHandleA", CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr GetModuleHandleA(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, EntryPoint = "GetProcAddress", CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, EntryPoint = "LoadLibraryA", CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr LoadLibraryA(string lpFileName);

    private static GL? _gl;
    private static IntPtr _context;
    private static uint _program;
    private static uint _vao;
    private static uint _vbo;

    private const string VertexShaderSource = "#version 120\nattribute vec2 aPosition;\nattribute vec3 aColor;\nvarying vec3 vColor;\nvoid main(){vColor=aColor;gl_Position=vec4(aPosition,0.0,1.0);}";
    private const string FragmentShaderSource = "#version 120\nvarying vec3 vColor;\nvoid main(){gl_FragColor=vec4(vColor,1.0);}";

    public static void DrawNormalTriangle()
    {
        if (!EnsureInitialized())
            return;

        _gl!.UseProgram(_program);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
    }

    private static bool EnsureInitialized()
    {
        var currentContext = OpenGLInfo.GetCurrentContext();
        if (currentContext == IntPtr.Zero)
            return false;

        if (_gl != null && _context == currentContext)
            return true;

        DisposeResources();
        _context = currentContext;
        _gl = GL.GetApi(GetProcAddress);
        InitializeResources();
        return _program != 0;
    }

    private static void InitializeResources()
    {
        _program = CreateProgram(VertexShaderSource, FragmentShaderSource);
        if (_program == 0)
            return;

        float[] vertices =
        {
            -0.65f, -0.55f, 1.0f, 0.2f, 0.2f,
             0.65f, -0.55f, 0.2f, 1.0f, 0.2f,
             0.0f,   0.65f, 0.2f, 0.2f, 1.0f,
        };

        _vao = _gl!.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* p = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        const uint stride = 5 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    private static uint CreateProgram(string vertexSource, string fragmentSource)
    {
        uint vertexShader = CreateShader(ShaderType.VertexShader, vertexSource);
        uint fragmentShader = CreateShader(ShaderType.FragmentShader, fragmentSource);
        if (vertexShader == 0 || fragmentShader == 0)
            return 0;

        uint program = _gl!.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.BindAttribLocation(program, 0, "aPosition");
        _gl.BindAttribLocation(program, 1, "aColor");
        _gl.LinkProgram(program);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
        return program;
    }

    private static uint CreateShader(ShaderType type, string source)
    {
        uint shader = _gl!.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        return shader;
    }

    private static IntPtr GetProcAddress(string name)
    {
        IntPtr proc = wglGetProcAddress(name);
        if (proc != IntPtr.Zero && proc != (IntPtr)1 && proc != (IntPtr)2 && proc != (IntPtr)3 && proc != new IntPtr(-1))
            return proc;

        IntPtr module = GetModuleHandleA("opengl32.dll");
        if (module == IntPtr.Zero)
            module = LoadLibraryA("opengl32.dll");

        return module == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(module, name);
    }

    private static void DisposeResources()
    {
        if (_gl == null)
            return;

        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_program != 0) _gl.DeleteProgram(_program);

        _vbo = 0;
        _vao = 0;
        _program = 0;
        _gl = null;
    }
}
