using System;
using System.Linq;

/// <summary>Behavioural gauntlet for the interpersonal ledger. Runs the real SocialLedger.cs.</summary>
public static class SocialLedgerGauntlet
{
    private static int _fail;

    private static void Check(string name, bool pass)
    {
        Console.WriteLine($"{(pass ? "PASS" : "FAIL")}: {name}");
        if (!pass) _fail++;
    }

    public static int Main()
    {
        // Bob: conscientious, disagreeable, anxious. Jen: agreeable. Pam: loud gossip.
        Personas.Test["Bob"] = new(.9f, .25f, .4f, .8f, .3f);
        Personas.Test["Jen"] = new(.6f, .8f, .5f, .4f, .7f);
        Personas.Test["Pam"] = new(.4f, .6f, .95f, .5f, .6f);
        var staff = new[] { "Bob", "Jen", "Pam" };

        // --- asymmetry: the whole point ---
        var l = new SocialLedger();
        l.Register(staff);
        l.Of("Bob", "Jen").Nudge(warmth: 6f);
        Check("opinions are directed, not shared",
            Math.Abs(l.Of("Bob", "Jen").Warmth - l.Of("Jen", "Bob").Warmth) > 1f);

        // --- trust gates belief: the same lie lands differently ---
        var trusting = new SocialLedger(); trusting.Register(staff);
        var wary = new SocialLedger(); wary.Register(staff);
        trusting.Of("Bob", "You").Nudge(trust: 9f);
        wary.Of("Bob", "You").Nudge(trust: -9f);
        var believed = trusting.Tell("Bob", "Jen", ClaimKind.StoleCredit, "You")!;
        var doubted = wary.Tell("Bob", "Jen", ClaimKind.StoleCredit, "You")!;
        Check("a trusted source is believed more than a distrusted one",
            believed.Confidence > doubted.Confidence * 2f);
        Check("distrusted claims still register, just weakly", doubted.Confidence is > 0f and < 0.3f);

        // --- hearing it already moves the sheet, before any confrontation ---
        Check("hearsay colours opinion immediately", trusting.Of("Bob", "Jen").Resentment > 0f);
        Check("an unrelated colleague is untouched", Math.Abs(trusting.Of("Bob", "Pam").Resentment) < 0.001f);

        // --- corroboration hardens a belief ---
        float before = believed.Confidence;
        trusting.Tell("Bob", "Jen", ClaimKind.StoleCredit, "Pam");
        Check("a second source hardens the same claim", believed.Confidence > before);

        // --- distance distorts ---
        var chain = new SocialLedger(); chain.Register(staff);
        var direct = chain.Tell("Bob", "Jen", ClaimKind.Sabotage, "You", hops: 0)!;
        var thirdHand = chain.Tell("Pam", "Jen", ClaimKind.Sabotage, "You", hops: 3)!;
        Check("confidence decays with each hop", direct.Confidence > thirdHand.Confidence);

        // --- intent: conviction becomes action ---
        var act = new SocialLedger(); act.Register(staff);
        act.Of("Bob", "You").Nudge(trust: 10f);
        act.Tell("Bob", "Jen", ClaimKind.StoleCredit, "You");
        var intent = act.NextIntent("Bob");
        Check("a strong belief produces an intent", intent is not null);
        Check("intent points at the person it is about", intent!.Target == "Jen");
        Check("no intent without a belief", act.NextIntent("Pam") is null);
        Check("friendly claims never produce a confrontation",
            new Func<bool>(() => { var f = new SocialLedger(); f.Register(staff);
                f.Of("Bob", "You").Nudge(trust: 10f);
                f.Tell("Bob", "Jen", ClaimKind.CoveredForMe, "You");
                return f.NextIntent("Bob") is null; })());

        // --- confrontation fallout, both directions ---
        var con = new SocialLedger(); con.Register(staff);
        con.Of("Bob", "You").Nudge(trust: 10f);
        var claim = con.Tell("Bob", "Jen", ClaimKind.StoleCredit, "You")!;
        float jenResentBefore = con.Of("Jen", "Bob").Resentment;
        con.ResolveConfrontation("Bob", "Jen", claim, accusedDenies: true);
        Check("being accused breeds resentment even when innocent",
            con.Of("Jen", "Bob").Resentment > jenResentBefore);
        Check("a confrontation is not repeated", claim.Acted);
        Check("a convincing denial costs the messenger their credibility",
            con.Of("Bob", "You").Trust < 10f);

        // --- consolidation: detail fades, feeling does not ---
        var mem = new SocialLedger(); mem.Register(staff);
        mem.Of("Bob", "You").Nudge(trust: 10f);
        mem.Tell("Bob", "Jen", ClaimKind.Sabotage, "You");
        float grudge = mem.Of("Bob", "Jen").Resentment;
        for (int i = 0; i < 4000; i++) mem.Tick(1f);
        Check("claims eventually fade from memory", mem.ClaimsHeldBy("Bob").Count == 0);
        Check("the grudge outlives the memory of why", mem.Of("Bob", "Jen").Resentment > grudge * 0.25f);

        // --- personality actually matters ---
        var pa = new SocialLedger(); pa.Register(staff);
        pa.Of("Bob", "You").Nudge(trust: 10f);
        pa.Of("Pam", "You").Nudge(trust: 10f);
        pa.Tell("Bob", "Jen", ClaimKind.StoleCredit, "You");
        pa.Tell("Pam", "Jen", ClaimKind.StoleCredit, "You");
        Check("the disagreeable take slights harder than the agreeable",
            pa.Of("Bob", "Jen").Resentment > pa.Of("Pam", "Jen").Resentment);

        // --- the player's lever ---
        Check("parser reads 'X said you...' as talking behind your back",
            ClaimParser.Extract("Jen said you were useless in the standup", staff, "Bob")
                is { About: "Jen", Kind: ClaimKind.TalkedBehindBack });
        Check("parser reads credit theft",
            ClaimParser.Extract("Pam took credit for your deck again", staff, "Bob")
                is { About: "Pam", Kind: ClaimKind.StoleCredit });
        Check("parser ignores the recipient themselves",
            ClaimParser.Extract("Bob said you were useless", staff, "Bob") is null);
        Check("parser stays quiet on harmless mail",
            ClaimParser.Extract("are we still on for lunch", staff, "Bob") is null);
        Check("an NPC cannot hold a claim about themselves",
            new SocialLedger().Tell("Bob", "Bob", ClaimKind.Slacking, "You") is null);

        // ---------- carnage tensor + prompt vector ----------
        var actor = new CarnageActor("Pam", "HR", "calls disasters learning opportunities",
            "Let's turn this into a growth opportunity.", 0.72f, 0.78f, 0f);

        // the mask: agreeable people show warmth they do not feel, so dissonance falls out
        // of personality rather than being authored
        var spite = new Opinion(); spite.Nudge(warmth: -4f, resentment: 8f);
        var agreeable = CarnageTensor.FromOpinion(spite, 0.9f, 0f);
        var blunt = CarnageTensor.FromOpinion(spite, 0.1f, 0f);
        Check("private contempt reads as contempt", agreeable.G < -1f && blunt.G < -1f);
        Check("the agreeable mask their contempt in public", agreeable.P > blunt.P);
        Check("masking is what creates dissonance", agreeable.Dissonance > blunt.Dissonance);

        // the bug from the reference: dissonance must not be shadowed by the gossip branch
        var synth = new CarnageSynthesizer();
        var hypocrisy = new CarnageTensor(1.4f, -1.8f, 0.3f);
        var reportH = synth.Synthesize(actor, actor with { Name = "Bob" }, "office_printer", hypocrisy);
        Check("high hypocrisy reaches the dissonance narrator",
            ((string)reportH.Metadata["dominant_vector_source"]).Contains("Dissonance"));

        Check("synthesis is deterministic",
            synth.Synthesize(actor, actor with { Name = "Bob" }, "office_printer", hypocrisy).Narrative == reportH.Narrative);
        Check("order matters: Pam-on-Bob differs from Bob-on-Pam",
            synth.Synthesize(actor with { Name = "Bob" }, actor, "office_printer", hypocrisy).Narrative != reportH.Narrative);
        Check("metadata carries the exact maths that triggered it",
            reportH.Metadata.ContainsKey("P") && reportH.Metadata.ContainsKey("Dissonance"));

        // prompt guardrails
        var c = PromptVectorController.Build(actor, "Bob", hypocrisy);
        Check("extreme dissonance reaches the top hypocrisy band",
            c.ToneFilters.Any(x => x.Contains("HYPOCRISY COLLAPSE")));
        Check("mid dissonance reads as doublespeak, not collapse",
            PromptVectorController.Build(actor, "Bob", new CarnageTensor(0.9f, -0.9f, 0f))
                .ToneFilters.Any(x => x.Contains("DOUBLE-SPEAK")));
        Check("bands are ordered, not interchangeable",
            PromptVectorController.Build(actor, "Bob", new CarnageTensor(0.5f, -0.3f, 0f))
                .ToneFilters.Any(x => x.Contains("MILD PERFORMANCE")));
        Check("camaraderie filter fires on high public affinity", c.ToneFilters.Any(x => x.Contains("CAMARADERIE")));
        Check("tone filters are additive, not exclusive", c.ToneFilters.Count >= 2);
        Check("extraversion drives pacing", c.Pacing.Contains("Muted") == false && c.Pacing.Length > 0);
        var quiet = PromptVectorController.Build(actor with { Extraversion = 0.1f }, "Bob", hypocrisy);
        Check("the quiet get muted pacing", quiet.Pacing.Contains("Muted"));
        Check("a neutral office still gets a filter",
            PromptVectorController.Build(actor, "Bob", new CarnageTensor(0f, 0f, 0f))
                .ToneFilters.Any(x => x.Contains("NEUTRALITY")));
        Check("the mask holds when hostility is low",
            PromptVectorController.Build(actor, "Bob", new CarnageTensor(-0.5f, -0.5f, 0f))
                .Forbidden.Any(x => x.Contains("must not drop")));
        Check("the mask strains in the middle band",
            PromptVectorController.Build(actor, "Bob", new CarnageTensor(-1.2f, -1.2f, 0f))
                .Forbidden.Any(x => x.Contains("straining")));
        Check("the mask slips above it",
            PromptVectorController.Build(actor, "Bob", new CarnageTensor(-1.9f, -1.9f, 0f))
                .Forbidden.Any(x => x.Contains("may be open")));
        Check("physics block carries the floats for tracing",
            c.Physics.ContainsKey("cognitive_dissonance_delta") && c.Physics.ContainsKey("hostility"));
        Check("prompt block is non-empty and labelled", c.ToPromptBlock().Contains("LINGUISTIC BOUNDARIES"));

        // ---------- directives: location, action, event ----------
        var led = new SocialLedger(); led.Register(staff);
        led.Of("Bob", "You").Nudge(trust: 10f);
        var sabotage = led.Tell("Bob", "Jen", ClaimKind.Sabotage, "You")!;
        sabotage.Confidence = 0.9f; sabotage.Heat = 0.9f;

        // terrified accountant + serious crime -> run to authority, not a face-to-face
        var scared = new CarnageTensor(-0.5f, -1.2f, 1.8f);
        var flee = DirectivePlanner.Plan("Bob", sabotage, scared, 0.85f, 0.2f, 0.3f)!;
        Check("fear + a serious claim routes to security",
            flee.Location == DirectiveLocation.Security && flee.Event == DirectiveEvent.Alert);
        Check("high paranoia makes them run", flee.Action == DirectiveAction.Run);
        Check("running is faster than walking", flee.SpeedMultiplier > 1.5f);
        Check("the action carries an animation hint", flee.AnimationTokens.Contains("run"));

        // same claim, conscientious NPC -> paperwork instead of panic
        var procedural = DirectivePlanner.Plan("Bob", sabotage, scared, 0.85f, 0.9f, 0.3f)!;
        Check("the conscientious file a report instead",
            procedural.Location == DirectiveLocation.HumanResources && procedural.Event == DirectiveEvent.Report);

        // same claim, fearless and disagreeable -> straight at them
        var bold = new CarnageTensor(-1.5f, -1.5f, 0.1f);
        var headOn = DirectivePlanner.Plan("Bob", sabotage, bold, 0.1f, 0.5f, 0.5f)!;
        Check("the fearless confront in person",
            headOn.Location == DirectiveLocation.TargetPerson && headOn.Event == DirectiveEvent.Confront);
        Check("anger shows in the walk", headOn.Action == DirectiveAction.March);

        // timid and quiet -> withdraw, stats still move
        var petty = led.Tell("Pam", "Jen", ClaimKind.Slacking, "You")!;
        petty.Confidence = 0.5f; petty.Heat = 0.5f;
        var withdraw = DirectivePlanner.Plan("Pam", petty, new CarnageTensor(0f, -0.2f, 0.2f), 0.9f, 0.5f, 0.1f)!;
        Check("the timid withdraw to their desk",
            withdraw.Location == DirectiveLocation.OwnDesk && withdraw.Event == DirectiveEvent.AdjustStats);
        Check("withdrawing is slower than walking", withdraw.SpeedMultiplier < 1f);

        // sociable but gutless -> breakroom to spread it
        var spread = DirectivePlanner.Plan("Pam", petty, new CarnageTensor(0f, -0.2f, 0.2f), 0.9f, 0.5f, 0.8f)!;
        Check("the sociable take it to the breakroom",
            spread.Location == DirectiveLocation.Breakroom && spread.Event == DirectiveEvent.Gossip);

        // a kindness produces a directive too
        var kind = led.Tell("Bob", "Pam", ClaimKind.CoveredForMe, "You")!;
        kind.Confidence = 0.8f; kind.Heat = 0.8f;
        var thanks = DirectivePlanner.Plan("Bob", kind, bold, 0.5f, 0.5f, 0.5f)!;
        Check("friendly beliefs produce reconciliation, not confrontation",
            thanks.Event == DirectiveEvent.Reconcile);

        Check("a belief nobody believes produces no directive",
            DirectivePlanner.Plan("Bob", new Claim { About = "Jen", Kind = ClaimKind.Slacking,
                Source = "You", Confidence = 0.05f, Heat = 0.1f }, bold, 0.5f, 0.5f, 0.5f) is null);
        Check("planning is deterministic",
            DirectivePlanner.Plan("Bob", sabotage, scared, 0.85f, 0.2f, 0.3f)!.Location == flee.Location);

        Console.WriteLine("--- variation capacity ---");
        Capacity.Report();

        Console.WriteLine(_fail == 0
            ? "PASS: social ledger gauntlet - all checks"
            : $"FAIL: {_fail} checks");
        return _fail == 0 ? 0 : 1;
    }
}
