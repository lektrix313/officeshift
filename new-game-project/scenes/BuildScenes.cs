using Godot;

/// <summary>
/// BUILD-TIME + TEST entry point (engines/godot.md rule: no runtime game logic
/// here). Single SceneTree script per assembly (Godot resolves exactly one via
/// --script; additional subclasses become uninstantiable). Dispatch on user args:
///
///   godot --headless --script res://scenes/BuildScenes.cs ++ world
///   godot --headless --script res://scenes/BuildScenes.cs ++ main
///
/// Proof capture (deterministic movie-writer run, engine stops via --quit-after):
///   godot --write-movie shots/frame.png --fixed-fps 30 --quit-after 450
///         --script res://scenes/BuildScenes.cs ++ capture
///   godot --write-movie shots/body_frame.png --fixed-fps 30 --quit-after 900
///         --script res://scenes/BuildScenes.cs ++ capture body
///   godot --write-movie shots/layout_frame.png --fixed-fps 30 --quit-after 800
///         --script res://scenes/BuildScenes.cs ++ capture layout
///   godot --write-movie shots/workday_frame.png --fixed-fps 30 --quit-after 1050
///         --script res://scenes/BuildScenes.cs ++ capture workday
///   godot --write-movie shots/reactions_frame.png --fixed-fps 30 --quit-after 800
///         --script res://scenes/BuildScenes.cs ++ capture reactions
///   godot --write-movie shots/object_states_frame.png --fixed-fps 30 --quit-after 500
///         --script res://scenes/BuildScenes.cs ++ capture object_states
/// </summary>
public partial class BuildScenes : SceneTree
{
    public override void _Initialize()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Length == 0)
        {
            GD.PushError("usage: --script res://scenes/BuildScenes.cs ++ world|main|capture");
            Quit(1);
            return;
        }

        switch (args[0])
        {
            case "world": BuildWorld(); break;
            case "main": BuildMain(); break;
            case "capture": StartCapture(); break;
            default:
                GD.PushError($"unknown target '{args[0]}' (expected world|main|capture)");
                Quit(1);
                break;
        }    }

    /// <summary>Instances the running game plus a frame-keyed driver for the proof recording.
    /// Second user arg picks the scenario: ++ capture sim | ++ capture portal</summary>
    private void StartCapture()
    {
        var main = GD.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate();
        if (main == null) { GD.PushError("capture: main.tscn missing"); Quit(1); return; }
        var args = OS.GetCmdlineUserArgs();
        var mode = args.Length > 1 ? args[1] : "sim";
        Root.AddChild(main);
        CurrentScene = (Node)main;
        Root.AddChild(new CaptureDriver(mode));
        // no Quit(): the engine runs until --quit-after frames elapse
    }

    private static StandardMaterial3D MakeMat(Color c, bool emissive = false)
    {
        var m = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.9f };
        if (emissive)
        {
            m.EmissionEnabled = true;
            m.Emission = c;
            m.EmissionEnergyMultiplier = 1.6f;
        }
        return m;
    }

    private static MeshInstance3D AddBox(Node3D parent, float x, float y, float z,
        float w, float h, float d, Color color, bool emissive = false)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(w, h, d) },
            Position = new Vector3(x, y, z),
            MaterialOverride = MakeMat(color, emissive),
            CastShadow = h > 0.4f ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private static void AddCollider(Node3D parent, float x, float y, float z, float w, float h, float d)
    {
        var body = new StaticBody3D { Position = new Vector3(x, y, z) };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(w, h, d) },
        });
        parent.AddChild(body);
    }

    private void BuildWorld()
    {
        var root = new Node3D { Name = "World" };
        root = (Node3D)SceneSaveUtil.AttachScript(root, "res://scripts/World.cs");

        // floor slab: collision so CharacterBody stands on it
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(64f, 0.2f, 44f) },
            Position = new Vector3(0f, -0.1f, 0f),
            MaterialOverride = MakeMat(Color.FromHtml("9aa3ad")),
        });
        AddCollider(root, 0f, -0.1f, 0f, 64f, 0.2f, 44f);

        foreach (var c in WorldData.Carpets)
        {
            var carpet = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(c.W, c.D) },
                Position = new Vector3(c.X, 0.01f, c.Z),
                MaterialOverride = MakeMat(c.Color),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            carpet.RotateX(-System.MathF.PI / 2f);
            root.AddChild(carpet);
        }

        foreach (var w in WorldData.Walls)
        {
            float cx = (w.X1 + w.X2) / 2f;
            float cz = (w.Z1 + w.Z2) / 2f;
            float len = System.MathF.Max(System.MathF.Abs(w.X2 - w.X1), 0.3f);
            float wid = System.MathF.Max(System.MathF.Abs(w.Z2 - w.Z1), 0.3f);
            AddBox(root, cx, WorldData.WallHeight / 2f, cz, len, WorldData.WallHeight, wid, Color.FromHtml("d8d4cc"));
            AddCollider(root, cx, WorldData.WallHeight / 2f, cz, len, WorldData.WallHeight, wid);
        }

        // Door frames are visual-only blockout markers; the gaps remain walkable.
        foreach (var door in WorldData.DoorFrames)
        {
            float half = door.Width / 2f;
            AddBox(root, door.X - half, 1.1f, door.Z, 0.14f, 2.2f, 0.22f, Color.FromHtml("596575"));
            AddBox(root, door.X + half, 1.1f, door.Z, 0.14f, 2.2f, 0.22f, Color.FromHtml("596575"));
            AddBox(root, door.X, 2.2f, door.Z, door.Width + 0.28f, 0.14f, 0.22f, Color.FromHtml("596575"));
        }

        foreach (var p in WorldData.Props)
        {
            bool emissive = p.Color == Color.FromHtml("35f0a0") || p.Color == Color.FromHtml("0af0ff");
            AddBox(root, p.X, p.Y, p.Z, p.W, p.H, p.D, p.Color, emissive);
            if (p.Solid)
                AddCollider(root, p.X, p.Y, p.Z, p.W, p.H, p.D);
        }

        foreach (var pc in WorldData.PodCenters)
        {
            foreach (var part in WorldData.PodPartitions(pc))
            {
                AddBox(root, part.X, part.Y, part.Z, part.W, part.H, part.D, part.Color);
                AddCollider(root, part.X, part.Y, part.Z, part.W, part.H, part.D);
            }
            foreach (var (top, body, monitor) in WorldData.PodDesks(pc))
            {
                AddBox(root, top.X, top.Y, top.Z, top.W, top.H, top.D, top.Color);
                AddBox(root, body.X, body.Y, body.Z, body.W, body.H, body.D, body.Color);
                AddCollider(root, body.X, body.Y, body.Z, body.W, body.H, body.D);
                AddBox(root, monitor.X, monitor.Y, monitor.Z, monitor.W, monitor.H, monitor.D, monitor.Color);
            }
        }

        foreach (var pl in WorldData.Plants)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.45f, Height = 0.6f },
                Position = new Vector3(pl.X, 0.3f, pl.Z),
                MaterialOverride = MakeMat(Color.FromHtml("a04a2e")),
            });
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.6f, Height = 1.2f },
                Position = new Vector3(pl.X, 1.1f, pl.Z),
                MaterialOverride = MakeMat(Color.FromHtml("3f8a3a")),
            });
        }

        foreach (var b in WorldData.BeanBags)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.8f, Height = 1.6f },
                Scale = new Vector3(1f, 0.55f, 1f),
                Position = new Vector3(b.X, 0.4f, b.Z),
                MaterialOverride = MakeMat(Color.FromHtml("e0a33a")),
            });
        }

        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.3f, Height = 0.1f },
            Position = new Vector3(15f, 0.05f, -15f),
            MaterialOverride = MakeMat(Color.FromHtml("555a63")),
        });

        // Lets the runtime distinguish a regenerated scene from the legacy artifact.
        root.AddChild(new Node { Name = "LayoutBlockoutV13" });

        var ok = SceneSaveUtil.PackAndValidate(root, "res://scenes/world.tscn");
        Quit(ok ? 0 : 1);
    }

    private void BuildMain()
    {
        var root = new Node3D { Name = "Main" };
        root = (Node3D)SceneSaveUtil.AttachScript(root, "res://scripts/GameMode.cs");

        var ok = SceneSaveUtil.PackAndValidate(root, "res://scenes/main.tscn");
        Quit(ok ? 0 : 1);
    }
}

