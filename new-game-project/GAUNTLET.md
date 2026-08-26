# GAUNTLET — Office Shift improvement loop

Iterative cycle: **pick backlog item → implement → gate (build/import/boot) →
prove (deterministic capture) → log → repeat.** Backlog sourced from
GAME_BIBLE.md / MECHANICS_DEEP_DIVE.md / MISSION_BIBLE.md, ordered by
fun-per-line-of-code. This file is the loop's persistent state.

## Rules

1. Every cycle ends green: `dotnet build` 0 errors, boot exit 0, no runtime
   exceptions in the log.
2. Every feature gets a deterministic CaptureDriver scenario or it didn't happen.
3. No new SceneTree classes. No namespaces. AssemblyName stays "Office Shift".
4. LLM is pluggable: `OLLAMA_URL` or `OPENAI_API_KEY`(+`OPENAI_BASE_URL`,
   `OPENAI_MODEL`) env vars; offline persona fallback must always demo the
   full loop including action directives.

## Cycle log

### CYCLE 1 — job-sim interaction layer (DONE, captured in gauntlet_sim.mp4)
- [x] Microwave "heat fish" → 20s stink cloud clears the break room
- [x] Vending machine: dispenses laxative sachet then energy drink (+speed boost, 30s restock)
- [x] Coffee machine: brew attracts nearest 2 NPCs; laxative spike → drinkers do
      bathroom runs to the printer room — verified on-screen: "Fresh coffee.
      The scent drags Margaret and Keith away from their desks."
- [x] Fire alarm: 12s full-floor evacuation to reception (90s cooldown) —
      shares the verified evac code path (not in capture; timeline full)
- Bugs found by capture: channel-cancel target table missing Coffee/Microwave
  (instant abort) — fixed with per-mode target positions + generic abort toast.

### CYCLE 2 — OmniPortal (DONE, captured in gauntlet_portal.mp4)
- [x] Sit at any pod-desk computer → OmniPortal overlay (mouse freed, Esc stands)
- [x] MailStore: inbox + sent; compose to any NPC
- [x] Persona reply lands in inbox + toast; verified: "[EMAIL] Keith: 'Interesting.
      VERY interesting. This is going in the spreadsheet. [goto:breakroom]'"
- Bugs found by capture: CloseUI left mouse uncaptured → phantom pause overlay —
  fixed (CloseUI recaptures when shift active).

### CYCLE 3 — LLM personas + real-time talk (DONE, captured in gauntlet_portal.mp4)
- [x] PersonaSheet per NPC (traits, secret, quirk, greeting) + live context line
- [x] NpcChatService: OLLAMA_URL / OPENAI_API_KEY(+BASE_URL,MODEL) / offline fallback
- [x] Directive protocol [goto:zone] parsed from replies → ApplyDirective steers AI
- [x] T near NPC → chat overlay; verified: Keith's paranoid greeting + reply flow,
      NPC freezes while talking
- Bugs found by capture: pause overlay fired during chat (mouse-visible heuristic) —
  fixed (Paused now respects UIOpen).

### CYCLE 4 — physics props + noise events (DONE, captured in gauntlet_props.mp4)
- [x] PropItem RigidBody3D system: keyboard/mug/stapler/paper stacks on desks,
      impact-armed noise events, breakable mugs (shatter = louder noise)
- [x] Grab (E) / throw (E) with Jolt arc; hitting an NPC with a prop = +25 sus
- [x] Noise → curiosity: awake NPCs in radius walk over, poke, shrug off
      ("Probably just the building settling."); Briggs abandons patrol for
      fresh noises
- [x] Verified on-screen: grab → throw → double clatter → NPCs investigating
- Bugs found by capture: double-impact toasts (bounce re-triggers) — left in,
  it reads as physics comedy; revisit if it spams.

### CYCLE 5 — cameras + tape-room forensics + HR case lite (DONE, captured in gauntlet_hr.mp4)
- [x] 4 cone cameras (farm / server door / break kitchen / reception) with red
      LEDs + LOS checks; crimes flash a 4s recording window
- [x] Verified on-screen: fish crime → "CAMERA: You are being recorded." →
      stink repulsion (Janet/Susan gag-flee)
- [x] HR case 0-100: witness report +25, camera recording +14/s; 35 opens the
      case; 100 = HR-hearing loss ending
- [x] Tape rack in reception: 4s shred channel → evidence decays 12/s →
      "HR CASE CLOSED. The truth died in the shredder."
