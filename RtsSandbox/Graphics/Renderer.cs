using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using RtsSandbox.Input;
using RtsSandbox.World;
using RtsSandbox.UI;

namespace RtsSandbox.Graphics;

public sealed class Renderer
{
    private GL _gl = null!;

    private Shader _terrainLit = null!;
    private Shader _unlit = null!;
    private uint _vaoRing, _vboRing;
    private int _ringVerts;
    private Shader _unlitInstanced = null!;


    public void Init(GL gl)
    {
        _gl = gl;

        _terrainLit = new Shader(_gl, TerrainLitVS, TerrainLitFS);
        _unlit = new Shader(_gl, UnlitVS, UnlitFS);
        _unlitInstanced = new Shader(_gl, UnlitInstancedVS, UnlitInstancedFS);

        CreateRingMesh();

    }

    private unsafe void CreateRingMesh()
    {
        // LineLoop ring in XZ plane
        const int seg = 48;
        _ringVerts = seg;

        float[] v = new float[seg * 3];
        int k = 0;
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * MathF.PI * 2f;
            float x = MathF.Cos(a);
            float z = MathF.Sin(a);

            v[k++] = x;
            v[k++] = 0f;
            v[k++] = z;
        }

        _vaoRing = _gl.GenVertexArray();
        _vboRing = _gl.GenBuffer();

        _gl.BindVertexArray(_vaoRing);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vboRing);

        fixed (float* p = v)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        _gl.BindVertexArray(0);
    }


    public void Dispose(GL gl)
    {
        _terrainLit.Dispose();
        _unlit.Dispose();
        if (_vboRing != 0) gl.DeleteBuffer(_vboRing);
        if (_vaoRing != 0) gl.DeleteVertexArray(_vaoRing);
        _unlitInstanced.Dispose();


    }

    public void BeginFrame(IWindow window, float skyR, float skyG, float skyB)
    {
        _gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);
        _gl.ClearColor(skyR, skyG, skyB, 1f);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    public void DrawTerrainLit(IWindow window, CameraController cam, Terrain terrain)
    {
        _terrainLit.Use();

        var vp = cam.View * cam.Proj;
        _terrainLit.SetMat4("uVP", vp);
        _terrainLit.SetVec3("uBaseColor", new Vector3(0.95f, 0.95f, 0.95f));
        _terrainLit.SetVec3("uLightDir", Vector3.Normalize(new Vector3(-0.3f, -1f, -0.2f)));
        _terrainLit.SetFloat("uAmbient", 0.35f);

        _gl.BindVertexArray(terrain.Vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)terrain.VertexCount);
        _gl.BindVertexArray(0);
    }

    public void DrawUnits(IWindow window, CameraController cam, Terrain terrain, UnitSystem units)
    {
        // placeholder: still uses cubes in UnitSystem for now
        units.Draw(_gl, _unlit, cam, terrain);
    }

    public void DrawProps(IWindow window, CameraController cam, Terrain terrain, PropSystem props)
    {
        props.EnsureInstancedMesh(_gl);
        props.DrawInstanced(_gl, _unlitInstanced, cam);
    }

    public void DrawMarkers(IWindow window, CameraController cam, Terrain terrain, NavMarkerSystem markers, double time)
    {
        _unlit.Use();

        _gl.Disable(GLEnum.DepthTest);
        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        _gl.BindVertexArray(_vaoRing);

        foreach (var (pos, alpha) in markers.Enumerate(time))
        {
            // expand: alpha 1->0 ဖြစ်သလို scale 2.0 -> 3.5
            float t = 1f - alpha;                 // 0..1
            float scale = 2.0f + 1.5f * t;

            float y = terrain.SampleHeight(pos.X, pos.Z) + 0.05f;

            var model =
                Matrix4x4.CreateScale(scale, 1f, scale) *
                Matrix4x4.CreateTranslation(new Vector3(pos.X, y, pos.Z));

            var mvp = model * cam.View * cam.Proj;
            _unlit.SetMat4("uMVP", mvp);

            // alpha fade
            _unlit.SetVec4("uColor", new Vector4(1.0f, 0.85f, 0.1f, 0.85f * alpha));

            _gl.LineWidth(3f);
            _gl.DrawArrays(PrimitiveType.LineLoop, 0, (uint)_ringVerts);
        }

        _gl.BindVertexArray(0);

        _gl.Disable(GLEnum.Blend);
        _gl.Enable(GLEnum.DepthTest);
    }



    private const string TerrainLitVS = @"
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNrm;

uniform mat4 uVP;

out vec3 vNrm;

void main()
{
    vNrm = normalize(aNrm);
    gl_Position = uVP * vec4(aPos, 1.0);
}";

    private const string TerrainLitFS = @"
#version 330 core
out vec4 FragColor;

in vec3 vNrm;

uniform vec3 uBaseColor;
uniform vec3 uLightDir; // direction TO light (we use -sunDir style)
uniform float uAmbient;

void main()
{
    vec3 N = normalize(vNrm);
    vec3 L = normalize(-uLightDir);

    float ndotl = max(dot(N, L), 0.0);
    float diffuse = ndotl;

    vec3 col = uBaseColor * (uAmbient + diffuse * (1.0 - uAmbient));
    FragColor = vec4(col, 1.0);
}";

    private const string UnlitVS = @"
#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uMVP;
void main(){ gl_Position = uMVP * vec4(aPos,1.0); }
";

    private const string UnlitFS = @"
#version 330 core
out vec4 FragColor;
uniform vec4 uColor;
void main(){ FragColor = uColor; }
";
    private const string UnlitInstancedVS = @"
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in mat4 aModel;

uniform mat4 uVP;

void main()
{
    gl_Position = uVP * aModel * vec4(aPos, 1.0);
}";

    private const string UnlitInstancedFS = @"
#version 330 core
out vec4 FragColor;
uniform vec4 uColor;
void main(){ FragColor = uColor; }";

}