/// <summary>Frame-keyed choreography for the deterministic proof recordings.</summary>
public partial class CaptureDriver : Node
{
    private readonly string _mode;
    private int _frame;
    private bool _eDown;

    public CaptureDriver(string mode) { _mode = mode; }

    public override void _Process(double delta)
    {
        _frame++;
        var gm = GameMode.Instance;
        if (gm?.Player == null || !IsInstanceValid(gm.Player)) return;
        var p = gm.Player;

        if (_mode == "portal") PortalStep(gm, p);
        else if (_mode == "props") PropsStep(gm, p);
        else if (_mode == "hr") HrStep(gm, p);
        else if (_mode == "wardrobe") WardrobeStep(gm, p);
        else if (_mode == "chaos") ChaosStep(gm, p);
        else if (_mode == "missions") MissionsStep(gm, p);
        else if (_mode == "body") BodyStep(gm, p);
        else if (_mode == "staff") StaffStep(gm, p);
        else if (_mode == "frame") FrameStep(gm, p);
        else if (_mode == "hearing") HearingStep(gm, p);
        else if (_mode == "layout") LayoutStep(gm, p);
        else if (_mode == "workday") WorkdayStep(gm, p);
        else if (_mode == "reactions") ReactionsStep(gm, p);
        else if (_mode == "object_states") ObjectStatesStep(gm, p);
        else SimStep(gm, p);
    }

