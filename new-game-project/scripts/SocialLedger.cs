using Godot;

/// <summary>What one NPC privately believes another did.</summary>
public enum ClaimKind
{
    StoleCredit,
    TalkedBehindBack,
    Slacking,
    Sabotage,
    Snitched,
    CoveredForMe,
    Romance,
    FiddledNumbers,
    // things the player physically does, witnessed first-hand
    Assault,
    BodyDisposal,
    Theft,
    Trespassing,
}

public static class ClaimText
{
    public static string Describe(ClaimKind kind) => kind switch
    {
        ClaimKind.StoleCredit => "took credit for their work",
        ClaimKind.TalkedBehindBack => "has been talking about them behind their back",
        ClaimKind.Slacking => "has been coasting while everyone else covers",
        ClaimKind.Sabotage => "deliberately sabotaged their work",
        ClaimKind.Snitched => "reported a colleague to management",
        ClaimKind.CoveredForMe => "covered for them when it counted",
        ClaimKind.Romance => "is involved with a colleague",
        ClaimKind.FiddledNumbers => "has been fiddling the numbers",
        ClaimKind.Assault => "put a colleague on the floor",
        ClaimKind.BodyDisposal => "was hiding a body",
        ClaimKind.Theft => "was taking things that are not theirs",
        ClaimKind.Trespassing => "was somewhere they have no business being",
        _ => "did something worth talking about",
    };

    /// <summary>Positive claims build warmth; the rest build resentment.</summary>
    public static bool IsFriendly(ClaimKind kind) => kind is ClaimKind.CoveredForMe;

    /// <summary>How badly this demands that the holder do something about it.</summary>
    public static float BaseHeat(ClaimKind kind) => kind switch
    {
        ClaimKind.Assault => 1.0f,
        ClaimKind.BodyDisposal => 1.0f,
        ClaimKind.Sabotage => 1.0f,
        ClaimKind.Theft => 0.75f,
        ClaimKind.Trespassing => 0.5f,
        ClaimKind.StoleCredit => 0.85f,
        ClaimKind.Snitched => 0.8f,
        ClaimKind.FiddledNumbers => 0.7f,
        ClaimKind.TalkedBehindBack => 0.65f,
        ClaimKind.Romance => 0.4f,
        ClaimKind.Slacking => 0.35f,
        _ => 0.15f,
    };
}

/// <summary>
/// An attributed statement an NPC is carrying around. Truth is NOT stored here -- the world
/// holds truth, a Claim holds only what its holder thinks. A lie is mechanically identical
/// to a fact until somebody verifies it.
/// </summary>
public sealed class Claim
{
    public required string About;
    public required ClaimKind Kind;
    public required string Source;   // "You" = the player, an NPC name, or "witnessed"
    public float Confidence;         // 0..1, trust-gated on arrival, decays, corroboration raises it
    public float Heat;               // 0..1, drives whether it becomes an action
    public bool Acted;
    public float HeardAt;
    public int Hops;

    public string Summary => $"{About} {ClaimText.Describe(Kind)} (via {Source}, {Confidence:P0})";
}

/// <summary>
/// One NPC's directed feelings about one colleague. Bob->Jen is a different object from
/// Jen->Bob; that asymmetry is the point. Six axes, because "fondness or spite, or love,
/// or anger" is not one scale.
/// </summary>
public sealed class Opinion
{
    public float Trust;
    public float Warmth;
    public float Respect;
    public float Fear;
    public float Attraction;
    public float Resentment;

    public const float Min = -10f, Max = 10f;

    public void Nudge(float trust = 0, float warmth = 0, float respect = 0,
        float fear = 0, float attraction = 0, float resentment = 0)
    {
        Trust = Mathf.Clamp(Trust + trust, Min, Max);
        Warmth = Mathf.Clamp(Warmth + warmth, Min, Max);
        Respect = Mathf.Clamp(Respect + respect, Min, Max);
        Fear = Mathf.Clamp(Fear + fear, Min, Max);
        Attraction = Mathf.Clamp(Attraction + attraction, Min, Max);
        Resentment = Mathf.Clamp(Resentment + resentment, Min, Max);
    }

    /// <summary>One-word read, for UI and for the LLM context line.</summary>
    public string Label =>
        Resentment > 5f ? "hostile"
        : Attraction > 5f && Warmth > 2f ? "smitten"
        : Warmth > 5f ? "fond"
        : Warmth > 2f ? "friendly"
        : Fear > 5f ? "intimidated"
        : Warmth < -4f ? "cold"
        : Resentment > 2.5f ? "sore"
        : "neutral";

