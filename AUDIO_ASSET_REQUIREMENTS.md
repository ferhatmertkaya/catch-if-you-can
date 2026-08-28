# Audio Asset Requirements

V1 runs with **ProceduralAudioSynth fallbacks** until real assets are imported. Use this guide when sourcing CC0 / royalty-free audio.

Always verify license before import and record each file in [AUDIO_ASSET_LICENSES.md](AUDIO_ASSET_LICENSES.md).

---

## Footsteps (Player) — min **8** variations

| Intended filenames | `footstep_wood_walk_01.wav` … `footstep_wood_walk_08.wav` |
|--------------------|-----------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Foley/Footsteps/Wood/` |
| Search keywords | `footsteps wood floor`, `shoes creaky floor`, `indoor footsteps` |
| Libraries | Sonniss GDC (`footstep wood`), Kenney Impact Sounds, Freesound CC0 (`footsteps wooden`), Pixabay (`footsteps floor`) |
| Notes | Mono, short one-shots (~0.1–0.3 s), 22050 or 44100 Hz |

Additional surface sets (optional V1+): `footstep_carpet_*`, `footstep_tile_*` (8 each).

---

## Doors — min **5** variations each action

| Intended filenames | `door_wood_open_01.wav` … `_05.wav`, `door_wood_close_01.wav` … `_05.wav`, `door_wood_slam_01.wav` … `_05.wav` |
|--------------------|----------------------------------------------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Foley/Doors/` |
| Search keywords | `door creak open`, `wood door close`, `door slam wood`, `old house door` |
| Libraries | Sonniss GDC (`door wood`), Freesound CC0 (`door creak`), Kenney |
| Notes | Mono preferred for spatial playback |

---

## Wood Creaks / House Settling — min **15** variations

| Intended filenames | `house_creak_01.wav` … `house_creak_15.wav` |
|--------------------|---------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Ambience/RoomTone/` or `Assets/CatchIfYouCan/Audio/Foley/Creaks/` |
| Search keywords | `house creak`, `wood settling`, `floor squeak`, `attic creak`, `old building` |
| Libraries | Sonniss GDC (`creak wood`), Freesound CC0, Pixabay |
| Notes | Sparse one-shots; avoid looping unless marked as room tone |

---

## Ghost Whispers — min **15** variations

| Intended filenames | `ghost_whisper_01.wav` … `ghost_whisper_15.wav` |
|--------------------|-------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Ghost/Whispers/` |
| Search keywords | `whisper horror`, `breath whisper`, `ghost voice`, `EVP`, `distant whisper` |
| Libraries | Freesound CC0 (check human voice license), Sonniss (`whisper`) |
| Notes | Mono, forceToMono on import; keep subtle levels |

---

## Ghost Footsteps — min **8** variations

| Intended filenames | `ghost_footstep_01.wav` … `ghost_footstep_08.wav` |
|--------------------|---------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Ghost/Footsteps/` |
| Search keywords | `slow footsteps`, `barefoot floor`, `creepy footsteps`, `distant steps` |
| Libraries | Sonniss GDC, Freesound CC0, Pixabay |
| Notes | Slower tempo than player steps; spatial mono |

---

## Impacts / Knocks — min **8** variations

| Intended filenames | `impact_wood_01.wav` … `impact_wood_08.wav`, `knock_door_01.wav` … `_08.wav` |
|--------------------|-------------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Ghost/Impacts/` |
| Search keywords | `knock door`, `thud wood`, `bang wall`, `impact muffled` |
| Libraries | Kenney Impact, Sonniss, Freesound CC0 |
| Notes | Used for ghost events and furniture interaction |

---

## UI — min **3** variations per type

| Intended filenames | `ui_click_01.wav` … `_03.wav`, `ui_confirm_01.wav` … `_03.wav`, `ui_back_01.wav` … `_03.wav` |
|--------------------|-----------------------------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/UI/` |
| Search keywords | `ui click`, `button tap`, `menu select`, `soft click` |
| Libraries | Kenney UI Audio, Freesound CC0, Pixabay |
| Notes | DecompressOnLoad; mono OK |

Journal stingers: `ui_journal_open_01.wav`, `ui_journal_close_01.wav`, `ui_evidence_found_01.wav`.

---

## Equipment — beeps, scans, toggles

| Intended filenames | `emf_beep_01.wav`, `uv_toggle_01.wav`, `camera_shutter_01.wav`, `evp_static_loop.wav`, `flashlight_click_01.wav` |
|--------------------|--------------------------------------------------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Equipment/` |
| Search keywords | `EMF beep`, `camera shutter`, `radio static`, `flashlight click`, `scanner beep` |
| Libraries | Sonniss, Kenney, Freesound CC0 |
| Notes | Short UI-like clips use DecompressOnLoad |

---

## Ambience — Exterior (stereo OK)

| Intended filenames | `amb_exterior_wind_loop.wav`, `amb_rain_loop.wav`, `amb_distant_traffic_loop.wav` |
|--------------------|-----------------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Ambience/Exterior/` |
| Search keywords | `wind loop`, `rain ambience`, `suburban night`, `crickets loop` |
| Libraries | Sonniss GDC ambience, Pixabay, Freesound CC0 |
| Notes | Streaming import; stereo allowed (`forceToMono` false) |

---

## Ambience — Room Tone (mono preferred)

| Intended filenames | `roomtone_hum_01.wav`, `roomtone_fridge_01.wav`, `roomtone_vent_01.wav` |
|--------------------|------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Ambience/RoomTone/` |
| Search keywords | `room tone`, `refrigerator hum`, `HVAC`, `electrical hum` |
| Libraries | Sonniss, Freesound CC0 |
| Notes | Streaming; mono preferred for RoomTone paths |

---

## Music

| Intended filenames | `music_menu_loop.wav`, `music_investigation_tension_loop.wav`, `music_hunt_stinger.wav` |
|--------------------|---------------------------------------------------------------------------------------|
| Folder | `Assets/CatchIfYouCan/Audio/Music/` |
| Search keywords | `horror ambient music`, `dark drone loop`, `investigation tension` |
| Libraries | Pixabay Music, Free Music Archive (verify license), Sonniss (check terms) |
| Notes | Streaming import |

---

## Import Pipeline

Files placed under `Assets/CatchIfYouCan/Audio/` are auto-configured by `CatchIfYouCanAudioImportProcessor`:

- **Ambience/** — Streaming, Vorbis ~0.7; stereo for Exterior, mono for RoomTone
- **Ghost/** — mono; short clips DecompressOnLoad
- **Foley/Footsteps/** — mono, DecompressOnLoad
- **UI/** — DecompressOnLoad
- **Equipment/** — DecompressOnLoad
- **Music/** — Streaming

Warnings logged once per batch if uncompressed estimate > 5 MB or sample rate is not 22050/44100/48000.

---

## Editor Menus

| Menu | Purpose |
|------|---------|
| Catch If You Can → Audio → Build Audio Mixer | Creates `AudioMixerConfig.asset` + mixer README |
| Catch If You Can → Audio → Generate Default Audio Events | Bakes procedural clips + `AudioEventDefinition` assets |
| Catch If You Can → Audio Debugger | Play-mode audio inspection |
| Catch If You Can → Setup Project | Ensures audio folders + default events |

Until real assets replace procedurals, the game remains fully playable using synthesized fallbacks.
