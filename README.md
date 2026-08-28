# CATCH IF YOU CAN

First-person ghost investigation horror for mobile (Android / iOS).

Built for **Unity 6.3 LTS** with **URP**, C#, landscape orientation, and touch-first controls.

## Requirements

- Unity **6000.3.x** (6.3 LTS)
- Android SDK / NDK (for Android builds)
- Xcode 15+ on macOS (for iOS builds)
- Modules: Android Build Support, iOS Build Support, Universal RP

## Open Project

1. Clone or copy the `CatchIfYouCan` folder.
2. Open Unity Hub → **Add** → select `CatchIfYouCan`.
3. Open with Unity 6.3 LTS.
4. Wait for package resolve (URP, Input System, AI Navigation, Addressables, TextMeshPro).
5. Menu: **Catch If You Can → Setup Project** (includes CC0 asset integration)
6. Optional: **Catch If You Can → Integrate External Assets** (rebuild prefabs from bundled Kenney/Quaternius models)
7. Open scene `Assets/CatchIfYouCan/Scenes/00_Boot.unity` and press Play.

Bundled assets: Kenney Furniture (120 FBX), Kenney Mini Dungeon, Quaternius rigged monsters. See **`ASSETS_NUTZUNG.md`** (German).

`SceneAutoSetup` wires Boot / Main Menu / Training / Investigation at runtime even if scene YAML script GUIDs are incomplete.

## Run

| Scene | Purpose |
|-------|---------|
| `00_Boot` | Splash → managers → Main Menu |
| `01_MainMenu` | PLAY, EQUIPMENT, JOURNAL, TRAINING, SETTINGS, CREDITS |
| `02_Training` | Short tutorial house |
| `03_Investigation` | Van + procedural house + ghost loop |

Play mode flow: Boot → Main Menu → Mission Select → Investigation.

## Controls (Mobile Landscape)

- **Left**: virtual joystick (move)
- **Right drag**: look
- **Interact / Use**: bottom-right
- **Equipment slots**: 3 slots above Use (tap / swipe)
- **Crouch / Sprint / Journal / Flashlight**: HUD buttons
- Sprint is hold (Auto-Sprint in Settings)

Keyboard fallback (Editor): WASD, mouse look, E interact, Shift sprint, 1–3 equipment.

## Android Build

1. **Catch If You Can → Setup Project**
2. Player Settings already target:
   - Package: `com.catchifyoucan.game`
   - Min API 24, Target API 35
   - ARM64, IL2CPP (Release)
   - Landscape
3. **Catch If You Can → Build Android Development** or **Build Android Release**
4. Output: `Builds/Android/`

Offline-first: no internet permission required for V1.

## iOS Build

1. **Catch If You Can → Build iOS**
2. Open generated Xcode project under `Builds/iOS/`
3. Set signing team, build to device
4. Landscape + Metal + IL2CPP
5. Safe Area handled via `SafeAreaFitter`
6. No microphone permission in V1

## Architecture

```
Assets/CatchIfYouCan/
  Scripts/Core, Player, Input, Interaction, Equipment,
           Ghost, AI, Procedural, UI, Audio, Save, Missions...
  Prefabs/, Scenes/, Shaders/, ScriptableObjects/, Input/
```

Key systems:

- `ProceduralHouseGenerator` — seeded modular house (6–14 rooms)
- `GhostController` + state machine + hunt AI
- Data-driven `EquipmentDefinition` / `GhostDefinition` / `MissionDefinition`
- JSON `SaveManager` progression
- `RuntimeUIFactory` builds playable UI without hand-authored Canvas prefabs

## Adding a Ghost

1. Create asset: **Create → Catch If You Can → Ghost Definition**  
   or extend `GhostDefinitionFactory.CreateAllDefaultGhosts()`
2. Set 3 evidence types, personality, visual profile
3. Run **Setup Project** or place under `ScriptableObjects/`

## Adding Equipment

1. Create `EquipmentDefinition` ScriptableObject
2. Create prefab with component inheriting `EquipmentBase` (e.g. `EMFDetector`)
3. Assign definition + hand pose
4. Unlock via shop / `SaveData.UnlockedEquipmentIds`

## Adding a Room

1. Build prefab with `RoomModule` + `RoomSocket`s
2. Create `RoomDefinition` with category + prefab variants
3. Assign to generator room definition list  
   Without prefabs, `PrimitiveRoomFactory` still generates playable rooms.

## Adding a Map Theme

1. Create `MissionDefinition` (theme, difficulty, reward, recommended gear)
2. Add to Mission Select list / `MissionDefinitionFactory`
3. Investigation scene reuses the same generator — no new giant scene required

## Editor Tools

| Menu | Action |
|------|--------|
| Setup Project | Layers, tags, SOs, build scenes, URP check, audio folders/events |
| Generate Placeholder Prefabs | Primitive props / gear / player / ghost |
| Validator | Missing scripts, broken refs, sockets |
| Generate 100 Houses | Seed validation report |
| Build Android / iOS | BuildPipeline wrappers |
| **Audio → Build Audio Mixer** | Mixer config + manual `.mixer` guide |
| **Audio → Generate Default Audio Events** | Procedural clip + event assets |
| **Audio Debugger** | Play-mode snapshot, tension, occlusion stats |

## Audio System

Horror audio is event-driven (`AudioEventLibrary`, spatial emitters, tension/snapshot directors). V1 ships with **ProceduralAudioSynth** fallbacks until real clips are imported.

| Doc | Purpose |
|-----|---------|
| [AUDIO_ASSET_REQUIREMENTS.md](AUDIO_ASSET_REQUIREMENTS.md) | Filename patterns, search terms, min variation counts |
| [AUDIO_ASSET_LICENSES.md](AUDIO_ASSET_LICENSES.md) | Mandatory attribution table for imports |
| [Assets/CatchIfYouCan/Audio/README_AUDIO.md](Assets/CatchIfYouCan/Audio/README_AUDIO.md) | Category checklist + architecture pointer |

Runtime debug: press **F9** in Development/Editor builds for the audio overlay.

## Asset Attribution

See `FREE_ASSET_RECOMMENDATIONS.md`, `THIRD_PARTY_ASSETS.md`, and `Assets/External/README_IMPORT_ASSETS.md`.

V1 ships with **primitive placeholders** so the game is playable without downloads.

## Troubleshooting

**Scripts missing in scenes**  
Play still works via `SceneAutoSetup`. Run Setup Project, then re-save scenes.

**URP pink materials**  
Assign URP pipeline asset in Graphics Settings (created by Unity URP template or Project Settings → Graphics). `RuntimeMaterialFactory` builds runtime materials as fallback.

**NavMesh missing**  
`NavMeshRuntimeBuilder` bakes at generation time. If agent cannot path, ghost fail-safe warps to a valid point off-camera.

**House generation failed**  
Generator retries then falls back to known-good seed `424242`.

**Touch look fights UI**  
`MobileInputController` ignores look when pointer is over UI / left joystick half.

**Compile errors about URP**  
Ensure `com.unity.render-pipelines.universal` resolved in Package Manager.

## Version

- Product: CATCH IF YOU CAN
- Bundle: `com.catchifyoucan.game`
- App version: `1.0.0` (see Player Settings)
