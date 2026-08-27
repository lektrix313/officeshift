using Godot;
using System.Collections.Generic;
using System.Linq;

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
    public List<OfficeObjectRuntime> OfficeObjects { get; } = new();
    public NpcBrain? Guard { get; private set; }

    public bool Started;
    public bool Over;
    public bool Won;
    public string EndReason = "";
    public double TimeLeft = Bal.ShiftSeconds;
    public double AlertTimer;
    public bool BeingWatched;
    public float MaxSuspicionValue;
    public PlayerConsequenceProfile PlayerProfile { get; } = new();
    public BossDifficulty BossDifficulty { get; set; } = BossDifficulty.Easy;
    public SocialSimulation Social { get; } = new();
    public MultiFloorNavigation Navigation { get; } = new();
    public bool UIOpen;
    public bool StinkActive => WorldEvents.StinkActive;
    public bool VendingLaxativeTaken;
    public bool VendingEnergyTaken;
    public float VendingCooldown;
    public float PhoneCooldown;
    public float AlarmCooldown;
    public float StinkTimer;
    public float EvacTimer;
    public float CaseEvidence;
    public bool CaseActive;
    public string CaseSuspectName { get; private set; } = "";
    public string CaseAllegation { get; private set; } = "";
    public bool TapeShredded;
    public List<StaffMemory> OfficeFeed { get; } = new();
    public List<CaseTestimony> CaseTestimonies { get; } = new();
    public bool HearingOpen { get; private set; }
    public bool HearingAvailable { get; private set; }
    private bool _framedCaseResolved;
    private float _hearingGraceTimer;
    private float _cameraCrimeUntil;
    private bool _onCameraToasted;
    private float _shiftElapsed;
    /// <summary>Capture-only accelerator; normal play remains one realtime hour.</summary>
    public float WorkdayTimeScale { get; set; } = 1f;
    public float MaxSusEver;
    private readonly List<string> _doneObjectives = new();
    private float _lureDwell;
    public MissionContract Active => MissionManager.Active;
    private readonly StatsTracker _stats = new();
    public StatsTracker Stats => _stats;

    public OmniPortal? Portal { get; private set; }
    public TalkOverlay? Talk { get; private set; }

    private float _chatterTimer = 9f;
    private float _gossipTimer = 7f;
    private float _hudTimer;
    private readonly List<ScheduledMeeting> _scheduledMeetings = new();

    private sealed record ScheduledMeeting(string Department, float Hour, string Room, string Source);

    /// <summary>Bodies awaiting their post-settle blood pool (port of poolSpawned logic).</summary>
    private readonly Dictionary<NpcBrain, float> _poolTimers = new();

    private readonly List<NoiseRef> _noises = new();
    private readonly List<float> _noiseBirth = new();
    private readonly List<NpcStimulus> _stimuli = new();
    private Vector3 _lastNoisePos;
    private float _lastNoiseAt = -999f;
    private readonly List<(HideSpotState Spot, NpcBrain Body, float Timer)> _smells = new();
    private readonly List<(HideSpotState Spot, NpcBrain Discoverer)> _discoveries = new();
    public bool PoliceIncoming;
    private float _policeTimer;
    public PoliceInterview? Interview { get; private set; }

    public sealed class StatsTracker
    {
        public int Bonks, Hides, Reports, Disguises, Cleans;
    }

    public override void _Ready()
    {
        Instance = this;
        MissionManager.LoadAll();

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

        var rosterErrors = RosterInvariantChecks.Validate();
        foreach (var error in rosterErrors) GD.PushError($"[Roster] {error}");
        if (rosterErrors.Count > 0) return;
        var workshopPath = Godot.FileAccess.FileExists("user://workshop.json") ? "user://workshop.json" : Godot.FileAccess.FileExists("res://workshop.json") ? "res://workshop.json" : "";
        string workshopError = "";
        var workshop = string.IsNullOrEmpty(workshopPath) ? null : WorkshopLevelData.Load(workshopPath, out workshopError);
        if (workshop == null && !string.IsNullOrEmpty(workshopPath))
            GD.PushWarning($"[Workshop] {workshopError} Falling back to canonical starter layout.");
        if (workshop != null)
        {
            var navigationError = MultiFloorNavigation.Validate(workshop);
            if (!string.IsNullOrEmpty(navigationError))
            {
                GD.PushWarning($"[Workshop] {navigationError} Falling back to canonical navigation.");
                workshop = null;
            }
            else Navigation.AddWorkshopFloors(workshop);
        }

        // ---- systems ----
        Social.SetSeed(42);
        Social.AddDefaultWaypoints();
        if (workshop != null)
        {
            foreach (var waypoint in workshop.Waypoints) Social.AddWorkshopWaypoint(waypoint);
            BindWorkshopAccess(workshop);
            Toast($"WORKSHOP LOADED: {workshop.Business} · {workshop.Waypoints.Count} authored waypoints", ToastKind.Success);
        }
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
        int rosterIndex = 0;
        foreach (var assignment in CanonicalStaff.Assignments)
        {
            var body = new NpcBody();
            body.Init(assignment.Profile.Name, assignment.Archetype);
            AddChild(body);
            var authored = workshop?.Staff.FirstOrDefault(member => member.Name.Equals(assignment.Profile.Name, System.StringComparison.OrdinalIgnoreCase));
            body.Position = authored == null ? assignment.SpawnPosition : new Vector3(-28f + authored.X * 2f, 0f, -20f + authored.Y * 2f);
            var brain = NpcBrain.Create(body, assignment.Zone);
            brain.SetFloor(authored?.FloorId ?? "floor-1");
            brain.InitializeWorkday(rosterIndex++);
            Social.RegisterNpc(brain.NpcName, rosterIndex);
            Npcs.Add(brain);
        }

        // the slob slumps at his desk
        foreach (var n in Npcs)
        {
            if (n.Arch == Archetype.Slob)
            {
                n.Body.Position = WorldData.SlobDeskPos;
            n.State = NpcState.Seated;
            n.WorkState = WorkdayState.FeelingSleepy;
            n.Body.SetWorkdayState(WorkdayState.FeelingSleepy);
            n.Body.ShowSleeping(true);
            }
        }

        // security
        var gBody = new NpcBody();
        gBody.Init(CanonicalStaff.ExecutiveThreatName, Archetype.Guard);
        AddChild(gBody);
        gBody.Position = CanonicalStaff.Find(CanonicalStaff.ExecutiveThreatName)?.SpawnPosition ?? WorldData.GuardPosts[0];
        Guard = NpcBrain.Create(gBody, "guard");
        Guard.PauseTimer = 2f;
        Npcs.Add(Guard);

        // wire player FX events
        Player.StainMopped += OnStainMopped;

        Interview = new PoliceInterview { Name = "Interview" };
        AddChild(Interview);

        CameraSystem.CreateNodes(this);

        // State-bearing object registry. Meshes remain replaceable placeholders.
        OfficeObjects.AddRange(OfficeObjectLibrary.CreateStarterObjects());

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

    public override void _Input(InputEvent e)
    {
        // _Input runs before Control nodes, so the full-screen start overlay cannot swallow clock-in.
        bool clickedToStart = e is InputEventMouseButton mb
            && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left;
        bool acceptedToStart = e.IsActionPressed("ui_accept");

        if (!Started && !Over && (clickedToStart || acceptedToStart))
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
            bool ghost = Active.Objectives.Any(o => o.Type == "GHOST");
            bool othersDone = Active.Objectives.Where(o => o.Type != "GHOST").All(o => ObjectiveDone(o));
            if (ghost && othersDone && MaxSusEver < 30f)
            {
                CompleteObjective("GHOST");
                EndGame(true, "Ghost protocol complete. No logs, no footage, no memories. You were never hired.");
            }
            else
            {
                EndGame(false, "The courier left without your package. Shift wasted. You're fired — from a job you never even had.");
            }
            return;
        }
        if (AlertTimer > 0) AlertTimer -= dt;
        MaxSusEver = System.MathF.Max(MaxSusEver, MaxSuspicionValue);

        _shiftElapsed += dt * WorkdayTimeScale;
        ProcessScheduledMeetings();
        bool visiblyWorking = Player.PlayerSusActivity() <= 0f;
        PlayerProfile.Tick(dt, visiblyWorking);
        foreach (var npc in Npcs) npc.TickAttitude(dt);
        TickOfficeObjects(dt);
        VendingCooldown = System.MathF.Max(0f, VendingCooldown - dt);
        PhoneCooldown = System.MathF.Max(0f, PhoneCooldown - dt);
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
        UIOpen = (Portal != null && Portal.IsOpen) || (Talk != null && Talk.IsOpen)
            || (Interview != null && Interview.IsOpen);

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
        if (HearingAvailable && !HearingOpen)
            _hearingGraceTimer = System.MathF.Max(0f, _hearingGraceTimer - dt);
        if (CaseActive && !string.IsNullOrEmpty(CaseSuspectName) &&
            !_framedCaseResolved && !HearingOpen && CaseEvidence >= 70f &&
            _hearingGraceTimer <= 0f)
        {
            ResolveFramedCase();
        }
        if (CaseEvidence >= 100f)
        {
            EndGame(false, "The HR hearing lasts four minutes. The spreadsheet is entered into evidence. You are escorted out with a box of desk plants.");
            return;
        }

        // ---- AI tick ----
        var ctx = new AiContext
        {
            PlayerPos = Player!.FeetPos,
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
            BossDifficulty = BossDifficulty,
            EvacPoint = WorldEvents.EvacPoint,
            BathroomPoint = new Vector3(-26.5f, 0f, -4f),
            CoffeePoint = new Vector3(25f, 0f, -19.2f),
            CoffeeSpiked = WorldEvents.CoffeeSpiked && WorldEvents.SpikeUsesLeft > 0,
            StinkActive = WorldEvents.StinkActive,
            StinkPos = WorldEvents.StinkPos,
            NoiseFresh = _shiftElapsed - _lastNoiseAt < 8f,
            NoisePos = _lastNoisePos,
            Stimuli = _stimuli,
            WorkdayElapsed = _shiftElapsed,
            Toast = Toast,
            AlarmSfx = () => Synth?.Alarm(),
            CanSee = CanSee,
            SetObjectState = (id, state, actor) => SetOfficeObjectState(id, state, false, actor),
            OnReportReachedGuard = OnReportReachedGuard,
            OnPlayerCaught = () => EndGame(false, "Officer Mr Purple caught you red-handed. HR would like a word. Several words. In a basement."),
        };
        Social.Tick(Npcs, (float)dt, WorkdayBalance.WorkdayStartHour + _shiftElapsed / (Bal.ShiftSeconds / 8f));
        TickMultiFloorRoutes((float)dt);
        AiDirector.Tick(Npcs, ctx, dt);
        if (Guard != null) AiDirector.GuardTick(Guard, ctx, dt);
        MaxSuspicionValue = AiDirector.Outputs.MaxSus;
        BeingWatched = AiDirector.Outputs.Watched;
        AlertTimer = ctx.AlertTimer; // guard logic may extend/consume it

        // ---- mission objectives ----
        foreach (var o in Active.Objectives)
        {
            if (o.Type != "LURE_NPC" || ObjectiveDone(o)) continue;
            var npc = Npcs.Find(x => x.NpcName == o.Npc);
            if (npc == null) continue;
            if (MissionZones.Contains(o.Zone, npc.Pos))
            {
                _lureDwell += dt;
                if (_lureDwell >= 3f)
                {
                    _lureDwell = 0f;
                    CompleteObjective(o);
                }
            }
            else _lureDwell = 0f;
        }

        // ---- office memory / gossip loop ----
        _gossipTimer -= dt;
        if (_gossipTimer <= 0f)
        {
            _gossipTimer = 12f;
            SpreadOneRumor();
        }

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

        // ---- smell clock: hidden bodies ripen, get discovered, police get called ----
        for (int i = _smells.Count - 1; i >= 0; i--)
        {
            var (spot, body, timer) = _smells[i];
            float t = timer - dt;
            if (t <= 0)
            {
                _smells.RemoveAt(i);
                PublishStimulus(NpcStimulusKind.Stink, spot.Pos, 1.15f, false,
                    $"A hidden body has started to smell near the {spot.Name}.", radius: 16f);
                Toast($"Something smells... ripe. Near the {spot.Name}.", ToastKind.Warn);
                NpcBrain? sniffer = null;
                float d1 = float.MaxValue;
                foreach (var n in Npcs)
                {
                    if (!n.Awake || n.Talking || n == Guard) continue;
                    float d = n.Pos.DistanceTo(spot.Pos);
                    if (d < d1) { d1 = d; sniffer = n; }
                }
                if (sniffer != null)
                {
                    ApplyDirective(sniffer, "coffeepoint", 0f);
                    sniffer.DirectiveZone = null;
                    sniffer.DirectiveTarget = spot.Pos;
                    sniffer.DirectiveTimer = 60f;
                    _discoveries.Add((spot, sniffer));
                }
            }
            else _smells[i] = (spot, body, t);
        }
        for (int i = _discoveries.Count - 1; i >= 0; i--)
        {
            var (spot, disc) = _discoveries[i];
            if (!disc.Awake || disc.Disposed) { _discoveries.RemoveAt(i); continue; }
            if (disc.Pos.DistanceTo(spot.Pos) < 1.4f)
            {
                _discoveries.RemoveAt(i);
                DiscoverBody(spot, disc);
            }
        }
        if (PoliceIncoming)
        {
            _policeTimer -= dt;
            if (_policeTimer <= 0 && !UIOpen && Interview != null && !Interview.IsOpen)
            {
                Interview.Open();
                UIOpen = true;
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

    /// <summary>Schedules a department meeting from a computer; staff receive the same event through the normal stimulus queue.</summary>
    public bool ScheduleDepartmentMeeting(string department, float hour, string room = "meeting_a", string source = "computer")
    {
        if (hour < WorkdayBalance.WorkdayStartHour || hour >= WorkdayBalance.WorkdayEndHour) return false;
        if (!new[] { "Accounts", "IT", "HR", "Sales", "Operations", "Facilities", "Security" }.Contains(department)) return false;
        _scheduledMeetings.Add(new ScheduledMeeting(department, hour, room, source));
        Toast($"Meeting scheduled: {department} at {hour:0.0} in {room.Replace('_', ' ')}. Productivity has been weaponized.", ToastKind.Success);
        return true;
    }

    private void ProcessScheduledMeetings()
    {
        if (_scheduledMeetings.Count == 0) return;
        float hour = WorkdayBalance.WorkdayStartHour + _shiftElapsed / (Bal.ShiftSeconds / (WorkdayBalance.WorkdayEndHour - WorkdayBalance.WorkdayStartHour));
        for (int i = _scheduledMeetings.Count - 1; i >= 0; i--)
        {
            var meeting = _scheduledMeetings[i];
            if (hour < meeting.Hour || hour >= meeting.Hour + WorkdayBalance.MeetingDurationHours) continue;
            _scheduledMeetings.RemoveAt(i);
            var participants = Npcs.Where(n => n.Department == meeting.Department && n.Awake).ToArray();
            foreach (var participant in participants)
            {
                ApplyDirective(participant, meeting.Room, 30f);
                PublishStimulus(NpcStimulusKind.MeetingPressure, participant.Pos, 0.8f, false,
                    $"{meeting.Department} meeting at {meeting.Room}.", radius: 24f, source: participant);
            }
            Toast($"{meeting.Department} department is filing into {meeting.Room.Replace('_', ' ')}. The desks are briefly yours.", ToastKind.Info);
        }
    }

    public void ApplyPlayerAction(ActionProfile action, float visibility = 1f, float credibility = 1f)
    {
        PlayerProfile.Apply(action, visibility, credibility);
        Toast($"COMPANY PROFILE — suspicion {PlayerProfile.SuspicionBand} / loyalty {PlayerProfile.LoyaltyBand} / work {PlayerProfile.WorkBand}", ToastKind.Info);
    }

    public void SetNpcAttitude(NpcBrain npc, NpcAttitudeKind kind, float strength, float duration, string source)
    {
        npc.SetAttitude(kind, strength, duration, source);
    }

    public void RecoverNpcAttitude(NpcBrain npc, float multiplier = 1f)
    {
        npc.RecoverAttitude(multiplier);
    }

    private void TickOfficeObjects(float dt)
    {
        foreach (var officeObject in OfficeObjects)
            officeObject.Tick(dt);
    }

    public OfficeObjectRuntime? OfficeObject(string id) =>
        OfficeObjects.FirstOrDefault(officeObject => officeObject.Id == id);

    /// <summary>Changes an object state and feeds its configured effect to the consequence engine.</summary>
    public bool SetOfficeObjectState(string id, OfficeObjectState next, bool playerLed = false,
        NpcBrain? actor = null, string? keycardId = null)
    {
        var officeObject = OfficeObject(id);
        if (officeObject == null || !officeObject.Definition.Allows(next)) return false;
        if (next == OfficeObjectState.Unlocked &&
            (officeObject.State is OfficeObjectState.Locked or OfficeObjectState.KeycardRequired) &&
            !officeObject.TryUse(keycardId))
            return false;
        if (!officeObject.SetState(next)) return false;
        officeObject.LastActor = actor;
        var profile = officeObject.Definition.ProfileFor(next);
        if (!profile.StimulusKind.HasValue) return true;
        PublishStimulus(profile.StimulusKind.Value, officeObject.Position, profile.Activation,
            playerLed, $"{officeObject.Definition.DisplayName} is {next}.",
            objectId: officeObject.Id, objectType: officeObject.Definition.Type,
            objectState: next, objectDepartment: officeObject.Definition.Department,
            stressDelta: profile.StressDelta, comfortDelta: profile.ComfortDelta,
            radius: profile.Radius, source: actor, activeUser: actor,
            preferredAction: profile.PreferredAction);
        return true;
    }

    /// <summary>Feeds player-led and ambient events into the same NPC consequence queue.</summary>
    public void PublishStimulus(NpcStimulusKind kind, Vector3 position, float intensity,
        bool playerLed, string description, object? evidenceRef = null,
        EvidenceKind? evidenceKind = null, float radius = 18f, NpcBrain? source = null,
        string objectId = "", OfficeObjectType? objectType = null,
        OfficeObjectState? objectState = null, string objectDepartment = "",
        float stressDelta = 0f, float comfortDelta = 0f,
        NpcReactionAction preferredAction = NpcReactionAction.Observe,
        NpcBrain? activeUser = null)
    {
        _stimuli.Add(new NpcStimulus
        {
            Id = $"{kind}:{_shiftElapsed:F2}:{_stimuli.Count}",
            Kind = kind,
            Position = position,
            Intensity = intensity,
            PlayerLed = playerLed,
            Description = description,
            ObjectId = objectId,
            ObjectType = objectType,
            ObjectState = objectState,
            ObjectDepartment = objectDepartment,
            StressDelta = stressDelta,
            ComfortDelta = comfortDelta,
            PreferredAction = preferredAction,
            EvidenceRef = evidenceRef,
            EvidenceKind = evidenceKind,
            Radius = radius,
            ActiveUser = activeUser,
            Source = source,
        });
    }

    private void TickMultiFloorRoutes(float dt)
    {
        foreach (var npc in Npcs)
        {
            if (!npc.IsChangingFloor || npc.WorkdayTarget == null) continue;
            npc.TickFloorTransition(dt);
        }
    }

    public bool TryMoveNpcToFloor(NpcBrain npc, string targetFloor, string? keycardId = null)
    {
        if (npc.FloorId.Equals(targetFloor, System.StringComparison.OrdinalIgnoreCase)) return true;
        var link = Navigation.FindLink(npc.FloorId, targetFloor);
        if (link == null || !Navigation.CanTraverse(npc.FloorId, targetFloor, keycardId))
        {
            PublishStimulus(NpcStimulusKind.AccessDenied, npc.Pos, ObjectBalance.AccessDeniedActivation, false,
                $"{npc.NpcName} cannot access {targetFloor}.", radius: ObjectBalance.AccessDeniedRadius, source: npc,
                preferredAction: NpcReactionAction.SeekHelp);
            return false;
        }
        npc.BeginFloorTransition(targetFloor);
        npc.SetReactionDestination(link.ToPosition, NpcReactionAction.GoToMeeting, 4f);
        return true;
    }

    private void BindWorkshopAccess(WorkshopLevelData workshop)
    {
        var cards = workshop.AccessCards.ToDictionary(card => card.Id, card => card, System.StringComparer.OrdinalIgnoreCase);
        foreach (var element in workshop.Elements)
        {
            if (string.IsNullOrEmpty(element.AccessCardId) || !cards.ContainsKey(element.AccessCardId)) continue;
            var type = element.Type switch
            {
                "door" => OfficeObjectType.Door,
                "terminal-desk" => OfficeObjectType.ServerTerminal,
                "elevator" => OfficeObjectType.Elevator,
                "stair" => OfficeObjectType.Stairwell,
                _ => (OfficeObjectType?)null,
            };
            if (!type.HasValue) continue;
            var id = $"workshop:{element.Id}";
            if (OfficeObject(id) != null) continue;
            var definition = OfficeObjectLibrary.Catalog.FirstOrDefault(candidate => candidate.Type == type.Value);
            if (definition == null) continue;
            var runtime = new OfficeObjectRuntime(id, definition, new Vector3(-28f + (element.X + element.Width / 2f) * 2f, 0f, -20f + (element.Y + element.Height / 2f) * 2f))
            {
                RequiredKeycard = element.AccessCardId,
                State = OfficeObjectState.KeycardRequired,
            };
            OfficeObjects.Add(runtime);
        }
    }

    public bool TryAccessOfficeObject(string id, string? keycardId, NpcBrain? actor = null)
    {
        var officeObject = OfficeObject(id);
        if (officeObject == null) return false;
        if (officeObject.TryUse(keycardId)) return true;
        ApplyPlayerAction(ConsequenceActions.RestrictedAccess, 0.8f, 1f);
        PublishStimulus(NpcStimulusKind.AccessDenied, officeObject.Position, ObjectBalance.AccessDeniedActivation,
            actor == null, $"Access denied at {officeObject.Definition.DisplayName}.",
            objectId: officeObject.Id, objectType: officeObject.Definition.Type,
            objectState: officeObject.State, objectDepartment: officeObject.Definition.Department,
            stressDelta: ObjectBalance.AccessDeniedStress, radius: ObjectBalance.AccessDeniedRadius, source: actor,
            preferredAction: NpcReactionAction.SeekHelp);
        return false;
    }

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

    /// <summary>Record a remembered incident for nearby witnesses and the office feed.</summary>
    public void RecordIncident(string subject, string incident, Vector3 pos, string narrative,
        MemoryKind kind = MemoryKind.Witness)
    {
        var feedMemory = new StaffMemory
        {
            Subject = subject,
            Incident = incident,
            Narrative = narrative,
            Location = pos,
            Confidence = kind == MemoryKind.Forged ? 58f : 82f,
            Kind = kind,
            Shared = false,
            Age = 0f,
        };
        OfficeFeed.Insert(0, feedMemory);
        while (OfficeFeed.Count > 24) OfficeFeed.RemoveAt(OfficeFeed.Count - 1);

        foreach (var witness in Npcs)
        {
            if (!witness.Awake || witness == Guard) continue;
            float distance = witness.Pos.DistanceTo(pos);
            if (distance > 12f) continue;
            bool sawIt = distance < Bal.WitnessAutoSeeDist || CanSee(witness, pos);
            if (!sawIt && kind != MemoryKind.Forged) continue;
            witness.Remember(new StaffMemory
            {
                Subject = subject,
                Incident = incident,
                Narrative = narrative,
                Location = pos,
                Confidence = kind == MemoryKind.Forged
                    ? 48f + witness.Personality.Agreeableness * 35f
                    : 55f + witness.Personality.Conscientiousness * 40f,
                Kind = kind,
                Shared = false,
                Age = 0f,
            });
        }
    }

    private void SpreadOneRumor()
    {
        var source = Npcs.FirstOrDefault(n => n.Arch == Archetype.Gossip && n.Awake &&
            n.Memories.Any(m => !m.Shared && m.Confidence > 20f));
        if (source == null) return;
        var original = source.Memories.Last(m => !m.Shared && m.Confidence > 20f);
        original.Shared = true;
        string hedge = original.Confidence > 70f ? "Pam says" : "Pam vaguely remembers";
        string narrative = $"{hedge} {original.Subject} was involved in {original.Incident.ToLowerInvariant()} near {WorldData.RoomAt(original.Location.X, original.Location.Z)}.";
        foreach (var listener in Npcs)
        {
            if (!listener.Awake || listener == source || listener == Guard) continue;
            float range = Bal.GossipRadius * source.Personality.GossipRadiusMultiplier;
            if (listener.Pos.DistanceTo(source.Pos) > range) continue;
            listener.Remember(new StaffMemory
            {
                Subject = original.Subject,
                Incident = original.Incident,
                Narrative = narrative,
                Location = original.Location,
                Confidence = original.Confidence * (0.48f + listener.Personality.Openness * 0.22f),
                Kind = MemoryKind.Rumor,
                Shared = false,
                Age = 0f,
            });
        }
        OfficeFeed.Insert(0, new StaffMemory
        {
            Subject = original.Subject,
            Incident = original.Incident,
            Narrative = narrative,
            Location = original.Location,
            Confidence = original.Confidence * 0.65f,
            Kind = MemoryKind.Rumor,
            Shared = false,
            Age = 0f,
        });
        while (OfficeFeed.Count > 24) OfficeFeed.RemoveAt(OfficeFeed.Count - 1);
        if (original.Kind == MemoryKind.Forged && original.Subject == CaseSuspectName)
        {
            CaseEvidence = System.MathF.Min(100f, CaseEvidence + 14f);
            Toast($"The rumor sticks. HR logs another corroborating account about {CaseSuspectName}.", ToastKind.Warn);
        }
        Toast($"OFFICE FEED: {narrative}", ToastKind.Warn);
    }

    /// <summary>Files a target-specific anonymous HR allegation and seeds staff memories.</summary>
    public void FileAnonymousReport(string suspect, string allegation, string details)
    {
        ApplyPlayerAction(ConsequenceActions.FramedReport, 0.9f, PlayerProfile.CompanyTrust / 100f + 0.5f);
        var target = Npcs.FirstOrDefault(n => n.NpcName == suspect && n != Guard);
        if (target == null) return;
        CaseSuspectName = suspect;
        CaseAllegation = allegation;
        CaseEvidence = System.MathF.Min(100f, CaseEvidence + (allegation == "A MURDER" ? 56f : 22f));
        CaseActive = true;
        _framedCaseResolved = false;
        HearingAvailable = true;
        HearingOpen = false;
        _hearingGraceTimer = 20f;
        BuildCaseTestimonies(suspect, allegation, target.Pos);
        string narrative = string.IsNullOrWhiteSpace(details)
            ? $"Anonymous report: {suspect} is implicated in {allegation.ToLowerInvariant()}."
            : $"Anonymous report: {suspect} — {details.Trim()}";
        RecordIncident(suspect, allegation, target.Pos, narrative, MemoryKind.Forged);
        PublishStimulus(NpcStimulusKind.PlayerCrime, target.Pos, 1.1f, true,
            $"The player filed a forged {allegation} allegation against {suspect}.", radius: 18f);
        foreach (var witness in Npcs)
        {
            if (!witness.Awake || witness == target || witness == Guard) continue;
            if (witness.Arch == Archetype.Gossip)
            {
                witness.Remember(new StaffMemory
                {
                    Subject = suspect,
                    Incident = allegation,
                    Narrative = narrative,
                    Location = target.Pos,
                    Confidence = 56f + witness.Personality.Extraversion * 20f,
                    Kind = MemoryKind.Forged,
                    Shared = false,
                    Age = 0f,
                });
            }
        }
        var reportedTarget = Npcs.Find(n => n.NpcName == suspect);
        if (reportedTarget != null)
        {
            reportedTarget.SetAttitude(NpcAttitudeKind.Resentful, 0.85f, 360f, "framed report");
            foreach (var friend in Npcs)
                if (friend != reportedTarget && friend.Pos.DistanceTo(reportedTarget.Pos) < 12f)
                    friend.SetAttitude(NpcAttitudeKind.Suspicious, 0.35f, 180f, "coworker reported");
        }
        Toast($"ANONYMOUS REPORT FILED. {suspect} is now the subject of an HR case: {allegation}.", ToastKind.Chaos);
        Toast("The office has received a version of events. Versions are powerful.", ToastKind.Warn);
    }

    private void BuildCaseTestimonies(string suspect, string allegation, Vector3 location)
    {
        CaseTestimonies.Clear();
        var witnesses = Npcs
            .Where(n => n != Guard && n.NpcName != suspect && n.Awake)
            .OrderByDescending(n => n.Personality.Conscientiousness)
            .Take(3)
            .ToList();
        for (int i = 0; i < witnesses.Count; i++)
        {
            var witness = witnesses[i];
            bool contradiction = i == 1 || witness.Personality.Openness < 0.4f;
            string claimedRoom = contradiction
                ? "the printer room"
                : WorldData.RoomAt(location.X, location.Z).ToString().ToLowerInvariant();
            string statement = contradiction
                ? $"{witness.NpcName}: I heard {suspect} was involved, but I remember the incident being near the printer room."
                : $"{witness.NpcName}: I saw enough to believe {suspect} was involved in {allegation.ToLowerInvariant()} near the {claimedRoom}.";
            CaseTestimonies.Add(new CaseTestimony
            {
                Witness = witness.NpcName,
                Suspect = suspect,
                Statement = statement,
                LocationClaim = claimedRoom,
                Confidence = 46f + witness.Personality.Conscientiousness * 42f,
                Contradictory = contradiction,
                Challenged = false,
                Coached = false,
            });
        }
    }

    public void OpenHearing()
    {
        if (!CaseActive || string.IsNullOrEmpty(CaseSuspectName) || Portal == null) return;
        HearingOpen = true;
        _hearingGraceTimer = 999f;
        Portal.OpenHearing();
        UIOpen = true;
    }

    public void ChallengeTestimony(int index)
    {
        if (!HearingOpen || index < 0 || index >= CaseTestimonies.Count) return;
        var testimony = CaseTestimonies[index];
        if (testimony.Challenged) return;
        testimony.Challenged = true;
        if (testimony.Contradictory)
        {
            testimony.Confidence = System.MathF.Max(0f, testimony.Confidence - 28f);
            CaseEvidence = System.MathF.Max(0f, CaseEvidence - 18f);
            Toast($"CHALLENGE: {testimony.Witness}'s story contradicts the location record. HR removes a confidence point.", ToastKind.Success);
        }
        else
        {
            CaseEvidence = System.MathF.Min(100f, CaseEvidence + 8f);
            Toast($"CHALLENGE FAILED: {testimony.Witness} is annoyingly consistent.", ToastKind.Warn);
        }
    }

    public void CoachTestimony(int index)
    {
        if (!HearingOpen || index < 0 || index >= CaseTestimonies.Count) return;
        var testimony = CaseTestimonies[index];
        if (testimony.Coached) return;
        testimony.Coached = true;
        testimony.Confidence = System.MathF.Max(20f, testimony.Confidence - 10f);
        CaseEvidence = System.MathF.Min(100f, CaseEvidence + 6f);
        Toast($"COACHING: {testimony.Witness} repeats your version with suspiciously perfect wording.", ToastKind.Info);
    }

    public void AppealCase()
    {
        if (!HearingOpen) return;
        int contradictions = CaseTestimonies.Count(t => t.Contradictory && t.Challenged);
        int coached = CaseTestimonies.Count(t => t.Coached);
        HearingOpen = false;
        HearingAvailable = false;
        UIOpen = false;
        Portal?.Close();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        if (contradictions >= 1 && CaseEvidence < 70f)
        {
            CaseEvidence = 0f;
            CaseActive = false;
            CaseSuspectName = "";
            CaseAllegation = "";
            _framedCaseResolved = false;
            Toast("APPEAL UPHELD. The stories do not agree. HR files the case under 'probably not'.", ToastKind.Success);
        }
        else
        {
            CaseEvidence = System.MathF.Min(100f, CaseEvidence + (coached > 0 ? 12f : 22f));
            Toast("APPEAL DENIED. The paperwork has paperwork. The case proceeds.", ToastKind.Chaos);
        }
    }

    private void ResolveFramedCase()
    {
        var target = Npcs.FirstOrDefault(n => n.NpcName == CaseSuspectName && n != Guard);
        if (target == null) return;
        _framedCaseResolved = true;
        HearingAvailable = false;
        HearingOpen = false;
        target.Quit = true;
        target.State = NpcState.Hidden;
        target.Body.SetVisibleRec(false);
        Toast($"HR DECISION: {target.NpcName} is responsible for {CaseAllegation.ToLowerInvariant()}. Badge revoked. Desk plants boxed.", ToastKind.Chaos);
        RecordIncident(target.NpcName, "a confirmed HR case", target.HomePos,
            $"HR confirmed {target.NpcName} for {CaseAllegation.ToLowerInvariant()}. The office remembers the verdict, not the truth.",
            MemoryKind.Forged);
        var sheet = Personas.RandomSheet();
        Personas.ByName[sheet.Name] = sheet;
        var body = new NpcBody();
        body.Init(sheet.Name, Archetype.Drone);
        AddChild(body);
        body.Position = target.HomePos;
        var replacement = NpcBrain.Create(body, "drone");
        replacement.InitializeWorkday(Npcs.Count);
        Npcs.Add(replacement);
        Toast($"Replacement hire: {sheet.Name}. {sheet.Traits}. Nobody asked for a reference.", ToastKind.Success);
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
        CompleteKnockout(victim);
        Synth?.Bonk();
        Toast(wasAsleep
            ? $"{victim.NpcName} was already asleep. You just made it official. And messy."
            : $"You bonked {victim.NpcName} with a keyboard. There is… some blood. Mop's in the supply closet.",
            ToastKind.Chaos);
        RecordIncident("You", "a knockout", victim.Pos,
            $"Someone saw you put {victim.NpcName} down near the {WorldData.RoomAt(victim.Pos.X, victim.Pos.Z)}.");
        PublishStimulus(NpcStimulusKind.PlayerCrime, victim.Pos, 1.6f, true,
            $"The player knocked out {victim.NpcName}.", victim, EvidenceKind.Body, 15f);

        /* Witness activation, suspicion, and waking now come from PlayerCrime stimulus processing. */
        /* The loop is retained only for disguise breakage on direct witnesses below. */
        foreach (var w in Npcs)
        {
            if (w == victim || !w.Awake) continue;
            float dist = w.Pos.DistanceTo(Player.FeetPos);
            bool seen = CanSee(w, Player.FeetPos) || dist < Bal.WitnessAutoSeeDist;
            if (!seen) continue;
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
        if (spot.SmellDelay > 0)
            _smells.Add((spot, victim, spot.SmellDelay));

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
        ApplyPlayerAction(ConsequenceActions.Cleanup, 0.7f, 1f);
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
        ApplyPlayerAction(ConsequenceActions.HonestReport, 1f, PlayerProfile.CompanyTrust / 100f + 0.5f);
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
        Toast("Officer Mr Purple has been informed. He is walking over with intent.", ToastKind.Warn);
        RecordIncident("You", "a witness report", reporter.Pos,
            $"{reporter.NpcName} reported suspicious activity to Mr Purple.");
        Synth?.Alarm();
    }

    /// <summary>Prop-induced knockout (chair to the back): KO + witnesses, no blood.</summary>
    public void OnPropKnockout(NpcBrain victim, Vector3 flopDir, string itemType)
    {
        if (victim.State == NpcState.Out || Player == null) return;
        bool wasAsleep = victim.State == NpcState.Seated;
        ApplyPlayerAction(ConsequenceActions.MajorCrime, 1f, 1f);
        FlashCrime();
        victim.KnockOut(flopDir);
        Stats.Bonks++;
        CompleteKnockout(victim);
        Synth?.Bonk();
        Toast(wasAsleep
            ? $"{victim.NpcName} was already asleep. The {itemType} made it official."
            : $"{victim.NpcName} eats a {itemType} to the back. Down. Very down.", ToastKind.Chaos);
        RecordIncident("You", $"a {itemType} assault", victim.Pos,
            $"A {itemType} knocked {victim.NpcName} out near the {WorldData.RoomAt(victim.Pos.X, victim.Pos.Z)}.");
        PublishStimulus(NpcStimulusKind.PlayerCrime, victim.Pos, 1.35f, true,
            $"The player hit {victim.NpcName} with a {itemType}.", victim, EvidenceKind.Body, 15f);

        foreach (var w in Npcs)
        {
            if (w == victim || !w.Awake) continue;
            float dist = w.Pos.DistanceTo(victim.Pos);
            bool seen = CanSee(w, victim.Pos) || dist < Bal.WitnessAutoSeeDist;
            if (!seen) continue;
            if (Player.DisguiseOf != null) Player.BlowDisguise();
        }
    }

    private void ClearInvestigationsOf(NpcBrain target, string spotName, float minSus)
    {
        foreach (var n in Npcs)
        {
            if ((n.State == NpcState.Curious || n.State == NpcState.Panic) &&
                (ReferenceEquals(n.InvestigateRef, target) || ReferenceEquals(n.PanicRef, target)))
            {
                n.ShrugItOff(minSus);
                Toast($"{n.NpcName} saw {target.NpcName} vanish into the {spotName}. \"…Nope. Not paid enough.\"", ToastKind.Warn);
            }
        }
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

        PublishStimulus(NpcStimulusKind.PlayerNoise, pos, shattered ? 1.35f : 0.9f, true,
            $"A {itemType} made a noise.", noiseRef, EvidenceKind.Noise, radius);

    }

    private void CompleteKnockout(NpcBrain victim)
    {
        var o = Active.Objectives.FirstOrDefault(x =>
            x.Type == "KNOCKOUT_NPC" && x.Npc == victim.NpcName && !ObjectiveDone(x));
        if (o != null) CompleteObjective(o);
    }

    // ================= mission objectives =================

    public MissionObjective? PendingObjective(string type) =>
        Active.Objectives.FirstOrDefault(o => o.Type == type && !ObjectiveDone(o));

    private static string ObjKey(MissionObjective o) => $"{o.Type}:{o.Npc}:{o.Zone}";

    private bool ObjectiveDone(MissionObjective o) => _doneObjectives.Contains(ObjKey(o));

    public void CompleteObjective(MissionObjective o)
    {
        string key = ObjKey(o);
        if (_doneObjectives.Contains(key)) return;
        _doneObjectives.Add(key);
        ApplyPlayerAction(ConsequenceActions.VisibleWork, 0.8f, 1f);
        Synth?.Success();
        Toast($"OBJECTIVE COMPLETE — {ObjectiveLabel(o)}", ToastKind.Success);
        if (Active.Objectives.All(ObjectiveDone))
            EndGame(true, Active.WinLine);
    }

    public void CompleteObjective(string type) 
    {
        var o = Active.Objectives.FirstOrDefault(x => x.Type == type && !_doneObjectives.Contains(ObjKey(x)));
        if (o != null) CompleteObjective(o);
    }

    public void AcceptContractById(string id)
    {
        var c = MissionManager.Loaded.FirstOrDefault(x => x.Id == id);
        if (c == null) { Toast($"No contract '{id}' on the board.", ToastKind.Info); return; }
        MissionManager.Accept(c);
        _doneObjectives.Clear();
        if (Player != null) { Player.HasBlueprint = false; Player.BlueprintSent = false; }
        Toast($"CONTRACT ACCEPTED — {c.Title}. {c.Brief}", ToastKind.Chaos);
        PushHud();
    }

    private static string ObjectiveLabel(MissionObjective o) => o.Type switch
    {
        "STEAL_BLUEPRINTS" => "Steal the blueprints",
        "PHOTO_WHITEBOARD" => "Photograph the whiteboard",
        "LURE_NPC" => $"Lure {o.Npc} into the {o.Zone}",
        "GHOST" => "Ghost: finish under 30 suspicion",
        "KNOCKOUT_NPC" => $"Bonk {o.Npc}. For the mission.",
        _ => o.Type,
    };

    // ================= body economy =================

    public void DisposeBody(NpcBrain victim, HideSpotState spot)
    {
        if (Player?.Carrying != victim) return;
        Player.Carrying = null;
        victim.Disposed = true;
        victim.State = NpcState.Hidden;
        victim.Body.SetVisibleRec(false);
        Stats.Hides++;
        Synth?.Pickup();
        ClearInvestigationsOf(victim, spot.Name, Bal.VanishPanicSus);
        Toast($"{victim.NpcName} was {spot.Action}. No body, no crime. Legally.", ToastKind.Success);
    }

    private void DiscoverBody(HideSpotState spot, NpcBrain discoverer)
    {
        var victim = spot.Occupants.Count > 0
            ? Npcs.Find(n => n.NpcName == spot.Occupants[0])
            : null;
        if (victim == null || victim.Disposed || victim.Quit) return;

        victim.Body.Position = new Vector3(spot.Pos.X + 0.6f, 0f, spot.Pos.Z);
        victim.Body.SetVisibleRec(true);
        victim.Body.PlayKnockoutPose();
        victim.Body.ShowSleeping(true);
        victim.State = NpcState.Out;
        spot.Occupants.Clear();
        spot.SmellDelay = 0f;

        Toast($"THE BODY. {discoverer.NpcName} found {victim.NpcName} in the {spot.Name}. THE POLICE HAVE BEEN CALLED.", ToastKind.Chaos);
        PublishStimulus(NpcStimulusKind.BodyFound, spot.Pos, 1.45f, false,
            $"{discoverer.NpcName} found {victim.NpcName}.", victim, EvidenceKind.Body, 20f, discoverer);
        RecordIncident("Unknown", "a body discovery", spot.Pos,
            $"{discoverer.NpcName} found {victim.NpcName} in the {spot.Name}. Nobody agrees who put them there.");
        Synth?.Alarm();
        CaseEvidence = System.MathF.Min(100f, CaseEvidence + 45f);
        if (!CaseActive) { CaseActive = true; Toast("HR CASE OPENED — a body is HR's whole personality.", ToastKind.Chaos); }

        PoliceIncoming = true;
        _policeTimer = 8f;
        Interview?.Prepare(victim.NpcName, spot.Name);
    }

    public void OnInterviewResolved(int passes, int total)
    {
        PoliceIncoming = false;
        UIOpen = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        if (passes >= 2)
        {
            CaseEvidence = System.MathF.Min(100f, CaseEvidence + (passes == total ? 30f : 55f));
            Toast($"Interview over. {passes}/{total} answers held up. The detective squints at you forever now.", passes == total ? ToastKind.Success : ToastKind.Warn);
        }
        else
        {
            EndGame(false, "The detective stops writing. 'That's the one, officer.' Handcuffs. Camera. Box of desk plants. FIN.");
        }
    }

    public void OpenResignation(NpcBrain npc)
    {
        if (UIOpen || Portal == null) return;
        Portal.OpenForge(npc);
        UIOpen = true;
    }

    public void OnResignationSent(NpcBrain npc, string letter)
    {
        string low = letter.ToLowerInvariant();
        bool hasReason = new[] { "family", "relocate", "opportunity", "startup", "health", "travel", "find myself", "soul", "abroad", "grief" }
            .Any(low.Contains);
        bool suspicious = new[] { "kill", "murder", "dead", "body", "i did it", "buried", "sorry about" }.Any(low.Contains);
        bool accepted = letter.Length > 60 && hasReason && !suspicious;
        string verdict = accepted
            ? "Letter reads exactly like them. Eerie."
            : suspicious
                ? "HR forwarded this to the police. Why would you write that."
                : "HR bounced it: 'Doesn't read like them.'";
        ResolveResignation(npc, accepted, verdict);
    }

    public void ResolveResignation(NpcBrain npc, bool accepted, string verdict)
    {
        Toast($"[RESIGNATION] {verdict}", accepted ? ToastKind.Success : ToastKind.Warn);
        if (!accepted)
        {
            CaseEvidence = System.MathF.Min(100f, CaseEvidence + 10f);
            return;
        }

        npc.Quit = true;
        npc.State = NpcState.Hidden;
        npc.Body.SetVisibleRec(false);
        Toast($"{npc.NpcName} has resigned, effective immediately. Their desk is already being auctioned.", ToastKind.Success);

        var sheet = Personas.RandomSheet();
        Personas.ByName[sheet.Name] = sheet;
        var body = new NpcBody();
        body.Init(sheet.Name, Archetype.Drone);
        AddChild(body);
        body.Position = npc.HomePos;
        var brain = NpcBrain.Create(body, "drone");
        brain.InitializeWorkday(Npcs.Count);
        Npcs.Add(brain);
        Toast($"New hire: {sheet.Name}. {sheet.Traits}. {sheet.Greeting}", ToastKind.Info);
        UIOpen = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
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

    public void PhoneLure(Vector3 phonePos)
    {
        if (PhoneCooldown > 0 || Player == null) return;
        PhoneCooldown = 30f;
        NpcBrain? best = null;
        float d1 = float.MaxValue;
        foreach (var n in Npcs)
        {
            if (!n.Awake || n.Talking || n == Guard) continue;
            float d = n.Pos.DistanceTo(Player.FeetPos);
            if (d < d1) { d1 = d; best = n; }
        }
        if (best == null) { Toast("You call every extension. Nobody picks up. The office is haunted.", ToastKind.Info); return; }
        Synth?.Pickup();
        ApplyDirective(best, "coffeepoint", 0f); // clear any stale directive
        best.DirectiveZone = null;
        best.DirectiveTarget = phonePos;
        best.DirectiveTimer = 25f;
        PublishStimulus(NpcStimulusKind.PhoneCall, phonePos, 0.9f, true,
            $"A desk phone is ringing for {best.NpcName}.", radius: 18f, source: best);
        Toast($"{best.NpcName} answers the desk phone: \"Yes, this is {best.NpcName}.\" They're heading over.", ToastKind.Info);
    }

    public void SpikeCoffee()
    {
        FlashCrime();
        WorldEvents.CoffeeSpiked = true;
        WorldEvents.SpikeUsesLeft = 3;
        Synth?.Pickup();
        PublishStimulus(NpcStimulusKind.CoffeeBreak, Player?.FeetPos ?? Vector3.Zero, 1.1f, true,
            "The player spiked the office coffee.", radius: 24f);
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
        PublishStimulus(NpcStimulusKind.CoffeeBreak, Player?.FeetPos ?? Vector3.Zero, 0.75f, true,
            "Fresh coffee is available at the break station.", radius: 24f);
        Toast(pulled > 0
            ? $"Fresh coffee. The scent drags {first?.NpcName} and {second?.NpcName} away from their desks."
            : "Fresh coffee. Nobody noticed. Tragic.", ToastKind.Info);
    }

    public void HeatFish()
    {
        ApplyPlayerAction(ConsequenceActions.MajorCrime, 0.7f, 1f);
        FlashCrime();
        WorldEvents.StinkActive = true;
        WorldEvents.StinkPos = new Vector3(22f, 0f, -15f);
        StinkTimer = 20f;
        PublishStimulus(NpcStimulusKind.Stink, WorldEvents.StinkPos, 1.2f, true,
            "Microwaved fish has filled the break room.", radius: 16f);
        Synth?.Alarm();
        Toast("FISH. MICROWAVED. The break room evacuates itself.", ToastKind.Chaos);
    }

    public void PullFireAlarm()
    {
        ApplyPlayerAction(ConsequenceActions.MajorCrime, 0.7f, 1f);
        FlashCrime();
        WorldEvents.Evacuating = true;
        WorldEvents.EvacPoint = new Vector3(0f, 0f, 17f);
        EvacTimer = 12f;
        AlarmCooldown = 90f;
        PublishStimulus(NpcStimulusKind.FireAlarm, WorldEvents.EvacPoint, 1.25f, true,
            "The fire alarm is ringing.", radius: 100f);
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
            PublishStimulus(NpcStimulusKind.MeetingPressure, n.Pos, 0.7f, true,
                $"A social directive sent {n.NpcName} to {result.DirectiveZone}.", radius: 14f, source: n);
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
        "meeting_a" => new Vector3(-25f, 0f, 18f),
        "meeting_b" => new Vector3(-12.5f, 0f, 18f),
        "hr" => new Vector3(13.5f, 0f, 18f),
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
        foreach (var o in Active.Objectives)
        {
            bool done = ObjectiveDone(o);
            s.Objectives.Add((ObjectiveLabel(o), done));
        }
        s.Stats = (Stats.Bonks, Stats.Hides, Stats.Reports, Stats.Disguises, Stats.Cleans);
        Hud.Push(s);
    }

    // ================= helpers =================

    private static StandardMaterial3D MakeMat(Color c) =>
        new() { AlbedoColor = c, Roughness = 0.9f };

    private static MeshInstance3D MakeBox(Vector3 size, Color c) =>
        new() { Mesh = new BoxMesh { Size = size }, MaterialOverride = MakeMat(c) };
}
