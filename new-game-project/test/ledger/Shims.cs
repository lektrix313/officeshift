// Minimal stand-ins so the REAL SocialLedger.cs can be compiled and exercised outside Godot.
// Only the handful of members the ledger actually touches are provided.
namespace Godot
{
    public static class Mathf
    {
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}

public sealed record PersonalityProfile(float Conscientiousness, float Agreeableness, float Extraversion, float Neuroticism, float Openness)
{
    public float ForgivenessMultiplier => 0.75f + Agreeableness * 0.5f;
}

public static class Personas
{
    public static readonly Dictionary<string, PersonalityProfile> Test = new();
    public static PersonalityProfile ProfileFor(string name) =>
        Test.TryGetValue(name, out var p) ? p : new(.5f, .5f, .5f, .5f, .5f);
}

public static class MailStore { public const string PlayerAddress = "You"; }
