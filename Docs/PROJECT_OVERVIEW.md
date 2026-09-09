# CATCH IF YOU CAN — project overview

**Read this first if you are new to the repository.** It is the map: what the game is,
how the code is arranged, which systems already exist, what is deliberately not built
yet, and the handful of rules that are enforced rather than trusted.

Everything below is a description of what is in the repository today. Where something is
planned rather than built, it says so.

---

## 1. What the game is

A first-person paranormal-investigation horror game. You enter a house, work out which
entity is in it from the evidence it leaves, and get out. Co-op is the intended shape of
the game; solo has to work completely offline.

| | |
|---|---|
| Engine | Unity **6000.5.10f1** |
| Render pipeline | **URP 17.5.0**, Forward+ |
| Scripting backend | **IL2CPP** |
| Primary target | **Mobile (iOS + Android).** Desktop is a development convenience, not the platform being designed for. |
| Language | C#, ~368 runtime scripts + 47 editor scripts + 8 custom shaders |

Mobile-first is not a slogan here. It decides real things: shadow-casting lights are
rationed, secondary cameras share a frame budget, particle systems are capped, and
anything that would sweep the scene per frame gets rejected in review.

---

## 2. Repository layout

```
Assets/CatchIfYouCan/
  Scripts/        24 subsystem folders (see §3)
  Editor/         menu items, asset builders, validators — 47 files
  Definitions/    checked-in ScriptableObject assets
  Prefabs/        build products of the editor tools
  Resources/      the content registry, and materials that keep shaders alive
  Scenes/         00_Boot  01_MainMenu  02_Lobby  02_Training  03_Investigation
  Scenes/Development/   nine DEV_ labs, never in a build
  Shaders/        8 shaders, each with a material under Resources
Docs/             the normative documents (see §8)
Scripts/          12 CI guard scripts (shell + python3, no Unity needed)
```

Two scenes matter most for gameplay: **`01_MainMenu`** (which also contains the playable
lobby) and **`03_Investigation`** (the mission world, loaded additively).

---

## 3. The subsystems

Ownership is by folder. Each folder has exactly one owning team, and the rule is that only
the owner writes it — everybody may read anything. `Docs/AGENT_OWNERSHIP.md` is the full
table; this is the short version, with the current file counts.

| Folder | Files | What lives there |
|---|---|---|
| `Procedural/` | 61 | House generation, the deterministic core, layout hashing, the mission bootstrap |
| `Audio/` | 53 | Ambience, zones, occlusion, reverb, footsteps, the hunt mix |
| `Equipment/` | 38 | The eleven tools, the loadout, definitions, presentation, the runtime factory |
| `UI/` | 32 | Every screen, the touch HUD, the runtime UI factory, the theme |
| `Ghost/` | 23 | Ghost definitions, behaviour, perception, hunts, rig control, visuals |
| `Art/` | 22 | Shader resolution, runtime materials, the mirror, post-processing |
| `Core/` | 20 | Service location, scene identity, the local-player service, logging |
| `Player/` | 17 | Rig, spawner, factory, controller, look, inventory, body motion, fear |
| `Interaction/` | 14 | `IInteractable` and every implementation, plus the interaction controller |
| `Development/` | 13 | The nine labs and the lab framework |
| `Session/` | 10 | Join handshake, match config, authority provider, the session API |
| `Objectives/` | 9 | Objectives and their completion |
| `Input/` | 9 | Touch, joystick, look, the input controller |
| `Environment/` | 9 | The lobby portal, lobby atmosphere, the equipment table, mission world loading |
| `Missions/` | 8 | Mission definitions, runtime, the world loader |
| `Evidence/` | 7 | Evidence types, the manager, the validator, what counts as proof |
| `Content/` | 6 | The content registry — what ships |
| `Save/` | 4 | Local persistence, no online dependency |
| `Character/` | 4 | Character definitions, rig profiles, the catalog |
| `Utilities/` `Electronics/` `AI/` `Weather/` `Graphics/` | 1–3 each | Small, single-purpose |

---

## 4. Player and gameplay systems

### 4.1 The player rig

Built entirely in code by `PlayerRigBuilder` / `PlayerFactory` — there is no player prefab
to drag into a scene. The hierarchy is:

```
Player                CharacterController, PlayerController, PlayerInventory,
                      InteractionController, FearSystem, PlayerNoiseEmitter
 ├─ CameraRoot        PlayerLook  (pitch pivot; yaw is on Player)
 │   ├─ HandAnchor    where holstered items hang
 │   └─ CameraBreath  CameraIdleMotion  (breathing, glances)
 │       └─ MainCamera
 └─ VisualRoot        the character model, PlayerBodyMotion, PlayerVisualAnimator
```

`PlayerFactory` holds measured literals — eye height, eye forward (0.21 m), capsule height,
visual scale — and they are measured, not guessed. It is a protected hotspot.

**Movement and camera.** Walk / sprint / crouch on a `CharacterController`. Crouch shrinks
the capsule and drops the camera on the *same smoothed value*, so the view and the collider
never disagree about how tall the player is. Look is yaw on the body, pitch on `CameraRoot`.

**First-person body.** The player has a real body, visible when looking down, driven by
`PlayerBodyMotion`: crouch fold (legs, then a spine lean), sprint lean, strafe, idle sway,
head/neck pitch following the camera, a per-finger grip solve around whatever is held, and
blinks. All of it is procedural on top of the animation clips, applied in `LateUpdate`.

**Input.** `MobileInputController` is the single source: touch joystick, look area, and
the HUD buttons, with keyboard equivalents for desktop development. There is exactly one
of it, exactly one movement joystick and exactly one transition overlay — that is
CI-enforced, because a second set of any of them once existed and which one drove the game
depended on load order.

### 4.2 Equipment

Eleven tools, all with real runtime implementations:

`flashlight`, `emf_detector`, `uv_light`, `thermometer`, `evp_recorder`,
`parabolic_microphone`, `photo_camera`, `spectral_grid`, `video_camera`,
`warding_relic`, `salt`

`PlayerInventory` holds **three investigation slots plus a dedicated place for the torch**,
so carrying a light does not cost you a tool. Items are built from `EquipmentDefinition`
assets through `EquipmentRuntimeFactory`; anything without a runtime path becomes an
explicitly-labelled `DEV_PLACEHOLDER` rather than a silent grey box.

`HeldEquipmentBase` handles carry, aim and drop for every item; `EquipmentPresentation`
lays each item onto the grip the body motion hands it. Both are hotspots — a change there
moves every item in the game at once.

### 4.3 The lobby and the portal

The lobby is where you pick a mission and kit up. It is a hand-built room in
`01_MainMenu`, and it contains the **portal** — the single most unusual system in the
project, and worth understanding before touching anything nearby.

Pressing START INVESTIGATION does **not** load a scene. It:

1. opens a torn hole in the lobby wall (a custom shader, a normalised radial field with
   noise-chewed edges),
2. loads `03_Investigation` **additively, behind the lobby**, and generates the mission
   world from the seed rolled at that moment,
3. renders that real world through the hole with a second camera using an **oblique near
   clip plane** placed on the wall surface,
4. and when you walk through, **hands the player over** — the same rig, moved between
   scenes, mapped through the portal pair's matrices. No fade, no loading screen, no
   respawn. Thrown objects cross too, with their velocity and spin rotated through the
   pair.

The hole is real: the wall's *collision* is cut (physics only, the mesh is untouched) and
restored the moment the portal is not open.

### 4.4 Evidence and identification

`EvidenceValidator` decides what counts as proof, and it is deliberately hard to get
around — three back doors into it have been found and closed. Selecting an entity in the
journal is *not* an answer; the identification goes through
`MissionManager.SubmitIdentification`, once, and a second answer is refused.

`Docs/GHOST_EVIDENCE_AUTHORITY.md` is normative for this.

---

## 5. The ghost

This is the area most likely to be worked on next, so it gets its own section.

### 5.1 What exists

