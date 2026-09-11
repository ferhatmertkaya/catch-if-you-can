# Agent ownership

**New to the repository? Read `PROJECT_OVERVIEW.md` first** - what the game is, how the
code is arranged, which systems exist and which are deliberately not built yet. This
document is the next one: it says who may write what.

Status: normative for parallel work. Applies to every agent, human or
otherwise, working on this repository at the same time as another one.

This document exists because the failure mode of parallel work on a Unity
project is not a merge conflict. Merge conflicts are loud and get fixed. The
failure is two agents each building a correct answer to the same question in
two places — two flashlights, two inventories, two hand anchors, two ideas of
what the player is holding — and both of them merging cleanly.

That has already happened here twice. `FlashlightEquipment` and
`HeldFlashlight` were both flashlights, and only one of them was ever in the
player's hand. `EquipmentManager` and `PlayerInventory` both owned a hand
anchor and both raised `EquipmentChanged`, so which one decided where an item
went depended on who asked. Neither was a conflict. Both were a merge.

---

## 1. The rule

> Every file in this repository has exactly one **primary owner** team. Only
> the primary owner changes it. Anyone else opens a request.

A team may *read* anything. Reading another team's code to understand it is
encouraged; that is how the duplicates above would have been avoided.

The unit of ownership is a folder, not a file, because a file that moves
between folders would otherwise change owner silently.

---

## 2. The teams

Twenty-one teams, mapped to the folders they own. The counts are the number of
`.cs` files in the folder, and are here only to say how big a surface each team is
holding. 368 runtime scripts, 47 editor scripts and 8 shaders in total.

| # | Team | Owns | Files | What it is responsible for |
|---|------|------|-------|----------------------------|
| 1 | **Core** | `Scripts/Core/**` | 20 | Service location, scene identity, scene installers, the local-player service, logging. The spine everything else hangs off. |
| 2 | **Player** | `Scripts/Player/**` | 17 | The player rig, spawner, factory, controller, look, inventory, body motion, fear. |
| 3 | **Character** | `Scripts/Character/**` | 4 | Character definitions, rig profiles, the catalog and the local selection. |
| 4 | **Equipment** | `Scripts/Equipment/**` | 38 | Held equipment, the loadout, definitions, the catalog, presentation and the runtime factory. |
| 5 | **Interaction** | `Scripts/Interaction/**` | 14 | `IInteractable` and every implementation of it, plus the interaction controller. |
| 6 | **Ghost** | `Scripts/Ghost/**` | 23 | Ghost definitions, behaviour, perception, hunts, rig control, visuals. |
| 7 | **AI** | `Scripts/AI/**` | 2 | Navigation and pursuit shared by ghosts and anything else that moves itself. |
| 8 | **Procedural** | `Scripts/Procedural/**` | 61 | House generation, the deterministic core, layout hashing, bootstrap. **See §4.** |
| 9 | **Audio** | `Scripts/Audio/**` | 53 | Ambience, zones, occlusion, reverb, footsteps, the hunt mix. |
| 10 | **UI** | `Scripts/UI/**` | 32 | Every screen, the touch HUD, the runtime UI factory, the theme. |
| 11 | **Input** | `Scripts/Input/**` | 9 | Touch, joystick, look, the HUD's input side and the input controller. |
| 12 | **Art / Rendering** | `Scripts/Art/**`, `Scripts/Graphics/**`, `Shaders/**` | 23 | Shader resolution, runtime materials, the mirror, post-processing. |
| 13 | **Environment** | `Art/Environment/**`, `Art/Particles/**`, `Scripts/Environment/**` | 9 | Authored rooms, props, doors, materials and textures; the lobby portal and its crossing; the lobby's own atmosphere and equipment table. The hand-built `ReferenceApartment` shell that used to stand in for the mission world is GONE - the portal opens onto the real generated house, and a guard fails if a stand-in comes back. |
| 14 | **Evidence** | `Scripts/Evidence/**` | 7 | Evidence types, the evidence manager and what counts as proof. |
| 15 | **Missions** | `Scripts/Missions/**`, `Scripts/Objectives/**` | 17 | Mission definitions, runtime, objectives and their completion. |
| 16 | **Electronics** | `Scripts/Electronics/**` | 2 | `IElectronicDevice`, interference and the breaker. |
| 17 | **Weather** | `Scripts/Weather/**` | 1 | Weather state and its particle systems. |
| 18 | **Save / Content** | `Scripts/Save/**`, `Scripts/Content/**` | 10 | Save data, the content registry, and what ships. |
| 19 | **Development** | `Scripts/Development/**`, `Scenes/Development/**` | 13 | The nine labs and the lab framework. See `DEVELOPMENT_LABS.md`. |
| 20 | **Tooling** | `Editor/**`, `Scripts/Utilities/**`, repo-root `Scripts/*.sh`, `.github/workflows/**` | 47 | Editor menus, asset builders, validators, the CI guards. |
| 21 | **Session / Networking** | `Scripts/Session/**` | 10 | The join handshake, the match config, the authority provider and the gameplay-facing session API. Owns the boundary that keeps Relay out of gameplay — see `MULTIPLAYER_RUNTIME_ARCHITECTURE.md`. |

