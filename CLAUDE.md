# Catch If You Can

A first-person paranormal-investigation horror game. Unity 6000.5.10f1, URP
17.5.0, Forward+, IL2CPP. Mobile is the primary target (iOS and Android);
desktop is a development convenience, not the platform being designed for.

## This project uses a multi-agent ownership model

Work is done by **specialist roles**, coordinated by a **Main Agent**. Before
implementing any non-trivial task:

1. Read `Docs/AGENT_OWNERSHIP.md` — who owns what.
2. Classify the task with `Docs/AGENT_TASK_ROUTER.md` §2.
3. Pick the **primary specialist**. If you cannot name one, it is two tasks.
4. Identify secondary specialists and reviewers (router §4).
5. Check the protected hotspots (`AGENT_OWNERSHIP.md` §4) and **state the
   invariant you are preserving**.
6. Use that specialist's DEV lab.
7. Run the validators the roles name.
8. Integrate through the Main Agent, in a stated order — never two writers on
   one hotspot.
9. QA before commit.

Forty roles are defined in `Docs/AGENT_ROSTER.json` (machine-readable) and
summarised in `AGENT_OWNERSHIP.md` §5b. `Scripts/check_agent_architecture.sh`
fails if the two drift apart.

**These are development roles, not runtime objects.** There is no
`AgentManager`, no `AgentService`, no agent `GameObject`, and there must not be
— the guard checks. Nothing under `Assets/` reads the roster.

**Roles 35 (Netcode) and 36 (Online Services) are BLOCKED**: every Unity package
host and `docs.unity3d.com` return a 403 policy denial here and no Unity Editor
is available, so no package version or API signature can be verified. Those two
domains stop rather than guess. Every other domain continues.

## Read these before changing anything

| Document | When it applies |
|---|---|
| `Docs/PROJECT_OVERVIEW.md` | **First, if you are new to this repository or handing it to somebody who is.** What the game is, how the code is arranged, which systems exist, what is deliberately not built yet, and how to get it running. Written to be read by somebody with no history here. |
| `Docs/AGENT_OWNERSHIP.md` | Always, if anyone else is working in this repo at the same time. Who owns what, and the files no two agents may touch at once. |
| `Docs/DETERMINISM.md` | **Normative.** Any change under `Scripts/Procedural/**` or to the deterministic set. A violation is a bug even if it looks right in the editor. |
| `Docs/DEVELOPMENT_LABS.md` | Working on, or in, one of the nine `DEV_` lab scenes. |
| `Docs/UNITY_VALIDATION.md` | Claiming that something works. |
| `Docs/NETWORKING.md` | Anything multiplayer. No netcode package is installed yet. |
| `Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md` | **Normative for the boundaries.** Who owns what, and why the pose is never replicated. |
| `Docs/GHOST_EVIDENCE_AUTHORITY.md` | **Normative.** Any change to what counts as evidence, or to who decides it. |
| `Docs/ROOM_FURNISHING.md` | **Normativ für die Einrichtung.** Wer entscheidet, was in einem Raum steht, in welcher Reihenfolge, und was auf keinen Fall zugestellt werden darf. |
| `Docs/HQ_MODULAR_MIGRATION.md` | **Normative for the environment migration.** The measured contract of the imported house pack: what it actually is, its openings, its materials, the UV formula for generated geometry, and why the logical cell does not move. |
| `Docs/PURCHASED_PORTAL_PACK.md` | **Normative for the seam.** Adopting a bought portal asset. What crosses from HDRP to URP and what cannot, why the artwork may lend the portal its look and never its shape, and why the images are copied rather than referenced. |
| `Docs/CROSSPLAY_PLATFORM_MATRIX.md` | Adding a platform, or anything tempted to branch on one. |
| `Docs/AGENT_OWNERSHIP.md` + `Docs/AGENT_ROSTER.json` | **Always.** Who owns what, the 40 specialist roles, and the 19 protected hotspots. |
| `Docs/AGENT_TASK_ROUTER.md` | **Normative for how work is assigned.** Routing, the handoff contract, the review matrix, the blocked-domain rule. |
| `Docs/PLATFORM_QUALITY_TIERS.md` | Anything that would differ between PC, console and mobile. Gameplay never differs; presentation may. |
| `Docs/EDITOR_WORKFLOWS.md` | **Normativ für die Menüstruktur.** Bevor ein Editor-Werkzeug hinzukommt oder ein Menüpunkt verschoben wird. Welcher Befehl was ändert, und welche man ohne Rückfrage klicken darf. |
| `Docs/PERFORMANCE_BUDGETS.md` | Before writing any performance number. Nothing is MEASURED yet, and saying it is fabricates evidence. |

**The house is built in two stages, and only the first one is deterministic.** Stage A
decides the layout — how many rooms, of what category, where, which walls carry doors — from
the mission seed alone, in an engine-free assembly, and folds the result into the layout hash.
Stage B reads that layout and places geometry: `ModularRoomBuilder` assembles floors, walls,
doorways, windows and trim from a `ModularInteriorCatalog`. Changing the art changes Stage B
only, so the same seed keeps producing the same house. Where Stage B still has a choice —
which of three interchangeable wall meshes this wall gets — it is *derived* by hashing the
room's identity, never drawn from a `CiycRandom` stream: a draw would advance that stream and
reach back into generation.

**Two session modes, chosen and never inferred.** Offline solo is exactly one
local player with no dependency on anything outside the device — no
Authentication, Lobby, Relay, transport or account, and the whole mission loop
works in airplane mode. Online is 1–8 players: one host plus up to seven clients,
the host occupying one of the eight. `MultiplayerProtocol.MaxPlayers` is the only
place that number lives; everything else derives it.

## The rules that are enforced, not trusted

- `Scripts/check_determinism.sh` — the deterministic set stays pure and the
  layout hash stays stable. 148 checks, all must pass.
- `Scripts/check_dev_scenes.sh` — no `DEV_` scene is ever enabled in the build
  list.
- `Scripts/check_equipment_catalog.sh` — the eleven items keep their runtime
  paths, the journal cannot prove evidence, nothing sweeps the scene per
  frame, no equipment id is looked up by string literal, no held item hides
  `HeldEquipmentBase`'s per-frame methods, and the flashlight's model and material
  paths resolve to real files, an item is not given its visual before it knows what it is,
  a failed model load never becomes a silent placeholder, and the flashlight's diagnostic
  pose ships switched off, a model's size is measured in its own space rather than as a
  world AABB, and the achieved size is checked against the wanted one, and every model
  and material path an item names — in the factory AND in the authored profile —
  resolves to a file that exists, because a path that resolves nowhere is silent: the
  load returns null, the honest capsule stands in, and a typo is indistinguishable from
  art nobody has made yet, and a CLONE adopts the visual it already carries instead of
  building a second on top of it - every item reaches the world as a clone, and
  Instantiate copies the GameObject while dropping the auto-property that pointed at it,
  which is the one state the build guard reads as "nothing built yet", and the TORCH
  slot sits outside the three-slot array while the selected index is validated against
  four, so indexing the array with it threw on every torch selection - after the assign
  and after the equip, so the exception escaped through AddItem and abandoned the rest
  of the pickup, and the grid projector goes on the FLOOR as well as a wall, and on the floor it LIES
  DOWN. It was wall-only for a reason that was true then and is not now: the field was a
  70-degree cone along +Y, so a projector standing on a floor pointed its lens at the
  ceiling - the field is a SPHERE, so which way it points no longer changes what is lit,
  and a rule that outlives its reason is just a rule. What it must not do is STAND: the
  same quarter turn the wall case uses maps +Y onto the floor rotation's forward, which
  the placement query builds from the player's own facing flattened into the floor, so it
  lies pointing away from whoever set it down. And it lies ON the floor rather than half
  in it - the placement puts the PIVOT on the contact point, which is right on a base and
  wrong on a side, so the lift is MEASURED off what is drawn (the projection volume left
  out, it having lifted this device through a ceiling once already) instead of written
  down. All of it declared on the device rather than in an Inspector value somebody must
  set per instance, with the placement query actually reading it; its display name is "DOTS Projector" while the id
  stays spectral_grid, because the id reaches the evidence contract and the name does
  not; X is bound to Interact in BOTH keyboard paths so it picks up, commits a wall
  placement and takes a mounted device back off, G raises a power signal a deployed
  device reads, and the press is spent ONCE - while a preview is up X belongs to the
  placement, because the thing the interaction ray finds then is the wall the preview
  stands on, which would place the device and pick it straight back up in the same
  frame. The router reads no key of its own; MobileInputController stays the only thing
  in gameplay code that does. The AIM step is GONE: it was reachable only from a HUD
  button this screen does not build, behind an F key whose signal nothing in the project
  reads - a projector in the hand now aims by itself, driven from the lifecycle so
  holstering, dropping and placing all end it through transitions that already happen. X
  is consumed ONLY on a valid candidate, so carrying one does not swallow every press;
  and G belongs to ONE device - the torch offers the press to a deployed device first
  and skips itself only when that device took it. A debug convenience may not share a
  key with a real control: the dev take/drop sat on X, so one press ran both paths and
  threw the projector on the floor. And the dots are REAL GEOMETRY, after three
  versions that were not. The shader reconstructed the world position of the surface behind each
  pixel from the scene depth texture, which is a correct technique, and the file compiled - the
  diagnostic ladder proved both: its magenta rung drew over the whole volume and its screen-UV
  rung drew a correct gradient. The rung below them read the RAW value out of `SampleSceneDepth`
  with no interpretation on top and came back flat blue, which in that rung means exactly 0.0 at
  every pixel. No depth texture reaches that pass in this project, on this platform, in this Unity
  version, and the pipeline asset asking for one globally does not change it. A technique that
  needs a resource nobody can hand it is the wrong technique here however right it is in the
  abstract, so it is GONE rather than debugged a fourth time. What replaces it: rays go out of the
  lens in every direction and one small quad is laid flat on each surface they hit, all of them in
  ONE mesh on ONE renderer with ONE material. The shader reads nothing from the frame buffer - no
  depth, no screen position, no inverse view-projection - so there is nothing left that can be
  unbound, and a check keeps it that way. The covering is a SPHERE BY CONSTRUCTION rather than by
  a shader working: a Fibonacci sphere is a uniform covering of all 4-pi steradians with no pole
  clustering and no wrap seam, so floor, ceiling, both side walls, in front and behind all get the
  same density, and the device's rotation only turns the pattern - no axis of it can remove a
  hemisphere (mistakes 34 and 39). The dot is a distance from the quad's own centre rather than a
  texture, because three importer defaults turned the last dot artwork into pills and a
  computation has no sampler to be deformed by (mistake 35). The pass is purely additive, so a
  pixel that is not on a dot outputs black and changes nothing, which keeps a dark room dark
  instead of washing it green (mistake 40). The cost is proportional to how much the device MOVES
  rather than to how many pixels it covers: the rays are cast at switch-on and then only past a
  movement, turn and time threshold, so a deployed projector - the case this device is for - casts
  once and then costs one draw call; carried, it casts a fifth as many. Nothing allocates per
  rebuild (the UVs and indices are written once as the arrays grow), the unused tail of the mesh is
  folded onto the lens rather than left holding last round's quads, and the bounds are set from the
  known range rather than recalculated over every vertex. Switch-on reports the DOT COUNT first,
  because that one number separates the two failures this device has spent its life confusing: a
  field that was never cast (zero dots - there is no geometry around the projector) and a field
  that was cast and is not drawn (thousands of dots and a black screen). The effect volume is
  marked with a COMPONENT so the lobby measures the device rather than the field (mistake 42); a
  clone adopts the head, the projection and the rig it already carries rather than building a
  second set beside them (mistakes 27, 30, 46); and the one surviving diagnostic - every dot quad
  magenta - ships at 0 in the shader AND in the C#, because a diagnostic that runs while somebody
  plays does not diagnose, it creates (mistake 23). And the rig stands at WORLD identity, re-asserted every frame: the quads are built in world coordinates and the rig hangs under the lens, so a LOCAL identity there means "wear the lens pose" and every dot would take the lens transform a second time - a projector two metres out, laid on its side, painting its dots four metres away and twice rotated. And the device's own LENS shows its power
  state: the model has carried an emission map since it arrived - black everywhere but the lens, so
  the SHAPE of the glow was painted long ago - and the material was missing the `_EMISSION`
  keyword, without which URP/Lit does not evaluate emission at all. An assigned map and a missing
  keyword look exactly like a model nobody painted a lens on, which is why it read as dark plastic
  rather than as a bug. The mask is what keeps the casing dark: one colour is pushed at the whole
  renderer, and a colour multiplied by a black mask is still black, so the glow cannot spread to
  the body however high the number goes. Off stays visible as green glass and under the project's
  own Bloom threshold (0.8, already in the menu volume - nothing about Bloom was changed); on
  clears it in the GREEN channel only, because a multiplier that lifts all three blooms white. It
  rides the SAME signal as the projection - `running` already folds in the battery, the switch and
  the lifecycle, so a second state variable for the glow could only disagree, and the disagreement
  would read as a device that is off and still lit. A property block rather than
  `renderer.material`, which would clone one material per projector on first touch, written only
  when the state actually changes, and the dot field is left out of it by its EffectVolume mark.
  102 checks.
- `Scripts/check_multiplayer_architecture.sh` — the deterministic assembly stays
  engine-free, gameplay never reaches a Relay API, remote players never read
  local input, ghost decisions stay host-only, online capacity has exactly one
  source, only the launcher installs a session, nothing starts one at boot, a
  client is told the ghost's state and never its reasoning, and a diagnostic
  never reports a latency it did not measure. 50 checks.

