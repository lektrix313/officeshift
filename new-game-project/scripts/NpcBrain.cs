using Godot;
using System.Collections.Generic;

// ============================================================================
// AI context + director — ports game.ts updateNpcs()/updateGuard()/npc.ts
// state primitives. The director ticks ALL brains from GameMode each frame.
// ============================================================================

public sealed class AiContext
{
    public required Vector3 PlayerPos;
    public bool PlayerVisibleToAi = true;
    public bool PlayerCrouching;
    public bool PlayerCarrying;
    public string? PlayerDisguise;
    public string? PlayerDept;
    public float PlayerActivity;            // playerSusActivity() port: 0, 1, 1.5, 2.5 or 3
    public NpcBrain? Guard;
    public required List<NpcBrain> Npcs;
    public required World WorldRef;
    public required BloodSystem Blood;
    public double AlertTimer;
    public bool Evacuating;
    public BossDifficulty BossDifficulty = BossDifficulty.Standard;
    public Vector3 EvacPoint;
    public Vector3 BathroomPoint;
    public Vector3 CoffeePoint;
    public bool CoffeeSpiked;
    public bool StinkActive;
    public Vector3 StinkPos;
    public bool NoiseFresh;
    public Vector3 NoisePos;
    public required List<NpcStimulus> Stimuli;
    public float WorkdayElapsed;
    public SocialSimulation? Social;

    public Action<string, ToastKind>? Toast;
    public Action? AlarmSfx;
    public Func<NpcBrain, Vector3, float, bool>? CanSee;
    public Action<NpcBrain>? OnReportReachedGuard;
    public Action? OnPlayerCaught;
    public Action<string, OfficeObjectState, NpcBrain?>? SetObjectState;
    public GameMode? GameMode;
}

public partial class NpcBrain : Node
{
    public NpcBody Body { get; private set; } = null!;
    public string NpcName => Body.DisplayName;
    public Archetype Arch { get; private set; }
    public ArchetypeSpec Spec { get; private set; } = Specs.Table[Archetype.Drone];
    public PersonalityProfile Personality { get; private set; } = Personas.ProfileFor("unknown");
    public NpcStatSheet Stats { get; private set; } = new(Personas.ProfileFor("unknown"));
    public NpcAttitude Attitude { get; } = new();
    public string Zone { get; set; } = "drone";
    public string FloorId { get; private set; } = "floor-1";
    public bool IsChangingFloor { get; private set; }
    public float FloorTransitionTimer { get; private set; }
    public string? TargetFloorId { get; private set; }

    public void SetFloor(string floorId) => FloorId = string.IsNullOrWhiteSpace(floorId) ? "floor-1" : floorId;
    public void BeginFloorTransition(string targetFloor, float seconds = 2.5f)
    {
        TargetFloorId = targetFloor;
        IsChangingFloor = true;
        FloorTransitionTimer = seconds;
        Moving = false;
    }
    public bool FinishFloorTransition()
    {
        if (!IsChangingFloor || string.IsNullOrEmpty(TargetFloorId)) return false;
        FloorId = TargetFloorId;
        TargetFloorId = null;
        IsChangingFloor = false;
        FloorTransitionTimer = 0f;
        return true;
    }
    public void TickFloorTransition(float dt)
    {
        if (!IsChangingFloor) return;
        FloorTransitionTimer = MathF.Max(0f, FloorTransitionTimer - dt);
        if (FloorTransitionTimer <= 0f) FinishFloorTransition();
    }
    public WorkerProfile WorkProfile { get; private set; } = WorkerProfiles.For("unknown");
    public StaffGameplayProfile StaffProfile { get; private set; } = CanonicalStaff.For("unknown");
    public string Job => StaffProfile.Job;
    public string Department => StaffProfile.Department;
    public WorkdayMovementStyle MovementStyle => StaffProfile.Movement;
    public StaffObservationChannel PrimaryObservation => StaffProfile.PrimaryChannel;
    public StaffObservationChannel SecondaryObservation => StaffProfile.SecondaryChannel;

    public NpcState State { get; set; } = NpcState.Routine;
    public WorkdayState WorkState { get; internal set; } = WorkdayState.Arriving;
    public Vector3? WorkdayTarget { get; private set; }
    public float WorkdayOffset { get; private set; }
    public bool WorkdayOwnsRoutine { get; private set; }
    public float Suspicion { get; set; }
    public bool Looted { get; set; }
    public List<StaffMemory> Memories { get; } = new();
    public bool GossipSpreadDone { get; set; }
    public bool PoolSpawned { get; set; }
    public bool CreepToastDone { get; set; }
    public bool CrabToastDone { get; set; }
    public float DistractTimer { get; set; }
    public bool Talking { get; set; }
    public Vector3? DirectiveTarget { get; set; }
    public string? DirectiveZone { get; set; }
    public float DirectiveTimer { get; set; }
    public Vector3 HomePos { get; set; }
    public float BathroomTimer { get; set; }
    public bool StinkReacted { get; set; }
    public bool Disposed { get; set; }
    public bool Quit { get; set; }
    public float BlindedUntil { get; set; }
    public float SlipCooldownUntil { get; set; }
    public NpcNeeds Needs { get; } = new();
    public NpcActionChoice? LastAutonomousChoice { get; private set; }
    public float AutonomousActionTimer { get; private set; }

    public void ApplyAutonomousChoice(NpcActionChoice choice, Vector3 destination)
    {
        LastAutonomousChoice = choice;
        AutonomousActionTimer = choice.DurationSeconds;
        WorkdayTarget = destination;
        MoveTarget = destination;
        if (choice.Action == NpcAutonomousAction.Work)
            WorkState = WorkdayState.WorkingAtDesk;
        else if (choice.Action == NpcAutonomousAction.Coffee)
            WorkState = WorkdayState.CoffeeBreak;
        else if (choice.Action == NpcAutonomousAction.Snack)
            WorkState = WorkdayState.StationaryUse;
        else if (choice.Action == NpcAutonomousAction.Toilet)
            WorkState = WorkdayState.Toilet;
        else if (choice.Action == NpcAutonomousAction.Print)
            WorkState = WorkdayState.WalkingToPrinter;
        else if (choice.Action == NpcAutonomousAction.Gossip)
            WorkState = WorkdayState.WaterCooler;
        Body.SetWorkdayState(WorkState);
    }

    public void TickAutonomousAction(float dt)
    {
        AutonomousActionTimer = MathF.Max(0f, AutonomousActionTimer - dt);
    }

    // Consequence reaction telemetry/state. Cooldowns are per stimulus kind so a noisy
    // printer cannot suppress a separate body, alarm, or player-crime reaction.
    private readonly Dictionary<NpcStimulusKind, float> _stimulusCooldowns = new();
    public NpcStimulusKind? ActiveStimulus { get; private set; }
    public NpcReactionAction ReactionAction { get; private set; } = NpcReactionAction.Ignore;
    public float ReactionActivation { get; private set; }
    public float ReactionCooldownRemaining { get; private set; }
    public string ReactionText { get; private set; } = "unbothered";

    public bool Awake => State != NpcState.Out && State != NpcState.Hidden;
    public Vector3 Pos => Body.Position;

    // curiosity / panic bookkeeping (director-owned)
    public Vector3? InvestigateTarget;
    public object? InvestigateRef;
    public EvidenceKind? InvestigateKind;
    public object? PanicRef;
    public EvidenceKind? PanicKind;
    public float PanicTimer;
    public float PanicDuration;
    public int PanicShownSecond = -1;
    public Vector3? ReportTarget;
    public Vector3 LastSeenPlayer;
    public float LostSightTimer;
    public Vector3? LastSeenAreaHint { get; private set; }
    public float LastSeenAreaHintConfidence { get; private set; }
    public float LastSeenAreaHintTimer { get; private set; }
    public float BossDeskTimer { get; set; }

    public void ClearAreaHint() => LastSeenAreaHint = null;

    public void RememberAreaHint(Vector3 position, float confidence, float duration)
    {
        LastSeenAreaHint = position;
        LastSeenAreaHintConfidence = Util.Clamp(confidence, 0f, 1f);
        LastSeenAreaHintTimer = duration;
    }

    public void TickAreaHint(float dt)
    {
        LastSeenAreaHintTimer = System.MathF.Max(0f, LastSeenAreaHintTimer - dt);
        if (LastSeenAreaHintTimer <= 0f) LastSeenAreaHint = null;
    }

