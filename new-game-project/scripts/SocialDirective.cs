// Pure C#: compiles into the game and the standalone harness.
using System;

/// <summary>Where the directive sends them.</summary>
public enum DirectiveLocation { Stay, OwnDesk, TargetPerson, Security, HumanResources, Breakroom, Printer, ServerRoom, Reception, Closet }

/// <summary>How they travel, and therefore how it reads on screen.</summary>
public enum DirectiveAction { Remain, Walk, March, Run, Skulk }

/// <summary>What fires when they get there.</summary>
public enum DirectiveEvent { None, AdjustStats, Confront, Report, Alert, Gossip, Reconcile }

/// <summary>
/// The executable output of the whole simulation: location + action + event.
/// A belief becomes a place to be, a way of getting there, and something that happens on
/// arrival -- which can publish a stimulus and set other NPCs off in turn.
/// </summary>
public sealed record SocialDirective(
    DirectiveLocation Location,
    DirectiveAction Action,
    DirectiveEvent Event,
    string TargetName,
    float Urgency,
    string Description)
{
    /// <summary>Speed multiplier applied to the NPC's base pace.</summary>
    public float SpeedMultiplier => Action switch
    {
        DirectiveAction.Run => 1.85f,
        DirectiveAction.March => 1.3f,
        DirectiveAction.Skulk => 0.6f,
        _ => 1f,
    };

    /// <summary>Animation token hint handed to AnimationLib.FindClip.</summary>
    public string[] AnimationTokens => Action switch
    {
        DirectiveAction.Run => new[] { "run", "sprint", "jog" },
        DirectiveAction.March => new[] { "march", "walk", "angry" },
        DirectiveAction.Skulk => new[] { "sneak", "crouch", "walk" },
        DirectiveAction.Remain => new[] { "idle", "stand" },
        _ => new[] { "walk" },
    };
}

/// <summary>
/// Deterministic planner. Converts a belief plus the tensor into a concrete directive.
/// No RNG: the same office state always yields the same behaviour, so it is replayable
/// and testable, and the LLM is never in the decision path.
/// </summary>
public static class DirectivePlanner
{
    /// <summary>Conviction above which a belief is worth leaving your desk over.</summary>
    public const float ActionThreshold = 0.2f;

    public static SocialDirective? Plan(string holder, Claim claim, CarnageTensor t,
        float agreeableness, float conscientiousness, float extraversion)
    {
        float conviction = claim.Confidence * claim.Heat;
        if (conviction < ActionThreshold) return null;

        // something friendly: go and say so, quietly
        if (ClaimText.IsFriendly(claim.Kind))
            return new SocialDirective(DirectiveLocation.TargetPerson, DirectiveAction.Walk,
                DirectiveEvent.Reconcile, claim.About, conviction,
                $"{holder} goes to square things with {claim.About}.");

        // nerve: disagreeable and resentful people go straight at you; the fearful do not
        float nerve = 1.4f - agreeableness - t.Phi * 0.35f + MathF.Max(0f, -t.G) * 0.5f;

        // a serious crime plus real fear routes to authority instead of a face-to-face.
        // this is the branch that produces "run to security, alert when in range".
        bool criminal = claim.Kind is ClaimKind.Sabotage or ClaimKind.FiddledNumbers or ClaimKind.Snitched
            or ClaimKind.Assault or ClaimKind.BodyDisposal or ClaimKind.Theft;
        // seeing someone floor a colleague goes to authority regardless of nerve
        bool violent = claim.Kind is ClaimKind.Assault or ClaimKind.BodyDisposal;
        if (violent && conviction > 0.4f)
        {
            bool byTheBook = conscientiousness > 0.6f;
            return new SocialDirective(
                byTheBook ? DirectiveLocation.HumanResources : DirectiveLocation.Security,
                DirectiveAction.Run,
                byTheBook ? DirectiveEvent.Report : DirectiveEvent.Alert,
                claim.About, conviction,
                $"{holder} saw what happened and runs for help.");
        }
        if (criminal && conviction > 0.45f && (t.Phi > 0.9f || nerve < 0.35f))
        {
            bool procedural = conscientiousness > 0.6f;
            return new SocialDirective(
                procedural ? DirectiveLocation.HumanResources : DirectiveLocation.Security,
                t.Phi > 1.4f ? DirectiveAction.Run : DirectiveAction.March,
                procedural ? DirectiveEvent.Report : DirectiveEvent.Alert,
                claim.About, conviction,
                procedural
                    ? $"{holder} takes it to HR, with documentation."
                    : $"{holder} breaks for security.");
        }

        // enough conviction and enough nerve: confront them in person
        if (conviction > 0.55f && nerve > 0.45f)
            return new SocialDirective(DirectiveLocation.TargetPerson,
                nerve > 0.9f ? DirectiveAction.March : DirectiveAction.Walk,
                DirectiveEvent.Confront, claim.About, conviction,
                $"{holder} goes looking for {claim.About}.");

        // no nerve, but extraverted enough to tell somebody
        if (extraversion > 0.45f)
            return new SocialDirective(DirectiveLocation.Breakroom, DirectiveAction.Walk,
                DirectiveEvent.Gossip, claim.About, conviction,
                $"{holder} drifts to the breakroom with something to share.");

        // withdraw and stew: the stats still move, nobody sees anything
        return new SocialDirective(DirectiveLocation.OwnDesk, DirectiveAction.Skulk,
            DirectiveEvent.AdjustStats, claim.About, conviction,
            $"{holder} returns to their desk and says nothing.");
    }
}
