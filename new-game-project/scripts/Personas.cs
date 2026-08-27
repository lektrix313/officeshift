using Godot;
using System.Collections.Generic;

public sealed record PersonaSheet(string Name, string Role, string Traits, string Secret, string Quirk, string Greeting);

public sealed record PersonalityProfile(float Conscientiousness, float Agreeableness, float Extraversion, float Neuroticism, float Openness)
{
    public string Summary => $"C {Conscientiousness:P0} · A {Agreeableness:P0} · E {Extraversion:P0} · N {Neuroticism:P0} · O {Openness:P0}";
    public float SuspicionSensitivity => 0.75f + Neuroticism * 0.5f;
    public float PanicDurationMultiplier => 1.2f - Neuroticism * 0.45f;
    public float ForgivenessMultiplier => 0.75f + Agreeableness * 0.5f;
    public float GossipRadiusMultiplier => 0.8f + Extraversion * 0.45f;
}

public static class Personas
{
    public static readonly Dictionary<string, PersonaSheet> ByName = new()
    {
        ["Bob"] = new("Bob", "Accounting", "friendly, boring, spreadsheet-obsessed", "can spot a number that does not add up from across the room", "turns every chat into an expense lecture", "Lovely to see you. Have you reconciled your receipts?"),
        ["Sleepy Steve"] = new("Sleepy Steve", "IT Support", "exhausted, annoyed, technically brilliant", "knows every password, backdoor, and dodgy workaround", "falls asleep while fixing things", "What broke now? Make it quick. I was dreaming in server logs."),
        ["Pam"] = new("Pam", "HR", "cheerful, immaculate, terrifyingly positive", "knows everybody's personal business", "calls disasters learning opportunities", "Let's turn this challenging incident into a growth opportunity."),
        ["Mr Purple"] = new("Mr Purple", "CEO", "stern, pompous, suspicious, executive", "believes every room improves when he enters it", "speaks about himself in the third person", "The company expects results. Purple expects more."),
        ["Fran"] = new("Fran", "Finance", "sharp, organised, quietly dangerous", "knows whether every purchase was legitimate", "checks totals while people are talking", "That number is interesting. Please explain it precisely."),
        ["Chad"] = new("Chad", "Sales", "loud, confident, friendly, distractible", "has never met a conversation he could not make about himself", "high-fives people without warning", "Big day! Huge opportunity! You got thirty seconds?"),
        ["Rita"] = new("Rita", "Reception", "friendly, chatty, observant", "knows exactly who entered and left", "remembers faces better than names", "Hi! Haven't seen you come in. Or have I?"),
        ["Mailroom Mike"] = new("Mailroom Mike", "Mailroom", "quiet, practical, mobile, overlooked", "knows every delivery, key, and back route", "appears beside carts without making a sound", "Package for you. Probably. Maybe."),
        ["Dave"] = new("Dave", "Legal", "serious, literal, liability-conscious", "can dismantle a lie by examining one word", "says technically speaking before everything", "Technically speaking, that is not an explanation."),
        ["Liz"] = new("Liz", "Marketing", "energetic, social, image-conscious", "has photos of half the building without meaning to", "turns every incident into content", "Wait, do that again. The lighting is amazing."),
        ["Nervous Ned"] = new("Nervous Ned", "Security Trainee", "terrified, twitchy, accidentally perceptive", "half of his paranoid theories are correct", "whispers breach theories to himself", "Was that supposed to happen? Is that supposed to happen?"),
        ["Manager Mo"] = new("Manager Mo", "Operations Manager", "process-obsessed, needy, meeting-loving", "is terrified Mr Purple will ask for results", "says quick word and means twenty minutes", "Quick word? Great. Let's align on alignment."),
        ["Jen"] = new("Jen", "Admin", "warm, competent, quietly indispensable", "knows the calendar and every spare pass", "solves problems before people notice them", "I can probably find a form for that."),
        ["Data Dave"] = new("Data Dave", "Data Analyst", "awkward, brilliant, pattern-focused", "sees network and data patterns before anyone else", "corrects statistics during arguments", "The anomaly is not random. It is just pretending to be."),
        ["Boring Bill"] = new("Boring Bill", "Process Analyst", "spectacularly dull, slow, harmless-looking", "has a long story about every room in the company", "never reaches the point", "That reminds me of something that happened in 2009..."),
        ["Boss Barbara"] = new("Boss Barbara", "Senior Manager", "calm, polished, formidable", "can detect inconsistent stories without raising her voice", "stares until people volunteer evidence", "Take your time. I am listening to the version you choose."),
        ["Joe"] = new("Joe", "Janitor", "relaxed, deadpan, invisible to corporate life", "has master keys and knows every back corridor", "does not react unless you make his job harder", "If it is not leaking, burning, or in my way, carry on."),
        ["Kevin"] = new("Kevin", "Procurement", "nervous, inventory-focused, permanently surprised", "knows where every gadget should be", "counts objects under his breath", "Was that there before? It should have been there."),
        ["Old Tom"] = new("Old Tom", "Senior Advisor", "slow-moving, historical, apparently retiring forever", "remembers scandals and access routes nobody else does", "starts stories with when this was a proper company", "I remember when this building had three fewer crimes."),
    };

