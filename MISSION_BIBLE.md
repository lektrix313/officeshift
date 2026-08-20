# OFFICE SHIFT — VOLUME III
## The Mission Authoring Toolkit & Contract Grammar

> Companion to GAME_BIBLE.md (Vol I) and MECHANICS_DEEP_DIVE.md (Vol II).
> Vol II closed with a confession: *"the sim writes better missions than we do."*
> This volume is us doing something about it — a formal grammar for contracts,
> a generator, a solvability prover, and the AI Director that conducts the chaos.
>
> **Core claim:** a mission is not a script. A mission is a *thesis* —
> "here is a thing you want, in a place that resists you, on a day that has opinions."
> The player's job is to write the proof.

---

# SECTION 1 — THE CONTRACT GRAMMAR

Every mission, authored or generated, is a tuple:

```
CONTRACT = [VERB] + [ASSET] + [LOCATION] + [TIME WINDOW]
         + [COMPLICATION × 0..3] + [TWIST × 0..1] + [EXFIL] + [STAKES]
```

Each slot is drawn from a lexicon with metadata. Slots are not flavor text — every choice **reconfigures the simulation**: guards get scheduled, NPCs get new memories, doors change hands, events land on the office calendar.

## 1.1 VERB Lexicon (the mission's core action)

| Verb | Object | Systems engaged |
|---|---|---|
| **STEAL** | Physical asset | Physics, container rules, exfil weight |
| **COPY** | Document/screen/whiteboard | Terminal access, camera exposure, printer logs (the log!) |
| **PLANT** | Evidence/device/file | Reverse-forensics, blame economy priming |
| **SWAP** | Deck/prototype/drug/test-results | Sleight-of-hand, discovery timers ("when do they notice?") |
| **DELETE** | Files/logs/tapes/case | Tech access, redundancy layers (cloud backups — there's always a backup on Floor 6) |
| **EXTRACT** | A *person* (defector/witness/rival) | Social escort, disguise pair, the slowest walk of your life |
| **RECRUIT** | Turn an NPC | Secrets market, relationship thresholds, multi-shift arc |
| **SABOTAGE** | Machine/demo/launch/career | Chain reactions, blame targeting, scheduling |
| **FRAME** | Specific NPC | The full Blame Economy pipeline (Vol II §3.5) |
| **PROTECT** | NPC or asset for the whole shift | Escort logic, threat simulation, betrayal-bait |
| **PHOTOGRAPH** | Whiteboard/prototype/document | Camera item, "incidental in background of selfie" route |
| **LIVESTEAL** | Data while a meeting is *in progress* | The Conference Call inverted — you're in the room where it happens |
| **GHOST** | Be assigned nothing and still profit | The meta-contract: appear in no logs, no memories, no footage |

## 1.2 ASSET Lexicon (what's at stake)

Assets carry **properties** that shape the mission:

```
ASSET = { size: pocket|hand|trolley|furniture,
          fragility, alarm_tagged?, biometric_locked?,
          social_weight (who cares), location_knowledge (who knows) }
```

Examples: prototype battery (hand, alarm-tagged, lab lead cares), Q3 financials (pocket, CFO cares, Legal *really* cares), the whiteboard in Exec (furniture-class: you are stealing a whiteboard; good luck; there is a photograph verb for cowards), Keith's suspicion spreadsheet (social_weight: terrifying), the office dog (yes there's a dog now, you did this, morale +40 while dognapped).

## 1.3 LOCATION Lexicon (where it resists you)

Each location is a **resistance profile**, not a backdrop:

| Location | Resistance |
|---|---|
| Cleanroom (9) | Hazmat protocol, contamination events, airlock timing, *no pigeons allowed* (the pigeon disagrees) |
| Executive Wing (12) | Biometrics, assistants, the orchid mic, social stealth only — violence here is campaign-warping |
| Server Core (6) | Deafening hum (acoustic cover), heat, IT goblins (perception 18m, never blink) |
| Open Floor (2–4) | No walls that matter, a hundred eyes, gossip physics |
| Tape Room (1) | Guard-adjacent, sign-in sheet (irony), the only room that watches the watchers |
| HR Suite (8) | Cameras illegal in review rooms (blind spots with *paperwork*), the fainting couch |
| Break Worlds (5) | Crowd cover, noise, the ball pit (item concealment tier-S, dignity cost: total) |
| Elevators | Small rooms with schedules, camera, and *no exits* — a mission space, not transport |