    public Vector3? MoveTarget;
    public float PauseTimer;
    public bool Moving;

    public static NpcBrain Create(NpcBody body, string zone)
    {
        var brain = new NpcBrain();
        brain.Body = body;
        brain.Arch = body.Arch;
        brain.Spec = body.Spec;
        brain.Personality = Personas.ProfileFor(body.DisplayName);
        brain.Stats = new NpcStatSheet(brain.Personality);
        brain.Zone = zone;
        brain.FloorId = "floor-1";
        brain.WorkProfile = CanonicalWorkdayProfiles.For(body.DisplayName);
        brain.StaffProfile = CanonicalStaff.For(body.DisplayName);
        brain.HomePos = body.Position;
        brain.Name = $"Brain_{body.DisplayName}";
        return brain;
    }

    /// <summary>Assigns a stable schedule phase so coworkers occupy different beats of the day.</summary>
    public void InitializeWorkday(int rosterIndex)
    {
        int hash = 17 + rosterIndex * 97;
        foreach (char c in NpcName) hash = unchecked(hash * 31 + c);
        WorkdayOffset = System.Math.Abs(hash % 3600);
        WorkdayOwnsRoutine = true;
        WorkState = WorkdayState.Arriving;
        Body.SetWorkdayState(WorkState);
    }

    /// <summary>Updates normal office activity; consequence NpcState values remain higher priority.</summary>
    public bool TryActivateStimulus(NpcStimulus stimulus, float now)
    {
        if (!Awake || Talking) return false;
        if (_stimulusCooldowns.TryGetValue(stimulus.Kind, out float readyAt) && now < readyAt)
            return false;

        float distance = Pos.DistanceTo(stimulus.Position);
        float stimulusRadius = System.MathF.Max(NpcStatBalance.MinimumStimulusRadius, stimulus.Radius);
        float proximity = distance < NpcStatBalance.MinimumStimulusRadius
            ? 1f
            : Util.Clamp(1f - distance / stimulusRadius, 0f, 1f);
        float attention = WorkdayAttentionMultiplier * Stats.Focus;
        float departmentMultiplier = string.IsNullOrEmpty(stimulus.ObjectDepartment)
            ? NpcStatBalance.NeutralDepartmentMultiplier
            : Stats.DepartmentMultiplier(stimulus.ObjectDepartment);
        float specialtyMultiplier = SpecialtyMultiplier(stimulus);
        float activation = stimulus.Intensity *
            (NpcStatBalance.ActivationBase + proximity * NpcStatBalance.ProximityWeight) *
            Stats.ActivationSensitivity *
            (NpcStatBalance.AttentionBase + attention * NpcStatBalance.AttentionWeight) *
            departmentMultiplier * specialtyMultiplier;
        if (stimulus.PlayerLed) activation *= NpcStatBalance.PlayerLedActivationMultiplier;
        ReactionActivation = Util.Clamp(activation, 0f, NpcStatBalance.ActivationCap);
        Stats.ApplyObjectEffect(stimulus.StressDelta, stimulus.ComfortDelta, ReactionActivation);
        float threshold = Stats.ActivationThreshold / System.MathF.Max(NpcStatBalance.FocusFloor, Stats.Focus);
        if (ReactionActivation < threshold)
        {
            _stimulusCooldowns[stimulus.Kind] = now + NpcStatBalance.BelowThresholdCooldown;
            ReactionCooldownRemaining = NpcStatBalance.BelowThresholdCooldown;
            ActiveStimulus = stimulus.Kind;
            ReactionAction = NpcReactionAction.Ignore;
            ReactionText = "ignores it";
            return false;
        }

        float cooldown = stimulus.Kind switch
        {
            NpcStimulusKind.PlayerCrime or NpcStimulusKind.BodyFound => NpcStatBalance.EvidenceReactionCooldown,
            NpcStimulusKind.BloodFound or NpcStimulusKind.PlayerNoise => NpcStatBalance.NoiseReactionCooldown,
            NpcStimulusKind.Stink or NpcStimulusKind.PrinterFailure or NpcStimulusKind.ObjectFailure => NpcStatBalance.FailureReactionCooldown,
            NpcStimulusKind.CoffeeBreak or NpcStimulusKind.PhoneCall or NpcStimulusKind.ComfortEvent => NpcStatBalance.ComfortReactionCooldown,
            _ => NpcStatBalance.DefaultReactionCooldown,
        } * Stats.ReactionCooldownMultiplier;
        _stimulusCooldowns[stimulus.Kind] = now + cooldown;
        ReactionCooldownRemaining = cooldown;
        ActiveStimulus = stimulus.Kind;
        ReactionAction = ChooseReaction(stimulus);
        ReactionText = ReactionLabel(ReactionAction);
        var attitude = stimulus.Kind switch
        {
            NpcStimulusKind.BodyFound or NpcStimulusKind.BloodFound or NpcStimulusKind.PlayerCrime =>
                AttitudeRules.For(ConsequenceActions.MajorCrime, stimulus.PlayerLed, ReactionAction == NpcReactionAction.Panic),
            NpcStimulusKind.ComfortEvent => NpcAttitudeKind.Grateful,
            NpcStimulusKind.Rumor => NpcAttitudeKind.Curious,
            NpcStimulusKind.AccessDenied or NpcStimulusKind.ObjectFailure => NpcAttitudeKind.Annoyed,
            _ => NpcAttitudeKind.Curious,
        };
        SetAttitude(attitude, Util.Clamp(ReactionActivation / NpcStatBalance.ActivationCap, 0.15f, 1f),
            stimulus.Kind == NpcStimulusKind.ComfortEvent ? 120f : ConsequenceBalance.DefaultAttitudeSeconds,
            stimulus.Description);
        return true;
    }

    private float SpecialtyMultiplier(NpcStimulus stimulus)
    {
        bool matches(StaffObservationChannel channel) => stimulus.Kind switch
        {
            NpcStimulusKind.AccessDenied => channel is StaffObservationChannel.IdentityAndVisitors or StaffObservationChannel.CalendarsAndAccess,
            NpcStimulusKind.PlayerCrime => channel is StaffObservationChannel.Numbers or StaffObservationChannel.Finance or StaffObservationChannel.VisualEvidence or StaffObservationChannel.Inventory or StaffObservationChannel.InconsistentStories,
            NpcStimulusKind.ObjectFailure or NpcStimulusKind.ITCalled => channel is StaffObservationChannel.Technology or StaffObservationChannel.NetworkPatterns or StaffObservationChannel.MaintenanceAndBackRoutes,
            NpcStimulusKind.MeetingPressure => channel is StaffObservationChannel.MeetingsAndTime or StaffObservationChannel.CalendarsAndAccess,
            NpcStimulusKind.Rumor => channel is StaffObservationChannel.GossipDrive or StaffObservationChannel.HumanResources or StaffObservationChannel.InstitutionalMemory,
            NpcStimulusKind.BodyFound or NpcStimulusKind.BloodFound => channel is StaffObservationChannel.PanicAndRumor or StaffObservationChannel.HumanResources or StaffObservationChannel.MaintenanceAndBackRoutes,
            _ => false,
        };
        return matches(PrimaryObservation) ? 1.3f : matches(SecondaryObservation) ? 1.15f : 1f;
    }

    public void SetAttitude(NpcAttitudeKind kind, float strength, float duration, string source)
    {
        Attitude.Set(kind, strength, duration * Personality.PanicDurationMultiplier, source);
    }

    public void RecoverAttitude(float multiplier = 1f) => Attitude.Recover(multiplier);

    public void TickAttitude(float dt)
    {
        float recovery = WorkdayAttentionMultiplier > 0.8f
            ? ConsequenceBalance.AttitudeDecayPerSecond
            : ConsequenceBalance.AttitudeDecayPerSecond * 1.25f;
        Attitude.Tick(dt, recovery);
    }

    public void TickReactionCooldowns(float dt)
    {
        ReactionCooldownRemaining = System.MathF.Max(0f, ReactionCooldownRemaining - dt);
        if (_stimulusCooldowns.Count == 0) return;
        var expired = new List<NpcStimulusKind>();
        foreach (var pair in _stimulusCooldowns)
            if (pair.Value <= AiDirector.Now) expired.Add(pair.Key);
        foreach (var kind in expired) _stimulusCooldowns.Remove(kind);
    }

