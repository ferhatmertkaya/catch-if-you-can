# CATCH IF YOU CAN — iOS Deploy v2

## Was ist neu in v2?

- `BuildIOS.sh` bevorzugt **6000.5.10f1**, akzeptiert notfalls andere Unity-6-Versionen (mit Warnung)
- Klare Fehlermeldung + öffnet Unity Hub / Anleitung
- Kein `zsh: no matches found` mehr bei fehlendem Xcode-Projekt
- `START_HERE_MAC.command` → Doppelklick im Finder
- `OpenInUnity.sh` → GUI-Fallback ohne Batchmode
- Input Actions Dateiendung korrigiert (`.inputactions`)

## Dein Fehler war

```
ERROR: Unity 6000.5.10f1 nicht gefunden.
```

Das heißt: Auf dem Mac ist (noch) **kein Unity Editor** unter  
`/Applications/Unity/Hub/Editor/...` installiert — nicht dass das Spiel kaputt ist.

## Ablauf (empfohlen)

### A) Einmalig Unity installieren

1. [Unity Hub](https://unity.com/download) installieren
2. **Unity 6.5 (6000.5.10f1)** installieren - exakt diese Version
3. Module anhaken: **iOS Build Support**
4. Xcode aus dem App Store (15+)

### B) Bauen

**Option 1 — Doppelklick**

`START_HERE_MAC.command` (Rechtsklick → Öffnen, beim ersten Mal)

**Option 2 — Terminal**

```bash
cd ~/Desktop/CatchIfYouCan
chmod +x BuildIOS.sh START_HERE_MAC.command OpenInUnity.sh
./BuildIOS.sh
```

**Option 3 — Nur GUI**

```bash
./OpenInUnity.sh
```

Dann in Unity:

1. **Catch If You Can → Setup Project**
2. **Catch If You Can → Build iOS**
3. `Builds/iOS` in Xcode öffnen

### C) Xcode → iPhone

1. Signing → dein Apple Team
2. Bundle ID: `com.catchifyoucan.game`
3. Device wählen → **Run**

## Unity liegt woanders?

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/DEINE_VERSION/Unity.app/Contents/MacOS/Unity" ./BuildIOS.sh
```

Alle Editoren auflisten:

```bash
ls /Applications/Unity/Hub/Editor
```

## Package

- Bundle: `com.catchifyoucan.game`
- iOS 15+, ARM64, IL2CPP, Landscape
- Produkt: CATCH IF YOU CAN 1.0.0
