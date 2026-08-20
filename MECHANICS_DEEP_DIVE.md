# OFFICE SHIFT — MECHANICS DEEP DIVE
## Volume II: The Office & NPC Systems Bible

> Companion to GAME_BIBLE.md. This is the systems-level specification:
> the NPC brain, the emotional engine, the job sim, the item catalog,
> physics chains, co-op mission planning, and streamer design.
> Rule of thumb for every system in this document: **it must generate stories.**

---

# SECTION 1 — THE NPC BRAIN

Every NPC runs a five-layer stack. Cheap layers run every frame; expensive layers tick on staggered budgets (2–10 Hz). 60–90 NPCs per floor, LOD'd: full brain on-screen, reduced brain off-screen, "schedule skeleton only" for other floors.

## 1.1 Layer 1 — Physiology (Needs)

Six needs, 0–100, drain at personality-scaled rates:

| Need | Drains | Satisfied by | When critical |
|---|---|---|---|
| **Caffeine** | Constantly, faster for Workaholics | Coffee, energy drinks, kombucha (barely) | Microsleeps at desk; slow perception; grumpy |
| **Hunger** | Steady; spikes at 12:30 | Lunch, snacks, stolen yogurt (crime) | Hangry: +30% irritability, will leave post for vending machine |
| **Bladder** | Steady, ×2 after coffee | Bathroom break (60–120s window) | The Leg Bounce; sprints to bathroom at 90+ |
| **Boredom** | In low-stimulation spots | Phone, gossip, watching drama (you) | Slacks off — perception drops, but wanders unpredictably |
| **Social Battery** | Drained by interaction, faster for introverts | Solitude, the roof, hiding in bathroom stalls | Snaps at people; seeks isolation (empty rooms — remember this) |
| **Stress** | Workload, alarms, witnessing | Venting to friends, vaping, nap pods | Meltdown threshold: spectacular, public, distracting |

**Design intent:** needs create the *schedule skeleton* — coffee runs at 9:15, lunch exodus at 12:30, the 3pm mass yawn. The player learns the office's circadian rhythm and crimes move to its beat. Spiking the coffee machine with decaf is a *mass-perception debuff* for the entire floor. Spiking it with laxatives is a mass bathroom event. Both are loadout items.

## 1.2 Layer 2 — Emotions (The Mood Vector)

Each NPC carries a live emotional state:

```
MOOD = { pleasure: -100..100,   // sad ↔ happy
         arousal:  0..100,      // calm ↔ agitated
         fear:     0..100,
         anger:    0..100,
         trust_in_player: -100..100 }   // see §1.4
```

- **Decay:** all emotions drift toward baseline (personality-set) at ~2/min. Drama fades. Grudges don't (those live in memory, §1.5).
- **Contagion:** emotions propagate through social clusters — one panicking NPC raises arousal +fear of everyone in 10m; a laughing break room raises pleasure floor-wide. *The photocopied face is a weapon of mass mood elevation.*
- **Meltdowns:** stress > 90 sustained → archetype-specific public breakdown: the Crier (bathroom, 20 min, door unlockable), the Rager (flips a desk, security responds — *you can cause this on purpose*), the Quitter (storms out — permanent population change), the Fainter (free body-shaped distraction).

### Emotion → Behavior Table

| State | Effect on their behavior |
|---|---|
| Happy + relaxed | Perception −20%, suspicion gain −30%, generous with favors, chats longer |
| Stressed | Perception +15% but attention narrow (misses the big picture), snaps, harder to stall |
| Angry at you | Reports you at 60 suspicion instead of 100; contradicts your testimony; may *sabotage* your desk |
| Afraid of you | Complies with instructions, won't report below 80, avoids being alone with you (bad for luring) |
| Afraid in general | Herds toward crowds, calls security sooner, locks doors behind them |
| Infatuated with you | Covers for you ("He's with me"), shares secrets, lowers guard — see §3.3 for the disaster side |
| Grieving | Stationary at victim's desk; immune to small talk; high perception near that desk |
| Suspicious (global) | Cross-references memories, watches you specifically, warns friends (gossip with *direction*) |