    public void SetReactionDestination(Vector3 target, NpcReactionAction action, float seconds = 8f)
    {
        State = NpcState.Routine;
        DirectiveZone = null;
        DirectiveTarget = target;
        DirectiveTimer = seconds;
        MoveTarget = target;
        PauseTimer = 0f;
        Moving = false;
        Body.ShowEmote(ReactionText);
    }

    private NpcReactionAction ChooseReaction(NpcStimulus stimulus)
    {
        bool technologyFailure = stimulus.ObjectType is OfficeObjectType.Computer or
            OfficeObjectType.ServerTerminal or OfficeObjectType.ServerRack;
        if (technologyFailure && (stimulus.Kind is NpcStimulusKind.ObjectFailure or NpcStimulusKind.ITCalled ||
                                  stimulus.PreferredAction == NpcReactionAction.SeekHelp))
        {
            return Stats.ITAffinity < NpcStatBalance.AntiITAffinityThreshold
                ? NpcReactionAction.Complain
                : NpcReactionAction.SeekHelp;
        }
        if (stimulus.ObjectType.HasValue && stimulus.PreferredAction != NpcReactionAction.Observe)
            return stimulus.PreferredAction;
        if (stimulus.Kind is NpcStimulusKind.BodyFound or NpcStimulusKind.BloodFound)
        {
            if (Personality.Neuroticism > 0.78f && ReactionActivation > 1.15f)
                return NpcReactionAction.Panic;
            return Spec.Reports && ReactionActivation > 0.9f ? NpcReactionAction.Report : NpcReactionAction.Investigate;
        }
        if (stimulus.Kind == NpcStimulusKind.PlayerCrime)
        {
            if (Arch == Archetype.Grifter && Personality.Agreeableness < 0.5f) return NpcReactionAction.Complain;
            return ReactionActivation > 1.1f && Spec.Reports ? NpcReactionAction.Report : NpcReactionAction.Observe;
        }
        if (stimulus.Kind is NpcStimulusKind.Stink or NpcStimulusKind.FireAlarm)
            return NpcReactionAction.Flee;
        if (stimulus.Kind is NpcStimulusKind.CoffeeBreak or NpcStimulusKind.PhoneCall)
            return Stats.SocialMultiplier > 0.65f ? NpcReactionAction.GoToCoffee : NpcReactionAction.Observe;
        if (stimulus.Kind is NpcStimulusKind.PrinterFailure or NpcStimulusKind.ObjectFailure)
            return stimulus.ObjectType == OfficeObjectType.Computer && Stats.ITAffinity < NpcStatBalance.AntiITAffinityThreshold
                ? NpcReactionAction.Complain
                : Personality.Neuroticism > 0.6f ? NpcReactionAction.Complain : NpcReactionAction.Investigate;
        if (stimulus.Kind == NpcStimulusKind.ITCalled)
            return Stats.ITAffinity < NpcStatBalance.AntiITAffinityThreshold ? NpcReactionAction.Complain : NpcReactionAction.SeekHelp;
        if (stimulus.Kind == NpcStimulusKind.AccessDenied)
            return Stats.Patience < 0.45f ? NpcReactionAction.Complain : NpcReactionAction.SeekHelp;
        if (stimulus.Kind == NpcStimulusKind.ComfortEvent)
            return NpcReactionAction.Recover;
        if (stimulus.Kind == NpcStimulusKind.MeetingPressure)
            return Stats.SocialMultiplier > 0.45f ? NpcReactionAction.GoToMeeting : NpcReactionAction.Complain;
        if (stimulus.Kind == NpcStimulusKind.Rumor)
            return Personality.Extraversion > 0.65f ? NpcReactionAction.Gossip : NpcReactionAction.Observe;
        return ReactionActivation > 1f ? NpcReactionAction.Investigate : NpcReactionAction.Observe;
    }

    private static string ReactionLabel(NpcReactionAction action) => action switch
    {
        NpcReactionAction.Observe => "watches closely",
        NpcReactionAction.Investigate => "goes to investigate",
        NpcReactionAction.Panic => "panics",
        NpcReactionAction.Report => "reports it",
        NpcReactionAction.Flee => "flees",
        NpcReactionAction.GoToCoffee => "heads to coffee",
        NpcReactionAction.GoToPrinter => "goes to the printer",
        NpcReactionAction.GoToMeeting => "heads to the meeting",
        NpcReactionAction.Gossip => "starts gossiping",
        NpcReactionAction.Complain => "complains",
        NpcReactionAction.SeekHelp => "calls the responsible department",
        NpcReactionAction.Recover => "feels better",
        NpcReactionAction.UseObject => "uses it",
        _ => "ignores it",
    };

    public void UpdateWorkday(float shiftElapsed, AiContext ctx)
    {
        if (!WorkdayOwnsRoutine) return;
        if (State == NpcState.Seated && shiftElapsed > 20f)
        {
            State = NpcState.Routine;
            Body.ShowSleeping(false);
        }
        float clock = (shiftElapsed + WorkdayOffset) % Bal.ShiftSeconds;
        float hour = WorkdayBalance.WorkdayStartHour + clock / (Bal.ShiftSeconds / (WorkdayBalance.WorkdayEndHour - WorkdayBalance.WorkdayStartHour));
        var authoredBeat = WorkProfile.Beats.FirstOrDefault(beat => hour >= beat.StartHour && hour < beat.EndHour);
        var next = authoredBeat?.State ?? DefaultWorkdayState(clock);
        if (next != WorkState)
        {
            WorkState = next;
            WorkdayTarget = null;
            MoveTarget = null;
            PauseTimer = 0f;
            Body.SetWorkdayState(next);
        }
        // Floor transition: if a beat specifies a different floor, initiate elevator/stair travel
        if (authoredBeat?.FloorId != null && !authoredBeat.FloorId.Equals(FloorId, StringComparison.OrdinalIgnoreCase) && !IsChangingFloor)
        {
            ctx.GameMode?.TryMoveNpcToFloor(this, authoredBeat.FloorId);
        }
        WorkdayTarget = authoredBeat != null
            ? AuthoredWorkdayPoint(authoredBeat, ctx)
            : WorkdayPoint(ctx);
    }

    private WorkdayState DefaultWorkdayState(float clock)
    {
        float share = WorkProfile.DeskShare;
        float normalized = clock / Bal.ShiftSeconds;
        if (MovementStyle == WorkdayMovementStyle.SnackSeeker && normalized > 0.2f && normalized < 0.85f)
            return WorkdayState.StationaryUse;
        if (MovementStyle == WorkdayMovementStyle.CoffeeSeeker && normalized > 0.15f && normalized < 0.9f)
            return WorkdayState.CoffeeBreak;
        if (MovementStyle == WorkdayMovementStyle.SocialButterfly && normalized > 0.25f && normalized < 0.8f)
            return WorkdayState.WaterCooler;
        if (MovementStyle == WorkdayMovementStyle.DeskAnchor && share > 0.75f)
            return WorkdayState.WorkingAtDesk;
        return WorkdaySchedule[(int)(clock / WorkdaySlotSeconds) % WorkdaySchedule.Length];
    }

    private Vector3 AuthoredWorkdayPoint(WorkdayBeat beat, AiContext ctx)
    {
        if (beat.Destination == "desk") return HomePos;
        if (beat.Destination == "break") return ctx.CoffeePoint;
        if (beat.Destination == "printer") return new Vector3(-27f, 0f, -10.5f);
        if (beat.Destination == "server") return new Vector3(-22f, 0f, -18f);
        if (beat.Destination == "closet") return new Vector3(26f, 0f, 11f);
        if (beat.Destination == "reception") return new Vector3(0f, 0f, 17f);
        var points = ctx.WorldRef.WaypointsFor(beat.Destination);
        return points.Length > 0 ? points[0] : WorkdayPoint(ctx);
    }

    public bool WorkdayNeedsMovement => WorkState is
        WorkdayState.Arriving or WorkdayState.WalkingToPrinter or WorkdayState.WaitingAtPrinter or
        WorkdayState.Printing or WorkdayState.PrinterBroken or WorkdayState.Toilet or WorkdayState.OnBreak or
        WorkdayState.CoffeeBreak or WorkdayState.MeetingWalk or WorkdayState.Meeting or
        WorkdayState.AnxiousMeeting or WorkdayState.PhoneCall or WorkdayState.WaterCooler or WorkdayState.WalkingThinking or
        WorkdayState.AnxiousWalking;

