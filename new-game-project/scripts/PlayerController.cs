using Godot;

/// <summary>
/// First-person player: movement, mouse-look, crouch, bonk, carry, disguise,
/// interaction channels (terminal download / mopping), contextual prompts.
/// Port of game.ts player-facing logic. CharacterBody3D; eye height 1.6/1.0.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    public World WorldRef { get; set; } = null!;
    public GameMode Mode { get; set; } = null!;

    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public bool Crouching { get; set; }
    public bool HasMop { get; set; }
    public bool HasBlueprint { get; set; }
    public bool BlueprintSent { get; set; }
    public string? DisguiseOf { get; set; }
    public string? DepartmentDisguise { get; set; }
    public NpcBrain? Carrying { get; set; }
    public PropItem? HeldProp { get; set; }
    public HeldItem HeldItem { get; set; }
    private float _boostTimer;

    /// <summary>Fired when a mop channel completes on a splat (GameMode handles evidence cleanup).</summary>
    public event Action<BloodSystem.Splat>? StainMopped;

    public float BonkTimer { get; private set; }
    public float ChannelT { get; set; } = -1f;
    public ChannelMode ChannelMode { get; private set; } = ChannelMode.None;
    public BloodSystem.Splat? ChannelSplat { get; set; }
    public string Prompt { get; private set; } = "";

    /// <summary>-1 when idle; else 0..1 completion of the active channel.</summary>
    public float ChannelProgressFraction =>
        ChannelT < 0 ? -1f : System.MathF.Min(1f, ChannelT / ChannelDuration());

    private float ChannelDuration() => ChannelMode switch
    {
        ChannelMode.Terminal => Bal.ChannelTime,
        ChannelMode.Mop => Bal.MopTime,
        ChannelMode.Coffee => 2f,
        ChannelMode.Microwave => 3f,
        ChannelMode.Tape => 4f,
        _ => 1f,
    };

    private Camera3D _camera = null!;
    private Vector3 _lastDripPos;
    private Node3D? _carriedBundle;
    private Node3D? _mopViewmodel;
    private float _photoCooldown;

    public Vector3 FeetPos => new(GlobalPosition.X, 0f, GlobalPosition.Z);

    /// <summary>Camera forward on the floor plane (port of forwardVec(); cameras look down -Z).</summary>
    public Vector3 ForwardFlat() => new(-System.MathF.Sin(Yaw), 0f, -System.MathF.Cos(Yaw));

    public override void _Ready()
    {
        var shape = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Bal.PlayerRadius, Height = 1.8f },
            Position = new Vector3(0f, 0.9f, 0f),
        };
        AddChild(shape);

        _camera = new Camera3D
        {
            Fov = 72f,
            Near = 0.1f,
            Far = 120f,
            Current = true,
        };
        AddChild(_camera);
        UpdateEye();
    }

    private float EyeHeight => Crouching ? 1.0f : 1.6f;

    private void UpdateEye() => _camera.Position = new Vector3(0f, EyeHeight, 0f);

    public override void _UnhandledInput(InputEvent e)
    {
        if (Mode.Over || !Mode.Started || Mode.UIOpen) return;
        bool mouseCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;

        if (e is InputEventMouseMotion mm && mouseCaptured)
        {
            Yaw -= mm.Relative.X * 0.0023f;
            Pitch = Util.Clamp(Pitch - mm.Relative.Y * 0.0023f, -1.4f, 1.4f);
            _camera.Rotation = new Vector3(Pitch, 0f, 0f);
        }

        if (!mouseCaptured) return;

        if (e.IsActionPressed("crouch")) Crouching = !Crouching;
        if (e.IsActionPressed("bonk")) TryBonk();
        if (e.IsActionPressed("carry")) ToggleCarry();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Mode.Started || Mode.Over) return;
        float dt = (float)delta;

        BonkTimer = System.MathF.Max(0f, BonkTimer - dt);
        _photoCooldown = System.MathF.Max(0f, _photoCooldown - dt);
        if (_boostTimer > 0) _boostTimer -= dt;
        Rotation = new Vector3(0f, Yaw, 0f);
        UpdateEye();

        if (Mode.UIOpen)
        {
            Velocity = Vector3.Zero;
            return;
        }

        if (Input.IsActionJustPressed("talk"))
            Mode.TryStartTalk();

        // ---- movement (port of updatePlayer) ----
        var fwd = ForwardFlat();
        var right = new Vector3(-fwd.Z, 0f, fwd.X);
        var move = new Vector3();
        if (Input.IsKeyPressed(Key.W)) move += fwd;
        if (Input.IsKeyPressed(Key.S)) move -= fwd;
        if (Input.IsKeyPressed(Key.A)) move -= right;
        if (Input.IsKeyPressed(Key.D)) move += right;
        if (move.LengthSquared() > 0f)
        {
            float speed = Carrying != null ? Bal.CarrySpeed : Crouching ? Bal.CrouchSpeed : Bal.WalkSpeed;
            if (_boostTimer > 0) speed *= 1.4f;
            Velocity = move.Normalized() * speed;
            MoveAndSlide();

            var pos = FeetPos;
            pos.X = Util.Clamp(pos.X, -31.4f, 31.4f);
            pos.Z = Util.Clamp(pos.Z, -21.4f, 21.4f);
            GlobalPosition = new Vector3(pos.X, GlobalPosition.Y, pos.Z);
        }
        else
        {
            Velocity = Vector3.Zero;
        }

        // ---- carrying a bleeding body leaves a trail ----
        if (Carrying != null && FeetPos.DistanceTo(_lastDripPos) > Bal.DripDistance)
        {
            _lastDripPos = FeetPos;
            Mode.Blood!.Spawn(FeetPos, 1, 0.45f);
        }

        // ---- interactions ----
        UpdateInteractions(dt);
    }

    // ================= actions =================

    public void TryBonk()
    {
        if (BonkTimer > 0 || Carrying != null || ChannelT >= 0) return;
        var fwd = ForwardFlat();
        NpcBrain? best = null;
        float bestDist = Bal.BonkRange;
        foreach (var n in Mode.Npcs)
        {
            if (!n.Awake) continue;
            var dvec = n.Pos - FeetPos;
            float d = dvec.Length();
            if (d > bestDist) continue;
            var dir = new Vector3(dvec.X, 0f, dvec.Z).Normalized();
            if (dir.Dot(fwd) < 0.35f) continue;
            best = n;
            bestDist = d;
        }
        if (best == null) return;
        BonkTimer = Bal.BonkCooldown;
        var flopDir = new Vector3(best.Pos.X - FeetPos.X, 0f, best.Pos.Z - FeetPos.Z).Normalized();
        Mode.OnBonkLanded(best, flopDir);
    }

    public void ToggleCarry()
    {
        if (ChannelT >= 0) return;

        if (Carrying != null)
        {
            // drop the body — it flops again, because physics has a sense of humour
            var n = Carrying;
            var fwd = ForwardFlat();
            var dropPos = new Vector3(FeetPos.X + fwd.X * 1.1f, 0f, FeetPos.Z + fwd.Z * 1.1f);
            WorldRef.ResolveCircle(ref dropPos, 0.3f);
            n.Body.Position = dropPos;
            n.Body.SetVisibleRec(true);
            RagdollFactory.Spawn(n.Body, fwd * 0.6f, null); // re-flop; factory handles fallback internally
            Mode.Blood!.Spawn(dropPos, 1, 0.7f);
            Carrying = null;
            FreeBundle();
            SynthPickup();
            Mode.Toast($"You set {n.NpcName} down. Gently-ish. They're leaking a bit.", ToastKind.Info);
            return;
        }

        // pick up nearest body
        NpcBrain? best = null;
        float bestDist = Bal.InteractRange;
        foreach (var n in Mode.Npcs)
        {
            if (n.State != NpcState.Out || !n.Body.Visible) continue;
            float d = n.Pos.DistanceTo(FeetPos);
            if (d < bestDist)
            {
                best = n;
                bestDist = d;
            }
        }
        if (best == null) return;

        Carrying = best;
        best.ClearRagdoll();
        best.Body.SetVisibleRec(false);
        _lastDripPos = FeetPos;

        // carried-body visual (a very dignified bundle), parented to the camera
        FreeBundle();
        _carriedBundle = new Node3D { Position = new Vector3(0.35f, -0.55f, -1.0f), Rotation = new Vector3(0f, 0f, 0.2f) };
        var torso = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.5f },
            Rotation = new Vector3(0f, 0f, System.MathF.PI / 2f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = best.Spec.Tint, Roughness = 0.9f },
        };
        _carriedBundle.AddChild(torso);
        var head = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f },
            Position = new Vector3(0.65f, 0.05f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("e8b98a") },
        };
        _carriedBundle.AddChild(head);
        _camera.AddChild(_carriedBundle);

        SynthPickup();
        Mode.Toast($"You are now carrying {best.NpcName}. Act natural. Mind the drips.", ToastKind.Warn);
    }

    private void FreeBundle()
    {
        _carriedBundle?.QueueFree();
        _carriedBundle = null;
    }

    private void SynthPickup() => Mode.Synth?.Pickup();

    public void BlowDisguise()
    {
        if (DisguiseOf == null) return;
        Mode.Toast($"Your {DisguiseOf} disguise is BLOWN. You are you again.", ToastKind.Warn);
        DisguiseOf = null;
    }

    // ================= interactions =================

    private bool _eHeld;

    private void UpdateInteractions(double dt)
    {
        // channel progress (hold E at terminal / mop)
        if (ChannelT >= 0)
        {
            Vector3? targetPos = ChannelMode switch
            {
                ChannelMode.Terminal => WorldRef.TerminalPos,
                ChannelMode.Mop => ChannelSplat?.Pos,
                ChannelMode.Coffee => new Vector3(25f, 0f, -20.4f),
                ChannelMode.Microwave => new Vector3(26.5f, 0f, -20.5f),
                ChannelMode.Tape => new Vector3(-2f, 0f, 19f),
                _ => null,
            };
            bool near = targetPos.HasValue && FeetPos.DistanceTo(targetPos.Value) < Bal.InteractRange + 0.3f;
            if (!Input.IsActionPressed("interact") || !near)
            {
                ChannelT = -1;
                ChannelSplat = null;
                Mode.Toast(ChannelMode == ChannelMode.Terminal
                    ? "Download aborted. The progress bar judged you."
                    : ChannelMode == ChannelMode.Mop
                        ? "Mopping abandoned. The stain remains. Judgy stain."
                        : "Task abandoned. Suspicious.", ToastKind.Info);
            }
            else
            {
                float duration = ChannelDuration();
                ChannelT += (float)dt;
                Prompt = ChannelMode switch
                {
                    ChannelMode.Terminal => $"Stealing blueprints… {System.Math.Min(100, (int)(ChannelT / duration * 100))}%",
                    ChannelMode.Mop => $"Mopping up evidence… {System.Math.Min(100, (int)(ChannelT / duration * 100))}%",
                    ChannelMode.Coffee => $"Brewing a fresh pot… {System.Math.Min(100, (int)(ChannelT / duration * 100))}%",
                    _ => $"Heating up fish… {System.Math.Min(100, (int)(ChannelT / duration * 100))}%",
                };
                if (ChannelT >= duration)
                {
                    ChannelT = -1;
                    switch (ChannelMode)
                    {
                        case ChannelMode.Terminal:
                            HasBlueprint = true;
                            Mode.Synth?.Success();
                            Mode.Toast("BLUEPRINTS ACQUIRED. Now mail them out via the mail trolley.", ToastKind.Success);
                            break;
                        case ChannelMode.Mop:
                            if (ChannelSplat != null)
                            {
                                StainMopped?.Invoke(ChannelSplat);
                                ChannelSplat = null;
                            }
                            break;
                        case ChannelMode.Coffee:
                            Mode.BrewCoffee();
                            break;
                        case ChannelMode.Microwave:
                            Mode.HeatFish();
                            break;
                        case ChannelMode.Tape:
                            Mode.DeleteTapes();
                            break;
                    }
                }
            }
            return; // hands busy
        }

        bool wantInteract = Input.IsActionPressed("interact");
        if (wantInteract && !_eHeld)
        {
            _eHeld = true;
            HandleInteractPress();
        }
        else if (!wantInteract)
        {
            _eHeld = false;
        }

        Prompt = ComputePrompt();
    }

    private void HandleInteractPress()
    {
        // 0. throw a held prop
        if (HeldProp != null)
        {
            if (ChannelT >= 0) return;
            ThrowProp();
            return;
        }
        // 1. carrying a body near a hide spot -> hide it
        if (Carrying != null)
        {
            var spot = WorldRef.NearestHideSpot(FeetPos, Bal.InteractRange + 0.4f);
            if (spot != null)
            {
                var victim = Carrying;
                Carrying = null;
                FreeBundle();
                Mode.HideBody(victim, spot);
            }
            return;
        }
        // 2. deliver blueprint at trolley
        if (HasBlueprint && FeetPos.DistanceTo(WorldRef.TrolleyPos) < Bal.InteractRange + 0.6f)
        {
            HasBlueprint = false;
            BlueprintSent = true;
            Mode.Synth?.Success();
            Mode.Toast("Blueprints mailed to \"definitely not a rival company\".", ToastKind.Success);
            Mode.EndGame(true, "Blueprint delivered. OmniCore never stood a chance.");
            return;
        }
        // 3. terminal
        if (!HasBlueprint && !BlueprintSent && FeetPos.DistanceTo(WorldRef.TerminalPos) < Bal.InteractRange)
        {
            ChannelMode = ChannelMode.Terminal;
            ChannelT = 0;
            Mode.Toast("Downloading blueprints… hold E. Try to look busy.", ToastKind.Info);
            return;
        }
        // 4. mop blood (requires mop)
        if (HasMop)
        {
            var splat = Mode.Blood!.NearestTo(FeetPos, Bal.InteractRange);
            if (splat != null)
            {
                ChannelMode = ChannelMode.Mop;
                ChannelSplat = splat;
                ChannelT = 0;
                Mode.Toast("Scrubbing… hold E. Hum something casual.", ToastKind.Info);
                return;
            }
        }
        // 4.5 job-sim props: vending / coffee / microwave / fire alarm
        var use = Interactables.Find(FeetPos, HeldItem, Mode);
        if (use != null)
        {
            switch (use.Id)
            {
                case "vending":
                    if (Mode.VendingCooldown > 0) return;
                    if (!Mode.VendingLaxativeTaken)
                    {
                        HeldItem = HeldItem.Laxative;
                        Mode.VendingLaxativeTaken = true;
                    }
                    else
                    {
                        HeldItem = HeldItem.EnergyDrink;
                        Mode.VendingEnergyTaken = true;
                        _boostTimer = 8f;
                    }
                    Mode.VendingCooldown = 30f;
                    SynthPickup();
                    Mode.Toast(HeldItem == HeldItem.Laxative
                        ? "Laxative sachet acquired. For coffee. Obviously."
                        : "Energy drink down the hatch. Legs get fast now.", ToastKind.Success);
                    return;
                case "coffeemaker":
                    if (HeldItem == HeldItem.Laxative)
                    {
                        HeldItem = HeldItem.None;
                        Mode.SpikeCoffee();
                    }
                    else
                    {
                        ChannelMode = ChannelMode.Coffee;
                        ChannelT = 0;
                        Mode.Toast("Brewing… hold E. The scent will do the rest.", ToastKind.Info);
                    }
                    return;
                case "microwave":
                    ChannelMode = ChannelMode.Microwave;
                    ChannelT = 0;
                    Mode.Toast("You found someone's fish in the fridge. Hold E. Commit the audacity.", ToastKind.Warn);
                    return;
                case "firealarm":
                    Mode.PullFireAlarm();
                    return;
                case "locker":
                    Mode.CycleUniform();
                    return;
                case "tapes":
                    ChannelMode = ChannelMode.Tape;
                    ChannelT = 0;
                    Mode.Toast("Shredding tapes… hold E. Feel the power.", ToastKind.Warn);
                    return;
            }
        }
        // 4.7 grab a desk prop
        if (Carrying == null)
        {
            var prop = NearestProp();
            if (prop != null)
            {
                HeldProp = prop;
                prop.Freeze = true;
                prop.GetParent().RemoveChild(prop);
                _camera.AddChild(prop);
                prop.Position = new Vector3(0.35f, -0.35f, -0.8f);
                prop.Rotation = Vector3.Zero;
                SynthPickup();
                Mode.Toast($"{Cap(prop.ItemType)} acquired. It makes a great argument.", ToastKind.Info);
                return;
            }
        }
        // 4.8 pod-desk computer -> OmniPortal
        foreach (var m in WorldData.MonitorPositions())
        {
            if (FeetPos.DistanceTo(m) < 1.7f)
            {
                Mode.OpenPortal();
                return;
            }
        }
        // 5. steal clothes from a body
        var body = NearestBody();
        if (body != null && !body.Looted)
        {
            body.Looted = true;
            DisguiseOf = body.NpcName;
            Mode.Stats.Disguises++;
            SynthPickup();
            Mode.Toast($"You are now \"definitely {body.NpcName}\". Shirt's a bit tight.", ToastKind.Success);
            return;
        }
        // 6. grab the mop from the supply closet
        HideSpotState? closet = FindSpot("closet");
        if (!HasMop && closet != null && FeetPos.DistanceTo(closet.Pos) < Bal.InteractRange + 0.5f)
        {
            EquipMop();
            return;
        }
        // 7. photocopy your face (distraction)
        HideSpotState? printer = FindSpot("printer");
        if (_photoCooldown <= 0 && printer != null && FeetPos.DistanceTo(printer.Pos) < Bal.InteractRange + 0.5f)
        {
            PhotocopyFace();
            return;
        }
    }

    private void ThrowProp()
    {
        if (HeldProp == null || _camera == null) return;
        var prop = HeldProp;
        HeldProp = null;
        _camera.RemoveChild(prop);
        Mode.AddChild(prop);
        var fwd = ForwardFlat();
        prop.GlobalPosition = new Vector3(GlobalPosition.X, EyeHeight, GlobalPosition.Z) + fwd * 0.6f;
        prop.Freeze = false;
        prop.Arm();
        prop.LinearVelocity = fwd * 7f + Vector3.Up * 2.5f;
        SynthPickup();
        Mode.Toast($"The {prop.ItemType} becomes someone else's problem.", ToastKind.Info);
    }

    private PropItem? NearestProp()
    {
        PropItem? best = null;
        float bestDist = 1.6f;
        foreach (var item in Mode.Items)
        {
            if (!IsInstanceValid(item) || item.Freeze) continue;
            float d = item.GlobalPosition.DistanceTo(new Vector3(GlobalPosition.X, item.GlobalPosition.Y, GlobalPosition.Z));
            if (d < bestDist)
            {
                best = item;
                bestDist = d;
            }
        }
        return best;
    }

    private static string Cap(string s) => System.Char.ToUpperInvariant(s[0]) + s[1..];

    private HideSpotState? FindSpot(string id)
    {
        foreach (var s in WorldRef.HideSpots) if (s.Id == id) return s;
        return null;
    }

    private void EquipMop()
    {
        HasMop = true;
        FreeMop();
        _mopViewmodel = new Node3D { Position = new Vector3(-0.42f, -0.35f, -0.8f) };
        var handle = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.7f },
            Rotation = new Vector3(0f, 0f, 0.5f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("8a6f4f") },
        };
        _mopViewmodel.AddChild(handle);
        var headM = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f },
            Position = new Vector3(-0.28f, -0.32f, 0f),
            Scale = new Vector3(1f, 0.6f, 1f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("d8d8d0"), Roughness = 1f },
        };
        _mopViewmodel.AddChild(headM);
        _camera.AddChild(_mopViewmodel);
        SynthPickup();
        Mode.Toast("Mop acquired. You have never looked more employable.", ToastKind.Success);
    }

    private void FreeMop()
    {
        _mopViewmodel?.QueueFree();
        _mopViewmodel = null;
    }

    /// <summary>The floor stops working to laugh at it (port of photocopyFace).</summary>
    private void PhotocopyFace()
    {
        _photoCooldown = Bal.PhotoCooldown;
        SynthPickup();
        int count = 0;
        foreach (var n in Mode.Npcs)
        {
            if (!n.Awake || n.State == NpcState.Report || n.State == NpcState.Seated) continue;
            if (n.Pos.DistanceTo(FeetPos) < Bal.PhotoDistractRadius)
            {
                n.DistractTimer = Bal.PhotoDistractSeconds;
                n.Body.ShowEmote(":D");
                count++;
            }
        }
        Mode.Toast(count > 0
            ? $"You photocopy your face. 50 copies. {count} coworker{(count > 1 ? "s" : "")} completely lose{(count > 1 ? "" : "s")} it."
            : "You photocopy your face. 50 copies. No one is around to appreciate it. Tragic.",
            ToastKind.Chaos);
    }

    private string ComputePrompt()
    {
        if (Carrying != null)
        {
            var spot = WorldRef.NearestHideSpot(FeetPos, Bal.InteractRange + 0.4f);
            if (spot != null)
                return $"E — Hide {Carrying.NpcName} in the {spot.Name} ({spot.Occupants.Count}/{spot.Capacity})";
            return $"Q — Drop {Carrying.NpcName} · Find somewhere… creative";
        }
        if (HasBlueprint && BlueprintSent == false && FeetPos.DistanceTo(WorldRef.TrolleyPos) < Bal.InteractRange + 0.6f)
            return "E — Mail the blueprints to your employers";
        if (!HasBlueprint && !BlueprintSent && FeetPos.DistanceTo(WorldRef.TerminalPos) < Bal.InteractRange)
            return "Hold E — Steal the blueprints";
        if (HasMop && Mode.Blood!.NearestTo(FeetPos, Bal.InteractRange) != null)
            return "Hold E — Mop up the evidence";
        var use = Interactables.Find(FeetPos, HeldItem, Mode);
        if (use != null)
            return use.Prompt;
        if (HeldProp != null)
            return $"E — Throw {HeldProp.ItemType}";
        var prop = NearestProp();
        if (prop != null)
            return $"E — Grab {prop.ItemType}";
        foreach (var m in WorldData.MonitorPositions())
        {
            if (FeetPos.DistanceTo(m) < 1.7f)
                return "E — Use computer (OmniPortal)";
        }
        var body = NearestBody();
        if (body != null)
            return body.Looted ? $"Q — Pick up {body.NpcName}" : $"Q — Pick up {body.NpcName} · E — \"Borrow\" their clothes";
        var closet = FindSpot("closet");
        if (!HasMop && closet != null && FeetPos.DistanceTo(closet.Pos) < Bal.InteractRange + 0.5f)
            return "E — Grab the mop";
        var printer = FindSpot("printer");
        if (printer != null && FeetPos.DistanceTo(printer.Pos) < Bal.InteractRange + 0.5f)
            return _photoCooldown > 0 ? $"Printer is cooling down… ({System.MathF.Ceiling(_photoCooldown)}s)" : "E — Photocopy your face. 50 copies.";
        return "";
    }

    private NpcBrain? NearestBody()
    {
        NpcBrain? best = null;
        float bestDist = Bal.InteractRange;
        foreach (var n in Mode.Npcs)
        {
            if (n.State != NpcState.Out || !n.Body.Visible) continue;
            float d = n.Pos.DistanceTo(FeetPos);
            if (d < bestDist)
            {
                best = n;
                bestDist = d;
            }
        }
        return best;
    }

    /// <summary>How suspicious the player currently looks: 0 = model employee.</summary>
    public float PlayerSusActivity()
    {
        float sus = 0;
        var room = WorldRef.RoomAt(FeetPos.X, FeetPos.Z);
        bool itBadge = DepartmentDisguise == "IT";
        if (room == RoomId.Server && DisguiseOf == null && !itBadge) sus = System.MathF.Max(sus, 1f);
        if (ChannelT >= 0 && ChannelMode == ChannelMode.Terminal) sus = System.MathF.Max(sus, 2.5f);
        if (ChannelT >= 0 && ChannelMode == ChannelMode.Mop)
            sus = System.MathF.Max(sus, DepartmentDisguise == "Facilities" ? 0f : 1.5f);
        if (Carrying != null) sus = System.MathF.Max(sus, 3f);
        return sus;
    }
}


