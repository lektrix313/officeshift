using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Headless end-to-end proof for the interpersonal system. Enabled with:
///   godot --headless --path . -- --smoke-social
/// Plants a belief on boot, then reports every directive planned and every event fired, so
/// the whole chain (claim -> intent -> directive -> travel -> event -> cascade) can be
/// verified in a real running game rather than only in the unit harness.
/// </summary>
public static class SocialSmoke
{
    public static bool Enabled { get; private set; }
    public static int DirectivesPlanned { get; private set; }
    public static int EventsFired { get; private set; }
    public static int CascadedClaims { get; private set; }

    public static void Detect()
    {
        var args = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToArray();
        _reactMode = args.Contains("--smoke-react");
        Enabled = _reactMode || args.Contains("--smoke-social");
        if (Enabled) GD.Print($"[Smoke] armed ({(_reactMode ? "reaction" : "social")} mode)");
    }

    public static readonly HashSet<DirectiveEvent> EventsSeen = new();

    /// <summary>
    /// Seed three beliefs chosen to drive three different branches of the planner:
    /// a plain confrontation, a panicked run to security, and a by-the-book report to HR.
    /// The difference between the last two is personality alone -- same claim, same fear.
    /// </summary>
    public static void Plant(GameMode gm)
    {
        if (!Enabled || _reactMode) return;   // reaction mode stages a crime instead
        var awake = gm.Npcs.Where(n => n.Awake).ToList();
        if (awake.Count < 4) { GD.Print("[Smoke] FAIL: need at least four awake NPCs"); return; }

        var victim = awake[^1];
        var bold = awake.FirstOrDefault(n => n != victim);
        var careless = awake.FirstOrDefault(n => n != victim && n != bold
            && Personas.ProfileFor(n.NpcName).Conscientiousness <= 0.6f);
        var procedural = awake.FirstOrDefault(n => n != victim && n != bold && n != careless
            && Personas.ProfileFor(n.NpcName).Conscientiousness > 0.6f);

        Seed(gm, bold, victim, fear: 0f, label: "confrontation");
        Seed(gm, careless, victim, fear: 6f, label: "panic -> security");
        Seed(gm, procedural, victim, fear: 6f, label: "procedure -> HR");
    }

    private static void Seed(GameMode gm, NpcBrain? holder, NpcBrain victim, float fear, string label)
    {
        if (holder == null) { GD.Print($"[Smoke] skipped {label}: no suitable NPC"); return; }
        gm.Ledger.Of(holder.NpcName, MailStore.PlayerAddress).Nudge(trust: 10f);
        var claim = gm.Ledger.Tell(holder.NpcName, victim.NpcName, ClaimKind.Sabotage, MailStore.PlayerAddress);
        if (claim == null) { GD.Print($"[Smoke] FAIL: {label} claim rejected"); return; }
        claim.Confidence = 0.95f;
        claim.Heat = 0.95f;
        // fear is what routes a serious accusation away from a face-to-face
        if (fear > 0f) gm.Ledger.Of(holder.NpcName, victim.NpcName).Nudge(fear: fear);
        GD.Print($"[Smoke] planted [{label}]: {holder.NpcName} " +
                 $"(conscientiousness {Personas.ProfileFor(holder.NpcName).Conscientiousness:F2}) " +
                 $"believes {victim.NpcName} sabotaged them");
    }

    private static int _traces;
    /// <summary>Rate-limited trace so a stalled pipeline can be located without flooding.</summary>
    public static void Trace(string msg)
    {
        if (!Enabled || _traces >= 12) return;
        _traces++;
        GD.Print($"[Smoke:trace] {msg}");
    }

    public static int Witnesses { get; private set; }

    public static void OnWitness(string npc, ClaimKind kind, float confidence)
    {
        if (!Enabled) return;
        Witnesses++;
        GD.Print($"[Smoke] witness: {npc} saw the player -- {kind} (confidence {confidence:P0})");
    }

    public static void OnDirective(string npc, SocialDirective d)
    {
        if (!Enabled) return;
        DirectivesPlanned++;
        GD.Print($"[Smoke] directive #{DirectivesPlanned}: {npc} -> location={d.Location} action={d.Action} " +
                 $"event={d.Event} target={d.TargetName} speed={d.SpeedMultiplier:F2} urgency={d.Urgency:F2}");
    }

    public static void OnEvent(string npc, SocialDirective d, int cascaded)
    {
        if (!Enabled) return;
        EventsFired++;
        EventsSeen.Add(d.Event);
        CascadedClaims += cascaded;
        GD.Print($"[Smoke] event fired: {npc} {d.Event} at {d.Location}; {cascaded} colleague(s) picked it up");
    }