    public bool WorkdayDistracted => WorkState is
        WorkdayState.DoomScrolling or WorkdayState.NotPayingAttention or WorkdayState.FeelingDrunk or
        WorkdayState.Stoned or WorkdayState.LSD or WorkdayState.KHole;

    public float WorkdayAttentionMultiplier => WorkState switch
    {
        WorkdayState.EngrossedWorking or WorkdayState.FeelingCurious => 1.2f,
        WorkdayState.WorkingAtDesk or WorkdayState.PickingUpSlack or WorkdayState.SuspiciousWorking => 1f,
        WorkdayState.DoomScrolling or WorkdayState.NotPayingAttention or WorkdayState.FeelingSleepy => 0.2f,
        WorkdayState.FeelingDrunk or WorkdayState.Stoned or WorkdayState.LSD or WorkdayState.KHole => 0.1f,
        WorkdayState.PanicAttack => 0f,
        _ => 0.7f,
    };

    public float WorkdaySpeedMultiplier => WorkState switch
    {
        WorkdayState.Speed or WorkdayState.Ecstasy => 1.55f,
        WorkdayState.Stoned or WorkdayState.LSD or WorkdayState.KHole => 0.45f,
        WorkdayState.FeelingDrunk => 0.7f,
        WorkdayState.AnxiousWalking or WorkdayState.PanicAttack => 1.25f,
        _ => 1f,
    };

    public float WorkdaySuspicionMultiplier => WorkState switch
    {
        WorkdayState.SuspiciousWorking or WorkdayState.WorriedWorking or WorkdayState.AnxiousWorking => 1.35f,
        WorkdayState.DoomScrolling or WorkdayState.NotPayingAttention or WorkdayState.Stoned => 0.35f,
        WorkdayState.DepressedWorking or WorkdayState.FeelingSleepy => 0.6f,
        WorkdayState.FeelingCurious or WorkdayState.EngrossedWorking => 1.15f,
        _ => 1f,
    } * (1f + Stats.CurrentStress * NpcStatBalance.StressActivationMultiplier -
        Stats.CurrentComfort * NpcStatBalance.ComfortActivationReduction);

    private Vector3 WorkdayPoint(AiContext ctx)
    {
        if (WorkState is WorkdayState.WalkingToPrinter or WorkdayState.WaitingAtPrinter or WorkdayState.Printing or WorkdayState.PrinterBroken)
            return new Vector3(-27f, 0f, -10.5f);

        if (WorkState == WorkdayState.Toilet) return ctx.BathroomPoint;

        if (WorkState is WorkdayState.OnBreak or WorkdayState.CoffeeBreak)
            return ctx.CoffeePoint;

        if (WorkState is WorkdayState.MeetingWalk or WorkdayState.Meeting or WorkdayState.AnxiousMeeting)
        {
            bool alternate = (NpcName.Length + (int)WorkdayOffset) % 2 == 0;
            var points = ctx.WorldRef.WaypointsFor(alternate ? "meeting_a" : "meeting_b");
            return points.Length > 0 ? points[0] : HomePos;
        }

        if (WorkState == WorkdayState.PhoneCall)
        {
            var phones = WorldData.Phones;
            if (phones.Length == 0) return HomePos;
            int index = (NpcName.Length + (int)WorkdayOffset) % phones.Length;
            return new Vector3(phones[index].X, 0f, phones[index].Z);
        }

        if (WorkState == WorkdayState.WaterCooler)
            return new Vector3(14f, 0f, -20.8f);

        if (WorkState is WorkdayState.WalkingThinking or WorkdayState.AnxiousWalking)
        {
            var points = ctx.WorldRef.WaypointsFor(Zone);
            int index = points.Length == 0 ? 0 : (int)(WorkdayOffset % points.Length);
            return points.Length > 0 ? points[index] : HomePos;
        }

        // Desk, phone-use, mood, substance, and reading states hold their home point.
        return HomePos;
    }

    private static float WorkdaySlotSeconds => Bal.ShiftSeconds / WorkdaySchedule.Length;
    private static readonly WorkdayState[] WorkdaySchedule =
    {
        WorkdayState.Arriving,
        WorkdayState.WorkingAtDesk,
        WorkdayState.WorkingAtDesk,
        WorkdayState.WalkingToPrinter,
        WorkdayState.WaitingAtPrinter,
        WorkdayState.Printing,
        WorkdayState.PrinterBroken,
        WorkdayState.Toilet,
        WorkdayState.WorkingAtDesk,
        WorkdayState.OnBreak,
        WorkdayState.WaterCooler,
        WorkdayState.CoffeeBreak,
        WorkdayState.StationaryUse,
        WorkdayState.MeetingWalk,
        WorkdayState.Meeting,
        WorkdayState.AnxiousMeeting,
        WorkdayState.Meeting,
        WorkdayState.PhoneCall,
        WorkdayState.DoomScrolling,
        WorkdayState.NotPayingAttention,
        WorkdayState.EngrossedWorking,
        WorkdayState.Reading,
        WorkdayState.HappyWorking,
        WorkdayState.WorriedWorking,
        WorkdayState.DistractedWorking,
        WorkdayState.AnnoyedWorking,
        WorkdayState.PickingUpSlack,
        WorkdayState.SuspiciousWorking,
        WorkdayState.DepressedWorking,
        WorkdayState.WalkingThinking,
        WorkdayState.FeelingSick,
        WorkdayState.FeelingHorny,
        WorkdayState.FeelingCurious,
        WorkdayState.FeelingSleepy,
        WorkdayState.FeelingDrunk,
        WorkdayState.Speed,
        WorkdayState.Stoned,
        WorkdayState.LSD,
        WorkdayState.KHole,
        WorkdayState.Ecstasy,
        WorkdayState.AnxiousWalking,
        WorkdayState.AnxiousWorking,
        WorkdayState.PanicAttack,
        WorkdayState.WorkingAtDesk,
        WorkdayState.WorkingAtDesk,
        WorkdayState.WorkingAtDesk,
        WorkdayState.WorkingAtDesk,
    };

    // ---------- state primitives (port of npc.ts) ----------

    public void AddSuspicion(float amount)
    {
        if (!Awake) return;
        Suspicion = System.MathF.Min(100f, Suspicion + amount * Personality.SuspicionSensitivity);
    }

    public void Remember(StaffMemory memory)
    {
        var existing = Memories.FirstOrDefault(m => m.Incident == memory.Incident && m.Subject == memory.Subject);
        if (existing != null)
        {
            existing.Confidence = System.MathF.Min(100f, existing.Confidence + memory.Confidence * 0.35f);
            existing.Age = 0f;
            return;
        }
        Memories.Add(memory);
        while (Memories.Count > 8) Memories.RemoveAt(0);
    }

    public void TickMemories(float dt)
    {
        foreach (var memory in Memories)
        {
            memory.Age += dt;
            memory.Confidence = System.MathF.Max(0f, memory.Confidence - dt * (memory.Kind == MemoryKind.Witness ? 0.12f : 0.2f));
        }
        Memories.RemoveAll(m => m.Confidence < 5f);
    }

    public void KnockOut(Vector3 flopDir)
    {
        State = NpcState.Out;
        Suspicion = 0;
        InvestigateTarget = null;
        InvestigateRef = null;
        InvestigateKind = null;
        PanicRef = null;
        PanicTimer = 0;
        PoolSpawned = false;
        Body.ClearEmote();
        ShowBang(false);
        Moving = false;

        // Preferred: physics crumple via factory; fallback: baked lying pose.
        Node? rd = RagdollFactory.Spawn(Body, flopDir, null);
        Body.ActiveRagdoll = rd;
        if (rd == null) Body.PlayKnockoutPose();
        Body.ShowSleeping(true);
    }

    public void ClearRagdoll()
    {
        RagdollFactory.Clear(Body);
        Body.ActiveRagdoll = null;
    }

    public void StartCurious(Vector3 target, object evidenceRef, EvidenceKind kind)
    {
        State = NpcState.Curious;
        InvestigateTarget = target;
        InvestigateRef = evidenceRef;
        InvestigateKind = kind;
        Body.ShowEmote("?");
    }

