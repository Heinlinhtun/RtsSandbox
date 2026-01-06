using System;
using System.IO;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using System.Numerics;

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

        // When running from bin/, assets live up the tree (e.g. ../../..)
        string? parent = baseDir;
        for (int i = 0; i < 5; i++)
        {
            parent = Directory.GetParent(parent!)?.FullName;
            if (parent == null) break;

            string candidate = Path.Combine(parent, path);
            if (File.Exists(candidate)) return candidate;
        }

        // Fallback: relative to working directory
        string p2 = Path.GetFullPath(path);
        return p2;
    }

    public SkinnedMeshGpu LoadGlbSkinnedMeshBindPose(string path,
    out Matrix4x4[] inverseBindMatrices)
    {
        var model = ModelRoot.Load(path);

        var skin = model.LogicalSkins[0];
        inverseBindMatrices = skin.InverseBindMatrices.ToArray();

        var mesh = model.LogicalMeshes[0];
        var prim = mesh.Primitives[0];

        var posAcc = prim.GetVertexAccessor("POSITION");
        var jointsAcc = prim.GetVertexAccessor("JOINTS_0");
        var weightsAcc = prim.GetVertexAccessor("WEIGHTS_0");
        var idxAcc = prim.IndexAccessor;

        int vCount = posAcc.Count;
        var vertices = new float[vCount * (3 + 4 + 4)];

        int k = 0;
        for (int i = 0; i < vCount; i++)
        {
            var p = posAcc.AsVector3Array()[i];
            var j = jointsAcc.AsVector4Array()[i];
            var w = weightsAcc.AsVector4Array()[i];

            vertices[k++] = p.X;
            vertices[k++] = p.Y;
            vertices[k++] = p.Z;

            vertices[k++] = j.X;
            vertices[k++] = j.Y;
            vertices[k++] = j.Z;
            vertices[k++] = j.W;

            vertices[k++] = w.X;
            vertices[k++] = w.Y;
            vertices[k++] = w.Z;
            vertices[k++] = w.W;
        }

        var indices = new List<uint>();
        foreach (var i in idxAcc.AsIndicesArray())
            indices.Add((uint)i);

        var gpu = new SkinnedMeshGpu();
        gpu.Create(_gl, vertices, indices.ToArray());

        return gpu;
    }

}
