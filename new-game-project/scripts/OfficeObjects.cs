using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Objects that can participate in the office simulation and consequence engine.</summary>
public enum OfficeObjectType
{
    Printer, Computer, DeskPhone, CoffeeMaker, Microwave, WaterCooler, VendingMachine, Toilet, Sink,
    Refrigerator, MeetingTable, Whiteboard, Projector, ProjectorScreen, FilingCabinet, Shredder,
    ServerRack, ServerTerminal, KeycardReader, SecurityCamera, Door, Elevator, Stairwell, ReceptionDesk,
    Cubicle, OfficeDesk, OfficeChair, PrinterPaperShelf, MailTrolley, GarbageBin, RecyclingBin,
    GarbageChute, Incinerator, CardboardCompactor, SupplyShelf, UniformLocker, FirstAidCabinet, FireAlarm,
    FireExtinguisher, MopBucket, WetFloorSign, Plant, WallPicture, WallClock, Noticeboard, WaterBottle,
    LunchContainer, Mug, Stapler, PaperStack
}

/// <summary>Reusable state vocabulary. Definitions restrict which states each object accepts.</summary>
public enum OfficeObjectState
{
    Working, Offline, OutOfPaper, Jammed, Glitchy, Hacked, ITCalled,
    Locked, Unlocked, KeycardRequired, Open, Closed, Occupied, Available,
    Brewing, Ready, Empty, Full, Spilled, Broken, Restocking, Overheated,
    Recording, Disabled, Blocked, Missing, LowBattery, Alarmed, Wet, Clean,
    MeetingActive, InUse, Idle, Lost, Found, Charging, NeedsRefill, Comfortable,
    Uncomfortable, Overdue, Delivered, Healthy
}

public sealed record OfficeObjectStateProfile(
    OfficeObjectState State,
    float Activation,
    float Radius,
    float Duration,
    float StressDelta,
    float ComfortDelta,
    NpcStimulusKind? StimulusKind = null,
    NpcReactionAction PreferredAction = NpcReactionAction.Observe);

public sealed class OfficeObjectDefinition
{
    public required OfficeObjectType Type;
    public required string DisplayName;
    public required OfficeObjectState DefaultState;
    public required OfficeObjectState[] ValidStates;
    public required OfficeObjectStateProfile[] Profiles;
    public string Department = "Facilities";
    public bool GameplayRelevant = true;
    public bool PlayerCanTrigger = true;

    public OfficeObjectStateProfile ProfileFor(OfficeObjectState state) =>
        Profiles.FirstOrDefault(p => p.State == state)
        ?? new OfficeObjectStateProfile(state, ObjectBalance.DefaultActivation, ObjectBalance.DefaultRadius,
            ObjectBalance.DefaultDuration, ObjectBalance.DefaultStress, ObjectBalance.DefaultComfort);

    public bool Allows(OfficeObjectState state) => System.Array.IndexOf(ValidStates, state) >= 0;
}

public sealed class OfficeObjectRuntime
{
    public string Id;
    public OfficeObjectDefinition Definition;
    public Vector3 Position;
    public OfficeObjectState State;
    public float StateTimer;
    public string? RequiredKeycard;
    public NpcBrain? LastActor;

    public OfficeObjectRuntime(string id, OfficeObjectDefinition definition, Vector3 position)
    {
        Id = id;
        Definition = definition;
        Position = position;
        State = definition.DefaultState;
    }

    public bool SetState(OfficeObjectState next, float duration = 0f)
    {
        if (!Definition.Allows(next) || State == next) return false;
        State = next;
        StateTimer = duration > 0f ? duration : Definition.ProfileFor(next).Duration;
        return true;
    }

    public bool TryUse(string? keycardId)
    {
        if (State is not (OfficeObjectState.Locked or OfficeObjectState.KeycardRequired)) return true;
        if (!string.IsNullOrEmpty(RequiredKeycard) && RequiredKeycard != keycardId) return false;
        return SetState(OfficeObjectState.Unlocked);
    }

