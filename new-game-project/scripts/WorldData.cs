using Godot;
using System.Collections.Generic;

// ============================================================================
// Static office layout tables — ported verbatim from src/game/world.ts.
// CONTRACT: types frozen. Values match the Three.js prototype exactly.
// ============================================================================

public static class WorldData
{
    public const float WallHeight = 3f;

    /// <summary>Axis-aligned wall segment between two floor points.</summary>
    public sealed record WallSeg(float X1, float Z1, float X2, float Z2);

    /// <summary>Center-positioned box prop. Solid=false => visual only. BlocksVision => LOS blocker.</summary>
    public sealed record PropBox(float X, float Y, float Z, float W, float H, float D,
        Color Color, bool Solid = true, bool BlocksVision = false);

    public sealed record CarpetZone(float X, float Z, float W, float D, Color Color);
    public sealed record PlantDef(float X, float Z);
    public sealed record BeanBagDef(float X, float Z);
    public sealed record HideSpotDef(string Id, string Name, string Action, Vector3 Pos, int Capacity);

    // ---------- walls (world.ts lines 62-66, 68-70, 85-88, 95-96, 117-119) ----------
    public static readonly WallSeg[] Walls =
    {
        // outer shell
        new(-32f, -22f, 32f, -22f),
        new(-32f, 22f, 32f, 22f),
        new(-32f, -22f, -32f, 22f),
        new(32f, -22f, 32f, 22f),
        // server room
        new(-32f, -12f, -12f, -12f),   // south wall
        new(-12f, -22f, -12f, -18f),   // east wall top
        new(-12f, -15f, -12f, -12f),   // east wall bottom -> door gap z -18..-15
        // printer room
        new(-22f, -12f, -22f, -8f),    // east wall top
        new(-22f, -6f, -22f, -2f),     // east wall bottom -> door gap z -8..-6
        new(-32f, -2f, -28.5f, -2f),   // south wall left
        new(-24.5f, -2f, -22f, -2f),   // south wall right -> door gap x -28.5..-24.5
        // break room (open concept)
        new(12f, -22f, 12f, -16f),
        new(12f, -12f, 12f, -8f),
        // supply closet
        new(22f, 8f, 32f, 8f),
        new(22f, 8f, 22f, 11f),
        new(22f, 12.5f, 22f, 13.5f),   // door gap z 11..12.5
    };

    // ---------- carpets (world.ts lines 56-59, 82) ----------
    public static readonly CarpetZone[] Carpets =
    {
        new(0f, 1f, 40f, 24f, Color.FromHtml("8792a0")),     // cubicle farm
        new(22f, -15f, 20f, 14f, Color.FromHtml("6f8f7a")),  // break room green
        new(0f, 18f, 64f, 8f, Color.FromHtml("8a6f4f")),     // reception wood-ish
        new(-22f, -17f, 20f, 10f, Color.FromHtml("5a6472")), // server room dark
        new(-13.2f, -16.5f, 1.6f, 3f, Color.FromHtml("c03a2b")), // restricted stripe
    };

