using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum OfficeWaypointTag
{
    Desk,
    Meeting,
    Social,
    Quiet,
    Coffee,
    Snack,
    Toilet,
    Printer,
    Server,
    Reception,
    Maintenance,
    Department,
    Cover,
}

public sealed record OfficeWaypoint(
    string Id,
    string Zone,
    Vector3 Position,
    IReadOnlySet<OfficeWaypointTag> Tags,
    int Capacity = 4,
    float Visibility = 0.5f,
    float SocialValue = 0.5f,
    float CoverValue = 0.5f);

public enum NpcAutonomousAction
{
    Work,
    Wander,
    Coffee,
    Snack,
    Toilet,
    Print,
    Gossip,
    Help,
    CheckObject,
    CallMeeting,
    Recover,
}

public sealed record NpcActionChoice(
    NpcAutonomousAction Action,
    string DestinationTag,
    float Score,
    float DurationSeconds,
    bool CreatesEvidence = false);

public sealed class NpcNeeds
{
    public float Social { get; private set; } = 0.25f;
    public float Coffee { get; private set; } = 0.2f;
    public float Snack { get; private set; } = 0.15f;
    public float Toilet { get; private set; } = 0.1f;
    public float Boredom { get; private set; } = 0.2f;

    public void Tick(float seconds, StaffGameplayProfile profile, bool isWorking)
    {
        float scale = seconds / 60f;
        Social = Util.Clamp(Social + scale * (0.12f + profile.SocialDrive * 0.2f), 0f, 1f);
        Coffee = Util.Clamp(Coffee + scale * profile.CoffeeNeed * 0.16f, 0f, 1f);
        Snack = Util.Clamp(Snack + scale * profile.SnackNeed * 0.12f, 0f, 1f);
        Toilet = Util.Clamp(Toilet + scale * profile.BathroomNeed * 0.1f, 0f, 1f);
        Boredom = Util.Clamp(Boredom + scale * (isWorking ? 0.08f : 0.02f), 0f, 1f);
    }

    public void Satisfy(NpcAutonomousAction action)
    {
        switch (action)
        {
            case NpcAutonomousAction.Coffee: Coffee *= 0.12f; break;
            case NpcAutonomousAction.Snack: Snack *= 0.1f; break;
            case NpcAutonomousAction.Toilet: Toilet *= 0.08f; break;
            case NpcAutonomousAction.Gossip: Social *= 0.35f; Boredom *= 0.45f; break;
            case NpcAutonomousAction.Work: Boredom *= 0.7f; break;
            case NpcAutonomousAction.Help: Social *= 0.55f; Boredom *= 0.6f; break;
            case NpcAutonomousAction.Recover: Boredom *= 0.5f; break;
        }
    }
}

public sealed class NpcRelationship
{
    public string A { get; }
    public string B { get; }
    public float Trust { get; private set; }
    public float Chemistry { get; }
    public float Friction { get; }
    public float InteractionCooldown { get; private set; }
    public string LastInteraction { get; private set; } = "none";

    public NpcRelationship(string a, string b, int seed)
    {
        A = a;
        B = b;
        Chemistry = Stable01(seed, a, b) * 2f - 1f;
        Friction = Stable01(seed + 31, b, a);
        Trust = 0.35f + Stable01(seed + 73, a, b) * 0.3f;
    }

    public bool Ready => InteractionCooldown <= 0f;

    public void Tick(float dt) => InteractionCooldown = MathF.Max(0f, InteractionCooldown - dt);

    public void ApplyInteraction(string kind, float trustDelta, float cooldownSeconds)
    {
        Trust = Util.Clamp(Trust + trustDelta, 0f, 1f);
        InteractionCooldown = cooldownSeconds;
        LastInteraction = kind;
    }

    private static float Stable01(int seed, string left, string right)
    {
        unchecked
        {
            int hash = seed;
            foreach (char c in left) hash = hash * 31 + c;
            foreach (char c in right) hash = hash * 31 + c;
            return (hash & 0x7fffffff) / (float)int.MaxValue;
        }
    }
}

public sealed class OfficeChaosBudget
{
    public float Current { get; private set; }
    public float Capacity { get; }
    public float RecoveryPerSecond { get; }

    public OfficeChaosBudget(float capacity = 6f, float recoveryPerSecond = 0.025f)
    {
        Capacity = MathF.Max(1f, capacity);
        RecoveryPerSecond = MathF.Max(0f, recoveryPerSecond);
    }

    public void Tick(float dt) => Current = MathF.Max(0f, Current - RecoveryPerSecond * dt);
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (Current + amount > Capacity) return false;
        Current += amount;
        return true;
    }
}

public sealed class SocialSimulation
{
    private readonly Dictionary<string, NpcNeeds> _needs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NpcRelationship> _relationships = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<OfficeWaypoint> _waypoints = new();
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<string> _registeredNames = new();
    private float _decisionTimer;
    private int _decisionSerial;
    private int _seed;

