using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Tracks bodies hidden in vents, nooks, and disposal points.
/// Bodies decompose over time, creating a stench that spreads.
/// After 3 game-days, the stench triggers office-wide investigation and shutdown.
/// 
/// Mechanics:
/// - Each hidden body has a hide timestamp and location
/// - Stench radius expands each day (1 cell/day → 3 cells by day 3)
/// - NPCs within stench radius feel Disgusted, suspicious, or sick
/// - Day 3: stench triggers fire alarm investigation → game over if not resolved
/// - Player can move bodies to delay detection
/// - Incinerator destroys bodies instantly (no stench)
/// </summary>
public static class StenchTracker
{
    public const float DayDurationMinutes = 60f; // 1 real minute = 1 game hour
    public const float GameDaysLimit = 3f;
    public const float StenchRadiusPerDay = 1.5f;
    public const float StenchSpreadRate = 0.02f; // radius growth per real second

    public sealed class HiddenBody
    {
        public string NpcName;
        public Vector3 Position;
        public float HideTimestamp;   // game time when hidden
        public float StenchRadius;    // current stench radius in world units
        public bool Discovered;
        public string HideType;       // "vent", "nook", "disposal"

        public HiddenBody(string name, Vector3 pos, float time, string hideType)
        {
            NpcName = name;
            Position = pos;
            HideTimestamp = time;
            HideType = hideType;
            StenchRadius = 0.5f;
            Discovered = false;
        }

        /// <summary>Game days since this body was hidden.</summary>
        public float DaysHidden(float currentTime) =>
            Math.Max(0, (currentTime - HideTimestamp) / (DayDurationMinutes * 60f));

        /// <summary>Stench has reached investigation threshold.</summary>
        public bool IsCritical(float currentTime) => DaysHidden(currentTime) >= GameDaysLimit;
    }

    private static readonly List<HiddenBody> _bodies = new();
    private static bool _investigationTriggered;

    public static IReadOnlyList<HiddenBody> Bodies => _bodies;
    public static bool InvestigationTriggered => _investigationTriggered;
    public static int ActiveBodyCount => _bodies.FindAll(b => !b.Discovered).Count;

    /// <summary>Register a hidden body. Returns false if disposal (body destroyed).</summary>
    public static bool HideBody(string npcName, Vector3 position, float gameTime, string hideType)
    {
        if (hideType == "disposal")
        {
            GD.Print($"[Stench] {npcName} incinerated at {position} — no evidence remains.");
            return false; // body destroyed, no stench
        }

        var body = new HiddenBody(npcName, position, gameTime, hideType);
        _bodies.Add(body);
        GD.Print($"[Stench] {npcName} hidden in {hideType} at {position}. Decomposition clock started.");
        return true;
    }

    /// <summary>Move a hidden body to a new location (resets position but not timer).</summary>
    public static void RelocateBody(int index, Vector3 newPosition)
    {
        if (index < 0 || index >= _bodies.Count) return;
        _bodies[index].Position = newPosition;
        GD.Print($"[Stench] Body relocated to {newPosition}.");
    }

    /// <summary>Discover a hidden body (found by NPC or player).</summary>
    public static void DiscoverBody(int index)
    {
        if (index < 0 || index >= _bodies.Count) return;
        _bodies[index].Discovered = true;
        GD.Print($"[Stench] {_bodies[index].NpcName}'s body discovered!");
    }

    /// <summary>
    /// Tick the stench system. Call once per frame.
    /// Updates stench radii and checks for investigation trigger.
    /// </summary>
    public static StenchTickResult Tick(float delta, float gameTime)
    {
        var result = new StenchTickResult();
        if (_investigationTriggered) return result;

        for (int i = _bodies.Count - 1; i >= 0; i--)
        {
            var body = _bodies[i];
            if (body.Discovered) continue;

            // Grow stench radius
            body.StenchRadius += StenchSpreadRate * delta;
            float days = body.DaysHidden(gameTime);

            // Phase escalation
            if (days >= GameDaysLimit && !body.Discovered)
            {
                _investigationTriggered = true;
                result.InvestigationTriggered = true;
                result.TriggerBody = body;
                GD.Print($"[Stench] CRITICAL: {body.NpcName} decomposition reached Day {GameDaysLimit}. Office investigation triggered!");
            }
            else if (days >= 2f)
            {
                result.SevereBodies.Add(body);
            }
            else if (days >= 1f)
            {
                result.ModerateBodies.Add(body);
            }
            else
            {
                result.MildBodies.Add(body);
            }

            // Check if any NPC is within stench radius
            result.AffectedPositions.Add((body.Position, body.StenchRadius, days));
        }

        return result;
    }

    /// <summary>Get the stench level at a world position (0 = clean, 1 = unbearable).</summary>
    public static float StenchAt(Vector3 position, float gameTime)
    {
        float totalStench = 0f;
        foreach (var body in _bodies)
        {
            if (body.Discovered) continue;
            float dist = position.DistanceTo(body.Position);
            if (dist > body.StenchRadius) continue;
            float proximity = 1f - (dist / body.StenchRadius);
            float age = body.DaysHidden(gameTime) / GameDaysLimit;
            totalStench += proximity * Math.Max(0.2f, age);
        }
        return Math.Min(1f, totalStench);
    }

    public static void Reset()
    {
        _bodies.Clear();
        _investigationTriggered = false;
    }
}

public sealed class StenchTickResult
{
    public bool InvestigationTriggered;
    public StenchTracker.HiddenBody? TriggerBody;
    public List<StenchTracker.HiddenBody> MildBodies = new();
    public List<StenchTracker.HiddenBody> ModerateBodies = new();
    public List<StenchTracker.HiddenBody> SevereBodies = new();
    public List<(Vector3 pos, float radius, float days)> AffectedPositions = new();
}
