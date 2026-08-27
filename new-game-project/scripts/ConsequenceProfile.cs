using System;
using System.Collections.Generic;

/// <summary>Player-facing campaign metrics. Values are deliberately bounded and readable.</summary>
public sealed class PlayerConsequenceProfile
{
    public float Suspicion { get; private set; }
    public float Loyalty { get; private set; }
    public float Work { get; private set; }
    public float CompanyThreat { get; private set; }

    public string SuspicionBand => Suspicion switch
    {
        < 25f => "LOW",
        < 60f => "NOTICED",
        _ => "WANTED",
    };

    public string LoyaltyBand => Loyalty switch
    {
        < 30f => "DISTRUSTED",
        < 70f => "ACCEPTED",
        _ => "TRUSTED",
    };

    public string WorkBand => Work switch
    {
        < 30f => "POOR",
        < 70f => "SOLID",
        _ => "OUTSTANDING",
    };

    public float CompanyTrust => Util.Clamp(
        Work * ConsequenceBalance.WorkTrustWeight +
        Loyalty * ConsequenceBalance.LoyaltyTrustWeight -
        Suspicion * ConsequenceBalance.SuspicionTrustPenalty, 0f, 100f);

    public void Apply(ActionProfile action, float visibility = 1f, float credibility = 1f)
    {
        float scale = Util.Clamp(visibility * credibility, 0f, ConsequenceBalance.MaxActionScale);
        Suspicion = Util.Clamp(Suspicion + action.SuspicionDelta * scale, 0f, 100f);
        Loyalty = Util.Clamp(Loyalty + action.LoyaltyDelta * scale, 0f, 100f);
        Work = Util.Clamp(Work + action.WorkDelta * scale, 0f, 100f);
        CompanyThreat = Util.Clamp(CompanyThreat + action.ThreatDelta * scale, 0f, 100f);
    }

    public void ApplySuspicion(float delta) => Suspicion = Util.Clamp(Suspicion + delta, 0f, 100f);
    public void ApplyLoyalty(float delta) => Loyalty = Util.Clamp(Loyalty + delta, 0f, 100f);
    public void ApplyWork(float delta) => Work = Util.Clamp(Work + delta, 0f, 100f);

    public void Tick(float dt, bool visiblyWorking)
    {
        if (Suspicion > 0f)
            Suspicion = Util.Clamp(Suspicion - ConsequenceBalance.SuspicionRecoveryPerSecond * dt, 0f, 100f);
        if (!visiblyWorking && Work > ConsequenceBalance.IdleGraceWorkThreshold)
            Work = Util.Clamp(Work - ConsequenceBalance.IdleWorkDecayPerSecond * dt, 0f, 100f);
    }
}

/// <summary>Named, inspectable result of a player action. No gameplay literals in callers.</summary>
public sealed record ActionProfile(
    string Id,
    float SuspicionDelta,
    float LoyaltyDelta,
    float WorkDelta,
    float ThreatDelta);

public static class ConsequenceActions
{
    public static readonly ActionProfile VisibleWork = new("visible_work", -0.4f, 0.8f, 4f, -0.2f);
    public static readonly ActionProfile WorkMissed = new("missed_work", 0.8f, -1.5f, -5f, 0.3f);
    public static readonly ActionProfile WorkFailedPublicly = new("work_failed", 2f, -2f, -9f, 0.8f);
    public static readonly ActionProfile TakeCredit = new("take_credit", 0.2f, 1.2f, 7f, 0.2f);
    public static readonly ActionProfile BlameCoworker = new("blame_coworker", -1.5f, 4f, 1f, 1.5f);
    public static readonly ActionProfile HonestReport = new("honest_report", -1f, 5f, 0.5f, -0.5f);
    public static readonly ActionProfile FramedReport = new("framed_report", -3f, 7f, 1f, 4f);
    public static readonly ActionProfile HelpCoworker = new("help_coworker", -1.2f, 3f, 2f, -0.8f);
    public static readonly ActionProfile MinorLie = new("minor_lie", 2f, -0.5f, 0f, 0.2f);
    public static readonly ActionProfile RestrictedAccess = new("restricted_access", 7f, -1f, 0f, 3f);
    public static readonly ActionProfile MajorCrime = new("major_crime", 14f, -3f, 0f, 10f);
    public static readonly ActionProfile Cleanup = new("cleanup", -2f, 1f, 0.5f, -1f);
}