| File | Does |
|---|---|
| `GhostController.cs` | Every ghost decision path. Each one gated on `SessionAuthority.CanSimulateGhost`. |
| `GhostStateMachine.cs` | The states and the transitions between them |
| `GhostPerception.cs` | What the ghost can sense — player position, line of sight, noise |
| `GhostInteractionBrain.cs` | Choosing an object in the world to interact with |
| `GhostSpawnManager.cs` | Where the ghost lives and where it starts |
| `GhostEventDirector.cs` | Timed and triggered activity |
| `GhostActivitySystem.cs` | The activity level that drives how often anything happens |
| `GhostDisturbance.cs` | Physical disturbances — objects, doors, lights |
| `GhostPerception` + `AI/NoiseManager.cs` | Noise events the ghost can hear |
| `GhostDefinition.cs` / `GhostDefinitionFactory.cs` | Entity types and their evidence signatures |
| `GhostRigController.cs`, `GhostVisualCatalog.cs`, `GhostSpectralReveal.cs`, `GhostOrb.cs` | How it looks and when it is visible |

The nine states in `GhostState.cs`:

```
Dormant → Roaming → Investigating → Manifesting → Interacting
                 → Event → Hunting → Searching → Cooldown
```

### 5.2 The one rule that matters here

> **Ghost decisions are host-only.** A client is told the ghost's *state*, never its
> *reasoning*.

`GhostStateMachine.AdoptReplicatedState` exists precisely so a client can take a state
without running the decisions that produced it. Four machines each deciding is four ghosts
wearing one transform — and it is also how a cheating client would learn where the ghost is
before it manifests.

This is checked by `Scripts/check_multiplayer_architecture.sh` and documented in
`Docs/GHOST_EVIDENCE_AUTHORITY.md`.

---

## 6. Multiplayer — where it actually stands

**Be clear about this before planning any work here.**

### What is built

- **Two session modes, chosen explicitly and never inferred.** Offline solo is exactly one
  local player with *no* dependency on anything outside the device — no authentication, no
  lobby, no relay, no account. The whole mission loop works in airplane mode. Online is
  1–8 players: one host plus up to seven clients, the host occupying one of the eight.
- **`MultiplayerProtocol.MaxPlayers = 8`** is the only place that number lives. Everything
  else derives it.
- **The authority boundary.** `SessionAuthority` answers who decides — ghost simulation,
  evidence validation, equipment ownership. `MultiplayerSessionService` installs a session
  and sets authority in the same act, so the two cannot disagree about who the host is.
- **Local vs remote players are the same rig.** A remote player uses the same
  `PlayerController`, animator and body motion; it just is not driven by this machine's
  input. `PlayerPresentationState` carries movement, crouch, sprint and pitch —
  **the pose itself is never replicated.**
- **`PlayerPresence`** answers "who is here"; `LocalPlayerService` answers "which one is
  mine". Asking the second one the first one's question is a documented past bug.

### What is NOT built

- **No netcode package is installed.** No Netcode for GameObjects, no Mirror, no Fish-Net,
  no transport, no relay, no lobby service. Nothing in the repository opens a socket.
- The architecture is *shaped* for netcode — the seams, the authority checks, the
  replication contracts and the diagnostics all exist and are enforced — but the wire is
  missing on purpose. Choosing and installing it is a decision that has not been made.
- Installing a package is explicitly something no team does without agreement, because it
  changes `Packages/manifest.json`, everyone's compile, and the build size.

`Docs/NETWORKING.md` and `Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md` are normative here.
Read the second one before touching a boundary.

---

## 7. World generation — two stages, one of them deterministic

This trips people up, so it is worth stating plainly.

**Stage A** decides the *layout* — how many rooms, of what category, where, which walls
carry doors — from the mission seed alone, in an **engine-free assembly** (no `UnityEngine`
types at all), and folds the result into a **layout hash**. Every client must build a
bit-identical world from the same seed.

**Stage B** reads that layout and places geometry. `ModularRoomBuilder` assembles floors,
walls, doorways, windows and trim from a `ModularInteriorCatalog`. Changing the art changes
Stage B only, so the same seed keeps producing the same house.

Where Stage B still has a choice — which of three interchangeable wall meshes this wall
gets — it is **derived** by hashing the room's identity, never drawn from a `CiycRandom`
stream. A draw would advance that stream and reach back into generation.

`Docs/DETERMINISM.md` is normative. A violation is a bug even when it looks right in the
editor, and `Scripts/check_determinism.sh` (148 checks) will fail on it.

### Art assets

The house interior comes from a **purchased modular pack** (HQ Modular House), which is
**not in the repository** — per-seat licence, and the LFS payload is already at GitHub's
free limit. The pack is gitignored; `Docs/VENDOR_ASSET_MANIFEST.txt` records which GUIDs
belong to it, so a missing pack is a *named, expected* state rather than a broken
reference. **You need the pack installed to open the main menu scene.**

