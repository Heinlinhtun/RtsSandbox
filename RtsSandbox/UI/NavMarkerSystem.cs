using System.Numerics;
using System.Collections.Generic;

namespace RtsSandbox.UI;

public sealed class NavMarkerSystem
{
    private struct Marker
    {
        public Vector3 Pos;
        public double SpawnTime;
    }

    private readonly List<Marker> _markers = new();
    private const double LifeTime = 1.2;

    public void Init() { }

    public void Spawn(Vector3 pos, double time)
    {
        _markers.Add(new Marker { Pos = pos, SpawnTime = time });
    }

    public void Update(double time)
    {
        _markers.RemoveAll(m => time - m.SpawnTime > LifeTime);
    }

    public IEnumerable<(Vector3 pos, float alpha)> Enumerate(double time)
    {
        foreach (var m in _markers)
        {
            float t = (float)((time - m.SpawnTime) / LifeTime);
            float alpha = 1f - Math.Clamp(t, 0f, 1f);
            yield return (m.Pos, alpha);
        }
    }
}
