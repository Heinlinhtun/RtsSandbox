using System;
using Silk.NET.OpenGL;

namespace RtsSandbox.Graphics;

public sealed class GpuMesh
{
    public uint Vao { get; private set; }
    public uint Vbo { get; private set; }
    public uint Ebo { get; private set; }
    public int IndexCount { get; private set; }

    public System.Numerics.Vector3 BoundsMin { get; private set; }
    public System.Numerics.Vector3 BoundsMax { get; private set; }
    public float BaseOffsetY => -BoundsMin.Y;
    public float BoundingRadius { get; private set; }

    public unsafe void Create(GL gl, float[] positions, uint[] indices)
    {
        IndexCount = indices.Length;

        ComputeBounds(positions);

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();

        gl.BindVertexArray(Vao);

        // VBO: positions (vec3)
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* p = positions)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(positions.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        // EBO: indices
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (uint* pi = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), pi, BufferUsageARB.StaticDraw);
        }

        gl.BindVertexArray(0);
    }

    private void ComputeBounds(float[] positions)
    {
        if (positions.Length < 3)
        {
            BoundsMin = BoundsMax = System.Numerics.Vector3.Zero;
            BoundingRadius = 0f;
            return;
        }

        var min = new System.Numerics.Vector3(float.MaxValue);
        var max = new System.Numerics.Vector3(float.MinValue);

        for (int i = 0; i < positions.Length; i += 3)
        {
            float x = positions[i];
            float y = positions[i + 1];
            float z = positions[i + 2];

            min.X = MathF.Min(min.X, x);
            min.Y = MathF.Min(min.Y, y);
            min.Z = MathF.Min(min.Z, z);

            max.X = MathF.Max(max.X, x);
            max.Y = MathF.Max(max.Y, y);
            max.Z = MathF.Max(max.Z, z);
        }

        BoundsMin = min;
        BoundsMax = max;

        var center = (min + max) * 0.5f;
        var extents = max - center;
        BoundingRadius = extents.Length();
    }

    public void Draw(GL gl)
    {
        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, 0);
        gl.BindVertexArray(0);
    }

    public void Dispose(GL gl)
    {
        if (Ebo != 0) gl.DeleteBuffer(Ebo);
        if (Vbo != 0) gl.DeleteBuffer(Vbo);
        if (Vao != 0) gl.DeleteVertexArray(Vao);
        Ebo = Vbo = Vao = 0;
        IndexCount = 0;
    }
}
