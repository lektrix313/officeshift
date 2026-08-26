using Godot;
using System.Collections.Generic;

/// <summary>
/// Blood evidence system (port of game.ts spawnBlood/removeSplat/nearestSplat).
/// Godot Decal nodes projected onto the floor; splat texture generated
/// procedurally once (radial dark-red blobs, port of makeBloodTexture).
/// FIFO cap Bal.MaxSplats.
/// </summary>
public partial class BloodSystem : Node3D
{
    public sealed record Splat(Decal Node, Vector3 Pos);

    private readonly List<Splat> _splats = new();
    private readonly List<Splat> _liquids = new();
    public IReadOnlyList<Splat> All => _splats;
    public IReadOnlyList<Splat> Liquids => _liquids;

    private Texture2D? _bloodTex;
    private Texture2D? _coffeeTex;
    private Texture2D? _waterTex;
    private readonly Random _rng = new();

    public override void _Ready()
    {
        _bloodTex = MakeBloodTexture();
        _coffeeTex = MakeLiquidTexture(new(0.35f, 0.2f, 0.08f, 0.95f), new(0.2f, 0.1f, 0.04f, 0f));
        _waterTex = MakeLiquidTexture(new(0.55f, 0.75f, 0.9f, 0.55f), new(0.4f, 0.55f, 0.75f, 0f));
    }

    /// <summary>Uniform-blob liquid texture (coffee / water) for slip puddles.</summary>
    private static ImageTexture MakeLiquidTexture(Color inner, Color edge)
    {
        const int size = 128;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float cx = size / 2f, cy = size / 2f, r = 44f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = System.MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                if (d > 1f) continue;
                float wobble = 1f + 0.18f * System.MathF.Sin(x * 0.35f) * System.MathF.Cos(y * 0.3f);
                float t = Util.Clamp(d / wobble, 0f, 1f);
                img.SetPixel(x, y, inner.Lerp(edge, t));
            }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Slippery puddle (coffee spill / extinguisher spray). NPCs who step in may slip.</summary>
    public void SpawnLiquid(Vector3 pos, string kind)
    {
        var tex = kind == "coffee" ? _coffeeTex : _waterTex;
        if (tex == null) return;
        var decal = new Decal
        {
            TextureAlbedo = tex,
            Size = new Vector3(1.3f, 0.05f, 1.3f),
            Position = new Vector3(pos.X, 0.025f, pos.Z),
        };
        AddChild(decal);
        _liquids.Add(new Splat(decal, decal.Position));
        while (_liquids.Count > 24)
        {
            var old = _liquids[0];
            _liquids.RemoveAt(0);
            old.Node.QueueFree();
        }
    }

    public Splat? NearestLiquidTo(Vector3 pos, float range)
    {
        Splat? best = null;
        float bestDist = range;
        foreach (var l in _liquids)
        {
            float dx = l.Pos.X - pos.X;
            float dz = l.Pos.Z - pos.Z;
            float d = System.MathF.Sqrt(dx * dx + dz * dz);
            if (d < bestDist) { best = l; bestDist = d; }
        }
        return best;
    }

    /// <summary>Procedural 128x128 splat texture — port of makeBloodTexture(): ~9 radial blobs.</summary>
    private static ImageTexture MakeBloodTexture()
    {
        const int size = 128;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var rng = new Random();

        for (int i = 0; i < 9; i++)
        {
            float cx = 34 + (float)rng.NextDouble() * 60f;
            float cy = 34 + (float)rng.NextDouble() * 60f;
            float r = 8 + (float)rng.NextDouble() * 22f;

            int minX = System.Math.Max(0, (int)(cx - r));
            int maxX = System.Math.Min(size - 1, (int)(cx + r));
            int minY = System.Math.Max(0, (int)(cy - r));
            int maxY = System.Math.Min(size - 1, (int)(cy + r));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = System.MathF.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    float t = d / r; // 0 center -> 1 edge

                    // radial gradient: rgba(140,10,10,.95) -> rgba(110,8,8,.8) @70% -> transparent
                    Color stop0 = new(140f / 255f, 10f / 255f, 10f / 255f, 0.95f);
                    Color stop1 = new(110f / 255f, 8f / 255f, 8f / 255f, 0.80f);
                    Color stop2 = new(90f / 255f, 5f / 255f, 5f / 255f, 0f);
                    var c = t < 0.7f ? stop0.Lerp(stop1, t / 0.7f) : stop1.Lerp(stop2, (t - 0.7f) / 0.3f);

                    var existing = img.GetPixel(x, y);
                    // additive-ish blend so overlapping blobs deepen
                    img.SetPixel(x, y, new Color(
                        System.MathF.Max(existing.R, c.R),
                        System.MathF.Max(existing.G, c.G),
                        System.MathF.Max(existing.B, c.B),
                        System.MathF.Min(1f, existing.A + c.A * (1f - existing.A))));
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Spawn count decals near pos with random offsets/rotations/sizes (maxSize * 0.5..1).</summary>
    public void Spawn(Vector3 pos, int count, float maxSize)
    {
        if (_bloodTex == null) return;
        for (int i = 0; i < count; i++)
        {
            float s = maxSize * (0.5f + (float)_rng.NextDouble() * 0.5f);
            var decal = new Decal
            {
                TextureAlbedo = _bloodTex,
                Size = new Vector3(s, 0.05f, s),
                CullMask = 1 << 1, // default layer only
                Position = new Vector3(
                    pos.X + ((float)_rng.NextDouble() - 0.5f) * 0.9f,
                    0.03f,
                    pos.Z + ((float)_rng.NextDouble() - 0.5f) * 0.9f),
                RotationDegrees = new Vector3(0f, (float)(_rng.NextDouble() * 360.0), 0f),
            };
            AddChild(decal);
            _splats.Add(new Splat(decal, decal.Position));
        }

        // cap the crime scene
        while (_splats.Count > Bal.MaxSplats)
        {
            var old = _splats[0];
            _splats.RemoveAt(0);
            old.Node.QueueFree();
        }
    }

    public void Remove(Splat s)
    {
        s.Node.QueueFree();
        _splats.Remove(s);
    }

    public void RemoveLiquid(Splat l)
    {
        l.Node.QueueFree();
        _liquids.Remove(l);
    }

    public Splat? NearestTo(Vector3 pos, float range)
    {
        Splat? best = null;
        float bestDist = range;
        foreach (var b in _splats)
        {
            float dx = b.Pos.X - pos.X;
            float dz = b.Pos.Z - pos.Z;
            float d = System.MathF.Sqrt(dx * dx + dz * dz);
            if (d < bestDist)
            {
                best = b;
                bestDist = d;
            }
        }
        return best;
    }

    public bool Contains(Splat s) => _splats.Contains(s);
}