    public static readonly Dictionary<string, PersonalityProfile> Profiles = new()
    {
        ["Bob"] = new(.95f, .82f, .45f, .35f, .3f), ["Sleepy Steve"] = new(.45f, .4f, .2f, .35f, .8f), ["Pam"] = new(.78f, .72f, .78f, .45f, .7f),
        ["Mr Purple"] = new(.92f, .2f, .35f, .55f, .35f), ["Fran"] = new(.98f, .35f, .25f, .6f, .4f), ["Chad"] = new(.35f, .8f, .98f, .25f, .55f),
        ["Rita"] = new(.7f, .72f, .9f, .5f, .65f), ["Mailroom Mike"] = new(.7f, .62f, .3f, .25f, .6f), ["Dave"] = new(.9f, .3f, .25f, .55f, .45f),
        ["Liz"] = new(.55f, .65f, .88f, .4f, .9f), ["Nervous Ned"] = new(.6f, .3f, .2f, .98f, .55f), ["Manager Mo"] = new(.7f, .55f, .65f, .7f, .45f),
        ["Jen"] = new(.9f, .8f, .6f, .3f, .65f), ["Data Dave"] = new(.98f, .25f, .2f, .45f, .92f), ["Boring Bill"] = new(.7f, .82f, .18f, .2f, .25f),
        ["Boss Barbara"] = new(.92f, .28f, .35f, .35f, .6f), ["Joe"] = new(.55f, .8f, .3f, .15f, .45f), ["Kevin"] = new(.75f, .42f, .35f, .75f, .5f),
        ["Old Tom"] = new(.65f, .85f, .5f, .2f, .7f),
    };

    public static PersonaSheet For(string name) => ByName.TryGetValue(name, out var p) ? p : new(name, "Employee", "ordinary", "nothing interesting", "plain-spoken", "Hey.");

    public static bool IsCanonical(string name) => CanonicalStaff.Find(name) != null;
    public static PersonalityProfile ProfileFor(string name) => Profiles.TryGetValue(name, out var p) ? p : new(.5f, .5f, .5f, .5f, .5f);

    public static string BehavioralTell(string name)
    {
        var p = ProfileFor(name);
        if (p.Conscientiousness > .85f) return "notices moved objects, missing procedure, and bad numbers";
        if (p.Agreeableness > .82f) return "gives the player the benefit of the doubt";
        if (p.Extraversion > .82f) return "spreads anything interesting across the floor";
        if (p.Neuroticism > .82f) return "panics quickly when evidence appears";
        if (p.Openness < .25f) return "dismisses odd noises as office nonsense";
        return "keeps a measured eye on the room";
    }

    private static readonly string[] HireNames = { "Derek", "Ashley", "Marcus", "Chloe", "Dev", "Tanya", "Oliver", "Bianca", "Raj", "Ingrid", "Pablo", "Yuki", "Fatima", "Gustav", "Renata", "Kwame" };
    private static readonly Random Rng = new();
    public static PersonalityProfile RollProfile() => new(.2f + (float)Rng.NextDouble() * .75f, .2f + (float)Rng.NextDouble() * .75f, .2f + (float)Rng.NextDouble() * .75f, .2f + (float)Rng.NextDouble() * .75f, .2f + (float)Rng.NextDouble() * .75f);
    public static PersonaSheet RandomSheet()
    {
        string name = HireNames[Rng.Next(HireNames.Length)];
        while (ByName.ContainsKey(name) || GameMode.Instance?.Npcs.Any(n => n.NpcName == name) == true) name = HireNames[Rng.Next(HireNames.Length)];
        Profiles[name] = RollProfile();
        return new(name, "New Hire", "suspiciously cheerful", "is probably three raccoons in a lanyard", "laughs one beat too late", "Hi! I'm new. Where do we file the existential dread?");
    }

    public static string ContextLine(NpcBrain n, GameMode gm)
    {
        var bits = new List<string> { $"You are {n.NpcName}, the {n.Job} in {n.Department}.", $"Personality: {For(n.NpcName).Traits}. Behavioral tell: {BehavioralTell(n.NpcName)}.", $"Current mood: suspicion {n.Suspicion:F0}/100 toward the new employee.", $"Observation specialty: {n.PrimaryObservation}.", $"Daily hook: {n.StaffProfile.RPGHook}." };
        if (gm.Player?.DisguiseOf != null) bits.Add("The new employee is wearing someone else's clothes.");
        if (gm.Player?.Carrying != null) bits.Add("They are carrying something heavy and person-shaped RIGHT NOW.");
        if (!n.Awake) bits.Add("You are unconscious. Reply as a sleep-mumble.");
        if (n.State == NpcState.Seated) bits.Add("You are dozing at your desk.");
        bits.Add($"Your secret (never reveal directly): {For(n.NpcName).Secret}");
        return string.Join(" ", bits);
    }
}
