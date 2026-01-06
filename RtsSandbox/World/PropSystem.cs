using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;
using RtsSandbox.Graphics;
using Shader = RtsSandbox.Graphics.Shader;

namespace RtsSandbox.World;

public sealed class PropSystem
{
    public readonly struct Obstacle
    {
        public Obstacle(Vector3 position, float radius, Matrix4x4 model)
        {
            Position = position;
            Radius = radius;
            Model = model;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public Matrix4x4 Model { get; }
    }

    private const int DefaultPropCount = 500;

    private Matrix4x4[] _models = Array.Empty<Matrix4x4>();
    private List<Obstacle> _obstacles = new();
    private int _instanceCount;

    // Base mesh (cube) + instance buffer
    private uint _vao;
    private uint _vboPos;
    private uint _vboInstance;

    private bool _ready;
    private GpuMesh? _propMesh;
    private int _currentCount = DefaultPropCount;

    public IReadOnlyList<Obstacle> Obstacles => _obstacles;

    public void Init(Terrain terrain, int? seed = 12345)
    {
        Randomize(DefaultPropCount, terrain, seed);
    }

    public void SetPropMesh(GpuMesh mesh)
    {
        _propMesh = mesh;
    }

    public void Randomize(int count, Terrain terrain, int? seed = null)
    {
        var rng = new Random(seed ?? Environment.TickCount);
        _currentCount = Math.Max(0, count);

        float maxX = (Terrain.W - 1) * terrain.CellSize;
        float maxZ = (Terrain.H - 1) * terrain.CellSize;

        _models = new Matrix4x4[_currentCount];
        _obstacles = new List<Obstacle>(_currentCount);
        _instanceCount = _currentCount;

        for (int i = 0; i < _currentCount; i++)
        {
            float x = (float)rng.NextDouble() * maxX;
            float z = (float)rng.NextDouble() * maxZ;
            float y = terrain.SampleHeight(x, z);

            float s = 0.8f + 1.8f * (float)rng.NextDouble();
            float yaw = (float)rng.NextDouble() * MathF.PI * 2f;

            var scale = Matrix4x4.CreateScale(s * 0.6f, s * 1.8f, s * 0.6f);
            var rot = Matrix4x4.CreateRotationY(yaw);
            var trans = Matrix4x4.CreateTranslation(new Vector3(x, y + (s * 0.9f), z));

            float baseOffset = _propMesh?.BaseOffsetY ?? 0f;
            var model = scale * rot * Matrix4x4.CreateTranslation(new Vector3(x, y + (s * 0.9f) + baseOffset, z));
            _models[i] = model;
            float radius = _propMesh?.BoundingRadius ?? (s * 0.75f);
            _obstacles.Add(new Obstacle(new Vector3(x, y, z), radius, model));
        }

        // force VAO/VBO reupload on next draw
        _ready = false;
    }

    public void PlaceSingle(Vector2 xz, Terrain terrain)
    {
        float y = terrain.SampleHeight(xz.X, xz.Y);

        float baseOffset = _propMesh?.BaseOffsetY ?? 0f;
        float radius = _propMesh?.BoundingRadius ?? 1.0f;

        _models = new[]
        {
            Matrix4x4.CreateTranslation(new Vector3(xz.X, y + baseOffset, xz.Y))
        };

        _obstacles = new List<Obstacle>(1)
        {
            new Obstacle(new Vector3(xz.X, y, xz.Y), radius, _models[0])
        };

        _instanceCount = 1;
        _ready = false;
    }

    /// <summary>
    /// VAO/VBO setup should be done ONCE. Call from Renderer.DrawProps before drawing.
    /// </summary>
    public unsafe void EnsureInstancedMesh(GL gl)
    {
        if (_ready)
        {
            // update instance buffer if already built
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboInstance);
            fixed (Matrix4x4* pm = _models)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_models.Length * sizeof(Matrix4x4)), pm, BufferUsageARB.StaticDraw);
            }
            return;
        }

        _ready = true;

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

        _vboPos = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboPos);
        fixed (float* p = cubePos)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(cubePos.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        _vboInstance = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboInstance);

        fixed (Matrix4x4* pm = _models)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_models.Length * sizeof(Matrix4x4)), pm, BufferUsageARB.StaticDraw);
        }

        int vec4Size = 4 * sizeof(float);
        int mat4Size = 16 * sizeof(float);

        for (uint i = 0; i < 4; i++)
        {
            gl.EnableVertexAttribArray(1 + i);
            gl.VertexAttribPointer(1 + i, 4, VertexAttribPointerType.Float, false, (uint)mat4Size, (void*)(i * vec4Size));
            gl.VertexAttribDivisor(1 + i, 1);
        }

        gl.BindVertexArray(0);
    }

    public void Draw(GL gl, Shader instancedUnlit, Shader unlit, CameraController cam)
    {
        if (_propMesh != null)
        {
            unlit.Use();
            unlit.SetVec4("uColor", new Vector4(0.10f, 0.55f, 0.18f, 1.0f));

            foreach (var obs in _obstacles)
            {
                var mvp = obs.Model * cam.View * cam.Proj;
                unlit.SetMat4("uMVP", mvp);
                _propMesh.Draw(gl);
            }

            return;
        }

        EnsureInstancedMesh(gl);

        instancedUnlit.Use();
        instancedUnlit.SetMat4("uVP", cam.View * cam.Proj);
        instancedUnlit.SetVec4("uColor", new Vector4(0.10f, 0.55f, 0.18f, 1.0f));

        gl.BindVertexArray(_vao);
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
