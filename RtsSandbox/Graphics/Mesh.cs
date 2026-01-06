using Silk.NET.OpenGL;

namespace RtsSandbox.Graphics;

public sealed class Mesh
{
    public uint Vao { get; private set; }
    public uint Vbo { get; private set; }
    public int VertexCount { get; private set; }
    public int StrideBytes { get; private set; }

    public void Create(GL gl, float[] vertices, int strideBytes)
    {
        VertexCount = vertices.Length / (strideBytes / sizeof(float));
        StrideBytes = strideBytes;

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);

        unsafe
        {
            fixed (float* p = vertices)
            {
                gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(vertices.Length * sizeof(float)),
                    p,
                    BufferUsageARB.StaticDraw);
            }
        }
    }

    public unsafe void AddAttribute(GL gl, uint index, int size, int offsetBytes)
    {
        gl.EnableVertexAttribArray(index);
        gl.VertexAttribPointer(
            index,
            size,
            VertexAttribPointerType.Float,
            false,
            (uint)StrideBytes,
            (void*)offsetBytes);
    }

    public void Bind(GL gl) => gl.BindVertexArray(Vao);

    public void Draw(GL gl)
    {
        gl.BindVertexArray(Vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)VertexCount);
    }

    public void Dispose(GL gl)
    {
        if (Vbo != 0) gl.DeleteBuffer(Vbo);
        if (Vao != 0) gl.DeleteVertexArray(Vao);
    }
}