Anything not listed is **Core**'s until a team claims it in a change to this
table.

---

## 3. Working across a boundary

When a change needs another team's file:

1. **Read their code first.** Most cross-boundary changes turn out to be
   unnecessary once you have read what is already there.
2. **Prefer an addition on your own side.** A new method on your class that
   calls their existing public API costs nothing to review.
3. **If you need something from them, ask for the smallest thing.** "Expose
   the beam light" is a request another team can accept in a minute.
   `HeldFlashlight.Beam` and `FearSystem.SetFlashlight` are both exactly this.
4. **Never add a second implementation because the first is inconvenient.**
   That is the failure this whole document is about. If the existing one is
   wrong, say so and fix it in place.
5. **Never reach into another team's private state by reflection.** It
   compiles, it passes review, and it fails silently the day they rename the
   field. The one exception is development labs, where it is confined to
   `WireLabField` and a break is loud and local.

---

## 4. Single-owner hotspots

Some files are dangerous for two agents to touch at once regardless of who
owns the folder. These have **one owner and no shared editing**, ever.

| File or area | Owner | Why it is a hotspot |
|---|---|---|
| `Scripts/Procedural/` deterministic set | Procedural | Every client must build a bit-identical world from a seed. A change that looks correct in the editor can still desynchronise a session. Governed by `DETERMINISM.md`, which is normative, and guarded by `Scripts/check_determinism.sh`. |
| `Scripts/Player/PlayerFactory.cs` | Player | The eye height, eye forward, capsule height and visual scale live here as literals, and every one of them is a number someone measured. Two agents adjusting the camera from two directions is how the camera ends up in the neck. |
| `Scripts/Player/PlayerBodyMotion.cs` | Player | The arm pose. It hands out the grip that every held item is laid on, so a change here moves every item in the game at once. |
| `Scripts/Equipment/HeldEquipmentBase.cs` | Equipment | Carrying, aiming and dropping for every item. Same reason. |
| `Scripts/Core/CiycServices.cs` | Core | The list of services every scene installs. Adding one here changes the startup of every scene in the project. |
| `Scripts/UI/RuntimeUIFactory.cs` | UI | Builds the whole HUD in code, including the screen-space rectangles. Two agents adding a button is two buttons on top of each other. |
| `ProjectSettings/**` | Tooling | Render pipeline, input handling, build settings, always-included shaders. A change here is invisible in a diff review and total in effect. |
| `Assets/CatchIfYouCan/Scenes/**` (`.unity`) | Whoever owns the scene | Scene YAML is a graph of cross-referencing documents. Two agents editing one scene produces a file that merges and does not open. Prefer code-built fixtures — see `DEVELOPMENT_LABS.md` §2. |
| `.meta` files | The file's owner | A regenerated GUID silently detaches every reference to the asset. Never delete or recreate a `.meta` to "fix" an import. |
| `Scripts/Equipment/EquipmentPresentation.cs` | Equipment (spec. 12) | Lays every held item on the grip it is handed. It does not recompute the grip, and a second offset applied here is how there came to be three. |
| `Scripts/Player/PlayerInventory.cs` | Equipment (spec. 9) | Three investigation slots plus the torch's own place, and the only file where equipment ownership is claimed and released, and the only file that writes `EquipmentBase.Carrier`. Claiming in a second place hands everybody's spare kit to the first person past; folding the torch back into the three leaves the fourth tool of the vertical slice with nowhere to go; a second writer of the carrier is a second answer to "which bag holds this", and an item that is both installed on a wall and the occupant of a slot comes back into the player's hand on the next slot change (CLAUDE.md mistake 49). |
| `Scripts/Ghost/GhostController.cs` | Ghost AI (spec. 13) | Every ghost decision path, each gated on `SessionAuthority.CanSimulateGhost`. Four machines each deciding is four ghosts wearing one transform. |
| `Scripts/Ghost/GhostStateMachine.cs` | Ghost AI (spec. 13) | Entering a state runs the host's decisions for it. `AdoptReplicatedState` exists so a client can take the state without running them. |
| `Scripts/Evidence/EvidenceValidator.cs` | Evidence (spec. 16) | What counts as proof. Three back doors round it have already been found and closed. Governed by `GHOST_EVIDENCE_AUTHORITY.md`. |
| `Scripts/Procedural/Deterministic/MultiplayerProtocol.cs` | Multiplayer (spec. 34) | `MaxPlayers = 8` and the protocol version. The only place the capacity lives; a second constant is a second answer. |
| `Scripts/Session/MultiplayerSessionService.cs`, `Scripts/Core/SessionAuthority.cs` | Multiplayer (spec. 34) | Installing a session also sets who decides. Two places doing that is two parts of the game disagreeing about who the host is. |
| `Scripts/Procedural/Deterministic/GenerationVersion.cs` | Procedural (spec. 20) | Changing it makes every existing build incompatible with itself. Never changed for a networking reason. |
| `Scripts/Art/MirrorCorner.cs`, `Shaders/PlanarMirror.shader` | Mirror (spec. 23) | The mirror plane is captured once and never rotates toward the player. Reverting to an off-axis frustum brings the portal effect back. |
| `Scripts/UI/UITheme.cs` | UI (spec. 29) | Every colour, weight and typeface in the game. Three separate bugs lived here at once: the brand green used as border, fill and hover, so every screen was green; `ColorBlock.fadeDuration` left unset, so every button lagged its press by 0.1 s; and no typeface at all. Guarded by `Scripts/check_ui_and_portal.sh`. |
| `Scripts/UI/MenuInputGate.cs` | UI (spec. 29) | The one owner of "a fullscreen menu is up". Two screens each restoring the touch HUD on their way out is how the joystick came back underneath a menu; only the last holder releasing can be sequenced correctly. |
| `Scripts/Environment/LobbyPortal.cs`, `Scripts/Art/PortalSurface.cs`, `Shaders/Portal.shader` | Environment (spec. 18) + VFX (spec. 24) | The doorway is a live second camera and an oblique clip plane. Accepting a mission opens it; walking through it starts the investigation. A second portal system, or a scene load put back on the accept path, removes the doorway entirely. |
| `Scripts/Missions/MissionWorldLoader.cs`, `Scripts/Procedural/InvestigationBootstrap.cs` | Procedural (spec. 20) + Environment (spec. 18) | The mission world is loaded additively behind the lobby and generated from `MissionRuntime.Seed` — the seed rolled once, before the portal opened. A second seed here, or gameplay started in `PrepareWorld`, means the player looks into one world and plays another, or is hunted while reading a noticeboard. |
| `Scripts/UI/TouchHudFactory.cs` + `Scripts/Player/PlayerFactory.cs` | UI (spec. 29) | Authoritative for movement, look, sprint, crouch, flashlight and carry, and the only place a `MobileInputController` or a `VirtualJoystick` is built. `RuntimeUIFactory` built a second set of all of it; which one drove the game depended on load order. |
| `Scripts/Save/SaveManager.cs` | Save (spec. 33) | Local persistence with no online dependency, permanently. A format change without a migration eats a save. |
| `Scripts/Procedural/ModularRoomBuilder.cs` | Procedural (spec. 20) | Stage B geometry for EVERY room in the game - floors, walls, doorways, windows, trim and, since it was found to place none, the room's light. It consumes the layout and never produces one, and it derives its wall-mesh variants by hashing the room's identity rather than drawing them, because a draw would advance a generation stream and reach back into Stage A. |
| `Scripts/Environment/HouseLightingDirector.cs`, `Scripts/Environment/AmbientRoomLight.cs` | Environment (spec. 18) | Dresses and rolls every room's practical from the mission seed, derived locally so a lighting tweak never becomes a determinism change. `AmbientRoomLight` is the one distinction it makes: a marked light is scenery, never dressed, never rolled off, and not what the wall switch owns. Two places deciding which a light is means one of them wires the ember to the switch. |
| `Scripts/Interaction/DraggableDoor.cs` + `Scripts/Interaction/InteractiveDoor.cs` | Interaction (spec. 11) | Two components on ONE hinge. The swinging one only writes while it animates, so it leaves a dragged leaf alone and then believes a stale angle; `SyncToAngle` is how the drag tells it where the leaf ended up. They share the hinge transform and the limit, because two answers to "how far does this door open" diverge the first time somebody changes one. |
| `.gitattributes`, `Scripts/lfs_debt.txt` | Tooling | The git-lfs boundary. The rules are listed one file at a time, so every new model and texture starts OUTSIDE lfs and stays there unless somebody adds a rule - three FBX files of 87-92 MiB arrived that way, against GitHub's 100 MiB hard refusal. `lfs_debt.txt` records what is already in the pack; `check_asset_references.sh` fails on the next one. |

