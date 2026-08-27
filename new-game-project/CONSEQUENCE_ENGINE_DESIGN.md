# Consequence Engine — Fun-First Social Model

## Design promise

Office Shift is a joyful co-op workplace comedy, not an anxiety simulator. The simulation can be deep underneath, but the player experience must stay readable, forgiving, and funny.

> The office remembers what happened, but it does not hold a grudge forever.

Every consequence should be one of:

1. A new opportunity.
2. A funny complication.
3. A recoverable setback.

Small mistakes create soft suspicion and social awkwardness. Hard consequences require repeated or severe evidence. A failed plan should usually produce a more complicated plan, not a forced restart.

## Canonical cast contract

Agent Red is the player. The main office has 19 NPCs: Bob, Sleepy Steve, Pam, Mr Purple, Fran, Chad, Rita, Mailroom Mike, Dave, Liz, Nervous Ned, Manager Mo, Jen, Data Dave, Boring Bill, Boss Barbara, Joe, Kevin, and Old Tom. Their job-specific observation channels are documented in `CANONICAL_STAFF.md` and are part of the consequence engine, not cosmetic flavor.

## Three visible player metrics

Only these are player-facing campaign metrics:

- **Suspicion** — how much attention is directed at the player.
- **Loyalty** — how strongly the company believes the player is on its side.
- **Work** — how useful and competent the player appears.

All three are bounded from 0 to 100 and change through authored action profiles, not scattered magic numbers.

Internal simulation values such as trust, stress, comfort, fear, anger, resentment, curiosity, gossip confidence, and corporate threat remain NPC or department context. They should be communicated through dialogue, emotes, movement, emails, gossip, access changes, and funny office updates rather than extra dashboards.

## Action-to-consequence pipeline

```text
Action occurs
  -> objective event is created
  -> eligible observers are found by radius, line of sight, cameras, logs, or social links
  -> observations receive confidence and source information
  -> individual beliefs and feelings update
  -> a timed attitude/reaction is selected
  -> gossip, reports, assistance, or investigation may be queued
  -> department/company context changes
  -> access, security, work expectations, and mission state adjust
```

There is no magical omniscience. A nearby NPC can react to a jammed printer. A floor-wide fire alarm can reach everyone. A keycard log may only affect Security when they review it. A rumor travels through relationships and proximity. Job specialties add relevance, not knowledge: Rita notices identity, Fran notices finance, Steve/Data Dave notice systems, Kevin notices inventory, Pam/Barbara notice HR and contradictions, Joe notices maintenance, and Liz notices visual evidence.

## Real-time cadence

- **Immediate:** direct interaction, alarm, crime, evidence discovery, visible work result.
- **Per-second:** nearby reactions, movement, panic, active suspicion, object-state response.
- **Every few seconds:** gossip, department aggregation, investigation progress, trust changes.
- **Shift milestones:** work review, promotions, campaign threat, company memory.

A priority queue keeps the office readable:

1. Emergency.
2. Direct player interaction.
3. Personal threat.
4. Work obligation.
5. Department concern.
6. Gossip.
7. Ambient routine.

## Timed attitudes

NPC reactions have three time horizons.

### Immediate reaction — seconds

Shock, confusion, anger, panic, curiosity, excitement, and embarrassment last roughly 3–20 seconds. They provide visible comedy and movement without creating long-term punishment.

### Active attitude — minutes

Suspicious, annoyed, impressed, grateful, afraid, nosy, protective, and resentful last roughly 1–8 realtime minutes. The active attitude affects conversation, willingness to help, attention, and local reaction choices.

### Shift memory — hours or until the shift ends

Important events remain as fallible memories for gossip, HR cases, debriefs, and future dialogue, but do not continuously punish the player every frame.

### Campaign memory — persistent but lightweight

Only major events persist between jobs: identity exposure, company threat pattern, a major frame, a destroyed building, a famous method, or a changed security policy. Persistent memories create new content rather than only adding difficulty.

## Fun-first duration guidance

| Event | Immediate | Active attitude | Long-term memory |
|---|---:|---:|---|
| Printer jam | 5–10 sec | 1–2 min annoyed | Low |
| Computer glitch | 5–15 sec | 2–4 min frustrated | Low |
| Coffee delivered | 3–8 sec | 2–5 min grateful | Low |
| Minor lie | 3–10 sec | 2–4 min doubtful | Medium |
| Failed keycard | 5–12 sec | 3–6 min suspicious | Medium |
| Taking credit | 5–15 sec | 4–8 min resentful | Medium |
| Blaming a coworker | 5–20 sec | 5–10 min distrust | High |
| Witnessed theft | 10–30 sec | 5–12 min suspicious | High |
| Body discovery | 20–45 sec | 5–15 min fearful | High |
| Fire alarm | 20–60 sec | 3–8 min disrupted | Medium |
| Hostage event | 30–90 sec | Until resolved | Very high |
| Building destruction | 1–3 min | Mission-long | Campaign |