    public string Detail => $"trust {Trust:0.0} warmth {Warmth:0.0} respect {Respect:0.0} fear {Fear:0.0} attraction {Attraction:0.0} grudge {Resentment:0.0}";
}

/// <summary>What an NPC has decided to do about a belief.</summary>
public enum SocialIntentKind { None, Confront, Gossip }

public sealed class SocialIntent
{
    public required string Target;
    public required Claim About;
    public required SocialIntentKind Kind;
    public float Urgency;
}

/// <summary>
/// The office's second brain: every NPC's private opinion sheet about every colleague, plus
/// the claims they are carrying. Deterministic -- the LLM only ever voices what this decides.
///
/// Memory hierarchy, the point of the whole thing:
///   episodic  -> Claims, detailed, decay fast
///   semantic  -> Opinions, durable, distilled from claims during Consolidate()
/// An NPC forgets WHAT you did long before they stop resenting you for it.
/// </summary>
public sealed class SocialLedger
{
    private readonly Dictionary<string, Dictionary<string, Opinion>> _opinions = new();
    private readonly Dictionary<string, List<Claim>> _claims = new();
    private float _clock;

    public const int MaxClaimsPerNpc = 10;
    public const float ConfrontThreshold = 0.55f;

    public IReadOnlyList<Claim> ClaimsHeldBy(string holder) =>
        _claims.TryGetValue(holder, out var list) ? list : Array.Empty<Claim>();

    public IReadOnlyDictionary<string, Opinion> OpinionsOf(string holder) =>
        _opinions.TryGetValue(holder, out var map) ? map : new Dictionary<string, Opinion>();

    /// <summary>Bob->Jen. Created on demand; absent means genuinely neutral.</summary>
    public Opinion Of(string holder, string target)
    {
        if (!_opinions.TryGetValue(holder, out var map)) _opinions[holder] = map = new();
        if (!map.TryGetValue(target, out var opinion)) map[target] = opinion = new Opinion();
        return opinion;
    }

    /// <summary>Seed baseline regard so a fresh office is not a room of blank strangers.</summary>
    public void Register(IEnumerable<string> names)
    {
        var all = names.ToList();
        foreach (var holder in all)
            foreach (var target in all)
            {
                if (holder == target) continue;
                var o = Of(holder, target);
                var mine = Personas.ProfileFor(holder);
                var theirs = Personas.ProfileFor(target);
                // agreeable people start warmer; similar conscientiousness reads as competence
                o.Warmth = (mine.Agreeableness - 0.5f) * 4f;
                o.Trust = (mine.Agreeableness - 0.5f) * 3f;
                o.Respect = (1f - MathF.Abs(mine.Conscientiousness - theirs.Conscientiousness)) * 2f - 1f;
                o.Fear = MathF.Max(0f, (theirs.Extraversion - mine.Extraversion) * 3f);
            }
        // the player is a stranger to everyone: neutral trust, nothing earned yet
        foreach (var holder in all) Of(holder, MailStore.PlayerAddress);
    }

    /// <summary>
    /// Someone tells <paramref name="holder"/> that <paramref name="about"/> did something.
    /// How much of it they buy is gated by what they think of the messenger -- which is why
    /// lying to a colleague who does not trust you simply does not land.
    /// </summary>
    public Claim? Tell(string holder, string about, ClaimKind kind, string source, int hops = 0)
    {
        if (holder == about) return null;

        var me = Personas.ProfileFor(holder);
        var sourceOpinion = Of(holder, source);
        // trust runs -10..10; map to a 0..1 credibility multiplier
        float credibility = Mathf.Clamp(0.5f + sourceOpinion.Trust / 20f, 0.05f, 1f);
        // neurotics believe bad news faster; open people hold looser convictions
        float bias = ClaimText.IsFriendly(kind) ? 1f : 0.7f + me.Neuroticism * 0.6f;
        // capped below certainty on purpose: one source is never proof, which leaves room
        // for corroboration to matter and for a denial to still move the needle
        float confidence = Mathf.Clamp(credibility * bias * MathF.Pow(0.75f, hops), 0f, 0.85f);

        if (!_claims.TryGetValue(holder, out var list)) _claims[holder] = list = new();

        // corroboration: hearing the same thing from a second mouth hardens it
        var existing = list.FirstOrDefault(c => c.About == about && c.Kind == kind);
        if (existing != null)
        {
            if (existing.Source != source)
            {
                existing.Confidence = Mathf.Clamp(existing.Confidence + confidence * 0.5f, 0f, 1f);
                existing.Heat = Mathf.Clamp(existing.Heat + 0.2f, 0f, 1f);
                existing.Acted = false; // corroborated gossip is worth acting on again
            }
            return existing;
        }

        var claim = new Claim
        {
            About = about,
            Kind = kind,
            Source = source,
            Confidence = confidence,
            Heat = ClaimText.BaseHeat(kind) * (0.6f + me.Neuroticism * 0.8f),
            HeardAt = _clock,
            Hops = hops,
        };
        list.Add(claim);
        while (list.Count > MaxClaimsPerNpc) list.RemoveAt(0);

        // hearing it already colours the sheet, before anyone confronts anyone
        Appraise(holder, about, kind, confidence);
        return claim;
    }