---

## 5. Things no team may do without agreement

- **Install a package.** It changes `Packages/manifest.json`, every other
  agent's compile, and often the build size. Netcode, Addressables, Animation
  Rigging and Cinemachine are all in this category.
- **Add a second inventory, loadout, flashlight, spawner or input router.**
  If the existing one does not fit, change the existing one.
- **Introduce a `Resources.Load` with a new string path.** The content
  registry exists so content is reached by reference. The exceptions are
  deliberate and documented in place: `CIYC_ContentRegistry` itself, and
  `Resources/Materials`, which exists so custom shaders survive build
  stripping.
- **Add a built-in-render-pipeline shader fallback.** `Standard` and
  `Particles/Standard Unlit` resolve everywhere and draw magenta under URP.
  Ask `Art.CiycShaders` and accept null.
- **Claim a Play Mode result without Unity having run.** Say NOT TESTED.
- **Enable a `DEV_` scene in the build list.** Guarded by
  `Scripts/check_dev_scenes.sh`, which fails CI.

---

## 5b. The specialist layer (V6)

Teams own **folders**. Specialists own **concerns**. Both are real and neither
replaces the other: three specialists — Equipment Architecture, Investigation
Device and Equipment Presentation — all work inside the Equipment team's folder.

> The **team** rule says who may write the file.
> The **specialist** rule says whose judgement the change needs.

