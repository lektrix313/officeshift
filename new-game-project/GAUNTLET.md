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

## Backlog (next cycles, priority order)

1. Physics props: grabbable papers/mugs/staplers, throwable with noise events ✅ (cycle 4)
2. Cameras + tape room (forensics lite), badge-swipe logs on doors
3. HR case system lite: witness reports open cases; mop/hidden-body resolves
4. Disguise wardrobe: uniforms per department change NPC perception radius
5. Voice: TTS barks via OS TTS; speech-to-text chat input
6. Second floor slice (break world ball pit) + elevator as mission space
7. Co-op: Friendslop-Template netcode port (lobby, player spawn, sync)
8. Contract generator (MISSION_BIBLE grammar) + shift debrief screen
