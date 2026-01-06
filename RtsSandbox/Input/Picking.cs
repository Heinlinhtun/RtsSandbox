using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;
using RtsSandbox.World;

namespace RtsSandbox.Input;

public static class Picking
{
    public static bool TryGetTerrainHit(
        IWindow window,
        IMouse mouse,
        CameraController cam,
        Terrain terrain,
        out Vector3 hit)
    {
        hit = default;

        if (!TryGetMouseRay(window, mouse, cam, out var ro, out var rd))
            return false;

        // Ray-march against heightmap
        float maxT = 2000f;
        float step = 1.0f;

        float prevT = 0f;
        Vector3 prevP = ro;
        float prevH = prevP.Y - terrain.SampleHeight(prevP.X, prevP.Z);

        for (float t = step; t <= maxT; t += step)
        {
            Vector3 p = ro + rd * t;
            float h = p.Y - terrain.SampleHeight(p.X, p.Z);

            if (prevH > 0 && h <= 0)
            {
                // Binary refine
                float a = prevT, b = t;
                for (int i = 0; i < 12; i++)
                {
                    float m = (a + b) * 0.5f;
                    Vector3 pm = ro + rd * m;
                    float hm = pm.Y - terrain.SampleHeight(pm.X, pm.Z);
                    if (hm > 0) a = m; else b = m;
                }

                Vector3 ph = ro + rd * b;
                hit = new Vector3(ph.X, terrain.SampleHeight(ph.X, ph.Z), ph.Z);
                return true;
            }

            prevT = t;
            prevH = h;
        }

        return false;
    }

    private static bool TryGetMouseRay(
        IWindow window,
        IMouse mouse,
        CameraController cam,
        out Vector3 origin,
        out Vector3 dir)
    {
        origin = default;
        dir = default;

        Vector2 m = mouse.Position;
        float w = window.Size.X;
        float h = window.Size.Y;

        float x = (2f * m.X) / w - 1f;
        float y = 1f - (2f * m.Y) / h;

        if (!Matrix4x4.Invert(cam.View * cam.Proj, out var invVP))
            return false;

        Vector4 near4 = Vector4.Transform(new Vector4(x, y, -1, 1), invVP);
        Vector4 far4 = Vector4.Transform(new Vector4(x, y, 1, 1), invVP);

        if (near4.W == 0 || far4.W == 0)
            return false;

        Vector3 p0 = new(near4.X / near4.W, near4.Y / near4.W, near4.Z / near4.W);
        Vector3 p1 = new(far4.X / far4.W, far4.Y / far4.W, far4.Z / far4.W);

        origin = p0;
        dir = Vector3.Normalize(p1 - p0);
        return true;
    }
}