    /// <summary>Fold a believed claim into durable feeling, scaled by personality.</summary>
    private void Appraise(string holder, string about, ClaimKind kind, float weight)
    {
        var me = Personas.ProfileFor(holder);
        var o = Of(holder, about);
        // disagreeable people take slights harder
        float sting = weight * (1.6f - me.Agreeableness);
        if (ClaimText.IsFriendly(kind))
            o.Nudge(trust: sting * 1.5f, warmth: sting * 2f, resentment: -sting);
        else if (kind == ClaimKind.Romance)
            o.Nudge(warmth: sting * 0.5f, attraction: sting * 0.4f);
        else
            o.Nudge(trust: -sting * 1.2f, warmth: -sting * 1.6f,
                respect: kind is ClaimKind.Slacking or ClaimKind.FiddledNumbers ? -sting * 2f : -sting * 0.5f,
                resentment: sting * 2f);
    }

    /// <summary>
    /// Consolidation. Claims fade; the feeling they produced does not. This is what lets an
    /// NPC forget the specifics of week one and still dislike you in week three.
    /// </summary>
    public void Tick(float dt)
    {
        _clock += dt;
        foreach (var (holder, list) in _claims)
        {
            foreach (var claim in list)
                claim.Confidence = MathF.Max(0f, claim.Confidence - dt * 0.004f);

            // a claim that has faded past usefulness leaves a residue of feeling behind
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Confidence > 0.08f) continue;
                Appraise(holder, list[i].About, list[i].Kind, 0.15f);
                list.RemoveAt(i);
            }
        }

        foreach (var (holder, map) in _opinions)
        {
            var forgiveness = Personas.ProfileFor(holder).ForgivenessMultiplier;
            foreach (var o in map.Values)
            {
                // grudges outlast warmth: resentment decays slowest, and slower still for the unforgiving
                o.Resentment = MathF.Max(0f, o.Resentment - dt * 0.0004f * forgiveness);
                o.Fear = MathF.Max(0f, o.Fear - dt * 0.020f);
                o.Warmth -= o.Warmth * dt * 0.004f;
                o.Attraction -= o.Attraction * dt * 0.003f;
            }
        }
    }

    /// <summary>
    /// What, if anything, this NPC wants to do about what they believe. Conscientious people
    /// sit on things; frightened people gossip instead of confronting; the disagreeable go
    /// straight at you.
    /// </summary>
    public SocialIntent? NextIntent(string holder)
    {
        if (!_claims.TryGetValue(holder, out var list)) return null;
        var me = Personas.ProfileFor(holder);

        SocialIntent? best = null;
        foreach (var claim in list)
        {
            if (claim.Acted || ClaimText.IsFriendly(claim.Kind)) continue;
            float conviction = claim.Confidence * claim.Heat;
            if (conviction < 0.2f) continue;

            var toward = Of(holder, claim.About);
            // fear and agreeableness push toward the gossip route instead of a face-to-face
            float nerve = 1.4f - me.Agreeableness - toward.Fear / 12f + toward.Resentment / 10f;
            var kind = conviction > ConfrontThreshold && nerve > 0.45f
                ? SocialIntentKind.Confront
                : SocialIntentKind.Gossip;

            float urgency = conviction * (kind == SocialIntentKind.Confront ? 1f : 0.6f) * (0.7f + me.Extraversion * 0.6f);
            if (best == null || urgency > best.Urgency)
                best = new SocialIntent { Target = claim.About, About = claim, Kind = kind, Urgency = urgency };
        }
        return best;
    }

    /// <summary>
    /// Resolve a confrontation. Both sheets move, and they move differently -- the accuser
    /// hardens, the accused resents being accused whether or not they did it.
    /// </summary>
    public void ResolveConfrontation(string accuser, string accused, Claim claim, bool accusedDenies)
    {
        claim.Acted = true;
        var accuserProfile = Personas.ProfileFor(accuser);
        var accusedProfile = Personas.ProfileFor(accused);

        var a = Of(accuser, accused);
        var b = Of(accused, accuser);

        // being accused stings regardless of guilt; that is what makes false rumours corrosive
        b.Nudge(trust: -1.5f, warmth: -2f, resentment: 2.5f * (1.6f - accusedProfile.Agreeableness),
            fear: accuserProfile.Extraversion > 0.6f ? 1.2f : 0f);

        if (accusedDenies)
        {
            // a flat denial from someone open enough to hear it takes the heat out
            float swayed = MathF.Max(0.08f, accuserProfile.Openness * (1f - claim.Confidence * 0.7f));
            claim.Confidence = Mathf.Clamp(claim.Confidence - swayed, 0f, 1f);
            a.Nudge(warmth: -1f, resentment: 1f);
            // and the messenger pays for it
            if (claim.Source != "witnessed" && claim.Source != accuser)
                Of(accuser, claim.Source).Nudge(trust: -2.5f * swayed, warmth: -1.5f * swayed, resentment: 1.5f * swayed);
        }
        else
        {
            claim.Confidence = Mathf.Clamp(claim.Confidence + 0.2f, 0f, 1f);
            a.Nudge(warmth: -2.5f, respect: -1.5f, resentment: 2f);
            if (claim.Source != "witnessed" && claim.Source != accuser)
                Of(accuser, claim.Source).Nudge(trust: 1.5f, warmth: 1f);
        }
    }

    /// <summary>A line for the LLM so replies are coloured by how this NPC actually feels.</summary>
    public string ContextFor(string holder, IEnumerable<string> colleagues)
    {
        var parts = new List<string>();
        foreach (var other in colleagues)
        {
            if (other == holder) continue;
            var o = Of(holder, other);
            if (o.Label != "neutral") parts.Add($"{other}: {o.Label}");
        }
        var beliefs = ClaimsHeldBy(holder).Where(c => c.Confidence > 0.35f).Select(c => c.Summary).ToList();
        var line = parts.Count > 0 ? $"How you feel about colleagues: {string.Join("; ", parts)}." : "";
        if (beliefs.Count > 0) line += $" You currently believe: {string.Join("; ", beliefs)}.";
        return line;
    }
}

