using System;
using System.IO;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using RtsSandbox.Graphics;

namespace RtsSandbox.Assets;

public sealed class AssetManager
{
    private readonly GL _gl;

    public AssetManager(GL gl) => _gl = gl;

    public GpuMesh LoadGlbStaticMesh(string path)
    {
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GLB not found: {fullPath}");

        var model = ModelRoot.Load(fullPath);

        // Take first mesh primitive (static)
        var mesh = model.LogicalMeshes.Count > 0 ? model.LogicalMeshes[0] : throw new Exception("GLB has no meshes.");
        var prim = mesh.Primitives.Count > 0 ? mesh.Primitives[0] : throw new Exception("Mesh has no primitives.");

        // Positions
        var posAcc = prim.GetVertexAccessor("POSITION");
        if (posAcc == null) throw new Exception("Primitive has no POSITION.");

        var posList = posAcc.AsVector3Array(); // IEnumerable<System.Numerics.Vector3>
        var positions = new float[posAcc.Count * 3];
        int k = 0;
        foreach (var p in posList)
        {
            positions[k++] = p.X;
            positions[k++] = p.Y;
            positions[k++] = p.Z;
        }

        // Indices (ensure triangles)
        var idxAcc = prim.IndexAccessor;
        if (idxAcc == null) throw new Exception("Primitive has no indices.");

        var indices = idxAcc.AsIndicesArray(); // IEnumerable<int>
        // To uint[]
        var idxTmp = new System.Collections.Generic.List<uint>(idxAcc.Count);
        foreach (var i in indices) idxTmp.Add((uint)i);

        var gpu = new GpuMesh();
        gpu.Create(_gl, positions, idxTmp.ToArray());
        return gpu;
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;

        // Try relative to output folder first
        string baseDir = AppContext.BaseDirectory;
        string p1 = Path.Combine(baseDir, path);
        if (File.Exists(p1)) return p1;

        // Fallback: relative to working directory
        string p2 = Path.GetFullPath(path);
        return p2;
    }
}
