# Unity Validation Procedure

Purpose: execute the validation that `Docs/DETERMINISM.md` §10 lists as **NOT
EXECUTED**. Everything below needs a real Unity Editor; none of it can be run in
a headless container.

What is already proven without Unity, and does **not** need repeating here: the
deterministic core builds under .NET, the 28-assertion harness passes, and
`Scripts/check_determinism.sh` passes. Those cover 24 of ~190 C# files. This
document covers the rest.

---

## 0. Which Unity Editor to install

```
ProjectSettings/ProjectVersion.txt
  m_EditorVersion:             6000.3.0f1
  m_EditorVersionWithRevision: 6000.3.0f1 (catchifyoucan)
```

**Install Unity `6000.3.0f1`.**

Required modules: **Android Build Support** (with OpenJDK + Android SDK/NDK) and,
on macOS, **iOS Build Support**.

### The revision hash is not recoverable — do not guess it

The second line should read `6000.3.0f1 (<12-hex-revision>)`. It instead contains
the literal string `catchifyoucan`. That is not a Unity revision, and the real one
cannot be recovered from anything in this repository.

Consequences, and what to do:

| Path | Effect |
|---|---|
| **Unity Hub install by version** | Works. Hub resolves `6000.3.0f1` itself and ignores the revision field. **Use this.** |
| Opening the project | Works. The Editor matches on `m_EditorVersion` and only warns about the revision. |
| CI actions that pin by revision (e.g. `game-ci` `unityVersion` + revision, direct `download.unity3d.com/…/<revision>/` URLs) | **Blocked.** They cannot resolve `catchifyoucan`. |

Do **not** substitute a plausible-looking hash. A wrong revision produces a CI job
that silently builds on a different Unity patch — precisely the environment drift
a cross-platform determinism test exists to detect. Recover the true revision from
whoever created the project, or pin by version string only.

**If Hub does not offer `6000.3.0f1`:** stop and tell us rather than opening with a
different patch. Opening with another version rewrites `ProjectVersion.txt` and
silently changes the toolchain under the determinism baseline. If you decide to
upgrade, that must be a deliberate, separately committed decision.

---

## 1. Open the project

1. Unity Hub → **Add** → select the repository root (the folder containing
   `Assets/`, `Packages/`, `ProjectSettings/`).
2. Set the editor version to `6000.3.0f1`.
3. Open. First import resolves packages and can take several minutes.
4. If a version-mismatch dialog appears, **cancel** and see §0.

Expected: the project opens with the Console clear of red entries.

---

## 2. Force a full script recompile

A clean import is not the same as a clean recompile — stale `Library/` artifacts
can hide a broken assembly.

**Preferred (guarantees a from-scratch compile):**

1. Close Unity.
2. Delete `Library/ScriptAssemblies/` (safe: regenerated on next open). Deleting
   the whole `Library/` folder also works but forces a full reimport.
3. Reopen the project.

**Quicker (usually sufficient):** in the Editor, **Right-click in Project window →
Reimport All**, or menu **Assets → Reimport All**.

**Verify the assemblies were actually produced:**

```
ls Library/ScriptAssemblies/
```

Expected to exist:

```
Assembly-CSharp.dll
Assembly-CSharp-Editor.dll
CatchIfYouCan.Procedural.Deterministic.dll
CatchIfYouCan.Determinism.Tests.dll
```

A missing `Assembly-CSharp.dll` means the runtime assembly failed to compile —
§3 will show why.

---

## 3. Detect Assembly-CSharp / Assembly-CSharp-Editor compile errors

**This is the step most likely to fail.** Neither assembly has ever been
compiled — all prior verification used .NET with hand-written UnityEngine stubs,
which is not evidence that they build.

1. Open **Window → General → Console**.
2. Enable **Error**, **Warning**, **Info**; disable **Collapse**.
3. Click **Clear**, then force a recompile (§2).
4. Read every red entry.

Compile errors appear as `Assets/…/File.cs(LINE,COL): error CSxxxx: message`.

**Also check the Editor log**, which captures compiler output the Console can
truncate:

| OS | Path |
|---|---|
| macOS | `~/Library/Logs/Unity/Editor.log` |
| Windows | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |
| Linux | `~/.config/unity3d/Editor.log` |

```bash
grep -nE "error CS|Compilation failed|Assembly-CSharp" ~/Library/Logs/Unity/Editor.log | head -50
```

