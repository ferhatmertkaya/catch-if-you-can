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
  paths, the journal cannot prove evidence, and nothing sweeps the scene per
  frame. 25 checks.
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
  become the player's. 48 checks.

- `Scripts/check_agent_architecture.sh` — the roster holds 40 unique roles with
  every field, the roster and `AGENT_OWNERSHIP.md` name the same roles, the
  hotspot policy and table survive, the handoff contract still demands preserved
  invariants, the blocked-domain rule still forbids fake netcode, no agent
  runtime object exists in the game, and `MaxPlayers` is still 8 in its one
  source. 13 checks.

- `Scripts/check_vertical_slice.sh` — the Suburban House stays solvable: its entity roster is
  named, every entity in it leaves evidence the four-tool kit can actually find, no two share
  an evidence signature, and the mission still recommends those four tools. 7 checks.

All seven run in CI (`.github/workflows/determinism.yml`). Run them locally before
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
10. **One value asked to mean two things.** `-1` was the offline player's client
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
