# Office Object Catalog

This is the level-designer reference for the first office slice. Every listed object
has a typed definition in `scripts/OfficeObjects.cs`; behavior is data-driven and
meshes are replaceable placeholders.

Reference direction: [Look.png](../Look.png), [look2.png](../look2.png), and
[look3.jpg](../look3.jpg).

The visual target is a bright, readable low-poly office: white ceiling grid and
walls, blue-gray carpet, warm wood desks, framed art, plants, filing cabinets,
large windows, clear central aisles, and colored character silhouettes. Keep
interactive objects visually legible from the aisle. Preserve a 0.9 m NPC
clearance around desks, chairs, doors, and service points.

## Object Rules

- `Working`, `Ready`, `Available`, `Healthy`, `Clean`, and `Full` are stable positive states.
- `Offline`, `OutOfPaper`, `Jammed`, `Glitchy`, `Blocked`, `Broken`, `Empty`, and `Overdue` create operational friction.
- `Hacked`, `Missing`, `Spilled`, `Alarmed`, and `AccessDenied` are high-consequence states.
- `ITCalled` sends the responsible NPC to request technical help; IT affinity controls whether that calms or frustrates them.
- `Locked`, `KeycardRequired`, `Unlocked`, `Open`, and `Closed` belong to access objects. A failed access attempt publishes an `AccessDenied` stimulus.
- Every state profile has activation, radius, duration, stress, comfort, and preferred action values. Avoid adding raw thresholds to gameplay code.
- **Radius is a hard reaction boundary.** An NPC only receives an ambient object-state stimulus when they are within that state's configured radius. The activation score also uses that radius for proximity falloff, so a small-radius state is local and a large-radius state is broadly noticeable.
- **Active-use exception:** an NPC who is actively trying to use the object receives that object's state event even when they are outside the ambient radius. This is how a worker reacts to their own jammed printer or glitching computer without making the whole office react.
- Global events use deliberately large radii. For example, the fire alarm's floor-wide radius reaches everyone in the building, while a mug spill or broken chair remains local.
- A state may be player-led or ambient. Player-led events receive extra attention and can be witnessed as evidence.

## 50 Interactables

