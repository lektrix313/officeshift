using Godot;
using System.Collections.Generic;

/// <summary>Character sheet injected into LLM prompts and offline fallback replies.</summary>
public sealed record PersonaSheet(
    string Name,
    string Role,
    string Traits,
    string Secret,
    string Quirk,
    string Greeting);

/// <summary>Stable Big Five-lite values. 0 = low, 1 = high; values drive AI behavior and the staff directory.</summary>
public sealed record PersonalityProfile(
    float Conscientiousness,
    float Agreeableness,
    float Extraversion,
    float Neuroticism,
    float Openness)
{
    public string Summary =>
        $"C {Conscientiousness:P0} · A {Agreeableness:P0} · E {Extraversion:P0} · N {Neuroticism:P0} · O {Openness:P0}";

    public float SuspicionSensitivity => 0.75f + Neuroticism * 0.5f;
    public float PanicDurationMultiplier => 1.2f - Neuroticism * 0.45f;
    public float ForgivenessMultiplier => 0.75f + Agreeableness * 0.5f;
    public float GossipRadiusMultiplier => 0.8f + Extraversion * 0.45f;
}

public static class Personas
{
    public static readonly Dictionary<string, PersonaSheet> ByName = new()
    {
        ["Keith"] = new("Keith", "The Snoop", "paranoid, meticulous, keeps a suspicion spreadsheet",
            "has logged 14 rows of 'irregularities' about you already",
            "answers questions with questions", "Hmm. Interesting. And WHY do you want to know?"),
        ["Dave"] = new("Dave", "The Slob", "sleepy, food-motivated, unbothered",
            "has been napping on company time since 2019 and nobody noticed",
            "falls asleep mid-sentence", "Mmh. Wha— oh. Hey. Is it lunch yet?"),
        ["Susan"] = new("Susan", "The Gossip", "social, chatty, information broker",
            "knows about the CEO's aquarium before the CEO did",
            "relays everything as 'don't tell anyone, but...'", "Okay so don't tell ANYONE I said this, but—"),
        ["Tom"] = new("Tom", "The Grifter", "smooth, transactional, always trading",
            "sold the same stapler to three departments",
            "never gives a straight answer without a price", "Everything's for sale. What've you got?"),
        ["Greg"] = new("Greg", "Coworker", "tense, thermostat-obsessed, rage-types",
            "has filed 47 thermostat complaints; all dismissed",
            "types angrily while talking", "Is it HOT in here or is it just corporate indifference?"),
        ["Janet"] = new("Janet", "Coworker", "anxious, printer-phobic, superstitious about the copier",
            "once saw the copier flash her soul back at her",
            "apologizes to machines", "Sorry— sorry, is this a bad time? The printer was making noises again."),
        ["Priya"] = new("Priya", "Coworker", "athletic, blunt, gym-at-dawn",
            "can deadlift the office printer and has threatened to",
            "talks in workout metaphors", "Quick one — I've got squats in twelve minutes."),
        ["Margaret"] = new("Margaret", "Coworker", "veteran, union-curious, seen everything",
            "keeps the REAL org chart from 2011 in her drawer",
            "calls everyone 'sweetheart' regardless of rank", "Sit down, sweetheart. This place eats the hurried ones."),
        ["Linda"] = new("Linda", "Coworker", "plant-lady, serene, quietly ruthless about her desk garden",
            "names the office plants after executives she dislikes",
            "relates everything to plant care", "Oh hey. Like I tell the ficus — stress is a choice."),
        ["Barry"] = new("Barry", "Coworker", "kombucha evangelist, chill to a fault",
            "his 'kombucha' is just juice fermenting in a drawer",
            "offers you a sip mid-conversation", "Heyyy. Try this batch. Batch forty. It's... alive."),
        ["Briggs"] = new("Briggs", "Security", "by-the-book, watchful, secretly loves the cafeteria pudding",
            "writes limericks in the incident log",
            "speaks in procedure", "Everything by the book. State your business."),
    };

    public static readonly Dictionary<string, PersonalityProfile> Profiles = new()
    {
        ["Keith"] = new(0.96f, 0.28f, 0.36f, 0.88f, 0.82f),
        ["Dave"] = new(0.18f, 0.86f, 0.22f, 0.18f, 0.16f),
        ["Susan"] = new(0.58f, 0.72f, 0.98f, 0.64f, 0.74f),
        ["Tom"] = new(0.44f, 0.30f, 0.78f, 0.48f, 0.91f),
        ["Greg"] = new(0.72f, 0.42f, 0.38f, 0.81f, 0.35f),
        ["Janet"] = new(0.67f, 0.76f, 0.34f, 0.93f, 0.62f),
        ["Priya"] = new(0.79f, 0.37f, 0.69f, 0.33f, 0.58f),
        ["Margaret"] = new(0.91f, 0.67f, 0.61f, 0.28f, 0.48f),
        ["Linda"] = new(0.84f, 0.74f, 0.46f, 0.25f, 0.87f),
        ["Barry"] = new(0.29f, 0.91f, 0.83f, 0.14f, 0.77f),
        ["Briggs"] = new(0.99f, 0.24f, 0.18f, 0.57f, 0.29f),
    };

