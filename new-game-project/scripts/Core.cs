using Godot;

// ============================================================================
// Core shared contracts — ported 1:1 from src/game/*.ts (the Three.js
// proof-of-fun prototype). Values are gameplay-balanced; DO NOT change them.
// ============================================================================

public enum Archetype { Snoop, Slob, Gossip, Grifter, Drone, Guard }

public enum NpcState { Routine, Curious, Panic, Report, Hunt, Seated, Out, Hidden }

/// <summary>Normal 9-5 activity state. Consequence states remain in NpcState and interrupt this layer.</summary>
public enum WorkdayState
{
    Arriving,
    WorkingAtDesk,
    WalkingToPrinter,
    WaitingAtPrinter,
    Printing,
    PrinterBroken,
    Toilet,
    OnBreak,
    MeetingWalk,
    Meeting,
    AnxiousMeeting,
    PhoneCall,
    DoomScrolling,
    WaterCooler,
    CoffeeBreak,
    StationaryUse,
    WalkingThinking,
    DepressedWorking,
    HappyWorking,
    WorriedWorking,
    DistractedWorking,
    AnnoyedWorking,
    PickingUpSlack,
    SuspiciousWorking,
    NotPayingAttention,
    EngrossedWorking,
    FeelingSick,
    FeelingHorny,
    FeelingCurious,
    FeelingSleepy,
    FeelingDrunk,
    Speed,
    Stoned,
    LSD,
    KHole,
    Ecstasy,
    AnxiousWalking,
    AnxiousWorking,
    PanicAttack,
    Reading,
}

public enum EvidenceKind { Blood, Body, Noise }

/// <summary>Shared consequence/workday stimulus categories consumed by every NPC.</summary>
public enum NpcStimulusKind
{
    PlayerCrime,
    PlayerNoise,
    BodyFound,
    BloodFound,
    Stink,
    FireAlarm,
    CoffeeBreak,
    PrinterFailure,
    MeetingPressure,
    PhoneCall,
    WorkdayActivity,
    ObjectFailure,
    AccessDenied,
    ITCalled,
    ComfortEvent,
    Rumor,
}

/// <summary>Action selected after an NPC's activation threshold is crossed.</summary>
public enum NpcReactionAction
{
    Ignore,
    Observe,
    Investigate,
    Panic,
    Report,
    Flee,
    GoToCoffee,
    GoToPrinter,
    GoToMeeting,
    Gossip,
    Complain,
    SeekHelp,
    Recover,
    UseObject,
}

/// <summary>One event entering the consequence engine. Player-led and ambient events share this shape.</summary>
public sealed class NpcStimulus
{
    public required string Id;
    public required NpcStimulusKind Kind;
    public required Vector3 Position;
    public float Intensity = 1f;
    public bool PlayerLed;
    public string Subject = "";
    public string Description = "";
    public string ObjectId = "";
    public string ObjectDepartment = "";
    public OfficeObjectType? ObjectType;
    public OfficeObjectState? ObjectState;
    public float StressDelta;
    public float ComfortDelta;
    public NpcReactionAction PreferredAction = NpcReactionAction.Observe;
    public object? EvidenceRef;
    public EvidenceKind? EvidenceKind;
    public float Radius = 18f;
    /// <summary>The NPC actively using the object; this user may react outside the ambient radius.</summary>
    public NpcBrain? ActiveUser;
    public NpcBrain? Source;
}

public enum ToastKind { Info, Warn, Chaos, Success }

public enum ChannelMode { None, Terminal, Mop, Coffee, Microwave, Tape, Photo, FloorTransition }

public enum RoomId { Server, Printer, Break, Closet, Reception, MeetingA, MeetingB, Hr, Floor }

/// <summary>XZ-plane axis-aligned box (ported from types.ts AABB).</summary>
public readonly record struct Aabb2(float MinX, float MinZ, float MaxX, float MaxZ)
{
    public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
}

/// <summary>Math helpers (this toolchain's MathF lacks Clamp).</summary>
public static class Util
{
    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
}

public enum HeldItem { None, Laxative, EnergyDrink }

/// <summary>
/// Mutable cross-system event state (single-player pragmatic static).
/// GameMode writes scenario state; AiDirector reads it per tick.
/// </summary>
public static class WorldEvents
{
    public static bool Evacuating;
    public static Vector3 EvacPoint = new(0f, 0f, 17f);
    public static bool CoffeeSpiked;
    public static int SpikeUsesLeft;
    public static bool StinkActive;
    public static Vector3 StinkPos;
}