    // ---------- props (world.ts lines 72-79, 90-92, 98-103, 113-114, 120, 123-126) ----------
    public static readonly PropBox[] Props =
    {
        // server racks + blinkenlights (x = -29 + i*3.4)
        new(-29f, 1.1f, -20.8f, 2.2f, 2.2f, 1.2f, Color.FromHtml("22262e"), BlocksVision: true),
        new(-25.6f, 1.1f, -20.8f, 2.2f, 2.2f, 1.2f, Color.FromHtml("22262e"), BlocksVision: true),
        new(-22.2f, 1.1f, -20.8f, 2.2f, 2.2f, 1.2f, Color.FromHtml("22262e"), BlocksVision: true),
        new(-18.8f, 1.1f, -20.8f, 2.2f, 2.2f, 1.2f, Color.FromHtml("22262e"), BlocksVision: true),
        new(-15.4f, 1.1f, -20.8f, 2.2f, 2.2f, 1.2f, Color.FromHtml("22262e"), BlocksVision: true),
        new(-29f, 1.6f, -20.15f, 1.8f, 0.25f, 0.06f, Color.FromHtml("35f0a0"), Solid: false),
        new(-25.6f, 1.6f, -20.15f, 1.8f, 0.25f, 0.06f, Color.FromHtml("35f0a0"), Solid: false),
        new(-22.2f, 1.6f, -20.15f, 1.8f, 0.25f, 0.06f, Color.FromHtml("35f0a0"), Solid: false),
        new(-18.8f, 1.6f, -20.15f, 1.8f, 0.25f, 0.06f, Color.FromHtml("35f0a0"), Solid: false),
        new(-15.4f, 1.6f, -20.15f, 1.8f, 0.25f, 0.06f, Color.FromHtml("35f0a0"), Solid: false),
        // blueprint terminal desk + screen
        new(-22f, 0.45f, -18.2f, 2.4f, 0.9f, 1.2f, Color.FromHtml("6b5f4a")),
        new(-22f, 1.25f, -18.4f, 1.1f, 0.7f, 0.12f, Color.FromHtml("0af0ff"), Solid: false),
        // the legendary printer
        new(-27f, 0.65f, -10.5f, 2.4f, 1.3f, 1.8f, Color.FromHtml("e8e8e8")),
        new(-27f, 1.45f, -10.5f, 1.8f, 0.3f, 1.3f, Color.FromHtml("bfc4cc"), Solid: false),
        // paper shelves
        new(-30.5f, 0.7f, -5f, 1.6f, 1.4f, 1.2f, Color.FromHtml("8a94a6")),
        // kitchen counter + coffee machine
        new(28f, 0.5f, -20.5f, 6f, 1f, 1.4f, Color.FromHtml("7d8aa0")),
        new(25f, 0.9f, -20.4f, 0.9f, 0.8f, 0.7f, Color.FromHtml("c0c8d4"), Solid: false),
        // vending machine
        new(31f, 1.1f, -17f, 1.6f, 2.2f, 1.2f, Color.FromHtml("c23a55"), BlocksVision: true),
        // water cooler
        new(14f, 0.8f, -20.8f, 0.8f, 1.6f, 0.8f, Color.FromHtml("bfd9e8")),
        // THE MAIL TROLLEY
        new(26f, 0.55f, -12f, 1.4f, 1.1f, 2f, Color.FromHtml("9a6a3a")),
        new(26f, 1.2f, -12f, 1.5f, 0.15f, 2.1f, Color.FromHtml("6e4a26"), Solid: false),
        // supply closet shelving
        new(30.5f, 0.9f, 12.5f, 2.6f, 1.8f, 1.6f, Color.FromHtml("7a6f5a")),
        // reception front desk + elevator decor
        new(0f, 0.6f, 17.5f, 8f, 1.2f, 1.6f, Color.FromHtml("8a6f4f")),
        new(0f, 1.6f, 21.7f, 4f, 3.2f, 0.2f, Color.FromHtml("b8c0cc"), Solid: false),
        new(0f, 1.6f, 21.55f, 0.15f, 3.2f, 0.3f, Color.FromHtml("666e7a"), Solid: false),
        // microwave (break room counter top) + fire alarm (wall by break room door)
        new(26.5f, 1.15f, -20.5f, 0.9f, 0.6f, 0.7f, Color.FromHtml("3a3f47"), Solid: false),
        new(12.3f, 1.5f, -9f, 0.16f, 0.3f, 0.16f, Color.FromHtml("c23a2b"), Solid: false),
        // whiteboard (break room wall) — mission photography target
        new(20f, 1.6f, -21.4f, 2.4f, 1.2f, 0.1f, Color.FromHtml("f2f2ee"), Solid: false),
    };

