using Godot;
using System.Collections.Generic;

// ============================================================================
// AI context + director — ports game.ts updateNpcs()/updateGuard()/npc.ts
// state primitives. The director ticks ALL brains from GameMode each frame.
// ============================================================================

public sealed class AiContext
{
    public required Vector3 PlayerPos;
    public bool PlayerVisibleToAi = true;
    public bool PlayerCrouching;
    public bool PlayerCarrying;
    public string? PlayerDisguise;
    public float PlayerActivity;            // playerSusActivity() port: 0, 1, 1.5, 2.5 or 3
    public NpcBrain? Guard;
    public required List<NpcBrain> Npcs;
    public required World WorldRef;
    public required BloodSystem Blood;
    public double AlertTimer;
    public bool Evacuating;
    public Vector3 EvacPoint;
    public Vector3 BathroomPoint;
    public Vector3 CoffeePoint;
    public bool CoffeeSpiked;
    public bool StinkActive;
    public Vector3 StinkPos;
    public bool NoiseFresh;
    public Vector3 NoisePos;

    public Action<string, ToastKind>? Toast;
    public Action? AlarmSfx;
    public Func<NpcBrain, Vector3, float, bool>? CanSee;
    public Action<NpcBrain>? OnReportReachedGuard;
    public Action? OnPlayerCaught;
}

public partial class NpcBrain : Node
{
    public NpcBody Body { get; private set; } = null!;
    public string NpcName => Body.DisplayName;
    public Archetype Arch { get; private set; }
    public ArchetypeSpec Spec { get; private set; } = Specs.Table[Archetype.Drone];
    public string Zone { get; set; } = "drone";

    public NpcState State { get; set; } = NpcState.Routine;
    public float Suspicion { get; set; }
    public bool Looted { get; set; }
    public bool GossipSpreadDone { get; set; }
    public bool PoolSpawned { get; set; }
    public bool CreepToastDone { get; set; }
    public bool CrabToastDone { get; set; }
    public float DistractTimer { get; set; }
    public bool Talking { get; set; }
    public Vector3? DirectiveTarget { get; set; }
    public string? DirectiveZone { get; set; }
    public float DirectiveTimer { get; set; }
    public Vector3 HomePos { get; set; }
    public float BathroomTimer { get; set; }
    public bool StinkReacted { get; set; }

    public bool Awake => State != NpcState.Out && State != NpcState.Hidden;
    public Vector3 Pos => Body.Position;

    // curiosity / panic bookkeeping (director-owned)
    public Vector3? InvestigateTarget;
    public object? InvestigateRef;
    public EvidenceKind? InvestigateKind;
    public object? PanicRef;
    public EvidenceKind? PanicKind;
    public float PanicTimer;
    public float PanicDuration;
    public int PanicShownSecond = -1;
    public Vector3? ReportTarget;
    public Vector3 LastSeenPlayer;
    public float LostSightTimer;
    public Vector3? MoveTarget;
    public float PauseTimer;
    public bool Moving;

    public static NpcBrain Create(NpcBody body, string zone)
    {
        var brain = new NpcBrain();
        brain.Body = body;
        brain.Arch = body.Arch;
        brain.Spec = body.Spec;
        brain.Zone = zone;
        brain.HomePos = body.Position;
        brain.Name = $"Brain_{body.DisplayName}";
        return brain;
    }

    // ---------- state primitives (port of npc.ts) ----------

    public void AddSuspicion(float amount)
    {
        if (!Awake) return;
        Suspicion = System.MathF.Min(100f, Suspicion + amount);
    }

    public void KnockOut(Vector3 flopDir)
    {
        State = NpcState.Out;
        Suspicion = 0;
        InvestigateTarget = null;
        InvestigateRef = null;
        InvestigateKind = null;
        PanicRef = null;
        PanicTimer = 0;
        PoolSpawned = false;
        Body.ClearEmote();
        ShowBang(false);
        Moving = false;

        // Preferred: physics crumple via factory; fallback: baked lying pose.
        Node? rd = RagdollFactory.Spawn(Body, flopDir, null);
        Body.ActiveRagdoll = rd;
        if (rd == null) Body.PlayKnockoutPose();
        Body.ShowSleeping(true);
    }

    public void ClearRagdoll()
    {
        RagdollFactory.Clear(Body);
        Body.ActiveRagdoll = null;
    }

