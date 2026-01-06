using System;
using System.Numerics;
using Silk.NET.OpenGL;
using RtsSandbox.Graphics;
using RtsSandbox.Input;
using Shader = RtsSandbox.Graphics.Shader;

namespace RtsSandbox.World;

public sealed class PropSystem
{
    // You can push this to 5000+ comfortably after instancing
    private const int PropCount = 2000;

    private Matrix4x4[] _models = Array.Empty<Matrix4x4>();
    private int _instanceCount;

    // Base mesh (cube) + instance buffer
    private uint _vao;
    private uint _vboPos;
    private uint _vboInstance;

    private bool _ready;

    public void Init(Terrain terrain, int? seed = 12345)
    {
        var rng = new Random(seed ?? Environment.TickCount);

        float maxX = (Terrain.W - 1) * terrain.CellSize;
        float maxZ = (Terrain.H - 1) * terrain.CellSize;

        _models = new Matrix4x4[PropCount];
        _instanceCount = PropCount;

        for (int i = 0; i < PropCount; i++)
        {
            float x = (float)rng.NextDouble() * maxX;
            float z = (float)rng.NextDouble() * maxZ;
            float y = terrain.SampleHeight(x, z);

            float s = 0.8f + 1.8f * (float)rng.NextDouble();
            float yaw = (float)rng.NextDouble() * MathF.PI * 2f;

            // Tree-ish scaling
            var scale = Matrix4x4.CreateScale(s * 0.6f, s * 1.8f, s * 0.6f);
            var rot = Matrix4x4.CreateRotationY(yaw);

            // Lift half height so it sits on ground
            var trans = Matrix4x4.CreateTranslation(new Vector3(x, y + (s * 0.9f), z));

            _models[i] = scale * rot * trans;
        }
    }

    /// <summary>
    /// VAO/VBO setup should be done ONCE. Call from Renderer.DrawProps before drawing.
    /// </summary>
    public unsafe void EnsureInstancedMesh(GL gl)
    {
        if (_ready) return;
        _ready = true;

        // --- Base cube vertices (pos only) ---
        float[] cubePos =
        {
            // Front (+Z)
            -0.5f,-0.5f, 0.5f,  0.5f,-0.5f, 0.5f,  0.5f, 0.5f, 0.5f,
            -0.5f,-0.5f, 0.5f,  0.5f, 0.5f, 0.5f, -0.5f, 0.5f, 0.5f,

            // Back (-Z)
             0.5f,-0.5f,-0.5f, -0.5f,-0.5f,-0.5f, -0.5f, 0.5f,-0.5f,
             0.5f,-0.5f,-0.5f, -0.5f, 0.5f,-0.5f,  0.5f, 0.5f,-0.5f,

            // Left (-X)
            -0.5f,-0.5f,-0.5f, -0.5f,-0.5f, 0.5f, -0.5f, 0.5f, 0.5f,
            -0.5f,-0.5f,-0.5f, -0.5f, 0.5f, 0.5f, -0.5f, 0.5f,-0.5f,

            // Right (+X)
             0.5f,-0.5f, 0.5f,  0.5f,-0.5f,-0.5f,  0.5f, 0.5f,-0.5f,
             0.5f,-0.5f, 0.5f,  0.5f, 0.5f,-0.5f,  0.5f, 0.5f, 0.5f,

            // Top (+Y)
            -0.5f, 0.5f, 0.5f,  0.5f, 0.5f, 0.5f,  0.5f, 0.5f,-0.5f,
            -0.5f, 0.5f, 0.5f,  0.5f, 0.5f,-0.5f, -0.5f, 0.5f,-0.5f,

            // Bottom (-Y)
            -0.5f,-0.5f,-0.5f,  0.5f,-0.5f,-0.5f,  0.5f,-0.5f, 0.5f,
            -0.5f,-0.5f,-0.5f,  0.5f,-0.5f, 0.5f, -0.5f,-0.5f, 0.5f,
        };

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        // VBO for cube positions
        _vboPos = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboPos);
        fixed (float* p = cubePos)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(cubePos.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        // Attribute 0 = vec3 position
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        // VBO for instance matrices
        _vboInstance = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboInstance);

        fixed (Matrix4x4* pm = _models)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_models.Length * sizeof(Matrix4x4)), pm, BufferUsageARB.StaticDraw);
        }

        // ✅ THIS IS WHERE YOUR LOOP GOES (VAO setup time)
        int vec4Size = 4 * sizeof(float);
        int mat4Size = 16 * sizeof(float);

        // Attribute locations 1..4 represent mat4 columns (vec4 each)
        for (uint i = 0; i < 4; i++)
        {
            gl.EnableVertexAttribArray(1 + i);
            gl.VertexAttribPointer(1 + i, 4, VertexAttribPointerType.Float, false, (uint)mat4Size, (void*)(i * vec4Size));
            gl.VertexAttribDivisor(1 + i, 1);
        }

        gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draw call happens every frame.
    /// Uses Instanced shader: uVP + aModel
    /// </summary>
    public void DrawInstanced(GL gl, Shader instancedUnlit, CameraController cam)
    {
        if (!_ready) return;

        instancedUnlit.Use();
        instancedUnlit.SetMat4("uVP", cam.View * cam.Proj);
        instancedUnlit.SetVec4("uColor", new Vector4(0.10f, 0.55f, 0.18f, 1.0f));

        gl.BindVertexArray(_vao);

        // ✅ THIS IS WHERE YOUR DRAW GOES (render time)
        gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 36, (uint)_instanceCount);

        gl.BindVertexArray(0);
    }

    public void Dispose(GL gl)
    {
        if (_vboInstance != 0) gl.DeleteBuffer(_vboInstance);
        if (_vboPos != 0) gl.DeleteBuffer(_vboPos);
        if (_vao != 0) gl.DeleteVertexArray(_vao);

        _vboInstance = _vboPos = _vao = 0;
        _ready = false;
    }
}
