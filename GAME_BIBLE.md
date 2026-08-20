# OFFICE SHIFT: INTERNAL AFFAIRS
## The Game Bible — AA Edition

> **Version:** 2.0 (AA Redesign)
> **Genre:** Immersive-sim comedy stealth RPG — co-op first-person
> **Elevator pitch:** *Hitman*'s social stealth × *Dishonored*'s systemic sandbox × *The Office*'s excruciating comedy — as two industrial spies failing upward through the most dysfunctional tech company on Earth.
> **Platforms:** PC (flat + VR), Steam Deck; console stretch goal
> **Engine target:** Unity or Unreal (see §21) — the Three.js prototype is the proof-of-fun, not the ship vehicle
> **Session length:** 30–75 min shifts (missions), 15–25 hr campaign
> **Players:** 1–2 online co-op (drop-in/drop-out), full campaign playable solo

---

# PART I — THE VISION

## 1. Design Pillars

1. **The Job Is The Cover.** You don't sneak past the office — you *work* in it. Every stealth verb is dressed as a normal work activity: carrying a body is "moving the ergonomic assessment dummy," cracking a safe is "auditing," mopping blood is "facilities excellence."
2. **Comedy Through Systems, Not Scripts.** Slapstick emerges from physics + AI: a body sliding down a stairwell, a suspicious coworker following a blood drip trail like Hansel and Gretel, a fire alarm evacuation interrupting your heist. Writers set the table; the simulation tells the jokes.
3. **Fail Upward.** Incompetence is indistinguishable from middle management. Every crime you get away with gets someone else blamed and gets *you* promoted. The corporate ladder is the tech tree.
4. **Two Spies, One Alibi.** Co-op isn't "same game, twice the guns." It's a relationship: distraction and actor, talker and locksmith, the one in the meeting and the one in the ceiling.
5. **Every NPC Is A Person With A Lunch Order.** The office remembers. Coworkers have routines, friendships, grudges, and group chats. Kill Janet and her desk-buddy grieves, gets suspicious, and starts a memorial Slack channel that security monitors.

## 2. Tone & Content

- **Comedy register:** dry corporate absurdism + slapstick violence. *Severance* meets *Weekend at Bernie's*.
- **Violence:** cartoonish but consequential. Blood is a gameplay resource (evidence), not a gore showcase. Bodies are a logistics problem with legs.
- **Rating target:** PEGI 16 / M-lite. Blood yes, suffering no. NPCs snore with visible 💤 bubbles when knocked out.
- **The rule of funny:** if a system interaction makes playtesters laugh *and* groan, it ships.

---

# PART II — THE WORLD

## 3. OmniCore Industries

A 40-story tech megacorp HQ — equal parts Google campus, WeWork graveyard, and Bond villain real estate. The campaign is a vertical climb: **your clearance level literally unlocks floors.**

| Floors | Zone | Flavour | Unlocks at |
|---|---|---|---|
| B2 | Parking & Loading Bay | Body logistics hub, courier vans, getaway routes | Intern |
| B1 | Mailroom & Archives | Document forgery, mail trolleys, the shredder room | Intern |
| 1 | Lobby, Security & Tape Room | Badge gates, X-ray, camera DVRs, front-desk social stealth | Intern |
| 2–4 | Cubicle Farms (Sales, Support, Marketing) | Dense social stealth, gossip hubs, printer ecosystem | Intern |
| 5 | Break Worlds | Ball pit, nap pods, kombucha bar, rooftop garden, gym | Junior |
| 6 | IT & Server Core | Terminal hacking, server racks, cable crawls, the IT goblins who see everything | Junior |
| 7 | Legal & Compliance | Where framed coworkers get processed. Extremely dangerous. | Associate |
| 8 | HR & Training | Mandatory seminars (stealth hell), performance review rooms | Associate |
| 9–10 | R&D Labs | Prototype theft, cleanrooms, laser grids (absurdly, comedically) | Senior |
| 11 | Finance | Forgery, wire transfers, the petty cash vault | Senior |
| 12 | Executive Wing | Mahogany, panic rooms, private chef, the CEO's suspiciously evil aquarium | Director+ |
| 13 (secret) | The Vault Floor | What OmniCore actually does. Endgame. | VP+ |
| Roof | Helipad & Antenna Farm | Exfiltration, sabotage, final missions | VP+ |

