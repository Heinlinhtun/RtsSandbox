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

    // IMPORTANT: must match the EBO data type
    public DrawElementsType IndexType { get; set; } = DrawElementsType.UnsignedInt;

    public unsafe void Create(GL gl, float[] positions, uint[] indices)
    {
        IndexCount = indices.Length;
        IndexType = DrawElementsType.UnsignedInt;

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

        // EBO: uint indices
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (uint* pi = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), pi, BufferUsageARB.StaticDraw);
        }

        gl.BindVertexArray(0);
    }

    public unsafe void Create(GL gl, float[] positions, ushort[] indices)
    {
        Console.WriteLine($"GpuMesh Creating: Verts={positions.Length / 3}, Indices={indices.Length}");

        // ပထမဆုံး vertex ၃ ခုကို ထုတ်ကြည့်ပါ (0,0,0 ဖြစ်နေလား စစ်ဖို့)
        if (positions.Length >= 3)
        {
            Console.WriteLine($"First Vertex: {positions[0]}, {positions[1]}, {positions[2]}");
        }

        // Bounds စစ်ဆေးခြင်း
        ComputeBounds(positions);
        Console.WriteLine($"Mesh Bounds: Min={BoundsMin}, Max={BoundsMax}");

        IndexCount = indices.Length;
        IndexType = DrawElementsType.UnsignedShort;

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

        // EBO: ushort indices
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (ushort* pi = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(ushort)), pi, BufferUsageARB.StaticDraw);
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
        gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, IndexType, 0);
        gl.BindVertexArray(0);

        var err = gl.GetError();
        if (err != GLEnum.NoError)
            Console.WriteLine($"GL ERROR after DrawElements: {err} (vao={Vao}, ebo={Ebo}, count={IndexCount}, indexType={IndexType})");
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