Personality should flavor these values modestly. Calm staff recover at about 0.75x, dramatic staff at about 1.25x, and highly anxious staff at no more than about 1.4x. Never let one personality create an unknowable difficulty spike.

## Recovery is always available

Every soft or medium negative attitude should have at least one clear recovery route:

- Talk: reassure, explain, apologize, flatter, redirect, recruit, or gossip.
- Help: fix equipment, find paper, deliver work, prepare a room, bring coffee, or assist in an emergency.
- Do visible work: convert suspicious inactivity into a believable work explanation.
- Give credit: repair resentment after taking or presenting someone else's work.
- Offer a better explanation: use cover, loyalty, work reputation, and evidence quality.
- Create a new event: shift attention with a printer issue, meeting, alarm, or urgent task.
- Use the co-op partner: distract, provide a second story, take responsibility for a harmless error, or maintain visible work.

Recovery should lower active attitudes faster than it erases important memories. Serious events can become survivable without becoming meaningless.

## NPC feelings and beliefs

Each NPC may internally track stress, comfort, fear, anger, trust, resentment, curiosity, confidence, and company loyalty. Only the most important state is shown at once:

- One primary attitude.
- One secondary feeling.
- One current goal.
- One active rumor.

Feelings modify behavior:

- High stress causes mistakes, distraction, and faster panic.
- High fear causes fleeing, immediate reports, and exaggerated testimony.
- High comfort increases forgiveness and cooperation.
- High resentment encourages hostile gossip and evidence gathering.
- High trust makes explanations and requests more effective.

Feelings recover through time, work, comfort objects, successful help, and ordinary routine.

## Gossip

Gossip is a lossy social network, not a global broadcast. A rumor contains a claim, source, confidence, emotional charge, distortion, age, and supporting evidence.

When NPC A tells NPC B:

```text
new confidence = source confidence
               * source trust
               * listener attention
               * social affinity
               * (1 - distortion)
```

Distortion increases with emotion, time, social distance, retellings, resentment, and the desire to impress. Players can participate by adding details, correcting a rumor, defending a target, pretending to be shocked, or planting a better explanation.

## Work as cover

Work is measured through readable outcomes, not tedious spreadsheet simulation. Tasks are short, visible, and useful for creating access:

- Deliver a document.
- Fix a printer.
- Prepare a meeting room.
- Process a small batch of mail.
- Answer a coworker's question.
- Update a presentation.
- Move files between departments.
- Complete a simple research task.

Taking credit transfers value rather than creating free value:

```text
player work += stolen work value * presentation quality
victim work -= stolen work value * attribution confidence
victim resentment += stolen work value
```

Blaming another employee requires a plausible connection, evidence, a believable story, and a target whose existing loyalty or work reputation makes the accusation stick.

## Reporting and loyalty

A report may be honest, exaggerated, partially true, fabricated, or self-protective. It contains a target, allegation, evidence, witness confidence, timing, motive, and the player's relationship to the target.

Reporting another employee can raise loyalty and temporarily lower suspicion, but creates social debt: resentment, hostile friends, HR contradictions, or a pattern that may eventually point back to the player.

## Company threat versus personal suspicion

These are deliberately separate:

- **Personal suspicion:** does the company suspect this player or cover identity?
- **Company threat level:** does the organization expect a professional espionage operation?

A perfect mission can produce:

```text
no witnesses
no player identification
contract completed
loyalty high
work high
company threat level high
next job starts at suspicion level 5
```

The player won, but the next company is ready for someone like them.

## End-of-shift readability

Show the player:

- Contract: Failed, Partial, Completed, or Completed with Bonus.
- Suspicion: Low, Noticed, or Wanted.
- Loyalty: Distrusted, Accepted, or Trusted.
- Work: Poor, Solid, or Outstanding.
- Style: Ghost, Social Engineer, Saboteur, Scapegoat Artist, Corporate Menace, or Absolute Disaster.

Then show funny details such as printer incidents, blamed coworkers, minutes spent distracting Security, impossible meetings, and whether the building survived.

## Sanity checks

- No single small mistake should hard-fail a shift.
- No attitude should last forever unless it represents a major campaign memory.
- High Work softens management consequences but never erases serious evidence.
- High Loyalty makes reports and explanations more credible but never makes the player invisible.
- Low Suspicion does not mean the company trusts the player.
- Every active negative attitude must have a readable recovery route.
- The player sees consequences through people and events before seeing numbers.
- Co-op can rescue a bad situation and should create funny role swaps.
- Repeat play should reveal staff personalities, not require memorizing hidden formulas.
- Chaos should be a valid successful playstyle with different penalties and rewards.
