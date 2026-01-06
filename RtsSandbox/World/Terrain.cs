using System;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RtsSandbox.World;

public sealed class Terrain
{
    public const int W = 256;
    public const int H = 256;

    public float CellSize { get; private set; } = 1.0f;
    public float HeightScale { get; private set; } = 10.0f;

    private readonly float[] _height = new float[W * H];

    public uint Vao { get; private set; }
    public uint Vbo { get; private set; }
    public int VertexCount { get; private set; }

    public void Init(GL gl)
    {
        BuildHeightmapProcedural();
        CreateMesh(gl);
    }

    public void Dispose(GL gl)
    {
        if (Vbo != 0) gl.DeleteBuffer(Vbo);
        if (Vao != 0) gl.DeleteVertexArray(Vao);
    }

    public float SampleHeight(float wx, float wz)
    {
        float gx = wx / CellSize;
        float gz = wz / CellSize;

        int x0 = (int)MathF.Floor(gx);
        int z0 = (int)MathF.Floor(gz);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        if (x0 < 0 || z0 < 0 || x1 >= W || z1 >= H) return 0;

        float tx = gx - x0;
        float tz = gz - z0;

        float h00 = _height[z0 * W + x0];
        float h10 = _height[z0 * W + x1];
        float h01 = _height[z1 * W + x0];
        float h11 = _height[z1 * W + x1];

        float hx0 = Lerp(h00, h10, tx);
        float hx1 = Lerp(h01, h11, tx);
        return Lerp(hx0, hx1, tz);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private void BuildHeightmapProcedural()
    {
        for (int z = 0; z < H; z++)
            for (int x = 0; x < W; x++)
            {
                float fx = x / (float)(W - 1);
                float fz = z / (float)(H - 1);

                float h =
                    0.6f * MathF.Sin(fx * 8f) * MathF.Cos(fz * 8f) +
                    0.4f * MathF.Sin(fx * 18f + 1.7f) * MathF.Cos(fz * 14f);

                _height[z * W + x] = (h * 0.5f + 0.5f) * HeightScale;
            }
    }

    private float HeightAt(int x, int z)
    {
        x = Math.Clamp(x, 0, W - 1);
        z = Math.Clamp(z, 0, H - 1);
        return _height[z * W + x];
    }

    private Vector3 NormalAt(int x, int z)
    {
        float hl = HeightAt(x - 1, z);
        float hr = HeightAt(x + 1, z);
        float hd = HeightAt(x, z - 1);
        float hu = HeightAt(x, z + 1);

        var dx = new Vector3(2 * CellSize, hr - hl, 0);
        var dz = new Vector3(0, hu - hd, 2 * CellSize);
        return Vector3.Normalize(Vector3.Cross(dz, dx));
    }

    private unsafe void CreateMesh(GL gl)
    {
        int quadsX = W - 1;
        int quadsZ = H - 1;

        VertexCount = quadsX * quadsZ * 6;

        // layout: pos(3) + normal(3) = 6 floats
        float[] v = new float[VertexCount * 6];
        int idx = 0;

        for (int z = 0; z < quadsZ; z++)
            for (int x = 0; x < quadsX; x++)
            {
                // 4 corners
                var p00 = new Vector3(x * CellSize, HeightAt(x, z), z * CellSize);
                var p10 = new Vector3((x + 1) * CellSize, HeightAt(x + 1, z), z * CellSize);
                var p01 = new Vector3(x * CellSize, HeightAt(x, z + 1), (z + 1) * CellSize);
                var p11 = new Vector3((x + 1) * CellSize, HeightAt(x + 1, z + 1), (z + 1) * CellSize);

                var n00 = NormalAt(x, z);
                var n10 = NormalAt(x + 1, z);
                var n01 = NormalAt(x, z + 1);
                var n11 = NormalAt(x + 1, z + 1);

                // Tri1: p00 p10 p11
                WriteVertex(v, ref idx, p00, n00);
                WriteVertex(v, ref idx, p10, n10);
                WriteVertex(v, ref idx, p11, n11);

                // Tri2: p00 p11 p01
                WriteVertex(v, ref idx, p00, n00);
                WriteVertex(v, ref idx, p11, n11);
                WriteVertex(v, ref idx, p01, n01);
            }

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);

        fixed (float* p = v)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));

        gl.BindVertexArray(0);
    }

    private static void WriteVertex(float[] buf, ref int idx, Vector3 pos, Vector3 nrm)
    {
        buf[idx++] = pos.X; buf[idx++] = pos.Y; buf[idx++] = pos.Z;
        buf[idx++] = nrm.X; buf[idx++] = nrm.Y; buf[idx++] = nrm.Z;
    }
}