## 1.4 COMPLICATION Lexicon (the day has opinions)

Complications are scheduled sim-events layered onto the shift. 120+ at ship; samples by category:

- **Calendar:** surprise audit · fire drill (not yours) · all-hands · birthday (target's best friend — floor empties *toward* the target) · performance-review day · board visit · bring-your-child day (tiny chaotic witnesses) · pigeon
- **Facilities:** elevator 3 out (queue comedy) · wifi down (cameras down, IT swarming) · floor waxing (friction modifier + legally binding wet-floor signs) · mould quarantine (reroute everyone) · broken AC (everyone sweating = composure −, doors propped +)
- **Social:** layoff rumor (paranoia ×2, loyalty ÷2) · two departments feuding · the CEO is "walking the floor" · someone's spouse started today (see Vol II §3.3) · Keith got promoted (OH NO — he outranks his own paranoia now)
- **Counter-intelligence:** another spy is active this shift (competing contract — you can sabotage, assist, or trade) · security drilled yesterday (panic countdowns −30%) · new hire orientation (15 strangers with no memories of you — bliss) · The Sniffer is in the building
- **Self-inflicted (your past shifts):** yesterday's frame job goes to hearing today · the camera you forgot has a tape · Dave is back from wellness leave and he has *questions*

## 1.5 TWIST Lexicon (mid-mission rewrites)

A twist detonates mid-shift and *changes the contract legally*:

- The asset moved (someone signed it out — find who: the signature is real, this is now a detective mission)
- The asset is a decoy (the real one is with the CEO — vertical escalation mid-shift)
- Your handler goes dark (objectives freeze; a rival handler calls with a *counter-offer*)
- The target wants to defect (STEAL becomes EXTRACT — a person is heavier than a battery)
- The office finds the body *from three shifts ago* (past crimes invoice you mid-mission)
- Your co-spy's secret objective activates (they've been *so* helpful today, haven't they)
- The building locks down for a drill — and doesn't unlock, because it's not a drill
- Keith figured it out. All of it. He wants to *join*. (Recruitment or disposal, choose fast.)

## 1.6 EXFIL & STAKES

- **Exfil variants:** lobby walk-out (the casual flex) · mail/courier (async — leaves before you do, be at the trolley by 16:30) · dead-drop (roof/bathroom tank/loading bay) · person-to-person handoff (a contact visits as "the pizza guy") · *don't* — store it in your ceiling cache across shifts (multi-day smuggling arcs).
- **Stakes tiers:** Routine (paycheck) · Priority (promotion points ×2) · Critical (handler's reputation — fail and your spy agency sends a *supervisor*) · Personal (the story contracts — these can end relationships, careers, and Keith).

---

# SECTION 2 — THE GENERATOR

## 2.1 Pipeline

```
seed → slot draft → coherence filter → solvability proof →
difficulty score → briefing text (LLM) → calendar injection → ship
```

1. **Slot draft** — weighted draw from each lexicon; weights respond to campaign state (your rank gates locations; your notoriety weights counter-intel complications; your *history* weights self-inflicted ones).
2. **Coherence filter** — a constraint solver rejects nonsense: FRAME requires the target to be present and blame-able; CLEANROOM + fire-drill complication only if sprinklers are authored there; romance twists require at least one living romantic lead, Keith.
3. **Solvability proof** (the AA differentiator) — the generator runs a *planner*, not a playwright:

```
SOLVABLE(c) ⇔ ∃ at least one authored path per required category
              ∧ no hard contradiction (asset inaccessible ∧ no verb grants access)
              ∧ time-feasible (path_duration ≤ shift_length − 15%)
```

The planner searches with the same verb table the player uses. If it can't prove a path, the contract is rerolled or *repaired* (add a ladder to a closet, relocate a guard, schedule a gap). **No dead missions. Ever.**
4. **Difficulty score** — computed from resistance profile + alarm level + complication synergy (two complications that share a schedule slot are *harder than their sum* — the generator knows this and prices it).
5. **Briefing text** — LLM-authored from the tuple + your handler's personality + campaign state, then lint-checked for leaks (never spoils the twist).

## 2.2 The Three Contract Boards

- **The Handler's Board** — main campaign; authored anchors at act boundaries, generated filler between; difficulty curves to your promotion pace.
- **The Bulletin Board** — procedurally generated side contracts posted to the office intranet *by NPCs* ("Someone steal back my yogurt — reward: a favor"). Playing the office's petty crimes builds favor currency and camouflage: you're just another weirdo stealing things.
- **The Rival Board** — contracts your rival agency is running *against* OmniCore this week. Ignore them (they'll make noise you can use), race them (same asset, live competition), or sabotage them (frame the rival spy for *your* crimes — the galaxy-brain blame loop).

## 2.3 Seed Culture

Every generated contract has a human-readable seed code (`OMNI-9X4A-FIREDRILL-DECOY`). Streamers share seeds; challenge boards rank runs; "seed of the week" is a live-ops freebie. The generator is deterministic per seed — same contract, same office weather, your crimes still your own.

---

# SECTION 3 — THE DIRECTOR (AI Dungeon Master in a Lanyard)

A pacing agent watches the shift and nudges the simulation within authored bounds. It does not cheat; it *schedules*.

## 3.1 Pacing Model

Tracks a tension curve per shift with target beats: **calm opening → first complication (09:45) → midpoint spike (lunch) → deep-work window (14:00, NPCs sleepy) → endgame squeeze (16:00+, everything converges)**.

- Tension too low → the Director releases a scheduled ambient event early, routes a Snoop's patrol past you, or has the Gossip start a conversation *right there*.
- Tension too high → a blessed interruption: the birthday cake arrives, the VP calls an impromptu all-hands *away* from your crime, Dave falls asleep in a doorway forming a soft wall.
- **Never visible:** the Director's interventions are always diegetically plausible. It has a budget per shift and it spends it like a producer, not a god.

## 3.2 Dramatic Irony Feed

The Director also writes the Story Feed (Vol II §9) and chooses the Spectator Cam subject: *the NPC nearest to discovering evidence* is almost always the correct broadcast. Tension is information asymmetry, and the audience gets both sides.

## 3.3 The Nemesis Thread

The Director maintains your **nemesis score** per NPC — a hidden heat map of who has the most evidence, motivation, and screen time against you. When a nemesis crosses threshold, the campaign *promotes them*: Keith becomes Head of Internal Compliance; the jilted ex transfers to Security; the framed rival's spouse gets a corner office and a vendetta budget. Nemeses get custom contracts *about you*. The endgame's hardest enemies are the ones you made.

---

# SECTION 4 — AUTHORED MISSIONS (Campaign Anchors)

Generated contracts fill the days; authored missions move the story. Anchor missions are hand-built **set-pieces that abuse the systems** — each teaches or twists one:

| Mission | Set-piece | System it spotlights |
|---|---|---|
| 1. Orientation | Steal a stapler. Seriously. The tutorial contract is *a stapler*. | Every core verb in a padded playpen |
| 4. The Hearing | Defend your first frame-job in HR court | Testimony, memory confidence, planted evidence |
| 7. Town Hall | Swap the CEO's deck live, from the AV booth, during the speech | LIVESTEAL, projector hijack, crowd cover |
| 9. Two Spies, One Cup | You and the rival spy are assigned the *same asset same shift* — meet-cute in the ceiling void | Rival systems, negotiation or violence at 60cm |
| 12. The Promotion Party | Your own promotion party. Everyone you wronged is invited. So is the evidence. | Social minefield speedrun; every choice from the last 5 shifts attends |
| 15. The Vault Floor | What OmniCore actually does | All keys, all clearance, all consequences |
| Finale. The Exit Interview | Every living NPC forms the panel; your career is the contract | The campaign's full state reads itself back to you |

**Rule for authored missions:** they may script *staging* (who's where at 09:00) but never *outcomes*. If the player frames the CEO's assistant during Mission 7's live deck swap, the mission adapts — the epilogue remembers.

---

# SECTION 5 — SCORING, STYLE & THE DEBRIEF

## 5.1 The Performance Review (parody-as-scoring)

Every shift ends with your review across five KPIs:

| KPI | Measures |
|---|---|
| **Delivery** | Objectives completed, on time |
| **Discretion** | Witnesses, tapes, case files opened, memories formed |
| **Synergy** | Blame deflections, favors banked, relationships improved |
| **Morale** | Office mood delta (yes, your crimes affect the vibe; yes, you're rated on it) |
| **Compliance** | How convincingly you did your *fake job* |

Style multipliers: **Ghost** (zero suspicion events) · **Berserker** (5+ bonks, still uncaught — frowned upon, spectacular) · **Cupid** (contract completed via romance) · **The Bureaucrat** (zero illegal actions; everything signed for) · **Chaos Certified** (3+ chain reactions, one building, no idea how you're employed).

## 5.2 Partial Success Is The Good Stuff

Failure is content, not a stop state:

- Asset lost but blame landed → *Pyrrhic delivery*: payment halved, promotion intact, new nemesis minted.
- Caught on tape but asset delivered → the agency "handles it" — you owe a **Marker** (a future contract you can't refuse).
- The whole shift burned → the agency declares it a *training exercise*, docked pay, and the office remembers everything next week. **The campaign continues.** Only three things end a campaign: the finale, exposure past Critical, or being fired from your fake job (dishonorable discharge; the achievement is called *"At Least The Spy Work Pays"* — it doesn't).

---

# SECTION 6 — WORKED EXAMPLES (Generator, End-to-End)

### Example A — seed `OMNI-2F7B-AUDIT-DECOY`
```
VERB:  SWAP        ASSET: Q3 financials deck (pocket, CFO cares)
LOC:   Executive Wing boardroom (biometric + assistant + orchid mic)
TIME:  Before the 15:00 board meeting
COMPL: surprise audit (Finance floor locked down) · floor waxing (friction, signs)
TWIST: the real deck is already with the CEO (decoy)
EXFIL: none — the swapped deck IS the delivery (it presents itself at 15:00)
```
Solvability proof finds 7 paths (clipboard route, assistant-seduction route, orchid-mic-blindness route via facilities requisition, …). Difficulty 8/10. Handler's briefing: *"The numbers need to say something else. Preferably before they say anything."*

### Example B — seed `OMNI-8841-PIGEON`
```
VERB:  PROTECT     ASSET: Dave (the Slob — social_weight: confusing)
LOC:   Whole floor (mobile asset, mostly horizontal)
TIME:  Full shift
COMPL: rival spy active (contract: FRAME Dave) · bring-your-child day
TWIST: Dave knows. Dave has known for weeks. Dave is *also* a spy. (Deep cover: horizontal)
EXFIL: Dave clocks out alive and un-framed
```
The generator flagged this as a comedy-critical seed and the Director budgets extra interventions. Difficulty: unknowable. It's Dave.

### Example C — the player's own ghost
```
VERB:  GHOST       (meta-contract: no assigned asset)
CONDITION: end the shift with your name in zero logs, zero footage,
           zero memories above confidence 50 — while Routine contracts
           for two *other* players (co-op) complete around you
```
Solo hardcore streamer mode. The Director is forbidden from helping. The Story Feed becomes the scoreboard.

---

# SECTION 7 — AUTHORING TOOLS (Ship With The Game)

- **Contract Composer** — the generator's UI: pick or randomize slots, watch the solvability proof run, playtest instantly. Community contracts upload with their seeds and their *proved path count* as a quality badge ("this contract has 9+ proven solutions").
- **Complication Calendar** — drag events onto a shift timeline and preview the NPC schedule ripples (watch the coffee queue reroute live).
- **Nemesis Tuner** — pick your campaign rival and write their trigger conditions; the Director does the rest.
- **The Replay Viewer** — every shift is fully recorded as sim-events (not video); re-watch from any NPC's perspective. Debugging tool, content machine, and the single best way to learn that Keith watched you for *forty full seconds* and did nothing because you brought him coffee that morning.

---

*Volume III complete. The grammar is closed, the generator is specified, and somewhere in the probability space there is a seed where Dave is the final boss. We will not be including it. It will find itself.*

*— End of the Office Shift bible (Vols I–III). The pigeon is real. The orchid is listening. Clock in.*
