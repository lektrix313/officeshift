using Godot;
using System.Collections.Generic;

/// <summary>
/// Root orchestrator attached to scenes/main.tscn. Owns the shift loop,
/// stats, win/lose, chatter, and wiring between World / Player / AI / Blood /
/// Audio / HUD. Port of game.ts Game.update(dt) frame flow:
///   updatePlayer -> updateNpcs -> updateGuard -> updateInteractions
///   -> updateChatter -> ragdoll settle pools -> carry drips (player-side).
/// </summary>
public partial class GameMode : Node3D
{
    public static GameMode? Instance { get; private set; }

    public World? WorldRef { get; private set; }
    public PlayerController? Player { get; private set; }
    public BloodSystem? Blood { get; private set; }
    public BlipSynth? Synth { get; private set; }
    public Hud? Hud { get; private set; }
    public List<NpcBrain> Npcs { get; } = new();
    public List<PropItem> Items { get; } = new();
    public NpcBrain? Guard { get; private set; }

    public bool Started;
    public bool Over;
    public bool Won;
    public string EndReason = "";
    public double TimeLeft = Bal.ShiftSeconds;
    public double AlertTimer;
    public bool BeingWatched;
    public float MaxSuspicionValue;
    public bool UIOpen;
    public bool StinkActive => WorldEvents.StinkActive;
    public bool VendingLaxativeTaken;
    public bool VendingEnergyTaken;
    public float VendingCooldown;
    public float AlarmCooldown;
    public float StinkTimer;
    public float EvacTimer;
    public float CaseEvidence;
    public bool CaseActive;
    public bool TapeShredded;
    private float _cameraCrimeUntil;
    private bool _onCameraToasted;
    private float _shiftElapsed;
    private readonly StatsTracker _stats = new();
    public StatsTracker Stats => _stats;

    public OmniPortal? Portal { get; private set; }
    public TalkOverlay? Talk { get; private set; }

    private float _chatterTimer = 9f;
    private float _hudTimer;

    /// <summary>Bodies awaiting their post-settle blood pool (port of poolSpawned logic).</summary>
    private readonly Dictionary<NpcBrain, float> _poolTimers = new();

    private readonly List<NoiseRef> _noises = new();
    private readonly List<float> _noiseBirth = new();
    private Vector3 _lastNoisePos;
    private float _lastNoiseAt = -999f;

    public sealed class StatsTracker
    {
        public int Bonks, Hides, Reports, Disguises, Cleans;
    }

    public override void _Ready()
    {
        Instance = this;

        // ---- environment / lighting (port of init()) ----
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Color.FromHtml("0e1420"),
            FogEnabled = true,
            FogLightColor = Color.FromHtml("0e1420"),
            FogDensity = 0.012f,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("cfd8ff"),
            AmbientLightEnergy = 0.55f,
        };
        AddChild(new WorldEnvironment { Environment = env });

        var sun = new DirectionalLight3D
        {
            LightColor = Color.FromHtml("fff2dd"),
            LightEnergy = 0.9f,
            ShadowEnabled = true,
        };
        sun.RotationDegrees = new Vector3(-52, 32, 0);
        AddChild(sun);

        // ---- world scene ----
        var worldScene = GD.Load<PackedScene>("res://scenes/world.tscn");
        WorldRef = worldScene?.Instantiate() as World;
        if (WorldRef == null)
        {
            GD.PushError("[GameMode] world.tscn missing or lacks World script — run BuildScenes ++ world");
            WorldRef = new World(); // degraded-but-running fallback
        }
        AddChild(WorldRef);

        // ---- systems ----
        Blood = new BloodSystem { Name = "Blood" };
        AddChild(Blood);
        Synth = new BlipSynth { Name = "Synth" };
        AddChild(Synth);
        Hud = new Hud { Name = "Hud" };
        AddChild(Hud);
        Hud.Toast("Welcome to OmniCore Industries. Try to look normal.", ToastKind.Info);

        // ---- player ----
        Player = new PlayerController { Name = "Player" };
        Player.WorldRef = WorldRef;
        Player.Mode = this;
        AddChild(Player);
        Player.Position = new Vector3(0f, 0f, 18.5f);
        Player.Yaw = 0f; // facing -Z into the cubicle farm

