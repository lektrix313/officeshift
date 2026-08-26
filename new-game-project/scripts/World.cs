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
        // world.tscn is generated. Until the Mono builder can refresh the checked-in
        // artifact, patch only legacy scenes at runtime; regenerated scenes carry a marker.
        if (GetNodeOrNull<Node>("LayoutBlockoutV13") == null)
            ApplyLegacyLayoutFallback();

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
                SmellDelay = def.SmellDelay,
            });
        }

        // waypoints
        foreach (var kv in WorldData.Waypoints) _waypoints[kv.Key] = kv.Value;
    }

    private void ApplyLegacyLayoutFallback()
    {
        RemoveLegacyPodGeometry();

        // The legacy scene already contains the first 16 wall segments and first 28 props.
        for (int i = 16; i < WorldData.Walls.Length; i++)
        {
            var w = WorldData.Walls[i];
            float cx = (w.X1 + w.X2) / 2f;
            float cz = (w.Z1 + w.Z2) / 2f;
            float len = System.MathF.Max(System.MathF.Abs(w.X2 - w.X1), 0.3f);
            float wid = System.MathF.Max(System.MathF.Abs(w.Z2 - w.Z1), 0.3f);
            AddBlockoutBox(cx, WorldData.WallHeight / 2f, cz, len, WorldData.WallHeight, wid, Color.FromHtml("d8d4cc"), true);
        }

        foreach (var door in WorldData.DoorFrames)
        {
            float half = door.Width / 2f;
            AddBlockoutBox(door.X - half, 1.1f, door.Z, 0.14f, 2.2f, 0.22f, Color.FromHtml("596575"), false);
            AddBlockoutBox(door.X + half, 1.1f, door.Z, 0.14f, 2.2f, 0.22f, Color.FromHtml("596575"), false);
            AddBlockoutBox(door.X, 2.2f, door.Z, door.Width + 0.28f, 0.14f, 0.22f, Color.FromHtml("596575"), false);
        }

        foreach (var p in WorldData.LayoutProps)
        {
            bool emissive = p.Color == Color.FromHtml("35f0a0") || p.Color == Color.FromHtml("0af0ff");
            AddBlockoutBox(p.X, p.Y, p.Z, p.W, p.H, p.D, p.Color, p.Solid, emissive);
        }

        foreach (var pc in WorldData.PodCenters)
        {
            foreach (var part in WorldData.PodPartitions(pc))
                AddBlockoutBox(part.X, part.Y, part.Z, part.W, part.H, part.D, part.Color, true);
        }
    }

    private void RemoveLegacyPodGeometry()
    {
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.Mesh is BoxMesh box && IsLegacyPodSize(box.Size))
            {
                child.Free();
                continue;
            }

            if (child is StaticBody3D body)
            {
                foreach (var shapeNode in body.GetChildren())
                {
                    if (shapeNode is CollisionShape3D shape && shape.Shape is BoxShape3D shapeBox && IsLegacyPodSize(shapeBox.Size))
                    {
                        body.Free();
                        break;
                    }
                }
            }
        }
    }

    private static bool IsLegacyPodSize(Vector3 size) =>
        (Mathf.IsEqualApprox(size.X, 5.4f) && Mathf.IsEqualApprox(size.Y, 1.4f) && Mathf.IsEqualApprox(size.Z, 0.12f)) ||
        (Mathf.IsEqualApprox(size.X, 0.12f) && Mathf.IsEqualApprox(size.Y, 1.4f) && Mathf.IsEqualApprox(size.Z, 4f));

    private void AddBlockoutBox(float x, float y, float z, float w, float h, float d, Color color, bool solid, bool emissive = false)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(w, h, d) },
            Position = new Vector3(x, y, z),
            MaterialOverride = MakeMaterial(color, emissive),
            CastShadow = h > 0.4f ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(mesh);
        if (!solid) return;

        var body = new StaticBody3D { Position = new Vector3(x, y, z) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(w, h, d) } });
        AddChild(body);
    }

    private static StandardMaterial3D MakeMaterial(Color color, bool emissive = false)
    {
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f };
        if (emissive)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 1.6f;
        }
        return material;
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