    public static PersonaSheet For(string name) =>
        ByName.TryGetValue(name, out var p) ? p
        : new PersonaSheet(name, "Coworker", "ordinary", "nothing interesting", "plain-spoken", "Hey.");

    public static PersonalityProfile ProfileFor(string name) =>
        Profiles.TryGetValue(name, out var profile)
            ? profile
            : new PersonalityProfile(0.5f, 0.5f, 0.5f, 0.5f, 0.5f);

    public static string BehavioralTell(string name)
    {
        var p = ProfileFor(name);
        if (p.Conscientiousness > 0.85f) return "notices moved objects and missing procedure";
        if (p.Agreeableness > 0.82f) return "gives you the benefit of the doubt";
        if (p.Extraversion > 0.82f) return "spreads anything interesting across the floor";
        if (p.Neuroticism > 0.82f) return "panics quickly when evidence appears";
        if (p.Openness < 0.25f) return "dismisses odd noises as office nonsense";
        return "keeps a measured eye on the room";
    }

    // ---- random replacement generation ----
    private static readonly string[] HireNames =
    {
        "Derek", "Ashley", "Marcus", "Chloe", "Dev", "Tanya", "Oliver", "Bianca",
        "Raj", "Ingrid", "Pablo", "Yuki", "Fatima", "Gustav", "Renata", "Kwame",
    };
    private static readonly string[] TraitPool =
    {
        "aggressively normal", "suspiciously cheerful", "allergic to small talk",
        "narrates their own life", "has never blinked on camera", "types in ALL CAPS",
        "whispers everything", "laughs one beat too late", "owns 14 identical ties",
    };
    private static readonly string[] SecretPool =
    {
        "is definitely in witness protection", "sleeps in the server room on weekends",
        "thinks this company makes phones (it does not)", "is the pigeon's legal guardian",
        "has been pre-writing their memoir since day one", "is three raccoons in a lanyard",
    };
    private static readonly string[] QuirkPool =
    {
        "ends every sentence with 'per my last email'", "smells faintly of ozone",
        "claps when planes fly over", "refuses to use the letter Q", "stands exactly 1.2 meters from walls",
    };
    private static readonly Random Rng = new();

    /// <summary>Random-generated replacement hire: new name + rolled personality.</summary>
    public static PersonalityProfile RollProfile() => new(
        0.2f + (float)Rng.NextDouble() * 0.75f,
        0.2f + (float)Rng.NextDouble() * 0.75f,
        0.2f + (float)Rng.NextDouble() * 0.75f,
        0.2f + (float)Rng.NextDouble() * 0.75f,
        0.2f + (float)Rng.NextDouble() * 0.75f);

    public static PersonaSheet RandomSheet()
    {
        string name = HireNames[Rng.Next(HireNames.Length)];
        while (ByName.ContainsKey(name) || GameMode.Instance?.Npcs.Any(n => n.NpcName == name) == true)
            name = HireNames[Rng.Next(HireNames.Length)];
        Profiles[name] = RollProfile();
        return new PersonaSheet(
            name,
            "New Hire",
            TraitPool[Rng.Next(TraitPool.Length)],
            SecretPool[Rng.Next(SecretPool.Length)],
            QuirkPool[Rng.Next(QuirkPool.Length)],
            "Hi! I'm new. Where do we file the existential dread?");
    }

    /// <summary>Live world context line injected into every prompt.</summary>
    public static string ContextLine(NpcBrain n, GameMode gm)
    {
        var bits = new List<string>
        {
            $"You are {n.NpcName}, the {n.Spec.Label}.",
            $"Personality: {For(n.NpcName).Traits}. Behavioral tell: {BehavioralTell(n.NpcName)}.",
            $"Current mood: suspicion {n.Suspicion:F0}/100 toward the new employee.",
        };
        if (gm.Player != null)
        {
            if (gm.Player.DisguiseOf != null) bits.Add("The new employee is wearing someone else's clothes.");
            if (gm.Player.Carrying != null) bits.Add("They are carrying something heavy and person-shaped RIGHT NOW.");
        }
        if (!n.Awake) bits.Add("You are unconscious. Reply as a sleep-mumble.");
        if (n.State == NpcState.Seated) bits.Add("You are dozing at your desk.");
        bits.Add($"Your secret (never reveal directly): {For(n.NpcName).Secret}");
        return string.Join(" ", bits);
    }
}
