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
| `Docs/AGENT_OWNERSHIP.md` | Always, if anyone else is working in this repo at the same time. Who owns what, and the files no two agents may touch at once. |
| `Docs/DETERMINISM.md` | **Normative.** Any change under `Scripts/Procedural/**` or to the deterministic set. A violation is a bug even if it looks right in the editor. |
| `Docs/DEVELOPMENT_LABS.md` | Working on, or in, one of the nine `DEV_` lab scenes. |
| `Docs/UNITY_VALIDATION.md` | Claiming that something works. |
| `Docs/NETWORKING.md` | Anything multiplayer. No netcode package is installed yet. |
| `Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md` | **Normative for the boundaries.** Who owns what, and why the pose is never replicated. |
| `Docs/GHOST_EVIDENCE_AUTHORITY.md` | **Normative.** Any change to what counts as evidence, or to who decides it. |
| `Docs/CROSSPLAY_PLATFORM_MATRIX.md` | Adding a platform, or anything tempted to branch on one. |
| `Docs/AGENT_OWNERSHIP.md` + `Docs/AGENT_ROSTER.json` | **Always.** Who owns what, the 40 specialist roles, and the 19 protected hotspots. |
| `Docs/AGENT_TASK_ROUTER.md` | **Normative for how work is assigned.** Routing, the handoff contract, the review matrix, the blocked-domain rule. |
| `Docs/PLATFORM_QUALITY_TIERS.md` | Anything that would differ between PC, console and mobile. Gameplay never differs; presentation may. |
| `Docs/PERFORMANCE_BUDGETS.md` | Before writing any performance number. Nothing is MEASURED yet, and saying it is fabricates evidence. |

**Two session modes, chosen and never inferred.** Offline solo is exactly one
local player with no dependency on anything outside the device — no
Authentication, Lobby, Relay, transport or account, and the whole mission loop
works in airplane mode. Online is 1–8 players: one host plus up to seven clients,
the host occupying one of the eight. `MultiplayerProtocol.MaxPlayers` is the only
place that number lives; everything else derives it.

## The rules that are enforced, not trusted

- `Scripts/check_determinism.sh` — the deterministic set stays pure and the
  layout hash stays stable. 158 checks, all must pass.
- `Scripts/check_dev_scenes.sh` — no `DEV_` scene is ever enabled in the build
  list.
- `Scripts/check_equipment_catalog.sh` — the eleven items keep their runtime
  paths, the journal cannot prove evidence, nothing sweeps the scene per
  frame, no equipment id is looked up by string literal, no held item hides
  `HeldEquipmentBase`'s per-frame methods, and the flashlight's model and material
  paths resolve to real files, an item is not given its visual before it knows what it is,
  a failed model load never becomes a silent placeholder, and the flashlight's diagnostic
  pose ships switched off, a model's size is measured in its own space rather than as a
  world AABB, and the achieved size is checked against the wanted one. 43 checks.
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
  torn rectangular breach built from a signed box field whose edge is chewed away by noise, a
  closed portal draws no lit pixel at all so the wall reads as whole, two independent noise
  layers drive it, every
  artistic control is a shader property that the material and the C# agree on, the portal camera
  stays culled and distance-gated, nothing allocates a buffer or a material per frame, quality comes
  from the project's own quality level rather than a parallel tier system, particles emit on the breach
  edge, there is exactly one shadowless light, and a failed preparation visibly collapses the
  portal instead of hiding it. It also keeps entry the player's: the portal never loads a scene
  itself, entry commits from exactly one call site behind a plane-side crossing whose sign must
  actually change and whose crossing point is inside the aperture, no trigger volume can commit
  entry, a crossing needs a prepared destination, a refused crossing returns the controls, the
  intro overlay is cleared whatever happens, and the first-person hand target, elbow hint and
  anchor agree on one side with the fist clear of the near clip plane, and the probe room
  stays outside the playable world without ever making the portal enterable, and the lobby's
  authored doorway is filled so the wall is a wall until a portal tears it. It also holds the
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
  ladder has named ends rather than a halved top. 128 checks.

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
  actually use, and every model a visual profile names really exists under `Resources` with a non-zero forward axis. 48 checks.

- `Scripts/check_project_tags.sh` — every tag and layer the code names really exists.
  Assigning an undefined tag throws and takes the rest of that build down with it; an
  undefined layer name returns -1 and says nothing. `Environment` was assigned by two
  runtime factories, compared by the NavMesh source filter and assigned by the prop
  builder while being defined nowhere, and `LightSwitch` was assigned one statement
  before the switch got its component. Both are checked now, along with the editor
  setup's ability to restore them. 25 checks.

All eight run in CI (`.github/workflows/determinism.yml`). Run them locally before
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