    // ---------- cubicle pods (world.ts lines 129-147) ----------
    public static readonly (float X, float Z)[] PodCenters =
    {
        (-15f, -6f), (-5f, -6f), (5f, -6f), (15f, -6f),
        (-15f, 1f),  (-5f, 1f),  (5f, 1f),  (15f, 1f),
        (-15f, 8f),  (-5f, 8f),  (5f, 8f),  (15f, 8f),
    };

    public const float PartitionColorHex = 0; // (color inline below)

    public static IEnumerable<PropBox> PodPartitions((float X, float Z) p)
    {
        yield return new(p.X, 0.7f, p.Z - 2f, 5.4f, 1.4f, 0.12f, Color.FromHtml("7f8ba3"), BlocksVision: true);
        yield return new(p.X, 0.7f, p.Z + 2f, 5.4f, 1.4f, 0.12f, Color.FromHtml("7f8ba3"), BlocksVision: true);
        yield return new(p.X - 2.7f, 0.7f, p.Z, 0.12f, 1.4f, 4f, Color.FromHtml("7f8ba3"), BlocksVision: true);
        yield return new(p.X + 2.7f, 0.7f, p.Z, 0.12f, 1.4f, 4f, Color.FromHtml("7f8ba3"), BlocksVision: true);
    }

    public static readonly (float Dx, float Dz)[] DeskOffsets =
    {
        (-1.2f, -1f), (1.2f, -1f), (-1.2f, 1f), (1.2f, 1f),
    };

    public static IEnumerable<(PropBox Top, PropBox Body, PropBox Monitor)> PodDesks((float X, float Z) p)
    {
        foreach (var (dx, dz) in DeskOffsets)
        {
            float x = p.X + dx;
            float z = p.Z + dz;
            yield return (
                new(x, 0.4f, z, 1.8f, 0.08f, 1.1f, Color.FromHtml("d9d2c5"), Solid: false),
                new(x, 0.2f, z, 1.6f, 0.4f, 0.9f, Color.FromHtml("b9b2a5")),
                new(x, 0.75f, z - 0.2f, 0.7f, 0.45f, 0.08f, Color.FromHtml("222831"), Solid: false));
        }
    }

    // ---------- dressing (world.ts lines 150-163, 105-111) ----------
    public static readonly PlantDef[] Plants =
    {
        new(-24f, 5f), new(24f, 3f), new(-8f, 12f), new(8f, -9.5f), new(0f, 13f), new(-30f, 0f),
    };

    public static readonly BeanBagDef[] BeanBags =
    {
        new(18f, -18f), new(21f, -14f), new(16f, -11f),
    };

    public const string LampBasePos = "15,-15"; // parsed by builder (kept simple)

    // ---------- waypoints (world.ts lines 168-191) ----------
    public static readonly Dictionary<string, Vector3[]> Waypoints = CreateWaypoints();

    private static Dictionary<string, Vector3[]> CreateWaypoints()
    {
        var floorPts = new List<Vector3>();
        foreach (float wx in new[] { -20f, -10f, 0f, 10f, 20f })
            foreach (float wz in new[] { -9.5f, -2.5f, 4.5f, 11f })
                floorPts.Add(new Vector3(wx, 0f, wz));

        var breakPts = new Vector3[]
        {
            new(16f, 0f, -16f), new(24f, 0f, -18f), new(20f, 0f, -10f),
            new(28f, 0f, -15f), new(14f, 0f, -19f),
        };
        var printerPts = new Vector3[] { new(-26.5f, 0f, -4f), new(-29f, 0f, -8f) };
        var corridorPts = new Vector3[] { new(-12f, 0f, -16.5f), new(-17f, 0f, -16.5f) };
        var closetPts = new Vector3[] { new(26f, 0f, 11f), new(24f, 0f, 12f) };

        return new Dictionary<string, Vector3[]>
        {
            ["floor"] = floorPts.ToArray(),
            ["break"] = breakPts,
            ["printer"] = printerPts,
            ["closet"] = closetPts,
            ["snoop"] = Merge(floorPts, breakPts, printerPts, corridorPts, closetPts),
            ["gossip"] = Merge(floorPts, breakPts, printerPts),
            ["drone"] = Merge(floorPts, breakPts),
            ["grifter"] = Merge(floorPts, closetPts, breakPts),
        };
    }

