using System;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RtsSandbox.Graphics;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    public void SetVec4(string name, Vector4 v)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        _gl.Uniform4(loc, v.X, v.Y, v.Z, v.W);
    }

    public uint Program { get; private set; }

    public Shader(GL gl, string vs, string fs)
    {
        _gl = gl;
        Program = CreateProgram(vs, fs);
    }

    public void Use() => _gl.UseProgram(Program);

    public void SetMat4(string name, Matrix4x4 m)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc == -1) return; // ဒါမှမဟုတ် error ထုတ်ပါ

        // System.Numerics Matrix ကို 直接 (direct) ပို့လို့ရပါတယ်
        unsafe
        {
            // transpose: true ပေးလိုက်ခြင်းက logic ကို အမှန်ဆုံးဖြစ်စေပါတယ်
            _gl.UniformMatrix4(loc, 1, false, (float*)&m);
        }
    }

    public void SetVec3(string name, Vector3 v)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        _gl.Uniform3(loc, v.X, v.Y, v.Z);
    }

    public void SetFloat(string name, float f)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        _gl.Uniform1(loc, f);
    }

    public void Dispose()
    {
        if (Program != 0)
        {
            _gl.DeleteProgram(Program);
            Program = 0;
        }
    }

    private uint CreateProgram(string vsSrc, string fsSrc)
    {
        uint vs = Compile(ShaderType.VertexShader, vsSrc);
        uint fs = Compile(ShaderType.FragmentShader, fsSrc);

        uint p = _gl.CreateProgram();
        _gl.AttachShader(p, vs);
        _gl.AttachShader(p, fs);
        _gl.LinkProgram(p);

        _gl.GetProgram(p, GLEnum.LinkStatus, out int ok);
        if (ok == 0) throw new Exception(_gl.GetProgramInfoLog(p));

        _gl.DetachShader(p, vs);
        _gl.DetachShader(p, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        return p;
    }

    private uint Compile(ShaderType type, string src)
    {
        uint sh = _gl.CreateShader(type);
        _gl.ShaderSource(sh, src);
        _gl.CompileShader(sh);

        _gl.GetShader(sh, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) throw new Exception(_gl.GetShaderInfoLog(sh));
        return sh;
    }
}
