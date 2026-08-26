using Godot;

/// <summary>
/// Visual + presentation layer for one NPC (port of npc.ts NPC class).
/// Owns: soldier.glb instance (tinted per archetype), name Label3D,
/// suspicion bar, text emotes, sleeping indicator, idle/walk animation,
/// primitive fallback when the GLB is unavailable. Runtime-constructed only.
/// Layout: root (position + yaw) -> Visual child (KO pose rotates this);
/// labels attach to root so they never tip over with the body.
/// </summary>
public partial class NpcBody : Node3D
{
    public string DisplayName { get; private set; } = "";
    public Archetype Arch { get; private set; }
    public ArchetypeSpec Spec { get; private set; } = Specs.Table[Archetype.Drone];
    public bool UsingRiggedModel { get; private set; }

    /// <summary>Yaw in radians (atan2 convention matching prototype facing).</summary>
    public float Facing { get; set; }

    /// <summary>Inner pivot that receives the knockout pose rotation.</summary>
    public Node3D Visual { get; private set; } = null!;
    public Skeleton3D? Skeleton { get; private set; }
    public AnimationPlayer? Anim { get; private set; }

    private string _idleClip = "";
    private string _walkClip = "";
    private string _currentClip = "";
    private bool _runRequest;

    private Label3D? _nameTag;
    private MeshInstance3D? _susBar;
    private readonly StandardMaterial3D _susBarMat = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = Color.FromHtml("39d97a"),
        NoDepthTest = true,
    };
    private Label3D? _emote;
    private Label3D? _zzz;
    private float _bobTime;
    private bool _sleeping;
    private float _sleepY = 2.7f;

    public override void _Ready()
    {
        Visual = new Node3D { Name = "Visual" };
        AddChild(Visual);

        BuildModel();

        // name tag
        _nameTag = new Label3D
        {
            Text = $"{DisplayName}\n{Spec.Label}",
            FontSize = 30,
            OutlineSize = 10,
            Modulate = Colors.White,
            Position = new Vector3(0f, 2.25f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            PixelSize = 0.002f,
        };
        AddChild(_nameTag);

        // suspicion bar (billboard quad)
        _susBar = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(1.1f, 0.12f) },
            Position = new Vector3(0f, 2.0f, 0f),
            MaterialOverride = _susBarMat,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_susBar);

        if (Arch == Archetype.Slob) ShowSleeping(true);
    }

    public void Init(string name, Archetype arch)
    {
        DisplayName = name;
        Arch = arch;
        Spec = Specs.Table[arch];
        Name = $"Npc_{name}";
    }

    // ---------- model ----------

    private void BuildModel()
    {
        PackedScene? glb = null;
        try { glb = ResourceLoader.Load<PackedScene>("res://assets/models/soldier.glb"); }
        catch { /* fall through to primitives */ }

        if (glb != null)
        {
            var model = glb.Instantiate() as Node3D;
            if (model != null)
            {
                Visual.AddChild(model);
                UsingRiggedModel = true;

                // normalize height to 1.78m (port of loadAssets scaling)
                float height = MeasureHeight(model);
                float scale = height > 0.1f ? 1.78f / height : 1f;
                model.Scale = Vector3.One * scale;

                // tint every surface 55% toward archetype color
                var tint = Spec.Tint;
                foreach (var mi in model.FindChildren("*", "MeshInstance3D", true))
                {
                    var mesh = (MeshInstance3D)mi;
                    for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                    {
                        var src = mesh.GetActiveMaterial(i) as StandardMaterial3D;
                        if (src == null) continue;
                        var m = (StandardMaterial3D)src.Duplicate();
                        m.AlbedoColor = src.AlbedoColor.Lerp(tint, 0.55f);
                        mesh.SetSurfaceOverrideMaterial(i, m);
                    }
                }

                Skeleton = model.FindChildren("*", "Skeleton3D", true).Count > 0
                    ? (Skeleton3D)model.FindChildren("*", "Skeleton3D", true)[0]
                    : null;
                Anim = model.FindChildren("*", "AnimationPlayer", true).Count > 0
                    ? (AnimationPlayer)model.FindChildren("*", "AnimationPlayer", true)[0]
                    : null;

                if (Anim != null)
                {
                    foreach (string clip in Anim.GetAnimationList())
                    {
                        string low = clip.ToLowerInvariant();
                        if (_idleClip.Length == 0 && low.Contains("idle")) _idleClip = clip;
                        if (_walkClip.Length == 0 && low.Contains("walk") && !low.Contains("idle")) _walkClip = clip;
                    }
                    if (_idleClip.Length > 0)
                    {
                        Anim.Play(_idleClip);
                        _currentClip = _idleClip;
                    }
                }
                return;
            }
        }

        // primitive fallback (port of npc.ts else-branch)
        UsingRiggedModel = false;
        var body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.32f, Height = 1.34f },
            Position = new Vector3(0f, 0.85f, 0f),
            MaterialOverride = MakeTinted(Spec.Tint),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        Visual.AddChild(body);
        var head = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f },
            Position = new Vector3(0f, 1.62f, 0f),
            MaterialOverride = MakeTinted(Color.FromHtml("e8b98a")),
        };
        Visual.AddChild(head);
    }

    private static StandardMaterial3D MakeTinted(Color c) =>
        new() { AlbedoColor = c, Roughness = 0.85f };

    private static float MeasureHeight(Node3D model)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var child in model.FindChildren("*", "MeshInstance3D", true))
        {
            var mi = (MeshInstance3D)child;
            var aabb = mi.Mesh.GetAabb();
            var from = mi.GlobalTransform * aabb.GetCenter() - aabb.Size / 2f * mi.GlobalTransform.Basis.Scale;
            var to = from + aabb.Size * mi.GlobalTransform.Basis.Scale;
            minY = System.MathF.Min(minY, from.Y);
            maxY = System.MathF.Max(maxY, to.Y);
        }
        return maxY > minY ? maxY - minY : 1.8f;
    }

    // ---------- animation ----------

    public void SetMoving(bool moving, bool run = false)
    {
        _runRequest = run;
        if (Anim == null || !UsingRiggedModel) return;
        string target = moving && _walkClip.Length > 0 ? _walkClip : _idleClip.Length > 0 ? _idleClip : "";
        if (target.Length == 0 || target == _currentClip) return;
        if (Anim.HasAnimation(target))
        {
            Anim.Play(target, 0.18);
            _currentClip = target;
        }
    }

    public override void _Process(double delta)
    {
        if (Anim != null && _walkClip.Length > 0 && Anim.CurrentAnimation == _walkClip)
            Anim.SpeedScale = _runRequest ? 1.7f : 1.0f;

        // zzz bobbing (port of updateVisuals zzz line)
        if (_zzz != null && _sleeping)
            _zzz.Position = new Vector3(0f, _sleepY + System.MathF.Sin(_bobTime += (float)delta * 2.5f) * 0.08f, 0f);
    }

    // ---------- labels / indicators ----------

    public void ShowEmote(string text)
    {
        ClearEmote();
        _emote = new Label3D
        {
            Text = text,
            FontSize = 40,
            OutlineSize = 14,
            Modulate = Color.FromHtml("ffd76a"),
            Position = new Vector3(0f, 2.75f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.002f,
        };
        AddChild(_emote);
    }

    public void ClearEmote()
    {
        _emote?.QueueFree();
        _emote = null;
    }

    public void ShowSleeping(bool on)
    {
        _sleeping = on;
        if (on && _zzz == null)
        {
            _zzz = new Label3D
            {
                Text = "Z z z",
                FontSize = 36,
                OutlineSize = 12,
                Modulate = Color.FromHtml("9fd8ff"),
                Position = new Vector3(0f, _sleepY, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.002f,
            };
            AddChild(_zzz);
        }
        else if (!on && _zzz != null)
        {
            _zzz.QueueFree();
            _zzz = null;
        }
    }

    /// <summary>t01 in [0..1]; shows/hides the billboard suspicion bar, green->yellow->red.</summary>
    public void SetSuspicion(float t01)
    {
        if (_susBar == null) return;
        bool show = t01 > 0.01f;
        _susBar.Visible = show;
        if (!show) return;
        t01 = Util.Clamp(t01, 0f, 1f);
        if (t01 < 0.5f) _susBarMat.AlbedoColor = Color.FromHtml("39d97a").Lerp(Color.FromHtml("ffd23a"), t01 * 2f);
        else _susBarMat.AlbedoColor = Color.FromHtml("ffd23a").Lerp(Color.FromHtml("ff3b30"), (t01 - 0.5f) * 2f);
        _susBar.Scale = new Vector3(System.MathF.Max(0.05f, t01), 1f, 1f);
    }

    /// <summary>Static lying-down pose used when no ragdoll is active (port of fallback pratfall).</summary>
    public void PlayKnockoutPose()
    {
        Visual.Rotation = new Vector3(-System.MathF.PI / 2f, 0f, 0f);
        Visual.Position = new Vector3(0f, 0.35f, 0f);
    }

    /// <summary>Ragdoll system attaches its node tree here; null when cleared/settled.</summary>
    public Node? ActiveRagdoll { get; set; }

    public void SetVisibleRec(bool visible) => Visible = visible;

    public override void _PhysicsProcess(double delta)
    {
        Rotation = new Vector3(0f, Facing, 0f);
    }
}