    public void StartPanic(EvidenceKind kind, object evidenceRef, float duration)
    {
        State = NpcState.Panic;
        PanicKind = kind;
        PanicRef = evidenceRef;
        PanicTimer = duration * Personality.PanicDurationMultiplier;
        PanicDuration = PanicTimer;
        PanicShownSecond = -1;
        Moving = false;
    }

    /// <summary>Ticks the countdown with per-second "!!{N}" emote; true when expired.</summary>
    public bool UpdatePanicTick(double dt)
    {
        PanicTimer -= (float)dt;
        int sec = System.Math.Max(0, (int)System.MathF.Ceiling(PanicTimer));
        if (sec != PanicShownSecond)
        {
            PanicShownSecond = sec;
            Body.ShowEmote(sec > 0 ? $"!!{sec}" : "!!");
        }
        return PanicTimer <= 0;
    }

    public void ShrugItOff(float minSus = 0f)
    {
        State = NpcState.Routine;
        InvestigateTarget = null;
        InvestigateRef = null;
        InvestigateKind = null;
        PanicRef = null;
        PanicKind = null;
        PanicTimer = 0;
        MoveTarget = null;
        PauseTimer = 2f;
        Body.ClearEmote();
        if (Suspicion < minSus) Suspicion = minSus * Personality.ForgivenessMultiplier;
    }

    public void StartReport(Vector3 guardPos)
    {
        State = NpcState.Report;
        ReportTarget = guardPos;
        Body.ClearEmote();
        ShowBang(true);
    }

    public void CalmDown()
    {
        ShrugItOff();
        Suspicion = 30;
        ShowBang(false);
    }

    private Label3D? _bang;
    private void ShowBang(bool on)
    {
        if (on && _bang == null)
        {
            _bang = new Label3D
            {
                Text = "!!",
                FontSize = 64,
                OutlineSize = 14,
                Modulate = Color.FromHtml("ff5a4a"),
                Position = new Vector3(0f, 2.75f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.005f,
            };
            Body.AddChild(_bang);
        }
        else if (!on && _bang != null)
        {
            _bang.QueueFree();
            _bang = null;
        }
    }

    // ---------- movement (port of stepToward) ----------

    /// <summary>Move toward target on XZ with collision pushout. True on arrival.</summary>
    public bool StepToward(World world, Vector3 target, double dt, float? speedOverride = null)
    {
        float speed = speedOverride ?? Spec.Speed;
        Vector3 pos = Body.Position;
        float dx = target.X - pos.X;
        float dz = target.Z - pos.Z;
        float dist = System.MathF.Sqrt(dx * dx + dz * dz);
        if (dist < Bal.ArrivalDist)
        {
            Moving = false;
            return true;
        }
        float step = System.MathF.Min(dist, speed * (float)dt);
        pos.X += dx / dist * step;
        pos.Z += dz / dist * step;
        world.ResolveCircle(ref pos, Bal.NpcRadius);
        Body.Position = pos;

        Moving = true;
        float desired = System.MathF.Atan2(dx, dz);
        float diff = desired - Body.Facing;
        while (diff > System.MathF.PI) diff -= System.MathF.Tau;
        while (diff < -System.MathF.PI) diff += System.MathF.Tau;
        Body.Facing += diff * System.MathF.Min(1f, (float)dt * 8f);
        return false;
    }
}

public static class AiDirector
{
    /// <summary>Director clock (seconds), used for blinded/slip cooldowns.</summary>
    public static float Now;

    /// <summary>Read by GameMode after Tick().</summary>
    public static class Outputs
    {
        public static float MaxSus;
        public static bool Watched;
    }

