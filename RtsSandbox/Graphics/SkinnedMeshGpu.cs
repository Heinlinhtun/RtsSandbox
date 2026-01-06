using Silk.NET.OpenGL;

namespace RtsSandbox.Graphics;

public sealed class SkinnedMeshGpu
{
    public uint Vao, Vbo, Ebo;
    public int IndexCount;

    public unsafe void Create(
        GL gl,
        float[] vertices,     // pos(3) + joints(4) + weights(4)
        uint[] indices)
    {
        IndexCount = indices.Length;

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);

        fixed (float* p = vertices)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)), p,
                BufferUsageARB.StaticDraw);
        }

        int stride = (3 + 4 + 4) * sizeof(float);

        // POSITION
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);

        // JOINTS (float for now)
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(3 * sizeof(float)));

        // WEIGHTS
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)((3 + 4) * sizeof(float)));

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (uint* pi = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)), pi,
                BufferUsageARB.StaticDraw);
        }

        gl.BindVertexArray(0);
    }

    public void Draw(GL gl)
    {
        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount,
            DrawElementsType.UnsignedInt, 0);
        gl.BindVertexArray(0);
    }
}
