# CATCH IF YOU CAN — iOS Deployment

Build the iOS Xcode project on macOS and run it on a device.

---

## Requirements

- macOS
- Unity **6000.5.10f1** (Unity 6.5), installed through Unity Hub
- Unity module: **iOS Build Support**
- Xcode 15 or newer
- An Apple Development signing team configured in Xcode

---

## "Unity 6000.5.10f1 not found"

This message from `BuildIOS.sh` means no Unity Editor was found under
`/Applications/Unity/Hub/Editor/`. It is a missing installation, not a broken
project.

Install Unity Hub from <https://unity.com/download>, then install Unity
6000.5.10f1 with the iOS Build Support module ticked.

If Unity is installed somewhere else, point the script at it:

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/<your-version>/Unity.app/Contents/MacOS/Unity" ./BuildIOS.sh
```

List the editors the machine has:

```bash
ls /Applications/Unity/Hub/Editor
```

`BuildIOS.sh` prefers 6000.5.10f1 and will fall back to another Unity 6.x
install with a warning.

---

## Build

Three routes; all three produce the same Xcode project in `Builds/iOS/`.

**Double-click** — `START_HERE_MAC.command` in Finder. The first time, use
right-click → Open, because the file is unsigned.

**Terminal**

```bash
cd <repository>
chmod +x BuildIOS.sh START_HERE_MAC.command OpenInUnity.sh
./BuildIOS.sh
```

**From the Unity GUI** — if the headless build fails, open the project and
build from the menu:

```bash
./OpenInUnity.sh
```

Then in Unity: **Catch If You Can → 5. BUILD → iOS**.

> `Catch If You Can → 9. ENTWICKLER - DEBUG → Migration → Setup Project` is a
> bulk migration command that rewrites project settings and assets. It is not
> part of a normal build and asks for confirmation before it runs. You do not
> need it to build.

Build logs are written to `Builds/Logs/`.

---

## Xcode → iPhone

1. Open `Builds/iOS/` in Xcode.
2. Signing & Capabilities → select your Apple Development team.
3. Confirm the bundle identifier is `com.catchifyoucan.game`.
4. Select the device and press **Run**.

---

## Player Settings

| | |
|---|---|
| Product | CATCH IF YOU CAN |
| Bundle identifier | `com.catchifyoucan.game` |
| Version | 1.0.0 |
| Minimum iOS | 15 |
| Architecture | ARM64 |
| Scripting backend | IL2CPP |
| Graphics | Metal |
| Orientation | Landscape |

Safe area insets are handled at runtime by `SafeAreaFitter`. V1 requests no
microphone permission.

---

## Helper Scripts

| Script | What it does |
|---|---|
| `START_HERE_MAC.command` | Finder entry point; runs the build with a readable error if Unity is missing. |
| `BuildIOS.sh` | Headless build to `Builds/iOS/`. Honours `UNITY_BIN`. |
| `OpenInUnity.sh` | Opens the project in the Unity GUI without batch mode. |
