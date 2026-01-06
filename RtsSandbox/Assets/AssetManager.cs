using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
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

        var mesh = model.LogicalMeshes.Count > 0 ? model.LogicalMeshes[0] : throw new Exception("GLB has no meshes.");
        var prim = mesh.Primitives.Count > 0 ? mesh.Primitives[0] : throw new Exception("Mesh has no primitives.");

        var posAcc = prim.GetVertexAccessor("POSITION");
        if (posAcc == null) throw new Exception("Primitive has no POSITION.");

        var posList = posAcc.AsVector3Array();
        var positions = new float[posAcc.Count * 3];
        int k = 0;
        foreach (var p in posList)
        {
            positions[k++] = p.X;
            positions[k++] = p.Y;
            positions[k++] = p.Z;
        }

        Console.WriteLine($"GLTF prim mode = {prim.DrawPrimitiveType}");

        var idxAcc = prim.IndexAccessor;
        if (idxAcc == null) throw new Exception("Primitive has no indices.");

        Console.WriteLine($"pos={posAcc.Count}, idx={idxAcc.Count}");

        var idx = idxAcc.AsIndicesArray(); // IReadOnlyList<int>
        int max = 0;
        for (int i = 0; i < idx.Count; i++)
            if (idx[i] > max) max = (int)idx[i];

        var gpu = new GpuMesh();

        if (max <= ushort.MaxValue)
        {
            var inds = new ushort[idx.Count];
            for (int i = 0; i < idx.Count; i++) inds[i] = (ushort)idx[i];

            gpu.Create(_gl, positions, inds);
            // gpu.IndexType is set inside Create()
        }
        else
        {
            var inds = new uint[idx.Count];
            for (int i = 0; i < idx.Count; i++) inds[i] = (uint)idx[i];

            gpu.Create(_gl, positions, inds);
        }

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
        return Path.GetFullPath(path);
    }

    public SkinnedMeshGpu LoadGlbSkinnedMeshBindPose(string path, out Matrix4x4[] inverseBindMatrices)
    {
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GLB not found: {fullPath}");

        var model = ModelRoot.Load(fullPath);

        if (model.LogicalSkins.Count == 0) throw new Exception("GLB has no skins.");
        var skin = model.LogicalSkins[0];
        inverseBindMatrices = skin.InverseBindMatrices.ToArray();

        var mesh = model.LogicalMeshes.Count > 0 ? model.LogicalMeshes[0] : throw new Exception("GLB has no meshes.");
        var prim = mesh.Primitives.Count > 0 ? mesh.Primitives[0] : throw new Exception("Mesh has no primitives.");

        var posAcc = prim.GetVertexAccessor("POSITION") ?? throw new Exception("Primitive has no POSITION.");
        var jointsAcc = prim.GetVertexAccessor("JOINTS_0") ?? throw new Exception("Primitive has no JOINTS_0.");
        var weightsAcc = prim.GetVertexAccessor("WEIGHTS_0") ?? throw new Exception("Primitive has no WEIGHTS_0.");
        var idxAcc = prim.IndexAccessor ?? throw new Exception("Primitive has no indices.");

        int vCount = posAcc.Count;
        var vertices = new float[vCount * (3 + 4 + 4)];

        // cache arrays (avoid calling AsVector*Array() repeatedly)
        var posArr = posAcc.AsVector3Array();
        var jointsArr = jointsAcc.AsVector4Array();
        var weightsArr = weightsAcc.AsVector4Array();

        int k = 0;
        for (int i = 0; i < vCount; i++)
        {
            var p = posArr[i];
            var j = jointsArr[i];
            var w = weightsArr[i];

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

        var idx = idxAcc.AsIndicesArray();

        // SkinnedMeshGpu currently uses uint indices; keep as uint (safe)
        var indices = new uint[idx.Count];
        for (int i = 0; i < idx.Count; i++) indices[i] = (uint)idx[i];

        var gpu = new SkinnedMeshGpu();
        gpu.Create(_gl, vertices, indices);

        return gpu;
    }
}
