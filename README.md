# CATCH IF YOU CAN

CATCH IF YOU CAN is a first-person paranormal-investigation horror game built
with Unity 6.5. Players investigate haunted locations, deploy specialised
paranormal equipment, collect evidence, survive hostile manifestations, and
identify the entity.

**The game is under active development.** The sections below mark each system
as IMPLEMENTED, PARTIAL or PLANNED. Nothing here is a promise about a shipped
build.

---

## Current Technology

| | |
|---|---|
| Unity | 6.5 — `6000.5.10f1` (exact version required) |
| Render pipeline | Universal Render Pipeline (URP) 17.5.0, Forward+ |
| Scripting backend | IL2CPP |
| Language | C# |
| Targets | iOS and Android; desktop is a development convenience, not a design target |
| Orientation | Landscape, touch-first |

Mobile is the primary platform. Where PC and mobile differ, gameplay stays
identical and only presentation changes — see `Docs/PLATFORM_QUALITY_TIERS.md`.

---

## Core Systems

| System | Status | Notes |
|---|---|---|
| First-person player, look, crouch, sprint | IMPLEMENTED | Rigged first-person body with procedural motion. |
| Touch HUD and mobile controls | IMPLEMENTED | `MobileInputController` is the only thing in gameplay code that reads a key. |
| Interaction and pickup | IMPLEMENTED | Ray-based; a refused pickup names its reason. |
| Equipment inventory | IMPLEMENTED | Three investigation slots plus a dedicated torch slot. |
| Equipment items | IMPLEMENTED | Eleven: flashlight, EMF detector, UV light, thermometer, EVP recorder, parabolic microphone, photo camera, spectral grid (DOTS) projector, video camera, warding relic, salt. |
| Equipment placement | IMPLEMENTED | Wall and floor devices with a live preview. |
| Evidence system | IMPLEMENTED | Equipment reports observations; a validator decides what is proved. See `Docs/GHOST_EVIDENCE_AUTHORITY.md`. |
| Ghost framework | IMPLEMENTED | State machine, hunt AI, per-entity evidence signatures. |
| Procedural house generation | IMPLEMENTED | Two-stage: a deterministic engine-free layout, then geometry from a modular catalog. See `Docs/DETERMINISM.md`. |
| Room furnishing | IMPLEMENTED | Function-driven, with measured keep-out zones. See `Docs/ROOM_FURNISHING.md`. |
| Lobby and portal handover | IMPLEMENTED | The lobby doorway renders the real prepared mission world and hands the player through without a scene load. |
| Audio | PARTIAL | Event-driven architecture is in place; `ProceduralAudioSynth` stands in until real clips are imported. |
| Save / progression | PARTIAL | JSON `SaveManager` exists; progression content is thin. |
| Mission definitions | PARTIAL | The Suburban House vertical slice is solvable end to end; further missions are data work. |
| HQ interior migration | PARTIAL | Rooms build from a modular catalog; the purchased pack is per-seat licensed and not in this repository. |
| Multiplayer | PLANNED | Architecture, ownership boundaries and a capacity contract (1–8 players) are designed and guarded, but **no netcode package is installed** and no session has ever connected. See `Docs/NETWORKING.md`. |

---

## Art Direction

Realistic Victorian paranormal horror: abandoned residential interiors,
believable proportions, consistent PBR materials under URP, mobile-conscious
geometry.

Asset selection, structure, naming and the criteria a pack has to meet are in
**[ASSET_USAGE.md](ASSET_USAGE.md)**.

---

## Requirements

- Unity **6000.5.10f1** (Unity 6.5) — exact version, see `Docs/UNITY_VALIDATION.md`
- Modules: Android Build Support, iOS Build Support, Universal RP
- Android SDK / NDK for Android builds
- Xcode 15+ on macOS for iOS builds

---

## Open the Project

1. Clone the repository.
2. Unity Hub → **Add** → select the repository folder.
3. Open with Unity **6000.5.10f1**.
4. Wait for package resolve (URP, Input System, AI Navigation; TextMeshPro ships inside uGUI).
5. Open `Assets/CatchIfYouCan/Scenes/00_Boot.unity` and press Play.

Some purchased packs are per-seat licensed and are not in the repository, so a
fresh clone will show unresolved references for them. That absence is expected
and recorded — see [ASSET_USAGE.md](ASSET_USAGE.md).

> **Ghost visuals need one rebuild.** Ghost prefabs are build products and are not
> committed, so every ghost spawns as the `DEV_PLACEHOLDER` capsule until they are
> generated — the game logs a one-time warning naming the command. Run
> **Catch If You Can → 9. ENTWICKLER - DEBUG → Migration → Integrate External Assets**
> once after opening the project. It also re-wires THE MIMICER and THE STATIC, whose
> definitions still point at prefabs that were removed with the Kenney kit.