    public void SetSeed(int seed)
    {
        _seed = seed;
        _rng.Seed = (ulong)(uint)seed;
    }

    public OfficeChaosBudget Chaos { get; } = new();
    public float SurpriseBudget { get; private set; } = 1f;
    public IReadOnlyList<OfficeWaypoint> Waypoints => _waypoints;
    public IReadOnlyDictionary<string, NpcNeeds> Needs => _needs;
    public IReadOnlyDictionary<string, NpcRelationship> Relationships => _relationships;

    public void AddWaypoint(OfficeWaypoint waypoint)
    {
        int existingIndex = _waypoints.FindIndex(existing => existing.Id.Equals(waypoint.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0) _waypoints[existingIndex] = waypoint;
        else _waypoints.Add(waypoint);
    }

    public void AddWorkshopWaypoint(WorkshopWaypointData waypoint, float worldScale = 2f, float originX = -28f, float originZ = -20f)
    {
        var tags = new HashSet<OfficeWaypointTag>();
        foreach (string tag in waypoint.Tags)
            if (Enum.TryParse<OfficeWaypointTag>(tag, true, out var parsed)) tags.Add(parsed);
        AddWaypoint(new OfficeWaypoint(waypoint.Id, waypoint.FloorId,
            new Vector3(originX + waypoint.X * worldScale, 0f, originZ + waypoint.Y * worldScale), tags,
            waypoint.Capacity, waypoint.Visibility, waypoint.SocialValue, waypoint.CoverValue));
    }

    public void AddDefaultWaypoints()
    {
        AddWaypoint(new OfficeWaypoint("coffee", "break", new Vector3(14f, 0f, -20.8f), Tags(OfficeWaypointTag.Coffee, OfficeWaypointTag.Social), 3, .8f, .95f, .2f));
        AddWaypoint(new OfficeWaypoint("snack", "break", new Vector3(17f, 0f, -20.8f), Tags(OfficeWaypointTag.Snack, OfficeWaypointTag.Social), 2, .8f, .7f, .2f));
        AddWaypoint(new OfficeWaypoint("toilet", "facilities", new Vector3(24f, 0f, 11f), Tags(OfficeWaypointTag.Toilet, OfficeWaypointTag.Quiet), 2, .35f, .1f, .65f));
        AddWaypoint(new OfficeWaypoint("printer", "floor", new Vector3(-27f, 0f, -10.5f), Tags(OfficeWaypointTag.Printer, OfficeWaypointTag.Department), 2, .75f, .35f, .15f));
        AddWaypoint(new OfficeWaypoint("server", "it", new Vector3(-22f, 0f, -18f), Tags(OfficeWaypointTag.Server, OfficeWaypointTag.Quiet, OfficeWaypointTag.Cover), 2, .25f, .1f, .85f));
        AddWaypoint(new OfficeWaypoint("meeting_a", "meetings", new Vector3(-8f, 0f, -2f), Tags(OfficeWaypointTag.Meeting, OfficeWaypointTag.Department), 8, .55f, .8f, .35f));
        AddWaypoint(new OfficeWaypoint("meeting_b", "meetings", new Vector3(8f, 0f, -2f), Tags(OfficeWaypointTag.Meeting, OfficeWaypointTag.Department), 8, .55f, .8f, .35f));
        AddWaypoint(new OfficeWaypoint("reception", "reception", new Vector3(0f, 0f, 17f), Tags(OfficeWaypointTag.Reception, OfficeWaypointTag.Social), 4, .95f, .8f, .05f));
    }

    public void RegisterNpc(string name, int seed)
    {
        if (!_needs.ContainsKey(name))
        {
            _needs[name] = new NpcNeeds();
            _registeredNames.Add(name);
        }
        foreach (string other in _registeredNames)
        {
            if (other.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            string id = RelationshipId(name, other);
            if (!_relationships.ContainsKey(id)) _relationships[id] = new NpcRelationship(name, other, seed);
        }
    }

    public void Tick(IReadOnlyList<NpcBrain> npcs, float dt, float currentHour)
    {
        Chaos.Tick(dt);
        SurpriseBudget = Util.Clamp(SurpriseBudget + dt / 180f, 0f, 1f);
        for (int index = 0; index < npcs.Count; index++)
        {
            var npc = npcs[index];
            RegisterNpc(npc.NpcName, index + 17);
            _needs[npc.NpcName].Tick(dt, npc.StaffProfile, npc.WorkState == WorkdayState.WorkingAtDesk);
        }
        foreach (var relationship in _relationships.Values) relationship.Tick(dt);
        foreach (var npc in npcs) npc.TickAutonomousAction(dt);
        _decisionTimer -= dt;
        if (_decisionTimer > 0f) return;
        _decisionTimer = 5f;
        MakeBoundedInteraction(npcs, currentHour);
        foreach (var npc in npcs.Where(n => n.Awake && n.AutonomousActionTimer <= 0f))
            TryChooseAndApply(npc, currentHour);
    }

    public NpcActionChoice ChooseAction(NpcBrain npc, float currentHour)
    {
        var profile = npc.StaffProfile;
        var needs = _needs.TryGetValue(npc.NpcName, out var found) ? found : new NpcNeeds();
        var choices = new List<NpcActionChoice>
        {
            Score(npc, NpcAutonomousAction.Work, "desk", profile.DeskShare * 1.8f + profile.WorkDiscipline, 45f),
            Score(npc, NpcAutonomousAction.Wander, npc.Zone, profile.SocialDrive * .65f + needs.Boredom * .7f, 24f),
            Score(npc, NpcAutonomousAction.Coffee, "coffee", profile.CoffeeNeed * 1.2f + needs.Coffee * 1.8f, 22f),
            Score(npc, NpcAutonomousAction.Snack, "snack", profile.SnackNeed * 1.1f + needs.Snack * 1.7f, 18f),
            Score(npc, NpcAutonomousAction.Toilet, "toilet", profile.BathroomNeed * .9f + needs.Toilet * 1.8f, 20f),
            Score(npc, NpcAutonomousAction.Gossip, "coffee", profile.GossipDrive * .9f + needs.Social * 1.2f, 26f),
            Score(npc, NpcAutonomousAction.Print, "printer", profile.Job.Contains("Accounts", StringComparison.OrdinalIgnoreCase) ? 1.15f : .3f, 16f),
            Score(npc, NpcAutonomousAction.Help, "department", profile.Forgiveness * .35f + profile.SocialDrive * .4f, 20f),
        };

        // Authored routine beats are the anchor. Autonomous choices only fill gaps
        // and may win when their utility is clearly higher than ordinary desk work.
        var authoredBeat = npc.WorkProfile.Beats.FirstOrDefault(beat => currentHour >= beat.StartHour && currentHour < beat.EndHour);
        if (authoredBeat != null)
        {
            var authoredAction = ActionForState(npc.WorkState);
            if (authoredAction.HasValue)
                return new NpcActionChoice(authoredAction.Value, authoredBeat.Destination, 3f, MathF.Max(8f, (authoredBeat.EndHour - currentHour) * 450f));
        }
        float best = choices.Max(choice => choice.Score);
        var tied = choices.Where(choice => choice.Score >= best - .12f).ToList();
        return tied[_rng.RandiRange(0, tied.Count - 1)];
    }

    private static NpcAutonomousAction? ActionForState(WorkdayState state) => state switch
    {
        WorkdayState.CoffeeBreak => NpcAutonomousAction.Coffee,
        WorkdayState.StationaryUse => NpcAutonomousAction.Snack,
        WorkdayState.Toilet => NpcAutonomousAction.Toilet,
        WorkdayState.Printing or WorkdayState.WalkingToPrinter => NpcAutonomousAction.Print,
        WorkdayState.WaterCooler => NpcAutonomousAction.Gossip,
        WorkdayState.Meeting or WorkdayState.MeetingWalk => NpcAutonomousAction.CallMeeting,
        WorkdayState.WorkingAtDesk or WorkdayState.Reading => NpcAutonomousAction.Work,
        _ => null,
    };

    public bool TryChooseAndApply(NpcBrain npc, float currentHour)
    {
        var choice = ChooseAction(npc, currentHour);
        if (!Chaos.TrySpend(choice.Action is NpcAutonomousAction.Gossip or NpcAutonomousAction.Help ? .04f : .01f))
            return false;
        npc.ApplyAutonomousChoice(choice, ResolveWaypoint(choice.DestinationTag, npc));
        _needs[npc.NpcName].Satisfy(choice.Action);
        SurpriseBudget = MathF.Max(0f, SurpriseBudget - (choice.Action == NpcAutonomousAction.Gossip ? .04f : .01f));
        return true;
    }

    private Vector3 ResolveWaypoint(string destination, NpcBrain npc)
    {
        var candidates = _waypoints.Where(point => point.Id.Equals(destination, StringComparison.OrdinalIgnoreCase) ||
                                                   point.Zone.Equals(destination, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0) return npc.HomePos;
        return candidates[_rng.RandiRange(0, candidates.Count - 1)].Position;
    }

    private NpcActionChoice Score(NpcBrain npc, NpcAutonomousAction action, string destination, float utility, float duration)
    {
        float personality = action switch
        {
            NpcAutonomousAction.Gossip => npc.StaffProfile.GossipDrive,
            NpcAutonomousAction.Work => npc.StaffProfile.WorkDiscipline,
            NpcAutonomousAction.Coffee => npc.StaffProfile.CoffeeNeed,
            NpcAutonomousAction.Snack => npc.StaffProfile.SnackNeed,
            NpcAutonomousAction.Toilet => npc.StaffProfile.BathroomNeed,
            _ => .5f,
        };
        float surprise = action is NpcAutonomousAction.Gossip or NpcAutonomousAction.Help or NpcAutonomousAction.CheckObject
            ? SurpriseBudget * .25f
            : 0f;
        return new NpcActionChoice(action, destination, MathF.Max(0f, utility + personality * .25f + surprise), duration);
    }

    private void MakeBoundedInteraction(IReadOnlyList<NpcBrain> npcs, float hour)
    {
        if (npcs.Count < 2 || !Chaos.TrySpend(.08f)) return;
        var candidates = npcs.Where(n => n.Awake && n.Attitude.Kind != NpcAttitudeKind.Afraid).ToList();
        if (candidates.Count < 2) return;
        int first = _rng.RandiRange(0, candidates.Count - 1);
        int second = _rng.RandiRange(0, candidates.Count - 1);
        if (first == second) return;
        var a = candidates[first];
        var b = candidates[second];
        var relationship = GetRelationship(a.NpcName, b.NpcName);
        if (relationship == null || !relationship.Ready) return;
        var needsA = _needs[a.NpcName];
        var needsB = _needs[b.NpcName];
        string kind;
        float trustDelta;
        if (a.StaffProfile.GossipDrive > .7f && relationship.Friction > .55f && needsA.Social > .45f)
        {
            kind = "gossip";
            trustDelta = -.04f;
            a.SetAttitude(NpcAttitudeKind.Curious, .35f, 35f, $"{a.NpcName} gossiping with {b.NpcName}");
        }
        else if (a.StaffProfile.SocialDrive > .65f && relationship.Chemistry > .45f)
        {
            kind = "friendly chat";
            trustDelta = .035f;
            a.SetAttitude(NpcAttitudeKind.Grateful, .25f, 25f, $"friendly chat with {b.NpcName}");
        }
        else
        {
            kind = "minor disagreement";
            trustDelta = -.015f;
            a.SetAttitude(NpcAttitudeKind.Annoyed, .22f, 18f, $"disagreement with {b.NpcName}");
        }
        relationship.ApplyInteraction(kind, trustDelta, 28f);
        needsA.Satisfy(kind == "friendly chat" ? NpcAutonomousAction.Help : NpcAutonomousAction.Gossip);
        needsB.Satisfy(kind == "friendly chat" ? NpcAutonomousAction.Help : NpcAutonomousAction.Gossip);
        b.SetAttitude(kind == "friendly chat" ? NpcAttitudeKind.Grateful : NpcAttitudeKind.Curious,
            .2f, 20f, $"{b.NpcName} interacted with {a.NpcName}");
        _decisionSerial++;
        SurpriseBudget = MathF.Max(0f, SurpriseBudget - .08f);
    }

    public NpcRelationship? GetRelationship(string a, string b) => _relationships.TryGetValue(RelationshipId(a, b), out var result) ? result : null;

    public static string RelationshipId(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";
    private static HashSet<OfficeWaypointTag> Tags(params OfficeWaypointTag[] tags) => new(tags);
}

public static class ProceduralStaffFactory
{
    private static readonly string[] FirstNames = { "Alex", "Casey", "Morgan", "Taylor", "Jordan", "Sam", "Avery", "Robin" };
    private static readonly string[] Jobs = { "Coordinator", "Analyst", "Assistant", "Specialist" };

    public static StaffGameplayProfile CreateReplacement(int seed, string department)
    {
        int index = Math.Abs(seed) % FirstNames.Length;
        float variation = (Math.Abs(seed / FirstNames.Length) % 100) / 100f;
        return new StaffGameplayProfile(
            $"{FirstNames[index]} {Math.Abs(seed) % 10}",
            Jobs[index % Jobs.Length],
            department,
            "procedural replacement with a familiar office silhouette",
            variation > .5f ? "helpful but distractible" : "quietly opportunistic",
            StaffObservationChannel.MeetingsAndTime,
            StaffObservationChannel.ConversationTiming,
            variation > .65f ? WorkdayMovementStyle.SocialButterfly : WorkdayMovementStyle.Fidgeter,
            .45f + variation * .35f,
            .25f + variation * .6f,
            .2f + variation * .35f,
            .15f + variation * .4f,
            .2f + variation * .5f,
            .55f + variation * .25f,
            .45f + variation * .35f,
            .35f + variation * .5f,
            .5f + variation * .3f,
            .25f + variation * .55f,
            "ordinary department access",
            "A procedural coworker inherits local gossip but not canonical secrets.");
    }
}
