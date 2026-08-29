// Pure C# on purpose: no Godot types, so the same source compiles into the game AND into
// the standalone gauntlet harness under test/ledger.
using System;
using System.Collections.Generic;

/// <summary>One participant, flattened out of NpcBrain/Personas so this stays testable.</summary>
public sealed record CarnageActor(
    string Name, string Job, string Quirk, string Greeting,
    float Agreeableness, float Extraversion, float Suspicion);

/// <summary>
/// The three scalars the narrative dispatch reads. Derived from the ledger, never hand-fed:
///   G   private feeling      -- what they actually think (warmth minus grudge)
///   P   public face          -- what they let the office see; agreeable people mask harder
///   Phi paranoia             -- fear of this colleague plus ambient suspicion
///   Dissonance |P - G|       -- how hard they are faking it. Corporate hypocrisy, quantified.
/// </summary>
public readonly record struct CarnageTensor(float P, float G, float Phi, float Respect = 0f, float Attraction = 0f)
{
    public float Dissonance => MathF.Abs(P - G);

    /// <summary>
    /// The mask is the interesting part: a warm professional baseline that agreeable people
    /// project over private contempt. High agreeableness + deep grudge = maximum dissonance,
    /// which falls straight out of personality instead of being authored.
    /// </summary>
    public static CarnageTensor FromOpinion(Opinion opinion, float agreeableness, float suspicion)
    {
        float g = Math.Clamp((opinion.Warmth - opinion.Resentment) / 5f, -2f, 2f);
        const float professionalBaseline = 1f;
        float mask = Math.Clamp(agreeableness, 0f, 1f);
        float p = Math.Clamp(g * (1f - mask) + mask * professionalBaseline, -2f, 2f);
        float phi = Math.Clamp(opinion.Fear / 4f + suspicion / 100f, 0f, 3f);
        float respect = Math.Clamp(opinion.Respect / 5f, -2f, 2f);
        float attraction = Math.Clamp(opinion.Attraction / 5f, -2f, 2f);
        return new CarnageTensor(p, g, phi, respect, attraction);
    }
}

public sealed record CarnageReport(string Narrative, IReadOnlyDictionary<string, object> Metadata);

/// <summary>
/// Deterministic office-disaster narrator. Every string is selected by arithmetic on the
/// tensor plus a stable hash of the two people involved -- no RNG anywhere, so the same
/// office state always produces the same incident, and it can be replayed and tested.
/// </summary>
public sealed class CarnageSynthesizer
{
    private sealed record Catalyst(string Name, string[] Actions);

    private static readonly Dictionary<string, Catalyst> Catalysts = new()
    {
        ["breakroom_microwave"] = new("Communal Breakroom Microwave", new[]
        {
            "reheated a highly volatile fish curry on a 'Power Level 10' setting",
            "left an un-splatter-shielded bowl of tomato soup to detonate internally",
            "attempted to hard-boil an egg, causing an architectural containment breach",
        }),
        ["network_router"] = new("Main IT Server Rack", new[]
        {
            "unplugged a critical Ethernet cable to charge a personal vape pen",
            "spilled a triple-shot vanilla latte directly into the ventilation array",
            "rerouted the company's external bandwidth into a private cryptocurrency mining rig",
        }),
        ["label_maker"] = new("Industrial Departmental Label Maker", new[]
        {
            "embarked on an unauthorized asset conquest, tagging all desks as 'SOVEREIGN territory'",
            "laminated and affixed a 'NOT TO BE MOVED UNDER PENALTY OF DEATH' tag to a stapler",
            "labelled the manager's left shoe as an 'OPERATIONAL LIABILITY'",
        }),
        ["office_printer"] = new("Central High-Volume Office Printer", new[]
        {
            "initiated an un-cancellable print job for a 4,000-page high-resolution PDF document",
            "jammed the bypass tray with heavy-duty construction paper while printing a meme",
            "left a highly confidential, unredacted payroll document sitting face-up on the output tray",
        }),
        ["supply_closet"] = new("Restricted Stationery Reserve", new[]
        {
            "annexed the entire quarterly allocation of retractable pens into a personal desk drawer",
            "replaced every biro in the reserve with the chewed, non-functional ones from reception",
            "instituted an unsanctioned two-key sign-out protocol for a box of paperclips",
        }),
        ["coffee_machine"] = new("Bean-to-Cup Morale Terminal", new[]
        {
            "descaled the machine mid-queue during the 09:00 peak demand window",
            "left exactly one millilitre in the communal pot to avoid triggering the refill obligation",
            "reprogrammed every saved preset to decaf as an act of undeclared psychological warfare",
        }),
    };

    private static readonly string[] ParanoiaFeedbacks =
    {
        "The surrounding airspace enters a state of 'Proximity Paranoia'. Nearby workers refuse to blink, treating keyboard keystrokes as active tactical declarations.",
        "A wave of localized panic spreads. Employees immediately lock their desks, clear their browser histories, and avoid direct eye contact.",
        "An acute corporate cold-war manifests. NPCs within a five-desk radius immediately begin communicating via heavily bulleted, defensive Outlook calendar blocks.",
    };

    private static readonly string[] GossipFeedbacks =
    {
        "A stealth Slack channel is auto-generated specifically to archive, analyze, and ruthlessly rank the instigator's performance history.",
        "The office gossip pipelines saturate completely. Key staff stop working to craft highly specific, deeply insulting custom emojis dedicated to this specific failure.",
        "Whispering networks trigger an organizational migration. Rumors multiply across the floor plan, turning the incident into a legendary office conspiracy theory.",
    };