    private void TapE()
    {
        if (!_eDown) { Input.ActionPress("interact"); _eDown = true; }
    }

    private void ReleaseE()
    {
        if (_eDown) { Input.ActionRelease("interact"); _eDown = false; }
    }

    private static void Glide(PlayerController p, int frame, int s, int e, float x0, float z0, float x1, float z1)
    {
        if (frame < s || frame > e) return;
        float t = (frame - s) / (float)(e - s);
        p.GlobalPosition = new Vector3(Mathf.Lerp(x0, x1, t), 0f, Mathf.Lerp(z0, z1, t));
        float dx = x1 - x0;
        float dz = z1 - z0;
        p.Yaw = System.MathF.Atan2(-dx, -dz);
    }

    // vending buy → spike coffee → brew (pulls 2 NPCs → bathroom runs) → microwave fish (stink)
    private void SimStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 90, 0f, 18.5f, 29.6f, -16.6f);
        if (_frame == 95) TapE();
        if (_frame == 98) ReleaseE();

        Glide(p, _frame, 100, 160, 29.6f, -16.6f, 25.3f, -19.6f);
        if (_frame == 165) TapE();
        if (_frame == 167) ReleaseE();

        if (_frame == 240) TapE();
        if (_frame == 305) ReleaseE();

        Glide(p, _frame, 310, 390, 25.3f, -19.6f, 26.5f, -19.5f);
        if (_frame == 395) TapE();
        if (_frame == 490) ReleaseE();
    }

    // OmniPortal email to Keith ([goto:breakroom]) → intercept him for live chat
    private void PortalStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 140, 0f, 18.5f, 6.2f, 10.4f);
        if (_frame == 145) TapE();
        if (_frame == 147) ReleaseE();

        if (_frame == 210)
        {
            gm.Portal?.ComposeTo("Keith", "quick sync", "Keith, meet me in the break room ASAP. Bring the spreadsheet.");
            gm.CloseUI();
            var dir = (new Vector3(20f, 0f, -15f) - p.FeetPos).Normalized();
            p.Yaw = System.MathF.Atan2(-dir.X, -dir.Z);
        }

        if (_frame == 300)
        {
            var keith = gm.Npcs.Find(n => n.NpcName == "Keith");
            if (keith != null)
            {
                p.GlobalPosition = keith.Pos + new Vector3(0.9f, 0f, 0.9f);
                var dir = (keith.Pos - p.FeetPos).Normalized();
                p.Yaw = System.MathF.Atan2(-dir.X, -dir.Z);
            }
        }
        if (_frame == 315) gm.TryStartTalk();
        if (_frame == 345) gm.Talk?.SubmitForCapture("Relax. You saw NOTHING. Walk with me.");
    }

    // grab keyboard → throw toward Keith → noise pulls him in → he shrugs it off
    private void PropsStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 110, 0f, 18.5f, -13.8f, -5.6f);
        if (_frame == 115) TapE();
        if (_frame == 117) ReleaseE();

        if (_frame == 130)
        {
            var dir = (new Vector3(0f, 0f, -2f) - p.FeetPos).Normalized();
            p.Yaw = System.MathF.Atan2(-dir.X, -dir.Z);
        }
        if (_frame == 140) TapE();
        if (_frame == 142) ReleaseE();

        if (_frame > 143 && _frame % 30 == 0)
        {
            var kb = gm.Items.Find(i => i.ItemType == "keyboard" && IsInstanceValid(i) && !i.Freeze);
            if (kb != null && gm.Player != null)
            {
                var dir = (kb.GlobalPosition - new Vector3(p.GlobalPosition.X, kb.GlobalPosition.Y, p.GlobalPosition.Z));
                if (dir.Length() > 0.5f)
                {
                    dir = dir.Normalized();
                    p.Yaw = System.MathF.Atan2(-dir.X, -dir.Z);
                    p.Pitch = -0.15f;
                }
            }
        }
    }

    // microwave fish ON CAMERA → case climbs → sprint to reception → shred tapes → case closes
    private void HrStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 140, 0f, 18.5f, 26.5f, -19.5f);
        if (_frame == 145) TapE();
        if (_frame == 240) ReleaseE();

        Glide(p, _frame, 245, 330, 26.5f, -19.5f, -2f, 19f);
        if (_frame == 340) TapE();
        if (_frame == 465) ReleaseE();
    }

    // locker → IT uniform → loiter in the server room with zero suspicion
    private void WardrobeStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 110, 0f, 18.5f, 27f, 12.2f);
        if (_frame == 115) TapE();
        if (_frame == 117) ReleaseE();

        Glide(p, _frame, 120, 210, 27f, 12.2f, -21.5f, -15.8f);

        if (_frame == 220)
        {
            var dir = (new Vector3(-22f, 0f, -17.2f) - p.FeetPos).Normalized();
            p.Yaw = System.MathF.Atan2(-dir.X, -dir.Z);
        }
    }

    // grab mug → throw into the walkway → coffee puddle → NPC slips → KO → curiosity
    private void ChaosStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 100, 0f, 18.5f, -16.5f, -3.6f);
        if (_frame == 105) TapE();
        if (_frame == 107) ReleaseE();

        if (_frame == 115)
        {
            var dir = (new Vector3(-10f, 0f, -2.5f) - p.FeetPos).Normalized();
            p.Yaw = System.MathF.Atan2(-dir.X, -dir.Z);
        }
        if (_frame == 120) TapE();
        if (_frame == 122) ReleaseE();
    }

    // accept OMNI-KEYS → email Keith to the break room → steal blueprints → win
    private void MissionsStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        if (_frame == 30) gm.AcceptContractById("OMNI-KEYS");

        Glide(p, _frame, 40, 130, 0f, 18.5f, 6.2f, 10.4f);
        if (_frame == 135) TapE();
        if (_frame == 137) ReleaseE();

        if (_frame == 140)
            gm.Portal?.ComposeTo("Keith", "sync", "Keith, meet me in the break room now. Bring the spreadsheet.");
        if (_frame == 150) gm.CloseUI();

        Glide(p, _frame, 150, 280, 6.2f, 10.4f, -21.5f, -15.8f);
        if (_frame == 285) TapE();
        if (_frame == 395) ReleaseE();

        Glide(p, _frame, 400, 490, -21.5f, -15.8f, 26f, -11.5f);
        if (_frame == 495) TapE();
        if (_frame == 497) ReleaseE();
    }

    // open the diegetic directory, then surface the most distinct staff member in-world
    private void StaffStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (_frame == 45)
            gm.Portal?.OpenStaffDirectory();
        if (_frame == 240)
            gm.Portal?.Close();
        if (_frame == 270)
        {
            var keith = gm.Npcs.Find(n => n.NpcName == "Keith");
            if (keith != null)
            {
                p.GlobalPosition = keith.Pos + new Vector3(0.9f, 0f, 0.9f);
                p.Yaw = System.MathF.Atan2(-(keith.Pos.X - p.FeetPos.X), -(keith.Pos.Z - p.FeetPos.Z));
            }
        }
        if (_frame == 300)
            gm.TryStartTalk();
    }

    // file a murder allegation against Tom → watch the office feed distort it → HR removes him
    private void FrameStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (_frame == 50)
        {
            gm.Portal?.Open();
            gm.Portal?.FileReportForCapture(
                "Tom",
                "A MURDER",
                "He was seen near the server room before the incident and has been selling silence for months.");
        }
        if (_frame == 160)
            gm.Portal?.OpenStaffDirectory();
        if (_frame == 320)
            gm.Portal?.Close();
    }

    // compresses the one-hour workday into a short capture while staff cycle through the FSM
    private void WorkdayStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            gm.WorkdayTimeScale = 120f;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (_frame == 30) gm.Toast("WORKDAY CAPTURE: 09:00 to 17:00 compressed into this recording.", ToastKind.Info);
        if (_frame == 920) gm.WorkdayTimeScale = 1f;
    }

    // player crime, then an ambient fire alarm; inspect activation/action/cooldown telemetry
    private void ReactionsStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (_frame == 45)
        {
            var victim = gm.Npcs.Find(n => n.NpcName == "Keith");
            if (victim != null)
            {
                p.GlobalPosition = victim.Pos + new Vector3(0.8f, 0f, 0f);
                gm.OnBonkLanded(victim, Vector3.Right);
            }
        }
        if (_frame == 150) gm.PullFireAlarm();
        if (_frame == 260) gm.Portal?.OpenStaffDirectory();
        if (_frame == 700) gm.Portal?.Close();
    }

    // trigger printer/computer/door state changes and open the staff telemetry view
    private void ObjectStatesStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (_frame == 45) gm.SetOfficeObjectState("printer", OfficeObjectState.OutOfPaper, true);
        if (_frame == 80) gm.SetOfficeObjectState("printer", OfficeObjectState.Jammed, false);
        if (_frame == 115) gm.SetOfficeObjectState("computer", OfficeObjectState.Glitchy, true);
        if (_frame == 150) gm.SetOfficeObjectState("computer", OfficeObjectState.ITCalled, false);
        if (_frame == 190) gm.TryAccessOfficeObject("door", "wrong-card");
        if (_frame == 230) gm.SetOfficeObjectState("coffeemaker", OfficeObjectState.Brewing, true);
        if (_frame == 280) gm.SetOfficeObjectState("watercooler", OfficeObjectState.InUse, false);
        if (_frame == 330) gm.Portal?.OpenStaffDirectory();
        if (_frame == 480) gm.Portal?.Close();
    }

    // walk the camera through the new meeting rooms and HR suite blockout
    private void LayoutStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        Glide(p, _frame, 20, 110, 0f, 18.5f, -25f, 13f);
        Glide(p, _frame, 120, 220, -25f, 13f, -25f, 19.5f);
        Glide(p, _frame, 230, 330, -25f, 19.5f, -12f, 13f);
        Glide(p, _frame, 340, 440, -12f, 13f, -12f, 19.5f);
        Glide(p, _frame, 450, 550, -12f, 19.5f, 13f, 13f);
        Glide(p, _frame, 560, 660, 13f, 13f, 13.5f, 19.5f);
        Glide(p, _frame, 670, 760, 13.5f, 19.5f, 0f, 18.5f);
    }

    // file a case, challenge the contradictory witness, then appeal before HR removes Tom
    private void HearingStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (_frame == 45)
        {
            gm.Portal?.Open();
            gm.Portal?.FileReportForCapture("Tom", "A MURDER", "Tom's stapler budget has always been suspicious.");
        }
        if (_frame == 80)
            gm.OpenHearing();
        if (_frame == 120)
            gm.ChallengeTestimony(1);
        if (_frame == 150)
            gm.AppealCase();
        if (_frame == 190)
            gm.Portal?.OpenStaffDirectory();
    }

    // dispose Keith → hide Susan until discovery/police interview → forge Janet's resignation
    private void BodyStep(GameMode gm, PlayerController p)
    {
        if (_frame == 20 && !gm.Over)
        {
            gm.Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        if (_frame == 35)
        {
            var keith = gm.Npcs.Find(n => n.NpcName == "Keith");
            var chute = gm.WorldRef?.HideSpots.Find(s => s.Id == "chute");
            if (keith != null && chute != null)
            {
                p.GlobalPosition = keith.Pos + new Vector3(0.8f, 0f, 0f);
                gm.OnBonkLanded(keith, Vector3.Right);
                p.Carrying = keith;
                gm.DisposeBody(keith, chute);
                p.GlobalPosition = new Vector3(11.5f, 0f, -20.5f);
            }
        }

        if (_frame == 90)
        {
            var susan = gm.Npcs.Find(n => n.NpcName == "Susan");
            var lamp = gm.WorldRef?.HideSpots.Find(s => s.Id == "lamp");
            if (susan != null && lamp != null)
            {
                p.GlobalPosition = susan.Pos + new Vector3(0.8f, 0f, 0f);
                gm.OnBonkLanded(susan, Vector3.Right);
                lamp.SmellDelay = 3f;
                p.Carrying = susan;
                gm.HideBody(susan, lamp);
                p.Carrying = null;
                p.GlobalPosition = new Vector3(15f, 0f, -15f);
            }
        }

        if (_frame == 120)
        {
            var janet = gm.Npcs.Find(n => n.NpcName == "Janet");
            var closet = gm.WorldRef?.HideSpots.Find(s => s.Id == "closet");
            if (janet != null && closet != null)
            {
                p.GlobalPosition = janet.Pos + new Vector3(0.8f, 0f, 0f);
                gm.OnBonkLanded(janet, Vector3.Right);
                closet.SmellDelay = 999f;
                p.Carrying = janet;
                gm.HideBody(janet, closet);
                p.Carrying = null;
            }
        }

        // The smell clock selects a nearby NPC, who walks to the hidden body.
        // Answer every interview prompt once it opens so the capture reaches the next beat.
        if (gm.Interview?.IsOpen == true)
            gm.Interview.SubmitForCapture("I was at my desk reviewing the morning meeting notes and answering email.");

        if (_frame == 660)
        {
            var janet = gm.Npcs.Find(n => n.NpcName == "Janet");
            if (janet != null) gm.OpenResignation(janet);
        }
        if (_frame == 690)
            gm.Portal?.SubmitForgeForCapture("I am relocating for a family opportunity and would like to resign effective immediately. Thank you for everything.");
    }
}