## 1.3 Layer 3 — Personality (Big Five, Lite)

Fixed at spawn, drives everything above:

- **Conscientiousness** — schedule rigidity, mess-tolerance (high-C notices moved objects: "Who touched my stapler?" — a detection stat you can weaponize or must respect)
- **Agreeableness** — favor cost, forgiveness speed, gaslight-resistance (low-A: never doubts themselves; high-A: doubts themselves *instantly*)
- **Extraversion** — social battery size, gossip radius, volume
- **Neuroticism** — stress gain, panic countdown length, meltdown resistance
- **Openness** — curiosity threshold: high-O investigates weirdness *gleefully* (dangerous), low-O ignores anything not in the employee handbook (wonderfully, exploitably incurious)

## 1.4 Layer 4 — Relationship Matrix

Every NPC pair (including you) has a directed edge:

```
REL(A→B) = { trust, affection, fear, respect, rivalry }   // -100..100 each
```

- Edges update from witnessed events, gossip retellings (with distortion), and direct interaction.
- **Triangles matter:** if A likes you and A likes B, A defends you to B. If A likes you and hates B, framing B is *easy*. Read the web before you pull threads — the Relationship Map is an unlockable intranet page (HR disguise or keylogger).
- **You have per-circle reputations:** Sales / IT / Facilities / Legal / Execs each track you separately. Facilities thinks you're "one of the good ones" if you mop without being asked. That's not a joke, that's a mechanic.

## 1.5 Layer 5 — Memory (Episodic, Distorting)

NPCs store events, not facts:

```
MEMORY = { what, where, when, who, confidence, emotional_charge, told_to[] }
```

- **Confidence decays** (fast for low-stress events, slow for trauma). Below threshold → "I might be misremembering."
- **Retelling distorts:** each hop through gossip loses detail and gains embellishment. A blood stain becomes "a whole… *puddle*, like, a LOT of blood, Susan said."
- **You can edit memories:** the *Gaslight* toolkit (§3.2) directly reduces confidence or rewrites the `what`. Forged memos create *false shared memories* ("Per my last email, you approved this.").
- **Security's case file** aggregates memories + physical evidence. A case dies when witness confidence drops below coherence — you can literally erode an investigation by messing with heads and timelines.

## 1.6 Behavior Arbitration (Utility AI)

Every decision tick, awake NPCs score candidate actions:

```
score(action) = need_urgency × duty_weight × personality_mod
              + emotion_mod + relationship_mod + curiosity_mod
              − risk_estimate(player_visible? alarm_level?)
```

Candidate pools: *duties* (desk work, meetings, rounds), *needs* (coffee, bathroom), *social* (chat, flirt, vent), *investigations* (that noise, that stain, that *smell*), *emergencies* (panic, report, flee, hide).

**The comedy guarantee:** ties are broken by personality quirks, so two NPCs facing the same stimulus diverge — Keith investigates the noise, Barry decides it's above his pay grade and goes for kombucha. Both reactions are useful. Both are funny.

---

# SECTION 2 — PERCEPTION & THE STIMULUS LATTICE

## 2.1 Senses

| Sense | Range | Notes |
|---|---|---|
| **Vision** | 8–16m cone (archetype), 270° peripheral blob at 2m (they *notice* movement) | Blocked by partitions/walls; crouch shrinks profile 35%; holding a folder adds +2s "processing delay" (folder = invisibility cloak of the middle class) |
| **Hearing** | 3 zones: clear (6m), muffled (through partition), ambient-drowned | Server room hum masks everything; printers are acoustic cover; the kombucha tap hiss is 2m of privacy |
| **Smell** (special) | The Sniffer's UV-lamp walk, plus blood smell for Facilities after 10 min | Late-campaign pressure valve against sloppy players |

## 2.2 The Stimulus Table (what NPCs notice, and what they do)