    /// <summary>Port of game.ts updateNpcs().</summary>
    public static void Tick(List<NpcBrain> npcs, AiContext ctx, double dt)
    {
        Now += (float)dt;
        float maxSus = 0f;
        foreach (var n in npcs)
        {
            n.TickMemories((float)dt);
            n.TickReactionCooldowns((float)dt);
            n.TickAreaHint((float)dt);
            n.TickFloorTransition((float)dt);
            n.Stats.Recover((float)dt);
        }
        bool watched = false;
        ProcessStimuli(npcs, ctx);

        foreach (var n in npcs)
        {
            if (n == ctx.Guard || !n.Awake) continue;
            WorkdayState before = n.WorkState;
            n.UpdateWorkday(ctx.WorkdayElapsed, ctx);
            if (before != n.WorkState)
            {
                NpcStimulusKind? kind = n.WorkState switch
                {
                    WorkdayState.WalkingToPrinter or WorkdayState.Printing => NpcStimulusKind.WorkdayActivity,
                    WorkdayState.PrinterBroken => NpcStimulusKind.PrinterFailure,
                    WorkdayState.Meeting or WorkdayState.AnxiousMeeting => NpcStimulusKind.MeetingPressure,
                    WorkdayState.PhoneCall => NpcStimulusKind.PhoneCall,
                    WorkdayState.OnBreak or WorkdayState.CoffeeBreak or WorkdayState.WaterCooler => NpcStimulusKind.CoffeeBreak,
                    _ => null,
                };
                if (kind.HasValue)
                {
                    ctx.Stimuli.Add(new NpcStimulus
                    {
                        Id = $"workday:{n.NpcName}:{ctx.WorkdayElapsed:F1}:{kind.Value}",
                        Kind = kind.Value,
                        Position = n.WorkdayTarget ?? n.Pos,
                        Intensity = kind == NpcStimulusKind.PrinterFailure ? 1.1f : 0.55f,
                        Description = $"{n.NpcName} is {n.WorkState}",
                        Source = n,
                    });
                    if (n.WorkState is WorkdayState.WalkingToPrinter or WorkdayState.Printing)
                        ctx.SetObjectState?.Invoke("printer", OfficeObjectState.InUse, n);
                    else if (n.WorkState == WorkdayState.PrinterBroken)
                        ctx.SetObjectState?.Invoke("printer", OfficeObjectState.Jammed, n);
                }
            }

            // --- laughing at your photocopied face ---
            if (n.DistractTimer > 0 && n.State != NpcState.Report)
            {
                n.DistractTimer -= (float)dt;
                if (n.DistractTimer <= 0) n.Body.ClearEmote();
                continue; // too busy laughing to notice anything
            }

            // --- curiosity: walking over to inspect evidence ---
            if (n.State == NpcState.Curious)
            {
                bool gone = n.InvestigateKind switch
                {
                    EvidenceKind.Body => n.InvestigateRef is not NpcBrain b || b.State != NpcState.Out || !b.Body.Visible,
                    EvidenceKind.Noise => n.InvestigateRef is not NoiseRef nr || nr.Expired,
                    _ => n.InvestigateRef is not BloodSystem.Splat s || !ctx.Blood.Contains(s),
                };
                if (gone)
                {
                    n.ShrugItOff();
                    n.AddSuspicion(Bal.VanishInvestigateSus);
                    ctx.Toast?.Invoke($"{n.NpcName}: \"Huh. Could've sworn I saw… something.\"", ToastKind.Info);
                }
                else if (n.InvestigateTarget.HasValue &&
                         n.StepToward(ctx.WorldRef, n.InvestigateTarget.Value, dt, n.Spec.Speed * Bal.CuriousSpeedMul))
                {
                    if (n.InvestigateKind == EvidenceKind.Noise)
                    {
                        // poked around the noise: nothing there, back to work
                        n.ShrugItOff(8f);
                        ctx.Toast?.Invoke($"{n.NpcName} pokes around. \"Probably just the building settling.\"", ToastKind.Info);
                    }
                    else
                    {
                        var kind = n.InvestigateKind!.Value;
                        float duration = kind == EvidenceKind.Body ? Bal.PanicDurationBody : Bal.PanicDurationBlood;
                        n.StartPanic(kind, n.InvestigateRef!, duration);
                        if (kind == EvidenceKind.Body && n.InvestigateRef is NpcBrain victim)
                            ctx.Toast?.Invoke($"{n.NpcName} found {victim.NpcName}'s body!! Security in {(int)duration}s — unless someone stops them.", ToastKind.Chaos);
                        else
                            ctx.Toast?.Invoke($"{n.NpcName} is staring at a pool of blood. Squealing in {(int)duration}s — mop it or stop them.", ToastKind.Warn);
                        ctx.AlarmSfx?.Invoke();
                    }
                }
                maxSus = System.MathF.Max(maxSus, n.Suspicion);
                PushVisual(n, dt);
                continue; // focused on the evidence, not on you
            }

            // --- panic: countdown until they run to security ---
            if (n.State == NpcState.Panic)
            {
                bool gone = n.PanicKind == EvidenceKind.Body
                    ? n.PanicRef is not NpcBrain pb || pb.State != NpcState.Out || !pb.Body.Visible
                    : n.PanicRef is not BloodSystem.Splat ps || !ctx.Blood.Contains(ps);
                if (gone)
                {
                    n.ShrugItOff(Bal.VanishPanicSus);
                    ctx.Toast?.Invoke($"{n.NpcName} looks again — nothing there. \"…Not paid enough for this.\"", ToastKind.Info);
                }
                else if (n.UpdatePanicTick(dt))
                {
                    if (n.Arch == Archetype.Grifter)
                    {
                        n.ShrugItOff();
                        n.Suspicion = 20;
                        ctx.Toast?.Invoke($"{n.NpcName} saw everything… and wants a cut. You gained an accomplice.", ToastKind.Success);
                    }
                    else if (n.Spec.Reports)
                    {
                        if (ctx.Guard is { } g && g.Awake)
                        {
                            n.StartReport(g.Pos);
                            ctx.Toast?.Invoke($"{n.NpcName} is RUNNING to security! Intercept or improvise!", ToastKind.Warn);
                            ctx.AlarmSfx?.Invoke();
                        }
                        else
                        {
                            ctx.Toast?.Invoke($"{n.NpcName} ran to tell security… but security is currently a floor lamp.", ToastKind.Chaos);
                            n.CalmDown();
                        }
                    }
                }
                maxSus = System.MathF.Max(maxSus, n.Suspicion);
                PushVisual(n, dt);
                continue;
            }

            // --- movement / behaviour ---
            if (n.State == NpcState.Report)
            {
                if (n.ReportTarget.HasValue &&
                    n.StepToward(ctx.WorldRef, n.ReportTarget.Value, dt, n.Spec.Speed * Bal.ReportSpeedMul))
                {
                    ctx.OnReportReachedGuard?.Invoke(n);
                }
            }
            else if (n.State == NpcState.Seated)
            {
                n.Moving = false; // sweet dreams
            }
            else if (n.State == NpcState.Routine)
            {
                if (n.Talking)
                {
                    n.Moving = false;
                }
                else if (ctx.Evacuating)
                {
                    n.MoveTarget = ctx.EvacPoint;
                    if (n.StepToward(ctx.WorldRef, ctx.EvacPoint, dt, n.Spec.Speed * 1.5f))
                        n.Moving = false;
                }
                else if (n.BathroomTimer > 0f)
                {
                    n.BathroomTimer -= (float)dt;
                    if (n.Pos.DistanceTo(ctx.BathroomPoint) > 1.1f)
                        n.StepToward(ctx.WorldRef, ctx.BathroomPoint, dt, n.Spec.Speed * n.WorkdaySpeedMultiplier);
                    else
                        n.Moving = false;
                }
                else if (n.DirectiveTimer > 0f)
                {
                    n.DirectiveTimer -= (float)dt;
                    if (n.DirectiveTarget.HasValue)
                    {
                        bool arrived = n.StepToward(ctx.WorldRef, n.DirectiveTarget.Value, dt,
                            n.Spec.Speed * n.WorkdaySpeedMultiplier);
                        if (arrived)
                        {
                            n.Moving = false;
                            if (ctx.CoffeeSpiked && WorldEvents.SpikeUsesLeft > 0 &&
                                n.Pos.DistanceTo(ctx.CoffeePoint) < 1.5f)
                            {
                                WorldEvents.SpikeUsesLeft--;
                                n.BathroomTimer = 14f;
                                n.DirectiveTimer = 0f;
                                n.DirectiveTarget = null;
                                ctx.Toast?.Invoke($"{n.NpcName} downs the coffee. Somewhere, a timer starts.", ToastKind.Chaos);
                            }
                        }
                    }
                    if (n.DirectiveTimer <= 0f)
                    {
                        n.DirectiveTarget = null;
                        n.PauseTimer = 0.5f;
                    }
                }
                else if (ctx.StinkActive && n.Pos.DistanceTo(ctx.StinkPos) < 9f)
                {
                    // flee to the farthest known waypoint from the stink
                    var pts = ctx.WorldRef.WaypointsFor(n.Zone);
                    var flee = n.Pos;
                    float bestD = -1f;
                    foreach (var pt in pts)
                    {
                        float d = pt.DistanceTo(ctx.StinkPos);
                        if (d > bestD) { bestD = d; flee = pt; }
                    }
                    n.StepToward(ctx.WorldRef, flee, dt, n.Spec.Speed * 1.2f);
                    if (!n.StinkReacted)
                    {
                        n.StinkReacted = true;
                        n.Body.ShowEmote(":(");
                        ctx.Toast?.Invoke($"{n.NpcName} gags. Someone microwaved FISH.", ToastKind.Chaos);
                    }
                }
                else if (n.WorkdayNeedsMovement && n.WorkdayTarget.HasValue)
                {
                    n.MoveTarget = n.WorkdayTarget;
                    n.StepToward(ctx.WorldRef, n.WorkdayTarget.Value, dt,
                        n.Spec.Speed * n.WorkdaySpeedMultiplier);
                }
                else if (n.WorkdayOwnsRoutine)
                {
                    // Desk, reading, phone, and mood states hold their work point.
                    n.Moving = false;
                    n.MoveTarget = null;
                }
                else if (n.PauseTimer > 0)
                {
                    n.PauseTimer -= (float)dt;
                    n.Moving = false;
                }
                else if (!n.MoveTarget.HasValue || n.StepToward(ctx.WorldRef, n.MoveTarget.Value, dt))
                {
                    var pts = ctx.WorldRef.WaypointsFor(n.Zone);
                    if (pts.Length > 0)
                        n.MoveTarget = pts[(int)(GD.RandRange(0, pts.Length - 1))];
                    n.PauseTimer = 1.5f + (float)GD.RandRange(0.0, 4.0);
                }
            }

            // --- perception ---
            if (n.State != NpcState.Seated && !n.Talking && !ctx.Evacuating && n.BlindedUntil <= Now)
            {
                bool seesPlayer = ctx.CanSee?.Invoke(n, ctx.PlayerPos, 1f) ?? false;
                float playerDist = n.Pos.DistanceTo(ctx.PlayerPos);
                float disguiseMul = ctx.PlayerDisguise != null ? Bal.SusDisguiseMul : 1f;

                if (seesPlayer && ctx.PlayerActivity > 0)
                {
                    watched = true;
                    if (n.WorkdayDistracted && n.WorkdayAttentionMultiplier < 0.2f)
                    {
                        n.LastSeenPlayer = ctx.PlayerPos;
                    }
                    else
                    {
                        n.AddSuspicion(ctx.PlayerActivity * n.Spec.Rate * Bal.SusGainScale * disguiseMul * n.WorkdaySuspicionMultiplier * n.WorkdayAttentionMultiplier * (float)dt);
                    }
                    n.LastSeenPlayer = ctx.PlayerPos;
                }
                else if (seesPlayer && ctx.PlayerActivity == 0)
                {
                    float deptMul = ctx.PlayerDept == "Sales" ? 0.5f : 1f; // nobody questions Sales
                    if (playerDist < Bal.CreepRange)
                    {
                        n.AddSuspicion(Bal.CreepRate * n.Spec.Rate * disguiseMul * deptMul * n.WorkdaySuspicionMultiplier * n.WorkdayAttentionMultiplier * (float)dt);
                        if (!n.CreepToastDone && n.Suspicion > 15)
                        {
                            n.CreepToastDone = true;
                            ctx.Toast?.Invoke($"{n.NpcName}: \"Do I… know you? You're standing VERY close.\"", ToastKind.Warn);
                        }
                    }
                    else if (ctx.PlayerCrouching && playerDist < Bal.CrabRange)
                    {
                        n.AddSuspicion(Bal.CrabRate * n.Spec.Rate * disguiseMul * deptMul * n.WorkdaySuspicionMultiplier * n.WorkdayAttentionMultiplier * (float)dt);
                        if (!n.CrabToastDone && n.Suspicion > 12)
                        {
                            n.CrabToastDone = true;
                            ctx.Toast?.Invoke($"{n.NpcName} is wondering why you're crab-walking past the cubicles.", ToastKind.Warn);
                        }
                    }
                }
                else if (n.Suspicion > 0 && n.Suspicion < 50)
                {
                    n.Suspicion = System.MathF.Max(0f, n.Suspicion - Bal.SusDecayPerSec * (float)dt);
                }

                // --- noticing evidence from afar ---
                if (n.State == NpcState.Routine)
                {
                    bool spotted = false;
                    foreach (var b in npcs)
                    {
                        if (b == n || b.State != NpcState.Out || !b.Body.Visible) continue;
                        if (ctx.CanSee?.Invoke(n, b.Pos, 1f) ?? false)
                        {
                            ctx.Stimuli.Add(new NpcStimulus
                            {
                                Id = $"body-seen:{n.NpcName}:{b.NpcName}:{Now:F1}",
                                Kind = NpcStimulusKind.BodyFound,
                                Position = b.Pos,
                                Intensity = 1.1f,
                                EvidenceRef = b,
                                EvidenceKind = EvidenceKind.Body,
                                Description = $"{n.NpcName} saw {b.NpcName} on the floor.",
                                // This is an observation by n, so n must consume it and enter panic/investigate.
                                Source = null,
                            });
                            spotted = true;
                            break;
                        }
                    }
                    if (!spotted)
                    {
                        foreach (var splat in ctx.Blood.All)
                        {
                            if (ctx.CanSee?.Invoke(n, splat.Pos, Bal.BloodSeenRangeMul) ?? false)
                            {
                                ctx.Stimuli.Add(new NpcStimulus
                                {
                                    Id = $"blood-seen:{n.NpcName}:{Now:F1}",
                                    Kind = NpcStimulusKind.BloodFound,
                                    Position = splat.Pos,
                                    Intensity = 0.95f,
                                    EvidenceRef = splat,
                                    EvidenceKind = EvidenceKind.Blood,
                                    Description = $"{n.NpcName} noticed a bloodstain.",
                                    // This is an observation by n, so n must consume it and enter panic/investigate.
                                    Source = null,
                                });
                                break;
                            }
                        }
                    }
                }
            }

            // --- gossip spreads the word ---
            if (n.Arch == Archetype.Gossip && !n.GossipSpreadDone && n.Suspicion >= Bal.GossipTriggerSus)
            {
                n.GossipSpreadDone = true;
                float gossipRadius = Bal.GossipRadius * n.Personality.GossipRadiusMultiplier;
                ctx.Stimuli.Add(new NpcStimulus
                {
                    Id = $"rumor:{n.NpcName}:{Now:F1}",
                    Kind = NpcStimulusKind.Rumor,
                    Position = n.Pos,
                    Intensity = 1f,
                    Radius = gossipRadius,
                    Description = $"{n.NpcName} is spreading office gossip.",
                    Source = n,
                });
                ctx.Toast?.Invoke($"{n.NpcName} is telling EVERYONE nearby.", ToastKind.Warn);
            }

            // --- suspicion climax ---
            float reportThreshold = 100f - (1f - n.Personality.Agreeableness) * 20f;
        if (n.PrimaryObservation is StaffObservationChannel.PanicAndRumor or StaffObservationChannel.IdentityAndVisitors)
            reportThreshold *= 0.85f;
            if (n.Suspicion >= reportThreshold && n.State == NpcState.Routine)
            {
                if (n.Arch == Archetype.Grifter)
                {
                    n.CalmDown();
                    n.Suspicion = 20;
                    ctx.Toast?.Invoke($"{n.NpcName} saw everything… and wants in. You gained an accomplice.", ToastKind.Success);
                }
                else if (n.Spec.Reports)
                {
                    if (ctx.Guard is { } g2 && g2.Awake)
                    {
                        n.StartReport(g2.Pos);
                        ctx.Toast?.Invoke($"{n.NpcName} is RUNNING to security! Intercept or improvise!", ToastKind.Warn);
                        ctx.AlarmSfx?.Invoke();
                    }
                    else
                    {
                        ctx.Toast?.Invoke($"{n.NpcName} ran to tell security… but security is currently a floor lamp.", ToastKind.Chaos);
                        n.CalmDown();
                    }
                }
            }

            maxSus = System.MathF.Max(maxSus, n.Suspicion);
            PushVisual(n, dt);

            // --- consequence engine: liquid slips (the floor is lava-adjacent) ---
            if (n.Awake && n.State is NpcState.Routine or NpcState.Curious && n.SlipCooldownUntil <= Now)
            {
                var liquid = ctx.Blood.NearestLiquidTo(n.Pos, 0.7f);
                if (liquid != null && (n.Moving || n.State == NpcState.Curious))
                {
                    n.SlipCooldownUntil = Now + 20f;
                    if (GD.RandRange(0.0, 1.0) < 0.85)
                    {
                        var slipDir = new Vector3((float)GD.RandRange(-1, 1), 0f, (float)GD.RandRange(-1, 1)).Normalized();
                        ctx.Toast?.Invoke($"{n.NpcName} slips in the spill. The sound was... wet.", ToastKind.Chaos);
                        ctx.AlarmSfx?.Invoke();
                        n.KnockOut(slipDir);
                        ctx.Blood.SpawnLiquid(n.Pos, "coffee");
                    }
                }
            }
        }

        // Workday transitions and evidence discovered during this pass feed the next reaction phase.
        ProcessStimuli(npcs, ctx);

        foreach (var n in npcs)
            maxSus = System.MathF.Max(maxSus, n.Suspicion);
        Outputs.MaxSus = maxSus;
        Outputs.Watched = watched;
    }