    public void StartCurious(Vector3 target, object evidenceRef, EvidenceKind kind)
    {
        State = NpcState.Curious;
        InvestigateTarget = target;
        InvestigateRef = evidenceRef;
        InvestigateKind = kind;
        Body.ShowEmote("?");
    }

    public void StartPanic(EvidenceKind kind, object evidenceRef, float duration)
    {
        State = NpcState.Panic;
        PanicKind = kind;
        PanicRef = evidenceRef;
        PanicTimer = duration;
        PanicDuration = duration;
        PanicShownSecond = -1;
        Moving = false;
    }

    /// <summary>Ticks the countdown with per-second "!!{N}" emote; true when expired.</summary>
    public bool UpdatePanicTick(double dt)
    {
        PanicTimer -= (float)dt;
        int sec = System.Math.Max(0, (int)System.MathF.Ceiling(PanicTimer));
        if (sec != PanicShownSecond)
        {
            PanicShownSecond = sec;
            Body.ShowEmote(sec > 0 ? $"!!{sec}" : "!!");
        }
        return PanicTimer <= 0;
    }

    public void ShrugItOff(float minSus = 0f)
    {
        State = NpcState.Routine;
        InvestigateTarget = null;
        InvestigateRef = null;
        InvestigateKind = null;
        PanicRef = null;
        PanicKind = null;
        PanicTimer = 0;
        MoveTarget = null;
        PauseTimer = 2f;
        Body.ClearEmote();
        if (Suspicion < minSus) Suspicion = minSus;
    }

    public void StartReport(Vector3 guardPos)
    {
        State = NpcState.Report;
        ReportTarget = guardPos;
        Body.ClearEmote();
        ShowBang(true);
    }

    public void CalmDown()
    {
        ShrugItOff();
        Suspicion = 30;
        ShowBang(false);
    }

    private Label3D? _bang;
    private void ShowBang(bool on)
    {
        if (on && _bang == null)
        {
            _bang = new Label3D
            {
                Text = "!!",
                FontSize = 64,
                OutlineSize = 14,
                Modulate = Color.FromHtml("ff5a4a"),
                Position = new Vector3(0f, 2.75f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.005f,
            };
            Body.AddChild(_bang);
        }
        else if (!on && _bang != null)
        {
            _bang.QueueFree();
            _bang = null;
        }
    }

    // ---------- movement (port of stepToward) ----------

    /// <summary>Move toward target on XZ with collision pushout. True on arrival.</summary>
    public bool StepToward(World world, Vector3 target, double dt, float? speedOverride = null)
    {
        float speed = speedOverride ?? Spec.Speed;
        Vector3 pos = Body.Position;
        float dx = target.X - pos.X;
        float dz = target.Z - pos.Z;
        float dist = System.MathF.Sqrt(dx * dx + dz * dz);
        if (dist < Bal.ArrivalDist)
        {
            Moving = false;
            return true;
        }
        float step = System.MathF.Min(dist, speed * (float)dt);
        pos.X += dx / dist * step;
        pos.Z += dz / dist * step;
        world.ResolveCircle(ref pos, Bal.NpcRadius);
        Body.Position = pos;

        Moving = true;
        float desired = System.MathF.Atan2(dx, dz);
        float diff = desired - Body.Facing;
        while (diff > System.MathF.PI) diff -= System.MathF.Tau;
        while (diff < -System.MathF.PI) diff += System.MathF.Tau;
        Body.Facing += diff * System.MathF.Min(1f, (float)dt * 8f);
        return false;
    }
}

public static class AiDirector
{
    /// <summary>Read by GameMode after Tick().</summary>
    public static class Outputs
    {
        public static float MaxSus;
        public static bool Watched;
    }