The old Kenney interior pack has been removed entirely, along with everything that pointed
at it — no production file names a Kenney path, and a guard enforces that it cannot come
back.

---

## 8. The rules that are enforced, not trusted

**12 shell guards, 848 checks, all green, all run in CI** (`.github/workflows/determinism.yml`).
They need nothing but a shell and `python3` — no Unity, no packages.

```
bash Scripts/check_determinism.sh              148   the deterministic set stays pure
bash Scripts/check_ui_and_portal.sh            274   UI, portal, lobby, crossing, handover
bash Scripts/check_hq_environment.sh           165   the environment migration
bash Scripts/check_vertical_slice.sh            61   the slice stays solvable
bash Scripts/check_multiplayer_architecture.sh  50   authority boundaries
bash Scripts/check_equipment_catalog.sh         42   the eleven items
bash Scripts/check_editor_menu.sh               30   editor menu structure and scale
bash Scripts/check_room_furnishing.sh           28   room furnishing rules
bash Scripts/check_project_tags.sh              24   every tag and layer really exists
bash Scripts/check_agent_architecture.sh        13   the role roster
bash Scripts/check_asset_references.sh          13   assets and git-lfs
bash Scripts/check_dev_scenes.sh                     no DEV_ scene ships
```

**Run them before you push.** They are fast, they need no setup, and they encode roughly
two dozen bugs this project has already made and does not intend to make again — those are
written up in `CLAUDE.md` under "The mistakes this project has already made", and reading
that list is the single fastest way to understand why the code looks the way it does.

A few of the standing rules:

- **No built-in-pipeline shader fallback.** `Shader.Find("Standard")` resolves everywhere
  and draws solid magenta under URP. Ask `Art.CiycShaders` and accept null — a missing
  object beats a magenta one.
- **No new `Resources.Load` string path.** Content is reached by reference through
  `Content.CiycContentRegistry`. A path that has never existed once made every ghost in
  the game a primitive capsule, silently, for months.
- **No second implementation because the first is inconvenient.** There were two
  flashlights and two inventories once. Both merged cleanly and only one of each was real.
- **No reflection into another class's private fields.** It compiles, it reviews clean, and
  it fails silently on the next rename. Ask for a public method.
- **Say NOT TESTED.** Do not claim a Play Mode result Unity has not produced.

---

## 9. Documents

Normative — read before changing the area:

| Document | Covers |
|---|---|
| `AGENT_OWNERSHIP.md` | Who owns what, and the protected hotspots |
| `DETERMINISM.md` | The deterministic set and the layout hash |
| `MULTIPLAYER_RUNTIME_ARCHITECTURE.md` | The authority boundaries, and why the pose is never replicated |
| `GHOST_EVIDENCE_AUTHORITY.md` | What counts as evidence, and who decides |
| `NETWORKING.md` | Multiplayer plans and the state of the netcode decision |
| `ROOM_FURNISHING.md` | Who decides what stands in a room (German) |
| `HQ_MODULAR_MIGRATION.md` | The measured contract of the purchased house pack |
| `EDITOR_WORKFLOWS.md` | The editor menu, and which commands are safe to click (German) |
| `UNITY_VALIDATION.md` | What "it works" is allowed to mean |
| `PLATFORM_QUALITY_TIERS.md` | What may differ between platforms (gameplay never does) |
| `PERFORMANCE_BUDGETS.md` | **Nothing is measured yet.** Do not write a performance number as if it were. |

---

## 10. Getting it running

1. Clone, and make sure **git-lfs** content is fetched:
   `git lfs fetch --all origin && git lfs checkout`
2. Install the **HQ Modular House** pack into `Assets/HQ Modular House/`. Without it,
   `01_MainMenu` opens with missing prefabs — that is expected, not broken.
3. Open with Unity **6000.5.10f1**. Other versions have not been tried.
4. Play from `00_Boot`.
5. Before pushing: `for g in Scripts/check_*.sh; do bash $g; done`

Desktop development keys: **WASD** move, **Shift** sprint, **C** crouch, **E** interact,
**F** flashlight, **Tab** journal, **X** take/drop (development builds only).