    public void Tick(float dt)
    {
        if (StateTimer <= 0f) return;
        StateTimer = System.MathF.Max(0f, StateTimer - dt);
        if (StateTimer <= 0f && State != Definition.DefaultState)
            SetState(Definition.DefaultState);
    }
}

public static class ObjectBalance
{
    public const float DefaultActivation = 0.35f;
    public const float DefaultRadius = 10f;
    public const float DefaultDuration = 0f;
    public const float DefaultStress = 0f;
    public const float DefaultComfort = 0f;
    public const float FailureActivation = 1.05f;
    public const float FailureRadius = 14f;
    public const float FailureStress = 0.75f;
    public const float ComfortActivation = 0.6f;
    public const float ComfortRadius = 12f;
    public const float ComfortGain = 0.7f;
    public const float AlarmActivation = 1.3f;
    public const float AlarmRadius = 100f;
    public const float CrimeActivation = 1.6f;
    public const float ComplaintSuspicion = 4f;
    public const float RecoveryComfort = 0.7f;
    public const float AccessDeniedActivation = 0.8f;
    public const float AccessDeniedRadius = 12f;
    public const float AccessDeniedStress = 0.35f;
}

/// <summary>
/// Catalog of the first 50 interactable office objects. This is simulation data,
/// independent from meshes, so placeholders can be replaced without changing behavior.
/// </summary>
public static class OfficeObjectLibrary
{
    private static OfficeObjectStateProfile P(OfficeObjectState state, float activation = ObjectBalance.DefaultActivation,
        float radius = ObjectBalance.DefaultRadius, float duration = ObjectBalance.DefaultDuration,
        float stress = ObjectBalance.DefaultStress, float comfort = ObjectBalance.DefaultComfort,
        NpcStimulusKind? stimulus = null, NpcReactionAction action = NpcReactionAction.Observe) =>
        new(state, activation, radius, duration, stress, comfort, stimulus, action);

    private static OfficeObjectDefinition D(OfficeObjectType type, string name, OfficeObjectState defaultState,
        OfficeObjectState[] states, OfficeObjectStateProfile[] profiles, string department = "Facilities",
        bool gameplay = true, bool playerCanTrigger = true) => new()
        {
            Type = type,
            DisplayName = name,
            DefaultState = defaultState,
            ValidStates = states,
            Profiles = profiles,
            Department = department,
            GameplayRelevant = gameplay,
            PlayerCanTrigger = playerCanTrigger,
        };