| # | Object | Valid states | Typical triggers | NPC effect |
|---:|---|---|---|---|
| 1 | Printer | Working, InUse, OutOfPaper, Jammed, Offline, Broken | Staff prints, empty tray, paper jam, power loss | Printer-phobic staff complain or call Facilities; patient staff investigate; nearby staff may queue for help. |
| 2 | Computer | Working, InUse, Glitchy, Hacked, Offline, ITCalled | Desk use, player hack, malware, network outage | Anti-IT staff activate faster and complain; technical staff seek IT; conscientious staff report tampering. |
| 3 | Desk phone | Idle, InUse, Offline, Recording | Incoming call, lure, wiretap, phone outage | Social staff answer or gather; suspicious staff observe; recording can raise evidence pressure. |
| 4 | Coffee maker | Ready, Brewing, Empty, Broken, Spilled | Brew, empty reservoir, sabotage, spilled coffee | Brewing creates comfort and a break migration; empty/broken creates complaints; spill creates investigation/slip risk. |
| 5 | Microwave | Ready, InUse, Broken, Overheated, Spilled | Lunch heating, fish, overheating, spill | Normal use is neutral; smell/overheat causes flee or complaints; comfort rises during an uneventful lunch. |
| 6 | Water cooler | Ready, Empty, Broken, InUse | Refill, drinking, gossip cluster, leak | Drinking restores comfort; empty/broken creates Facilities requests; social staff use it as a conversation point. |
| 7 | Vending machine | Ready, Empty, Broken, Restocking | Purchase, stock depletion, maintenance | Restocking attracts Facilities; failure frustrates impatient staff; successful purchase gives a short comfort beat. |
| 8 | Toilet | Available, Occupied, Blocked, Broken | Bathroom trip, queue, plumbing failure | Occupied/blocked creates impatience; a usable toilet reduces stress and supports normal routine. |
| 9 | Sink | Available, Blocked, Broken, InUse | Washing, clogged drain, cleaning | Clean use is calming; blockage creates complaints and a Facilities call. |
| 10 | Refrigerator | Available, Full, Empty, Broken, Spilled | Lunch storage, spoiled food, door left open | Missing lunch or spill frustrates owners; a working fridge supports comfort; spoiled food creates smell. |
| 11 | Meeting table | Available, Occupied, MeetingActive, Broken | Meeting start, crowding, damaged furniture | MeetingActive creates attendance pressure; overcrowding raises stress; a good meeting can improve comfort. |
| 12 | Whiteboard | Clean, InUse, Missing, Hacked | Planning, erased notes, forged plan, sabotage | In-use planning focuses Operations; missing or hacked notes activate suspicion and reports. |
| 13 | Projector | Working, Offline, Glitchy, Broken | Presentation, cable fault, player sabotage | Glitches frustrate presenters; IT-affine staff seek help; anti-IT staff blame technology faster. |
| 14 | Projector screen | Available, InUse, Broken | Meeting presentation, tear, obstruction | A working screen supports meeting focus; failure creates complaints and schedule delay. |
| 15 | Filing cabinet | Locked, Unlocked, Blocked, Missing | Key use, obstruction, stolen file | Missing files trigger reports; blocked drawers frustrate conscientious staff; unlocked access lowers search friction. |
| 16 | Paper shredder | Ready, InUse, Jammed, Full, Broken | HR cleanup, evidence destruction, jam | Jammed/full states create HR stress; successful shredding lowers case evidence and can comfort careful staff. |
| 17 | Server rack | Working, InUse, Overheated, Offline, Hacked, Recording | IT maintenance, overload, hack, surveillance | IT staff investigate; anti-IT staff panic/complain; security-affine staff report hacked racks. |
| 18 | Server terminal | Working, Locked, Unlocked, InUse, Hacked, Glitchy, ITCalled | Keycard use, blueprint download, hack, fault | Access denial creates help-seeking or complaints; hacking creates high suspicion and IT response. |
| 19 | Keycard reader | Locked, Unlocked, KeycardRequired, Offline, Hacked | Valid card, denied card, reader sabotage | Repeated denial raises frustration; security staff inspect; hacked readers cause reports. |
| 20 | Security camera | Recording, Offline, Hacked, Disabled | Normal surveillance, tape outage, player hack | Recording increases caution; offline creates security concern; hacked/disabled states trigger reports and HR evidence shifts. |
| 21 | Door | Locked, Unlocked, KeycardRequired, Open, Closed, Blocked, Hacked | Door use, card swipe, forced entry, obstruction | Denied access causes help requests or complaints; hacked doors trigger security reports; open doors reduce route friction. |
| 22 | Elevator | Available, Occupied, Offline, KeycardRequired, Blocked | Floor travel, crowding, outage, restricted floor | Queueing creates impatience; outage triggers Facilities; keycard denial activates security awareness. |
| 23 | Stairwell | Available, Locked, KeycardRequired, Blocked, Occupied | Floor travel, emergency lock, obstruction | A clear route lowers travel stress; blocked/restricted stairs create complaints or help calls. |
| 24 | Reception desk | Available, Occupied, InUse, Blocked | Visitor check-in, receptionist work, clutter | Reception activity gathers social NPCs; blocked desk frustrates visitors; smooth service improves comfort. |
| 25 | Cubicle | Available, Occupied, Blocked, Missing | Staff assignment, privacy wall move, removal | Occupancy anchors workday routines; missing walls reduce privacy and increase distraction/suspicion. |
| 26 | Office desk | Available, Occupied, InUse, Blocked | Staff work, desk reassignment, obstruction | In-use desks anchor work states; blocked desks delay work; a clean desk supports focus. |
| 27 | Office chair | Available, Occupied, Broken, Blocked | Sitting, chair theft, damage | A working chair supports comfort; broken chairs frustrate and pull Facilities attention. |
| 28 | Printer paper shelf | Full, Empty, Restocking, Blocked | Supply use, refill, delivery delay | Empty supplies trigger printer failure reactions; restocking gives Facilities a useful task. |
| 29 | Mail trolley | Available, Occupied, Delivered, Blocked | Mail run, blueprint delivery, clutter | Delivery completion gives Operations a positive beat; blocked trolleys create route complaints. |
| 30 | Garbage bin | Available, Full, Empty, Overdue | Normal disposal, missed pickup, overflow | Overdue bins create smell/flee reactions; empty bins support comfort and Facilities pride. |
| 31 | Recycling bin | Available, Full, Empty, Overdue | Recycling, overflow, pickup | Full bins cause mild complaints; successful recycling creates a positive Operations/F​​acilities beat. |
| 32 | Garbage chute | Available, Blocked, Full, Broken | Disposal, jam, body disposal, maintenance | Blocked chute produces complaints and Facilities calls; player disposal remains a consequence event. |
| 33 | Incinerator | Locked, Unlocked, InUse, Overheated, Broken | Janitorial keycard, disposal, heat fault | Restricted access creates denial reactions; overheated incinerator causes flee and security/Facilities attention. |
| 34 | Cardboard compactor | Available, InUse, Jammed, Full | Shipping cleanup, jam, overflow | Normal use is productive; jam/full states create complaints and a Facilities response. |
| 35 | Supply shelf | Full, Empty, Blocked, Restocking | Office supply use, reorder, delivery | Empty shelves frustrate conscientious staff; restocking creates a calm purposeful activity. |
| 36 | Uniform locker | Locked, Unlocked, KeycardRequired, Missing, Blocked | Uniform change, theft, missing stock | Missing uniforms create suspicion; valid access enables impersonation routes; denial creates help/complaint reactions. |
| 37 | First-aid cabinet | Locked, Unlocked, KeycardRequired, Empty, InUse | Injury, treatment, missing supplies | In-use treatment lowers stress; empty/locked access frustrates injured staff and raises HR attention. |
| 38 | Fire alarm | Available, Alarmed, Disabled, Broken | Player pull, drill, sabotage, failure | Alarmed creates mass evacuation; disabled/broken states create security and Facilities concern. |
| 39 | Fire extinguisher | Available, Empty, InUse, Missing | Fire response, spray, theft | Successful use is reassuring; empty/missing equipment raises safety stress and may trigger reports. |
| 40 | Mop bucket | Available, InUse, Empty, Missing | Cleaning, spill response, theft | Cleaning lowers environmental stress; missing equipment frustrates Facilities and leaves evidence in place. |
| 41 | Wet-floor sign | Clean, Wet, Missing | Mop activity, spill, sign removal | Wet with sign encourages caution; missing sign increases slip risk and security/Facilities concern. |
| 42 | Plant | Healthy, Uncomfortable, Missing, InUse | Watering, neglect, theft, decorating | Healthy plants provide a small comfort signal; missing or unhealthy plants upset plant-focused staff. |
| 43 | Wall picture | Available, Missing, Blocked | Dressing, theft, moved furniture | Mostly atmospheric; missing art creates mild complaints and can reveal moved-object evidence. |
| 44 | Wall clock | Working, Offline, Broken | Normal timekeeping, battery loss, sabotage | A working clock supports schedule focus; an incorrect/offline clock creates lateness anxiety. |
| 45 | Noticeboard | Available, Missing, InUse, Hacked | Announcements, forged notice, removal | Legitimate use improves coordination; hacked notices create suspicion and reports. |
| 46 | Water bottle | Full, Empty, Missing, Spilled | Drinking, theft, knock-over | Drinking restores comfort; spill creates investigation/slip risk; missing bottles create personal frustration. |
| 47 | Lunch container | Full, Empty, Missing, Spilled | Lunch, theft, dropped meal | Eating improves comfort; missing lunch creates strong owner frustration; spill causes smell/flee behavior. |
| 48 | Coffee mug | Full, Empty, Missing, Spilled, Broken | Drinking, theft, throw, drop | Drinking improves comfort; missing mug irritates owners; broken/spilled mug creates noise and investigation. |
| 49 | Stapler | Available, Empty, Missing, Broken | Desk work, theft, jam | Normal use supports work; missing/broken tools frustrate conscientious staff and may prompt complaints. |
| 50 | Paper stack | Full, Empty, Missing, Spilled | Printing, scattering, theft | Empty paper triggers printer failure; scattered paper creates investigation; a full stack supports productive work. |