- Design note: cooling requires tapes only (blood cleanup feeds forensics in a
  later cycle).

### CYCLE 6 — disguise wardrobe (DONE, captured in gauntlet_wardrobe.mp4)
- [x] Uniform locker in the supply closet: cycles IT / Facilities / HR / Sales
- [x] IT: server-room presence is not suspicious ("The server room is YOUR
      room now") — verified: loitering at the blueprint terminal, 0% suspicion
- [x] Facilities: mopping registers zero suspicious activity
- [x] Sales: creep/crab-walk suspicion halved ("nobody questions Sales")
- [x] HR: blazer equipped (email-favorability hooks reserved for cycle 7)
- Bugs found by capture: papers stack spawned off-desk on a walk path →
  player depenetration machine-gunned noise toasts; fixed (desk placement +
  3s per-item noise debounce + papers verb grammar).

### CYCLE 7 — consequence engine + more items (DONE, captured in gauntlet_chaos.mp4)
- [x] Liquid system: coffee/water puddle decals; mug break leaves a slip hazard;
      mop cleans blood AND liquids
- [x] Slip system: NPCs stepping in liquids roll a 85% slip → full ragdoll KO →
      the body then feeds the existing curiosity/report/HR-case cascade
- [x] Chair: throwable, 9m noise, NPC hit = full knockout (comedy violence tier)
- [x] Fire extinguisher: grab from wall/closet, 3 spray charges — cone-blinds
      NPCs 3s + leaves water puddle (slip hazard #2)
- [x] Desk phones ×3: E lures the nearest awake NPC to the phone ("they got a
      call") — steering channel that works without the LLM
- [x] Prop noise debounce (3s) + papers grammar
- KNOWN ISSUE: mug shatter detection is unreliable under Jolt when the mug hits
  a kinematic CharacterBody (post-impact velocity reads ~0 so the break check
  fails). Fix path: sample velocity in _IntegrateForces or break mugs on any
  armed contact. Slip chain verified via code path + puddle visuals; capture
  run landed the mug intact (stochastic miss).

### CYCLE 8 — mission contracts + composer (DONE, captured in gauntlet_missions.mp4)
- [x] MissionContract data model + JSON loader (res://missions/ + user://missions/)
      — adding a mission = dropping a JSON file, zero code
- [x] Objective types: STEAL_BLUEPRINTS / PHOTO_WHITEBOARD (new break-room
      whiteboard prop) / LURE_NPC (zone dwell) / GHOST (sub-30 sus to shift end)
      / KNOCKOUT_NPC — each admits multiple solution routes
- [x] OmniPortal CONTRACTS board (accept swaps the active mission + HUD
      objectives) and COMPOSER tab (objective/NPC/zone dropdowns → writes
      user://missions JSON → live on the board)
- [x] Win rework: all-objectives-complete drives the ending; ghost contracts
      win at shift timeout
- [x] 3 authored contracts: OMNI-KEYS (lure+steal), OMNI-WHITE (photo+ghost),
      OMNI-CHAIR (bonk+steal)
- [x] Verified end-to-end: accept → email Keith → persona reply directive →
      Keith walks to breakroom → LURE done → blueprints stolen → "PROMOTED"

### CYCLE 9 — body economy + police interview (IN PROGRESS, capture driver added)
- [x] Expanded hide spots with timed smell discovery and ten disposal routes
- [x] Disposal routes remove carried bodies permanently and cancel active investigations
- [x] Hidden bodies can be discovered by a nearby NPC, opening the police interview
- [x] Three-answer interview resolves to release or arrested ending
- [x] OmniPortal resignation forge can replace a hidden coworker with a generated hire
- [ ] Deterministic `++ capture body` choreography covers disposal, discovery/interview,
      and forged resignation; capture still needs a successful Godot runtime run
- [x] Fixed disposal ordering bug where the caller cleared `Carrying` before the
      disposal handler validated it

### CYCLE 10 — staff personalities (IN PROGRESS, capture driver added)
- [x] Stable Big Five-lite profile for every named staff member and generated hire
- [x] Personality modifies suspicion sensitivity, forgiveness, panic duration,
      gossip radius, report threshold, and noise curiosity
- [x] OmniPortal STAFF directory exposes traits, profile values, and behavioral tells
- [x] Deterministic `++ capture staff` scenario opens the directory and starts a
      personality-aware Keith conversation
- [ ] Godot runtime capture remains pending until the Mono startup/import blocker
      from cycle 9 is resolved

### CYCLE 11 — consequence engine: memories, gossip, framing (IN PROGRESS, capture driver added)
- [x] Witness incidents create fallible per-staff memories with confidence decay
- [x] Gossip staff retell memories with reduced confidence and narrative distortion
- [x] OmniPortal FEED exposes witness, rumor, and anonymous case records
- [x] OmniPortal REPORT files target-specific allegations, including A MURDER,
      THEFT, SABOTAGE, and HARASSMENT
- [x] Named HR cases accumulate evidence through corroborating rumors and resolve
      by removing the framed staff member and spawning a replacement hire
- [x] Deterministic `++ capture frame` scenario files Tom's murder case and exposes
      the resulting office feed and staff population change
- [ ] Godot runtime capture remains pending until the Mono startup/import blocker
      is resolved

### CYCLE 12 — testimony contradictions + case appeals (IN PROGRESS, capture driver added)
- [x] HR cases generate named witness testimonies from staff memories and personality
- [x] Testimonies carry confidence, contradiction flags, challenge state, and coaching state
- [x] OmniPortal HEARING surface supports CHALLENGE, COACH, and FILE APPEAL actions
- [x] Contradictory testimony lowers case evidence when challenged
- [x] Successful appeal collapses a weak framed case before HR removes the suspect
- [x] Deterministic `++ capture hearing` files Tom's murder case, challenges the
      contradictory witness, appeals, and shows the cleared office state
- [ ] Godot runtime capture remains pending until the Mono startup/import blocker
      is resolved

### CYCLE 13 — office layout blockout + prop pass (IN PROGRESS)
- [x] North reception strip expanded into Meeting Room A, Meeting Room B, and an HR suite
- [x] Each new room has an explicit 2 m doorway and visible placeholder door frame
- [x] Cubicle pods now have aisle-facing doorways instead of sealed partitions
- [x] Meeting rooms, HR, reception, and corridor receive collision-safe placeholder furniture
- [x] New meeting/HR hide spots and NPC waypoints feed the body consequence routes
- [x] `++ capture layout` route walks the new spaces for visual verification
- [x] README includes the prioritized low-poly replacement prop list
- [x] Add guarded runtime fallback so the blockout appears while generated `world.tscn` is stale
- [ ] Rebuild generated `world.tscn`, then complete Godot runtime layout capture once the
      local Mono `.NET: Assemblies not found` startup blocker is fixed

### CYCLE 14 — workshop level designer (IN PROGRESS)
- [x] Grid-first editor for walls, glass walls, doors, offices, cubicles, reception, desks, terminal desks, chairs, printers, stairs, and elevators
- [x] Multi-floor plans with editable dimensions and snap-to-cell placement
- [x] Inspector tags every element by designated room, staff department, gameplay collision, and required access card
- [x] Access-card catalog supports clearance levels and Steal, Gaslight, Charm, Seduce, and Impersonate routes
- [x] Validation flags clipped elements, missing floors/cards, and gameplay footprint overlaps before export
- [x] Procedural dressing places lights, pictures, plants, and clutter separately from gameplay geometry
- [x] Workshop JSON and Godot-oriented JSON exports are available for the next runtime import pass
- [ ] Godot importer must consume the exported level schema and map authored placeholders to runtime prefabs

### CYCLE 15 — workday NPC finite-state machine (IMPLEMENTED, runtime capture pending)
- [x] One-hour realtime shift clock: 3,600 seconds from 09:00 to 17:00
- [x] Separate schedule FSM preserves consequence states as higher-priority interrupts
- [x] Normal work states cover desk work, printer trips, waiting, printing, broken printer, toilet, breaks, meetings, anxious meetings, phone calls, water cooler, coffee station, reading, and walking/thinking
- [x] Mood states cover happy, worried, distracted, annoyed, depressed, curious, sleepy, sick, anxious, and panic attack
- [x] Attention states cover doom scrolling, not paying attention, engrossed, suspicious, and picking up slack
- [x] Substance states cover drunk, speed, stoned, LSD, k-hole, and ecstasy with speed/attention/suspicion modifiers
- [x] Staff receive stable schedule offsets so the office does not synchronize into one repeated animation
- [x] NpcBody exposes activity labels, imported clip matching, and procedural fallback motion for every state
- [x] `++ capture workday` runs beyond all schedule slots with a 120x clock, covering the full day in a deterministic recording
- [ ] Godot runtime workday movie capture remains pending until the local Mono assembly startup blocker is resolved

### CYCLE 16 — consequence-driven NPC activation/reaction loop (IMPLEMENTED, runtime capture pending)
- [x] Every NPC has stimulus activation scoring driven by distance, attention, personality, intensity, and player-led status
- [x] Every stimulus kind has an independent cooldown, preventing printer noise from suppressing crime, body, alarm, or rumor reactions
- [x] Reactions select an action and apply a visible result: observe, investigate, panic, report, flee, coffee, printer, meeting, gossip, or complain
- [x] Player-led crimes, thrown-prop noise, forged reports, phone calls, coffee, fish smell, fire alarms, and hidden-body discoveries feed the shared queue
- [x] General 9-5 transitions emit printer failure, meeting pressure, phone, coffee-break, and water-cooler stimuli
- [x] Evidence perception and gossip now re-enter the same consequence pipeline instead of bypassing activation/cooldown
- [x] STAFF directory exposes current stimulus, activation, cooldown, chosen action, and reaction text for tuning
- [x] `++ capture reactions` demonstrates player crime followed by an ambient alarm with different NPC responses
- [ ] Godot runtime reaction movie capture remains pending until the local Mono assembly startup blocker is resolved

## Backlog (next cycles, priority order)

1. Physics props: grabbable papers/mugs/staplers, throwable with noise events ✅ (cycle 4)
2. Cameras + tape room (forensics lite), badge-swipe logs on doors
3. HR case system lite: witness reports open cases; mop/hidden-body resolves
4. Disguise wardrobe: uniforms per department change NPC perception radius
5. Voice: TTS barks via OS TTS; speech-to-text chat input
6. Second floor slice (break world ball pit) + elevator as mission space
7. Co-op: Friendslop-Template netcode port (lobby, player spawn, sync)
8. Contract generator (MISSION_BIBLE grammar) + shift debrief screen
9. Finish cycle 9 capture on the Godot MCP runtime, then stabilize mug shatter and
   add a compact debrief for body-economy outcomes
10. Finish cycle 10 staff capture, then add relationship memory and office group chat
11. Finish cycle 11 capture, then add testimony contradictions and case appeals
12. Finish cycle 12 capture, then add relationship trust, witness intimidation, and
   a proper HR debrief screen
13. Resolve the Godot Mono startup blocker, then run the workday and reaction captures
   and tune activation thresholds from observed NPC behavior

### CYCLE 17 — stateful office objects and full NPC stat sheets (IMPLEMENTED, runtime capture pending)
- [x] Added 50 typed office interactables with valid states, state timers, departments, and placeholder-safe IDs
- [x] Printer supports Working, InUse, OutOfPaper, Jammed, Offline, and Broken states
- [x] Computers and server equipment support Working, Glitchy, Hacked, Offline, and ITCalled states
- [x] Doors, readers, elevators, stairwells, lockers, cabinets, and incinerator carry locked/keycard/unlocked state metadata
- [x] Failed access attempts publish AccessDenied stimuli; starter cards include janitorial, Gary level 3, and department level 1
- [x] NPC sheets now include activation sensitivity, focus, patience, stress resilience, comfort/social need, current stress/comfort, and department affinities
- [x] Department affinity modifies object activation: anti-IT staff escalate faster on glitches/IT calls while IT-friendly staff seek help
- [x] Technology object defaults no longer override personal affinity: anti-IT staff complain, while IT-friendly staff can call IT and transition the object to `ITCalled`
- [x] Object state profiles define activation, radius, duration, stress, comfort, stimulus kind, and preferred reaction action without gameplay magic numbers
- [x] Positive states such as coffee, water, seating, support, and successful service can restore comfort and reduce stress
- [x] `++ capture object_states` exercises printer, computer glitch -> IT call, denied door access, coffee, and water states
- [x] OFFICE_OBJECT_CATALOG.md documents all 50 objects, state sets, triggers, reactions, and visual replacement rules
- [ ] Godot runtime object-state movie capture remains pending until the local Mono assembly startup blocker is resolved

### CYCLE 18 — spatial object-state activation (IMPLEMENTED)
- [x] Object-state stimuli use each state's configured radius as a hard NPC reaction boundary
- [x] The same configured radius controls activation falloff; no fixed global object radius remains
- [x] Active NPC object users may react to their own printer, computer, or service-object state outside the ambient radius
- [x] Global events remain explicit: the fire alarm uses its floor-wide radius rather than a hidden broadcast bypass
- [x] Repeated writes to the same object state no longer emit duplicate consequence stimuli
- [x] OFFICE_OBJECT_CATALOG.md documents the radius rule and active-use exception