        // ---- coworkers ----
        foreach (var def in Roster.Coworkers)
        {
            var body = new NpcBody();
            body.Init(def.Name, def.Arch);
            AddChild(body);
            body.Position = new Vector3(def.X, 0f, def.Z);
            Npcs.Add(NpcBrain.Create(body, def.Zone));
        }

        // the slob slumps at his desk
        foreach (var n in Npcs)
        {
            if (n.Arch == Archetype.Slob)
            {
                n.Body.Position = WorldData.SlobDeskPos;
                n.State = NpcState.Seated;
                n.Body.ShowSleeping(true);
            }
        }

        // security
        var gBody = new NpcBody();
        gBody.Init(Roster.GuardName, Archetype.Guard);
        AddChild(gBody);
        gBody.Position = WorldData.GuardPosts[0];
        Guard = NpcBrain.Create(gBody, "guard");
        Guard.PauseTimer = 2f;
        Npcs.Add(Guard);

        // wire player FX events
        Player.StainMopped += OnStainMopped;

        CameraSystem.CreateNodes(this);

        // desk props
        foreach (var def in WorldData.PropItems)
        {
            var item = PropItem.Create(def.Type, new Vector3(def.X, def.Y, def.Z));
            AddChild(item);
            Items.Add(item);
        }