**The building is alive:** elevators break, fire drills happen, the coffee machine on 4 is sentient-adjacent, facilities keeps sealing off floors for "mould" (that's you, you're the mould).

## 4. The Shift Structure (Mission Format)

Each in-game **day** is one mission ("shift"), structured in phases:

1. **Briefing (menu/spy van)** — pick contract objectives, loadout, cover identity details.
2. **Clock-in (08:30)** — enter through the lobby like everyone else. Badge, small talk, metal detector.
3. **The Working Day (09:00–17:00, compressed to ~45 min)** — freeform sandbox. Objectives live inside a *simulated workday*: meetings at fixed times, lunch rush, the 3pm slump, fire drills.
4. **Clock-out / Exfil** — leave with the goods before 17:30 or the day "ends badly" (locked down, overtime interrogation).
5. **Debrief** — performance review parody: KPIs, blame assignments, promotion points, paycheck, unlocks.

**Dynamic events per shift:** fire drill, surprise audit, birthday party (everyone gathers — perfect cover), wifi outage (cameras down, IT deployed), executive walkthrough, pigeon in the server room, someone *else's* spy operation happening concurrently.

---

# PART III — RPG SYSTEMS

## 5. Your Cover Identity (Character Creation)

You don't build a face — you build a *lie*:

- **Name & backstory** — procedurally suggested, fully editable. The game will make NPCs reference it.
- **Department** (starting floor + disguise defaults + skill affinities):
  - *Sales* — +Deception, meetings tolerance, nobody knows what you do (perfect cover)
  - *IT Support* — +Hacking, badge-access to server rooms, everyone avoids you (also perfect cover)
  - *Facilities* — +Strength, master key access, mopping is invisible to witnesses
  - *HR* — +Authority, can summon anyone to a "quick chat" (isolation as a weapon)
  - *Intern* — +Invisibility (nobody looks at interns), -everything else, comedy difficulty
- **Quirks** (pick 2): *Sweats Profusely*, *Photographic Memory*, *Resting Friendly Face*, *Chronic Loud Typing*, *Unreasonably Good At Small Talk*, *Irritable Bowel* (timed bathroom breaks = scheduled privacy windows).

## 6. Core Stats (0–10, leveled via use — "learn by doing")

| Stat | Governs |
|---|---|
| **Brawn** | Carry speed/weight, takedown force, throwing distance (bodies, printers) |
| **Finesse** | Lockpicking, pickpocketing, quiet movement, sleight-of-hand swaps |
| **Deception** | Dialogue success, lie quality, disguise integrity, conference-call survival |
| **Tech** | Hacking speed/depth, camera loops, elevator override, printer firmware (yes) |
| **Authority** | NPCs obey your instructions, send people away, "I'll take it from here" |
| **Composure** | Suspicion decay on you, heartbeat minigame stability, deadpan bonus |

## 7. Skill Tree — "The Org Chart"

Five branches, ~60 perks total. Perks are written as corporate achievements:

- **The Floor** (stealth/movement): *Ceiling Tile Parkour*, *Silent Shoes Policy*, *Personal Space Invader* (longer close-range tolerance), *Trolley Mastery* (push bodies 50% faster)…
- **The Toolbox** (tech/physics): *Advanced Lamp-Craft* (posed bodies read as furniture at 2× range), *Printer Whisperer*, *Cameras? What Cameras* (loop 3 feeds)…
- **The Mouth** (social): *Buzzword Fluency* (auto-win one jargon check per shift), *Deadpan Lying*, *Weaponised Small Talk* (stall NPC indefinitely), *Funeral Voice* (deliver bad news convincingly)…
- **The Muscle** (takedowns/logistics): *Keyboard Warrior* (one-hit bonks), *Weekend At Bernie's* (walk a body like a drunk friend), *Two-Man Lift Solo* (carry bodies at full speed)…
- **The Climb** (meta/career): *Fail Upward* (double promotion points when someone else is blamed), *Teflon Performance Review*, *Inner Source* (start each shift with one NPC's secrets)…

## 8. Promotion & Clearance (Campaign Progression)

- **Promotion Points (PP)** earned from: contract objectives, *successful blame deflection*, performance-review scores, optional objectives ("synergy bonuses").
- **Ranks:** Intern → Junior Associate → Associate → Senior Associate → Team Lead → Manager → Senior Manager → Director → VP → SVP → Executive. Each unlocks floors, meetings, budgets, and fewer questions asked.
- **Clearance cards** are physical objects — steal, clone, forge, or earn them. A Director's card works even if you're not a Director. For 4 minutes, until the system flags it.
- **Office politics layer:** every promotion displaces a real NPC rival who *remembers*. Demoted rivals become recurring antagonists (or allies, if you frame someone they hate).

## 9. The Blame Economy (Signature System)

Every incident (missing person, blood stain, stolen file, broken printer) opens an **HR Case** with a suspect list. You can:

- **Plant evidence** on a coworker (their badge near the scene, their prints on the keyboard, files in their desk).
- **Testify** in HR hearings (dialogue minigame; Authority + Deception checks).
- **Ghost** — do nothing and let the case go cold (suspicion diffuses randomly — risky).
- **Take the fall gracefully** (quirk-dependent; sometimes cheaper than exposure).

Closed cases permanently remove framed NPCs (escorted out, box of desk plants, slow clap optional). The office population is a *resource you sculpt*: remove the snoops, keep the slobs, cultivate the gossips as unwitting misinformation broadcasters.

---

# PART IV — CORE MECHANICS

## 10. The Moment-to-Moment Loop

**Look normal → create opportunity → commit crime → manage evidence → redirect blame → get promoted.**

Every 60 seconds of play should offer a choice between at least three of:
- A social approach (talk, stall, befriend, blackmail)
- A physical approach (crawl, climb, throw, drag, stack)
- A technical approach (hack, loop, forge, overload)
- A chemical approach (spike the coffee, laxatives in the kombucha, "decaf" swap)
- A bureaucratic approach (schedule a meeting over the crime scene, requisition the room, file a ticket that deploys IT elsewhere)

## 11. Full Physics & Interaction Model

**Everything that looks grabbable is grabbable.** The physics layer is systemic, not scripted:

- **Prop verbs:** grab, carry, drag, throw, stack, wedge (doorstop!), jam (photocopier, elevator), spill, ignite, short-circuit, flush, shred, photocopy, microwave, laminate.
- **Body verbs:** carry (shoulder), drag (one hand, leaves blood trail), wheel (trolley/chair — fast but rattly), pose (sitting at desk, napping on beanbag, *as furniture*), hide (containers with capacity and smell timers), launch (mail chute, stairwell — extremely loud, extremely funny).
- **Fluid sim (lite):** blood, coffee, kombucha, printer ink, and mop water are decal/puddle systems with *spreading* and *tracking* — step in blood and you leave footprints. NPCs follow footprints. The mop is a weapon of mass erasure.
- **Chain reactions:** fire → sprinkler → water → shorted servers → outage → camera downtime → opportunity. Or: fire → evacuation → the server room is *empty* for 4 minutes.
- **Structural:** ceiling tiles (crawlspace layer), vent covers, elevator ceilings, windows (they open; gravity exists; HR has questions).

## 12. Stealth & The Suspicion Model

Per-NPC, multi-axis awareness — no binary "alerted":

1. **Suspicion (0–100)** — "I think *you specifically* are up to something." Decays slowly. Feeds reports.
2. **Curiosity** — triggered by stimuli: blood, bodies, odd sounds, open doors that should be shut, you standing too close, you crab-walking. Curious NPCs *investigate* (walk over, ❓), then escalate.
3. **Panic countdown** — on confirming a body/blood/crime: visible ⏱️ (scaling with their courage: Snoop 8s, Slob 20s, Grifter never — he wants in). Interruptible by: hiding the evidence, cleaning it, bribing, blackmailing, or a swift keyboard.
4. **Alarm (global)** — security states: *Normal → Heightened → Lockdown → Sweeps*. Each shift's alarm state persists and ratchets; the building *learns* across the campaign (more cameras, more guards, pat-down Tuesdays).

**NPC archetypes** (each a full behavioral package):
Snoop (patrols, remembers faces), Gossip (vector of both suspicion and *your planted rumors*), Slob (asleep, stealable life), Grifter (blackmailable → accomplice), Workaholic (always at desk — a turret with a lanyard), Vaper (disappears on breaks — exploitable schedule), Facilities (goes anywhere, unwitnessed — the best disguise), Security (professional, checkpoints, responds to tiers), Exec (biometric keys with legs).

**Social stealth:** reading badge colors, matching walking speeds, holding any folder, joining conversations physically, sitting in on meetings, laughing at jokes a half-second late (composure check).

## 13. Evidence & Forensics

The building investigates *properly* (this is the AA flex):

- **Cameras** — wall/dome cams with real vision cones; recordings go to the Tape Room (Floor 1). Delete tapes, loop feeds, wear a hood, or just never be seen. Getting caught on tape starts a *taped-evidence* HR case that's very hard to beat.
- **Forensics lite** — blood type matches (mopped-in-time = cold case), fingerprints on weapons (wipe the keyboard!), badge-swipe logs (don't badge into rooms you "shouldn't" be in), printer logs (everything you copy is *logged*, including your face, which is how the photocopy gag becomes a real plot thread).
- **Witnesses** — statements cross-checked; two contradictory testimonies = case collapses (deliberately feed different stories to two gossips!).
- **The Sniffer** — late-campaign hire: a bloodhound-adjacent facilities guy with a UV lamp. Fear him.

## 14. Combat & Takedowns

Violence is a *last resort with paperwork*:

- **Bonk tier:** keyboard, mug, stapler, fire extinguisher (AOE foam = area blind), monitor, office chair (charge attack), the ceremonial "Employee of the Month" plaque.
- **Non-lethal defaults:** knockout → body problem. Lethal options exist (shredder, window, server rack electrical) but escalate forensics hard and unlock darker comedy writing. Fully non-lethal runs are supported and rewarded (*Conscientious Objector* perk line).
- **Struggle system:** spotted mid-takedown → grapple minigame → witness can break free and run (chase comedy).
- **Security:** arrest > kill. Getting tased starts the *Escape Custody* sequence (interrogation room minigame, vents, the classic).

## 15. Gadgets & Loadout (Spy Shop)

Between shifts, spend your *actual salary* (you're double-paid: OmniCore wages + spy retainer) at the dead-drop vending machine:

- Badge cloner · RF jammer · Camera loop box · Sticky-note keylogger · Laxative sachets · Decaf swap kit · Dye-pack (framing) · Fake blood (reverse-framing: fake a crime to clear a real one) · Remote whoopee cushion (distraction) · Drone with a tie on it · Extendable grabber arm · Suction cup suit (roof missions) · The Briefcase (smuggle anything scanner-proof)

---

# PART V — THE SOCIAL SIMULATION

## 16. AI NPC Chat & Voice (The Headline Feature)

Every named NPC is a **persistent character with an LLM-driven dialogue system** (local small model for latency-critical barks; cloud LLM for full conversations, with a strict character sheet + memory injection):

- **Full conversations:** walk up to anyone, talk (typed or voice-to-text). They know: their job, their schedule, office gossip, what they've *seen you do*, their opinion of you, and their secrets. They lie, deflect, flirt, complain about Keith.
- **Barks & voice:** all NPCs fully voice-acted via TTS with per-character voice profiles (pipeline: LLM line → TTS → lipsync-lite). Ambient conversations between NPCs are generated from their relationship graph and recent events — the office *talks about what happened yesterday*.
- **Memory:** episodic memory per NPC ("Tuesday: saw new guy crab-walking near server room, carrying heavy bag, smelled of toner"). Memories decay, distort through gossip, and can be *edited* (gaslight perk line, forged memos).
- **The Group Chat:** a diegetic Slack parody you can (illegally) read — NPCs coordinate, gossip, organize searches, post memorial tributes. Planting your laptop with a keylogger = reading the company's mind.

## 17. Dialogue Set-Pieces (Minigame Scenes)

LLM conversation + structured stakes on top:

- **The Conference Call Trap** — you're dragged into a call you're not cleared for. Mute-button fencing: unmute only when you know the answer, deploy buzzword combos, blame audio quality ("you're breaking up—" *mimes static*), while your co-spy physically extracts you (fire alarm, "pizza guy", fake page from reception).
- **The Performance Review** — quarterly; your KPIs are real gameplay stats. Talk your way from "concerning patterns" to "promotable asset."
- **The HR Hearing** — defend yourself or prosecute a framed rival. Ace-Attorney-in-khakis.
- **The Interrogation** — flipped script: *you* interrogate a witness to learn what they saw before deciding their fate.
- **The Exit Interview** — for NPCs you've gotten fired. Pure comedy. Optional. You monster.
- **Watercooler Poker** — high-stakes gossip trading: spend secrets to learn secrets.

## 18. Relationships & Office Politics Sim

- Relationship graph: friends, rivals, couples (break them up for chaos bonuses), mentor pairs, the weird alliance between IT and Facilities.
- Your **reputation** per social circle, not globally: Sales loves you, Legal is watching, IT tolerates you (you bring them energy drinks).
- **Events the sim throws:** birthday parties, layoffs rumors, unionization whispers, the CEO's town halls, inter-departmental wars you can stoke and profit from.

---

# PART VI — CO-OP DESIGN

## 19. Two-Player Espionage

- **Drop-in/drop-out**; solo gets an AI handler on the radio instead of a partner.
- **Designed asymmetries:**
  - *The Meeting & The Ceiling* — one attends the mandatory all-hands (dialogue minigame), one has the floor empty.
  - *Talker & Locksmith* — the talker stalls the VP in her office doorway while the other is *under her desk*.
  - *Two-person lifts:* server cages, window-washing platform, the overhead projector that weighs as much as a body (it does not contain a body) (it contains a body).
  - *Alibi protocol:* you must be seen *together* at the moment of the crime you are both committing elsewhere. Remote-view tablet + voice sync = being in two places at once.
- **Proximity voice chat** with in-world consequences: whisper in vents, use the phone system for cross-floor calls, and *never* talk near the smoke detector (it's a mic. It's always been a mic).
- **Secret objectives:** each spy privately rolls a bonus objective per shift, sometimes conflicting (*"Ensure Keith is fired"* vs *"Protect Keith"*). Co-op trust mechanics. Divorce speedrun any%.

---

# PART VII — VR MODE

## 20. Full VR Compatibility (PCVR / Quest standalone stretch)

Not a port — a parallel design:

- **Physical comedy is the point:** actually mime typing to look busy; physically stack boxes to reach vents; hold a body upright and *walk it* like a puppet; wave at the security camera with the victim's arm.
- **Interaction set:** two-hand grab economy, physics throwing, pocket inventory (chest/hip holsters), watch-based UI (your smartwatch IS the HUD — suspicion meter, objectives, group chat), physical badge swiping, manual lockpicking (tension wrench feel), mop = two-handed scrubbing workout.
- **Dialogue in VR:** speak aloud (speech-to-text → LLM) or pick from a wrist-menu of intents; NPC voice via spatialized TTS.
- **Comfort tiers:** teleport + smooth loco, seated mode, "vignette on carry," cart-sickness guard rails; all takedowns are *implied* in comfort mode (screen-punch bonk → optional).
- **VR-exclusive content:** the Trust Fall takedown, whiteboard diagram forging (draw it yourself), the executive putting green, VR co-op high-five system (mechanically required for two sync interactions — it's important to high-five after a successful crime).

---

# PART VIII — PRESENTATION & TECH

## 21. Technical Architecture

| System | Choice | Notes |
|---|---|---|
| Engine | **Unreal 5** (primary) / Unity HDRP (alt) | AA lighting + physics + VR maturity; Three.js prototype remains the playable pitch |
| Physics | Chaos (UE) — full rigid body + cloth-lite for papers/ties | Ragdolls: active-ragdoll hybrids (pose-matched, punchy) |
| NPC AI | Utility AI + behavior trees; schedule sim on a 24-min compressed day cycle | 60–90 concurrent NPCs per floor slice; LOD'd brains |
| Dialogue | Local SLM (barks/simple) + cloud LLM (conversations), hard character-sheet guardrails, content-safety layer, deterministic "combat barks" fallback if offline | Voice: neural TTS w/ per-character profiles; lipsync via viseme curves |
| Networking | Host-authoritative co-op (2P), deterministic lockstep for the sim where feasible | Physics: host-simulated, client-predicted for held props |
| VR | OpenXR; shared flat/VR lobbies supported (asymmetric co-op: one VR, one flat) | |
| Persistence | Per-campaign office state: population, relationships, alarm level, HR cases, your reputation | Save scumming allowed but narrated by your disappointed handler |
| Modding | Floor editor + contract editor + NPC-pack support (stretch) | |

## 22. Art Direction

- **Style:** stylized-realistic ("Pixar does a procedural") — clean shapes, expressive faces, slightly-too-bright corporate palette gone subtly wrong per floor (Legal is beige to a threatening degree).
- **Animation:** motion-matching locomotion + a dedicated **Slapstick Layer** (procedural flails, double-takes, slow looks at camera). Active ragdolls tuned for comedy timing, not realism.
- **UI:** diegetic-first — smartwatch HUD, phone, laptop desktop parody, the intranet ("OmniPortal") as the menu system. Patch notes appear as company memos.

## 23. Audio

- **Muzak engine:** dynamic smooth-jazz/corporate-lofi that *drops instruments out* as alarm rises; full alarm = the muzak keeps playing but slightly wrong (detuned).
- **Sound as mechanics:** printers are loud (cover), keyboard clatter masks footsteps, the kombucha tap hisses, the server room hum is deafening (perfect crime room, terrible ambiance).
- **Voice:** full TTS cast + a celebrity-voice pack (stretch goal, licensing permitting).

## 24. Accessibility

Full subtitle/speaker-label system, colorblind-safe suspicion indicators (icon + bar, never color-only), one-handed control schema, arachnophobia-style toggle for the server-room spiders (V2), difficulty granular per pillar (combat / stealth / social sliders), "assist mode" that automates dialogue minigames without skipping content.

---

# PART IX — CAMPAIGN & CONTENT

## 25. Campaign Structure (15–25 hrs)

- **Act I (Intern→Associate):** learn the verbs; small thefts; your first framed rival; discover OmniCore is spying on *someone else too*.
- **Act II (Senior→Manager):** rival spy agency revealed (they're *also* embedded; it's the ridiculously friendly Sales VP); mid-campaign twist: your handler goes dark.
- **Act III (Director→Executive):** the Vault Floor; choose your ending: sell the secret, expose OmniCore, *become* OmniCore (hostile takeover via HR), or burn it all down (literally, with the fire system you now have admin rights to).
- **Side content:** 30+ optional contracts, 12 rival-spy encounters, the office romance subplot you can third-wheel, the rat in the walls (he's also a spy) (he's a drone).

## 26. Mission/Contract Generator (Replayability)

Procedural contracts compose: **[Asset] + [Location] + [Complication] + [Twist]**
e.g. *Steal* + *prototype battery* + *from the cleanroom* + *during a surprise audit* + *but the floor manager is your framed-rival's vengeful spouse.*

## 27. Live Ops (AA-honest)

Seasonal floors (Holiday Party = chaos mode), contract packs, new disguises, community contract browser. **No battle pass. No FOMO. We're not monsters — we just play them at work.**

---

# PART X — THE MECHANICS INDEX (The "100s of Mechanics" Appendix)

A non-exhaustive verb/noun list the sim supports — design rule: *if a player asks "can I…?", the answer is probably yes.*

**Stealth:** crouch, vent-crawl, ceiling-crawl, locker-hide, desk-sleep-fake, crowd-blend, meeting-sit-in, badge-tailgate, camera-duck, camera-loop, light-break, fuse-box-darkness, fire-alarm-pull, sprinkler-trigger, elevator-override, stairwell-echo-listen, door-wedge, door-jam, keycard-pickpocket, ID-swap-photo, uniform-steal, janitor-cart-stowaway, mail-chute-slide, dumbwaiter-ride, window-cleaner-platform, roof-access-pick, helipad-stowaway, shadow-the-exec, ghost-the-badge-log, spoof-the-badge-log, photocopier-distraction, printer-fire-staging, smoke-bomb-vape-cloud, pigeon-release…

**Social:** small-talk, deep-talk, stall, befriending, blackmail, bribe, gossip-seeding, rumor-correcting, compliment-fishing, weaponized-empathy, fake-cry, buzzword-fencing, meeting-hijack, minute-taking (alter the record), toast-giving (funeral), toast-giving (birthday), mentorship-farm, sabotage-a-friendship, arrange-a-date, ruin-a-date, HR-report, anonymous-HR-report, union-agitate, strike-organize (distraction tier-S), promote-a-patsy, demote-a-rival…

**Logistics:** shoulder-carry, drag, trolley-wheel, chair-wheel, rug-roll, furniture-pose, lamp-dress, scarecrow-pose (break room), meeting-seat-prop (sunglasses on), vent-stash, rack-file, shredder-feed, incinerator-feed, mail-out, courier-out, drone-out, aquarium-dunk (do not), freezer-hold, ceiling-cache, desk-drawer-cache, bathroom-tank-cache, plant-pot-cache…

**Crime:** hack-terminal, clone-badge, forge-memo, forge-signature, doctor-video, plant-files, plant-blood, plant-badge, swap-hard-drive, swap-presentation (the *Why We're All Doomed* deck), keylog, phone-tap, coffee-spike, kombucha-spike, decaf-swap, chair-height-sabotage, ergonomics-report-weaponization, thermostat-war, wifi-outage-scheduling…

**The Meta:** performance-review-maxxing, KPI-laundering, blame-deflection, case-ghosting, testimony-coaching, witness-relocation (storage cupboard), rival-grooming, rival-framing, handler-radio, dead-drop-shopping, cover-identity-maintenance (attend your fake job!), dual-life-scheduling, exit-interview-attending (sociopath route)…

*(Target: 250+ implemented verbs at ship. Every verb must have at least one clip-worthy failure state.)*

---

# PART XI — RISKS & OPEN QUESTIONS

| Risk | Mitigation |
|---|---|
| LLM dialogue breaks tone or safety | Hard character sheets, retrieval-bounded memory, curated fallback bark decks, offline mode = fully scripted |
| Systemic sim is unfinishable by QA | Chaos-harness: automated soak-tests of AI+physics overnight; "comedy is a bug we keep" triage board |
| VR + flat co-op parity | Asymmetric design from day one, not a post-port |
| Co-op netcode vs full physics | Host-auth + prop-ownership handoff; ragdolls host-only |
| Scope: 250 verbs | Tier the index: 60 ship-verbs (quality), rest via updates/mods |
| Office violence ratings | Slapstick framing, non-lethal default path, 💤 not gore-porn |

## Vertical Slice Definition (what we build first)

One floor (Cubicle Farm 3) + Lobby + Server Core slice, 25 NPCs, 1 contract, core verbs (≈40), suspicion/curiosity/panic loop, blood & cleanup, tape room, 1 dialogue set-piece (Conference Call Trap), flat + VR, 2P co-op. **The Three.js prototype already proves: bonk → ragdoll → blood → curiosity → panic → mop/hide → blame. Everything else is scale and polish.**

---

*This document is a living bible. It will be updated as OmniCore's legal team requires. OmniCore's legal team is not aware of it.*
