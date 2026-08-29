// Pure C#: compiles into the game and into the standalone test harness.
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Turns simulation floats into hard linguistic guardrails for the LLM. The model writes the
/// prose; the maths decides what it is allowed to sound like. Nothing here is stochastic, so
/// the same office state always produces the same constraint block.
///
/// SIGN CONVENTION -- fixed here because the two reference scripts disagreed:
///   G is PRIVATE REGARD, not "gossip intensity". Negative G = private contempt.
///   (Reference script 1 triggered gossip on g < -0.5; script 2 on g > 1.0. Contradictory.
///    Private regard is the one that falls out of the ledger, so that is what G means.)
/// </summary>
public static class PromptVectorController
{
    public sealed record Constraints(
        IReadOnlyList<string> ToneFilters,
        string Pacing,
        IReadOnlyList<string> Forbidden,
        IReadOnlyDictionary<string, object> Physics)
    {
        /// <summary>Compact block appended to the LLM system prompt.</summary>
        public string ToPromptBlock() =>
            "LINGUISTIC BOUNDARIES (derived from office physics -- obey exactly):\n" +
            $"- Mandatory tone: {string.Join(" | ", ToneFilters)}\n" +
            $"- Pacing and syntax: {Pacing}\n" +
            $"- Forbidden: {string.Join(" ", Forbidden)}\n" +
            $"- Underlying social physics: {string.Join(", ", Physics.Select(kv => $"{kv.Key}={kv.Value}"))}";
    }

    /// <summary>0 = below the first threshold; otherwise the 1-based intensity level.</summary>
    private static int Band(float v, float t1, float t2, float t3) =>
        t3 > 0f && v >= t3 ? 3 : v >= t2 ? 2 : v >= t1 ? 1 : 0;

    private static void Add(List<string> into, int level, string[] levels)
    {
        if (level > 0 && level <= levels.Length) into.Add(levels[level - 1]);
    }

    public static Constraints Build(CarnageActor instigator, string targetName, CarnageTensor t)
    {
        float p = t.P, g = t.G, phi = t.Phi, dissonance = t.Dissonance;
        float hostility = p < 0 ? -p : 0f;

        // Graded bands, not binary thresholds. Binary flags collapsed a continuous space into
        // 20 reachable tone sets; intensity levels give the model a genuinely different
        // instruction for "mildly wary" and "actively terrified" instead of one bucket.
        var tone = new List<string>();
        Add(tone, Band(phi, 0.6f, 1.2f, 2.0f), new[]
        {
            "GUARDED: mildly watchful, reads small gestures as signals.",
            "HEAVILY PARANOID: treat ordinary workplace gestures as active threats.",
            "ACUTE THREAT RESPONSE: every keystroke nearby is a tactical declaration.",
        });
        Add(tone, Band(-g, 0.5f, 1.0f, 1.6f), new[]
        {
            "COOL DISREGARD: clipped, minimally cooperative.",
            "SUBTERRANEAN CONTEMPT: coded, conspiratorial, deniable phrasing.",
            "TOTAL PRIVATE WRITE-OFF: they have already decided this person is finished.",
        });
        Add(tone, Band(g, 0.5f, 1.2f, 1.8f), new[]
        {
            "QUIET REGARD: small unprompted courtesies.",
            "GENUINE LOYALTY: will cover, will vouch, will take the hit.",
            "OPEN DEVOTION: embarrassingly, visibly in this person's corner.",
        });
        Add(tone, Band(hostility, 0.4f, 1.0f, 1.8f), new[]
        {
            "CLIPPED FORMALITY: politeness a half-degree too cold.",
            "PASSIVE-AGGRESSIVE RAGE: barely concealed malice under polite formatting.",
            "OPEN HOSTILITY: the mask is off and the pretence is abandoned.",
        });
        Add(tone, Band(dissonance, 0.6f, 1.4f, 2.4f), new[]
        {
            "MILD PERFORMANCE: saying the professional thing, meaning slightly less of it.",
            "CORPORATE DOUBLE-SPEAK SHOCK: dense management jargon over real feeling.",
            "TOTAL HYPOCRISY COLLAPSE: warmth and contempt in the same sentence, unresolved.",
        });
        Add(tone, Band(p, 0.6f, 1.2f, 1.8f), new[]
        {
            "PROFESSIONAL WARMTH: pleasant, unremarkable, frictionless.",
            "OVER-THE-TOP CAMARADERIE: toxic positivity and aggressive teamwork energy.",
            "MANIC SYNERGY EVANGELISM: exhaustingly, suspiciously enthusiastic.",
        });
        Add(tone, Band(-t.Respect, 0.5f, 1.2f, 0f), new[]
        {
            "PROFESSIONAL DOUBT: quietly checks their work.",
            "OPEN DISDAIN FOR COMPETENCE: treats their output as a liability.",
        });
        Add(tone, Band(t.Respect, 0.5f, 1.2f, 0f), new[]
        {
            "DEFERENCE: defers on technical matters without being asked.",
            "PROFESSIONAL AWE: quotes this person as an authority.",
        });
        Add(tone, Band(t.Attraction, 0.6f, 1.3f, 0f), new[]
        {
            "UNSPOKEN INTEREST: lingers slightly too long in conversation.",
            "BADLY CONCEALED INFATUATION: transparent to everyone except themselves.",
        });
        if (tone.Count == 0) tone.Add("STANDARD PROFESSIONAL NEUTRALITY.");

        string pacing =
            instigator.Extraversion > 0.85f ? "Relentless, unprompted, rapid-fire. Exclamation marks used as weapons."
            : instigator.Extraversion > 0.65f ? "Loud and forward. Talks over the reply."
            : instigator.Extraversion > 0.35f ? "Standard corporate email rhythm."
            : instigator.Extraversion > 0.15f ? "Terse. Answers the question and stops."
            : "Muted, monosyllabic, evasive. Retreats into micro-tasks.";

        var forbidden = new List<string>
        {
            $"{instigator.Name} never apologises.",
            hostility > 1.8f
                ? $"{instigator.Name}'s corporate mask has slipped; the contempt may be open."
                : hostility > 1.0f
                    ? $"{instigator.Name}'s mask is straining but must hold."
                    : $"{instigator.Name} must not drop the corporate mask.",
            "No fantasy or RPG language; bureaucratic office doublespeak only.",
        };

        var physics = new Dictionary<string, object>
        {
            ["public_affinity_index"] = MathF.Round(p, 2),
            ["private_regard"] = MathF.Round(g, 2),
            ["internal_paranoia_rating"] = MathF.Round(phi, 2),
            ["cognitive_dissonance_delta"] = MathF.Round(dissonance, 2),
            ["hostility"] = MathF.Round(hostility, 2),
            ["respect"] = MathF.Round(t.Respect, 2),
            ["attraction"] = MathF.Round(t.Attraction, 2),
            ["target"] = targetName,
        };

        return new Constraints(tone, pacing, forbidden, physics);
    }
}