## NPC Stat Effects

Object reactions use the NPC stat sheet rather than object-specific magic thresholds.
The important authored dimensions are:

- `ActivationSensitivity`: how strongly an NPC notices a stimulus.
- `Focus`: reduces distraction while working and improves response to operational changes.
- `Patience`: raises the activation threshold for minor annoyances.
- `StressResilience`: controls how quickly stress becomes visible and how well an NPC recovers.
- `ComfortNeed`: scales the value of coffee, water, food, seating, plants, and successful support.
- `SocialNeed`: makes phones, water coolers, reception, and meetings more attractive.
- Department affinities: IT, Facilities, Security, and Operations/HR/Reception.

Affinity is intentionally a multiplier, not a hard-coded personality exception. An
employee who dislikes IT receives more activation from computer glitches and an
`ITCalled` result tends toward `Complain`; an IT-friendly employee tends toward
`SeekHelp`. The same object can therefore create different reactions in different
staff members.

## Keycard and Access Model

Doors, readers, elevators, stairwells, server terminals, lockers, first-aid
cabinets, and the incinerator can require cards. The starter registry includes:

- `janitorial` for the incinerator.
- `gary-level-3` for the server terminal.
- `department-level-1` for the starter door.

The runtime API is `GameMode.TryAccessOfficeObject`. A valid card transitions the
object to `Unlocked`; a failed attempt publishes an `AccessDenied` stimulus with
the object's department and location. Stealing, gaslighting, charming, seducing,
and impersonating remain acquisition methods in the workshop schema and can be
mapped to these object IDs without changing the object behavior.

## Visual Replacement Contract

Use the reference images as the blockout target: bright ceiling panels, blue-gray
flooring, white partitions, warm wood desktops, dark filing cabinets, large green
plants, framed wall art, visible clocks, clear exit signage, and a wide central
walkway. Replace each placeholder mesh one-for-one while preserving its authored
footprint, collision flag, object type, object ID, and gameplay position.