/// <summary>
/// Pulls a structured claim out of something the player typed. Deterministic on purpose:
/// the simulation must not depend on the LLM cooperating, and this has to work with the
/// API down. The LLM can still add claims on top via its directive vocabulary.
/// </summary>
public static class ClaimParser
{
    private static readonly (ClaimKind Kind, string[] Cues)[] Cues =
    {
        (ClaimKind.Sabotage,         new[] { "sabotage", "sabotaged", "wrecked", "deleted your", "broke your", "trashed your" }),
        (ClaimKind.StoleCredit,      new[] { "took credit", "stole credit", "took the credit", "passed off your", "claimed your work", "your idea" }),
        (ClaimKind.Snitched,         new[] { "snitch", "snitched", "reported you", "told hr", "grassed", "ratted" }),
        (ClaimKind.FiddledNumbers,   new[] { "fiddl", "cooked the books", "falsif", "faking the numbers", "fudged" }),
        (ClaimKind.CoveredForMe,     new[] { "covered for you", "stuck up for you", "defended you", "backed you up" }),
        (ClaimKind.Romance,          new[] { "sleeping with", "hooking up", "seeing each other", "has a thing for", "crush on", "into you" }),
        (ClaimKind.Slacking,         new[] { "slacking", "coasting", "doing nothing", "lazy", "does nothing" }),
        // deliberately last and broadest: "Jen said you were..." is the common shape
        (ClaimKind.TalkedBehindBack, new[] { "behind your back", "badmouth", "slagging", "talking about you", "said you", "says you", "said that you", "was saying" }),
    };

    /// <summary>Returns the colleague the text is about and what it accuses them of.</summary>
    public static (string About, ClaimKind Kind)? Extract(string text, IEnumerable<string> npcNames, string exclude)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var low = text.ToLowerInvariant();

        string? about = null;
        int earliest = int.MaxValue;
        foreach (var name in npcNames)
        {
            if (name == exclude) continue;
            // match the full name or its first token, so "Boss Barbara" also answers to "Barbara"
            foreach (var token in new[] { name, name.Split(' ')[^1], name.Split(' ')[0] }.Distinct())
            {
                if (token.Length < 3) continue;
                int at = low.IndexOf(token.ToLowerInvariant(), StringComparison.Ordinal);
                if (at < 0 || at >= earliest) continue;
                earliest = at;
                about = name;
            }
        }
        if (about == null) return null;

        foreach (var (kind, cues) in Cues)
            foreach (var cue in cues)
                if (low.Contains(cue)) return (about, kind);

        return null;
    }
}
