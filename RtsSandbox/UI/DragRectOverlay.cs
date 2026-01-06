using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using RtsSandbox.Graphics;


namespace RtsSandbox.UI;

public sealed class DragRectOverlay
{
    private Graphics.Shader _shader = null!;
    private uint _vao, _vbo;
    private GL _gl = null!;


    public unsafe void Init(GL gl)
    {
        _gl = gl;
        _shader = new Graphics.Shader(gl, VS, FS);

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(8 * sizeof(float)), null, BufferUsageARB.DynamicDraw);

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        gl.BindVertexArray(0);
    }

    public unsafe void Draw(IWindow window, Vector2 a, Vector2 b)
    {
        float w = window.Size.X;
        float h = window.Size.Y;

        float x0 = (MathF.Min(a.X, b.X) / w) * 2f - 1f;
        float x1 = (MathF.Max(a.X, b.X) / w) * 2f - 1f;
        float y0 = 1f - (MathF.Min(a.Y, b.Y) / h) * 2f;
        float y1 = 1f - (MathF.Max(a.Y, b.Y) / h) * 2f;

        Span<float> verts = stackalloc float[]
        {
        x0, y0,
        x1, y0,
        x1, y1,
        x0, y1
    };

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        fixed (float* p = verts)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(8 * sizeof(float)),
                p);
        }

        _gl.Disable(GLEnum.DepthTest);
        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        _shader.Use();
        _shader.SetVec4("uColor", new Vector4(1, 1, 0, 0.25f));

        _gl.DrawArrays(PrimitiveType.LineLoop, 0, 4);

        _gl.Disable(GLEnum.Blend);
        _gl.Enable(GLEnum.DepthTest);

        _gl.BindVertexArray(0);
    }

    public void Dispose(GL gl)
    {
        gl.DeleteBuffer(_vbo);
        gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }

    private const string VS = @"
#version 330 core
layout(location=0) in vec2 aPos;
void main(){ gl_Position = vec4(aPos,0,1); }";

    private const string FS = @"
#version 330 core
out vec4 FragColor;
uniform vec4 uColor;
void main(){ FragColor = uColor; }";
}
