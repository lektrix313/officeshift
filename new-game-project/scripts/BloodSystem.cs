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
    public IReadOnlyList<Splat> All => _splats;

    private Texture2D? _bloodTex;
    private readonly Random _rng = new();

    public override void _Ready()
    {
        _bloodTex = MakeBloodTexture();
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
