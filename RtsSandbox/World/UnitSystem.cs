using System;
using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using RtsSandbox.Graphics;
using RtsSandbox.Input;
using Shader = RtsSandbox.Graphics.Shader;

namespace RtsSandbox.World;

public sealed class UnitSystem
{
    public int SelectedCount => _selectedCount;

    private const int UnitCount = 5;
    private const float UnitSpeed = 12f;
    private const float SelectRadius = 2.0f;
    private const float AvoidRadius = 2.5f;
    private const float AvoidStrength = 6.5f;

    private readonly Vector3[] _pos = new Vector3[UnitCount];
    private readonly Vector3[] _target = new Vector3[UnitCount];
    private readonly bool[] _hasTarget = new bool[UnitCount];

    private readonly bool[] _selected = new bool[UnitCount];
    private int _selectedCount;
    private SkinnedMeshGpu? _skinnedMesh;
    private Matrix4x4[]? _invBind;
    // Cube mesh for now (36 vertices)
    private uint _vaoCube, _vboCube;
    private RtsSandbox.Graphics.GpuMesh? _unitMesh;
    public void SetUnitMesh(RtsSandbox.Graphics.GpuMesh mesh) => _unitMesh = mesh;


    public void SetSkinnedMesh(SkinnedMeshGpu mesh, Matrix4x4[] invBind)
    {
        _skinnedMesh = mesh;
        _invBind = invBind;
    }


    private Terrain? _terrain;

    public void Init(Terrain terrain)
    {
        _terrain = terrain;
        SpawnAt(new Vector2(120, 120));
    }

    public void SpawnAt(Vector2 center)
    {
        if (_terrain == null) throw new InvalidOperationException("Terrain not set before spawning.");

        float spacing = 2.2f;
        int cols = (int)MathF.Ceiling(MathF.Sqrt(UnitCount));
        int k = 0;

        for (int i = 0; i < UnitCount; i++)
        {
            int r = k / cols;
            int c = k % cols;

            float ox = (c - (cols - 1) * 0.5f) * spacing;
            float oz = r * spacing;

            float y = _terrain.SampleHeight(center.X + ox, center.Y + oz);
            _pos[i] = new Vector3(center.X + ox, y, center.Y + oz);
            _target[i] = _pos[i];
            _hasTarget[i] = false;
            _selected[i] = false;
            k++;
        }

        _selectedCount = 0;
    }

    public void ClearSelection()
    {
        Array.Fill(_selected, false);
        _selectedCount = 0;
    }

    public void SelectSingle(int idx)
    {
        ClearSelection();
        if (idx >= 0 && idx < UnitCount)
        {
            _selected[idx] = true;
            _selectedCount = 1;
        }
    }

    public void ToggleSelect(int idx)
    {
        if (idx < 0 || idx >= UnitCount) return;
        _selected[idx] = !_selected[idx];
        _selectedCount += _selected[idx] ? 1 : -1;
        if (_selectedCount < 0) _selectedCount = 0;
    }

    public int PickNearestUnitXZ(Vector3 groundHit)
    {
        int best = -1;
        float bestDist2 = SelectRadius * SelectRadius;

        for (int i = 0; i < UnitCount; i++)
        {
            var p = _pos[i];
            float dx = p.X - groundHit.X;
            float dz = p.Z - groundHit.Z;
            float d2 = dx * dx + dz * dz;

            if (d2 <= bestDist2)
            {
                bestDist2 = d2;
                best = i;
            }
        }

        return best;
    }

    public void IssueMoveFormation(Vector3 center)
    {
        int n = _selectedCount;
        if (n <= 0) return;

        int cols = (int)MathF.Ceiling(MathF.Sqrt(n));
        float spacing = 2.2f;

        int k = 0;
        for (int i = 0; i < UnitCount; i++)
        {
            if (!_selected[i]) continue;

            int r = k / cols;
            int c = k % cols;

            float ox = (c - (cols - 1) * 0.5f) * spacing;
            float oz = r * spacing;

            // keep hit.Y (terrain height)
            _target[i] = new Vector3(center.X + ox, center.Y, center.Z + oz);
            _hasTarget[i] = true;
            k++;
        }
    }

    public void Update(Terrain terrain, PropSystem props, double dt)
    {
        float step = UnitSpeed * (float)dt;

        for (int i = 0; i < UnitCount; i++)
        {
            if (!_hasTarget[i]) continue;

            var p = _pos[i]; p.Y = 0;
            var t = _target[i]; t.Y = 0;

            var to = t - p;
            float dist = to.Length();

            if (dist <= 0.05f)
            {
                _pos[i] = t;
                _hasTarget[i] = false;
                continue;
            }

            var dir = to / dist;

            // simple avoidance against props
            Vector3 avoid = Vector3.Zero;
            foreach (var obs in props.Obstacles)
            {
                var op = new Vector2(obs.Position.X, obs.Position.Z);
                var up = new Vector2(p.X, p.Z);
                var delta = up - op;
                float d = delta.Length();
                float desired = obs.Radius + AvoidRadius;
                if (d < desired && d > 1e-3f)
                {
                    float push = (desired - d) / desired;
                    avoid += new Vector3(delta.X, 0, delta.Y) * push;
                }
            }

            if (avoid.LengthSquared() > 1e-5f)
            {
                avoid = Vector3.Normalize(avoid) * AvoidStrength;
                dir = Vector3.Normalize(dir + avoid * 0.1f);
            }

            var move = dir * MathF.Min(step, dist);
            _pos[i] = p + move;
        }
    }