    private static Vector3[] Merge(params System.Collections.Generic.IEnumerable<Vector3>[] lists)
    {
        var all = new List<Vector3>();
        foreach (var l in lists) all.AddRange(l);
        return all.ToArray();
    }

    // ---------- hide spots (world.ts lines 193-199) ----------
    public static readonly HideSpotDef[] HideSpotDefs =
    {
        new("printer", "Office Printer", "shoved under the office printer", new Vector3(-27f, 0f, -9f), 1),
        new("trolley", "Mail Trolley", "wheeled toward the loading bay in the mail trolley", new Vector3(26f, 0f, -12f), 1),
        new("rack", "Server Rack", "filed between the server racks", new Vector3(-25.5f, 0f, -20f), 1),
        new("closet", "Supply Closet", "stacked neatly in the supply closet", new Vector3(27f, 0f, 11f), 2),
        new("lamp", "Floor Lamp", "promoted to floor lamp", new Vector3(15f, 0f, -15f), 1),
    };

    // ---------- POIs (world.ts lines 216-224) ----------
    public static readonly Vector3 TerminalPos = new(-22f, 0f, -17.2f);
    public static readonly Vector3 TrolleyPos = new(26f, 0f, -12f);
    public static readonly Vector3 SlobDeskPos = new(-16.2f, 0f, 0f);

    public static readonly Vector3[] GuardPosts =
    {
        new(0f, 0f, 17f), new(-12f, 0f, 5f), new(12f, 0f, 5f), new(0f, 0f, -3f),
    };

    private static List<Vector3>? _monitorCache;
    public static List<Vector3> MonitorPositions()
    {
        if (_monitorCache != null) return _monitorCache;
        _monitorCache = new List<Vector3>();
        foreach (var pc in PodCenters)
            foreach (var (_, _, monitor) in PodDesks(pc))
                _monitorCache.Add(new Vector3(monitor.X, 0f, monitor.Z));
        return _monitorCache;
    }

    /// <summary>Grabbable desk props (runtime-spawned RigidBody3D items).</summary>
    public sealed record PropItemDef(string Type, float X, float Y, float Z);

    public static readonly PropItemDef[] PropItems =
    {
        new("keyboard", -13.8f, 0.55f, -7f),
        new("mug", -16.2f, 0.55f, -5f),
        new("stapler", -3.8f, 0.55f, 0f),
        new("papers", 5f, 0.55f, 2f),
        new("keyboard", 6.2f, 0.55f, 6.6f),
        new("mug", 16.2f, 0.55f, 9f),
        new("papers", -6.2f, 0.55f, -5f),
        new("stapler", 2f, 1.25f, 17.2f),
        new("chair", -8f, 0.55f, 3f),
        new("chair", 18f, 0.55f, -12f),
        new("extinguisher", 14.5f, 0.5f, -19.5f),
        new("extinguisher", 24f, 0.5f, 10.5f),
    };

    /// <summary>Desk phones: E lures the nearest awake NPC to the phone (they "got a call").</summary>
    public static readonly (float X, float Z)[] Phones =
    {
        (2f, 17f),
        (-13.8f, -5.4f),
        (16.2f, 8.4f),
    };

    /// <summary>Ported verbatim from world.ts roomAt().</summary>
    public static RoomId RoomAt(float x, float z)
    {
        if (x >= -32 && x <= -12 && z >= -22 && z <= -12) return RoomId.Server;
        if (x >= -32 && x <= -22 && z >= -12 && z <= -2) return RoomId.Printer;
        if (x >= 12 && x <= 32 && z >= -22 && z <= -8) return RoomId.Break;
        if (x >= 22 && x <= 32 && z >= 8 && z <= 13.5f) return RoomId.Closet;
        if (z >= 14) return RoomId.Reception;
        return RoomId.Floor;
    }
}