    private static readonly string[] DissonanceFeedbacks =
    {
        "The instigator suffers severe Corporate Hypocrisy Shock. They become physically incapable of normal language and can only speak in frantic management buzzwords.",
        "Cognitive overload occurs. The subject aggressively prints out their own emails and files them into physical cabinets while muttering about 'process optimizations'.",
        "A structural breakdown ensues. The instigator signs off every conversation with a passive-aggressive 'Per my last email' before retreating to a supply closet.",
    };

    public static IReadOnlyCollection<string> CatalystKeys => Catalysts.Keys;

    /// <summary>
    /// Stable, order-sensitive hash of the pair. Lets the same tensor state read differently
    /// for Bob-on-Jen than for Jen-on-Bob without introducing any randomness.
    /// </summary>
    private static int PairSalt(string a, string b)
    {
        unchecked
        {
            int h = 17;
            foreach (var c in a) h = h * 31 + c;
            h = h * 131 + 7;
            foreach (var c in b) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    /// <summary>Deterministic non-negative index into a pool.</summary>
    private static int Index(float scalar, int salt, int length) =>
        length <= 0 ? 0 : (int)(MathF.Abs(scalar) * 100f + salt) % length;

    /// <summary>Pick a catalyst from office state alone, so callers need not choose one.</summary>
    public static string CatalystFor(string instigator, string victim, CarnageTensor t)
    {
        var keys = new List<string>(Catalysts.Keys);
        keys.Sort(StringComparer.Ordinal); // stable regardless of dictionary ordering
        return keys[Index(t.Phi + t.Dissonance, PairSalt(instigator, victim), keys.Count)];
    }

    public CarnageReport Synthesize(CarnageActor instigator, CarnageActor victim,
        string catalystKey, CarnageTensor tensor)
    {
        float p = tensor.P, g = tensor.G, phi = tensor.Phi, dissonance = tensor.Dissonance;
        int salt = PairSalt(instigator.Name, victim.Name);

        // 1. archetype
        string archetype, delivery;
        if (phi > 1.2f)
        {
            archetype = "THE PASSIVE-AGGRESSIVE PAYLOAD";
            delivery = "via an unsigned, aggressively formatted, laminated sticky note";
        }
        else if (p < -0.8f && g < -0.8f)
        {
            archetype = "THE BOUNDARY BREACH";
            delivery = $"while making intense, unblinking eye contact directly with {victim.Name}";
        }
        else
        {
            archetype = "THE CORPORATE WEAPONIZATION";
            delivery = "while explicitly carbon-copying the entire C-Suite on the operational thread";
        }

        // 2. catalyst + action
        if (!Catalysts.TryGetValue(catalystKey, out var catalyst))
        {
            catalystKey = "breakroom_microwave";
            catalyst = Catalysts[catalystKey];
        }
        string action = catalyst.Actions[Index(g, salt, catalyst.Actions.Length)];

        // 3. dominant vector.
        //    Dissonance is tested FIRST. In the reference implementation it sat behind the
        //    gossip branch, which shadowed 84% of the high-hypocrisy region (P>0.5, G<-0.9) --
        //    exactly the states the dissonance pool exists to narrate.
        string[] pool;
        string dominant;
        if (dissonance > 1.4f)
        {
            pool = DissonanceFeedbacks;
            dominant = $"Cognitive Dissonance Spike (|P-G|: {dissonance:F2})";
        }
        else if (g < phi && g < -0.5f)
        {
            pool = GossipFeedbacks;
            dominant = $"Private Gossip Layer (G: {g:F2})";
        }
        else
        {
            pool = ParanoiaFeedbacks;
            dominant = $"Internal Paranoia Layer (Phi: {phi:F2})";
        }
        string feedback = pool[Index(dissonance + phi, salt, pool.Length)];

        string narrative =
            $"=== SYSTEMIC OFFICE DISASTER LOG: {archetype} ===\n" +
            $"LOCATION FIELD: Near the [{catalyst.Name}]\n\n" +
            $"INCIDENT: {instigator.Name} ({instigator.Job}) has actively {action} {delivery}.\n" +
            $"TARGET IMPACT: {victim.Name} ({victim.Job}), who famously {victim.Quirk}, was caught in the crossfire.\n" +
            $"INTERACTION INTERCEPT: When approached, {instigator.Name} turned slowly and said: '{instigator.Greeting}'\n\n" +
            $"SYSTEMIC CORRUPTION CASCADE:\n" +
            $"Driven heavily by the {dominant}, the network destabilizes. {feedback}\n" +
            $"================================================================";

        var metadata = new Dictionary<string, object>
        {
            ["instigator"] = instigator.Name,
            ["victim"] = victim.Name,
            ["catalyst_used"] = catalystKey,
            ["calculated_archetype"] = archetype,
            ["dominant_vector_source"] = dominant,
            ["P"] = p,
            ["G"] = g,
            ["Phi"] = phi,
            ["Dissonance"] = dissonance,
            ["action_index"] = Index(g, salt, catalyst.Actions.Length),
            ["feedback_index"] = Index(dissonance + phi, salt, pool.Length),
        };
        return new CarnageReport(narrative, metadata);
    }
}
