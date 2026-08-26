using System.Collections.Generic;

/// <summary>Central tuning values for NPC activation, recovery, and department friction.</summary>
public static class NpcStatBalance
{
    public const float MinStat = 0f;
    public const float MaxStat = 1f;
    public const float BaseActivationSensitivity = 1f;
    public const float NeuroticActivationBonus = 0.5f;
    public const float BaseReactionCooldown = 1f;
    public const float CalmCooldownReduction = 0.2f;
    public const float ActivationBase = 0.45f;
    public const float ProximityWeight = 0.55f;
    public const float AttentionBase = 0.35f;
    public const float AttentionWeight = 0.65f;
    public const float PlayerLedActivationMultiplier = 1.15f;
    public const float ActivationCap = 2f;
    public const float ActivationDistance = 18f;
    public const float MinimumStimulusRadius = 0.1f;
    public const float FocusFloor = 0.5f;
    public const float BelowThresholdCooldown = 3f;
    public const float EvidenceReactionCooldown = 12f;
    public const float NoiseReactionCooldown = 7f;
    public const float FailureReactionCooldown = 18f;
    public const float ComfortReactionCooldown = 12f;
    public const float DefaultReactionCooldown = 8f;
    public const float FocusAttentionFloor = 0.2f;
    public const float StressPerActivation = 0.5f;
    public const float ComfortPerActivation = 0.35f;
    public const float StressRecoveryPerSecond = 0.8f;
    public const float ComfortRecoveryPerSecond = 0.45f;
    public const float DefaultDepartmentAffinity = 1f;
    public const float HatesDepartmentMultiplier = 1.35f;
    public const float LikesDepartmentMultiplier = 0.7f;
    public const float NeutralDepartmentMultiplier = 1f;
    public const float DepartmentAffinityThreshold = 0.75f;
    public const float AntiITAffinityThreshold = 0.45f;
    public const float PatientActivationThreshold = 0.58f;
    public const float ImpatientActivationThreshold = 0.34f;
    public const float StressActivationMultiplier = 0.45f;
    public const float ComfortActivationReduction = 0.25f;
    public const float AccessDeniedActivation = 0.8f;
    public const float AccessDeniedRadius = 12f;
    public const float AccessDeniedStress = 0.35f;
}

/// <summary>
/// Runtime NPC stats. Personality is the authored source; these values are the
/// gameplay-facing sheet used by object states and the consequence engine.
/// </summary>
public sealed class NpcStatSheet
{
    public float ActivationSensitivity { get; }
    public float ReactionCooldownMultiplier { get; }
    public float Focus { get; }
    public float Patience { get; }
    public float StressResilience { get; }
    public float ComfortNeed { get; }
    public float SocialNeed { get; }
    public float ITAffinity { get; }
    public float FacilitiesAffinity { get; }
    public float SecurityAffinity { get; }
    public float OperationsAffinity { get; }
    public float CurrentStress { get; private set; }
    public float CurrentComfort { get; private set; }

    public NpcStatSheet(PersonalityProfile personality)
    {
        ActivationSensitivity = Clamp(NpcStatBalance.BaseActivationSensitivity + personality.Neuroticism * NpcStatBalance.NeuroticActivationBonus);
        ReactionCooldownMultiplier = Clamp(1.2f - personality.Conscientiousness * 0.35f - personality.Agreeableness * 0.15f);
        Focus = Clamp(0.35f + personality.Conscientiousness * 0.45f + personality.Openness * 0.2f);
        Patience = Clamp(0.2f + personality.Agreeableness * 0.55f + personality.Conscientiousness * 0.25f);
        StressResilience = Clamp(0.25f + (1f - personality.Neuroticism) * 0.55f + personality.Agreeableness * 0.2f);
        ComfortNeed = Clamp(0.25f + (1f - personality.Conscientiousness) * 0.35f + personality.Extraversion * 0.3f);
        SocialNeed = Clamp(0.2f + personality.Extraversion * 0.65f);

        ITAffinity = DepartmentAffinity(personality.Openness, personality.Conscientiousness);
        FacilitiesAffinity = DepartmentAffinity(personality.Agreeableness, personality.Conscientiousness);
        SecurityAffinity = DepartmentAffinity(personality.Conscientiousness, 1f - personality.Openness);
        OperationsAffinity = DepartmentAffinity(personality.Conscientiousness, personality.Agreeableness);
    }

    public float DepartmentMultiplier(string department)
    {
        float affinity = department switch
        {
            "IT" => ITAffinity,
            "Facilities" => FacilitiesAffinity,
            "Security" => SecurityAffinity,
            "Operations" or "Reception" or "HR" => OperationsAffinity,
            _ => NpcStatBalance.DefaultDepartmentAffinity,
        };
        return affinity < NpcStatBalance.AntiITAffinityThreshold
            ? NpcStatBalance.HatesDepartmentMultiplier
            : affinity > NpcStatBalance.DepartmentAffinityThreshold
                ? NpcStatBalance.LikesDepartmentMultiplier
                : NpcStatBalance.NeutralDepartmentMultiplier;
    }

    public float ActivationThreshold => Patience > 0.7f
        ? NpcStatBalance.PatientActivationThreshold
        : NpcStatBalance.ImpatientActivationThreshold;

    public float StressMultiplier => 1f + (1f - StressResilience);
    public float ComfortMultiplier => ComfortNeed;
    public float SocialMultiplier => SocialNeed;

    public void ApplyObjectEffect(float stressDelta, float comfortDelta, float activation)
    {
        CurrentStress = Clamp(CurrentStress + stressDelta * activation * StressMultiplier);
        CurrentComfort = Clamp(CurrentComfort + comfortDelta * activation * ComfortMultiplier);
    }

    public void Recover(float dt)
    {
        CurrentStress = Clamp(CurrentStress - NpcStatBalance.StressRecoveryPerSecond * dt);
        CurrentComfort = Clamp(CurrentComfort - NpcStatBalance.ComfortRecoveryPerSecond * dt);
    }

    public void ReceiveComfort(float amount)
    {
        CurrentStress = Clamp(CurrentStress - amount * NpcStatBalance.StressRecoveryPerSecond);
        CurrentComfort = Clamp(CurrentComfort + amount);
    }

    private static float DepartmentAffinity(float primary, float secondary) =>
        Clamp(0.2f + primary * 0.55f + secondary * 0.25f);

    private static float Clamp(float value) =>
        value < NpcStatBalance.MinStat ? NpcStatBalance.MinStat
        : value > NpcStatBalance.MaxStat ? NpcStatBalance.MaxStat
        : value;
}