Forty specialists are defined in **`Docs/AGENT_ROSTER.json`**, which is the
machine-readable source and carries, for each one: mission, owns, may_read,
protected_files, forbidden_changes, required_reviewers, preferred_dev_lab,
validators and escalation_rules. Every role names the `team` whose folder it
writes in.

`Scripts/check_agent_architecture.sh` fails if the roster and this document
drift apart.

| Group | Roles |
|---|---|
| Leadership / architecture | 1 Main / Lead Architect · 2 Core Architecture · 3 Performance · 4 QA / Validation |
| Player / character | 5 Player Controller · 6 Character / Rig · 7 Animation · 8 First-Person Hands / IK |
| Equipment | 9 Equipment Architecture · 10 Investigation Device · 11 Defensive Equipment · 12 Equipment Presentation |
| Ghost / horror | 13 Ghost AI · 14 Hunt System · 15 Ghost Event · 16 Evidence · 17 Ghost Visual |
| World / environment | 18 Environment / Level · 19 Interaction · 20 Procedural Generation · 21 Lighting · 22 Shader / Material · 23 Mirror / Reflection · 24 VFX / Atmosphere |
| Audio | 25 Audio Architecture · 26 Sound Design · 27 Ambience · 28 Footstep / Player Audio |
| UI / progression | 29 UI / HUD · 30 Menu / Lobby UX · 31 Journal · 32 Progression / Economy · 33 Save / Settings |
| Network | 34 Multiplayer Architecture · 35 Netcode / Transport **(BLOCKED)** · 36 Online Services **(BLOCKED)** · 37 Crossplay · 38 Platform / Build |
| Content / tools | 39 Art / Prop Pipeline · 40 Editor Tools / Dev Lab |

**These are development roles, not runtime objects.** There is no
`AgentManager`, no `AgentService`, no agent `GameObject`, and there must not
be. Nothing under `Assets/` reads the roster.

How a task is routed, what a specialist must hand back, and the cross-agent
review matrix are in **`Docs/AGENT_TASK_ROUTER.md`**.

---

## 5c. A protected hotspot is not "never edit"

It means four things, and all four are required:

1. the **owning specialist participates**;
2. a cross-domain change gets its **reviewer** from the router's §4 matrix;
3. the **existing invariant is stated** before the change, not after;
4. the **relevant guards run**, and the static baseline is diffed rather than
   counted — a falling error count is early termination, not success.

---

## 6. What to do when this document is wrong

Change it in the same commit as the code that makes it wrong. A stale
ownership table is worse than none: it is a table people trust.