    // Box selection: project each unit to screen and test if inside rect
    public void ApplyBoxSelection(
        IWindow window,
        CameraController cam,
        Terrain terrain,
        Vector2 startPx,
        Vector2 endPx,
        bool additive,
        bool toggle)
    {
        GetRect(startPx, endPx, out float minX, out float minY, out float maxX, out float maxY);

        if (!additive && !toggle)
            ClearSelection();

        for (int i = 0; i < UnitCount; i++)
        {
            float y = terrain.SampleHeight(_pos[i].X, _pos[i].Z);
            var wpos = new Vector3(_pos[i].X, y + 0.5f, _pos[i].Z);
            var model = Matrix4x4.CreateTranslation(new Vector3(_pos[i].X, y + 0.5f, _pos[i].Z));


            if (!TryWorldToScreen(window, cam, wpos, out var sp))
                continue;

            bool inside = sp.X >= minX && sp.X <= maxX && sp.Y >= minY && sp.Y <= maxY;
            if (!inside) continue;

            if (toggle)
                ToggleSelect(i);
            else
            {
                if (!_selected[i])
                {
                    _selected[i] = true;
                    _selectedCount++;
                }
            }
        }
    }

    public void Draw(GL gl, Shader unlit, CameraController cam, Terrain terrain)
    {
        // If mesh loaded -> draw mesh, else draw cubes
        if (_unitMesh != null)
        {
            for (int i = 0; i < UnitCount; i++)
            {
                float y = terrain.SampleHeight(_pos[i].X, _pos[i].Z);

                // NOTE: GLB units are often bigger/smaller; adjust scale if needed
                var model =
                    Matrix4x4.CreateScale(1.0f) *
                    Matrix4x4.CreateTranslation(new Vector3(_pos[i].X, y, _pos[i].Z));

                var mvp = model * cam.View * cam.Proj;

                unlit.Use();
                unlit.SetMat4("uMVP", mvp);

                // vec4 color shader သုံးနေတယ်ဆို SetVec4
                if (_selected[i])
                    unlit.SetVec4("uColor", new Vector4(1.0f, 0.9f, 0.2f, 1.0f));
                else
                    unlit.SetVec4("uColor", new Vector4(0.2f, 0.75f, 0.35f, 1.0f));

                _unitMesh.Draw(gl);
            }

            return;

        }

        // fallback cubes (your existing cube draw)
        EnsureCubeMesh(gl);
        gl.BindVertexArray(_vaoCube);

        for (int i = 0; i < UnitCount; i++)
        {
            float y = terrain.SampleHeight(_pos[i].X, _pos[i].Z);
            var worldPos = new Vector3(_pos[i].X, y + 0.5f, _pos[i].Z);

            var model = Matrix4x4.CreateTranslation(worldPos);
            var mvp = model * cam.View * cam.Proj;

            unlit.Use();
            unlit.SetMat4("uMVP", mvp);

            if (_selected[i])
                unlit.SetVec4("uColor", new Vector4(1.0f, 0.9f, 0.2f, 1.0f));
            else
                unlit.SetVec4("uColor", new Vector4(0.2f, 0.75f, 0.35f, 1.0f));

            gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
        }

        gl.BindVertexArray(0);
    }


    private static void GetRect(Vector2 a, Vector2 b, out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = MathF.Min(a.X, b.X);
        minY = MathF.Min(a.Y, b.Y);
        maxX = MathF.Max(a.X, b.X);
        maxY = MathF.Max(a.Y, b.Y);
    }

    private static bool TryWorldToScreen(IWindow window, CameraController cam, Vector3 world, out Vector2 screen)
    {
        screen = default;

        // CPU convention used in your project: world * view * proj
        Vector4 clip = Vector4.Transform(new Vector4(world, 1f), cam.View * cam.Proj);
        if (clip.W <= 1e-5f) return false;

        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;

        float w = window.Size.X;
        float h = window.Size.Y;

        float sx = (ndcX * 0.5f + 0.5f) * w;
        float sy = (1f - (ndcY * 0.5f + 0.5f)) * h;

        screen = new Vector2(sx, sy);
        return true;
    }

    private unsafe void EnsureCubeMesh(GL gl)
    {
        if (_vaoCube != 0) return;

        float[] v =
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

        _vaoCube = gl.GenVertexArray();
        _vboCube = gl.GenBuffer();

        gl.BindVertexArray(_vaoCube);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboCube);

        unsafe
        {
            fixed (float* p = v)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            }
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        gl.BindVertexArray(0);
    }
}
