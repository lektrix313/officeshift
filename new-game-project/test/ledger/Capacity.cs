using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Counts how many genuinely distinct instruction blocks the maths can emit.</summary>
public static class Capacity
{
    public static void Report()
    {
        var actor = new CarnageActor("X", "j", "q", "g", 0.5f, 0.5f, 0f);
        var toneSets = new HashSet<string>();
        var full = new HashSet<string>();
        var narratives = new HashSet<string>();
        var synth = new CarnageSynthesizer();

        var ext = new[] { 0.05f, 0.25f, 0.5f, 0.75f, 0.95f };
        var resp = new[] { -1.5f, -0.8f, 0f, 0.8f, 1.5f };
        var attr = new[] { 0f, 0.9f, 1.6f };
        int samples = 0;
        for (float p = -2f; p <= 2f; p += 0.2f)
        for (float g = -2f; g <= 2f; g += 0.2f)
        for (float phi = 0f; phi <= 3f; phi += 0.3f)
        foreach (var e in ext)
        foreach (var r in resp)
        foreach (var at in attr)
        {
            samples++;
            var t = new CarnageTensor(p, g, phi, r, at);
            var c = PromptVectorController.Build(actor with { Extraversion = e }, "T", t);
            toneSets.Add(string.Join("|", c.ToneFilters));
            full.Add(string.Join("|", c.ToneFilters) + "//" + c.Pacing + "//" + string.Join("|", c.Forbidden));
            narratives.Add(synth.Synthesize(actor, actor with { Name = "Y" },
                CarnageSynthesizer.CatalystFor("X", "Y", t), t).Narrative);
        }

        Console.WriteLine($"  sampled office states          : {samples:N0}");
        Console.WriteLine($"  distinct tone-filter sets      : {toneSets.Count}");
        Console.WriteLine($"  distinct full constraint blocks: {full.Count}");
        Console.WriteLine($"  distinct offline narratives    : {narratives.Count}");
    }
}