**Pass condition:** zero `error CS` entries, and all four assemblies present.

Warnings do not block. Note any that name files under `Scripts/Procedural/`.

---

## 4. Run the determinism EditMode suite

1. **Window → General → Test Runner**.
2. **EditMode** tab.
3. Expect the assembly **`CatchIfYouCan.Determinism.Tests`** with **27 tests** under
   `CatchIfYouCan.Tests.DeterminismTests`.
4. **Run All**.

If the assembly does not appear: `com.unity.test-framework` (1.4.5) failed to
resolve, or `Assembly-CSharp` failed to compile. Fix §3 first.

**Pass condition: every test green.** These matter most:

| Test | Why |
|---|---|
| `Pcg32_MatchesPublishedReferenceVectors` | The RNG stream is what the golden hashes rest on |
| `E_UnityEngineRandom_CannotPerturbGeneration` | Only provable in the real engine |
| `E_GenerationDoesNotAdvanceUnityEngineRandom` | The converse; also engine-only |
| `F_GoldenSeeds_ReproduceRecordedHashes` | Unity's toolchain agrees with .NET |
| `DuplicatePropStableId_IsRejected` and the other duplicate-id tests | Never executed under NUnit |
| `G_PerturbingCollectionOrder_DoesNotChangeHash` | Hash canonicalisation |

**Batch mode alternative:**

```bash
"<UnityPath>" -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -assemblyNames CatchIfYouCan.Determinism.Tests \
  -testResults ./Builds/Logs/editmode-results.xml \
  -logFile ./Builds/Logs/editmode.log
```

Exit code `0` = all passed. Results XML names any failure.

---

## 5. Golden-seed validation

Menu: **Tools → Catch If You Can → Determinism → Validate Golden Seeds**

Checks all 24 committed entries (12 seeds × 2 maps) against
`GoldenSeedTable.cs`.

**Pass:** dialog reports all 24 reproduce; Console shows one info line.

**Fail:** each mismatch is logged as
`seed <N> (<MAP>): expected <HASH>, got <HASH>`.

A failure here means Unity's compiler produced different generation output from
.NET's — a genuine cross-toolchain divergence. **Do not run *Generate Golden
Seeds* to make it pass.** That overwrites the evidence. Send the Console output.

Related menu items (same path): *Compare Two Layouts* (reports the first
authoritative difference between two seeds), *Print Layout Report*, *Generate
Golden Seeds* (regenerate only after a deliberate `GenerationVersion` bump).

> Note: these Editor tools use `ContentSnapshot.CreateFallback()`, not the
> project's authored `PropDefinition`/`RoomDefinition` assets. They validate the
> generator, not the shipped content. §8 covers the authored-content path.

---

## 6. Android Development Build

Player settings are already correct in `ProjectSettings.asset` — **IL2CPP**
(`scriptingBackend.Android: 1`), **ARM64** (`AndroidTargetArchitectures: 2`),
min SDK 24, target SDK 35, `com.catchifyoucan.game`. The Development build
therefore already satisfies the IL2CPP requirement; no settings change is needed.

1. **File → Build Settings → Android → Switch Platform** (first switch is slow).
2. Confirm all four scenes are listed and enabled:
   `00_Boot`, `01_MainMenu`, `02_Training`, `03_Investigation`.
3. Menu: **Catch If You Can → Build Android Development**
   (sets `BuildOptions.Development | AllowDebugging`).

Output: `Builds/Android/CatchIfYouCan_dev.apk`

Install and capture logs:

```bash
adb install -r Builds/Android/CatchIfYouCan_dev.apk
adb logcat -c
adb logcat | tee android-run.log | grep -E "CIYC|Unity"
```

---

## 7. iOS Xcode build

macOS + Xcode 15+ required.

**Scripted:** `./BuildIOS.sh` — prefers an installed `6000.3.x`, runs
`CatchIfYouCanBuildMenu.BuildIOSBatch` in batch mode, logs to
`Builds/Logs/ios_build_<timestamp>.log`, and zips the export.

**Manual:** **File → Build Settings → iOS → Switch Platform**, then
**Catch If You Can → Build iOS**.

Either path applies `ConfigureIOSPlayerSettings()`: IL2CPP, ARM64, iOS 15.0
minimum, landscape, bundle id `com.catchifyoucan.game`.