    /// <summary>Port of game.ts updateNpcs().</summary>
    public static void Tick(List<NpcBrain> npcs, AiContext ctx, double dt)
    {
        float maxSus = 0f;
        bool watched = false;

        foreach (var n in npcs)
        {
            if (n == ctx.Guard || !n.Awake) continue;

            // --- laughing at your photocopied face ---
            if (n.DistractTimer > 0 && n.State != NpcState.Report)
            {
                n.DistractTimer -= (float)dt;
                if (n.DistractTimer <= 0) n.Body.ClearEmote();
                continue; // too busy laughing to notice anything
            }

            // --- curiosity: walking over to inspect evidence ---
            if (n.State == NpcState.Curious)
            {
                bool gone = n.InvestigateKind switch
                {
                    EvidenceKind.Body => n.InvestigateRef is not NpcBrain b || b.State != NpcState.Out || !b.Body.Visible,
                    EvidenceKind.Noise => n.InvestigateRef is not NoiseRef nr || nr.Expired,
                    _ => n.InvestigateRef is not BloodSystem.Splat s || !ctx.Blood.Contains(s),
                };
                if (gone)
                {
                    n.ShrugItOff();
                    n.AddSuspicion(Bal.VanishInvestigateSus);
                    ctx.Toast?.Invoke($"{n.NpcName}: \"Huh. Could've sworn I saw… something.\"", ToastKind.Info);
                }
                else if (n.InvestigateTarget.HasValue &&
                         n.StepToward(ctx.WorldRef, n.InvestigateTarget.Value, dt, n.Spec.Speed * Bal.CuriousSpeedMul))
                {
                    if (n.InvestigateKind == EvidenceKind.Noise)
                    {
                        // poked around the noise: nothing there, back to work
                        n.ShrugItOff(8f);
                        ctx.Toast?.Invoke($"{n.NpcName} pokes around. \"Probably just the building settling.\"", ToastKind.Info);
                    }
                    else
                    {
                        var kind = n.InvestigateKind!.Value;
                        float duration = kind == EvidenceKind.Body ? Bal.PanicDurationBody : Bal.PanicDurationBlood;
                        n.StartPanic(kind, n.InvestigateRef!, duration);
                        if (kind == EvidenceKind.Body && n.InvestigateRef is NpcBrain victim)
                            ctx.Toast?.Invoke($"{n.NpcName} found {victim.NpcName}'s body!! Security in {(int)duration}s — unless someone stops them.", ToastKind.Chaos);
                        else
                            ctx.Toast?.Invoke($"{n.NpcName} is staring at a pool of blood. Squealing in {(int)duration}s — mop it or stop them.", ToastKind.Warn);
                        ctx.AlarmSfx?.Invoke();
                    }
                }
                maxSus = System.MathF.Max(maxSus, n.Suspicion);
                PushVisual(n, dt);
                continue; // focused on the evidence, not on you
            }

            // --- panic: countdown until they run to security ---
            if (n.State == NpcState.Panic)
            {
                bool gone = n.PanicKind == EvidenceKind.Body
                    ? n.PanicRef is not NpcBrain pb || pb.State != NpcState.Out || !pb.Body.Visible
                    : n.PanicRef is not BloodSystem.Splat ps || !ctx.Blood.Contains(ps);
                if (gone)
                {
                    n.ShrugItOff(Bal.VanishPanicSus);
                    ctx.Toast?.Invoke($"{n.NpcName} looks again — nothing there. \"…Not paid enough for this.\"", ToastKind.Info);
                }
                else if (n.UpdatePanicTick(dt))
                {
                    if (n.Arch == Archetype.Grifter)
                    {
                        n.ShrugItOff();
                        n.Suspicion = 20;
                        ctx.Toast?.Invoke($"{n.NpcName} saw everything… and wants a cut. You gained an accomplice.", ToastKind.Success);
                    }
                    else if (n.Spec.Reports)
                    {
                        if (ctx.Guard is { } g && g.Awake)
                        {
                            n.StartReport(g.Pos);
                            ctx.Toast?.Invoke($"{n.NpcName} is RUNNING to security! Intercept or improvise!", ToastKind.Warn);
                            ctx.AlarmSfx?.Invoke();
                        }
                        else
                        {
                            ctx.Toast?.Invoke($"{n.NpcName} ran to tell security… but security is currently a floor lamp.", ToastKind.Chaos);
                            n.CalmDown();
                        }
                    }
                }
                maxSus = System.MathF.Max(maxSus, n.Suspicion);
                PushVisual(n, dt);
                continue;
            }

            // --- movement / behaviour ---
            if (n.State == NpcState.Report)
            {
                if (n.ReportTarget.HasValue &&
                    n.StepToward(ctx.WorldRef, n.ReportTarget.Value, dt, n.Spec.Speed * Bal.ReportSpeedMul))
                {
                    ctx.OnReportReachedGuard?.Invoke(n);
                }
            }
            else if (n.State == NpcState.Seated)
            {
                n.Moving = false; // sweet dreams
            }
            else if (n.State == NpcState.Routine)
            {
                if (n.Talking)
                {
                    n.Moving = false;
                }
                else if (ctx.Evacuating)
                {
                    n.MoveTarget = ctx.EvacPoint;
                    if (n.StepToward(ctx.WorldRef, ctx.EvacPoint, dt, n.Spec.Speed * 1.5f))
                        n.Moving = false;
                }
                else if (n.BathroomTimer > 0f)
                {
                    n.BathroomTimer -= (float)dt;
                    if (n.Pos.DistanceTo(ctx.BathroomPoint) > 1.1f)
                        n.StepToward(ctx.WorldRef, ctx.BathroomPoint, dt, n.Spec.Speed);
                    else
                        n.Moving = false;
                }
                else if (n.DirectiveTimer > 0f)
                {
                    n.DirectiveTimer -= (float)dt;
                    if (n.DirectiveTarget.HasValue)
                    {
                        bool arrived = n.StepToward(ctx.WorldRef, n.DirectiveTarget.Value, dt);
                        if (arrived)
                        {
                            n.Moving = false;
                            if (ctx.CoffeeSpiked && WorldEvents.SpikeUsesLeft > 0 &&
                                n.Pos.DistanceTo(ctx.CoffeePoint) < 1.5f)
                            {
                                WorldEvents.SpikeUsesLeft--;
                                n.BathroomTimer = 14f;
                                n.DirectiveTimer = 0f;
                                n.DirectiveTarget = null;
                                ctx.Toast?.Invoke($"{n.NpcName} downs the coffee. Somewhere, a timer starts.", ToastKind.Chaos);
                            }
                        }
                    }
                    if (n.DirectiveTimer <= 0f)
                    {
                        n.DirectiveTarget = null;
                        n.PauseTimer = 0.5f;
                    }
                }
                else if (ctx.StinkActive && n.Pos.DistanceTo(ctx.StinkPos) < 9f)
                {
                    // flee to the farthest known waypoint from the stink
                    var pts = ctx.WorldRef.WaypointsFor(n.Zone);
                    var flee = n.Pos;
                    float bestD = -1f;
                    foreach (var pt in pts)
                    {
                        float d = pt.DistanceTo(ctx.StinkPos);
                        if (d > bestD) { bestD = d; flee = pt; }
                    }
                    n.StepToward(ctx.WorldRef, flee, dt, n.Spec.Speed * 1.2f);
                    if (!n.StinkReacted)
                    {
                        n.StinkReacted = true;
                        n.Body.ShowEmote(":(");
                        ctx.Toast?.Invoke($"{n.NpcName} gags. Someone microwaved FISH.", ToastKind.Chaos);
                    }
                }
                else if (n.PauseTimer > 0)
                {
                    n.PauseTimer -= (float)dt;
                    n.Moving = false;
                }
                else if (!n.MoveTarget.HasValue || n.StepToward(ctx.WorldRef, n.MoveTarget.Value, dt))
                {
                    var pts = ctx.WorldRef.WaypointsFor(n.Zone);
                    if (pts.Length > 0)
                        n.MoveTarget = pts[(int)(GD.RandRange(0, pts.Length - 1))];
                    n.PauseTimer = 1.5f + (float)GD.RandRange(0.0, 4.0);
                }
            }

            // --- perception ---
            if (n.State != NpcState.Seated && !n.Talking && !ctx.Evacuating)
            {
                bool seesPlayer = ctx.CanSee?.Invoke(n, ctx.PlayerPos, 1f) ?? false;
                float playerDist = n.Pos.DistanceTo(ctx.PlayerPos);
                float disguiseMul = ctx.PlayerDisguise != null ? Bal.SusDisguiseMul : 1f;

                if (seesPlayer && ctx.PlayerActivity > 0)
                {
                    watched = true;
                    n.AddSuspicion(ctx.PlayerActivity * n.Spec.Rate * Bal.SusGainScale * disguiseMul * (float)dt);
                    n.LastSeenPlayer = ctx.PlayerPos;
                }
                else if (seesPlayer && ctx.PlayerActivity == 0)
                {
                    if (playerDist < Bal.CreepRange)
                    {
                        n.AddSuspicion(Bal.CreepRate * n.Spec.Rate * disguiseMul * (float)dt);
                        if (!n.CreepToastDone && n.Suspicion > 15)
                        {
                            n.CreepToastDone = true;
                            ctx.Toast?.Invoke($"{n.NpcName}: \"Do I… know you? You're standing VERY close.\"", ToastKind.Warn);
                        }
                    }
                    else if (ctx.PlayerCrouching && playerDist < Bal.CrabRange)
                    {
                        n.AddSuspicion(Bal.CrabRate * n.Spec.Rate * disguiseMul * (float)dt);
                        if (!n.CrabToastDone && n.Suspicion > 12)
                        {
                            n.CrabToastDone = true;
                            ctx.Toast?.Invoke($"{n.NpcName} is wondering why you're crab-walking past the cubicles.", ToastKind.Warn);
                        }
                    }
                }
                else if (n.Suspicion > 0 && n.Suspicion < 50)
                {
                    n.Suspicion = System.MathF.Max(0f, n.Suspicion - Bal.SusDecayPerSec * (float)dt);
                }

                // --- noticing evidence from afar ---
                if (n.State == NpcState.Routine)
                {
                    bool spotted = false;
                    foreach (var b in npcs)
                    {
                        if (b == n || b.State != NpcState.Out || !b.Body.Visible) continue;
                        if (ctx.CanSee?.Invoke(n, b.Pos, 1f) ?? false)
                        {
                            n.StartCurious(b.Pos, b, EvidenceKind.Body);
                            ctx.Toast?.Invoke($"{n.NpcName} spotted something person-shaped on the floor. \"…Hello?\"", ToastKind.Warn);
                            spotted = true;
                            break;
                        }
                    }
                    if (!spotted)
                    {
                        foreach (var splat in ctx.Blood.All)
                        {
                            if (ctx.CanSee?.Invoke(n, splat.Pos, Bal.BloodSeenRangeMul) ?? false)
                            {
                                n.StartCurious(splat.Pos, splat, EvidenceKind.Blood);
                                ctx.Toast?.Invoke($"{n.NpcName} noticed a stain. \"Is that… ketchup?\"", ToastKind.Info);
                                break;
                            }
                        }
                    }
                }
            }

            // --- gossip spreads the word ---
            if (n.Arch == Archetype.Gossip && !n.GossipSpreadDone && n.Suspicion >= Bal.GossipTriggerSus)
            {
                n.GossipSpreadDone = true;
                int count = 0;
                foreach (var o in npcs)
                {
                    if (o == n || !o.Awake || o == ctx.Guard) continue;
                    if (o.Pos.DistanceTo(n.Pos) < Bal.GossipRadius)
                    {
                        o.AddSuspicion(Bal.GossipSpreadAmount);
                        count++;
                    }
                }
                ctx.Toast?.Invoke($"{n.NpcName} is telling EVERYONE. ({count} coworkers looped in)", ToastKind.Warn);
            }

            // --- suspicion climax ---
            if (n.Suspicion >= 100 && n.State == NpcState.Routine)
            {
                if (n.Arch == Archetype.Grifter)
                {
                    n.CalmDown();
                    n.Suspicion = 20;
                    ctx.Toast?.Invoke($"{n.NpcName} saw everything… and wants in. You gained an accomplice.", ToastKind.Success);
                }
                else if (n.Spec.Reports)
                {
                    if (ctx.Guard is { } g2 && g2.Awake)
                    {
                        n.StartReport(g2.Pos);
                        ctx.Toast?.Invoke($"{n.NpcName} is RUNNING to security! Intercept or improvise!", ToastKind.Warn);
                        ctx.AlarmSfx?.Invoke();
                    }
                    else
                    {
                        ctx.Toast?.Invoke($"{n.NpcName} ran to tell security… but security is currently a floor lamp.", ToastKind.Chaos);
                        n.CalmDown();
                    }
                }
            }

            maxSus = System.MathF.Max(maxSus, n.Suspicion);
            PushVisual(n, dt);
        }

        Outputs.MaxSus = maxSus;
        Outputs.Watched = watched;
    }

