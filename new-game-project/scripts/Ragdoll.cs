using Godot;
using System.Collections.Generic;

/// <summary>
/// Jolt ragdoll system (port of ragdoll.ts). On KnockOut the NpcBrain asks
/// RagdollFactory.Spawn: a chain of sphere rigid bodies mirroring PART_DEFS
/// masses/radii/topology, pin-jointed, with the comedic flop impulse
/// (dir*2.2 + up*1.4), linear damp 0.45 / angular 0.7, office-bounds clamp,
/// and a settle-freeze after Bal.RagdollSettleSeconds that bakes the final
/// crumpled pose and fires onSettled once.
///
/// The skinned GLB visual is hidden while the primitive corpse is active
/// (the prototype's fallback aesthetic — upgrade path: PhysicalBoneSimulator3D).
/// </summary>
public static class RagdollFactory
{
    private sealed class PartDef
    {
        public string Key = "";
        public string? ParentKey;
        public float Mass;
        public float Radius;
        public Vector3 Offset; // relative to feet position
    }

    // port of ragdoll.ts PART_DEFS (sphere shapes throughout)
    private static readonly PartDef[] Parts =
    {
        new() { Key = "hips",   ParentKey = null,     Mass = 5f,   Radius = 0.18f, Offset = new(0f,      0.95f, 0f) },
        new() { Key = "chest",  ParentKey = "hips",   Mass = 3f,   Radius = 0.16f, Offset = new(0f,      1.25f, 0f) },
        new() { Key = "head",   ParentKey = "chest",  Mass = 1.2f, Radius = 0.13f, Offset = new(0f,      1.55f, 0f) },
        new() { Key = "lArm",   ParentKey = "chest",  Mass = 0.7f, Radius = 0.10f, Offset = new(0.35f,   1.25f, 0f) },
        new() { Key = "rArm",   ParentKey = "chest",  Mass = 0.7f, Radius = 0.10f, Offset = new(-0.35f,  1.25f, 0f) },
        new() { Key = "lLeg",   ParentKey = "hips",   Mass = 1.1f, Radius = 0.12f, Offset = new(0.15f,   0.55f, 0f) },
        new() { Key = "rLeg",   ParentKey = "hips",   Mass = 1.1f, Radius = 0.12f, Offset = new(-0.15f,  0.55f, 0f) },
    };

    /// <summary>Attaches a ragdoll under body (hiding the skinned visual). Returns root or null on failure.</summary>
    public static Node? Spawn(NpcBody body, Vector3 flopDirFromPlayer, Action<Vector3>? onSettled)
    {
        try
        {
            var root = new RagdollRoot(body, onSettled);
            body.AddChild(root);

            var mat = new StandardMaterial3D { AlbedoColor = body.Spec.Tint, Roughness = 0.85f };
            var skin = new StandardMaterial3D { AlbedoColor = Color.FromHtml("e8b98a"), Roughness = 0.8f };

            var bodiesByKey = new Dictionary<string, RigidBody3D>();

            foreach (var def in Parts)
            {
                var rb = new RigidBody3D
                {
                    Name = $"Part_{def.Key}",
                    Mass = def.Mass,
                    LinearDamp = 0.45f,
                    AngularDamp = 0.7f,
                    CanSleep = false,
                    Position = def.Offset,
                };
                rb.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = def.Radius } });
                var mesh = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = def.Radius, Height = def.Radius * 2f },
                    MaterialOverride = def.Key == "head" ? skin : mat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                };
                rb.AddChild(mesh);

                // comedic flop impulse (port: dir*2.2 lateral + 1.4 up)
                Vector3 dir = flopDirFromPlayer.LengthSquared() > 0.001f
                    ? new Vector3(flopDirFromPlayer.X, 0f, flopDirFromPlayer.Z).Normalized()
                    : new Vector3((float)GD.RandRange(-0.5, 0.5), 0f, (float)GD.RandRange(-0.5, 0.5)).Normalized();
                rb.LinearVelocity = dir * 2.2f + Vector3.Up * 1.4f;

                bodiesByKey[def.Key] = rb;
            }

            foreach (var def in Parts)
            {
                if (def.ParentKey == null) continue;
                if (!bodiesByKey.TryGetValue(def.Key, out var child)) continue;
                if (!bodiesByKey.TryGetValue(def.ParentKey!, out var parent)) continue;

                var joint = new PinJoint3D
                {
                    Position = (child.Position + parent.Position) / 2f,
                };
                joint.NodeA = joint.GetPathTo(child);
                joint.NodeB = joint.GetPathTo(parent);
                root.AddChild(joint);
            }

            foreach (var kv in bodiesByKey) root.AddChild(kv.Value);

            // hide the skinned/primitive standing model while the corpse physics plays
            body.Visual.Visible = false;
            GD.Print("[Ragdoll] spawned primitive crumple");
            return root;
        }
        catch (System.Exception ex)
        {
            GD.PushError($"[Ragdoll] spawn failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Removes any active ragdoll tree and restores the standing visual (pickup path).</summary>
    public static void Clear(NpcBody body)
    {
        if (body.ActiveRagdoll is RagdollRoot root && System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root) != 0)
        {
            root.QueueFree();
        }
        else if (body.ActiveRagdoll != null)
        {
            body.ActiveRagdoll.QueueFree();
        }
        body.ActiveRagdoll = null;
        body.Visual.Visible = true;
    }
}

/// <summary>Owns the part bodies; settles (freezes) them after Bal.RagdollSettleSeconds.</summary>
public partial class RagdollRoot : Node3D
{
    private readonly List<RigidBody3D> _parts = new();
    private readonly Action<Vector3>? _onSettled;
    private readonly NpcBody _body;
    private float _age;
    private bool _settled;

    public RagdollRoot(NpcBody body, Action<Vector3>? onSettled)
    {
        _body = body;
        _onSettled = onSettled;
        Name = "Ragdoll";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_settled) return;
        _age += (float)delta;

        // keep the mess inside the office (port of ragdoll.ts clamp)
        foreach (var p in _parts)
        {
            var pos = p.GlobalPosition;
            pos.X = System.MathF.Max(-31.3f, System.MathF.Min(31.3f, pos.X));
            pos.Z = System.MathF.Max(-21.3f, System.MathF.Min(21.3f, pos.Z));
            p.GlobalPosition = pos;
        }

        if (_age >= Bal.RagdollSettleSeconds)
        {
            _settled = true;
            Vector3 center = Vector3.Zero;
            foreach (var p in _parts)
            {
                p.Freeze = true; // final pose stays baked
                p.LinearVelocity = Vector3.Zero;
                center += p.GlobalPosition;
            }
            _onSettled?.Invoke(_parts.Count > 0 ? center / _parts.Count : _body.GlobalPosition);
        }
    }

    public override void _EnterTree()
    {
        // collect parts added by factory after children are registered
        CallDeferred(nameof(CollectParts));
    }

    private void CollectParts()
    {
        _parts.Clear();
        foreach (var child in GetChildren())
            if (child is RigidBody3D rb) _parts.Add(rb);
    }
}

