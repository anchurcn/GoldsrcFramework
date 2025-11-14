using System;
using Microsoft.Extensions.Logging;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace GoldsrcFramework.Graphics;

/// <summary>
/// Stride 渲染器 - 使用 Stride 引擎在 GoldSrc 中渲染现代 3D 图形
/// </summary>
public unsafe class StrideRenderer : IDisposable
{
    private readonly ILogger<StrideRenderer>? _logger;
    private bool _isInitialized = false;
    private bool _disposed = false;
    private float _rotation = 0.0f;

    // Stride 图形对象
    private GraphicsDevice? _graphicsDevice;
    private CommandList? _commandList;
    private Stride.Graphics.Buffer? _vertexBuffer;
    private Stride.Graphics.Buffer? _indexBuffer;
    private int _indexCount;

    // 顶点结构
    private struct VertexPositionColor
    {
        public Vector3 Position;
        public Color4 Color;

        public VertexPositionColor(Vector3 position, Color4 color)
        {
            Position = position;
            Color = color;
        }

        public static readonly VertexDeclaration Layout = new VertexDeclaration(
            VertexElement.Position<Vector3>(),
            VertexElement.Color<Color4>()
        );
    }

    public StrideRenderer(ILogger<StrideRenderer>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化 Stride 渲染器
    /// </summary>
    public bool Initialize(IntPtr hglrc, IntPtr hdc)
    {
        if (_isInitialized)
        {
            _logger?.LogWarning("StrideRenderer already initialized");
            return true;
        }
        try
        {
            _logger?.LogInformation("Initializing Stride renderer with OpenGL context HGLRC: 0x{HGLRC:X}, HDC: 0x{HDC:X}", hglrc, hdc);

            var adapters = GraphicsAdapterFactory.Adapters;
            return true;
            _graphicsDevice = GraphicsDevice.New(GraphicsAdapterFactory.Default ,DeviceCreationFlags.None);
            _logger?.LogInformation("GraphicsDevice created with OpenGL backend");

            return true;
            // 创建 CommandList
            var gc = new GraphicsContext(_graphicsDevice);
            _commandList = gc.CommandList;
            return true;

            _logger?.LogInformation("CommandList created");

            // 创建立方体的顶点数据（24个顶点，每个面4个顶点）
            var vertices = new VertexPositionColor[24];
            float size = 50.0f;

            // 前面（红色）
            vertices[0] = new VertexPositionColor(new Vector3(-size, -size, -size), new Color4(1, 0, 0, 1));
            vertices[1] = new VertexPositionColor(new Vector3(size, -size, -size), new Color4(1, 0, 0, 1));
            vertices[2] = new VertexPositionColor(new Vector3(size, size, -size), new Color4(1, 0, 0, 1));
            vertices[3] = new VertexPositionColor(new Vector3(-size, size, -size), new Color4(1, 0, 0, 1));

            // 后面（绿色）
            vertices[4] = new VertexPositionColor(new Vector3(-size, -size, size), new Color4(0, 1, 0, 1));
            vertices[5] = new VertexPositionColor(new Vector3(-size, size, size), new Color4(0, 1, 0, 1));
            vertices[6] = new VertexPositionColor(new Vector3(size, size, size), new Color4(0, 1, 0, 1));
            vertices[7] = new VertexPositionColor(new Vector3(size, -size, size), new Color4(0, 1, 0, 1));

            // 左面（蓝色）
            vertices[8] = new VertexPositionColor(new Vector3(-size, -size, size), new Color4(0, 0, 1, 1));
            vertices[9] = new VertexPositionColor(new Vector3(-size, -size, -size), new Color4(0, 0, 1, 1));
            vertices[10] = new VertexPositionColor(new Vector3(-size, size, -size), new Color4(0, 0, 1, 1));
            vertices[11] = new VertexPositionColor(new Vector3(-size, size, size), new Color4(0, 0, 1, 1));

            // 右面（黄色）
            vertices[12] = new VertexPositionColor(new Vector3(size, -size, -size), new Color4(1, 1, 0, 1));
            vertices[13] = new VertexPositionColor(new Vector3(size, -size, size), new Color4(1, 1, 0, 1));
            vertices[14] = new VertexPositionColor(new Vector3(size, size, size), new Color4(1, 1, 0, 1));
            vertices[15] = new VertexPositionColor(new Vector3(size, size, -size), new Color4(1, 1, 0, 1));

            // 顶面（青色）
            vertices[16] = new VertexPositionColor(new Vector3(-size, size, -size), new Color4(0, 1, 1, 1));
            vertices[17] = new VertexPositionColor(new Vector3(size, size, -size), new Color4(0, 1, 1, 1));
            vertices[18] = new VertexPositionColor(new Vector3(size, size, size), new Color4(0, 1, 1, 1));
            vertices[19] = new VertexPositionColor(new Vector3(-size, size, size), new Color4(0, 1, 1, 1));

            // 底面（洋红色）
            vertices[20] = new VertexPositionColor(new Vector3(-size, -size, -size), new Color4(1, 0, 1, 1));
            vertices[21] = new VertexPositionColor(new Vector3(-size, -size, size), new Color4(1, 0, 1, 1));
            vertices[22] = new VertexPositionColor(new Vector3(size, -size, size), new Color4(1, 0, 1, 1));
            vertices[23] = new VertexPositionColor(new Vector3(size, -size, -size), new Color4(1, 0, 1, 1));

            // 创建顶点缓冲区
            _vertexBuffer = Stride.Graphics.Buffer.Vertex.New(_graphicsDevice, vertices, GraphicsResourceUsage.Default);
            _logger?.LogInformation("Vertex buffer created with {Count} vertices", vertices.Length);

            // 创建索引数据（每个面2个三角形，6个索引）
            var indices = new ushort[36];
            for (int i = 0; i < 6; i++)
            {
                int baseIndex = i * 4;
                int indexOffset = i * 6;
                // 第一个三角形
                indices[indexOffset + 0] = (ushort)(baseIndex + 0);
                indices[indexOffset + 1] = (ushort)(baseIndex + 1);
                indices[indexOffset + 2] = (ushort)(baseIndex + 2);
                // 第二个三角形
                indices[indexOffset + 3] = (ushort)(baseIndex + 0);
                indices[indexOffset + 4] = (ushort)(baseIndex + 2);
                indices[indexOffset + 5] = (ushort)(baseIndex + 3);
            }

            // 创建索引缓冲区
            _indexBuffer = Stride.Graphics.Buffer.Index.New(_graphicsDevice, indices);
            _indexCount = indices.Length;
            _logger?.LogInformation("Index buffer created with {Count} indices", indices.Length);

            _isInitialized = true;
            _logger?.LogInformation("Stride renderer initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Stride renderer");
            return false;
        }
    }

    /// <summary>
    /// 渲染立方体
    /// </summary>
    public void Render()
    {
        return;
        if (!_isInitialized || _graphicsDevice == null || _commandList == null || _vertexBuffer == null || _indexBuffer == null)
            return;
        try
        {
            // 更新旋转角度
            _rotation += 1.0f;

            // 重置命令列表
            _commandList.Reset();
            // render target
            var myRenderTarget = Texture.New2D(_graphicsDevice, 512, 512, false, PixelFormat.R8G8B8A8_UNorm, TextureFlags.ShaderResource | TextureFlags.RenderTarget);

            _commandList.Clear(myRenderTarget, Color4.White);

            // 关闭并提交命令列表
            _commandList.Close();
            _commandList.Flush();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during rendering");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logger?.LogInformation("Disposing Stride renderer");

        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _commandList?.Dispose();
        _graphicsDevice?.Dispose();

        _disposed = true;
        _isInitialized = false;
    }
}

