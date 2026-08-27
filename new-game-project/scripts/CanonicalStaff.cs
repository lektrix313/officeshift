using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum StaffObservationChannel
{
    Numbers, Technology, HumanResources, ExecutivePresence, Finance, IdentityAndVisitors,
    DeliveriesAndRoutes, Documentation, VisualEvidence, PanicAndRumor, GossipDrive,
    MeetingsAndTime, CalendarsAndAccess, NetworkPatterns, ConversationTiming,
    InconsistentStories, MaintenanceAndBackRoutes, Inventory, InstitutionalMemory,
}

public sealed record StaffGameplayProfile(
    string Name, string Job, string Department, string AppearanceHook, string BehavioralHook,
    StaffObservationChannel PrimaryChannel, StaffObservationChannel SecondaryChannel,
    WorkdayMovementStyle Movement, float DeskShare, float SocialDrive, float BathroomNeed,
    float SnackNeed, float CoffeeNeed, float SuspicionSensitivity, float Forgiveness,
    float GossipDrive, float WorkDiscipline, float ConversationHazard, string UsefulAccess,
    string RPGHook);

public sealed record CanonicalStaffAssignment(
    StaffGameplayProfile Profile,
    Archetype Archetype,
    Vector3 SpawnPosition,
    string Zone,
    bool IsExecutiveThreat = false);

public static class CanonicalStaff
{
    public const string PlayerName = "Agent Red";
    public const string ExecutiveThreatName = "Mr Purple";

    private static StaffGameplayProfile P(string name, string job, string department, string appearance, string behavior,
        StaffObservationChannel primary, StaffObservationChannel secondary, WorkdayMovementStyle movement,
        float desk, float social, float bathroom, float snack, float coffee, float sensitivity, float forgiveness,
        float gossip, float discipline, float conversation, string access, string hook) =>
        new(name, job, department, appearance, behavior, primary, secondary, movement, desk, social, bathroom,
            snack, coffee, sensitivity, forgiveness, gossip, discipline, conversation, access, hook);