    public static readonly OfficeObjectDefinition[] Catalog =
    {
        D(OfficeObjectType.Printer, "Office printer", OfficeObjectState.Working,
            new[] { OfficeObjectState.Working, OfficeObjectState.InUse, OfficeObjectState.OutOfPaper, OfficeObjectState.Jammed, OfficeObjectState.Offline, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.OutOfPaper, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, ObjectBalance.FailureStress, 0f, NpcStimulusKind.PrinterFailure, NpcReactionAction.GoToPrinter), P(OfficeObjectState.Jammed, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.9f, 0f, NpcStimulusKind.PrinterFailure, NpcReactionAction.GoToPrinter), P(OfficeObjectState.Offline, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.65f, 0f, NpcStimulusKind.PrinterFailure, NpcReactionAction.SeekHelp) }, "Facilities"),
        D(OfficeObjectType.Computer, "Desk computer", OfficeObjectState.Working,
            new[] { OfficeObjectState.Working, OfficeObjectState.Glitchy, OfficeObjectState.Hacked, OfficeObjectState.Offline, OfficeObjectState.ITCalled, OfficeObjectState.InUse },
            new[] { P(OfficeObjectState.Glitchy, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.65f, 0f, NpcStimulusKind.ObjectFailure, NpcReactionAction.SeekHelp), P(OfficeObjectState.Hacked, 1.2f, 18f, 0f, 1f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report), P(OfficeObjectState.ITCalled, 0.8f, 16f, 20f, 0.2f, 0f, NpcStimulusKind.ITCalled, NpcReactionAction.SeekHelp) }, "IT"),
        D(OfficeObjectType.DeskPhone, "Desk phone", OfficeObjectState.Idle,
            new[] { OfficeObjectState.Idle, OfficeObjectState.InUse, OfficeObjectState.Offline, OfficeObjectState.Recording },
            new[] { P(OfficeObjectState.InUse, 0.5f, 10f, 4f, 0f, 0.2f, NpcStimulusKind.PhoneCall, NpcReactionAction.GoToCoffee) }, "IT"),
        D(OfficeObjectType.CoffeeMaker, "Coffee maker", OfficeObjectState.Ready,
            new[] { OfficeObjectState.Ready, OfficeObjectState.Brewing, OfficeObjectState.Empty, OfficeObjectState.Broken, OfficeObjectState.Spilled },
            new[] { P(OfficeObjectState.Brewing, ObjectBalance.ComfortActivation, ObjectBalance.ComfortRadius, 3.5f, 0f, ObjectBalance.ComfortGain, NpcStimulusKind.ComfortEvent, NpcReactionAction.Recover), P(OfficeObjectState.Empty, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.45f, 0f, NpcStimulusKind.CoffeeBreak, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.Microwave, "Microwave", OfficeObjectState.Ready,
            new[] { OfficeObjectState.Ready, OfficeObjectState.InUse, OfficeObjectState.Broken, OfficeObjectState.Overheated, OfficeObjectState.Spilled },
            new[] { P(OfficeObjectState.InUse, 0.75f, 14f, 3f, 0.1f, 0f, NpcStimulusKind.WorkdayActivity), P(OfficeObjectState.Overheated, 1.1f, 16f, 0f, 0.7f, 0f, NpcStimulusKind.Stink, NpcReactionAction.Flee) }, "Facilities"),
        D(OfficeObjectType.WaterCooler, "Water cooler", OfficeObjectState.Ready,
            new[] { OfficeObjectState.Ready, OfficeObjectState.Empty, OfficeObjectState.Broken, OfficeObjectState.InUse },
            new[] { P(OfficeObjectState.InUse, ObjectBalance.ComfortActivation, ObjectBalance.ComfortRadius, 5f, 0f, ObjectBalance.ComfortGain, NpcStimulusKind.ComfortEvent, NpcReactionAction.Recover), P(OfficeObjectState.Empty, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.35f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.VendingMachine, "Vending machine", OfficeObjectState.Ready,
            new[] { OfficeObjectState.Ready, OfficeObjectState.Empty, OfficeObjectState.Broken, OfficeObjectState.Restocking },
            new[] { P(OfficeObjectState.Restocking, 0.4f, 10f, 30f, 0f, 0.15f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Facilities"),
        D(OfficeObjectType.Toilet, "Office toilet", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.Blocked, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Blocked, 0.8f, 10f, 0f, 0.55f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.Sink, "Break-room sink", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Blocked, OfficeObjectState.Broken, OfficeObjectState.InUse },
            new[] { P(OfficeObjectState.Blocked, 0.6f, 10f, 0f, 0.35f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.Refrigerator, "Office refrigerator", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Broken, OfficeObjectState.Spilled },
            new[] { P(OfficeObjectState.Spilled, 0.8f, 12f, 0f, 0.5f, 0f, NpcStimulusKind.Stink, NpcReactionAction.Flee) }, "Facilities"),
        D(OfficeObjectType.MeetingTable, "Meeting table", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.MeetingActive, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.MeetingActive, 0.55f, 14f, 20f, 0f, 0.35f, NpcStimulusKind.MeetingPressure, NpcReactionAction.GoToMeeting) }, "Operations"),
        D(OfficeObjectType.Whiteboard, "Whiteboard", OfficeObjectState.Clean,
            new[] { OfficeObjectState.Clean, OfficeObjectState.InUse, OfficeObjectState.Missing, OfficeObjectState.Hacked },
            new[] { P(OfficeObjectState.InUse, 0.35f, 10f, 5f, 0f, 0.2f, NpcStimulusKind.MeetingPressure, NpcReactionAction.GoToMeeting), P(OfficeObjectState.Hacked, 0.8f, 14f, 0f, 0.45f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "Operations"),
        D(OfficeObjectType.Projector, "Meeting projector", OfficeObjectState.Offline,
            new[] { OfficeObjectState.Working, OfficeObjectState.Offline, OfficeObjectState.Glitchy, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Glitchy, 0.8f, 14f, 0f, 0.55f, 0f, NpcStimulusKind.MeetingPressure, NpcReactionAction.Complain) }, "IT"),
        D(OfficeObjectType.ProjectorScreen, "Projector screen", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.InUse, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.InUse, 0.3f, 10f, 20f, 0f, 0.2f, NpcStimulusKind.MeetingPressure, NpcReactionAction.GoToMeeting) }, "Operations"),
        D(OfficeObjectType.FilingCabinet, "Filing cabinet", OfficeObjectState.Locked,
            new[] { OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.Blocked, OfficeObjectState.Missing },
            new[] { P(OfficeObjectState.Missing, 0.75f, 12f, 0f, 0.5f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "HR"),
        D(OfficeObjectType.Shredder, "Paper shredder", OfficeObjectState.Ready,
            new[] { OfficeObjectState.Ready, OfficeObjectState.InUse, OfficeObjectState.Jammed, OfficeObjectState.Full, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Jammed, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.75f, 0f, NpcStimulusKind.PrinterFailure, NpcReactionAction.Complain) }, "HR"),
        D(OfficeObjectType.ServerRack, "Server rack", OfficeObjectState.Working,
            new[] { OfficeObjectState.Working, OfficeObjectState.InUse, OfficeObjectState.Overheated, OfficeObjectState.Offline, OfficeObjectState.Hacked, OfficeObjectState.Recording },
            new[] { P(OfficeObjectState.Overheated, 1.15f, 20f, 0f, 0.8f, 0f, NpcStimulusKind.Stink, NpcReactionAction.Flee), P(OfficeObjectState.Offline, ObjectBalance.FailureActivation, 20f, 0f, 0.9f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.SeekHelp), P(OfficeObjectState.Hacked, 1.3f, 24f, 0f, 1f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "IT"),
        D(OfficeObjectType.ServerTerminal, "Server terminal", OfficeObjectState.Locked,
            new[] { OfficeObjectState.Working, OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.InUse, OfficeObjectState.Hacked, OfficeObjectState.Glitchy, OfficeObjectState.ITCalled },
            new[] { P(OfficeObjectState.Hacked, 1.2f, 20f, 0f, 0.9f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report), P(OfficeObjectState.Glitchy, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.65f, 0f, NpcStimulusKind.ObjectFailure, NpcReactionAction.SeekHelp), P(OfficeObjectState.ITCalled, 0.8f, 16f, 20f, 0.2f, 0f, NpcStimulusKind.ITCalled, NpcReactionAction.SeekHelp) }, "IT"),
        D(OfficeObjectType.KeycardReader, "Keycard reader", OfficeObjectState.Locked,
            new[] { OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.KeycardRequired, OfficeObjectState.Offline, OfficeObjectState.Hacked },
            new[] { P(OfficeObjectState.KeycardRequired, 0.55f, 10f, 0f, 0.25f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe), P(OfficeObjectState.Hacked, 1.1f, 16f, 0f, 0.8f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "Security"),
        D(OfficeObjectType.SecurityCamera, "Security camera", OfficeObjectState.Recording,
            new[] { OfficeObjectState.Recording, OfficeObjectState.Offline, OfficeObjectState.Hacked, OfficeObjectState.Disabled },
            new[] { P(OfficeObjectState.Hacked, 1.1f, 18f, 0f, 0.7f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report), P(OfficeObjectState.Offline, 0.7f, 14f, 0f, 0.35f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.SeekHelp) }, "Security"),
        D(OfficeObjectType.Door, "Office door", OfficeObjectState.Closed,
            new[] { OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.KeycardRequired, OfficeObjectState.Open, OfficeObjectState.Closed, OfficeObjectState.Blocked, OfficeObjectState.Hacked },
            new[] { P(OfficeObjectState.Locked, 0.5f, 8f, 0f, 0.2f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe), P(OfficeObjectState.KeycardRequired, 0.7f, 10f, 0f, 0.4f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.SeekHelp), P(OfficeObjectState.Hacked, 1f, 15f, 0f, 0.65f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "Security"),
        D(OfficeObjectType.Elevator, "Elevator", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.Offline, OfficeObjectState.KeycardRequired, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.Offline, ObjectBalance.FailureActivation, 14f, 0f, 0.65f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain), P(OfficeObjectState.KeycardRequired, 0.7f, 12f, 0f, 0.35f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.SeekHelp) }, "Facilities"),
        D(OfficeObjectType.Stairwell, "Stairwell", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Locked, OfficeObjectState.KeycardRequired, OfficeObjectState.Blocked, OfficeObjectState.Occupied },
            new[] { P(OfficeObjectState.KeycardRequired, 0.65f, 12f, 0f, 0.3f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.SeekHelp) }, "Facilities"),
        D(OfficeObjectType.ReceptionDesk, "Reception desk", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.InUse, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.InUse, 0.35f, 12f, 0f, 0f, 0.2f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Reception"),
        D(OfficeObjectType.Cubicle, "Cubicle workstation", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.Blocked, OfficeObjectState.Missing },
            new[] { P(OfficeObjectState.Missing, 0.5f, 10f, 0f, 0.2f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Operations"),
        D(OfficeObjectType.OfficeDesk, "Office desk", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.InUse, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.InUse, 0.25f, 8f, 0f, 0f, 0.15f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Operations"),
        D(OfficeObjectType.OfficeChair, "Office chair", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.Broken, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.Broken, 0.7f, 10f, 0f, 0.45f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.PrinterPaperShelf, "Printer paper shelf", OfficeObjectState.Full,
            new[] { OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Restocking, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.Empty, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.7f, 0f, NpcStimulusKind.PrinterFailure, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.MailTrolley, "Mail trolley", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Occupied, OfficeObjectState.Delivered, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.Delivered, 0.25f, 10f, 0f, 0f, 0.2f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Facilities"),
        D(OfficeObjectType.GarbageBin, "Garbage bin", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Overdue },
            new[] { P(OfficeObjectState.Overdue, 0.8f, 12f, 0f, 0.6f, 0f, NpcStimulusKind.Stink, NpcReactionAction.Flee) }, "Facilities"),
        D(OfficeObjectType.RecyclingBin, "Recycling bin", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Overdue },
            new[] { P(OfficeObjectState.Full, 0.45f, 10f, 0f, 0.2f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.GarbageChute, "Garbage chute", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Blocked, OfficeObjectState.Full, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Blocked, 0.8f, 12f, 0f, 0.55f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.Incinerator, "Incinerator", OfficeObjectState.Locked,
            new[] { OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.InUse, OfficeObjectState.Overheated, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Overheated, 1.1f, 18f, 0f, 0.8f, 0f, NpcStimulusKind.Stink, NpcReactionAction.Flee) }, "Facilities"),
        D(OfficeObjectType.CardboardCompactor, "Cardboard compactor", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.InUse, OfficeObjectState.Jammed, OfficeObjectState.Full },
            new[] { P(OfficeObjectState.Jammed, ObjectBalance.FailureActivation, ObjectBalance.FailureRadius, 0f, 0.7f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities"),
        D(OfficeObjectType.SupplyShelf, "Supply shelf", OfficeObjectState.Full,
            new[] { OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Blocked, OfficeObjectState.Restocking },
            new[] { P(OfficeObjectState.Empty, 0.7f, 12f, 0f, 0.45f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.SeekHelp) }, "Facilities"),
        D(OfficeObjectType.UniformLocker, "Uniform locker", OfficeObjectState.Locked,
            new[] { OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.KeycardRequired, OfficeObjectState.Missing, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.Missing, 0.65f, 12f, 0f, 0.35f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "Facilities"),
        D(OfficeObjectType.FirstAidCabinet, "First-aid cabinet", OfficeObjectState.Locked,
            new[] { OfficeObjectState.Locked, OfficeObjectState.Unlocked, OfficeObjectState.KeycardRequired, OfficeObjectState.Empty, OfficeObjectState.InUse },
            new[] { P(OfficeObjectState.InUse, 0.4f, 10f, 4f, 0f, 0.25f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "HR"),
        D(OfficeObjectType.FireAlarm, "Fire alarm", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Alarmed, OfficeObjectState.Disabled, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Alarmed, ObjectBalance.AlarmActivation, ObjectBalance.AlarmRadius, 12f, 0.2f, 0f, NpcStimulusKind.FireAlarm, NpcReactionAction.Flee) }, "Security"),
        D(OfficeObjectType.FireExtinguisher, "Fire extinguisher", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Empty, OfficeObjectState.InUse, OfficeObjectState.Missing },
            new[] { P(OfficeObjectState.Missing, 0.6f, 12f, 0f, 0.3f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.SeekHelp) }, "Facilities"),
        D(OfficeObjectType.MopBucket, "Mop bucket", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.InUse, OfficeObjectState.Empty, OfficeObjectState.Missing },
            new[] { P(OfficeObjectState.InUse, 0.25f, 10f, 2f, 0f, 0.3f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Facilities"),
        D(OfficeObjectType.WetFloorSign, "Wet-floor sign", OfficeObjectState.Clean,
            new[] { OfficeObjectState.Clean, OfficeObjectState.Wet, OfficeObjectState.Missing },
            new[] { P(OfficeObjectState.Wet, 0.55f, 10f, 0f, 0.1f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Facilities"),
        D(OfficeObjectType.Plant, "Office plant", OfficeObjectState.Healthy,
            new[] { OfficeObjectState.Healthy, OfficeObjectState.Uncomfortable, OfficeObjectState.Missing, OfficeObjectState.InUse },
            new[] { P(OfficeObjectState.Uncomfortable, 0.35f, 10f, 0f, 0.1f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities", false),
        D(OfficeObjectType.WallPicture, "Wall picture", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Missing, OfficeObjectState.Blocked },
            new[] { P(OfficeObjectState.Missing, 0.3f, 8f, 0f, 0.1f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe) }, "Operations", false),
        D(OfficeObjectType.WallClock, "Wall clock", OfficeObjectState.Working,
            new[] { OfficeObjectState.Working, OfficeObjectState.Offline, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Offline, 0.35f, 8f, 0f, 0.15f, 0f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Complain) }, "Facilities", false),
        D(OfficeObjectType.Noticeboard, "Noticeboard", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Missing, OfficeObjectState.InUse, OfficeObjectState.Hacked },
            new[] { P(OfficeObjectState.InUse, 0.35f, 10f, 5f, 0f, 0.2f, NpcStimulusKind.WorkdayActivity, NpcReactionAction.Observe), P(OfficeObjectState.Hacked, 0.7f, 14f, 0f, 0.35f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Report) }, "HR", false),
        D(OfficeObjectType.WaterBottle, "Water bottle", OfficeObjectState.Full,
            new[] { OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Missing, OfficeObjectState.Spilled },
            new[] { P(OfficeObjectState.Spilled, 0.6f, 8f, 0f, 0.25f, 0f, NpcStimulusKind.PlayerNoise, NpcReactionAction.Investigate) }, "Facilities", false),
        D(OfficeObjectType.LunchContainer, "Lunch container", OfficeObjectState.Full,
            new[] { OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Missing, OfficeObjectState.Spilled },
            new[] { P(OfficeObjectState.Missing, 0.65f, 12f, 0f, 0.55f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Complain), P(OfficeObjectState.Spilled, 0.8f, 12f, 0f, 0.45f, 0f, NpcStimulusKind.Stink, NpcReactionAction.Flee) }, "Operations", false),
        D(OfficeObjectType.Mug, "Coffee mug", OfficeObjectState.Full,
            new[] { OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Missing, OfficeObjectState.Spilled, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Spilled, 0.7f, 10f, 0f, 0.35f, 0f, NpcStimulusKind.PlayerNoise, NpcReactionAction.Investigate) }, "Operations", false),
        D(OfficeObjectType.Stapler, "Stapler", OfficeObjectState.Available,
            new[] { OfficeObjectState.Available, OfficeObjectState.Empty, OfficeObjectState.Missing, OfficeObjectState.Broken },
            new[] { P(OfficeObjectState.Missing, 0.45f, 9f, 0f, 0.2f, 0f, NpcStimulusKind.PlayerCrime, NpcReactionAction.Complain) }, "Operations", false),
        D(OfficeObjectType.PaperStack, "Paper stack", OfficeObjectState.Full,
            new[] { OfficeObjectState.Full, OfficeObjectState.Empty, OfficeObjectState.Missing, OfficeObjectState.Spilled },
            new[] { P(OfficeObjectState.Empty, 0.65f, 12f, 0f, 0.5f, 0f, NpcStimulusKind.PrinterFailure, NpcReactionAction.GoToPrinter) }, "Facilities", false),
    };

    public static OfficeObjectDefinition For(OfficeObjectType type) =>
        Catalog.First(definition => definition.Type == type);

    public static OfficeObjectDefinition For(string name)
    {
        string normalized = name.Replace("_", "").Replace("-", "").ToLowerInvariant();
        return Catalog.FirstOrDefault(definition => definition.Type.ToString().Replace("_", "").ToLowerInvariant() == normalized)
            ?? For(OfficeObjectType.OfficeDesk);
    }

    /// <summary>Starter instances map the visible prototype objects to stable simulation ids.</summary>
    public static List<OfficeObjectRuntime> CreateStarterObjects()
    {
        var positions = new Dictionary<OfficeObjectType, Vector3>
        {
            [OfficeObjectType.Printer] = new(-27f, 0f, -10.5f),
            [OfficeObjectType.Computer] = new(-22f, 0f, -17.2f),
            [OfficeObjectType.DeskPhone] = new(-13.8f, 0.55f, -7f),
            [OfficeObjectType.CoffeeMaker] = new(25f, 0f, -20.4f),
            [OfficeObjectType.Microwave] = new(26.5f, 0f, -20.5f),
            [OfficeObjectType.WaterCooler] = new(14f, 0f, -20.8f),
            [OfficeObjectType.VendingMachine] = new(31f, 0f, -17f),
            [OfficeObjectType.Refrigerator] = new(30.5f, 0f, -20.5f),
            [OfficeObjectType.ServerRack] = new(-25.6f, 0f, -20.8f),
            [OfficeObjectType.ServerTerminal] = new(-22f, 0f, -18.2f),
            [OfficeObjectType.KeycardReader] = new(-12.3f, 0f, -15f),
            [OfficeObjectType.SecurityCamera] = new(-12.5f, 2.5f, -13f),
            [OfficeObjectType.Door] = new(-12.3f, 0f, -15f),
            [OfficeObjectType.MeetingTable] = new(-25f, 0f, 18f),
            [OfficeObjectType.Whiteboard] = new(20f, 0f, -21.4f),
            [OfficeObjectType.MailTrolley] = new(26f, 0f, -12f),
            [OfficeObjectType.UniformLocker] = new(27f, 0f, 11f),
            [OfficeObjectType.Incinerator] = new(-31f, 0f, -20.5f),
            [OfficeObjectType.GarbageChute] = new(11.5f, 0f, -20.5f),
            [OfficeObjectType.FireAlarm] = new(12.3f, 0f, -9f),
            [OfficeObjectType.ReceptionDesk] = new(0f, 0f, 17.5f),
            [OfficeObjectType.Shredder] = new(-27f, 0f, -10.8f),
        };

        var result = new List<OfficeObjectRuntime>();
        foreach (var definition in Catalog)
        {
            positions.TryGetValue(definition.Type, out var position);
            var runtime = new OfficeObjectRuntime(definition.Type.ToString().ToLowerInvariant(), definition, position);
            if (definition.Type == OfficeObjectType.Incinerator) runtime.RequiredKeycard = "janitorial";
            if (definition.Type == OfficeObjectType.ServerTerminal) runtime.RequiredKeycard = "gary-level-3";
            if (definition.Type == OfficeObjectType.Door)
            {
                runtime.RequiredKeycard = "department-level-1";
                runtime.SetState(OfficeObjectState.KeycardRequired);
            }
            if (definition.Type == OfficeObjectType.KeycardReader)
                runtime.SetState(OfficeObjectState.KeycardRequired);
            result.Add(runtime);
        }
        return result;
    }
}