    private static void PushVisual(NpcBrain n, double dt)
    {
        bool showBar = n.Awake && n.State != NpcState.Seated && n.Suspicion > 1f;
        n.Body.SetSuspicion(showBar ? n.Suspicion / 100f : 0f);
        n.Body.SetMoving(n.Moving, run: n.State is NpcState.Report or NpcState.Hunt);
    }

    /// <summary>Port of game.ts updateGuard().</summary>
    public static void GuardTick(NpcBrain g, AiContext ctx, double dt)
    {
        if (!g.Awake) return;

        // even Briggs cannot resist the photocopied face
        if (g.DistractTimer > 0 && g.State != NpcState.Hunt)
        {
            g.DistractTimer -= (float)dt;
            if (g.DistractTimer <= 0) g.Body.ClearEmote();
            return;
        }

        if (g.State == NpcState.Hunt)
        {
            bool sees = ctx.CanSee?.Invoke(g, ctx.PlayerPos, Bal.GuardHuntVisionMul) ?? false;
            if (sees)
            {
                g.LastSeenPlayer = ctx.PlayerPos;
                g.LostSightTimer = 0;
                if (ctx.PlayerCarrying)
                    ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, 8); // personally witnessing carrying refreshes the hunt
            }
            else
            {
                g.LostSightTimer += (float)dt;
            }

            bool arrived = g.StepToward(ctx.WorldRef, g.LastSeenPlayer, dt);
            float dist = g.Pos.DistanceTo(ctx.PlayerPos);
            if (dist < Bal.GuardCatchDist)
            {
                ctx.OnPlayerCaught?.Invoke();
                return;
            }
            if ((arrived && g.LostSightTimer > Bal.GuardGiveUpArrived) ||
                g.LostSightTimer > Bal.GuardGiveUpLostSight ||
                ctx.AlertTimer <= 0)
            {
                g.State = NpcState.Routine;
                g.MoveTarget = null;
                g.PauseTimer = 1f;
                ctx.AlertTimer = 0;
                ctx.Toast?.Invoke("Briggs lost you. He pretends he meant to walk here all along.", ToastKind.Info);
            }
            return;
        }