- `Scripts/check_ui_and_portal.sh` — the menu palette stays black, white and grey with the
  green as an accent, `fadeDuration` stays zero so a button answers a touch on the same
  frame, the branded fonts resolve to files that exist, only `MenuInputGate` suspends the
  player's input and HUD, START INVESTIGATION opens the lobby doorway instead of loading a
  scene, and the lobby scene actually carries a portal. It also holds the vertical slice
  together: exactly one `MobileInputController`, one movement joystick and one transition
  overlay; the portal shows the real mission world rather than a stand-in; the world loader
  never rolls a seed of its own; a prepared world is scenery until it is entered; and the
  mission actually puts its loadout in the player's hands; the torch has a dedicated place
  outside the three investigation slots, a mission cannot be generated twice, finishing one
  returns to the lobby without replaying the cinematic, and the portal camera can never
  become the player's. It also holds the production portal to its architecture: the energy mask is a
  torn oval breach built from a normalised radial field whose edge is chewed away by noise, a
  closed portal draws no lit pixel at all so the wall reads as whole, two independent noise
  layers drive it, every
  artistic control is a shader property that the material and the C# agree on, the portal camera
  stays culled and distance-gated, nothing allocates a buffer or a material per frame, quality comes
  from the project's own quality level rather than a parallel tier system, particles emit on the breach
  edge and are given both a sprite and a transparent additive surface — a URP particle
  material left at the shader's defaults is an OPAQUE billboard on the default white
  texture, which is a solid square, there is exactly one shadowless light, and a failed preparation visibly collapses the
  portal instead of hiding it. It also keeps entry the player's: the portal never loads a scene
  itself, entry commits from exactly one call site behind a plane-side crossing whose sign must
  actually change and whose crossing point is inside the aperture, no trigger volume can commit
  entry, a crossing needs a prepared destination, a refused crossing returns the controls, the
  intro overlay is cleared whatever happens, and the first-person hand target, elbow hint and
  anchor agree on one side with the fist clear of the near clip plane, and the probe room
  stays outside the playable world without ever making the portal enterable, and the lobby's
  north wall is one solid object with no door frame and no runtime patch behind it, no wall
  in the room carries a skirting board any more, and the east window wall keeps its pieces. It also holds the
  portal camera to the technique it implements: the far room is sampled in screen space rather
  than by the quad's own UV, the buffer is rendered at the shape it is sampled at, the
  projection is reset before it is made oblique, and the oblique near plane's side is derived
  from where the camera actually is instead of assumed - assumed, it clips the whole room away
  whenever the destination Transform faces the other way, which is a black interior behind a
  lit rim. The camera is gated by its component rather than its GameObject and is enabled only
  once the pose and the clip plane are written; the orientation convention is validated rather
  than compensated for; the view refreshes on a cadence the quality level drives; the edge
  distortion is capped at 1.5% of the screen and falls to zero at the centre; no per-platform
  flip is hand-coded; the debug readout is opt-in; and nothing renders recursively. It also
  holds the one-world contract: there is exactly one portal implementation, no
  `ReferenceApartment` stands in for the mission, the view is aimed at the prepared world the
  player will actually enter, that world is prepared additively behind the lobby and reused
  rather than regenerated, no timer can hand the player over, and a failed preparation has a
  state of its own instead of reporting itself as a doorway nobody asked anything of. And it
  bounds the lobby's cost: the mirror and the portal share one arbiter, both ask it before
  rendering, its budget comes from the project's own quality level, and the view buffer's
  ladder has named ends rather than a halved top. And when it cannot read a file at all it
  ABORTS instead of answering: its stripped-source cache was written with `|| true`, so a lost
  fork left a zero-byte entry behind and every later check on that one file reported the code
  as broken - eight red lines about a portal nobody had touched. A cache entry that could not
  be produced is not a verdict about the project (mistake 26). And it reads the one thing no other
  check in this repository can: that every local in every shader is declared before it is
  used. HLSL does not hoist, so a use one line early is a compile error, and a shader that
  fails to compile is drawn magenta — indistinguishable on screen from mistake 2. It also
  holds the purchased-pack seam: a bought pack may lend the portal its LOOK and never its
  shape, its samplers compile out when unused, ticking the box with no texture assigned
  keeps the procedural portal rather than blacking it out, and the adapter copies the
  pack's artwork into the project instead of referencing a folder that exists on one
  machine. And it keeps the numbers reachable: the style can be edited while the game
  runs, the opening can be resized after it is built (mesh, plane and culling bounds
  together), the drawn quad is larger than the hole so the glow is not cut off square,
  and the portal's far anchor is raised to the portal's own height instead of being the
  floor-level spawn point, and the tear cuts the wall's COLLISION so the player can walk
  through it — physics only, so the wall stays one mesh with no z-fighting patch, and it
  is solid again the moment the portal is not open. And it holds the opening to ONE source:
  `openingSize` alone sizes the quad, the shader breach, the collision hole, the crossing
  test and the threshold — no second fraction, no third trigger size, and no orphaned
  field left in the scene, where a serialized value silently beats every code default. It also
  keeps the wall findable: the tear is cut after physics exists rather than in Awake, where a
  query against an unsynced physics scene finds nothing at all, the wall is recognised by
  shape rather than by name, a failed resolve names what it rejected, the drawn quad is
  larger than the hole so the rim is not cut off square, and the portal reports a MEASURED
  passability rather than a claimed one. And it holds the crossing to a HANDOVER rather
  than a load: the seamless path neither fades, reloads, regenerates nor respawns, the
  lobby player is carried through and mapped by the same matrix that posed the camera
  they walked into, no ground under the arrival refuses the crossing instead of dropping
  them, a refusal gives the controls back, and the prepared world raises no curtain — an
  opaque overlay set at preparation time IS the black frame. And it keeps the carried
  player ALIVE: the rig is a scene root, so unloading the lobby destroys it — the move
  to the mission scene happens before the unload, a parented player refuses rather than
  being destroyed silently, the handover counts the cameras that can actually reach
  Display 1, the input gate is released by the handover rather than by a destruction
  order, and no case card is dropped over a room the player already walked into.
  And it keeps the lobby's KIT: the lobby is the preparation area, so what the player
  picked up walks through with them — the mission installs a starter loadout only where
  there was no lobby to take one from, the hand anchor that holstered items hang from is
  wired on every route, and what they walked in with is reported, because an empty bag
  and a lost bag look identical in a dark house. And it lets THROWN things through the
  same doorway: a dropped item already flies, but it landed on the lobby floor behind
  the picture and died with the scene — the same object changes scene now, never
  rebuilt, with its velocity and spin rotated through the pair rather than zeroed, on a
  sign change inside the opening rather than an overlap, once rather than every physics
  step, and only when something declared it transferable. And it points the doorway at a
  ROOM: the portal's far side is a mission entry anchor measured against real floor
  collision inside the entrance room, not the van's spawn — that stands outdoors on
  ground this project does not have, which is a fall past a skybox. One transform feeds
  the preview camera and the crossing, so what is shown and what is walked into cannot
  diverge, and no floor under the entrance leaves it null, which refuses. And it holds
  both floor tests to the RIGHT WORLD: a physics query is global across every loaded
  scene, and while a portal is open the lobby is loaded too — complete with a 40x40 m
  safety floor that sits under very nearly anywhere, so a scene-blind probe validates
  an arrival against a surface that is unloaded seconds later and the player lands and
  then falls. And it keeps the world the doorway opens ONTO in the mission's own scene:
  `new GameObject` lands in the ACTIVE scene, which during a preparation is the lobby, so the
  generated house hung off a lobby object — every room failed the mission's own "is this floor
  mine" test, the entry anchor stayed null, and a null destination reads as a failed preparation
  and collapses the doorway; had it opened, the lobby unload would have taken the house with it
  and the player would have arrived and then fallen. The root is moved into the generator's own
  scene, re-checked when a layout is built rather than only in `Awake` — which runs INSIDE
  `AddComponent`, one line above the `SetParent` that decides the scene — every runtime manager
  is parented before its component is added, and a rejected floor is named together with the
  scene that owns it, because "no floor" covered both an empty ray and a house in the wrong
  scene and those need different fixes. And it keeps the doorway PASSABLE now that the wall
  is made of modules: the portal switches off EVERY collider standing in its opening rather
  than the one it picked as "the wall" — three modules stand there, `_wallSolid` can only be
  one of them, and the others stayed solid, so the door was open to the eye and shut to the
  body. Collected once when the aperture is built rather than per frame, and a floor under
  the threshold is left alone, because switching that off drops the player through the world
  on the way in. And it holds the small room BEHIND the doorway to being one room of a house
  rather than a special case: the test room is a `GeneratedHouse` with a single room, so the
  entry anchor, the lighting, the NavMesh, the content report and the seamless handover all
  run unchanged — a second path past that chain is the one that breaks, because it is the one
  that runs less often. It is parented into the MISSION's scene rather than left in the active
  one (mistake 17), stands far from the lobby because both scenes are loaded during a
  preparation and physics is global across them, touches neither a generation stream nor the
  layout hash, and its way back is the route that already exists — `PendingEntryMode =
  DirectLobby` plus `LoadMainMenu`, on E rather than on a trigger, once rather than per press,
  with the intent withdrawn when there is no loader so it cannot skip an intro nobody asked to
  skip. And it keeps the doorway's WALL a decision and its numbers a measurement: which wall a
  portal is cut into is a choice about the room and is made by selecting the pieces, while
  where that wall stands, how thick it is and which way it faces is measured — the inside is
  found from the player's spawn, because a normal has two directions and the wrong one turns
  the portal outward, and a slab whose thinnest axis is its height is refused as the floor it
  is. And it keeps the investigation board a FUNCTION rather than a model: a board that
  already hangs on the wall gets neither placeholder timber nor a second prefab, its
  interaction body is measured around what is actually there and converted back into its own
  space (a world size in a BoxCollider is scaled twice), a model that brings its own collider
  gets no second one, and nothing can end up carrying two boards — two would be two ways into
  the same panel. And it lets a wall made of MODULES be a wall: the width test moved from each
  collider to their union, because every module of the purchased pack is 2.5 to 4 m wide against
  a 4.70 m opening and three side by side do not add up to one - so the shape search found
  nothing, and the refusal it wrote sat ABOVE the line that collects what stands in the doorway,
  so nothing was collected either and every wall collider stayed solid while the tear was
  visibly open. The doorway is cleared first and refused after, the aperture is measured across
  all the parts rather than one, the assembled wall must still span the opening, and the probe
  tool measures the same way the portal decides. And it holds the doorway to showing ONE room:
  the magenta probe room is a diagnostic and is off by default, because switched on it IS the
  bug it diagnoses - a purple box shown through the opening while the world prepares, replaced by
  a different room the moment it is ready, which is the only "purple transition" and the only
  visible room swap in the game. It also holds the lobby's own atmosphere: the room gets warm
  practicals over the places the player actually stands with exactly one of them casting shadows,
  a `Lobby_DustParticles` system that is bounded and has no collision or trails, a dust material
  that is configured rather than left at URP's opaque white-square defaults, and no built-in
  shader fallback. The equipment table reads the existing definitions and builds through the
  existing call rather than carrying a second list, and measures the table instead of assuming a
  height. A door is dragged by a clamped angle composed from its rest rotation rather than an
  accumulated Euler, turns about its hinge, and tells the swinging door where the leaf ended up
  so the next keypress does not snap from a stale angle. And nothing added for any of it
  references `MobileInputController`, while the debug conveniences are fenced out of a shipping
  build entirely. And it holds the lobby kit to a MEASURED spot: a fixed step to the
  side put eleven items inside a wall, because a downward ray finds the floor perfectly
  well from inside one — so a spot is now tried, proven clear with a box that stands ON
  the floor rather than in it, and a wired anchor overrides the search outright. And one
  named item is laid at the player's FEET, because the kit going wherever there is clear
  floor is correct and is not the same as findable — a 0.22 m projector among ten others
  off to one side reads as one that never spawned — through the same build call, and
  instead of its place on the grid rather than as well as it, and BEFORE the grid,
  because last is the one position where the item whose absence is the question can be
  missing for an unrelated reason — and one item that throws costs that item rather than
  the ten behind it, which is the damage half of mistake 27, recorded once and then not
  fixed. And a placed item has something the interact ray can HIT: the only collider a
  fresh item carries is the drop capsule, which is created switched off, while the
  summary beside it says the pickup trigger "is a separate collider and stays on" - a
  sentence describing an object nobody builds, so the ray went through every item on the
  floor. Measured around what is there and converted back into the item's own space, a
  trigger rather than a wall, and never a second one - measured off what is DRAWN rather
  than off the drop capsule, whose bounds Unity does not keep updated while it is
  disabled: what can be seen and what can be hit have to be the same box. And a refused
  pickup NAMES its reason, because the controller drops a refused target, so no prompt,
  no outline and no name is what all three refusals and an empty ray look like alike - a
  diagnostic that describes and never decides - and the body it aims at is built where
  the VISUAL is built rather than on the last line of whoever places the item, because
  anything that threw in front of that line left a positioned, visible object carrying
  no collider and no pickup component at all, which is what "the model is there and
  looking at it does nothing" was; every runtime item is a pickup by construction now.
  And it does not LIE about the project, in either direction: this file runs under
  `pipefail` and asks its questions as `printf | grep <quiet>`, and a quiet grep stops
  reading the moment it matches - the pipe closes under printf, printf ends with "write
  error: Broken pipe", and pipefail then reports a FAILED pipeline for a pattern that was
  FOUND. Whether it happens is a race between how fast printf writes and how early the
  match is, which is why one commit passed here and failed on the runner. It lies both
  ways, and the second is the worse one: plain, a found pattern reads as absent (false
  RED); negated, a found pattern reads as absent and the check PASSES (false GREEN), and
  nineteen checks in this file had that shape. So the idiom is banned rather than tuned -
  every grep here reads to the end of its input - and the ban is itself a check, over every
  guard that sets pipefail rather than only this one.
  And it holds the cinematic menu to being
  AUTHORED rather than built. The menu went through both wrong answers before this one: an editor
  tool, so the rows existed only after somebody clicked and on no other machine (mistake 47), then
  a runtime builder, so they always existed and nobody could edit them. Both are wrong for a
  screen somebody wants to DESIGN. It is now real, saved GameObjects in `01_MainMenu.unity` -
  `MainMenuRoot/Navigation/Button_Play/Label_Play` and its two siblings, each row carrying its own
  `SelectionBrush` - written once by `MainMenuAuthoringTool`. The guard holds the line between the
  two owners: the navigation writes no caption text, touches no `anchoredPosition`, `sizeDelta`,
  `localScale`, `font` or `fontSize`, and creates no GameObject at all, so everything typed or
  dragged in the Inspector survives Play Mode; the brush is SWITCHED rather than built; the tool
  writes a caption only when it CREATES the label, so a second run cannot overwrite what somebody
  retyped; it finds inactive objects (the panels and the retired tap label are exactly that sort);
  it reuses the scene's canvas and EventSystem rather than adding a second of either; and a new
  EventSystem is moved into THIS scene (mistake 17). Three rows - PLAY, SETTINGS, CREDITS - in that
  order, because the navigation activates by INDEX, and no QUIT. PLAY still hands over through
  `MainMenuModeController.EnterLobby`; the older `MainMenuController.OnPlay` reaching for
  `SceneLoader.LoadInvestigation` is refused by name, because that component is not in this scene
  and neither is the `UIManager` it talks to (mistake 1). TAP ANYWHERE TO START goes away WITH its
  input path, because `MainMenuTapToStart` reads raw Input from anywhere and hiding only the label
  would leave one click on SETTINGS also starting the lobby (mistake 32). And a menu that was never
  authored is NEVER SILENT: the runtime check names the exact command that writes it, because a
  screen that simply stays as it was, with nothing anywhere saying why, is the whole of mistake 47.
  Both sprites import as Sprite with transparency preserved, and the grunge backing is MEASURED
  rather than claimed - every pixel of its outermost row fully transparent, nowhere fully opaque -
  because one opaque border pixel is, stretched, exactly the hard rectangle the texture exists to
  avoid, and the first attempt failed precisely that way at 98.4% covered. One of these checks was
  itself written wrong and caught by its own tooth test: it grepped the file for `AuthoringCommand`
  and stayed green when the constant was renamed away, because the NAME still appeared twice as a
  use - mistake 8, inside a check written to catch mistake 47. It reads the declaration now.
  And NO file names a TMPro type without the `TMP_PRESENT` guard, because an unguarded one does
  not break its own screen - it fails ASSEMBLY-CSHARP, and with it every gameplay script in the
  project (mistake 48). And the menu is built by ONE builder with TWO callers: the editor command
  writes it into the scene so it can be dragged and retyped, and `MainMenuModeController` builds
  it on Start so the screen is not silently the old one wherever nobody clicked - mistake 47,
  which this project paid for twice before the two halves were allowed to coexist. What makes
  them compatible is that the builder ADOPTS: every object is looked up before it is created and
  an existing one keeps its rectangle, its text and its font, so a scene that carries the authored
  menu is left completely alone. The guard holds all three parts - the navigation creates nothing,
  the tool keeps no copy of the construction, and the adopt path returns what it found untouched.
  And the scene objects appear WITHOUT a click: `MainMenuSceneAuthoring` writes them the moment
  `01_MainMenu` is open for editing and the menu is missing, because a step a person has to
  remember is a step that does not happen - the menu command existed for three rounds and was
  never clicked. That makes it code which works on the user's scene unasked, so it is held to
  harder rules than a menu item and each one is checked: it never saves (only marks dirty), it
  stays out of Play and out of compiling, it builds only when the menu is genuinely absent, and
  it touches no scene but that one. 327 checks.