    public static readonly IReadOnlyList<CanonicalStaffAssignment> Assignments = new[]
    {
        A(P("Bob", "Accounting", "Accounting", "green, short, round, moustache, pens in pocket", "friendly spreadsheet bore", StaffObservationChannel.Numbers, StaffObservationChannel.ConversationTiming, WorkdayMovementStyle.DeskAnchor, .82f, .48f, .25f, .4f, .5f, .9f, .8f, .35f, .95f, .8f, "expense records", "Numbers that do not add up become a mystery."), Archetype.Drone, -5f, -1f, "accounting"),
        A(P("Sleepy Steve", "IT Support", "IT", "permanently exhausted", "knows every password and workaround", StaffObservationChannel.Technology, StaffObservationChannel.NetworkPatterns, WorkdayMovementStyle.CoffeeSeeker, .5f, .25f, .3f, .2f, .95f, .65f, .55f, .2f, .35f, .25f, "systems and backdoors", "Coffee wakes him up; favors buy access."), Archetype.Slob, -10f, 4f, "it"),
        A(P("Pam", "HR Manager", "HR", "cheerful and immaculate", "turns catastrophes into learning opportunities", StaffObservationChannel.HumanResources, StaffObservationChannel.GossipDrive, WorkdayMovementStyle.SocialButterfly, .55f, .78f, .25f, .15f, .35f, .7f, .65f, .9f, .75f, .5f, "HR cases and secrets", "Can make a report official—or public."), Archetype.Gossip, 10f, 4f, "hr"),
        A(P("Mr Purple", "CEO", "Executive", "stern purple executive presence", "believes he is smartest in every room", StaffObservationChannel.ExecutivePresence, StaffObservationChannel.InconsistentStories, WorkdayMovementStyle.Sentinel, .2f, .35f, .2f, .15f, .25f, 1.15f, .25f, .15f, .9f, .55f, "executive floor and directives", "Employees change behavior when he appears."), Archetype.Guard, 0f, 11f, "executive", true),
        A(P("Fran", "Finance Controller", "Finance", "sharp and organised", "quietly catches discrepancies", StaffObservationChannel.Finance, StaffObservationChannel.Numbers, WorkdayMovementStyle.DeskAnchor, .85f, .22f, .25f, .15f, .3f, 1f, .35f, .2f, .98f, .25f, "budgets and approvals", "Claims can be checked against records."), Archetype.Drone, 5f, -1f, "finance"),
        A(P("Chad", "Sales Executive", "Sales", "loud and confident", "thinks everyone is his mate", StaffObservationChannel.ConversationTiming, StaffObservationChannel.MeetingsAndTime, WorkdayMovementStyle.SocialButterfly, .25f, .98f, .3f, .45f, .75f, .35f, .75f, .55f, .45f, .4f, "sales floor", "A mobile conversation distraction."), Archetype.Grifter, 10f, 1f, "sales"),
        A(P("Rita", "Receptionist", "Reception", "friendly and chatty", "knows exactly who enters and leaves", StaffObservationChannel.IdentityAndVisitors, StaffObservationChannel.GossipDrive, WorkdayMovementStyle.SocialButterfly, .4f, .9f, .3f, .25f, .65f, 1f, .45f, .95f, .6f, .55f, "visitor logs and badges", "Disguises are her specialty."), Archetype.Gossip, 0f, 17f, "reception"),
        A(P("Mailroom Mike", "Mailroom Coordinator", "Mailroom", "quiet and constantly moving", "knows deliveries, keys, and back routes", StaffObservationChannel.DeliveriesAndRoutes, StaffObservationChannel.Inventory, WorkdayMovementStyle.ErrandRunner, .2f, .3f, .25f, .25f, .2f, .7f, .6f, .45f, .65f, .2f, "deliveries and keys", "Can move packages unnoticed."), Archetype.Drone, -20f, 4f, "mailroom"),
        A(P("Dave", "Legal Counsel", "Legal", "serious and literal", "interrogates wording and documentation", StaffObservationChannel.Documentation, StaffObservationChannel.InconsistentStories, WorkdayMovementStyle.DeskAnchor, .78f, .25f, .2f, .15f, .25f, 1f, .3f, .15f, .8f, .7f, "contracts and policies", "Precise lies work better."), Archetype.Drone, -16f, 1f, "legal"),
        A(P("Liz", "Marketing", "Marketing", "energetic and image-conscious", "turns incidents into content", StaffObservationChannel.VisualEvidence, StaffObservationChannel.GossipDrive, WorkdayMovementStyle.Fidgeter, .35f, .85f, .3f, .35f, .8f, .8f, .55f, .8f, .55f, .45f, "cameras and launches", "May create evidence or distraction."), Archetype.Gossip, 15f, -5f, "marketing"),
        A(P("Nervous Ned", "Security Trainee", "Security", "grey and terrified", "assumes every noise is a breach", StaffObservationChannel.PanicAndRumor, StaffObservationChannel.IdentityAndVisitors, WorkdayMovementStyle.Fidgeter, .3f, .2f, .35f, .2f, .4f, 1.1f, .3f, .85f, .45f, .25f, "security desk", "Raises false alarms; some are right."), Archetype.Snoop, -5f, 8f, "security"),
        A(P("Manager Mo", "Department Manager", "Operations", "middle-management incarnate", "loves meetings and quick words", StaffObservationChannel.MeetingsAndTime, StaffObservationChannel.CalendarsAndAccess, WorkdayMovementStyle.Fidgeter, .35f, .62f, .25f, .2f, .35f, .8f, .45f, .5f, .75f, 1.1f, "meetings and calendars", "Can trap Red in conversation."), Archetype.Drone, 5f, 8f, "operations"),
        A(P("Jen", "Administrator", "Administration", "warm and competent", "knows how the office functions", StaffObservationChannel.CalendarsAndAccess, StaffObservationChannel.DeliveriesAndRoutes, WorkdayMovementStyle.ErrandRunner, .58f, .55f, .3f, .25f, .55f, .75f, .7f, .55f, .9f, .35f, "calendars, passes, and paperwork", "Best source of legitimate excuses."), Archetype.Drone, 5f, 4f, "administration"),
        A(P("Data Dave", "Data Analyst", "IT", "hardcore office nerd", "spots network patterns", StaffObservationChannel.NetworkPatterns, StaffObservationChannel.Numbers, WorkdayMovementStyle.DeskAnchor, .9f, .18f, .2f, .15f, .3f, 1.05f, .35f, .1f, 1f, .2f, "databases and logs", "Sees patterns across incidents."), Archetype.Snoop, -10f, -1f, "it"),
        A(P("Boring Bill", "Process Analyst", "Operations", "spectacularly dull", "never reaches the point", StaffObservationChannel.ConversationTiming, StaffObservationChannel.MeetingsAndTime, WorkdayMovementStyle.DeskAnchor, .75f, .2f, .3f, .2f, .3f, .45f, .85f, .35f, .6f, 1.35f, "process documents", "A human timing hazard."), Archetype.Drone, 15f, 4f, "operations"),
        A(P("Boss Barbara", "Senior Manager", "Executive", "calm and polished", "detects inconsistent stories", StaffObservationChannel.InconsistentStories, StaffObservationChannel.HumanResources, WorkdayMovementStyle.ErrandRunner, .55f, .45f, .2f, .15f, .25f, 1.1f, .25f, .25f, .9f, .8f, "management reviews", "Tests cover stories."), Archetype.Snoop, 0f, 4f, "executive"),
        A(P("Joe", "Janitor", "Janitorial", "relaxed and invisible", "has keys and knows every back corridor", StaffObservationChannel.MaintenanceAndBackRoutes, StaffObservationChannel.IdentityAndVisitors, WorkdayMovementStyle.ErrandRunner, .15f, .3f, .45f, .3f, .2f, .65f, .8f, .25f, .5f, .2f, "master keys and disposal", "Usually ignores Red unless inconvenienced."), Archetype.Drone, 26f, 11f, "janitorial"),
        A(P("Kevin", "Procurement Manager", "Procurement", "red-orange and surprised", "tracks everything bought", StaffObservationChannel.Inventory, StaffObservationChannel.Finance, WorkdayMovementStyle.Fidgeter, .55f, .35f, .3f, .3f, .35f, .95f, .45f, .25f, .75f, .3f, "inventory and equipment", "Notices missing gadgets."), Archetype.Drone, 20f, -5f, "procurement"),
        A(P("Old Tom", "Senior Advisor", "Executive", "apparently retiring forever", "knows forgotten scandals and routes", StaffObservationChannel.InstitutionalMemory, StaffObservationChannel.InconsistentStories, WorkdayMovementStyle.SocialButterfly, .35f, .7f, .25f, .25f, .35f, .8f, .85f, .75f, .55f, .9f, "old files and routes", "Remembers what security forgot."), Archetype.Grifter, -20f, 11f, "executive"),
    };

    public static IReadOnlyList<StaffGameplayProfile> Npcs => Assignments.Select(assignment => assignment.Profile).ToArray();
    public static int TotalStaffCount => Assignments.Count + 1; // Agent Red plus coworkers
    public static int CoworkerCount => Assignments.Count;
    public static bool IsValid => RosterInvariantChecks.Validate().Count == 0;
    public static string ValidationSummary => string.Join(" | ", RosterInvariantChecks.Validate());
    public static CanonicalStaffAssignment? Find(string name) => Assignments.FirstOrDefault(a => a.Profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    public static StaffGameplayProfile For(string name) => Find(name)?.Profile ??
        new StaffGameplayProfile(name, "Employee", "General", "ordinary", "keeps working", StaffObservationChannel.ConversationTiming, StaffObservationChannel.MeetingsAndTime, WorkdayMovementStyle.Fidgeter, .5f, .5f, .3f, .3f, .3f, .75f, .5f, .5f, .5f, .5f, "ordinary office access", "No special hook yet.");

    private static CanonicalStaffAssignment A(StaffGameplayProfile profile, Archetype archetype, float x, float z, string zone, bool threat = false) =>
        new(profile, archetype, new Vector3(x, 0f, z), zone, threat);
}