/// <summary>Live noise event reference for curiosity gone-checks.</summary>
public sealed class NoiseRef
{
    public required Vector3 Pos;
    public bool Expired;
}

public enum MemoryKind { Witness, Rumor, Forged }

/// <summary>One staff member's fallible account of an office incident.</summary>
public sealed class StaffMemory
{
    public required string Subject;
    public required string Incident;
    public required string Narrative;
    public required Vector3 Location;
    public required float Confidence;
    public MemoryKind Kind;
    public bool Shared;
    public float Age;
}

/// <summary>A witness statement assembled from one staff member's imperfect memory.</summary>
public sealed class CaseTestimony
{
    public required string Witness;
    public required string Suspect;
    public required string Statement;
    public required string LocationClaim;
    public required float Confidence;
    public bool Contradictory;
    public bool Challenged;
    public bool Coached;
}

/// <summary>Zone ids usable in chat/email directives, mapped to points by GameMode.</summary>
public static class DirectiveZones
{
    public static readonly string[] Valid = { "breakroom", "server", "printer", "reception", "closet", "meeting_a", "meeting_b", "hr", "desk", "player" };

    public static bool IsValid(string? z) => z != null && System.Array.IndexOf(Valid, z.ToLowerInvariant()) >= 0;
}



/// <summary>Ported from npc.ts ARCHETYPES.</summary>
public sealed record ArchetypeSpec(
    float Range,      // vision range (m)
    float Fov,        // half-angle in RADIANS
    float Rate,       // suspicion gain multiplier
    float Speed,      // m/s
    bool Reports,     // runs to security after panicking
    string Label,
    Color Tint);

public static class Specs
{
    public static readonly System.Collections.Generic.Dictionary<Archetype, ArchetypeSpec> Table = new()
    {
        [Archetype.Snoop]   = new(16f, 1.30f, 2.0f, 2.6f, true,  "The Snoop",   Color.FromHtml("c03a5a")),
        [Archetype.Gossip]  = new(12f, 1.15f, 1.5f, 2.4f, true,  "The Gossip",  Color.FromHtml("d070c0")),
        [Archetype.Drone]   = new(10f, 1.05f, 1.0f, 2.2f, true,  "Coworker",    Color.FromHtml("4a7dc0")),
        [Archetype.Grifter] = new(10f, 1.05f, 0.6f, 2.3f, false, "The Grifter", Color.FromHtml("9a7a2a")),
        [Archetype.Slob]    = new(6f,  1.20f, 0.5f, 1.8f, true,  "The Slob",    Color.FromHtml("6a8a5a")),
        [Archetype.Guard]   = new(15f, 1.25f, 3.0f, 4.4f, false, "Security",    Color.FromHtml("30343c")),
    };
}

/// <summary>All tunables ported from game.ts top-level consts.</summary>
public static class Bal
{
    // One in-game working day takes one hour of realtime (compressed 9:00-17:00 clock).
    public const float ShiftSeconds = 3600f;
    public const float InteractRange = 2.4f;
    public const float BonkRange = 2.1f;
    public const float BonkCooldown = 0.8f;
    public const float ChannelTime = 3.5f;
    public const float MopTime = 2.2f;
    public const float PhotoCooldown = 30f;
    public const float RagdollSettleSeconds = 2.8f;

    // suspicion model
    public const float SusGainScale = 22f;        // activity * rate * 22 * disguiseMul * dt
    public const float CreepRange = 1.6f;         // standing-too-close radius
    public const float CreepRate = 9f;
    public const float CrabRange = 8f;            // crouch-walk detection radius
    public const float CrabRate = 5f;
    public const float SusDecayPerSec = 2.5f;     // while unseen and sus < 50
    public const float SusDisguiseMul = 0.3f;
    public const float CrouchVisionMul = 0.65f;

    // bonk witnesses
    public const float WitnessAutoSeeDist = 4f;   // within 4m they saw it regardless of LOS
    public const float WitnessWakeSus = 100f;
    public const float WitnessSus = 70f;

    // curiosity -> panic -> report chain
    public const float PanicDurationBody = 8f;
    public const float PanicDurationBlood = 4.5f;
    public const float CuriousSpeedMul = 0.9f;
    public const float ReportSpeedMul = 1.7f;
    public const float BloodSeenRangeMul = 0.8f;
    public const float VanishInvestigateSus = 12f;
    public const float VanishPanicSus = 40f;
    public const float MoppedStainSus = 35f;
    public const float GossipTriggerSus = 60f;
    public const float GossipRadius = 9f;
    public const float GossipSpreadAmount = 30f;