        Portal = new OmniPortal { Name = "Portal" };
        AddChild(Portal);
        Talk = new TalkOverlay { Name = "Talk" };
        AddChild(Talk);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Started && e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && !Over)
        {
            Started = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (Started && !Over && e.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible; // pause overlay takes over
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)System.Math.Min(delta, 0.05);

        if (Over)
        {
            if (Input.IsActionJustPressed("restart")) GetTree().ReloadCurrentScene();
            return;
        }

        if (!Started || Player == null || Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            PushHud(); // start/pause screens stay responsive
            return;
        }

        // ---- shift clock ----
        TimeLeft -= dt;
        if (TimeLeft <= 0)
        {
            EndGame(false, "The courier left without your package. Shift wasted. You're fired — from a job you never even had.");
            return;
        }
        if (AlertTimer > 0) AlertTimer -= dt;

        _shiftElapsed += dt;
        VendingCooldown = System.MathF.Max(0f, VendingCooldown - dt);
        AlarmCooldown = System.MathF.Max(0f, AlarmCooldown - dt);
        if (StinkTimer > 0)
        {
            StinkTimer -= dt;
            if (StinkTimer <= 0)
            {
                WorldEvents.StinkActive = false;
                foreach (var n in Npcs) n.StinkReacted = false;
                Toast("The fish smell finally dies. The office breathes again.", ToastKind.Info);
            }
        }
        if (EvacTimer > 0)
        {
            EvacTimer -= dt;
            if (EvacTimer <= 0)
            {
                WorldEvents.Evacuating = false;
                Toast("All clear. Everyone shuffles back to their desks, betrayed.", ToastKind.Info);
            }
        }
        UIOpen = (Portal != null && Portal.IsOpen) || (Talk != null && Talk.IsOpen);

        // ---- surveillance + HR case ----
        if (_shiftElapsed < _cameraCrimeUntil && Player != null &&
            CameraSystem.IsSeen(Player.FeetPos, WorldRef!))
        {
            CaseEvidence = System.MathF.Min(100f, CaseEvidence + 14f * dt);
            if (!_onCameraToasted)
            {
                _onCameraToasted = true;
                Toast("CAMERA: You are being recorded. Smile for the file.", ToastKind.Warn);
            }
        }
        if (!CaseActive && CaseEvidence >= 35f)
        {
            CaseActive = true;
            Toast("HR CASE OPENED. Evidence is mounting. Shred the tapes or bleed points.", ToastKind.Chaos);
            Synth?.Alarm();
        }
        if (TapeShredded && CaseEvidence > 0f)
        {
            CaseEvidence = System.MathF.Max(0f, CaseEvidence - 12f * dt);
            if (CaseEvidence <= 0f)
            {
                TapeShredded = false;
                CaseActive = false;
                _onCameraToasted = false;
                Toast("HR CASE CLOSED. The truth died in the shredder. Somehow.", ToastKind.Success);
            }
        }
        if (CaseEvidence >= 100f)
        {
            EndGame(false, "The HR hearing lasts four minutes. The spreadsheet is entered into evidence. You are escorted out with a box of desk plants.");
            return;
        }

        // ---- AI tick ----
        var ctx = new AiContext
        {
            PlayerPos = Player.FeetPos,
            PlayerVisibleToAi = true,
            PlayerCrouching = Player.Crouching,
            PlayerCarrying = Player.Carrying != null,
            PlayerDisguise = Player.DisguiseOf,
            PlayerDept = Player.DepartmentDisguise,
            PlayerActivity = Player.PlayerSusActivity(),
            Guard = Guard,
            Npcs = Npcs,
            WorldRef = WorldRef!,
            Blood = Blood!,
            AlertTimer = AlertTimer,
            Evacuating = WorldEvents.Evacuating,
            EvacPoint = WorldEvents.EvacPoint,
            BathroomPoint = new Vector3(-26.5f, 0f, -4f),
            CoffeePoint = new Vector3(25f, 0f, -19.2f),
            CoffeeSpiked = WorldEvents.CoffeeSpiked && WorldEvents.SpikeUsesLeft > 0,
            StinkActive = WorldEvents.StinkActive,
            StinkPos = WorldEvents.StinkPos,
            NoiseFresh = _shiftElapsed - _lastNoiseAt < 8f,
            NoisePos = _lastNoisePos,
            Toast = Toast,
            AlarmSfx = () => Synth?.Alarm(),
            CanSee = CanSee,
            OnReportReachedGuard = OnReportReachedGuard,
            OnPlayerCaught = () => EndGame(false, "Officer Briggs caught you red-handed. HR would like a word. Several words. In a basement."),
        };
        AiDirector.Tick(Npcs, ctx, dt);
        if (Guard != null) AiDirector.GuardTick(Guard, ctx, dt);
        MaxSuspicionValue = AiDirector.Outputs.MaxSus;
        BeingWatched = AiDirector.Outputs.Watched;
        AlertTimer = ctx.AlertTimer; // guard logic may extend/consume it

        // ---- ambient chatter ----
        _chatterTimer -= dt;
        if (_chatterTimer <= 0)
        {
            _chatterTimer = 11f + (float)GD.RandRange(0.0, 7.0);
            var candidates = new List<NpcBrain>();
            foreach (var n in Npcs) if (n.Awake && n != Guard) candidates.Add(n);
            if (candidates.Count > 0)
            {
                var who = candidates[(int)(GD.RandRange(0, candidates.Count - 1))];
                var line = Flavor.AmbientLines[(int)(GD.RandRange(0, Flavor.AmbientLines.Length - 1))];
                Toast(line.Replace("{name}", who.NpcName), ToastKind.Info);
            }
        }

        // ---- noise evidence expiry (20s): investigators shrug via gone-check ----
        for (int i = _noises.Count - 1; i >= 0; i--)
        {
            if (_shiftElapsed - _noiseBirth[i] > 20f)
            {
                _noises[i].Expired = true;
                _noises.RemoveAt(i);
                _noiseBirth.RemoveAt(i);
            }
        }

        // ---- live "player" directive follow + persona chat results ----
        foreach (var n in Npcs)
        {
            if (n.DirectiveZone == "player" && n.DirectiveTimer > 0)
                n.DirectiveTarget = Player.FeetPos;
        }
        while (NpcChatService.Results.TryDequeue(out var item))
            OnChatResult(item.Brain, item.Result, item.Via);

        // ---- blood pools after ragdoll settles (port of poolSpawned) ----
        foreach (var n in Npcs)
        {
            if (n.State == NpcState.Out && !n.PoolSpawned && !_poolTimers.ContainsKey(n))
                _poolTimers[n] = Bal.RagdollSettleSeconds;
        }
        var expiredPools = new List<NpcBrain>();
        foreach (var kv in _poolTimers)
        {
            float t = kv.Value - dt;
            if (t <= 0) expiredPools.Add(kv.Key);
            else _poolTimers[kv.Key] = t;
        }
        foreach (var n in expiredPools)
        {
            _poolTimers.Remove(n);
            if (n.State == NpcState.Out)
            {
                n.PoolSpawned = true;
                Blood!.Spawn(n.Pos, 3, 1.5f);
            }
        }

        // ---- HUD push at ~8 Hz ----
        _hudTimer -= dt;
        if (_hudTimer <= 0)
        {
            _hudTimer = 0.12f;
            PushHud();
        }
    }

    // ================= services =================

    /// <summary>Port of canSee(). Vision cone + range + crouch factor + LOS.</summary>
    public bool CanSee(NpcBrain n, Vector3 target, float rangeMul = 1f)
    {
        if (!n.Awake || n.State == NpcState.Seated) return false;
        float range = n.Spec.Range * rangeMul * (Player != null && Player.Crouching ? Bal.CrouchVisionMul : 1f);
        float dx = target.X - n.Pos.X;
        float dz = target.Z - n.Pos.Z;
        float dist = System.MathF.Sqrt(dx * dx + dz * dz);
        if (dist > range) return false;
        float ang = System.MathF.Atan2(dx, dz);
        float diff = ang - n.Body.Facing;
        while (diff > System.MathF.PI) diff -= System.MathF.Tau;
        while (diff < -System.MathF.PI) diff += System.MathF.Tau;
        if (System.MathF.Abs(diff) > n.Spec.Fov) return false;
        return !WorldRef!.LosBlocked(n.Pos, target);
    }

    public void Toast(string msg, ToastKind kind = ToastKind.Info) => Hud?.Toast(msg, kind);

    public void EndGame(bool won, string reason)
    {
        if (Over) return;
        Over = true;
        Won = won;
        EndReason = reason;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (won) Synth?.Success();
        else Synth?.Alarm();
        PushHud();
    }

    /// <summary>Port of tryBonk() aftermath. Called by PlayerController on a landed swing.</summary>
    public void OnBonkLanded(NpcBrain victim, Vector3 flopDir)
    {
        if (victim.State == NpcState.Out || Player == null) return;
        bool wasAsleep = victim.State == NpcState.Seated;
        FlashCrime();
        victim.KnockOut(flopDir);
        Blood!.Spawn(victim.Pos, 4, 1.1f);
        Stats.Bonks++;
        Synth?.Bonk();
        Toast(wasAsleep
            ? $"{victim.NpcName} was already asleep. You just made it official. And messy."
            : $"You bonked {victim.NpcName} with a keyboard. There is… some blood. Mop's in the supply closet.",
            ToastKind.Chaos);

        foreach (var w in Npcs)
        {
            if (w == victim || !w.Awake) continue;
            float dist = w.Pos.DistanceTo(Player.FeetPos);
            bool seen = CanSee(w, Player.FeetPos) || dist < Bal.WitnessAutoSeeDist;
            if (!seen) continue;
            if (w.State == NpcState.Seated && dist < 5f)
            {
                w.State = NpcState.Routine; // the noise wakes the slob
                w.Body.ShowSleeping(false);
                w.AddSuspicion(Bal.WitnessWakeSus);
                Toast($"The commotion woke {w.NpcName} up — and they saw EVERYTHING.", ToastKind.Warn);
            }
            else
            {
                w.AddSuspicion(Bal.WitnessSus);
                Toast($"{w.NpcName} saw that. {w.NpcName} is reconsidering your friendship.", ToastKind.Warn);
            }
            if (Player.DisguiseOf != null) Player.BlowDisguise();
        }
    }

    /// <summary>Port of hideBodyIn(): occupants, Hidden state, investigation shrug-offs, gag props.</summary>
    public void HideBody(NpcBrain victim, HideSpotState spot)
    {
        spot.Occupants.Add(victim.NpcName);
        victim.State = NpcState.Hidden;
        victim.Body.SetVisibleRec(false);
        victim.Body.ShowSleeping(false);
        Stats.Hides++;
        Synth?.Pickup();

        // investigators of this body lose the plot
        foreach (var n in Npcs)
        {
            if ((n.State == NpcState.Curious || n.State == NpcState.Panic) &&
                (ReferenceEquals(n.InvestigateRef, victim) || ReferenceEquals(n.PanicRef, victim)))
            {
                n.ShrugItOff(Bal.VanishPanicSus);
                Toast($"{n.NpcName} saw {victim.NpcName} vanish into the {spot.Name}. \"…Nope. Not paid enough.\"", ToastKind.Warn);
            }
        }
        Toast($"{victim.NpcName} was {spot.Action}. No one will ever look there.", ToastKind.Success);

        // comedy gag props (port of hideBodyIn visuals)
        switch (spot.Id)
        {
            case "lamp":
            {
                var pole = MakeBox(new Vector3(0.1f, 1.7f, 0.1f), Color.FromHtml("3a3f47"));
                pole.Position = new Vector3(spot.Pos.X, 0.85f, spot.Pos.Z);
                var shade = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.15f, Height = 0.7f },
                    Position = new Vector3(spot.Pos.X, 1.85f, spot.Pos.Z),
                    MaterialOverride = MakeMat(Color.FromHtml("f2e3b3")),
                };
                var tie = MakeBox(new Vector3(0.12f, 0.5f, 0.05f), victim.Spec.Tint);
                tie.Position = new Vector3(spot.Pos.X, 1.2f, spot.Pos.Z + 0.1f);
                AddChild(pole); AddChild(shade); AddChild(tie);
                break;
            }
            case "printer":
            {
                for (int i = 0; i < 2; i++)
                {
                    var shoe = MakeBox(new Vector3(0.16f, 0.14f, 0.45f), Color.FromHtml("2a2a2e"));
                    shoe.Position = new Vector3(spot.Pos.X + (i == 0 ? -0.2f : 0.2f), 0.07f, spot.Pos.Z + 0.9f);
                    AddChild(shoe);
                }
                break;
            }
            case "trolley":
            {
                var lump = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.55f, Height = 1.1f },
                    Scale = new Vector3(1f, 0.6f, 1.4f),
                    Position = new Vector3(spot.Pos.X, 1.15f, spot.Pos.Z),
                    MaterialOverride = MakeMat(Color.FromHtml("cbb791")),
                };
                AddChild(lump);
                break;
            }
        }
    }

    /// <summary>Mop completion handler (subscribed to PlayerController.StainMopped).</summary>
    private void OnStainMopped(BloodSystem.Splat splat)
    {
        if (!Blood!.Contains(splat)) return;
        Blood.Remove(splat);
        Stats.Cleans++;
        Synth?.Pickup();
        Toast("Blood mopped. Just a janitor doing janitor things. Nothing to see here.", ToastKind.Success);

        foreach (var n in Npcs)
        {
            if ((n.State == NpcState.Curious || n.State == NpcState.Panic) &&
                (ReferenceEquals(n.InvestigateRef, splat) || ReferenceEquals(n.PanicRef, splat)))
            {
                n.ShrugItOff(Bal.MoppedStainSus);
                Toast($"{n.NpcName} blinks. The stain is gone. \"I need more coffee.\"", ToastKind.Info);
            }
        }
    }

    /// <summary>Port of onReport(): witness reached security -> hunt begins.</summary>
    private void OnReportReachedGuard(NpcBrain reporter)
    {
        reporter.CalmDown();
        Stats.Reports++;
        CaseEvidence = System.MathF.Min(100f, CaseEvidence + 25f);
        if (!CaseActive && CaseEvidence >= 35f) { CaseActive = true; Toast("HR CASE OPENED — a witness went on record. Shred the tapes.", ToastKind.Chaos); }
        AlertTimer = Bal.GuardAlertOnReport;
        if (Guard != null && Player != null)
        {
            Guard.State = NpcState.Hunt;
            Guard.LastSeenPlayer = Player.FeetPos;
            Guard.LostSightTimer = 0f;
        }
        Toast("Officer Briggs has been informed. He is walking over with intent.", ToastKind.Warn);
        Synth?.Alarm();
    }

    /// <summary>Impact noise from thrown/dropped props: nearby NPCs come poke at it.</summary>
    public void OnNoise(Vector3 pos, float radius, string itemType, bool shattered)
    {
        var noiseRef = new NoiseRef { Pos = pos };
        _noises.Add(noiseRef);
        _noiseBirth.Add(_shiftElapsed);
        _lastNoisePos = pos;
        _lastNoiseAt = _shiftElapsed;
        Synth?.Bonk();
        Toast(shattered
            ? $"The {itemType} SHATTERS across the floor. Everyone heard that."
            : $"The {itemType} {PropItem.NoiseVerb(itemType)}. Everyone within earshot heard that.", ToastKind.Warn);

        foreach (var n in Npcs)
        {
            if (!n.Awake || n.Talking) continue;
            if (n.Pos.DistanceTo(pos) > radius) continue;
            n.StartCurious(pos, noiseRef, EvidenceKind.Noise);
        }
    }

    // ================= scenario events =================

    public void FlashCrime() => _cameraCrimeUntil = _shiftElapsed + 4f;

    public void DeleteTapes()
    {
        if (TapeShredded || CaseEvidence <= 0f)
        {
            Toast("No tapes worth shredding. Yet.", ToastKind.Info);
            return;
        }
        TapeShredded = true;
        Synth?.Pickup();
        Toast("Tapes shredded. The case is bleeding out. Stay clean.", ToastKind.Success);
    }

    private static readonly string[] UniformCycle = { "IT", "Facilities", "HR", "Sales" };

    public string NextUniformName()
    {
        var cur = Player?.DepartmentDisguise;
        int idx = cur == null ? -1 : System.Array.IndexOf(UniformCycle, cur);
        return UniformCycle[(idx + 1) % UniformCycle.Length];
    }

    public void CycleUniform()
    {
        if (Player == null) return;
        var next = NextUniformName();
        Player.DepartmentDisguise = Player.DepartmentDisguise == next ? null : next;
        Synth?.Pickup();
        Toast(next switch
        {
            "IT" => "IT uniform on. The server room is YOUR room now.",
            "Facilities" => "Facilities overalls on. Nobody sees the cleaner. Nobody ever sees the cleaner.",
            "HR" => "HR blazer on. People will tell you things. Voluntarily. Terrifying.",
            "Sales" => "Sales lanyard on. Nobody knows what Sales does — including Sales.",
            _ => "Uniform off. You are a nameless new hire again.",
        }, ToastKind.Success);
    }

    public void SpikeCoffee()
    {
        FlashCrime();
        WorldEvents.CoffeeSpiked = true;
        WorldEvents.SpikeUsesLeft = 3;
        Synth?.Pickup();
        Toast("Coffee spiked. Three cups until the office learns regret.", ToastKind.Chaos);
    }

    public void BrewCoffee()
    {
        Synth?.Pickup();
        NpcBrain? first = null, second = null;
        float d1 = float.MaxValue, d2 = float.MaxValue;
        foreach (var n in Npcs)
        {
            if (!n.Awake || n == Guard || n.Talking) continue;
            float d = n.Pos.DistanceTo(Player!.FeetPos);
            if (d < d1) { d2 = d1; second = first; d1 = d; first = n; }
            else if (d < d2) { d2 = d; second = n; }
        }
        int pulled = 0;
        if (first != null) { ApplyDirective(first, "coffeepoint", 12f); pulled++; }
        if (second != null) { ApplyDirective(second, "coffeepoint", 12f); pulled++; }
        Toast(pulled > 0
            ? $"Fresh coffee. The scent drags {first?.NpcName} and {second?.NpcName} away from their desks."
            : "Fresh coffee. Nobody noticed. Tragic.", ToastKind.Info);
    }

    public void HeatFish()
    {
        FlashCrime();
        WorldEvents.StinkActive = true;
        WorldEvents.StinkPos = new Vector3(22f, 0f, -15f);
        StinkTimer = 20f;
        Synth?.Alarm();
        Toast("FISH. MICROWAVED. The break room evacuates itself.", ToastKind.Chaos);
    }

    public void PullFireAlarm()
    {
        FlashCrime();
        WorldEvents.Evacuating = true;
        WorldEvents.EvacPoint = new Vector3(0f, 0f, 17f);
        EvacTimer = 12f;
        AlarmCooldown = 90f;
        Synth?.Alarm();
        Toast("FIRE ALARM. Everyone files to reception. You did this.", ToastKind.Chaos);
    }

    public void ApplyDirective(NpcBrain n, string zone, float seconds)
    {
        n.DirectiveZone = zone;
        n.DirectiveTimer = seconds;
        n.DirectiveTarget = ZonePoint(n, zone);
        n.PauseTimer = 0f;
    }

    public void OpenPortal()
    {
        if (UIOpen || Portal == null) return;
        Portal.Open();
        UIOpen = true;
    }

    public void TryStartTalk()
    {
        if (UIOpen || Talk == null || Player == null) return;
        NpcBrain? best = null;
        float bestDist = 2.6f;
        foreach (var n in Npcs)
        {
            if (!n.Awake) continue;
            float d = n.Pos.DistanceTo(Player.FeetPos);
            if (d < bestDist) { best = n; bestDist = d; }
        }
        if (best == null) return;
        Talk.Open(best);
        UIOpen = true;
    }

    public void CloseUI()
    {
        Portal?.Close();
        Talk?.Close();
        UIOpen = false;
        if (Started && !Over)
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnChatResult(NpcBrain n, NpcChatService.Result result, string via)
    {
        if (Talk != null && Talk.IsOpen && Talk.CurrentNpc == n)
            Talk.AppendNpcLine(result.Reply);
        Toast($"[{via}] {n.NpcName}: \"{Truncate(result.Reply, 90)}\"", ToastKind.Info);

        if (result.DirectiveZone != null && DirectiveZones.IsValid(result.DirectiveZone))
        {
            ApplyDirective(n, result.DirectiveZone, 20f);
            Toast($"{n.NpcName} is heading to the {result.DirectiveZone}. You asked nicely.", ToastKind.Success);
        }
    }

    private static string Truncate(string s, int len) => s.Length <= len ? s : s[..(len - 3)] + "...";

    private Vector3 ZonePoint(NpcBrain n, string zone) => zone switch
    {
        "breakroom" => new Vector3(20f, 0f, -15f),
        "server" => new Vector3(-22f, 0f, -17.5f),
        "printer" => new Vector3(-26.5f, 0f, -4f),
        "reception" => new Vector3(0f, 0f, 17f),
        "closet" => new Vector3(27f, 0f, 11f),
        "coffeepoint" => new Vector3(25f, 0f, -19.2f),
        "desk" => n.HomePos,
        "player" => Player != null ? Player.FeetPos : n.HomePos,
        _ => n.HomePos,
    };

    // ================= HUD =================

    private void PushHud()
    {
        if (Hud == null) return;
        var s = new HudSnapshot
        {
            Started = Started,
            Paused = Started && !Over && !UIOpen && Input.MouseMode != Input.MouseModeEnum.Captured,
            Over = Over,
            Won = Won,
            EndReason = EndReason,
            Prompt = Player?.Prompt ?? "",
            ChannelProgress = Player == null ? -1f : Player.ChannelProgressFraction,
            Carrying = Player?.Carrying?.NpcName,
            Disguise = Player?.DisguiseOf,
            Crouching = Player?.Crouching ?? false,
            HasMop = Player?.HasMop ?? false,
            HasBlueprint = Player?.HasBlueprint ?? false,
            BlueprintSent = Player?.BlueprintSent ?? false,
            Alert = AlertTimer > 0 || (Guard?.State == NpcState.Hunt),
            TimeLeft = (float)System.Math.Max(0.0, TimeLeft),
            MaxSuspicion = MaxSuspicionValue,
            BeingWatched = BeingWatched,
            Held = Player?.HeldItem switch
            {
                HeldItem.Laxative => "[laxative sachet]",
                HeldItem.EnergyDrink => "[energy drink]",
                _ => null,
            },
            Dept = Player?.DepartmentDisguise,
            CaseActive = CaseActive,
            CasePct = CaseEvidence,
        };
        bool channeling = Player != null && Player.ChannelProgressFraction >= 0;
        s.Objectives.Add(("Infiltrate the server room",
            Player != null && (Player.HasBlueprint || Player.BlueprintSent || channeling)));
        s.Objectives.Add(("Steal the blueprints", Player != null && (Player.HasBlueprint || Player.BlueprintSent)));
        s.Objectives.Add(("Mail them out via the mail trolley", Player != null && Player.BlueprintSent));
        s.Objectives.Add(("Don't get caught (optional-ish)", false));
        s.Stats = (Stats.Bonks, Stats.Hides, Stats.Reports, Stats.Disguises, Stats.Cleans);
        Hud.Push(s);
    }

    // ================= helpers =================

    private static StandardMaterial3D MakeMat(Color c) =>
        new() { AlbedoColor = c, Roughness = 0.9f };

    private static MeshInstance3D MakeBox(Vector3 size, Color c) =>
        new() { Mesh = new BoxMesh { Size = size }, MaterialOverride = MakeMat(c) };
}