---

## Scenes

| Scene | Purpose |
|-------|---------|
| `00_Boot` | Splash → managers → Main Menu |
| `01_MainMenu` | Menu, and the interactive lobby room the mission is entered from |
| `02_Training` | Short tutorial house |
| `03_Investigation` | Procedural house and the ghost loop |

Flow: Boot → Main Menu → Mission Select → the lobby portal → Investigation.

The nine `DEV_` lab scenes under `Scenes/Development/` are never in a build;
`Scripts/check_dev_scenes.sh` enforces that.

---

## Controls

**Mobile (landscape)**

- Left: virtual joystick (move)
- Right drag: look
- Interact: bottom right
- Equipment: three slots, plus a dedicated torch button
- Crouch / Sprint / Journal: HUD buttons; sprint is hold, with Auto-Sprint in Settings

**Keyboard (Editor)**

- WASD move, mouse look, Shift sprint
- **E** or **X** interact — pick up, commit a wall placement, take a mounted device back off
- **G** power — a deployed device takes the press first, otherwise the torch
- 1–3 equipment slots

---

## Builds

Build commands live under **Catch If You Can → 5. BUILD**.

**Android** — `Build Android Development` or `Build Android Release`, output in
`Builds/Android/`. Package `com.catchifyoucan.game`, min API 24, target API 35,
ARM64, IL2CPP, landscape. No internet permission is required.

**iOS** — see **[DEPLOY_IOS.md](DEPLOY_IOS.md)**.

---

## Repository Layout

```
Assets/CatchIfYouCan/
  Scripts/        24 subsystem folders
  Editor/         menu commands and asset builders
  Art/            production art, by category
  Definitions/    checked-in ScriptableObject assets
  Prefabs/        build products of the editor tools
  Resources/      content registry, and everything loaded by path
  Scenes/         00_Boot 01_MainMenu 02_Training 03_Investigation
  Scenes/Development/   the nine DEV_ labs, never in a build
  Shaders/        custom shaders, each with a material under Resources
Assets/External/  third-party packs that ship with the repository
Docs/             architecture, determinism, ownership and workflow docs
Scripts/          the CI guards
```

---

## Editor Tools

Commands sit in named groups under the **Catch If You Can** menu, and every one
carries a risk tag saying what it changes — `[NUR LESEN]` is read-only and safe
to click. `Docs/EDITOR_WORKFLOWS.md` is normative for the menu; a full
inventory is in `Docs/EDITOR_MENU_INVENTORY.md`.

The four commands that can rewrite the project go through a single confirmation
that states scope, count, whether it reimports, and whether it saves scenes.

---

## Enforced Rules

Twelve shell guards under `Scripts/` run in CI (`.github/workflows/determinism.yml`)
and enforce what would otherwise be trusted: determinism and layout-hash
stability, the equipment catalog, multiplayer boundaries, UI and portal
architecture, the HQ environment migration, asset references, project tags,
the editor menu, room furnishing, the vertical slice, the agent roster, and the
dev-scene exclusion.

They need nothing but a shell (and `python3` for some). Run them before pushing:

```
for g in Scripts/check_*.sh; do bash "$g" || echo "FAILED: $g"; done
```

`CLAUDE.md` is the working contract for this repository — what each guard
protects, and the mistakes the project has already made and must not repeat.

---

## Audio

Horror audio is event-driven (`AudioEventLibrary`, spatial emitters, tension and
snapshot directors), with procedural fallbacks until real clips are imported.

| Doc | Purpose |
|-----|---------|
| [AUDIO_ASSET_REQUIREMENTS.md](AUDIO_ASSET_REQUIREMENTS.md) | Filename patterns, search terms, minimum variation counts |
| [AUDIO_ASSET_LICENSES.md](AUDIO_ASSET_LICENSES.md) | Attribution table for imported audio |
| [Assets/CatchIfYouCan/Audio/README_AUDIO.md](Assets/CatchIfYouCan/Audio/README_AUDIO.md) | Category checklist |

Press **F9** in Editor or Development builds for the audio overlay.

---

## Attribution

Licences and attribution: **[THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md)**,
**[AUDIO_ASSET_LICENSES.md](AUDIO_ASSET_LICENSES.md)**.
Asset pipeline and art direction: **[ASSET_USAGE.md](ASSET_USAGE.md)**.

---

## Version

- Product: CATCH IF YOU CAN
- Bundle identifier: `com.catchifyoucan.game`
- App version: `1.0.0` (Player Settings)
