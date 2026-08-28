# Audio Requirements

`AudioManager` tolerates missing clips (null-safe). V1 runs with **ProceduralAudioSynth** fallbacks until real assets are imported — see project root [AUDIO_ASSET_REQUIREMENTS.md](../../../AUDIO_ASSET_REQUIREMENTS.md) and [AUDIO_ASSET_LICENSES.md](../../../AUDIO_ASSET_LICENSES.md).

## Advanced Architecture

The runtime audio stack lives under `Assets/CatchIfYouCan/Scripts/Audio/`:

| Component | Role |
|-----------|------|
| `AudioManager` | Channel volumes, event playback, mixer/snapshot integration |
| `AudioEventLibrary` + `AudioEventDefinition` | Data-driven event IDs → clips |
| `ProceduralAudioSynth` | Runtime + editor-baked fallback clips |
| `AudioEmitterPool` / `SpatialAudioEmitter` | Pooled 3D one-shots with priority eviction |
| `AudioSnapshotController` | Hunt/tension/pause mix states (mixer or procedural) |
| `TensionAudioDirector` | Dynamic tension bed + snapshot transitions |
| `RoomToneController` + `ReverbZoneController` | Room-aware tone and reverb morphing |
| `AudioOcclusionController` + `AudioPortal` | Cross-room attenuation |
| `RuntimeAudioBusRouter` | Group mute/volume when no `.mixer` asset is assigned |
| `UiAudioService` / `JournalAudio` | UI feedback hooks |

### Editor Tooling

| Menu | Action |
|------|--------|
| Catch If You Can → Audio → Build Audio Mixer | `AudioMixerConfig.asset` + `Audio/Mixer/README_MIXER.md` |
| Catch If You Can → Audio → Generate Default Audio Events | Bakes `Audio/Generated/*.asset` + `ScriptableObjects/Audio/*.asset` |
| Catch If You Can → Audio Debugger | Play-mode inspector (snapshot, ghost, tension, occlusion) |
| Catch If You Can → Setup Project | Ensures folders + default events |

Import settings for paths under `Assets/CatchIfYouCan/Audio/` are applied automatically by `CatchIfYouCanAudioImportProcessor`.

Press **F9** in Development/Editor builds for the runtime `AudioDebugOverlay`.

## Categories to Provide

### Ambient
- House creaks
- Wind / rain loops
- Distant traffic
- Wood floor stress
- Pipes / fridge hum
- Clock tick (sparse)

### Ghost
- Whisper one-shots
- Distant cry
- Footsteps (slow)
- Knock / slam
- Manifestation swell
- Hunt heartbeat / drone

### Equipment
- EMF beep (loopable rate-driven preferred as one-shots)
- UV toggle
- Camera shutter
- EVP static / whisper responses
- Parabolic hiss
- Flashlight click
- Battery low click

### Player
- Footsteps walk / run
- Door open / close / slam
- Hide enter / exit
- Breath (high fear only)

### UI
- Button tap
- Journal open
- Evidence found pulse
- Mission complete / fail stingers
- Entity discovered sting

## Implementation Notes

- Prefer mono for UI, spatial mono/stereo for world SFX
- Keep ghost audio sparse — silence is part of the horror
- Name clips clearly, e.g. `SFX_Ghost_Whisper_01.wav`
- Assign via `AudioEventDefinition` assets or Inspector fields on managers
- First-run tip: “BEST EXPERIENCED WITH HEADPHONES”