    // guard
    public const float GuardCatchDist = 1.5f;
    public const float GuardGiveUpLostSight = 6f;
    public const float GuardGiveUpArrived = 4f;
    public const float GuardAlertOnReport = 20f;
    public const float GuardAlertSeesCarry = 15f;
    public const float GuardAlertFindsBody = 14f;
    public const float GuardAlertFindsBlood = 12f;
    public const float GuardPatrolSpeed = 2.0f;
    public const float GuardHuntVisionMul = 1.15f;

    // photocopy gag
    public const float PhotoDistractRadius = 12f;
    public const float PhotoDistractSeconds = 7f;

    // carrying
    public const float CarrySpeed = 2.9f;
    public const float CrouchSpeed = 2.2f;
    public const float WalkSpeed = 4.6f;
    public const float DripDistance = 0.9f;

    // misc
    public const float ArrivalDist = 0.35f;
    public const float PlayerRadius = 0.35f;
    public const float NpcRadius = 0.3f;
    public const int MaxSplats = 90;
}

/// <summary>Legacy compatibility shape. Runtime spawning uses CanonicalStaff.Assignments.</summary>
public sealed record CoworkerDef(string Name, Archetype Arch, float X, float Z, string Zone);

public static class Roster
{
    // Compatibility projection for old mission/debug callers; never use for spawning.
    public static readonly CoworkerDef[] Coworkers =
    {
        new("Bob",          Archetype.Drone,   -5f,  -1f, "drone"),
        new("Sleepy Steve", Archetype.Slob,    -10f,  4f, "drone"),
        new("Pam",          Archetype.Gossip,   10f,  4f, "gossip"),
        new("Mr Purple",    Archetype.Snoop,     0f, 11f, "snoop"),
        new("Fran",         Archetype.Drone,     5f, -1f, "drone"),
        new("Chad",         Archetype.Grifter,  10f,  1f, "grifter"),
        new("Rita",         Archetype.Gossip,    0f, 17f, "gossip"),
        new("Mailroom Mike",Archetype.Drone,   -20f,  4f, "drone"),
        new("Dave",         Archetype.Drone,   -16f,  1f, "drone"),
        new("Liz",          Archetype.Gossip,    15f, -5f, "gossip"),
        new("Nervous Ned",  Archetype.Snoop,    -5f,  8f, "snoop"),
        new("Manager Mo",   Archetype.Drone,     5f,  8f, "drone"),
        new("Jen",          Archetype.Drone,     5f,  4f, "drone"),
        new("Data Dave",    Archetype.Snoop,   -10f, -1f, "snoop"),
        new("Boring Bill",  Archetype.Drone,    15f,  4f, "drone"),
        new("Boss Barbara", Archetype.Snoop,     0f,  4f, "snoop"),
        new("Joe",          Archetype.Drone,    26f, 11f, "drone"),
        new("Kevin",        Archetype.Drone,    20f, -5f, "drone"),
        new("Old Tom",      Archetype.Grifter, -20f, 11f, "grifter"),
    };

    public const string GuardName = CanonicalStaff.ExecutiveThreatName;
}

public static class Flavor
{
    /// <summary>Ambient chatter lines, ported from game.ts AMBIENT_LINES ('{name}' placeholder).</summary>
    public static readonly string[] AmbientLines =
    {
        "{name} is microwaving fish in the break room. The audacity.",
        "{name} just said \"circle back\" eleven times in one sentence.",
        "{name} is rage-typing an email about the thermostat.",
        "{name} has been \"in a meeting\" for three hours. The meeting is solitaire.",
        "{name} labeled their yogurt. It is plain yogurt. Nobody wants it.",
        "Someone ate {name}'s lunch. HR is \"looking into it\".",
        "{name} is explaining crypto to the vending machine.",
        "{name} scheduled a meeting about there being too many meetings.",
        "{name} printed 200 pages of a PDF they will never read.",
        "The office plant died. {name} is holding a small funeral.",
        "{name} is chewing loudly. Morale has never been lower.",
        "{name} just replied-all to the entire company. Chaos reigns.",
        "{name} put \"synergy\" on the whiteboard and underlined it twice.",
        "{name} is watching a tutorial on how to look busy.",
    };
}

/// <summary>Runtime hide-spot state (defs live in WorldData; occupants mutate at runtime).</summary>
public sealed class HideSpotState
{
    public required string Id;
    public required string Name;
    public required string Action;
    public required Vector3 Pos;
    public required int Capacity;
    public required float SmellDelay;
    public System.Collections.Generic.List<string> Occupants { get; } = new();

    public bool HasRoom => Occupants.Count < Capacity;
}