Output: an `.xcodeproj` under `Builds/iOS/` (Unity names it `Unity-iPhone.xcodeproj`;
`BuildIOS.sh` locates it by glob and fails with exit 2 if none is produced).

In Xcode: **Signing & Capabilities → Team**, then **Product → Run** on a device.
Capture the device console (Xcode **Window → Devices and Simulators**, or
Console.app filtered on the device).

---

## 8. Verify deterministic hashes across Editor, Android and iOS

This is T4 — the one claim the engine-free core argues for but does not measure.

### How the hash is observable

Every successful generation logs, at info level, via `Debug.Log` (present in
release builds too):

```
[CIYC] House generated: seed <SEED>, <N> rooms, attempt <A>, hash <16-HEX>
```

On a validation failure it additionally logs the full `LayoutHash.ToReport()`
block with all seven section hashes.

### Blocker: there is currently no runtime fixed-seed entry point

`MissionManager.StartInvestigation` accepts a `seedOverride`, but nothing on the
live path passes one, so every run draws a fresh seed from
`SessionSeedSource.Next()`. `TrainingBootstrap` writes a `ciyc_training_seed`
PlayerPref that **nothing reads**. Two platforms will therefore never generate the
same seed by chance.

Comparing hashes across platforms consequently requires a **temporary** seed pin.
Make it, validate, then revert it — do not commit it.

In `Assets/CatchIfYouCan/Scripts/Missions/MissionManager.cs`, in
`StartInvestigation`, temporarily replace:

```csharp
int seed = seedOverride ?? SessionSeedSource.Next();
```

with:

```csharp
int seed = 184726392; // TEMPORARY validation pin - REVERT before committing
```

Any fixed value works; use the same one on all three platforms.

### Procedure

Build and run the Investigation scene once per platform with the pin in place:

| Platform | How to capture |
|---|---|
| **Editor** | Enter Play Mode, reach an investigation, read the Console |
| **Android** | `adb logcat \| grep "House generated"` |
| **iOS** | Xcode device console, filter `House generated` |

Record the full line from each.

**Pass condition:** identical 16-hex `hash` on all three, for the same seed.

**On mismatch — this is a real finding, capture before changing anything:**

1. The three log lines (seed + hash + room count from each platform).
2. In the Editor: **Tools → Catch If You Can → Determinism → Compare Two Layouts**,
   both seeds set to the pinned value, and copy the full output — it gives the
   per-section breakdown (Rooms / Connections / Doors / Furniture / Props /
   GameplaySpawns) that localises which stage diverged.
3. Unity version, scripting backend, and device model for each platform.

The section that differs names the failing stage. That is the difference between
a targeted fix and a bisect.

**Finally: revert the seed pin** and confirm `git diff` is clean.

---

## What to send back if anything fails

Send the **raw text**, not a screenshot or a summary — the exact error string is
what identifies the cause.

| Step | Send |
|---|---|
| **0** Wrong/missing version | The Hub version list, and the exact dialog text |
| **2** Assemblies missing | `ls Library/ScriptAssemblies/` |
| **3** Compile errors | Every `error CS…` line **with file, line and column**, plus `grep -nE "error CS" <Editor.log>`. Send all of them — the first error often causes the rest |
| **4** Tests fail | Failing test names, the NUnit assertion message and stack trace (right-click → Copy in Test Runner), or `editmode-results.xml` |
| **4** Test assembly missing | Console text about `com.unity.test-framework`, plus `Packages/manifest.json` |
| **5** Golden mismatch | Every `seed <N> (<MAP>): expected …, got …` line. **Do not regenerate the table** |
| **6** Android build fails | The `[Android Development] Build failed` line plus ~50 lines of preceding Console/Editor.log context |
| **7** iOS build fails | `Builds/Logs/ios_build_<timestamp>.log` (the script prints the last 60 lines on failure) |
| **8** Hash mismatch | The three `House generated` lines, the *Compare Two Layouts* output, and platform/backend/device for each |
| **Runtime error in play** | The full `[CIYC]` block — a generation failure prints the whole `LayoutHash.ToReport()` |

Always include: Unity version as shown in **Help → About Unity**, OS and version,
and `git rev-parse HEAD`.

---

## Scope

This procedure validates the existing determinism work only. It introduces no
multiplayer, no NGO, and no gameplay changes. The only edit it asks for is the
temporary seed pin in §8, which must be reverted.