    private static float _elapsed;
    private static bool _done;

    private static bool _reactMode;
    private static bool _crimeDone;
    private static float _susBefore;
    private static string _crimeVictim = "";
    private static GameMode? _gm;
    private static float _crimeTimer;

    /// <summary>
    /// --smoke-react: stage a real knockout in front of witnesses and report who reacts.
    /// Uses the actual OnBonkLanded path, not a synthetic stimulus, so it exercises exactly
    /// what a player triggers.
    /// </summary>
    public static void StageCrime(GameMode gm, float dt)
    {
        _gm = gm;
        if (!_reactMode || _crimeDone) return;
        _crimeTimer += dt;
        if (_crimeTimer < 35f) return;                // past the 20s arrival window, at desks

        var victim = gm.Npcs.FirstOrDefault(n => n.Awake && n != gm.Guard);
        if (victim == null || gm.Player == null) return;
        _crimeDone = true;

        // put the player right next to the victim so line-of-sight is not the variable
        gm.Player.GlobalPosition = victim.Pos + new Vector3(0.8f, 0f, 0f);
        int near = gm.Npcs.Count(n => n.Awake && n != victim && n.Pos.DistanceTo(victim.Pos) < 18f);
        GD.Print($"[Smoke] staging knockout of {victim.NpcName}; {near} awake colleague(s) within 18m");

        _susBefore = gm.Npcs.Where(n => n != victim).Sum(n => n.Suspicion);
        _crimeVictim = victim.NpcName;
        gm.OnBonkLanded(victim, Vector3.Forward);
        foreach (var n in gm.Npcs.Where(x => x != victim && x.Awake)
                     .OrderBy(x => x.Pos.DistanceTo(victim.Pos)).Take(6))
            GD.Print($"[Smoke]   {n.NpcName,-16} {gm.SightReport(n, victim.Pos)}");
    }

    /// <summary>
    /// Self-terminating: quits as soon as a belief has produced an event AND that event has
    /// been picked up by someone else, or after a hard deadline. Avoids having to guess a
    /// frame count, which made the verdict depend on how far the walk had got.
    /// </summary>
    public static void Update(Node ctx, float dt)
    {
        if (!Enabled || _done) return;
        _elapsed += dt;
        if (_reactMode)
        {
            bool reacted = Witnesses > 0 && DirectivesPlanned > 0;
            if (_elapsed < 55f) return;   // let the reaction play out before judging
            Report();
            ctx.GetTree().Quit(reacted ? 0 : 1);
            return;
        }
        bool satisfied = CascadedClaims > 0
            && EventsSeen.Contains(DirectiveEvent.Confront)
            && (EventsSeen.Contains(DirectiveEvent.Alert) || EventsSeen.Contains(DirectiveEvent.Report));
        if (!satisfied && _elapsed < 240f) return;
        Report();
        ctx.GetTree().Quit(satisfied ? 0 : 1);
    }

    /// <summary>Prints the verdict. Idempotent.</summary>
    public static void Report()
    {
        if (!Enabled || _done) return;
        _done = true;
        if (_reactMode)
        {
            if (_gm != null && _crimeVictim.Length > 0)
            {
                float now = _gm.Npcs.Where(n => n.NpcName != _crimeVictim).Sum(n => n.Suspicion);
                int investigating = _gm.Npcs.Count(n => n.State is NpcState.Curious or NpcState.Report or NpcState.Panic);
                GD.Print($"[Smoke] office suspicion {_susBefore:F1} -> {now:F1}; " +
                         $"{investigating} NPC(s) in Curious/Report/Panic");
            }
            bool r = Witnesses > 0 && DirectivesPlanned > 0;
            GD.Print(r ? "[Smoke] PASS: witnesses formed beliefs about the player and acted"
                       : "[Smoke] FAIL: the office did not react to the player");
            return;
        }
        bool ok = DirectivesPlanned > 0 && CascadedClaims > 0
            && EventsSeen.Contains(DirectiveEvent.Confront)
            && (EventsSeen.Contains(DirectiveEvent.Alert) || EventsSeen.Contains(DirectiveEvent.Report));
        GD.Print($"[Smoke] witnesses={Witnesses} directives={DirectivesPlanned} events={EventsFired} cascaded_claims={CascadedClaims}");
        GD.Print($"[Smoke] branches exercised: {string.Join(", ", EventsSeen.OrderBy(e => e.ToString()))}");
        GD.Print(ok ? $"[Smoke] PASS: belief became behaviour and spread, in {_elapsed:F1}s of sim time"
                    : "[Smoke] FAIL: no directive reached its event");
    }
}