    private static void ProcessStimuli(List<NpcBrain> npcs, AiContext ctx)
    {
        if (ctx.Stimuli.Count == 0) return;
        var stimuli = ctx.Stimuli
            .GroupBy(stimulus => stimulus.Id)
            .Select(group => group.First())
            .ToArray();
        ctx.Stimuli.Clear();
        foreach (var stimulus in stimuli)
        {
            foreach (var n in npcs)
            {
                if (n == ctx.Guard || !n.Awake) continue;
                bool activeUser = n == stimulus.ActiveUser;
                if (n == stimulus.Source && !activeUser) continue;
                if (!activeUser && n.Pos.DistanceTo(stimulus.Position) > stimulus.Radius) continue;
                if (stimulus.PlayerLed && stimulus.Kind == NpcStimulusKind.PlayerCrime &&
                    n.Pos.DistanceTo(stimulus.Position) >= Bal.WitnessAutoSeeDist &&
                    !(ctx.CanSee?.Invoke(n, stimulus.Position, 1f) ?? false)) continue;
                if (!n.TryActivateStimulus(stimulus, Now)) continue;

                switch (n.ReactionAction)
                {
                    case NpcReactionAction.Investigate:
                        if (stimulus.EvidenceKind.HasValue && stimulus.EvidenceRef != null)
                            n.StartCurious(stimulus.Position, stimulus.EvidenceRef, stimulus.EvidenceKind.Value);
                        else
                            n.SetReactionDestination(stimulus.Position, n.ReactionAction);
                        break;
                    case NpcReactionAction.Panic:
                        if (stimulus.EvidenceKind.HasValue && stimulus.EvidenceRef != null)
                            n.StartPanic(stimulus.EvidenceKind.Value, stimulus.EvidenceRef, Bal.PanicDurationBlood);
                        else
                            n.Body.ShowEmote("!!");
                        break;
                    case NpcReactionAction.Report:
                        if (ctx.Guard is { } guard && guard.Awake)
                            n.StartReport(guard.Pos);
                        else
                            n.Body.ShowEmote("no security");
                        break;
                    case NpcReactionAction.Flee:
                        Vector3 flee = stimulus.Kind == NpcStimulusKind.FireAlarm
                            ? ctx.EvacPoint
                            : FarthestWaypoint(n, ctx, stimulus.Position);
                        n.SetReactionDestination(flee, n.ReactionAction, 10f);
                        break;
                    case NpcReactionAction.GoToCoffee:
                        n.SetReactionDestination(stimulus.Kind == NpcStimulusKind.PhoneCall
                            ? stimulus.Position : ctx.CoffeePoint, n.ReactionAction, 10f);
                        break;
                    case NpcReactionAction.GoToPrinter:
                        n.SetReactionDestination(new Vector3(-27f, 0f, -10.5f), n.ReactionAction, 10f);
                        break;
                    case NpcReactionAction.GoToMeeting:
                        var meetings = ctx.WorldRef.WaypointsFor("meeting_a");
                        n.SetReactionDestination(meetings.Length > 0 ? meetings[0] : stimulus.Position,
                            n.ReactionAction, 10f);
                        break;
                    case NpcReactionAction.Gossip:
                        n.AddSuspicion(Bal.GossipSpreadAmount * stimulus.Intensity);
                        n.Body.ShowEmote("...");
                        break;
                    case NpcReactionAction.Complain:
                        n.AddSuspicion(ObjectBalance.ComplaintSuspicion);
                        n.Body.ShowEmote("ugh");
                        break;
                    case NpcReactionAction.SeekHelp:
                        n.Body.ShowEmote("IT called");
                        if ((stimulus.ObjectType == OfficeObjectType.Computer ||
                             stimulus.ObjectType == OfficeObjectType.ServerTerminal ||
                             stimulus.ObjectType == OfficeObjectType.ServerRack) &&
                            stimulus.ObjectState != OfficeObjectState.ITCalled)
                            ctx.SetObjectState?.Invoke(stimulus.ObjectId, OfficeObjectState.ITCalled, n);
                        break;
                    case NpcReactionAction.Recover:
                        n.Stats.ReceiveComfort(System.MathF.Max(stimulus.ComfortDelta, ObjectBalance.RecoveryComfort));
                        n.Body.ShowEmote("ahh");
                        break;
                    case NpcReactionAction.Observe:
                        n.Body.ShowEmote(stimulus.PlayerLed ? "!" : "hmm");
                        if (stimulus.PlayerLed || stimulus.Kind == NpcStimulusKind.Rumor)
                            n.AddSuspicion(8f * stimulus.Intensity);
                        break;
                }
            }
        }
    }

