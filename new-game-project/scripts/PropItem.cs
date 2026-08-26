using Godot;

/// <summary>
/// Grabbable/throwable desk prop (port of the bible's item-catalog spirit:
/// nothing is set dressing). RigidBody3D that reports hard impacts as noise
/// events; breakable items shatter into a louder second noise.
/// </summary>
public partial class PropItem : RigidBody3D
{
    public string ItemType { get; private set; } = "";
    public float NoiseRadius { get; private set; } = 6f;
    public bool Breakable { get; private set; }
    private bool _armed;
    private ulong _lastNoiseMsec;

    public static string NoiseVerb(string type) => type == "papers" ? "scatter noisily" : "clatter loudly";
    private static StandardMaterial3D MakeMat(string type) => type switch
    {
        "mug" => new StandardMaterial3D { AlbedoColor = Color.FromHtml("e8e8e8"), Roughness = 0.6f },
        "stapler" => new StandardMaterial3D { AlbedoColor = Color.FromHtml("c23a2b"), Roughness = 0.5f },
        "keyboard" => new StandardMaterial3D { AlbedoColor = Color.FromHtml("222831"), Roughness = 0.7f },
        _ => new StandardMaterial3D { AlbedoColor = Color.FromHtml("f2ead8"), Roughness = 1f }, // paper stack
    };

    public static PropItem Create(string type, Vector3 pos)
    {
        var item = new PropItem();
        item.ItemType = type;
        item.Name = $"Prop_{type}_{pos.X}_{pos.Z}";

        switch (type)
        {
            case "mug":
                item.Mass = 0.3f;
                item.NoiseRadius = 6f;
                item.Breakable = true;
                item.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.06f, Height = 0.14f } });
                item.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.05f, Height = 0.14f },
                    MaterialOverride = MakeMat(type),
                });
                break;
            case "stapler":
                item.Mass = 0.4f;
                item.NoiseRadius = 5f;
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.22f, 0.08f, 0.07f) } });
                item.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.08f, 0.07f) },
                    MaterialOverride = MakeMat(type),
                });
                break;
            case "keyboard":
                item.Mass = 0.7f;
                item.NoiseRadius = 7f;
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.42f, 0.05f, 0.16f) } });
                item.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.05f, 0.16f) },
                    MaterialOverride = MakeMat(type),
                });
                break;
            case "chair":
                item.Mass = 3f;
                item.NoiseRadius = 9f;
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.45f, 0.9f, 0.45f) } });
                item.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.45f, 0.9f, 0.45f) },
                    MaterialOverride = MakeMat(type),
                });
                break;
            case "extinguisher":
                item.Mass = 2f;
                item.NoiseRadius = 6f;
                item.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.09f, Height = 0.5f } });
                item.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.5f },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("c23a2b"), Metallic = 0.4f, Roughness = 0.4f },
                });
                break;
            default: // papers
                item.Mass = 0.5f;
                item.NoiseRadius = 2.5f;
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.32f, 0.08f, 0.24f) } });
                item.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.32f, 0.08f, 0.24f) },
                    MaterialOverride = MakeMat(type),
                });
                break;
        }

        item.Position = pos;
        item.ContactMonitor = true;
        item.MaxContactsReported = 4;
        item.BodyEntered += item.OnBodyEntered;
        return item;
    }

    /// <summary>Impact noise arms after the item is first grabbed/thrown or 2s after spawn
    /// (prevents spawn-settling from emitting fake noise).</summary>
    public override void _Ready()
    {
        var timer = new Godot.Timer { WaitTime = 2.0, OneShot = true };
        timer.Timeout += () => _armed = true;
        AddChild(timer);
        timer.Start();
    }

    public void Arm() => _armed = true;

    private void OnBodyEntered(Node body)
    {
        if (!_armed) return;
        float speed = LinearVelocity.Length();
        if (speed < 1.5f) return;

        // debounce: settling/bumps shouldn't machine-gun the office
        ulong now = Time.GetTicksMsec();
        if (now - _lastNoiseMsec < 3000) return;
        _lastNoiseMsec = now;

        float radius = NoiseRadius * (Breakable && speed > 3.5f ? 1.4f : 1f);
        GameMode.Instance?.OnNoise(GlobalPosition, radius, ItemType, Breakable && speed > 4.5f);

        // smacking an NPC with office supplies is its own reward
        if (GameMode.Instance?.Player != null)
        {
            foreach (var n in GameMode.Instance.Npcs)
            {
                if (!n.Awake) continue;
                if (n.Pos.DistanceTo(GlobalPosition) < 0.9f)
                {
                    if (ItemType == "chair")
                    {
                        // comedy violence tier: a chair to the back is a full takedown
                        var dir = (n.Pos - GlobalPosition); dir.Y = 0f;
                        GameMode.Instance.OnPropKnockout(n, dir.Normalized(), ItemType);
                    }
                    else
                    {
                        n.AddSuspicion(25);
                        GameMode.Instance.Toast($"You hit {n.NpcName} with a {ItemType}. They will remember this.", ToastKind.Chaos);
                    }
                    break;
                }
            }
        }

        if (Breakable)
        {
            // the mug is gone; the coffee puddle remains as a slip hazard
            if (ItemType == "mug")
                GameMode.Instance?.Blood?.SpawnLiquid(GlobalPosition, "coffee");
            _armed = false;
            QueueFree();
        }
    }
}


