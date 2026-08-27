using System;
using System.Collections.Generic;
using System.Linq;

public static class RosterInvariantChecks
{
    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var assignments = CanonicalStaff.Assignments;
        var names = assignments.Select(a => a.Profile.Name).ToList();

        if (assignments.Count != 19) errors.Add($"Expected 19 coworkers, found {assignments.Count}.");
        if (CanonicalStaff.TotalStaffCount != 20) errors.Add($"Expected 20 total staff, found {CanonicalStaff.TotalStaffCount}.");
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count) errors.Add("Canonical roster contains duplicate names.");
        if (assignments.Count(a => a.IsExecutiveThreat) != 1) errors.Add("Expected exactly one executive threat.");
        if (!assignments.Any(a => a.Profile.Name == CanonicalStaff.ExecutiveThreatName && a.Archetype == Archetype.Guard)) errors.Add("Mr Purple is not the canonical guard assignment.");
        if (names.Contains(CanonicalStaff.PlayerName, StringComparer.OrdinalIgnoreCase)) errors.Add("Agent Red must not be spawned as an NPC.");
        foreach (var assignment in assignments)
        {
            if (!ReferenceEquals(assignment.Profile, CanonicalStaff.For(assignment.Profile.Name))) errors.Add($"Profile lookup mismatch for {assignment.Profile.Name}.");
            if (string.IsNullOrWhiteSpace(assignment.Zone)) errors.Add($"Missing zone for {assignment.Profile.Name}.");
        }
        return errors;
    }
}