        // patrol
        if (ctx.NoiseFresh)
        {
            // recent noise: Briggs checks it out personally
            if (g.StepToward(ctx.WorldRef, ctx.NoisePos, dt, Bal.GuardPatrolSpeed * 1.3f))
                g.Moving = false;
        }
        else if (g.PauseTimer > 0)
        {
            g.PauseTimer -= (float)dt;
            g.Moving = false;
        }
        else if (!g.MoveTarget.HasValue || g.StepToward(ctx.WorldRef, g.MoveTarget.Value, dt, Bal.GuardPatrolSpeed))
        {
            var posts = WorldData.GuardPosts;
            if (posts.Length > 0)
                g.MoveTarget = posts[(int)(GD.RandRange(0, posts.Length - 1))];
            g.PauseTimer = 2f + (float)GD.RandRange(0.0, 3.0);
        }

        // guard personally witnessing blatant crime
        if ((ctx.CanSee?.Invoke(g, ctx.PlayerPos, 1f) ?? false) && ctx.PlayerCarrying)
        {
            ctx.Toast?.Invoke("Briggs saw you carrying a \"mannequin\". He is not buying it.", ToastKind.Warn);
            g.State = NpcState.Hunt;
            g.LastSeenPlayer = ctx.PlayerPos;
            ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, Bal.GuardAlertSeesCarry);
            ctx.AlarmSfx?.Invoke();
            return;
        }

