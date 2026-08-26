# Office Shift — Godot (prototype parity port)

Status doc per godogen runtime manifest: what is built, what is left, assets.

## What is built (prototype parity + gauntlet cycles 1-3 — DONE)

The full Three.js proof-of-fun loop, ported 1:1 to Godot 4.7 / C# / Jolt,
plus three gauntlet improvement cycles on top:

- **Job-sim layer**: microwave fish stink (mass repulsion), vending machine
  (laxative / energy drink), coffee machine (brew attracts NPCs; spike →
  bathroom runs), fire alarm (full-floor evacuation)
- **OmniPortal**: working in-game computer at any pod desk — inbox, sent,
  compose; NPC personas reply by email
- **LLM personas**: every NPC has a character sheet (traits/secret/quirk);
  talk with T or email via OmniPortal; replies carry [goto:zone] directives
  that steer NPC pathing. Pluggable backend: set `OLLAMA_URL` or
  `OPENAI_API_KEY` for live LLM; deterministic offline personas otherwise.
- Proof videos: `shots/gauntlet_sim.mp4` (scenario chain) and
  `shots/gauntlet_portal.mp4` (email → reply → Keith walks; live chat).

- **Office world** (510-node generated scene): cubicle farm, server room w/
  racks + blueprint terminal, printer room, break room, supply closet,
  reception; walls/props with Jolt colliders; vision-blocker AABBs for NPC LOS.
- **Player**: FPS controller (walk/crouch/carry speeds), bonk cone attack,
  carry/drop with viewmodel, disguise looting, mop, terminal download +
  mail-trolley delivery, photocopy gag, contextual prompts.
- **NPC AI**: 6 archetypes (Snoop/Gossip/Drone/Grifter/Slob/Security) with
  prototype-exact vision cones, suspicion dynamics, curiosity → panic →
  report chains, gossip spread, zone wandering; guard patrol/hunt/catch.
- **FX**: Jolt ragdoll crumple with settle-freeze + blood pool, Decal-based
  blood evidence (FIFO 90), procedural synth audio (bonk/alarm/success/pickup).
- **HUD**: objectives, clock, colorblind-safe suspicion meter, toasts,
  start/pause/end overlays.
- **Proof capture**: deterministic movie-writer run assembling `shots/proof.mp4`.

## Run it

```
dotnet build
<mono godot> --path .          # windowed play
```

Proof video: `shots/proof.mp4` (15 s, 30 fps).
Recapture: see `scenes/BuildScenes.cs` header (capture target).

## What is left (post-parity roadmap)

- PhysicalBoneSimulator3D ragdoll upgrade path (primitive crumple ships today).
- Footprint tracking from blood steps; more hide-spot gag props.
- Vertical-slice systems per GAME_BIBLE.md (cameras/tape room, HR cases,
  dialogue set-pieces, co-op — Friendslop-Template flagged as netcode base).

| Asset | Source | Status |
|---|---|---|
| `assets/models/soldier.glb` | copied from web prototype | imported, tinted per archetype |
| blood splat texture | procedural at runtime | done |
| audio blips | procedural synth at runtime | done |
| generated art (Gemini/Grok/Tripo3D) | deferred | awaiting API keys |

## Environment notes (Windows box)

See ARCHITECTURE.md "Hard-won environment rules". Key: AssemblyName must equal
config/name verbatim; one SceneTree class max; console-exe invocation pattern;
open the project ONLY in the mono editor build.