    private static Vector3 FarthestWaypoint(NpcBrain n, AiContext ctx, Vector3 from)
    {
        var points = ctx.WorldRef.WaypointsFor(n.Zone);
        Vector3 farthest = n.Pos;
        float best = -1f;
        foreach (var point in points)
        {
            float distance = point.DistanceTo(from);
            if (distance > best) { best = distance; farthest = point; }
        }
        return farthest;
    }

    private static void PushVisual(NpcBrain n, double dt)
    {
        bool showBar = n.Awake && n.State != NpcState.Seated && n.Suspicion > 1f;
        n.Body.SetSuspicion(showBar ? n.Suspicion / 100f : 0f);
        n.Body.SetMoving(n.Moving, run: n.State is NpcState.Report or NpcState.Hunt);
    }

    /// <summary>Port of game.ts updateGuard().</summary>
    public static void GuardTick(NpcBrain g, AiContext ctx, double dt)
    {
        if (!g.Awake) return;

        // even Mr Purple cannot resist the photocopied face
        if (g.DistractTimer > 0 && g.State != NpcState.Hunt)
        {
            g.DistractTimer -= (float)dt;
            if (g.DistractTimer <= 0) g.Body.ClearEmote();
            return;
        }

        if (g.State == NpcState.Hunt)
        {
            bool sees = ctx.CanSee?.Invoke(g, ctx.PlayerPos, Bal.GuardHuntVisionMul) ?? false;
            if (sees)
            {
                g.LastSeenPlayer = ctx.PlayerPos;
                g.RememberAreaHint(ctx.PlayerPos, WorkdayBalance.AreaHintConfidence, WorkdayBalance.BossHintMemorySeconds);
                g.LostSightTimer = 0;
                if (ctx.PlayerCarrying)
                    ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, 8); // personally witnessing carrying refreshes the hunt
            }
            else
            {
                g.LostSightTimer += (float)dt;
            }

            Vector3 huntTarget = g.LastSeenAreaHint ?? g.LastSeenPlayer;
            bool arrived = g.StepToward(ctx.WorldRef, huntTarget, dt);
            float dist = g.Pos.DistanceTo(ctx.PlayerPos);
            if (dist < Bal.GuardCatchDist)
            {
                ctx.OnPlayerCaught?.Invoke();
                return;
            }
            if ((arrived && g.LostSightTimer > Bal.GuardGiveUpArrived) ||
                g.LostSightTimer > Bal.GuardGiveUpLostSight ||
                ctx.AlertTimer <= 0)
            {
                g.State = NpcState.Routine;
                g.ClearAreaHint();
                g.MoveTarget = null;
                g.PauseTimer = 1f;
                ctx.AlertTimer = 0;
                ctx.Toast?.Invoke("Mr Purple lost you. He pretends he meant to walk here all along.", ToastKind.Info);
            }
            return;
        }

        // patrol: on easy/standard difficulty Mr Purple spends part of the day in his office.
        // Hard mode keeps him moving, but he still follows visible patrol posts.
        float bossHour = WorkdayBalance.WorkdayStartHour + ctx.WorkdayElapsed / (Bal.ShiftSeconds / (WorkdayBalance.WorkdayEndHour - WorkdayBalance.WorkdayStartHour));
        var bossProfile = new BossBehaviorProfile(ctx.BossDifficulty);
        bool atDeskBeat = (bossHour - WorkdayBalance.WorkdayStartHour) / (WorkdayBalance.WorkdayEndHour - WorkdayBalance.WorkdayStartHour) < bossProfile.DeskShare;
        if (atDeskBeat && !ctx.NoiseFresh)
        {
            g.MoveTarget = g.HomePos;
            g.StepToward(ctx.WorldRef, g.HomePos, dt, Bal.GuardPatrolSpeed);
            g.Moving = false;
            return;
        }

        // patrol
        if (ctx.NoiseFresh)
        {
            // recent noise: Mr Purple checks it out personally
            if (g.StepToward(ctx.WorldRef, ctx.NoisePos, dt, Bal.GuardPatrolSpeed * 1.3f))
                g.Moving = false;
        }
        else if (g.PauseTimer > 0)
        {
            g.PauseTimer -= (float)dt;
            g.Moving = false;
        }
        else if (!g.MoveTarget.HasValue || g.StepToward(ctx.WorldRef, g.MoveTarget.Value, dt, Bal.GuardPatrolSpeed))
        {
            var posts = WorldData.GuardPosts;
            if (posts.Length > 0)
                g.MoveTarget = posts[(int)(GD.RandRange(0, posts.Length - 1))];
            g.PauseTimer = 2f + (float)GD.RandRange(0.0, 3.0);
        }

        // guard personally witnessing blatant crime
        if ((ctx.CanSee?.Invoke(g, ctx.PlayerPos, 1f) ?? false) && ctx.PlayerCarrying)
        {
            ctx.Toast?.Invoke("Mr Purple saw you carrying a \"mannequin\". He is not buying it.", ToastKind.Warn);
            g.State = NpcState.Hunt;
            g.LastSeenPlayer = ctx.PlayerPos;
            ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, Bal.GuardAlertSeesCarry);
            ctx.AlarmSfx?.Invoke();
            return;
        }

        // guard stumbling onto a body -> immediate hunt
        foreach (var b in ctx.Npcs)
        {
            if (b == g || b.State != NpcState.Out || !b.Body.Visible) continue;
            if (ctx.CanSee?.Invoke(g, b.Pos, 1f) ?? false)
            {
                ctx.Toast?.Invoke($"Mr Purple found {b.NpcName}'s body. He has decided it was you.", ToastKind.Warn);
                g.State = NpcState.Hunt;
                g.LastSeenPlayer = ctx.PlayerPos;
                ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, Bal.GuardAlertFindsBody);
                ctx.AlarmSfx?.Invoke();
                return;
            }
        }
        foreach (var s in ctx.Blood.All)
        {
            if (ctx.CanSee?.Invoke(g, s.Pos, 0.9f) ?? false)
            {
                ctx.Toast?.Invoke("Mr Purple found a bloodstain and is connecting dots that don't exist.", ToastKind.Warn);
                g.State = NpcState.Hunt;
                g.LastSeenPlayer = ctx.PlayerPos;
                ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, Bal.GuardAlertFindsBlood);
                ctx.AlarmSfx?.Invoke();
                return;
            }
        }
    }

    // guard posts come from WorldData via the world's waypoint-agnostic table;
    // exposed here to avoid a GameMode dependency inside the director.
    public static Vector3[] GuardPosts => WorldData.GuardPosts;

    private static Vector3[] GuardPostsOf(AiContext ctx) => WorldData.GuardPosts;
}