public static class ConsequenceBalance
{
    public const float WorkTrustWeight = 0.45f;
    public const float LoyaltyTrustWeight = 0.4f;
    public const float SuspicionTrustPenalty = 0.35f;
    public const float SuspicionRecoveryPerSecond = 0.08f;
    public const float IdleGraceWorkThreshold = 70f;
    public const float IdleWorkDecayPerSecond = 0.02f;
    public const float MaxActionScale = 1.4f;
    public const float DefaultAttitudeSeconds = 180f;
    public const float MinimumAttitudeSeconds = 20f;
    public const float MaximumAttitudeSeconds = 600f;
    public const float ImmediateReactionSeconds = 10f;
    public const float AttitudeDecayPerSecond = 1f;
    public const float RecoveryAttitudeMultiplier = 0.6f;
    public const float SoftSuspicionThreshold = 25f;
    public const float HardSuspicionThreshold = 70f;
    public const float SoftSuspicionGraceSeconds = 45f;
}

public enum NpcAttitudeKind
{
    Comfortable,
    Curious,
    Annoyed,
    Suspicious,
    Grateful,
    Resentful,
    Afraid,
    Impressed,
    Protective,
}

/// <summary>One readable, temporary attitude toward the player.</summary>
public sealed class NpcAttitude
{
    public NpcAttitudeKind Kind { get; private set; } = NpcAttitudeKind.Comfortable;
    public float Strength { get; private set; }
    public float RemainingSeconds { get; private set; }
    public string Source { get; private set; } = "routine";

    public bool Active => RemainingSeconds > 0f && Strength > 0.01f;

    public void Set(NpcAttitudeKind kind, float strength, float duration, string source)
    {
        Kind = kind;
        Strength = Util.Clamp(strength, 0f, 1f);
        RemainingSeconds = Util.Clamp(duration, ConsequenceBalance.MinimumAttitudeSeconds, ConsequenceBalance.MaximumAttitudeSeconds);
        Source = source;
    }

    public void Recover(float multiplier = 1f)
    {
        RemainingSeconds *= ConsequenceBalance.RecoveryAttitudeMultiplier * multiplier;
        Strength *= ConsequenceBalance.RecoveryAttitudeMultiplier;
    }

    public void Tick(float dt, float recoveryMultiplier = 1f)
    {
        RemainingSeconds = System.MathF.Max(0f, RemainingSeconds - dt * recoveryMultiplier);
        if (RemainingSeconds <= 0f)
            Strength = 0f;
    }
}

public static class AttitudeRules
{
    public static NpcAttitudeKind For(ActionProfile action, bool playerVisible, bool severe = false) => action.Id switch
    {
        "visible_work" or "help_coworker" => NpcAttitudeKind.Grateful,
        "take_credit" => NpcAttitudeKind.Resentful,
        "blame_coworker" or "framed_report" => NpcAttitudeKind.Suspicious,
        "honest_report" => NpcAttitudeKind.Protective,
        "restricted_access" => NpcAttitudeKind.Curious,
        "major_crime" when severe => NpcAttitudeKind.Afraid,
        "major_crime" => NpcAttitudeKind.Suspicious,
        _ when playerVisible => NpcAttitudeKind.Curious,
        _ => NpcAttitudeKind.Comfortable,
    };

    public static float DurationFor(ActionProfile action, float personalityMultiplier)
    {
        float baseDuration = action.Id switch
        {
            "visible_work" or "help_coworker" => 150f,
            "take_credit" => 300f,
            "blame_coworker" or "framed_report" => 360f,
            "restricted_access" => 240f,
            "major_crime" => 480f,
            _ => ConsequenceBalance.DefaultAttitudeSeconds,
        };
        return Util.Clamp(baseDuration * personalityMultiplier,
            ConsequenceBalance.MinimumAttitudeSeconds,
            ConsequenceBalance.MaximumAttitudeSeconds);
    }
}