| Stimulus | Curiosity | Response chain |
|---|---|---|
| Blood stain | Medium-high | ❓ → inspect → "ketchup?" → confirm → 😱 countdown (4.5s) → report |
| Fresh footprints | Low (per print) | ❓ → *follow the trail* (Hansel mode) → finds you or the body |
| Body in open | Very high | ❓ → approach → nudge ("…hello?") → 😱 countdown (8s) → report |
| Body posed as furniture | Low, but scales with proximity + high-Conscientiousness | ❓ → stare → "is that lamp… breathing?" → escalate |
| Moved personal object | Only high-C owners | ❓ → minor grudge → memory note |
| You in wrong department | Low, rises with wrongness | ❓ → "Can I help you?" (Authority check to deflect) |
| You crouching | Medium in 8m | "Are you… crab-walking?" → slow suspicion |
| You too close too long | Medium | Personal-space complaint → suspicion; flirty NPCs read it differently (see §3.3) |
| Carried "mannequin" | High | "Is that a person?" → Composure contest → report |
| Alarm/announcement | Floor-wide | Archetype-dependent: muster point, keep working (Workaholic), loot the kitchen (Grifter) |
| Silence after a crash | High | Investigation squads form organically — nearest 2–3 NPCs |

---

# SECTION 3 — SOCIAL ENGINEERING (The Human Exploit Kit)

## 3.1 The Favor Economy

Befriending is a currency loop: **do favors → build trust → call in favors.**

- Favors you can give: cover their shift, fetch coffee order correctly (memory minigame), fix their printer (Tech check), listen to them vent (timed active-listening minigame), lie for them, share gossip they value.
- Favors you can call in: alibi ("we were together"), distraction-on-demand, badge loan, schedule intel ("Keith does his snoop-round at 2"), silence ("you saw nothing"), testimony.
- **Favor debt decays** and is public-ish: call in too many and the circle talks — "he's using her."

## 3.2 Gaslighting (Full Toolkit)

Targeted reality-erosion against one NPC. Each action costs setup and carries *their* Openness/Agreeableness as resistance:

1. **Object drift** — move their stuff 2 cm/day. Effect: stress +, self-confidence −. Three days in, they stop trusting their own eyes — *their witness confidence drops 30%*.
2. **Phantom approvals** — forged memo: "You approved the server-room requisition Tuesday." Low-A targets accept it and will *defend* the false memory under questioning.
3. **The Loop** — answer their question with the exact same sentence twice, deny the first time happened. Stress +++, relationship damage with onlookers if done in public (there is always an onlooker).
4. **Calendar ghosting** — schedule a meeting with them, don't show, deny it existed. Do it to a witness *before* their HR hearing testimony.
5. **Gaslight cascade** — push self-doubt past threshold → target voluntarily checks into "wellness leave" (removes them for a week, no forensics, no body, nobody blamed — the pacifist's assassination).

**Counterplay:** skeptical low-A NPCs start documenting you. The Snoop keeps a *spreadsheet*. If the spreadsheet reaches HR, you face the "pattern of behavior" hearing — the hardest social set-piece in the game.

## 3.3 Flirting & Romance (The Danger Zone)

- Attraction is its own hidden stat per NPC (orientation, type, your quirks — *Resting Friendly Face* finally pays off).
- **Flirt ladder:** banter → coffee "coincidence" → lunch date (insta-trust +30) → office romance.
- **Benefits:** lovers cover for you automatically, share credentials ("borrow my badge, gorgeous"), provide rock-solid alibis, and their panic countdowns for *your* crimes run 2× slower (denial).
- **Disasters (the actual design):** jealousy events if you flirt two people in the same circle; the jilted become *super-snoops* targeting you; HR's fraternization policy is a trap mission; breaking up is a boss fight; dating two coworkers simultaneously is the game's true hard mode and the streamer endgame.
- **Spouse twist:** late-campaign, a framed rival's spouse joins the company *specifically to find who destroyed their partner*. If that's you: they're immune to flirting, gaslighting, and bribes. Fear the motivated.

## 3.4 Blackmail & The Secrets Market

- Every NPC spawns with 1–3 secrets (embezzling stapler budgets, secretly living in the office, the thing in 2019, being a *rival* spy).
- Discovery vectors: keylogger, desk snooping, dumpster diving, bathroom-stall eavesdropping, the group chat, their best friend (favors).
- Secrets are spendable: silence, services, testimony, or sell *upward* (give Legal dirt on Finance for clearance goodwill).
- **Grifter NPCs** are fences: they trade secrets for favors and always know more than they say.

## 3.5 Mobbing (Weaponized HR)

Get the office to turn on a target: seed 3+ negative memories about them across different circles, let gossip cross-pollinate, then file the anonymous report. The floor does the rest — isolation, exclusion from meetings, and finally a *legitimate* firing. Zero forensics point to you; it takes 2–3 in-game days; it is monstrous; it is extremely funny; the achievement is called **"Thoughts and Prayers."**

---

# SECTION 4 — THE JOB SIM (Work As A Weapon)

Your cover job is a real simulated job. You can do it **well**, **badly**, or **criminally**.

## 4.1 Department Duties (minigame loops)

| Department | Duties | Doing it well | Doing it badly |
|---|---|---|---|
| **Sales** | Cold calls (rhythm/dialogue minigame), pipeline updates (spreadsheet fudge) | Commission bonuses, client intel, excuses to be anywhere ("client visit") | Quota miss → PIP → your manager *shadows you* (a personal NPC escort — nightmare) |
| **IT Support** | Ticket queue (dispatch puzzles), server rounds | Master device access, "maintenance" cover for anywhere with wires | Tickets escalate → real IT contractors arrive (unpredictable outsiders!) |
| **Facilities** | Rounds checklist, spill cleanup, supply restock | Master keys, universal invisibility (nobody sees the cleaner), the mop is *yours* | Complaints → inspection → your caches found |
| **HR** | Review sessions, policy emails, mediations | Summon anyone anywhere ("quick chat"), edit personnel files, pre-approve your own claims | Mishandled case → external audit (lockdown-grade scrutiny) |
| **Intern** | Fetch quests for everyone | Universal floor access, invisibility through insignificance | You get fired. From the fake job. At your real job. Embarrassing. |

## 4.2 Malicious Compliance (Work Actions As Espionage)

Every legitimate work action has a listed second use:

- **Schedule a meeting** → empties a room for 30–60 min; attendees are *located and accounted for*.
- **Book the conference room** → it's *yours*; lockable; has a phone for untraceable-ish calls.
- **Submit a maintenance ticket** → deploys Facilities (not you, the real ones) to a location of your choosing.
- **CC someone** → they must acknowledge; they're at their desk in 3 minutes reading nonsense.
- **Order catering** → crowd magnet with a delivery door propped open.
- **Schedule the fire drill** (HR only, high level) → *the entire building files out past the lobby.* The single biggest legal mass-distraction in the game.
- **Performance-review a witness** → they're in a room with you, alone, for 20 minutes. Professionally.
- **Approve your own requisition** → legitimate purchase orders for spy gear, delivered by mail, logged as office supplies. The drone with a tie on it arrives in 2 business days.

## 4.3 The Visibility/Competence Tradeoff

A live tension axis: **competence raises promotion speed but also raises *expectations*** — more duties, more meetings, more people looking for you. The perfect run balances "promotable nonentity." The sim tracks both axes and middle management has opinions.

---

# SECTION 5 — THE ITEM CATALOG (Physics-First)

Design rule: **every item lists mass, breakable?, flammable?, throwable?, and its verbs.** Nothing is set dressing. Sample of the 300+ item catalog:

## 5.1 Desk Ecosystem

| Item | Verbs | Crimes & comedy |
|---|---|---|
| Keyboard | bonk, throw, keylog (plant), wash (destroy evidence) | The classic takedown; leaves "QWERTY" imprint (forensics gag) |
| Monitor | bonk (heavy, 2H), short-circuit, stack | Blocks sightlines when stacked; falling monitor dominoes |
| Stapler | bonk (light), staple (documents, sleeves, *not* coworkers — HR was specific) | Stapling evidence to a patsy's desk |
| Mug | throw, spill, spike, bonk (fragile — one use) | 47 identical "World's Okayest Employee" mugs; switch them all for psychic damage |
| Desk phone | call (anyone, anywhere), tap, booby-trap (ringer volume max) | Page a witness to an empty room |
| Sticky notes | write, plant, forge, *cover camera lens* | The humblest and mightiest item |
| Stress ball | throw (silent), squeeze (composure +) | Squeaking it in a meeting: power move |
| Nameplate | swap | Swap two rivals' nameplates; watch the floor reorganize around chaos |
| Ergonomic chair | ride, wheel-body, charge-bonk, sabotage (height piston) | The piston prank launches victims 40cm. Physics-accurate. Dev-tested. |

## 5.2 Kitchen & Break Room

| Item | Verbs | Notes |
|---|---|---|
| Coffee machine | brew, spike (decaf/laxative/espresso concentrate), sabotage (spray) | Floor-wide buff/debuff dispenser; the highest-leverage item in the game |
| Microwave | cook, explode (metal fork included for your convenience), stink-bomb (fish) | Smell radius 12m; clears a break room legally |
| Vending machine | buy, shake (physics puzzle for free snacks), hide-behind, tip (DO NOT — oh you did) | Dead-drop variant contains spy shop |
| Kombucha tap | pour, spike, overflow | Ferments. Becomes flammable by Friday. The sim tracks this. |
| Birthday cake | deliver, hide-inside (small items, not bodies — we checked), smash | Mandatory floor-wide gathering event on rails |

## 5.3 IT & Electrical

| Item | Verbs | Notes |
|---|---|---|
| Server rack | hide (capacity 1, warm), short, pull-cables (floor-wide outage) | Cable-pull minigame: which cable? Wrong answer = someone's Netflix dies and they come investigate |
| Printer/copier | print, copy (anything on the glass), jam, log-check, firmware-hack, hide-under (cap 1) | Everything copied is LOGGED. Your face is in the log now. Deal with it |
| Projector | display, hijack | Put the *wrong deck* on the big screen during the all-hands |
| Ethernet wall ports | tap, loop | Camera loop requires physical access — sneaking with a purpose |
| UPS battery | lug (heavy), short (arc flash = light + sound event), roll downhill | Physics object of destiny |

## 5.4 Facilities & Logistics

Mop & bucket (clean, spill-as-obstacle, bucket-on-door), wet floor sign (place anywhere — legally binding force field; NPCs path around it; *you* may ignore it), mail trolley (body cap 1, mail cap ∞, rattle volume scales with body), ladder (climb, "borrow" forever — nobody ever stops a person carrying a ladder; universal access token, an actual real-world exploit, in the game), fire extinguisher (foam AOE blind + wall-slide propulsion in low friction — yes we know, we're keeping it), box of paper reams (cover-carry: hold it and walk anywhere, perception −40%), ladder again (it's that good).

## 5.5 Executive Tier

Golf putter (bonk+, putting minigame, execs respect a good line), whiskey decanter (spike++, gift, shatter-alarm), the aquarium (do not), panic room door (final-tier lock), the CEO's directional microphone hidden in the orchid (it's a mic. It's always been a mic).

---

# SECTION 6 — PHYSICS & SYSTEMIC CHAINS

## 6.1 Simulation Layers

1. **Rigid bodies** — every catalog item; mass-based verbs; two-hand rules in VR.
2. **Fluid decals** — blood/coffee/ink/mop-water: spread, dry (timer), track (footprints), clean (mop strokes × stain size).
3. **Fire** — ignition points, spread along flammable decals (Friday kombucha, paper stacks), suppression (sprinklers: everything wet, electronics dead, alarm global).
4. **Electricity** — short circuits arc (light+sound), outages zone-by-zone, cameras die with their floor's circuit.
5. **Air/smell** — microwave fish, smoke, vape clouds: radius, duration, NPC repulsion vector.
6. **Sound** — every physics impact emits radius/quality; NPC hearing resolves through the stimulus lattice.

## 6.2 Chain Reactions (the Immersive-Sim Promise)

Canonical test chains QA must be able to run:

- `stapler thrown → monitor crack → spark → paper stack ignites → sprinkler → server rack shorts → floor outage → cameras down → 4-minute crime window → you, holding a ladder, whistling`
- `laxative coffee → 09:40 mass bathroom event → cubicle farm empty → clean theft → but Dave (decaf, him specifically) never left → witness problem → Dave-shaped lamp`
- `fire drill scheduled (you) → building musters → lobby empty → walk the mail trolley out the FRONT → courier arrives during drill → exfil complete → cake at the muster point (you ordered catering too) → everyone's happy, nobody suspects, one guy is in the trolley`

## 6.3 Body Physics

- **Active ragdolls:** pose-matched, comedic timing (0.3s anticipation crouch before collapse — reads as *theatrical faint*).
- **Drag modes:** shoulder (fast, dripping, obvious), one-hand drag (slow, quiet, blood trail), trolley (fast, rattly, clean), chair-wheel (medium, looks INSANE to witnesses), Bernie's-walk (perk: full speed, looks like helping a drunk friend, requires Composure check per witness).
- **Posing:** sitting-at-desk (mouse in hand — withstands casual glance), nap-pod sleeper, meeting attendee (sunglasses perk), furniture (lamp, coat rack, *coat stand with hat*), break-room scarecrow. Each pose has a discovery threshold vs. distance and observer Conscientiousness.
- **Storage:** every container lists capacity, smell timer, discovery events (the printer gets opened when someone prints — *someone always prints*).

---

# SECTION 7 — DUAL MISSION PLANNING (Co-op)

## 7.1 The Planning Phase (Spy Van Corkboard)

Before each shift, both players get 90 seconds at the corkboard:

- **Blueprint view** of the target floor with live intel pins (camera coverage, NPC schedules you've learned, guard posts).
- **Role cards** — drag onto timeline slots: *Cover* (does the job sim visibly), *Operator* (does the crime), *Support* (distractions on-call), *Wildcard* (improvises; +10% style XP).
- **Sync points** — linked actions on a shared timeline: "09:40 — A pulls fire alarm → B enters server room (cameras die with alarm circuit… this shift only, patched tomorrow — USE IT)."
- **Contingency triggers** — if/then cards: *IF alarm ≥ Lockdown → A initiates Conference Call Trap on self (deliberate detention = B runs free).* Pre-agreed chaos.

## 7.2 Co-op Verb Pairs (signature moves)

- **The Meeting & The Ceiling** — A attends the mandatory meeting (dialogue minigame), B crawls above it.
- **Talker & Locksmith** — A stalls the VP in her doorway; B is under the desk with her laptop.
- **Bernie's Weekend, Co-op Edition** — each player holds one arm; body reads as "drunk friend" at full walk speed; both players must Composure-check per witness; failure is synchronized panic.
- **The Alibi Protocol** — be seen together on camera at 10:00 sharp while the crime (committed at 10:00 by *nobody*, obviously) happens via pre-rigged physics trap (tipped vending machine timer, candle + paper stack, the pigeon).
- **Good Cop / Bad Cop** on a witness — A threatens, B comforts; trust flows to B; B learns everything; A takes the reputation hit. Switchable next witness.

## 7.3 Betrayal Layer

Secret objectives sometimes conflict. The game never *forces* betrayal — it merely pays for it. The post-shift debrief shows both players' full stats; nothing says "divorce" like learning your partner got a bonus for Keith's firing while you were *protecting* Keith.

---

# SECTION 8 — MISSION MULTIPLICITY (100s of Ways, Proven)

Deep-dive example: **CONTRACT — "Steal the prototype battery from the R&D cleanroom (Floor 9), exfil before 17:30."**

Sample solution paths (each a real, supported route — combinatorics across them yield hundreds):

1. **The Ghost** — ceiling crawl from 8, vent drop, suction gloves, out the same way. Talk to nobody. Exist to no one.
2. **The Employee** — get legitimately promoted to Senior (3–4 shifts of actual good work), badge in, walk out with it logged as "testing sample." Boring. Perfect. Achievement: *"The Long Con."*
3. **The Janitor** — Facilities cover + mop + master keys. Nobody sees the cleaner. Nobody *ever* sees the cleaner.
4. **The Baker** — cake delivery to R&D (you ordered catering for their crunch week), crowd gathers, cleanroom door propped, battery in the cake box. The cake is real. Morale matters.
5. **The Pyro** — paper stack + candle + timer on floor 8 → evacuation → cleanroom empties → 4-minute window → you're wearing the facilities suit.
6. **The Lover** — seduce the lab lead (2-shift arc), "borrow my badge gorgeous," return it before lunch. She never knows. (She knows. Act III consequence.)
7. **The Puppetmaster** — frame a rival for *planning* to steal it; security sweeps R&D, removes the battery as evidence, evidence locker is on Floor 1, and Floor 1's tape room has a window you unlocked two shifts ago.
8. **The Bureaucrat** — requisition the battery for "cross-department synergy testing." Approved. Signed. Delivered to your desk. Mail it out in the 4pm collection.
9. **The Gaslighter** — convince the lab tech he already gave it to you. He remembers it now. Vividly. He'll testify to it.
10. **The Chaos Gremlin** — pigeon in the cleanroom. Contamination protocol. Full hazmat evacuation. Battery decontaminated and left unattended on a cart. A cart with wheels. Wheels are the best invention.
11. **The Co-op Special** — A triggers the Conference Call Trap *on the lab lead*, B does the lift in 90 seconds, A leaves the call with "let's take this offline."
12. **The Audit** — (HR route) schedule a compliance audit of R&D; auditors must inventory the prototype; you *are* the clipboard.

**The design guarantee:** every contract is authored against a verb checklist — physical / technical / social / bureaucratic / chemical / chaos — minimum 3 authored paths per category, plus whatever the sim invents that day.

---

# SECTION 9 — STREAMER & SPECTATOR DESIGN

Built to be *watched*:

- **Dramatic Irony Cam** (spectator/stream overlay): picture-in-picture showing the NPC currently closest to discovering evidence — the audience watches Keith follow the footprints while you obliviously photocopy your face. Tension as a broadcast product.
- **The Story Feed** — an auto-generated news ticker of sim events ("Susan has formed a search party for the missing yogurt • Keith's suspicion spreadsheet has 14 rows • Dave asleep again"). Stream overlays can dock it.
- **Clip magnets** — physics chains are tuned for shareability: ragdoll anticipation timing, the sprinkler moment, trolley escapes. The rewind-10s "Instant Replay" button exists *because* you will not believe what just happened.
- **Chat integration (Twitch):** viewers name coworkers (subscriber names join the office population — "sorry, @xX_noob got fired because of you, streamer"), vote on daily office events (fire drill vs. birthday vs. audit), and one viewer per shift may possess the pigeon.
- **Fails are content:** every fail state has a comedy beat and a recovery path — getting caught starts the Escape Custody sequence, not a game-over screen. The show must go on.

---

# SECTION 10 — BALANCING GUARDRAILS

| Exploit risk | Guardrail |
|---|---|
| Kill everyone → empty office → free crime | Population is a resource: replacements arrive (new personalities, higher paranoia), alarm ratchets permanently, the Sniffer is hired, and Act III requires *living* witnesses |
| Decaf-spike every day | Machine serviced (fixed) next day; repeat offense → the coffee fund meeting (yes it's a trap; yes Keith organized it) |
| Meeting-schedule spam | Rooms have booking conflicts; assistants get suspicious of phantom meetings; calendar forensics |
| Flirt everyone | Circle gossip compares notes; the jilted-snoop pipeline |
| Mop the entire floor's sins away | Mop water gets dirty (visual + smell), bucket refills at closets only, the mop sheds fibers (The Sniffer's favorite) |
| Save-scum social checks | Handler narrates disappointment; "canon" mode ironman for streamers |

---

*Volume II complete. Volume III (mission authoring toolkit & contract grammar) is intentionally unstarted — the sim writes better missions than we do, and that terrifies us too.*
