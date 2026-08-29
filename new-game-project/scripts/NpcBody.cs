using Godot;

/// <summary>
/// Visual + presentation layer for one NPC (port of npc.ts NPC class).
/// Owns: character-specific GLB/FBX visuals (tinted per archetype), name Label3D,
/// suspicion bar, text emotes, sleeping indicator, idle/walk animation,
/// primitive fallback when an authored model cannot be loaded. Runtime-constructed only.
/// Layout: root (position + yaw) -> Visual child (KO pose rotates this);
/// labels attach to root so they never tip over with the body.
/// </summary>
public partial class NpcBody : Node3D
{
    public string DisplayName { get; private set; } = "";
    public Archetype Arch { get; private set; }
    public ArchetypeSpec Spec { get; private set; } = Specs.Table[Archetype.Drone];
    public bool UsingRiggedModel { get; private set; }
    public float Facing { get; set; }
    public Node3D Visual { get; private set; } = null!;
    public Skeleton3D? Skeleton { get; private set; }
    public AnimationPlayer? Anim { get; private set; }

    private string _idleClip = "";
    private string _walkClip = "";
    private string _runClip = "";
    private string _workdayClip = "";
    private string _currentClip = "";
    private string _turnLeftClip = "";
    private string _turnRightClip = "";
    private string _turnLeft90Clip = "";
    private string _turnRight90Clip = "";
    private string _strafeLeftClip = "";
    private string _strafeRightClip = "";
    private Vector3 _lastPos;
    private bool _moving;
    private float _lastYaw;
    private bool _runRequest;
    private WorkdayState _workdayState = WorkdayState.Arriving;
    private Label3D? _nameTag;
    private MeshInstance3D? _susBar;
    private readonly StandardMaterial3D _susBarMat = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Color.FromHtml("39d97a"), NoDepthTest = true };
    private Label3D? _emote;
    private Label3D? _activityTag;
    private Label3D? _zzz;
    private float _bobTime;
    private bool _sleeping;
    private WorkdayState _lastVisualState = WorkdayState.Arriving;
    private float _sleepY = 2.7f;

    public override void _Ready()
    {
        Visual = new Node3D { Name = "Visual" };
        AddChild(Visual);
        BuildModel();
        _nameTag = new Label3D { Text = $"{DisplayName}\n{Spec.Label}", FontSize = 30, OutlineSize = 10, Modulate = Colors.White, Position = new Vector3(0f, 2.25f, 0f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.002f };
        AddChild(_nameTag);
        _susBar = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1.1f, 0.12f) }, Position = new Vector3(0f, 2.0f, 0f), MaterialOverride = _susBarMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_susBar);
        if (Arch == Archetype.Slob) ShowSleeping(true);
    }

    public void Init(string name, Archetype arch)
    {
        DisplayName = name;
        Arch = arch;
        Spec = Specs.Table[arch];
        Name = $"Npc_{name}";
        if (Visual != null && IsInsideTree())
        {
            foreach (var child in Visual.GetChildren()) child.QueueFree();
            UsingRiggedModel = false;
            Skeleton = null;
            Anim = null;
            _idleClip = _walkClip = _runClip = _workdayClip = _currentClip = _turnLeftClip = _turnRightClip = "";
        _turnLeft90Clip = _turnRight90Clip = _strafeLeftClip = _strafeRightClip = "";
            BuildModel();
        }
    }

    private static readonly string[] CharacterModelPaths =
    {
        "res://assets/models/AgentX.glb",
        "res://assets/models/Bob.glb",
        "res://assets/models/Sleepy Steve.glb",
        "res://assets/models/Nervous+Ned.glb",
        "res://assets/models/BossmanT-Pose.fbx",
    };

    private static readonly System.Collections.Generic.Dictionary<string, string> CharacterModels = new()
    {
        ["Agent Red"] = "res://assets/models/AgentX.glb",
        ["Bob"] = "res://assets/models/Bob.glb",
        ["Sleepy Steve"] = "res://assets/models/Sleepy Steve.glb",
        ["Nervous Ned"] = "res://assets/models/Nervous+Ned.glb",
        ["Mr Purple"] = "res://assets/models/BossmanT-Pose.fbx",
    };
    private static readonly ConfigFile SavedAdjustments = new();
    private static bool _loggedClips;
    private static bool _adjustmentsLoaded;

    private static void EnsureAdjustmentsLoaded()
    {
        if (_adjustmentsLoaded) return;
        _adjustmentsLoaded = true;
        SavedAdjustments.Load("user://animation-sandbox-adjustments.cfg");
    }

    private void ApplySavedAdjustment(Node3D model)
    {
        EnsureAdjustmentsLoaded();
        string key = System.IO.Path.GetFileName(ResolveModelPath());
        // ConfigFile.GetValue returns a Godot.Variant, which does not implement IConvertible --
        // Convert.ToSingle threw on every NPC, aborting _Ready before name tags, suspicion bars,
        // skeleton and AnimationPlayer were ever wired up.
        float scale = SavedAdjustments.GetValue(key, "scale", 1f).AsSingle();
        float yaw = SavedAdjustments.GetValue(key, "yaw", 0f).AsSingle();
        float height = SavedAdjustments.GetValue(key, "height", 0f).AsSingle();
        model.Scale *= scale;
        model.Rotation = new Vector3(0f, yaw, 0f);
        model.Position = new Vector3(0f, height, 0f);
    }

    private string ResolveModelPath()
    {
        if (!string.IsNullOrEmpty(DisplayName) && CharacterModels.TryGetValue(DisplayName, out var namedPath)) return namedPath;
        int hash = 17;
        foreach (char c in DisplayName) hash = unchecked(hash * 31 + c);
        return CharacterModelPaths[System.Math.Abs(hash) % CharacterModelPaths.Length];
    }

    private void BuildModel()
    {
        string modelPath = ResolveModelPath();
        PackedScene? glb = null;
        try { glb = ResourceLoader.Load<PackedScene>(modelPath); } catch { }
        if (glb != null)
        {
            var model = glb.Instantiate() as Node3D;
            if (model != null)
            {
                Visual.AddChild(model);
                UsingRiggedModel = true;
                float height = MeasureHeight(model);
                model.Scale = Vector3.One * (height > 0.1f ? 1.78f / height : 1f);
                ApplySavedAdjustment(model);
                foreach (var mi in model.FindChildren("*", "MeshInstance3D", true))
                {
                    var mesh = (MeshInstance3D)mi;
                    if (mesh.Mesh == null) continue;
                    for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                    {
                        var src = mesh.GetActiveMaterial(i) as StandardMaterial3D;
                        if (src == null) continue;
                        var material = (StandardMaterial3D)src.Duplicate();
                        material.AlbedoColor = src.AlbedoColor.Lerp(Spec.Tint, 0.55f);
                        mesh.SetSurfaceOverrideMaterial(i, material);
                    }
                }
                var skeletons = model.FindChildren("*", "Skeleton3D", true);
                Skeleton = skeletons.Count > 0 ? (Skeleton3D)skeletons[0] : null;
                var players = model.FindChildren("*", "AnimationPlayer", true);
                Anim = players.Count > 0 ? (AnimationPlayer)players[0] : null;
                AnimationLib.EnsureLoaded();
                if (Anim == null) { Anim = new AnimationPlayer(); model.AddChild(Anim); }
                AnimationLib.InjectClips(Anim, Skeleton);
                // exact-name-first matching: a plain "contains" pass picks "left strafe walking"
                // as the walk cycle, because it sorts before "walking"
                _idleClip = PickClip("idle");
                _walkClip = PickClip("walking", "walk");
                _runClip = PickClip("running", "run");
                _turnLeftClip = PickClip("left turn", "left turn 90");
                _turnRightClip = PickClip("right turn", "right turn 90");
                _turnLeft90Clip = PickClip("left turn 90");
                _turnRight90Clip = PickClip("right turn 90");
                _strafeLeftClip = PickClip("left strafe walking", "left strafe");
                _strafeRightClip = PickClip("right strafe walking", "right strafe");
                AnimDiag.ReportBody(DisplayName, modelPath, true, Skeleton, Anim);
                if (!_loggedClips)
                {
                    _loggedClips = true;
                    GD.Print($"[NpcBody] clips resolved for {DisplayName}: idle='{_idleClip}' walk='{_walkClip}' " +
                             $"run='{_runClip}' turnL='{_turnLeftClip}' turnR='{_turnRightClip}'");
                }
                if (_idleClip.Length > 0) { Anim.Play(_idleClip); _currentClip = _idleClip; }
                return;
            }
        }
        UsingRiggedModel = false;
        AnimDiag.ReportBody(DisplayName, modelPath, false, null, null);
        Visual.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.32f, Height = 1.34f }, Position = new Vector3(0f, 0.85f, 0f), MaterialOverride = MakeTinted(Spec.Tint), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        Visual.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f }, Position = new Vector3(0f, 1.62f, 0f), MaterialOverride = MakeTinted(Color.FromHtml("e8b98a")) });
    }

    private static StandardMaterial3D MakeTinted(Color c) => new() { AlbedoColor = c, Roughness = 0.85f };
    private static float MeasureHeight(Node3D model)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var child in model.FindChildren("*", "MeshInstance3D", true))
        {
            var mi = (MeshInstance3D)child;
            if (mi.Mesh == null) continue;
            var aabb = mi.Mesh.GetAabb();
            minY = System.MathF.Min(minY, aabb.Position.Y);
            maxY = System.MathF.Max(maxY, aabb.End.Y);
        }
        return maxY > minY ? maxY - minY : 1.8f;
    }

    /// <summary>
    /// Best clip for a set of preferences, most specific first. Exact basename beats a
    /// substring hit, so "walking" wins over "left strafe walking".
    /// </summary>
    private string PickClip(params string[] preferences)
    {
        if (Anim == null) return "";
        string best = "";
        int bestScore = 0;
        foreach (string clip in Anim.GetAnimationList())
        {
            string bare = clip[(clip.LastIndexOf('/') + 1)..].ToLowerInvariant();
            for (int i = 0; i < preferences.Length; i++)
            {
                string want = preferences[i];
                int score = bare == want ? 100 - i : bare.Contains(want) ? 50 - i : 0;
                if (score > bestScore) { bestScore = score; best = clip; }
            }
        }
        return best;
    }

    public void SetMoving(bool moving, bool run = false)
    {
        _runRequest = run;
        _moving = moving;
        if (Anim == null || !UsingRiggedModel) return;
        string target = moving ? (run && _runClip.Length > 0 ? _runClip : _walkClip.Length > 0 ? _walkClip : _idleClip) : (_workdayClip.Length > 0 ? _workdayClip : _idleClip);
        if (target.Length > 0 && target != _currentClip && Anim.HasAnimation(target)) { Anim.Play(target, 0.18); _currentClip = target; }
    }

    public void SetWorkdayState(WorkdayState state)
    {
        _workdayState = state;
        _workdayClip = FindWorkdayClip(state);
        SetActivityTag(WorkdayLabel(state));
        if (state == WorkdayState.FeelingSleepy) ShowSleeping(true); else if (_sleeping && state != WorkdayState.PanicAttack) ShowSleeping(false);
        if (Anim != null && _workdayClip.Length > 0 && _currentClip != _workdayClip) { Anim.Play(_workdayClip, 0.18); _currentClip = _workdayClip; }
    }

    private string FindWorkdayClip(WorkdayState state)
    {
        if (Anim == null) return "";
        string[] tokens = state switch
        {
            WorkdayState.WalkingToPrinter or WorkdayState.MeetingWalk or WorkdayState.WalkingThinking or WorkdayState.AnxiousWalking => new[] { "walk", "strafe" },
            WorkdayState.WorkingAtDesk or WorkdayState.DepressedWorking or WorkdayState.HappyWorking or WorkdayState.WorriedWorking or WorkdayState.DistractedWorking or WorkdayState.AnnoyedWorking or WorkdayState.PickingUpSlack or WorkdayState.SuspiciousWorking or WorkdayState.NotPayingAttention or WorkdayState.EngrossedWorking or WorkdayState.AnxiousWorking => new[] { "work", "typing", "write", "idle" },
            WorkdayState.Reading => new[] { "read", "book", "idle" }, WorkdayState.WaitingAtPrinter or WorkdayState.Printing or WorkdayState.PrinterBroken => new[] { "print", "work", "idle" }, WorkdayState.DoomScrolling => new[] { "phone", "idle" }, WorkdayState.PhoneCall => new[] { "phone", "talk", "idle" }, WorkdayState.PanicAttack => new[] { "panic", "idle" }, WorkdayState.OnBreak or WorkdayState.WaterCooler or WorkdayState.CoffeeBreak or WorkdayState.Meeting or WorkdayState.AnxiousMeeting => new[] { "talk", "idle" }, WorkdayState.Speed or WorkdayState.Ecstasy => new[] { "run", "walk", "strafe", "idle" }, WorkdayState.Arriving => new[] { "walk", "idle" }, _ => new[] { "idle" },
        };
        foreach (string clip in Anim.GetAnimationList()) { string low = clip.ToLowerInvariant(); foreach (string token in tokens) if (low.Contains(token) && (!low.Contains("idle") || token == "idle")) return clip; }
        return _idleClip;
    }

    private void SetActivityTag(string text)
    {
        if (_activityTag == null) { _activityTag = new Label3D { FontSize = 22, OutlineSize = 8, Position = new Vector3(0f, 2.58f, 0f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.0018f }; AddChild(_activityTag); }
        _activityTag.Text = text; _activityTag.Modulate = WorkdayColor(_workdayState);
    }

    private static string WorkdayLabel(WorkdayState state) => state switch
    {
        WorkdayState.WorkingAtDesk => "working at desk", WorkdayState.WalkingToPrinter => "walking to printer", WorkdayState.WaitingAtPrinter => "waiting at printer", WorkdayState.Printing => "printing", WorkdayState.PrinterBroken => "printer not working", WorkdayState.Toilet => "in the toilet", WorkdayState.OnBreak => "on break", WorkdayState.MeetingWalk => "walking to meeting", WorkdayState.Meeting => "in meeting", WorkdayState.AnxiousMeeting => "anxious / meeting", WorkdayState.PhoneCall => "on phone call", WorkdayState.DoomScrolling => "doom scrolling", WorkdayState.WaterCooler => "at water cooler", WorkdayState.CoffeeBreak => "coffee break", WorkdayState.StationaryUse => "stationary use", WorkdayState.WalkingThinking => "walking / thinking", WorkdayState.DepressedWorking => "working / depressed", WorkdayState.HappyWorking => "working / happy", WorkdayState.WorriedWorking => "working / worried", WorkdayState.DistractedWorking => "working / distracted", WorkdayState.AnnoyedWorking => "working / annoyed", WorkdayState.PickingUpSlack => "picking up slack", WorkdayState.SuspiciousWorking => "working / suspicious", WorkdayState.NotPayingAttention => "not paying attention", WorkdayState.EngrossedWorking => "engrossed at computer", WorkdayState.FeelingSick => "feeling sick", WorkdayState.FeelingHorny => "feeling horny", WorkdayState.FeelingCurious => "feeling curious", WorkdayState.FeelingSleepy => "feeling sleepy", WorkdayState.FeelingDrunk => "feeling drunk", WorkdayState.Speed => "on speed", WorkdayState.Stoned => "stoned", WorkdayState.LSD => "on LSD", WorkdayState.KHole => "in a k-hole", WorkdayState.Ecstasy => "on ecstasy", WorkdayState.AnxiousWalking => "anxious / walking", WorkdayState.AnxiousWorking => "anxious / working", WorkdayState.PanicAttack => "panic attack", WorkdayState.Reading => "reading", _ => "arriving",
    };

    private static Color WorkdayColor(WorkdayState state) => state switch { WorkdayState.HappyWorking => Color.FromHtml("67d99b"), WorkdayState.DepressedWorking => Color.FromHtml("8c98aa"), WorkdayState.WorriedWorking or WorkdayState.AnxiousWorking or WorkdayState.AnxiousWalking or WorkdayState.AnxiousMeeting => Color.FromHtml("ffd76a"), WorkdayState.AnnoyedWorking or WorkdayState.PanicAttack => Color.FromHtml("ff7c6e"), WorkdayState.Speed or WorkdayState.Ecstasy or WorkdayState.LSD => Color.FromHtml("d995dc"), _ => Color.FromHtml("d6e0dc") };

    /// <summary>
    /// Owns the clips that SetMoving cannot pick, because they depend on how the body is
    /// actually moving rather than on a bool: turning on the spot, and sidestepping.
    /// Leaves the plain forward walk/run/idle choice to SetMoving.
    /// </summary>
    private void UpdateLocomotionAnimation(double delta)
    {
        if (Anim == null || !UsingRiggedModel || ActiveRagdoll != null || delta <= 0) return;

        float dt = (float)delta;
        float yaw = GlobalRotation.Y;
        float yawRate = Mathf.AngleDifference(_lastYaw, yaw) / dt;
        _lastYaw = yaw;

        Vector3 pos = GlobalPosition;
        Vector3 velocity = (pos - _lastPos) / dt;
        _lastPos = pos;
        velocity.Y = 0f;

        bool onTurnClip = _currentClip == _turnLeftClip || _currentClip == _turnRightClip
                       || _currentClip == _turnLeft90Clip || _currentClip == _turnRight90Clip;
        bool onStrafeClip = _currentClip == _strafeLeftClip || _currentClip == _strafeRightClip;

        if (_moving && velocity.Length() > 0.05f)
        {
            // sidestepping: lateral travel dominates forward travel relative to facing
            Vector3 facing = new(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
            Vector3 right = new(facing.Z, 0f, -facing.X);
            float forward = velocity.Dot(facing);
            float lateral = velocity.Dot(right);
            if (MathF.Abs(lateral) > MathF.Abs(forward) * 1.2f)
            {
                string strafe = lateral > 0f ? _strafeRightClip : _strafeLeftClip;
                Play(strafe);
                return;
            }
            if (onStrafeClip) Play(_runRequest && _runClip.Length > 0 ? _runClip : _walkClip);
            return;
        }

        if (MathF.Abs(yawRate) > 0.7f)
        {
            // a sharp pivot uses the 90-degree clip where one exists
            bool sharp = MathF.Abs(yawRate) > 2.0f;
            string target = yawRate > 0f
                ? (sharp && _turnLeft90Clip.Length > 0 ? _turnLeft90Clip : _turnLeftClip)
                : (sharp && _turnRight90Clip.Length > 0 ? _turnRight90Clip : _turnRightClip);
            Play(target);
        }
        else if (onTurnClip || onStrafeClip)
        {
            Play(_workdayClip.Length > 0 ? _workdayClip : _idleClip);
        }
    }

    private void Play(string clip)
    {
        if (Anim == null || clip.Length == 0 || clip == _currentClip || !Anim.HasAnimation(clip)) return;
        Anim.Play(clip, 0.14);
        _currentClip = clip;
    }

    public override void _Process(double delta)
    {
        if (Anim != null && _walkClip.Length > 0 && Anim.CurrentAnimation == _walkClip) Anim.SpeedScale = _runRequest ? 1.7f : 1f;
        UpdateLocomotionAnimation(delta);
        if (_workdayState != _lastVisualState && Visual != null && ActiveRagdoll == null) { _lastVisualState = _workdayState; Visual.Rotation = Vector3.Zero; }
        if (Visual != null && ActiveRagdoll == null && _workdayState != WorkdayState.WorkingAtDesk) { float sway = System.MathF.Sin(_bobTime += (float)delta * (_workdayState is WorkdayState.Speed or WorkdayState.Ecstasy ? 8f : 2f)) * (_workdayState is WorkdayState.DistractedWorking or WorkdayState.DoomScrolling or WorkdayState.Stoned ? 0.025f : 0.008f); Visual.Rotation = new Vector3(0f, sway, 0f); }
        if (_zzz != null && _sleeping) _zzz.Position = new Vector3(0f, _sleepY + System.MathF.Sin(_bobTime += (float)delta * 2.5f) * 0.08f, 0f);
    }

    public void ShowEmote(string text) { ClearEmote(); _emote = new Label3D { Text = text, FontSize = 40, OutlineSize = 14, Modulate = Color.FromHtml("ffd76a"), Position = new Vector3(0f, 2.75f, 0f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.002f }; AddChild(_emote); }
    public void ClearEmote() { _emote?.QueueFree(); _emote = null; }
    public void ShowSleeping(bool on) { _sleeping = on; if (on && _zzz == null) { _zzz = new Label3D { Text = "Z z z", FontSize = 36, OutlineSize = 12, Modulate = Color.FromHtml("9fd8ff"), Position = new Vector3(0f, _sleepY, 0f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.002f }; AddChild(_zzz); } else if (!on && _zzz != null) { _zzz.QueueFree(); _zzz = null; } }
    public void SetSuspicion(float t01) { if (_susBar == null) return; bool show = t01 > 0.01f; _susBar.Visible = show; if (!show) return; t01 = Util.Clamp(t01, 0f, 1f); if (t01 < 0.5f) _susBarMat.AlbedoColor = Color.FromHtml("39d97a").Lerp(Color.FromHtml("ffd23a"), t01 * 2f); else _susBarMat.AlbedoColor = Color.FromHtml("ffd23a").Lerp(Color.FromHtml("ff3b30"), (t01 - 0.5f) * 2f); _susBar.Scale = new Vector3(System.MathF.Max(0.05f, t01), 1f, 1f); }
    public void PlayKnockoutPose() { Visual.Rotation = new Vector3(-System.MathF.PI / 2f, 0f, 0f); Visual.Position = new Vector3(0f, 0.35f, 0f); }
    public Node? ActiveRagdoll { get; set; }
    public void SetVisibleRec(bool visible) => Visible = visible;
    public override void _PhysicsProcess(double delta) { Rotation = new Vector3(0f, Facing, 0f); }
}
