using Godot;
using System.Collections.Generic;

/// <summary>
/// Runtime world API attached to scenes/world.tscn root by the build-time
/// scene builder. Provides collision pushout, NPC line-of-sight, room lookup,
/// waypoints, and hide-spot state. Port of game.ts losBlocked/circleVsAABBs
/// plus world.ts waypoints/hideSpots.
/// </summary>
public partial class World : Node3D
{
    public List<Aabb2> Colliders { get; } = new();
    public List<Aabb2> VisionBlockers { get; } = new();
    public List<HideSpotState> HideSpots { get; } = new();

    public Vector3 TerminalPos => WorldData.TerminalPos;
    public Vector3 TrolleyPos => WorldData.TrolleyPos;
    public Vector3 SlobDeskPos => WorldData.SlobDeskPos;

    private readonly Dictionary<string, Vector3[]> _waypoints = new();

    public override void _Ready()
    {
        // walls: collide + block vision
        foreach (var w in WorldData.Walls)
        {
            float minX = System.MathF.Min(w.X1, w.X2);
            float maxX = System.MathF.Max(w.X1, w.X2);
            float minZ = System.MathF.Min(w.Z1, w.Z2);
            float maxZ = System.MathF.Max(w.Z1, w.Z2);
            var box = new Aabb2(minX, minZ, maxX, maxZ);
            Colliders.Add(box);
            VisionBlockers.Add(box);
        }

        // props
        foreach (var p in WorldData.Props)
        {
            if (!p.Solid) continue;
            var box = new Aabb2(p.X - p.W / 2f, p.Z - p.D / 2f, p.X + p.W / 2f, p.Z + p.D / 2f);
            Colliders.Add(box);
            if (p.BlocksVision) VisionBlockers.Add(box);
        }

        // cubicle pod partitions (both lists) + desk bodies (colliders only)
        foreach (var pc in WorldData.PodCenters)
        {
            foreach (var part in WorldData.PodPartitions(pc))
            {
                var box = new Aabb2(part.X - part.W / 2f, part.Z - part.D / 2f, part.X + part.W / 2f, part.Z + part.D / 2f);
                Colliders.Add(box);
                VisionBlockers.Add(box);
            }
            foreach (var (_, body, _) in WorldData.PodDesks(pc))
            {
                Colliders.Add(new Aabb2(body.X - body.W / 2f, body.Z - body.D / 2f, body.X + body.W / 2f, body.Z + body.D / 2f));
            }
        }

        // plants (world.ts line 158: ±0.4 collider square)
        foreach (var pl in WorldData.Plants)
            Colliders.Add(new Aabb2(pl.X - 0.4f, pl.Z - 0.4f, pl.X + 0.4f, pl.Z + 0.4f));

        // hide spots
        foreach (var def in WorldData.HideSpotDefs)
        {
            HideSpots.Add(new HideSpotState
            {
                Id = def.Id,
                Name = def.Name,
                Action = def.Action,
                Pos = def.Pos,
                Capacity = def.Capacity,
            });
        }

        // waypoints
        foreach (var kv in WorldData.Waypoints) _waypoints[kv.Key] = kv.Value;
    }

    public RoomId RoomAt(float x, float z) => WorldData.RoomAt(x, z);

    public Vector3[] WaypointsFor(string zone)
    {
        if (_waypoints.TryGetValue(zone, out var pts)) return pts;
        return _waypoints.TryGetValue("drone", out var fallback)
            ? fallback
            : System.Array.Empty<Vector3>();
    }

    /// <summary>True when the 2D segment a->b crosses any vision-blocking AABB. Port of game.ts losBlocked()/segHitsAABB().</summary>
    public bool LosBlocked(Vector3 a, Vector3 b)
    {
        foreach (var w in VisionBlockers)
        {
            if (SegHitsAabb(a.X, a.Z, b.X, b.Z, w)) return true;
        }
        return false;
    }

    /// <summary>Push a circle (radius r, feet pos) out of solid AABBs. Port of game.ts circleVsAABBs(); mutates pos.XZ.</summary>
    public void ResolveCircle(ref Vector3 pos, float r)
    {
        foreach (var b in Colliders)
        {
            float cx = System.MathF.Max(b.MinX, System.MathF.Min(pos.X, b.MaxX));
            float cz = System.MathF.Max(b.MinZ, System.MathF.Min(pos.Z, b.MaxZ));
            float dx = pos.X - cx;
            float dz = pos.Z - cz;
            float d2 = dx * dx + dz * dz;
            if (d2 >= r * r) continue;

            if (d2 < 1e-8f)
            {
                // center inside box: push out along smallest penetration axis
                float pushL = pos.X - b.MinX;
                float pushR = b.MaxX - pos.X;
                float pushT = pos.Z - b.MinZ;
                float pushB = b.MaxZ - pos.Z;
                float m = System.MathF.Min(System.MathF.Min(pushL, pushR), System.MathF.Min(pushT, pushB));
                if (m == pushL) pos.X = b.MinX - r;
                else if (m == pushR) pos.X = b.MaxX + r;
                else if (m == pushT) pos.Z = b.MinZ - r;
                else pos.Z = b.MaxZ + r;
            }
            else
            {
                float d = System.MathF.Sqrt(d2);
                pos.X = cx + dx / d * r;
                pos.Z = cz + dz / d * r;
            }
        }
    }

    public HideSpotState? NearestHideSpot(Vector3 pos, float range)
    {
        HideSpotState? best = null;
        float bestDist = range;
        foreach (var s in HideSpots)
        {
            if (!s.HasRoom) continue;
            float d = s.Pos.DistanceTo(pos);
            if (d < bestDist)
            {
                best = s;
                bestDist = d;
            }
        }
        return best;
    }

    private static bool SegHitsAabb(float x1, float z1, float x2, float z2, Aabb2 b)
    {
        float dx = x2 - x1;
        float dz = z2 - z1;
        float tmin = 0f, tmax = 1f;

        if (System.MathF.Abs(dx) < 1e-9f)
        {
            if (x1 < b.MinX || x1 > b.MaxX) return false;
        }
        else
        {
            float t1 = (b.MinX - x1) / dx;
            float t2 = (b.MaxX - x1) / dx;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = System.MathF.Max(tmin, t1);
            tmax = System.MathF.Min(tmax, t2);
            if (tmin > tmax) return false;
        }

        if (System.MathF.Abs(dz) < 1e-9f)
        {
            if (z1 < b.MinZ || z1 > b.MaxZ) return false;
        }
        else
        {
            float t1 = (b.MinZ - z1) / dz;
            float t2 = (b.MaxZ - z1) / dz;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = System.MathF.Max(tmin, t1);
            tmax = System.MathF.Min(tmax, t2);
            if (tmin > tmax) return false;
        }
        return true;
    }
}
