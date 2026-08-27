using System;
using System.Collections.Generic;

public enum WorkdayMovementStyle { DeskAnchor, Fidgeter, SocialButterfly, ErrandRunner, SnackSeeker, CoffeeSeeker, Sentinel }
public sealed record WorkdayBeat(string Id, float StartHour, float EndHour, WorkdayState State, string Destination, bool DepartmentEvent = false);
public sealed record WorkerProfile(string Name, string Job, string Department, WorkdayMovementStyle Movement, string HomeZone, float DeskShare, float SocialDrive, float BathroomNeed, float SnackNeed, float CoffeeNeed, IReadOnlyList<WorkdayBeat> Beats);
public enum BossDifficulty { Easy, Standard, Hard }

public sealed class BossBehaviorProfile
{
    public BossDifficulty Difficulty { get; }
    public float DeskShare { get; }
    public float PatrolShare => 1f - DeskShare;
    public float HintRadius { get; }
    public float HintDelay { get; }
    public float SearchDuration { get; }
    public BossBehaviorProfile(BossDifficulty difficulty)
    {
        Difficulty = difficulty;
        (DeskShare, HintRadius, HintDelay, SearchDuration) = difficulty switch
        {
            BossDifficulty.Easy => (0.8f, 14f, 4f, 8f),
            BossDifficulty.Hard => (0.2f, 9f, 1.5f, 14f),
            _ => (0.5f, 12f, 2.5f, 10f),
        };
    }
}

public static class WorkdayBalance
{
    public const float WorkdayStartHour = 9f;
    public const float WorkdayEndHour = 17f;
    public const float MeetingDurationHours = 0.5f;
    public const float AreaHintConfidence = 0.65f;
    public const float BossDeskToleranceSeconds = 22f;
    public const float BossHintMemorySeconds = 18f;
}

public static class CanonicalWorkdayProfiles
{
    private static WorkdayBeat Beat(string id, float start, float end, WorkdayState state, string destination, bool group = false) => new(id, start, end, state, destination, group);

    public static WorkerProfile For(string name)
    {
        var assignment = CanonicalStaff.Find(name);
        if (assignment == null) return new WorkerProfile(name, "Employee", "General", WorkdayMovementStyle.Fidgeter, "floor", .5f, .5f, .3f, .3f, .3f, Array.Empty<WorkdayBeat>());
        var p = assignment.Profile;
        var beats = BuildBeats(p);
        return new WorkerProfile(p.Name, p.Job, p.Department, p.Movement, assignment.Zone, p.DeskShare, p.SocialDrive, p.BathroomNeed, p.SnackNeed, p.CoffeeNeed, beats);
    }

    private static IReadOnlyList<WorkdayBeat> BuildBeats(StaffGameplayProfile p)
    {
        var beats = new List<WorkdayBeat>();
        if (p.Name == "Mr Purple")
        {
            beats.Add(Beat("executive-round", 9.5f, 10.2f, WorkdayState.WalkingThinking, "executive"));
            beats.Add(Beat("executive-desk", 10.2f, 10.8f, WorkdayState.WorkingAtDesk, "desk"));
            beats.Add(Beat("executive-round", 10.8f, 12.2f, WorkdayState.WalkingThinking, "floor"));
            beats.Add(Beat("boardroom", 13f, 14f, WorkdayState.Meeting, "meeting_a", true));
            beats.Add(Beat("executive-round", 14f, 16.5f, WorkdayState.WalkingThinking, "floor"));
            return beats;
        }
        if (p.Movement == WorkdayMovementStyle.DeskAnchor)
        {
            beats.Add(Beat("morning-work", 9.2f, 11.3f, WorkdayState.WorkingAtDesk, "desk"));
            beats.Add(Beat("lunch", 12f, 12.5f, WorkdayState.OnBreak, "coffee"));
            beats.Add(Beat("afternoon-work", 13f, 16.2f, WorkdayState.WorkingAtDesk, "desk"));
        }
        else if (p.Movement == WorkdayMovementStyle.CoffeeSeeker)
        {
            beats.Add(Beat("morning-work", 9.2f, 10.1f, WorkdayState.WorkingAtDesk, "desk"));
            beats.Add(Beat("coffee", 10.1f, 10.35f, WorkdayState.CoffeeBreak, "coffee"));
            beats.Add(Beat("ticket-round", 10.35f, 12f, WorkdayState.WalkingThinking, "floor"));
            beats.Add(Beat("afternoon-coffee", 14.2f, 14.5f, WorkdayState.CoffeeBreak, "coffee"));
        }
        else if (p.Movement == WorkdayMovementStyle.SnackSeeker)
        {
            beats.Add(Beat("morning-work", 9.2f, 11.4f, WorkdayState.WorkingAtDesk, "desk"));
            beats.Add(Beat("snack", 11.4f, 11.7f, WorkdayState.StationaryUse, "snack"));
            beats.Add(Beat("lunch", 12f, 12.5f, WorkdayState.OnBreak, "coffee"));
            beats.Add(Beat("snack", 14.3f, 14.8f, WorkdayState.StationaryUse, "snack"));
        }
        else if (p.Movement == WorkdayMovementStyle.SocialButterfly)
        {
            beats.Add(Beat("morning-social", 9.1f, 9.5f, WorkdayState.Meeting, "meeting_a", true));
            beats.Add(Beat("floor-chat", 10.2f, 11f, WorkdayState.WaterCooler, "coffee"));
            beats.Add(Beat("lunch", 12f, 12.5f, WorkdayState.OnBreak, "coffee"));
            beats.Add(Beat("afternoon-social", 14f, 14.6f, WorkdayState.WalkingThinking, "floor"));
        }
        else
        {
            beats.Add(Beat("morning-work", 9.2f, 10.2f, WorkdayState.WorkingAtDesk, "desk"));
            beats.Add(Beat("round", 10.2f, 11.3f, WorkdayState.WalkingThinking, p.Department.ToLowerInvariant()));
            beats.Add(Beat("lunch", 12f, 12.5f, WorkdayState.OnBreak, "coffee"));
            beats.Add(Beat("afternoon-round", 14f, 15.3f, WorkdayState.WalkingThinking, p.Department.ToLowerInvariant()));
        }
        return beats;
    }
}

// Compatibility facade for older callers. New code must use CanonicalWorkdayProfiles.
public static class WorkerProfiles
{
    public static int StartingStaffCount => CanonicalStaff.CoworkerCount;
    public static WorkerProfile For(string name) => CanonicalWorkdayProfiles.For(name);
}