- `Scripts/check_editor_menu.sh` — the editor menu stays legible, and the purchased architecture has ONE scale. The game scale is the measured ratio 2.95 / 3.92 in one place, with no tool carrying its own copy; the decision is made on effective world scale rather than `localScale`, because a vendor piece at localScale 1 inside a corrected wrapper IS already corrected and its own field says otherwise; an already-corrected ancestor is recognised and a second application is a named verdict rather than a silent pass; architecture is told from props by FOLDER, since a filename classifier caught 3 of 105 in a pack that numbers its prefabs and calls its glass Steklo; an undecidable piece is reported ambiguous rather than guessed, because a chair may already be at real-world size and shrinking one that was right is invisible; the portal is excluded, its opening being a gameplay dimension; the migration audits before it can apply and converts only original-size pieces; and the correction goes on a CIYC wrapper with nothing applied back to the purchased package. Also the menu itself: Fifty-one commands sit in
  seven named groups with none hiding in another root menu, every one carries a risk tag saying
  what it changes, everything under Safe Inspection is tagged read-only, the pack classifier
  that found 3 of 105 prefabs can no longer write the catalog (one writer, the verified one),
  removing the test room finds an INACTIVE one - `GameObject.Find` skips those, so a switched-off
  room survived and the next Build put a second beside it - and can be undone, the room is found
  through the whole scene rather than across its roots so tidying the hierarchy cannot hide it,
  no authoring command saves the open scene behind the user, and the four commands that can
  rewrite the project all go through ONE confirmation that states scope, count, reimport, saving
  and the way out. 30 checks.

- `Scripts/check_room_furnishing.sh` — die Räume werden nach Funktion eingerichtet, und nichts
  davon greift in die Generierung zurück. Der Würfel-Ersatz für ein fehlendes Möbelstück ist weg
  und kann nicht zurück; die Einrichtung zieht keinen `CiycRandom`-Strom, fragt die Physikszene
  nicht und hängt an keiner Instanz-ID; die Sperrzonen — Türschwenkbereich mit der echten
  Blattdicke, Laufwege in der Breite des Spieler-Colliders, Fenster als Höhengrenze statt als
  Verbot — stehen fest, bevor das erste Möbel gesetzt wird, und werden am Ende gegen das
  Ergebnis nachgemessen; Möbelmaße stehen in keinem Asset, sondern werden im eigenen Raum des
  Modells gemessen und je Sorte einmal gecacht; gesetzt wird über die gemessene Mitte statt über
  einen Pivot, der in diesem Paket 40 m danebenliegt; Deko liegt auf einer geprüften Fläche, die
  sie tragen kann, und trägt weder Rigidbody noch aktiven Collider; ein Licht entsteht nur an
  einer wirklich platzierten Leuchte, aus dem Budget und ohne Echtzeitschatten; eingerichtet wird
  vor der Lichtschaltersuche und vor dem NavMesh; eine Raumart ohne Profil wird gemeldet statt
  umgewidmet; und der Inhaltsbericht meldet ein Unity-Primitivmesh unabhängig von seiner Größe —
  der Würfel war 1 m groß und wäre unter jeder Größenschwelle durchgerutscht. 28 checks.

- `Scripts/check_agent_architecture.sh` — the roster holds 40 unique roles with
  every field, the roster and `AGENT_OWNERSHIP.md` name the same roles, the
  hotspot policy and table survive, the handoff contract still demands preserved
  invariants, the blocked-domain rule still forbids fake netcode, no agent
  runtime object exists in the game, and `MaxPlayers` is still 8 in its one
  source. 13 checks.