        // guard stumbling onto a body -> immediate hunt
        foreach (var b in ctx.Npcs)
        {
            if (b == g || b.State != NpcState.Out || !b.Body.Visible) continue;
            if (ctx.CanSee?.Invoke(g, b.Pos, 1f) ?? false)
            {
                ctx.Toast?.Invoke($"Briggs found {b.NpcName}'s body. He has decided it was you.", ToastKind.Warn);
                g.State = NpcState.Hunt;
                g.LastSeenPlayer = ctx.PlayerPos;
                ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, Bal.GuardAlertFindsBody);
                ctx.AlarmSfx?.Invoke();
                return;
            }
        }
        foreach (var s in ctx.Blood.All)
        {
            if (ctx.CanSee?.Invoke(g, s.Pos, 0.9f) ?? false)
            {
                ctx.Toast?.Invoke("Briggs found a bloodstain and is connecting dots that don't exist.", ToastKind.Warn);
                g.State = NpcState.Hunt;
                g.LastSeenPlayer = ctx.PlayerPos;
                ctx.AlertTimer = System.Math.Max(ctx.AlertTimer, Bal.GuardAlertFindsBlood);
                ctx.AlarmSfx?.Invoke();
                return;
            }
        }
    }

    // guard posts come from WorldData via the world's waypoint-agnostic table;
    // exposed here to avoid a GameMode dependency inside the director.
    public static Vector3[] GuardPosts => WorldData.GuardPosts;

    private static Vector3[] GuardPostsOf(AiContext ctx) => WorldData.GuardPosts;
}