- `Scripts/check_vertical_slice.sh` — the Suburban House stays solvable: its entity roster is
  named, every entity in it leaves evidence the four-tool kit can actually find, no two share
  an evidence signature, and the mission still recommends those four tools. It also keeps the
  identification a decision: selecting an entity is not an answer, the confirm step goes
  through `MissionManager.SubmitIdentification`, a second answer is refused, and the
  identification bonus is paid for being right rather than for turning up. It also keeps
  the world generation switchable without being removable: `generateWorld` can skip it
  entirely for looking at the character and the equipment, but the calls stay and an
  empty floor stands in, so switching it back on restores the same run from the same seed. It also keeps the
  house lighting out of generation: the director derives from the seed locally rather than
  from a `CiycRandom` stream, and it runs on entry rather than while the world is only being
  previewed through the portal, and the tool that writes ghost prefabs writes them where the
  runtime looks. It also keeps things that were built from being invisible: every code-built
  item carries a visual profile, the flashlight points at its finished model, the portal never
  ends up with a renderer and no material, the HUD's own panels stay overlays rather than
  opaque sheets over the game, and every volume slider reaches the mixer. It also keeps the
  player honest: the crouch camera has one source of truth (a measured depth times the shared
  crouch progress, never the head's current drop), and a rig that cannot animate says so
  instead of standing in a T-pose. It also stops a visual being built before the thing it is a
  visual OF: an item told what it is after `AddComponent` rebuilds, the doorway starts opening
  before its far world is ready, and Nathan's bound textures import at a size the material can
  actually use, and every model a visual profile names really exists under `Resources` with a non-zero forward axis.
  It also tells a room's PRACTICAL from its residual glow: the director dresses and rolls the
  switchable lamp and leaves an `AmbientRoomLight` exactly as it was built, the wall switch owns
  the practical rather than whichever light the hierarchy returns first, and which of the two a
  light is gets decided in one place. And the player's own body follows their own eyes: the head
  and neck pitch the SAME way the camera does (a negated angle turned the neck up when the eyes
  looked down, which only the mirror can show, because nobody can see their own head), the neck's
  asymmetric limits stay asymmetric so the next sign error cannot hide behind them, and the crouch
  camera is gated by the reference it is about to use rather than by a flag Awake cached before
  `PlayerRigBuilder` had assigned it. 61 checks.

- `Scripts/check_project_tags.sh` — every tag and layer the code names really exists.
  Assigning an undefined tag throws and takes the rest of that build down with it; an
  undefined layer name returns -1 and says nothing. `Environment` was assigned by two
  runtime factories, compared by the NavMesh source filter and assigned by the prop
  builder while being defined nowhere, and `LightSwitch` was assigned one statement
  before the switch got its component. Both are checked now, along with the editor
  setup's ability to restore them. 24 checks.

- `Scripts/check_asset_references.sh` — every asset a scene names really arrives on the
  machine that opens it, or is a DECLARED vendor asset. Every git-lfs rule matches a tracked file, every git-lfs file is
  stored as a pointer rather than as pack content, every `.meta` under `Assets` declares a
  guid, no two share one, every tracked asset carries its `.meta`, and every prefab instance
  in a scene or prefab resolves to an asset that exists. It also reads the working copy,
  which is where the failure actually lives: all-pointers is a checkout that never asked for
  LFS content and is fine, all-materialised is fine, and a mix of the two is a partial fetch
  — it names the files that are still pointers, because those are the ones Unity shows in
  red. It reports the LFS payload against GitHub's free 1 GiB allowance. And it tells an
  EXPECTED absence from a broken one: the purchased packs are gitignored, so the guids a
  scene names in them resolve to nothing here, which from inside the repository looks
  exactly like mistake 14. `Scripts/write_vendor_manifest.sh`, run on the machine that has
  the packs, writes down which guids are theirs; the guard then passes an absence the
  manifest names while the pack is missing, and still fails a guid the manifest does not
  name, or one it does name while the pack IS installed. And it asks the OPPOSITE question too:
  a big binary with no LFS rule at all. `.gitattributes` lists its rules one file at a time
  rather than by pattern, so every new model and texture starts outside LFS and stays there
  unless somebody remembers - six props arrived that way in one commit, three FBX files of 87 to
  92 MiB, and the push carried 542 MB. GitHub warns over 50 MiB and REFUSES over 100 MiB, so the
  largest was eight megabytes from being rejected outright, and a pack object is in every clone
  of that history forever. What is already in cannot come out without a rewrite, so it is
  WRITTEN DOWN in `Scripts/lfs_debt.txt` and the check fails on the NEXT one - a baseline rather
  than a silent pass, because a number nobody recorded is a number nobody can tell has moved.
  13 checks.

- `Scripts/check_hq_environment.sh` — the house interior comes from a modular catalog, and
  nothing can quietly put the old one back. No production file names a Kenney content path
  (there is no exception any more - the two ghost meshes that used to be allowed by full
  path are Quaternius monsters now), the folders are really gone, and no
  integration tool still declares the methods that built 130 assets on one click. The
  generator tries the modular builder first, a room it cannot build says so loudly, and the
  primitive stand-in is fenced behind `UNITY_EDITOR || DEVELOPMENT_BUILD` — a shipped house
  of grey boxes looks exactly like one that was never migrated. The builder consumes the
  layout and never produces one, derives its variants from the room's identity instead of
  drawing them from a generation stream, and stays outside the engine-free assembly. The
  catalog names every structural role and no vendor. Nothing fabricates a blocker in a
  doorway: no primitive door returns when the door prefab is missing, a missing one leaves the
  opening clear and says so, and every generated primitive takes its material from one place —
  `GameObject.CreatePrimitive` on its own carries Unity's built-in default material, which is a
  Built-in-pipeline shader and draws magenta under URP. It also protects the project's own art
  from a pack conversion: a "Built-in to URP" pass run over the whole project rewrites
  `m_Shader` on every material it can reach, and the nine CIYC materials driven by custom
  shaders must not be among them — a converted one silently becomes URP/Lit, the dissolve
  stops dissolving, the portal stops being a portal, and nothing errors. And a generated
  primitive that could not be given a material has its RENDERER switched off rather than
  left carrying Unity's built-in default, which is a Built-in-pipeline shader and draws
  magenta under URP — the collider stays, so an invisible floor still holds the player
  up. And it keeps the fallback room from looking like the migration that never happened: the
  stand-in shell is textured from the project's own room materials, reached through the content
  catalog rather than by a path — the catalog is under `Resources`, so what it references ships,
  while a path into `Assets/.../Materials` resolves in the editor and nowhere else — the four
  material fields on that catalog are actually READ, two of them having been declared and used
  by nobody, every generated surface is tiled one texture tile per metre because a cube's UVs run
  0..1 whatever its size and one tile smeared over a 6 m wall reads exactly like no texture at
  all, a room primitive with no material is hidden and reported rather than left carrying Unity's
  magenta built-in default, and every material guid the catalog names resolves to a file that
  exists. And it holds the first HQ room to the seam the migration measured: the pack supplies the
  SURFACE, never the structure — it has zero floor and zero ceiling parts and its walls are not a
  kit, with pivots up to 29 m from their own mesh — so the shell takes wall, floor and ceiling
  materials from the modular catalog, an undrawable material is refused with the four ways in
  named apart (null shader, unsupported shader, Unity's error shader, an HDRP shader in a URP
  project — all four look identical on screen), the measured density goes onto a COPY so the
  purchased material is never edited, the three surfaces are shared across every room rather than
  per room, a density of zero means unknown and leaves the material as authored instead of
  collapsing it to one texel, a vendor insert brings no collider and casts no shadow and is
  switched off rather than destroyed by context, a window wall keeps one collider across its span
  because a window is not a way through, the window opening fits under a 3 m ceiling, the catalog
  no longer demands floor and ceiling modules the pack does not have, and the test-room tool
  builds exactly one 6 x 3 x 6 cell while scanning, importing and refreshing nothing. And it holds
  the texture SIZE, which is three separate ways to warp a wall: a wall's UVs are projected from
  each vertex rather than counted from each face's corner — counted, a wall with an opening is
  four boxes that each restart the pattern at zero, so the wallpaper jumps at every doorway —
  every texture map is rebased by one shared divisor rather than the colour map alone, because a
  detail normal left behind puts the bumps on a pattern that moved, which reads as warped rather
  than as mis-sized; and the density is measured in WORLD metres with the transform included
  (this pack scales a Plane by 1.45) as the MEDIAN of every piece rather than whichever was
  enumerated first (the same material runs 0.10 to 0.55 U/m across the pack), with an
  inconsistent pack reported instead of silently averaged. And it holds the pack to being LOOKED
  AT rather than reasoned about: the folder is located instead of assumed — a wrong path
  classifies zero prefabs and reports a calm zero, which reads as an empty pack — the surface
  density is sampled across the pack rather than across whatever a filename classifier caught (this
  pack numbers its prefabs and names its glass `Steklo`, so three stragglers were classified, one
  of them a 36 x 57 m demo assembly, and the density was measured off those), a measurement that
  cannot be right is refused on aspect and on size rather than written into the catalog — a square
  texture cannot repeat 9 m across and 2 m up — a refusal keeps the material and drops only the
  number, and the pack can be listed: folders, pieces with their size and pivot offset, and every
  material with its tiling. And it holds the first HQ room to what the inventory actually showed:
  the catalog is written from verified asset paths rather than classified filenames, a surface
  material is resolved on the piece that wears it — the pack has three materials called `white`
  and nineteen called `1`, so a project-wide search by name is a coin toss — an ambiguous name is
  refused, the door and window are EXTRACTED from the 4 m wall prefabs that carry them rather than
  instantiated whole (the pack ships neither as an object of its own, and the shell would stand
  through a 3 m ceiling), a match of nothing is switched off and reported instead of leaving that
  shell, which way is up is measured on the instantiated object because the meshes carry their
  height on Z, one density is measured on prefab 5 and floor and ceiling are derived from it by
  texel parity and SAID to be derived, and the doorway is the pack's own 1.25 x 2.60 so the leaf
  drops in unsqueezed while still leaving a lintel, and an insert is positioned by its MESH rather
  than by its pivot — this pack's pivots sit 13 to 40 m from their own geometry, so placing by
  pivot puts the door thirty metres from the doorway — measured on the visible parts only (the
  disabled wall shell is most of the prefab) and from transformed corners so rotation cannot skew
  it. A room can also be built BY HAND through the same builder: `HQRoomAuthoring` describes a
  room and makes no geometry of its own, and the window rule has one home, explicit for a person
  and derived for the generator, so a hand-built room cannot drift from a generated one. And it holds
  the piece browser to LOOKING: it never reimports, never retiles a vendor material, never writes
  an asset and never rescales a purchased piece — a whole multiple of the pack's own median module
  width is a DESIGNED size, not an oversized one (15 and 16 are exactly 2x and 3x to the
  centimetre), the audit runs on a button rather than per repaint, the wrapper corrects the origin
  while the vendor prefab goes in as an untouched instance, and a material with no base map is
  named as such rather than left looking like a lost one — this pack ships textureless FBX
  duplicates beside its textured materials. And it holds a hierarchy tidy-up to being a MOVE:
  reparenting goes through `Undo.SetTransformParent`, world position, rotation and lossyScale are
  re-measured after every move and a drift is REPORTED rather than compensated for by hand — a
  manual correction hides the bad reparent that caused it — an organisational folder sits at the
  origin unrotated and unscaled so it cannot shift what is put into it later, nothing is added,
  removed, switched on or off, or unpacked, anything the audit could not classify is offered
  unticked rather than moved, and the scene is left dirty for the user to save after looking. And
  it holds the lobby's dormancy, which is load-bearing rather than tidy: nothing builds that room
  at runtime — all thirty-odd objects are authored in `01_MainMenu.unity` and
  `MainMenuModeController.interactiveRoomRoots` merely switches the one root on — but a room that
  is ACTIVE when the scene loads has already had its moon light claim the scene's sun and its
  emitters start over the menu, because Unity does not define whether that controller's `Awake`
  runs before or after theirs. So the editing switch turns it off in `sceneSaving` and on again in
  `sceneSaved`, and off on `ExitingEditMode` because entering Play serialises what the editor
  holds rather than what the file says; it touches only the active flag, finds the room by walking
  the scene rather than with `GameObject.Find` (which skips inactive objects, the only kind this
  looks for), and warns when the hand-placed house has been parented under the room, where it
  would vanish with it, and it NAMES the four lobby children that carry a script and no renderer —
  the mirror, armchair, table and board are built in `Start()`, so no switch can reveal geometry
  that does not exist yet and building it here would run gameplay code in the editor. And a plan
  to sort the hierarchy ticks only what was PROVEN: the default follows the verdict rather than
  being set per branch, proven is kept apart from conditional and from unclear, and moving the
  lobby under a folder is offered conditional because its visibility would then also depend on
  that folder staying active. And it holds the authoring previews for those four holders to being
  the SAME builder: `MirrorCorner.Build` took a `withReflection` flag rather than growing an
  editor-side twin, the editor makes no geometry of its own, every preview object is prefixed and
  flagged `HideFlags.DontSave` so it cannot reach the scene file however the scene is saved, the
  switch deletes only its own previews from one name-guarded call site, and they are removed
  before Play — `DontSave` keeps an object out of the file but not out of Play, and a surviving
  preview would stand beside the one the runtime builds. And it holds the white-material diagnosis
  to being a DIAGNOSIS: the doctor edits no material, reassigns no renderer, writes no asset and
  triggers no reimport, it reads the selection rather than the pack, its pack index is built once
  per run, a proposed swap carries the texture name that proves the correspondence — the pack
  names textures `<fbx>_<slot>_AlbedoTransparency`, so a textureless `SHKAF3` is matched to the
  authored `commode1` by `4_SHKAF3_AlbedoTransparency`, and 20 of 30 textureless names line up
  that way — and a textureless material with no twin is left alone, because ten of them have none
  and some trim is painted white on purpose. And it asks WHICH SOURCE first: an object whose
  materials all live inside a model file is a model instance dragged from the FBX, whose embedded
  materials carry no texture in this pack — the finished parts sit beside it in `walls prefabs/`
  with `wallpaper3` and `white` on them — so that is named before any single slot is blamed, and a
  slot name with fewer than three letters proposes nothing, because this pack has wall slots 1-6
  and window materials 1-4 named after a different FBX and matching those to each other offered
  window glass for a door wall. And every room it builds has LIGHT in it: `PrimitiveRoomFactory`
  put a lamp at each room's light socket and this builder never did, so once the rooms came from
  here the lighting director had nothing to direct - its own report said "0 of 0 practicals"
  every session and nobody read it. What was left on screen was the ambient term, which is the
  same value in every corner of every room, so there was no falloff, no side and no shadow: a
  room lit like that does not look dark, it looks dead. It gets a ceiling practical for the
  director to dress and roll, plus a weak warm residual light that is marked as scenery - no
  switch, never rolled off, no real-time shadows, and its flicker attached only after its
  brightness is set, because `CandleFlicker` reads that in `Awake` and `Awake` runs inside
  `AddComponent`. The ceiling rose does not glow on its own: a socket that glows while its lamp
  is off is a lamp that lies. 167 checks.

- `Scripts/check_scene_budget.sh` — a scene costs what it NAMES, not what is switched on in
  it: Unity loads every asset a scene references when that scene loads, active or not. The app
  booted on an iPhone, played its intro and closed with no managed exception, because iOS had
  killed it - `01_MainMenu` names 24.7 million triangles in eleven models, about 846 MiB of
  vertex and index buffers before a single texture, and every prop in it (a candle holder, a
  rotary phone, a table) is roughly 1.5 million triangles on its own. Raw photogrammetry
  exports that were never decimated. The triangle count is MEASURED out of each binary FBX
  rather than estimated from a file size, and the numbers are written down in
  `Scripts/scene_budget.txt` as a baseline: the debt can shrink and cannot silently grow. It is
  not fixable by an import setting - Unity's model importer has no polygon reduction - so the
  guard reports the gap to a mobile working figure rather than pretending a threshold makes it
  go away. 2 checks.

All thirteen run in CI (`.github/workflows/determinism.yml`). Run them locally before
pushing; they need nothing but a shell (and `python3` for the roster checks).

## The mistakes this project has already made

Repeating one of these is the most likely way to break something.

1. **A second implementation instead of a fix.** There were two flashlights
   and two inventories. Both merged cleanly and only one of each was real. If
   the existing one is wrong, change it.
2. **A built-in shader fallback.** `Shader.Find("Standard")` resolves
   everywhere and draws solid magenta under URP. Ask `Art.CiycShaders` and
   accept null — a missing object is better than a magenta one.
3. **A `Resources.Load` path that has never existed.** The ghost prefab path
   had the project name in it twice, missed silently for the life of the
   project, and made every ghost the primitive fallback. Reach content through
   `Content.CiycContentRegistry` by reference.
4. **Reflection into another class's private fields.** It compiles, reviews
   clean, and fails silently on the next rename. Ask for a public method.
5. **A property getter that reads itself.** `GhostSpawnManager.Player` tested
   the property instead of the backing field. The first read recursed until the
   stack ran out — an uncatchable crash sitting in the ghost spawn path, which
   nothing noticed because nothing had spawned a ghost with a player present.
   `check_multiplayer_architecture.sh` looks for the shape now.
6. **Asking `LocalPlayerService` "where is the player".** It holds exactly one:
   the one on this machine. Correct in single player and silently wrong with a
   second one. Ask `PlayerPresence` who is here; ask `LocalPlayerService` which
   one is mine.
7. **Reading a low compiler error count as success.** The offline typecheck
   harness stops early when its own stub breaks and then reports *zero*
   project errors. Always diff against the recorded baseline; never read a
   drop as good news without finding out which errors went and why.
8. **A guard satisfied by a doc comment.** Twice now. A check grepped the whole
   file for `EndSession`, and a `<see cref="EndSession"/>` in a comment kept it
   green after the method was renamed away; the same hole let a
   `<see cref="LaunchStatus.NoOnlineProvider"/>` stand in for the refusal
   itself. Grep the declaration or the statement, never the name. The reverse
   also bites: a check that greps for a forbidden call will match the doc
   comment that warns against it, so strip comment lines first.
9. **Trusting the offline stub as if it were Unity.** The typecheck harness's
   `UnityStub.cs` is hand-written by whoever needed a symbol, so it can encode
   the same misconception as the code it is checking - and then the mistake is
   invisible, because both sides agree. `ObstacleAvoidanceType` was declared in
   `UnityEngine` in the stub and used unqualified in three files; the enum is
   really in `UnityEngine.AI`, and the one file without `using UnityEngine.AI`
   would not compile in Unity. The harness passed for weeks. A real compiler on
   a real machine found it in minutes. When the stub is the only thing agreeing
   with you about an API, that is not verification.
10. **A name that resolves nowhere, twice more.** Mistake 3 was a `Resources.Load`
   path. The same shape came back as tags: `go.tag = "Environment"` in four places and
   `go.tag = "LightSwitch"` in one, with neither name in `TagManager.asset`. Unity
   *throws* on an undefined tag, so each of those lines aborted the build it sat in -
   the primitive rooms got a floor and no walls, the house got no light switches and no
   breaker box, the NavMesh got no sources, and five prop prefabs of 120 never finished.
   It logged loudly the whole time and nobody read it. `check_project_tags.sh` checks
   every tag and layer literal against the project settings now.
11. **A `private` Unity message that hides a `virtual` one.** `HeldFlashlight` declared
   `private void LateUpdate()`. `HeldEquipmentBase` declares `protected virtual void
   LateUpdate()`, and Unity dispatches a message to the most-derived declaration *by
   name* - so the base one never ran, and what it does there is call `PlaceInHand()` for
   any frame the body motion's pose callback did not already place. The torch was built
   correctly, given its real model and material, and then left at the anchor instead of
   being solved onto the grip: a hand that animates normally, holding nothing. One of
   nine `HeldEquipmentBase` subclasses did this, and it was the one item anybody noticed.
   C# calls it CS0108 and the offline typecheck harness was printing errors only, so the
   warning that names the bug exactly was never on screen. `check_equipment_catalog.sh`
   checks the shape now; the harness prints warnings now.
12. **A world AABB used to compute a local scale.** `Renderer.bounds` is world space.
   Reading it and dividing a target size by it gives a LOCAL scale that is only right while
   every ancestor has scale 1 - otherwise the parent chain is applied a second time. The
   same line, in two files, looked like two unrelated bugs: a flashlight arriving in the
   hand 2 mm long, and room walls a hundred times too big. Measure in the model's own
   space, and check the size you got against the size you wanted.
13. **One value asked to mean two things.** `-1` was the offline player's client
   id, and `-1` was about to become "nobody owns this item", which would have
   made the solo player's carried torch read as unowned and let the first person
   past take it. `MultiplayerProtocol.LocalOnlyClientId` and `NoClientId` are
   now separate, and the harness asserts they differ.
14. **Content removed without removing what pointed at it.** The Kenney house interior —
   120 furniture models, 115 prop prefabs, 15 room prefabs, 115 prop definitions, 15 room
   definitions — was deleted because a purchased modular pack replaces it. Deleting the
   assets alone would have left the content catalog naming 130 GUIDs that resolve nowhere,
   `ExternalAssetPaths` naming two folders that no longer exist, and `ExternalAssetIntegrator`
   calling a builder that was gone. A catalog full of missing references looks exactly like a
   working one until it is opened, which is the same failure as mistake 3 wearing different
   clothes. The catalog is written empty instead, `PropDefinitionFactory.ModelSources` is an
   empty array with the machinery intact behind it, and no constant names a deleted folder.
15. **A file that is correct in the repository and absent on the machine.** The scene said
   `Missing Prefab with guid: cbaf2094dea28…` in red for `CIYC_HauntedRotaryPhone`, and
   every part of the repository was right: the `.meta` declared that guid, the scene
   reference matched it byte for byte, the git-lfs pointer's sha256 matched the file, and
   the remote held the object. What was wrong was the working copy — a 134-byte pointer
   where a 110 MB binary FBX belongs, because git-lfs had fetched some objects and not that
   one. Nothing in the repository could say so, so nothing did. The whole LFS payload is
   1.03 GiB, which is over GitHub's free 1 GiB storage and bandwidth allowance, and running
   out mid-fetch leaves exactly this partial state. `check_asset_references.sh` reads the
   working copy now and distinguishes "no LFS content at all" from "some of it".

16. **A shader that did not compile, read as four different bugs.** `Portal.shader` used
   `gate` one statement above the line declaring it. HLSL has no hoisting, so that is not a
   wrong pixel — it is a compile error, and Unity draws a shader that failed to compile with
   its magenta error shader. The portal was therefore a solid magenta rectangle, and every
   explanation offered for it was about something else: the built-in-shader fallback of
   mistake 2, a diagnostic probe room behind the opening, a purchased HDRP pack imported into
   a URP project. All three are real failure modes that look exactly like this one, which is
   why nobody read the shader. The guard could not see it either: it grepped for the
   declaration and for the use as separate patterns, and both were present — just in the
   wrong order. Order is checked now, across every shader in the project, because no compiler
   runs in CI and nothing else would ever say so.

17. **An object created in the wrong scene, which nothing in the object could see.**
   `new GameObject` puts the object in the ACTIVE scene, not in the scene of the component
   that created it. The house generator made its own root that way, and while the lobby
   portal prepares the mission world the investigation scene is loaded ADDITIVELY with the
   lobby still active - so the entire generated house hung off an object the LOBBY owned.
   One cause, and neither of its two faces looks like a scene-ownership bug. The mission's
   entry anchor proves its floor by asking whether the collider belongs to its own scene -
   it has to, because a physics query is global across every loaded scene and the lobby's
   40 x 40 m safety floor sits under nearly anywhere - so every room reported "no floor",
   the anchor stayed null, and the doorway read a null destination as a failed preparation
   and shut itself a second after opening. And had it opened, unloading the lobby would
   have destroyed the house, so the player arrived in a room and then fell through the
   world. The log said "no floor at (0.0, 0.0, 0.0)" about rooms that plainly had one,
   because the rejection was silent about WHY. `Awake` cannot fix this on its own either:
   it runs inside `AddComponent`, one line above the `SetParent` that decides which scene
   the component is in.

18. **A field cleared in the Inspector, which reads as no change at all.** The lobby was
   rebuilt by hand and the commit that carried it also set `WallMaterial`, `FloorMaterial`,
   `CeilingMaterial` and `TrimMaterial` on the Resources content catalog to `{fileID: 0}`.
   Those four are the fallback room's surfaces, and the rule for a primitive with no material
   is to switch its RENDERER off rather than ship Unity's magenta built-in default - so the
   stand-in room does not go magenta, it goes INVISIBLE, with its colliders still holding the
   player up. Nothing errors, nothing is missing, and the diff is four lines that look like
   tidying. It was caught by a guard that checks the catalog's references resolve, and only
   because clearing a reference and deleting the asset behind it produce the same "does not
   resolve". The materials themselves were never gone; the pointing was.

19. **A scene wired to a folder that is deliberately not in the repository.** The main menu now
   instances 26 prefabs from the purchased HQ pack, which is gitignored - per-seat licence, and
   the LFS payload is already over GitHub's free allowance. Every one of those guids resolves on
   the machine that built the room and nowhere else, and from inside the repository that is
   character-for-character the same evidence as mistake 14: a guid nothing declares. The two
   need opposite responses and cannot be told apart by looking, so the machine that HAS the pack
   writes down which guids are its (`Scripts/write_vendor_manifest.sh`) and commits the result.
   The absence is then a named, expected state rather than a silent one - and a guid the
   manifest does not name, or one it does name while the pack is installed, still fails.

20. **Ein eingebauter Ersatz-Snapshot, der so tat, als gäbe es Inhalt.** Die Räume waren leer
   und enthielten trotzdem dunkle Blöcke, und beides hatte dieselbe Ursache in zwei Schritten.
   `ContentSnapshotFactory.Create` liefert bei leeren Katalogen `ContentSnapshot.CreateFallback()`
   — vier erfundene Archetypen, `FURN_SHELF`, `FURN_TABLE`, `PROP_CRATE`, `PROP_LAMP`. Stage A
   plant daraus ordentlich Möbel und faltet die Platzierungen in den Layout-Hash, also *gab* es
   Möbel, aus Sicht der Generierung. Stage B fand dann für keine davon eine `PropDefinition` —
   beide Kataloge sind seit dem Entfernen der Kenney-Inhalte leer (Fehler 14) — und
   `PropSpawner` baute je Platzierung einen Würfel. Nicht einmal in der geplanten Größe: der
   Aufrufer gab `definition != null ? definition.BoundsSize : Vector3.one` mit, und mit null
   fiel das auf **1 × 1 × 1 m**, im dunklen Trim-Material, mehrere pro Raum. Aus einem Meter
   Entfernung im First-Person füllt einer davon das Bild. Der Fallback-Snapshot war als
   Sicherheitsnetz gedacht und war in Wahrheit eine Quelle, die Inhalt *behauptete*, den es
   nicht gab. Ein Größenfilter hätte ihn nie gefangen — ein Kleiderschrank ist größer. Was ihn
   verrät, ist das Unity-Primitivmesh, und genau darauf schaut der Bericht jetzt.

21. **Eine Absage, die den Fehler erst gemacht hat.** Das Portal fand seine Wand nicht mehr und
   schrieb ordentlich "no wall collider found" - und genau diese Zeile war der Schaden. Sie stand
   in `EnsureWallAperture` UEBER dem Aufruf, der einsammelt, was sonst noch in der Oeffnung steht;
   das `return` daneben hiess also nicht nur "kein Loch", sondern "auch nichts abschalten". Der
   Riss war sichtbar offen und jedes Wandcollider blieb fest: eine unsichtbare Wand in einer
   offenen Tuer, also genau das, was diese Klasse seit jeher verhindern soll. Die Ursache dahinter
   war harmlos und alt: `ResolveWall` verlangte EIN Collider von mindestens der Oeffnungsbreite,
   und solange die Lobbywand ein Quader von 10,6 m war, stimmte das. Von Hand aus Modulen des
   gekauften Pakets gebaut ist jedes 2,5 bis 4 m breit gegen eine Oeffnung von 4,70 m, und drei
   nebeneinander addieren sich nicht zu einem. Zwei Lehren, und die zweite ist die teurere: ein
   Test, der ein Ganzes an einem Teil misst, faellt durch, sobald das Ganze aus Teilen besteht -
   und ein Fehlerpfad ist Code, der laeuft, also gehoert er in dieselbe Reihenfolge-Ueberlegung
   wie der Erfolgspfad. Was NICHT mehr passiert, wenn er zu frueh abbricht, sieht niemand.

22. **Ein Regisseur ohne Ensemble, der genau das meldete.** `PrimitiveRoomFactory` setzte an der
   Licht-Steckdose jedes Raums eine Leuchte; `ModularRoomBuilder`, der die Raeume seit der
   Migration baut, nie. `HouseLightingDirector` lief also weiter, fand nichts und schrieb es auch
   hin - "Lit the house: 0 of 0 practicals" - jede Sitzung, ohne Fehler, ohne Warnung, in einer
   Info-Zeile zwischen hundert anderen. Auf dem Bildschirm blieb der Umgebungsterm uebrig, und der
   ist in jeder Ecke jedes Raums derselbe Wert: kein Abfall, keine Richtung, kein Schatten. Ein
   Raum, der ueberall gleich hell ist, sieht nicht dunkel aus, sondern TOT, und so ist er auch
   gemeldet worden - nicht als "es ist zu hell" oder "es fehlt Licht", sondern als "der Raum sieht
   tot aus", was nach einem Materialproblem klingt. Zwei Dinge daran: eine Null in einem Bericht
   ist ein Ergebnis, kein leerer Bericht, und ein Nachfolger, der eine Sache seines Vorgaengers
   nicht uebernimmt, faellt nirgends auf, weil beide fuer sich richtig aussehen.

23. **Eine Diagnose, die zum diagnostizierten Fehler wurde.** Das Portal zeigte, waehrend die
   Missionswelt vorbereitet wird, einen hell magentafarbenen Kasten - `PortalProbeRoom`, gedacht
   als Diagnose: ein dunkles Portalzentrum hat zwei Ursachen, die auf einem Screenshot gleich
   aussehen (der Renderpfad ist kaputt, oder er geht und die andere Seite ist schwarz), und
   Magenta trennt sie. Als VOREINSTELLUNG war es etwas anderes: der Spieler sah einen lila Raum,
   und in dem Moment, in dem die Vorbereitung fertig war, wurde der durch einen ANDEREN Raum
   ersetzt. Gemeldet wurde das als "beim Durchgehen verschwindet der Raum kurz und ein anderer
   erscheint, mit lila Wolken" - also als Ladeuebergang, als Szenenwechsel, als Partikeleffekt.
   Es war keines davon. Es war ein Diagnosewerkzeug, das lief, waehrend jemand zusah. Ein
   Werkzeug, das den Normalfall veraendert, diagnostiziert nicht mehr, sondern erzeugt.

24. **Zwei Vorzeichen, die sich gegenseitig recht gaben.** `ApplyHeadPitch` berechnete
   `-SignedAngle(waagerecht, blickrichtung, rechts)`. `SignedAngle` ist bei dieser Achse schon
   POSITIV, wenn nach unten geschaut wird - genau das, was die zwei Zeilen darunter annehmen: der
   Kommentar ("Positive pitch is looking down"), die unsymmetrische Klammer (`-maxPitchUp,
   maxPitchDown`, weil ein Hals weiter nach unten geht als nach oben) und `RotateWorld`. Das Minus
   liess also alle drei miteinander uebereinstimmen und mit der Zahl nicht: schaute der Spieler
   nach unten, hob der Kopf sich. Zwei Jahre unsichtbar, weil niemand seinen eigenen Kopf sieht -
   in der Ego-Perspektive gibt es keine Ansicht, in der das auffaellt. Aufgefallen ist es beim
   ersten Mal vor dem Spiegel, der einzigen Stelle im Spiel, die das eigene Gesicht rendert. Und
   `PlayerLook.Pitch` behauptete in seiner Zusammenfassung das Gegenteil des eigenen Codes
   ("negative looking down", waehrend `Quaternion.Euler(_pitch, 0, 0)` bei positivem Wert nach
   unten kippt) - die eine Zeile, die man bei einem Winkel ueberhaupt liest.

25. **Ein Awake, das ein Feld las, das eine Zeile spaeter zugewiesen wird.** `PlayerRigBuilder`
   schreibt `AddComponent<PlayerController>()` und in der naechsten Zeile
   `SetPrivateField(playerController, "cameraRoot", ...)`. `Awake` laeuft INNERHALB von
   `AddComponent`, also war `cameraRoot` dort null, und `_hasCameraRoot = cameraRoot != null`
   speicherte fuer die Lebensdauer des Spielers **false**. `UpdateCrouch` kehrte an seiner ersten
   Zeile um: die Kapsel schrumpfte, die Figur ging in die Hocke, der Blick blieb auf Stehhoehe.
   Also genau "Ducken sieht man nicht", das der Kommentar derselben Methode als den Fehler
   beschreibt, gegen den sie geschrieben wurde. Dieselbe Datei kannte die Falle schon - ihr
   `Start` sucht `PlayerBodyMotion` ausdruecklich deshalb dort statt in `Awake` - nur nicht fuer
   ihr eigenes serialisiertes Feld. Ein gecachtes Flag kann veralten, eine Referenz nicht: die
   Pruefung gehoert dorthin, wo sie benutzt wird.

26. **Zwei Waechter, die ROT meldeten, ohne dass etwas kaputt war.** Beide pruefen eine echte
   Invariante und beide hingen an etwas, das sich aendern darf.

   Der eine suchte die drei Waende an der Portaloeffnung ueber ihre `fileID` in der Szene. Unity
   vergibt die beim Neuserialisieren einer Szene neu - nach einer gewoehnlichen Bearbeitungssitzung
   hiess `4 (8)` statt 2070585590 auf einmal 593429620 - und der Waechter meldete "ohne Collider
   laeuft der Spieler hindurch" ueber drei Waende, die einen Collider hatten. Fehler 3 und 10,
   diesmal im Waechter statt im Spielcode. Der andere las JEDEN Shader unter `Assets/`, also auf
   der einen Maschine, die die gekauften Pakete hat, auch deren: `Triplanar.shader` aus einem
   gitignorierten Paket, das im Repository gar nicht existiert. CI war gruen, die Arbeitsmaschine
   rot, und die genannte Datei durfte der Leser nicht einmal anfassen - dieselbe Spaltung wie
   Fehler 19, in einem Waechter.

   Ein falsches ROT ist schlimmer als eine fehlende Pruefung. Es schickt den Leser hinter einem
   Fehler her, den es nicht gibt, es kostet die Sitzung, in der er ihn sucht, und beim naechsten
   Mal glaubt er der Zeile nicht mehr - womit die Pruefung genau dann nichts mehr wert ist, wenn
   sie recht hat. Also: gesucht wird ueber den NAMEN, an dem ein Mensch das Objekt ohnehin
   erkennt, geprueft wird nur, was uns gehoert, und was uebersprungen wurde, wird GEZAEHLT
   ("18 vendor shader(s) skipped") statt stillschweigend ausgelassen.

27. **Ein Awake, das annahm, es sei noch nie gelaufen.** `SpectralGridProjection.Awake` schrieb
   `_renderer = gameObject.AddComponent<MeshRenderer>()` und griff in der naechsten Zeile darauf
   zu. `EquipmentRuntimeFactory` baut jedes Ausruestungsstueck EINMAL als lebende Vorlage - dabei
   laeuft dieses Awake und hinterlaesst MeshFilter und MeshRenderer - und was der Spieler
   bekommt, ist ein `Instantiate` dieser Vorlage. Der Klon traegt beide also schon, `Renderer` ist
   `[DisallowMultipleComponent]`, und `AddComponent` gibt dann **null** zurueck statt eines
   zweiten. Die naechste Zeile dereferenzierte das.
   Der Stacktrace zeigte auf `SetActive(true)` in `MissionEquipmentInstaller`, darunter der
   Ausruestungstisch, darunter eine Coroutine - drei Stellen, die alle wie die Ursache aussahen
   und keine davon war es. Und der Schaden war groesser als das eine Item: die Ausnahme flog
   mitten aus der Platzierungsschleife, also lag danach gar nichts mehr auf dem Boden. Ein Awake,
   das voraussetzt, noch nie gelaufen zu sein, ist fuer alles falsch, was jemals geklont wird -
   und geklont wird in diesem Projekt jedes einzelne Ausruestungsstueck. `GetComponent` zuerst.
   Dieselbe Form steht noch in fuenf Lichtbauern (`HeldFlashlight`, `UVLight`, `WardingRelic`,
   und beiden Kameras): sie erzeugen je ein `new GameObject`, stuerzen also nicht ab, sondern
   haengen dem Klon ein ZWEITES Licht an. Nicht angefasst, weil sie funktionieren und Hotspots
   sind - aber aufgeschrieben, damit die naechste doppelte Helligkeit nicht neu gesucht wird.

28. **Ein Strahl nach unten, der auch in einer Wand den Boden findet.** Die Ausruestung wurde
   "einen festen Schritt nach vorn und 1,9 m nach rechts" vom Spawn abgelegt, und die Hoehe
   dabei sauber per `Physics.Raycast` GEMESSEN - die Zahl stimmte also, und trotzdem lagen alle
   elf Gegenstaende in der Wand. Ein Strahl nach unten beantwortet nur "wie hoch ist hier der
   Boden", und diese Frage hat auch mitten in einer Wand eine richtige Antwort. Was nie gefragt
   wurde, war "ist hier ueberhaupt Platz". Der Seitenversatz war einmal richtig, fuer einen Raum,
   den es nicht mehr gibt: seit die Lobby aus Modulen des gekauften Pakets steht, ist rechts vom
   Spawn eine Wand. Gemeldet wurde es als "die sind aktuell alle in der Wand", und von aussen
   sieht das genauso aus wie elf Gegenstaende, die gar nicht erst gebaut wurden - dieselbe
   Ununterscheidbarkeit wie in Fehler 20 und 27. Die Lehre ist nicht "mehr messen", sondern
   WELCHE Frage gemessen wird: eine gemessene Zahl beweist nur das, wonach gefragt wurde. Ein
   Kasten, der auf dem Boden STEHT statt darin, sieht den Unterschied; ein Strahl nie. Und ein
   Platz, der aus einer festen Zahl im Code kommt, ist eine Behauptung ueber einen Raum - der
   verdrahtete `layoutAnchor` ist die Entscheidung, die keine Suche ueberstimmen darf.

29. **Eine Zahl, die mit der Geometrie mitgewandert ist, ohne je zu ihr gepasst zu haben.** Der
   Projektor lag als Streichholzschachtel von 5,5 x 2,7 x 1,3 cm auf dem Boden - zu klein, um
   ihn zu sehen, und zu klein, als dass der Interakt-Strahl ihn gefunden haette. `length` stand
   auf 0,055 m, und der Kommentar daneben nannte Z die TIEFE des Geraets und versprach 0,225 x
   0,111 m in der Hand. Aus der Datei gelesen ist das Mesh X 0,9400, Y 0,4647, Z 1,9025: Z ist
   die LAENGSTE Achse, um den Faktor zwei. Die laengste Achse auf 5,5 cm zu setzen schrumpft
   alles andere mit. Die gewuenschte GROESSE stimmte, die Zahl war von ihr aus nie erreichbar.
   Und beim Modellwechsel wurde geprueft, dass die Geometrie identisch ist - Vertices, Normalen,
   UVs und Indizes hashen gleich - und daraus geschlossen, die Zahlen duerften unveraendert mit.
   Der Schluss ist richtig und die Folgerung falsch: identische Geometrie liefert ein
   identisches Ergebnis, ein identisch FALSCHES eingeschlossen. "Hat sich nichts geaendert"
   beweist nicht "war vorher richtig", und der Kommentar, der die Zahl erklaerte, war genau die
   Quelle, die niemand gegen die Datei geprueft hatte.

30. **Eine Referenz, die den Klon nicht ueberlebt, waehrend das Objekt es tut.** Der
   DOTS-Projektor stand sichtbar und richtig gross im Raum und war kein Gegenstand: hinschauen
   tat nichts, kein Name, kein Prompt, kein Aufheben. Das Debug-Schild sagte es dann in einer
   Zeile - `hit "FLOOR_Lobby_01" at 2.52 m` -, waehrend das Fadenkreuz mitten auf dem Geraet lag.
   Der Strahl ging hindurch.
   `HeldEquipmentBase.CarriedRoot` ist eine Auto-Property, `_dropCollider` ein nicht
   oeffentliches Feld; Unity serialisiert beide nicht. `Instantiate` kopiert dagegen jedes
   GameObject und jede Komponente. Jedes Ausruestungsstueck erreicht die Welt als Klon - die
   Laufzeitfabrik haelt je Id eine lebende Vorlage -, der Klon kam also mit Visual und
   Wurfkapsel VORHANDEN und beiden Referenzen NULL an. Genau das liest
   `if (CarriedRoot != null) return;` als "es wurde noch nichts gebaut", und `Start` baute ein
   zweites Visual exakt auf das erste. Zwei deckungsgleiche Kopien desselben Meshes sehen aus
   wie ein Objekt: die Doppelung war unsichtbar, und was man stattdessen sah, war ein
   Gegenstand ohne Funktion, weil die Komponente auf etwas anderes zeigte als das, was da stand.
   Vier Runden lang wurde am Symptom gesucht - Groesse, Collider, Tasche, Prompt -, und jede
   dieser Erklaerungen war fuer sich plausibel. Was fehlte, war nicht Genauigkeit, sondern die
   Frage, ob das Ding ueberhaupt noch dasselbe Objekt ist, das die Komponente zu besitzen
   glaubt. Ein Name taugt als Identitaet nicht, das Visual heisst wie der Gegenstand - eine
   KOMPONENTE ueberlebt die Kopie. Und ein sichtbares, totes Objekt meldet sich nie von selbst:
   `EquipmentSpawnDiagnostic` laeuft jetzt bei jedem Spawn die ganze Kette ab und nennt das
   fehlende Glied.

31. **Ein Index, der bis vier zaehlt, in einem Feld mit drei Plaetzen.** `_slots` fasst die drei
   Ermittlungsplaetze; `SelectSlot` prueft den gewuenschten Index gegen `SelectableSlotCount`,
   und das sind VIER, weil die Fackel einen eigenen Platz mit Index 3 hat. Die letzte Zeile der
   Methode las dann `_slots[_selectedIndex]` - also `_slots[3]` in einem Feld der Laenge drei.
   IndexOutOfRangeException, bei jedem Druck auf die Fackeltaste, bei `SelectTorch`, und beim
   automatischen Rueckfall auf die Fackel, wenn die Tasche leer ist. Genau das tut die Lobby
   beim Start, wenn sie dem Spieler die Fackel aus der Hand nimmt.
   Der Schaden war nicht "es passiert nichts". Geworfen wurde NACH `_selectedIndex = index` und
   NACH `EquipSelected()`, die Ausnahme verliess also `AddItem` und
   `InteractivePickup.Interact` und liess den Rest des Aufhebens liegen - mit halb gesetztem
   Zustand. Von aussen sah das aus wie "aufgehoben, aber weder in der Tasche noch in der Hand",
   und dieselbe Beschreibung passt auf ein Dutzend anderer Ursachen. Jeder andere Zugriff in
   derselben Datei geht ueber `GetSlot`, das die Fackel kennt; diese eine Zeile ging daran
   vorbei. Ein Sonderplatz, der nur an EINER Stelle nicht mitgedacht wird, ist genau so viel
   wert wie gar keiner.

32. **Zwei Tasten, die niemand zu Ende verfolgt hatte.** Der Projektor liess sich nicht an die
   Wand haengen, und dahinter lagen zwei getrennte Ursachen, die beide erst im Editor auffielen.
   F tat NICHTS: `PressUse()` setzt ein Flag, und `UsePressed` wird im ganzen Projekt von
   niemandem gelesen; der Use-Knopf, der es sonst ausloest, wird in `WireHUD` ausdruecklich auf
   null gesetzt. Ein Signal, das gesendet und nie empfangen wird - dieselbe Form wie "public und
   niemand ruft es auf", nur eine Ebene tiefer.
   Und X warf das Geraet auf den Boden. `DebugItemTools` lag seit jeher auf X, als X noch keine
   Spieltaste war. In dem Moment, in dem X zur Interakt-Taste wurde, lief EIN Druck durch BEIDE
   Pfade: die Interaktion tat ihres, und die Entwicklerhilfe legte ab, was in der Hand war. Wer
   mit dem Projektor an eine Wand ging und X drueckte, sah ihn fallen.
   Die Lehre ist nicht "besser testen", sondern dass eine neue Tastenbindung eine Suche nach
   derselben Taste im ganzen Projekt verlangt - auch in Dateien, die als Entwicklerhilfe gelten
   und deshalb bei der Ueberlegung nicht mitgedacht werden. Und ein Schritt, der nur ueber einen
   Knopf erreichbar ist, den ein Bildschirm nicht baut, ist kein Schritt, sondern eine Sackgasse:
   das AIM ist ersatzlos weg, weil es keine Entscheidung trug.

33. **Eine Erklaerung, die den Code beschuldigt hat, den sie nicht gelesen hatte.** Der
   DOTS-Projektor zeigte beim Einschalten keinen einzigen Punkt, und die Ursache stand angeblich
   fest: gebaut war ein Kegel-Mesh mit einem eigenen Shader, also zeichnete der Punkte INNERHALB
   eines Volumens, also Punkte in der Luft, also nichts auf einer Wand. Das klang zwingend, es
   passte zum Klassennamen, zum Mesh und zum Symptom - und es war falsch. `SpectralGrid.shader`
   rekonstruiert je Pixel die Weltposition der Flaeche DAHINTER aus dem Tiefenpuffer und rechnet
   die Punkte dort; das ist eine richtige Projektion, bei der die Verdeckung sogar geschenkt ist.
   Die Erklaerung stand schon in zwei Dateien - im Kommentar der Klasse und im Kommentar eines
   neuen Waechters - und waere als der aufgeschriebene Grund committed worden, den der naechste
   Leser geerbt haette. Warum der Shader nichts zeigte, ist bis heute NICHT geklaert: die
   Tiefentextur, die er braucht, ist eingeschaltet (`CIYC_URP.asset`, `m_RequireDepthTexture: 1`),
   und seine +Y-Wurfachse passt zum Rest des Geraets. Die Lehre ist nicht "vorsichtiger
   formulieren". Eine Begruendung, warum etwas nicht funktioniert, ist eine BEHAUPTUNG UEBER
   CODE und wird spaeter gelesen wie jede andere - aus der Form des Drumherums abgeleitet ist
   sie geraten, und geraten bleibt sie in dem Moment dauerhaft, in dem sie in einem Kommentar
   steht.

34. **Zwei Vorwaerts, die beide richtig sind.** Ein Unity-Spot leuchtet sein eigenes +Z entlang.
   Die Trage-Konvention dieses Projekts ist +Y: der Kegel, `FieldStrengthAt`, die Vierteldrehung
   in `OrientForPlacement` und der Kommentar daneben benutzen alle +Y als die Achse, an der ein
   Geraet arbeitet. Das neue Projektionslicht bekam seinen Linsenversatz auf +Z und gar keine
   Drehung - beides fuer sich richtig, zusammen 90 Grad daneben. Ein an die Wand geschraubter
   Projektor haette seitwaerts aus sich heraus geleuchtet, in genau die Wand, an der er haengt.
   Zu sehen gewesen waere: nichts. Also dasselbe Bild wie ein Licht, das nie angegangen ist, wie
   eine fehlende Cookie-Textur und wie ein `SetRunning`, das niemand ruft - vier Ursachen, ein
   Symptom, und keine davon meldet sich von selbst. Verwandt mit Fehler 24, wo drei Zeilen
   einander recht gaben und keine davon der Zahl. Wo zwei Konventionen aufeinandertreffen, ist
   die Umrechnung die Arbeit, und sie gehoert hingeschrieben statt weggelassen. Nebenbei aus
   derselben Runde: eine Cookie wird in der GROESSE geschrieben, in der sie benutzt wird -
   `CIYC_URP.asset` haelt den Cookie-Atlas auf 2048, eine 2048er Maske IST also der ganze Atlas,
   und eine, die der Importer verkleinern muss, verliert ihre Punkte an den Filter.

35. **Drei Filtereinstellungen, die aus Kreisen Pillen gemacht haben.** Die DOTS-Punkte kamen im
   Editor als laengliche Striche an, und die Cookie, die sie erzeugt, enthielt nachweislich
   runde Scheiben - gleicher Radius in X und Y, aus einer Distanzfunktion gezeichnet. Die
   Verformung entstand also nicht beim Zeichnen, sondern beim SAMPLEN, und zwar dreifach:
   `enableMipMap: 1` auf einer Textur, die ueber einen ganzen Raum IMMER vergroessert wird, also
   nie eine Mipstufe waehlt und nur weicher wird; `aniso: 4`, und anisotrope Filterung ist per
   Definition ein richtungsabhaengiger Filter, der einen 3-Pixel-Punkt im schraegen Blick
   entlang einer Achse auszieht; und `wrapU: 0` neben `wrapV: 1` - Repeat quer, Clamp hoch -
   was woertlich ein anisotropes Sampling-Setup auf einem Feld runder Punkte ist. Das dritte
   war ausserdem GEGEN die eigene Absicht gesetzt: der Kommentar der Meta-Vorlage sagte "eine
   Spot-Cookie klemmt in beide Richtungen", und der Aufruf uebergab die Flagge vertauscht. Die
   Lehre steckt in der Reihenfolge der Fragen: bei einer verformten Textur ist die erste Frage
   nicht "ist die Quelle falsch gezeichnet", sondern "wird sie richtig gelesen" - und ein
   Importer hat drei Voreinstellungen, die fuer eine Maske alle drei falsch sind.

36. **Ein Anspruch, der "hat gewirkt" statt "war meiner" bedeutete.** `TryClaimPower` gab false
   zurueck, wenn der gewaehlte Projektor nicht montiert war - gedacht als "ich habe nichts
   getan, nimm du". Die Fackel nahm den Druck dann und lehnte ihn ihrerseits ab, also standen
   im Log zwei Zeilen von zwei Geraeten:
     [CIYC][DOTS] [BLOCKED] POWER reason=NotMounted
     Flashlight: WrongState: not held or placed (World)
   Beide stimmen fuer sich, und das Paar ist falsch. Wer den Projektor gewaehlt hat, adressiert
   mit G den Projektor, ob der an der Wand haengt oder in der Hand liegt. Ein Anspruch auf eine
   Eingabe beantwortet die Frage "wem gehoert dieser Druck", nicht "hat dieser Druck etwas
   bewirkt" - wer die zweite Frage beantwortet, gibt Eingaben an das naechstbeste Geraet weiter,
   sobald das erste gerade nichts zu tun hat.

37. **Eine Szene, die aus leeren GameObjects bestand, und ein Lader, der das jedes Mal
   reparierte.** `03_Investigation.unity` enthielt NULL MonoBehaviours. `WORLD`, `VanAnchor`,
   `HouseAnchor`, `MANAGERS/ProceduralHouseGenerator`, `GhostSpawnManager`, `MissionManager`
   und `INVESTIGATION_BOOTSTRAP` waren leere Objekte, benannt nach Komponenten, die sie nicht
   trugen. `MissionWorldLoader` hing den Bootstrap beim Laden selbst an, band die drei Anker und
   meldete es als Fehler - laut, korrekt und bei jeder Mission neu. Eine Reparatur, die
   funktioniert, ist trotzdem eine Reparatur: sie laeuft, bevor die Szene etwas sagen kann, sie
   kann nur die Felder setzen, die sie kennt, und JEDES andere serialisierte Feld steht dann auf
   dem Wert, den eine frisch angehaengte Komponente zufaellig hat, nicht auf dem, den der Code
   als Initialisierer schreibt. Beim Nachtragen von Hand gilt genau dasselbe in die andere
   Richtung: ein Feld, das im YAML fehlt, faellt auf 0/null zurueck und nicht auf den
   C#-Initialisierer, also muessen alle sechzehn hingeschrieben werden (Fehler 18). Und die
   Pruefung dazu muss zwei Zustaende unterscheiden, die gleich aussehen: das Dokument fehlt, und
   das Dokument steht da, ist aber in keiner `m_Component`-Liste eingetragen. Der erste Anlauf
   suchte nur die guid in der Datei und blieb gruen, als die Komponente von ihrem Objekt
   abgehaengt wurde.

38. **Ein Materialname, der neunzehn Materialien trifft.** Der Fenster-Insert nannte `"1"` als
   eines der Materialien, die behalten werden sollen - gemeint war der Fensterrahmen. Dieses
   gekaufte Paket hat NEUNZEHN Materialien namens `"1"`, und die Wandschale von Prefab 7 traegt
   eines davon. Also wurde die ganze 4-m-Wand mit ausgewaehlt, und `ModularRoomBuilder` lehnte
   sie jede Mission neu ab: `measured=(3.764, 4.116, 0.402) opening=2.05 x 0.90 -> REFUSED`. Die
   Ablehnung war richtig und der Fehler stand woanders - eine Zeile, die korrekt meldet, dass
   etwas nicht passt, sagt nicht, WARUM das Falsche ueberhaupt angeboten wurde. CLAUDE.md kannte
   die Regel schon: ein Name mit weniger als drei Zeichen identifiziert in diesem Paket nichts.
   Sie stand beim Weiss-Material-Doktor und galt fuer den Katalog-Schreiber nicht mit.

39. **Zwei Anlaeufe, die die Form des EMITTERS hatten statt der Form des RAUMS.** Der
   DOTS-Projektor bekam erst einen Spot mit 70 Grad, dann fuenf breitere Spots. Beide Male war
   die Frage falsch gestellt: ein Spot ist ein Kegel, und ein Kegel kann keine Kugel abdecken -
   fuenf davon auch nicht, sie lassen zwischen sich Luecken und muessen, um die zu schliessen,
   so breit werden, dass die Cookie an ihrem Rand die Punkte zu Pillen zieht. Was das Geraet
   TUT, ist in alle Richtungen strahlen; was ein Spot kann, ist in eine. Die Referenzbilder
   zeigten das von Anfang an - ein Muster, das den Projektor umschliesst und zu seiner Achse hin
   zusammenlaeuft - und zwei Runden lang wurde trotzdem am Kegel weitergebaut, weil ein Kegel
   das war, was schon dastand. Die Loesung war nicht mehr Kegel, sondern eine andere Koordinate:
   ein Winkelraster in KUGELKOORDINATEN um die Linse, ausgewertet auf der Flaeche, die der
   Tiefenpuffer hinter jedem Pixel liefert. Und sie kostet nichts extra, weil ein Punkt darin
   kein Objekt ist, sondern ein Test.

40. **Ein gruenes Licht, das den Raum aufhellt, statt Punkte, die auf ihm liegen.** Beide
   Licht-Fassungen mussten hell genug sein, um gesehen zu werden, und ein Licht, das hell genug
   ist, beleuchtet alles dazwischen mit: aus einem dunklen Raum mit hellen Punkten wurde ein
   gruener Raum. Die Referenz ist das Gegenteil - stockdunkel, und darin tausende sehr helle,
   sehr kleine Punkte. Ein additiver Pass kann das, was ein Licht nicht kann: wo kein Punkt ist,
   gibt er Schwarz aus und aendert damit exakt nichts, egal wie hell die Punkte selbst sind.
   Helligkeit und Flut sind bei einem Licht dieselbe Zahl und hier zwei verschiedene Dinge.

41. **Eine Szene kostet, was sie NENNT, nicht was in ihr eingeschaltet ist.** Die App lief auf
   dem iPhone an, spielte ihr Intro und schloss sich - ohne Exception, ohne Log, ohne Absturz
   im Managed-Code, weil es keiner war: iOS hatte den Prozess wegen Speicher beendet. Gesucht
   wurde zuerst im Boot-Pfad, denn dort war das Letzte zu sehen. Die Ursache lag eine Szene
   weiter: `01_MainMenu` referenziert 24,7 Millionen Dreiecke in elf Modellen, rund 846 MiB
   reine Vertex- und Indexpuffer, bevor eine einzige Textur dazukommt. Unity laedt beim
   Szenenwechsel ALLES, was eine Szene nennt - ob das Objekt aktiv ist, spielt keine Rolle,
   und der Lobby-Raum ist beim Laden ausdruecklich inaktiv. Jeder einzelne Prop ist ~1,5
   Millionen Dreiecke: ein Kerzenhalter, ein Waehlscheibentelefon, ein Tisch. Rohe
   Photogrammetrie-Exporte, nie dezimiert.
   Zwei Lehren. Ein Absturz ohne Log ist ein Befund und keine Sackgasse - "kein Stacktrace"
   grenzt die Ursachen ein, statt sie zu verbergen, denn ein Managed-Fehler haette einen
   hinterlassen. Und eine Zahl, die keine Importeinstellung senken kann, muss man aufschreiben:
   der Modell-Importer hat keine Polygonreduktion, also ist eine Schwelle, die man setzt und
   nicht erreicht, nur eine Ausrede. `Scripts/scene_budget.txt` haelt den Stand fest, damit die
   Schuld schrumpfen kann und nicht unbemerkt waechst.

42. **Eine Wirkung, die als Koerper vermessen wurde.** Der DOTS-Projektor lag nicht mehr vor den
   Fuessen des Spielers, sondern auf **y = 6,49 m** - ueber der drei Meter hohen Lobbydecke, in
   einem Raum, dessen Boden auf null liegt. Gemeldet wurde es als "dots geraet fehlt jetzt", und
   von innen stimmt das: ein Gegenstand ausserhalb des Raums und ein Gegenstand, der nie gebaut
   wurde, sehen gleich aus (Fehler 20, 27 und 28, zum vierten Mal).
   Die Rechnung geht genau auf. Der Tisch legt jeden Gegenstand hin, indem er seine Box misst und
   ihn so weit hebt, bis die UNTERSEITE auf dem Boden steht. Gemessen wurde aber auch
   `SpectralGrid_Volume` - der Kasten, in dem der Shader seine Punkte rechnet, mit einer halben
   Kantenlaenge von 5,5 m. Also: Zielhoehe 1,30 m, Boxunterkante 1,30 + 0,247 (Kopf) + 0,06
   (Linse) - 5,5 = -3,893, Hub 1,30 - (-3,893) = 5,193, Endhoehe 6,493. Im Log steht 6,49.
   Der Kasten ist kein Modell, sondern die Bildschirmflaeche, auf der die Wirkung laeuft; er ist
   absichtlich so gross wie die Reichweite und nie zu sehen. Genau deshalb faellt er niemandem
   auf, der ihn misst. Die Lehre ist nicht "weniger messen", sondern dass "wie gross ist dieses
   Ding" zwei verschiedene Fragen sind, sobald ein Objekt etwas zeichnet, das nicht es selbst ist
   - und dass die Antwort dem Objekt gehoert, nicht dem Messenden. Markiert wird das mit einer
   KOMPONENTE (`EffectVolume`), weil eine Komponente das Einzige ist, was `Instantiate`
   mitbringt: jedes Ausruestungsstueck erreicht die Welt als Klon, und ein Name, ein Tag oder ein
   privates Feld waere beim Klon weg (Fehler 30).

43. **Ein Waechter, der ROT meldete, weil sein eigenes `printf` gestorben ist.** CI meldete
   `check_ui_and_portal.sh` mit 296 von 297, und die eine rote Zeile - "eine Wand aus mehreren
   Modulen zaehlt zusammen" - war auf jeder lokalen Maschine gruen, auf demselben Commit, auch
   in einem frisch ausgecheckten Arbeitsbaum. Gleicher Inhalt, zwei Urteile.
   Die Ursache stand eine Zeile ueber dem FAIL im Log und niemand haette sie gesucht:
     Scripts/check_ui_and_portal.sh: line 941: printf: write error: Broken pipe
   Das Skript laeuft unter `set -o pipefail` und stellt seine Fragen als
   `printf '%s' "$code" | grep -q MUSTER`. Ein `grep -q` hoert in dem Moment auf zu lesen, in
   dem es trifft; die Pipe schliesst sich unter dem `printf`, das noch schreibt, und `pipefail`
   nimmt den Fehler des ERSTEN Glieds als Ergebnis der ganzen Pipeline. Also: Muster GEFUNDEN,
   Pipeline FEHLGESCHLAGEN. Ob es passiert, ist ein Wettlauf zwischen der Schreibgeschwindigkeit
   und der Position des Treffers - lokal gewinnt `printf`, auf dem Runner verlor es. Mit einer
   grossen Zeichenkette ist es in 200 von 200 Laeufen reproduzierbar.
   Es luegt in BEIDE Richtungen, und die zweite ist die teurere. `if ... | grep -q` macht aus
   einem gefundenen Muster ein fehlendes: falsch ROT. `if ! ... | grep -q` macht aus einem
   gefundenen Muster ein fehlendes und laesst die Pruefung damit BESTEHEN: falsch GRUEN. In
   dieser einen Datei hatten neunzehn Pruefungen die zweite Form - stillschweigend gruen, seit
   sie geschrieben wurden, ohne dass irgendwer es haette bemerken koennen.
   Zwei Lehren. Ein falsches ROT kostet eine Sitzung (Fehler 26); ein falsches GRUEN kostet die
   Pruefung. Und die erste Erklaerung fuer "auf CI rot, lokal gruen" war schon einmal geraten
   worden: der Kommentar oben in dieser Datei schrieb dieselbe Sprunghaftigkeit "unter Last
   verlorenen Forks" zu und baute einen Cache mit Wiederholungen dagegen. Das war eine
   Behauptung ueber Code, die niemand nachgemessen hatte (Fehler 33) - die Zeile im Log nennt
   den Mechanismus, und der ist weder Last noch Zufall, sondern SIGPIPE.

44. **Ein Regler, der Beweise erzeugt hat, statt etwas zu bewegen.** Fuer "es sagt PROJECTING und
   der Bildschirm bleibt schwarz" wurde eine sechsstufige Diagnose in den Shader gebaut: Stufe 1
   magenta ueber das ganze Volumen, dann Tiefenrekonstruktion, Reichweite, Winkelraster, Punkte
   ohne und mit Verdeckung. Die Stufe wird ueber ein Inspector-Feld gewaehlt.
   Der Nutzer hat alle sechs Stufen durchgestellt. **Alle sechs waren unsichtbar.** Das las sich
   wie ein vernichtender Befund - nicht einmal ein hartes `return half4(1,0,1,1)` in der ersten
   Zeile des Fragment-Shaders kam an, also musste der Pass selbst tot sein: Culling, ColorMask,
   Pass-Tags, Metal-Kompilierung. Sechs neue Verdaechtige.
   Es war keiner davon. `PushProperties()` lief an genau zwei Stellen - `Configure()` und
   `SetRunning(true)` -, und `LateUpdate` schob nur Linse und Achsen nach. Eine Aenderung im
   Inspector waehrend des Spiels erreichte den Renderer also NIE. Alle sechs Stufen waren Stufe
   0. Der eigene Log sagte es sogar hin: `debugStage=0`, gedruckt beim Einschalten, und nichts
   danach hat den Wert je wieder geschrieben. Nur ein G aus/an haette es getan - und die Batterie
   (75 Einheiten bei 0,85/s = 88 Sekunden) war vorher leer.
   Die Lehre ist nicht "Diagnosen sind gefaehrlich". Sie ist: ein Diagnosewerkzeug ist selbst
   Code und braucht denselben Beweis wie das, was es untersucht. Dieses hier hat nicht nichts
   getan - es hat SECHS FALSCHE BEFUNDE geliefert, jeder davon plausibel, jeder in eine andere
   Richtung. Ein Regler, der nicht bewegen kann, was er benennt, ist schlimmer als gar keiner,
   weil man ihm glaubt. Verwandt mit Fehler 23 (das Werkzeug wurde zum Fehler) und mit 43 (der
   Waechter log ueber das Projekt) - dreimal jetzt hat die Messvorrichtung gelogen, nicht das
   Gemessene.

45. **Vier Erklaerungen fuer denselben Shader, und keine davon war gemessen.** Der
   DOTS-Shader ist in dieser Sitzung zum dritten Mal umgebaut worden, und beim Nachzaehlen
   stand fest: er hat noch NIE einen Pixel gezeichnet - nicht bei 51c76f2, nicht bei db070a6,
   nicht bei c3ad2d8, und von seinem Vorgaenger sagt Fehler 33 dasselbe. Darauf lagen
   inzwischen ein Verdeckungsmarsch, ein Halo und ein Streifwinkel-Term aus ddx/ddy: drei
   Schichten, von denen keine je eine Kamera gesehen hatte, auf einer Grundlage, die auch
   keine gesehen hatte. Jede einzelne war fuer sich verteidigbar, und genau das ist die
   Falle - eine unbestaetigte Schicht hat keine Gegner, sie hat nur noch keine Zeugen.
   Ausgeschaltet reicht dabei nicht. `occlusionStrength = 0f` stand voreingestellt auf aus und
   war trotzdem eine Zeile, die jeder spaetere Leser fuer erprobt haelt, und ein `SetFloat` auf
   ein Uniform, das der Shader nicht mehr deklariert, schreibt ins Leere und sagt nichts -
   dieselbe Stille wie Fehler 44, nur eine Ebene tiefer. Also WEG, in beiden Dateien, mit einer
   Pruefung, die sie draussen haelt, bis die Stufe darunter bestaetigt ist.
   Und die Diagnose selbst braucht eine Ordnung. Eine Sprosse, die UNTER dem sitzt, was sie
   halbieren soll, wird aus einem Grund weiter hinten dunkel und liefert dann einen FALSCHEN
   Befund statt gar keinem. Der Waechter liest deshalb die Zeilennummern und vergleicht sie,
   statt der Reihenfolge zu glauben; die Magenta-Sprosse ist die erste Anweisung im Fragment-
   Shader, und ueber ihr steht nichts ausser dem Lesen der Stufe. Eine Sprosse ohne eigenen
   Zweig gibt es nicht mehr: Stufe 5 fiel auf Stufe 0 durch und haette jedem, der sie waehlt,
   die Antwort auf eine andere Frage gegeben.
   Die teuerste Lehre steckt aber im Waechter, nicht im Shader. Die Paritaetspruefung - jedes
   Property, das C# schiebt, ist im Shader deklariert und umgekehrt - las zuerst die
   `Shader.PropertyToID`-Zeilen. Eine Id zu HABEN beweist nicht, dass geschoben wird: beim
   Zahntest blieb sie gruen, nachdem der `SetFloat`-Aufruf geloescht war. Sie liest jetzt die
   echten `_block.Set`-Aufrufe. Ein Waechter, dessen Name mehr behauptet als seine Frage misst,
   ist ein leeres Versprechen, das man erst bemerkt, wenn man sich darauf verlassen hat.

46. **Ein Klon, der seinen eigenen Kopf zweimal bekam.** `HeldEquipmentBase.BuildCarried`
   uebernimmt seit Fehler 30 das Visual, das ein Klon schon mitbringt, statt ein zweites zu
   bauen. `SpectralGridProjector.BuildCarried` rief danach unbedingt
   `new GameObject("ProjectorHead")`. Jeder Projektor im Spiel ist ein Klon einer lebenden
   Vorlage, also kam jeder mit einem geerbten Kopf samt Projektion und Zeichenvolumen an - und
   bekam ein zweites Paar daneben gebaut.
   Es hat sich nie gemeldet, und zwar aus dem denkbar unguenstigsten Grund: das geerbte Paar
   ist STUMM. Ein nie eingeschalteter `MeshRenderer` kommt ausgeschaltet ueber die Kopie, und
   `_running` ist ein privates Feld, das die Kopie gar nicht mitbringt. Also nichts doppelt
   gezeichnet, nichts heller, keine Fehlermeldung - nur ein totes Duplikat des eigenen Effekts,
   das in jeder Suche nach "warum zeichnet das nichts" als Treffer auftaucht und keiner ist.
   Ein Korrektur, die an einer Stelle angebracht wird, gilt fuer alles darunter mit: das Visual
   war repariert, und der Kopf, der daran haengt, nicht. Und die Zaehlung dazu muss vom GERAET
   ausgehen. Von der Komponente aus gezaehlt findet sie nur ihre eigenen Kinder und meldet eine
   saubere 1, waehrend die zweite Projektion nebenan haengt - eine Zahl, die genau das nicht
   sehen kann, wofuer sie da ist.

47. **Ein Bildschirm, den nur ein Klick erzeugen konnte.** Das Hauptmenue wurde gebaut, gemessen,
   mit acht Pruefungen abgesichert, committet und gepusht - und auf dem Bildschirm stand danach
   Byte fuer Byte dasselbe wie vorher: TAP ANYWHERE TO START. Nichts war kaputt. Der Code war
   ein EDITOR-WERKZEUG, also entstanden die vier Zeilen erst in dem Moment, in dem jemand Unity
   oeffnete und einen Menuepunkt anklickte. Die Szenendatei enthielt weiterhin nur
   `MainMenuBrandingCanvas`, `GameLogo_Baked` und `TapToStartText`.
   Gemeldet wurde es als **"wieso hast du die Menüeinträge entfernt?"**, und von aussen ist das
   die einzig moegliche Lesart: ein Bildschirm ohne Menue und ein Bildschirm, dessen Menue nie
   gebaut wurde, sind dasselbe Bild. Dieselbe Ununterscheidbarkeit wie in den Fehlern 20, 27, 28
   und 42 - zum fuenften Mal - nur liegt die Ursache diesmal nicht im Spiel, sondern in der
   AUSLIEFERUNG.
   Die Lehre steht schon in diesem Dokument, eine Ueberschrift weiter: "Most work on this project
   happens where Unity cannot run." Daraus folgt die Regel "Assets durch Editor-Werkzeuge
   autorisieren" - und die gilt fuer ASSETS, fuer Prefabs und Szenen-YAML, die man nicht von Hand
   schreiben darf. Sie auf einen ganzen BILDSCHIRM anzuwenden macht aus einer Vorsichtsmassnahme
   eine Sackgasse: was ein Werkzeug erzeugt, existiert erst nach dem Klick, und wer nicht klickt,
   bekommt den alten Stand ohne einen einzigen Hinweis darauf, warum.
   Also gehoert die Frage vor jede Aenderung an etwas Sichtbarem: **wodurch erreicht das den
   Bildschirm?** Wenn die Antwort "jemand klickt in Unity" lautet, ist die Arbeit nicht
   ausgeliefert, sondern nur vorbereitet. Die Konstruktion liegt jetzt zur Laufzeit, in einer
   Klasse, die ein Bauteil aufruft, das ohnehin schon in der Szene steht - das Werkzeug ruft
   DENSELBEN Bauer auf, statt eine zweite Kopie zu behalten (Fehler 1), und ist damit nur noch
   eine Bequemlichkeit: man sieht das Menue ohne Play.
   Und eine Sache am Rande, die teuer haette werden koennen: ein Bauer, der zur Laufzeit
   `AddComponent` aufruft, loest das `Awake` der Komponente SOFORT aus - eine Zeile bevor er ihr
   die Zeilen uebergeben kann. `Bind` durfte deshalb nicht nur Felder schreiben, sondern musste
   neu verdrahten. Fehler 25 und 27, ein drittes Mal, und das Symptom waere das teuerste gewesen,
   das dieses Projekt kennt: ein Menue, das perfekt gezeichnet wird und auf nichts antwortet.

   **Nachtrag, und das ist die eigentliche Lehre.** Der Laufzeit-Bauer war die Antwort auf die
   falsche Haelfte der Frage. Er loeste "existiert der Bildschirm" und schuf dabei ein neues
   Problem: niemand konnte ihn mehr anfassen. Wer ein Menue GESTALTEN will, braucht Objekte, die
   in der Szene stehen - anklickbar in der Hierarchie, verschiebbar in der Szenenansicht, mit
   einem Textfeld im Inspector, das beim naechsten Start noch so dasteht. Code, der beim Laden
   irgendetwas davon neu erzeugt, nimmt genau das wieder weg.
   Beide Antworten waren also einseitig, und die richtige trennt die Zustaendigkeit statt sie zu
   verschieben: **die Szene ist die Gestaltung, C# ist das Verhalten.** Ein Editor-Werkzeug
   schreibt die Objekte EINMAL hinein; danach gehoeren Text, Schrift, Groesse und jedes
   RectTransform dem Menschen, und die Navigation entscheidet nur noch, welche Zeile gewaehlt ist
   und was ein Druck tut. Was von Fehler 47 bleibt, ist die eine Pflicht, die keine der beiden
   Architekturen aufhebt: ein Bildschirm, der fehlt, muss es SAGEN. Der Laufzeit-Pruefer nennt
   deshalb den Befehl, der ihn schreibt - denn "nichts passiert und nirgends steht warum" war nie
   die Architektur, sondern das Schweigen.

48. **Ein `using`, das nicht eine Datei gekostet hat, sondern das ganze Spiel.** Gemeldet wurden
   ZWEI Dinge: "das Platzieren des DOTS funktioniert nicht mehr" und "im Menue steht wieder nur
   Tap to Start". Zwei Systeme, die keine einzige Codezeile teilen - und der Commit davor hatte
   nachweislich KEINE Datei unter `Equipment/`, `Player/` oder `Interaction/` angefasst.
   Genau das ist der Befund: zwei unabhaengige Systeme koennen nicht gleichzeitig von einer
   Aenderung kaputtgehen, die keines von beiden beruehrt - es sei denn, gar kein Skript laeuft
   mehr. Ein Compilerfehler faellt nicht die Datei, in der er steht, sondern die ganze Assembly,
   und von innen sieht das aus wie zehn gleichzeitig kaputte Features.
   Die Ursache war ein `using TMPro;` ohne `#if TMP_PRESENT || UNITY_TEXTMESHPRO`. Jede andere
   Datei im Projekt, die TMP anfasst, hat diesen Schutz - `RuntimeUIFactory` fuenfmal, `UITheme`
   sechsmal - und `TMP_PRESENT` ist hier in keiner ProjectSettings-Zeile und in keinem asmdef
   gesetzt. Die Konvention stand also sechsmal sichtbar daneben und wurde uebergangen, weil der
   Nutzer ausdruecklich `TextMeshProUGUI` verlangt hatte und `Assets/TextMesh Pro/` im Projekt
   liegt - beides stimmt und beides beweist nicht, dass der Namensraum aufloest.
   Und der Stub hat mitgelogen, weil er dazu gebracht wurde: die TMPro-Typen sind FUER DIESE
   AENDERUNG in `UnityStub.cs` geschrieben worden, und danach war der Typecheck gruen. Das ist
   Fehler 9 in seiner reinsten Form - der Stub war die einzige Instanz, die zugestimmt hat, und
   er hat zugestimmt, weil ich ihn dafuer geschrieben habe. Der Beweis sieht jetzt andersherum
   aus: die TMPro-Typen sind WIEDER AUS dem Stub raus, und das Projekt compiliert trotzdem - erst
   das zeigt, dass die Abhaengigkeit wirklich weg ist.
   Die Lehre ist nicht "TMP ist gefaehrlich". Sie ist: eine Konvention, die im Projekt mehrfach
   identisch dasteht, ist eine ANTWORT auf eine Frage, die schon einmal gestellt wurde. Wer
   daran vorbeibaut, muss die Frage kennen, nicht nur die Abweichung wollen. Und wer zwei
   unzusammenhaengende Systeme gleichzeitig ausfallen sieht, sucht nicht zwei Fehler, sondern
   die eine Ebene, auf der sie zusammenhaengen.

## Den Stand holen

Unity schreibt bei jeder Sitzung an getrackten Dateien herum - Materialien, ProBuilder-Settings,
neu serialisierte Szenen - und `git pull --rebase` bricht dann mit "Sie haben Aenderungen, die
nicht zum Commit vorgemerkt sind" ab. Das ist kein Fehler im Repository und war bisher jedes Mal
nur Unity-Rauschen. Der Befehl dafuer, fertig zum Kopieren:

```
git checkout -- . && git pull --rebase origin claude-work
```

Das verwirft NUR Aenderungen an getrackten Dateien. Nicht getrackte Dateien - frisch importierte
Modelle, Texturen, alles noch nicht Hinzugefuegte - bleiben unberuehrt.

Wenn von Hand etwas geaendert wurde, das behalten werden soll, stattdessen:

```
git stash push -u && git pull --rebase origin claude-work && git stash pop
```

**Diesen Befehl unaufgefordert mitliefern**, wenn nach einem Push zum Pullen aufgefordert wird -
die Frage ist in dieser Zusammenarbeit schon viermal gestellt worden.

## Unity Editor availability

Most work on this project happens where Unity cannot run. When it cannot:

- Author assets through **editor tools** (`Editor/*Builder.cs`), not by
  hand-writing prefab or scene YAML. A prefab is a graph of cross-referencing
  documents; hand-writing one is how references get silently broken.
  ScriptableObject `.asset` files are the exception — a single document with
  one script reference is safe to write and can be verified by parsing it.
- Preserve `.meta` GUIDs. Never regenerate one to fix an import.
- Mark every runtime claim **NOT TESTED**. Do not say a thing works because
  it compiles.

## Layout

```
Assets/CatchIfYouCan/
  Scripts/        22 subsystem folders; see the ownership table
  Editor/         menu items and asset builders
  Definitions/    checked-in ScriptableObject assets
  Prefabs/        build products of the editor tools
  Resources/      the content registry, and materials that keep shaders alive
  Scenes/         00_Boot 01_MainMenu 02_Lobby 02_Training 03_Investigation
  Scenes/Development/  the nine DEV_ labs, never in a build
  Shaders/        five custom shaders, each with a material under Resources
Docs/             the documents above
Scripts/          the CI guards
```
