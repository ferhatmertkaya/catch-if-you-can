# Unity Validation Procedure — Unity 6.5 (6000.5.10f1)

Purpose: execute the validation that `Docs/DETERMINISM.md` §10 lists as **NOT
EXECUTED**, on the migrated Editor baseline. All of it needs a real Unity Editor;
none can run in the headless container that produced the determinism work.

**Migration status: NOT COMPLETE.** The repository has been prepared for
`6000.5.10f1`, but the project has never been opened in it. The migration is not
a PASS until §3 compiles clean and §4/§5 execute green. Until then this is a
prepared, unverified baseline.

Already proven without Unity, and not repeated here: the deterministic core
builds under .NET, the 28-assertion harness passes, and
`Scripts/check_determinism.sh` passes. That covers 24 of ~190 C# files.

---

## 0. Editor and modules

**Install Unity `6000.5.10f1` (Unity 6.5). Exactly this version.**

Required modules:

| Module | Why |
|---|---|
| **Android Build Support** | §6 |
| ├ *OpenJDK* | Android build prerequisite |
| ├ *Android SDK & NDK Tools* | IL2CPP ARM64 |
| **iOS Build Support** (macOS only) | §7 |
| **Universal Windows / Mac / Linux Build Support (IL2CPP)** — the host one | Lets the Editor compile IL2CPP locally; optional but useful for isolating IL2CPP-only failures |

Not required: WebGL, tvOS, visionOS, Linux server.

Also needed: **Xcode 15+** (macOS, for §7) and `adb` on `PATH` (for §6).

### The revision hash is intentionally absent

`ProjectSettings/ProjectVersion.txt` now contains exactly one line:

```
m_EditorVersion: 6000.5.10f1
```

The `m_EditorVersionWithRevision` line has been **removed on purpose**. The
previous baseline carried `6000.3.0f1 (catchifyoucan)` — a literal string where a
12-hex revision belongs, which broke any CI that pins by revision. Rather than
replace one fabricated value with another, the line is omitted so **the real
6000.5.10f1 Editor writes the true revision on first import**.

That is expected and wanted. After the first open, `ProjectVersion.txt` will gain
a second line — commit it (§11, step 9).

Unity Hub resolves `6000.5.10f1` by version string and does not need the
revision. **If Hub does not offer `6000.5.10f1`, stop and tell us** rather than
opening with a different patch: doing so rewrites `ProjectVersion.txt` and moves
the toolchain under the determinism baseline.

---

## 1. First open

1. Unity Hub → **Add** → select the repository root (the folder containing
   `Assets/`, `Packages/`, `ProjectSettings/`).
2. Set the editor version to **`6000.5.10f1`**.
3. Open. First import resolves packages and recompiles everything; expect several
   minutes.
4. Any version-mismatch dialog → **cancel**, see §0.

**Unity will modify files during this import. That is normal.** Expected changes:

| File | What happens |
|---|---|
| `ProjectSettings/ProjectVersion.txt` | Gains `m_EditorVersionWithRevision` with the **real** revision — **commit it** |
| `Packages/manifest.json` | Editor-locked packages (URP and other SRP packages) may be auto-upgraded to the versions 6000.5 ships |
| `Packages/packages-lock.json` | **Created** (does not exist yet) — **commit it**, it is the reproducible resolution record |
| `ProjectSettings/*.asset` | Serialization-format touch-ups on version change |
| `Assets/**/*.meta` | New `.meta` files for anything not yet imported |
| `Library/` | Regenerated; git-ignored, ignore it |

**Do not accept, without checking:** changes to
`Assets/CatchIfYouCan/Scripts/Procedural/**`. Nothing in Unity should rewrite
source. See §9.

---

## 2. Force a full script recompile

A clean import is not the same as a clean recompile — stale `Library/` artifacts
can hide a broken assembly.

**Preferred:** close Unity, delete `Library/ScriptAssemblies/`, reopen.
(Deleting all of `Library/` also works but forces a full reimport.)

**Quicker:** **Assets → Reimport All**.

**Verify the assemblies exist:**

```bash
ls Library/ScriptAssemblies/
```

Expected:

```
Assembly-CSharp.dll
Assembly-CSharp-Editor.dll
CatchIfYouCan.Procedural.Deterministic.dll
CatchIfYouCan.Determinism.Tests.dll
```

A missing `Assembly-CSharp.dll` means the runtime assembly failed to compile —
§3 shows why.

---

## 3. Detect Assembly-CSharp / Assembly-CSharp-Editor compile errors

**The step most likely to fail, and the whole point of the migration.** Neither
Unity assembly has ever been compiled — on 6000.3 or 6000.5. All prior
verification used .NET against hand-written UnityEngine stubs, which is not
evidence that they build.

1. **Window → General → Console**.
2. Enable **Error**, **Warning**, **Info**; disable **Collapse**.
3. **Clear**, then force a recompile (§2).
4. Read every red entry.

Errors appear as `Assets/…/File.cs(LINE,COL): error CSxxxx: message`.

**Also check the Editor log** — it captures compiler output the Console truncates:

| OS | Path |
|---|---|
| macOS | `~/Library/Logs/Unity/Editor.log` |
| Windows | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |
| Linux | `~/.config/unity3d/Editor.log` |

```bash
grep -nE "error CS|Compilation failed|Assembly-CSharp" ~/Library/Logs/Unity/Editor.log | head -50
```

**Pass:** zero `error CS`, all four assemblies present.

Warnings do not block, but report any naming `Scripts/Procedural/`. On a major
Editor jump, obsolete-API warnings (`CS0618`) are the likely category.

---

## 4. Run the 27 EditMode determinism tests

1. **Window → General → Test Runner** → **EditMode** tab.
2. Expect assembly **`CatchIfYouCan.Determinism.Tests`** with **27 tests** under
   `CatchIfYouCan.Tests.DeterminismTests`.
3. **Run All**.

If the assembly is absent: `com.unity.test-framework` failed to resolve, or
`Assembly-CSharp` failed to compile. Fix §3 first.

**Pass: all 27 green.** The ones that matter most on a version migration:

| Test | Why it matters here |
|---|---|
| `Pcg32_MatchesPublishedReferenceVectors` | Proves 6000.5's compiler produces the same RNG stream. **If this fails, every golden hash is invalid** |
| `F_GoldenSeeds_ReproduceRecordedHashes` | The migration's core assertion: same seeds, same layouts, new Editor |
| `E_UnityEngineRandom_CannotPerturbGeneration` | Engine-only; unprovable outside Unity |
| `E_GenerationDoesNotAdvanceUnityEngineRandom` | The converse |
| `G_PerturbingCollectionOrder_DoesNotChangeHash` | Hash canonicalisation under a new `List.Sort` implementation |
| `DuplicatePropStableId_IsRejected` + 5 sibling duplicate-id tests | Have never executed under NUnit |

**Batch mode:**

```bash
"<UnityPath>" -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -assemblyNames CatchIfYouCan.Determinism.Tests \
  -testResults ./Builds/Logs/editmode-results.xml \
  -logFile ./Builds/Logs/editmode.log
```

Exit `0` = all passed; the XML names failures.

---

## 5. Validate all 24 golden seeds

Menu: **Tools → Catch If You Can → Determinism → Validate Golden Seeds**

Checks all 24 committed entries (12 seeds × 2 maps) in `GoldenSeedTable.cs`.

**Pass:** dialog reports all 24 reproduce.

**Fail:** each mismatch logs `seed <N> (<MAP>): expected <HASH>, got <HASH>`.

> ### A golden mismatch after the Unity upgrade is a REGRESSION, not an expected consequence
>
> The generation core is engine-free integer arithmetic with a frozen PCG32. A
> different Unity version must not change its output. If hashes moved, something
> real changed — compiler codegen, an `int`/`float` behaviour difference, or an
> accidental source change during import.
>
> **Do not run *Generate Golden Seeds*.** That overwrites the only evidence.
> **Do not bump `GenerationVersion`.** It marks deliberate algorithm changes, and
> this would not be one. Capture the output and send it (§12).

Related items on the same menu: *Compare Two Layouts* (first authoritative
difference between two seeds, with per-section hashes), *Print Layout Report*,
*Generate Golden Seeds* (only after a deliberate `GenerationVersion` bump).

> These Editor tools use `ContentSnapshot.CreateFallback()`, not the project's
> authored `PropDefinition`/`RoomDefinition` assets. They validate the generator,
> not the shipped content. §8 exercises the authored path.

---

## 6. Android Development Build

`ProjectSettings.asset` already specifies **IL2CPP** (`scriptingBackend.Android: 1`),
**ARM64** (`AndroidTargetArchitectures: 2`), min SDK 24, target SDK 35,
`com.catchifyoucan.game`. The Development build therefore already satisfies the
IL2CPP requirement — no settings change needed.

1. **File → Build Settings → Android → Switch Platform** (first switch is slow).
2. Confirm four scenes listed and enabled: `00_Boot`, `01_MainMenu`,
   `02_Training`, `03_Investigation`.
3. **Catch If You Can → Build Android Development**.

Output: `Builds/Android/CatchIfYouCan_dev.apk`

```bash
adb install -r Builds/Android/CatchIfYouCan_dev.apk
adb logcat -c
adb logcat | tee android-run.log | grep -E "CIYC|Unity"
```

---

## 7. iOS Xcode build

macOS + Xcode 15+.

**Scripted:** `./BuildIOS.sh` — now prefers `6000.5.10f1` strictly, falls back to
any `6000.5.x` and then any `6000.x` **with a warning**. Runs
`CatchIfYouCanBuildMenu.BuildIOSBatch` in batch mode, logs to
`Builds/Logs/ios_build_<timestamp>.log`, zips the export.

**Manual:** **File → Build Settings → iOS → Switch Platform**, then
**Catch If You Can → Build iOS**.

Either applies `ConfigureIOSPlayerSettings()`: IL2CPP, ARM64, iOS 15.0 minimum,
landscape, `com.catchifyoucan.game`.

Output: an `.xcodeproj` under `Builds/iOS/` (Unity names it
`Unity-iPhone.xcodeproj`; the script locates it by glob and exits 2 if none).

In Xcode: **Signing & Capabilities → Team**, then **Product → Run** on a device.

---

## 8. Verify deterministic hashes across Editor, Android and iOS

This is T4 — the claim the engine-free core argues for but does not measure.

### How the hash is observable

Every successful generation logs, via `Debug.Log` (present in release builds too):

```
[CIYC] House generated: seed <SEED>, <N> rooms, attempt <A>, hash <16-HEX>
```

On a validation failure it also logs the full `LayoutHash.ToReport()` block with
all seven section hashes.

### Blocker: no runtime fixed-seed entry point

`MissionManager.StartInvestigation` accepts a `seedOverride`, but nothing on the
live path passes one — every run draws a fresh seed from
`SessionSeedSource.Next()`. `TrainingBootstrap` writes a `ciyc_training_seed`
PlayerPref that **nothing reads**. Two platforms will never draw the same seed by
chance.

Cross-platform comparison therefore needs a **temporary** seed pin. Make it,
validate, revert. Do not commit it.

In `Assets/CatchIfYouCan/Scripts/Missions/MissionManager.cs`, in
`StartInvestigation`, temporarily replace:

```csharp
int seed = seedOverride ?? SessionSeedSource.Next();
```

with:

```csharp
int seed = 184726392; // TEMPORARY validation pin - REVERT before committing
```

Use the same value on all three platforms.

### Procedure

Run the Investigation scene once per platform with the pin in place:

| Platform | Capture |
|---|---|
| **Editor** | Play Mode → reach an investigation → Console |
| **Android** | `adb logcat \| grep "House generated"` |
| **iOS** | Xcode device console, filter `House generated` |

**Pass:** identical 16-hex `hash` on all three for the same seed.

**On mismatch — capture before changing anything:**

1. The three log lines (seed, hash, room count).
2. Editor → **Tools → Catch If You Can → Determinism → Compare Two Layouts**,
   both seeds set to the pinned value; copy the full output for the per-section
   breakdown (Rooms / Connections / Doors / Furniture / Props / GameplaySpawns).
3. Unity version, scripting backend, device model per platform.

**Then revert the seed pin** and confirm `git diff` is clean.

---

## 9. Package compatibility review

**First-open on 6000.5.10f1 failed: the project entered Safe Mode.** Every error
was inside `Library/PackageCache`, none in `Assets/CatchIfYouCan/Scripts`:

| Package | Error |
|---|---|
| `com.unity.addressables` 2.4.3 | `CS0619` `Object.GetInstanceID()` obsolete, use `GetEntityId` |
| `com.unity.ai.navigation` 2.0.7 | `CS0619` `Object.GetInstanceID()` obsolete, use `GetEntityId` |
| `com.unity.inputsystem` 1.14.0 | `CS0619` legacy `TreeView` / `TreeViewItem` / `TreeViewState` obsolete |

`Library/PackageCache` is generated data and must never be hand-patched. The fix
is the manifest.

### Applied

| Package | Action | Why |
|---|---|---|
| `com.unity.textmeshpro` 3.0.9 | **Removed** | From Unity 6 TMP ships inside `com.unity.ugui` 2.0.0 under the same `Unity.TextMeshPro` assembly name, so `using TMPro;` in `UITheme`/`RuntimeUIFactory` and the reflection lookup in `RuntimeUIFactory` both still resolve. The standalone package is legacy and conflicts |
| `com.unity.addressables` 2.4.3 | **Removed** | **Provably unused** — zero references in any tracked file except two markdown mentions: no code, no asmdef, no `AddressableAssetSettings`, nothing orphaned. Removing it eliminates one of the three errors outright, which is a smaller change than upgrading it |

### Still blocked — needs versions only the Editor can supply

`com.unity.ai.navigation`, `com.unity.inputsystem` and
`com.unity.render-pipelines.universal` must be **updated**, not removed:

- **`com.unity.inputsystem`** — `EventSystemUtil.cs` uses
  `UnityEngine.InputSystem.UI` behind `#if ENABLE_INPUT_SYSTEM`. Removal would
  compile, but it would drop `InputSystemUIInputModule` from a touch-first game
  and orphan `CIYCInputActions.inputactions`. That is a gameplay change, which
  this pass is not permitted to make.
- **`com.unity.ai.navigation`** — used by reflection only
  (`NavMeshRuntimeBuilder`), with a working fallback to the built-in
  `NavMeshBuilder`. Removal would compile but silently downgrade navigation
  quality.
- **`com.unity.render-pipelines.universal` 17.1.0** — SRP packages are locked to
  the Editor; 17.1.x belongs to the Unity 6.0/6.1 line.

The correct versions were **not guessed**. Unity's package registry
(`packages.unity.com`) and docs are unreachable from the environment that
prepared this migration, and inventing version numbers would either fail to
resolve or reproduce Safe Mode on the next attempt. See §10 for how to obtain
them authoritatively.

### Unchanged, deliberately

`com.unity.test-framework` 1.4.5, `com.unity.ugui` 2.0.0, and every
`com.unity.modules.*` (built-in, always `1.0.0`, editor-locked). Not upgraded
blindly.

**After the next import, diff `Packages/manifest.json` and `packages-lock.json`**
and send both. That diff is the authoritative compatibility record.

---

## 10. Recovering the remaining package versions

Two ways to get authoritative numbers for `com.unity.inputsystem`,
`com.unity.ai.navigation` and `com.unity.render-pipelines.universal`. Both use
the installed Editor as the source of truth. Package Manager works in Safe Mode —
that is what Safe Mode is for.

### Path A — let Unity resolve (fastest)

1. Close Unity.
2. `git pull` (removes the two dead packages).
3. Delete `Packages/packages-lock.json` — it pins the old resolution and will
   otherwise fight the manifest.
4. Reopen in **6000.5.10f1**. Expect Safe Mode again: the remaining three are
   still on incompatible versions.
5. **Window → Package Manager → In Project**. Each of the three shows an update
   arrow; take the version Unity offers (it only offers compatible ones).
6. Let it recompile and exit Safe Mode.
7. Send back `Packages/manifest.json` and `Packages/packages-lock.json`.

### Path B — read the Editor's own recommended versions

Every Editor ships a built-in manifest naming the versions it recommends:

| OS | Path |
|---|---|
| macOS | `/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/Resources/PackageManager/Editor/manifest.json` |
| Windows | `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\Resources\PackageManager\Editor\manifest.json` |
| Linux | `<install>/Editor/Data/Resources/PackageManager/Editor/manifest.json` |

```bash
cat "/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/Resources/PackageManager/Editor/manifest.json"
```

Send that file and exact pins can be written for you without another
open-and-fail cycle.

Either path ends the same way: the resolved `manifest.json` and
`packages-lock.json` get committed, and **`packages-lock.json` must be committed**
— it is the reproducible record of what the build actually used.

---

## 11. First-open checklist

- [ ] 1. Install Unity **6000.5.10f1** + Android Build Support (OpenJDK, SDK/NDK) + iOS Build Support (macOS)
- [ ] 2. Close Unity, `git pull`, delete `Packages/packages-lock.json`
- [ ] 3. Hub → Add → repo root → open with **6000.5.10f1**
- [ ] 4. Resolve the three remaining packages via Package Manager (§10) and let it exit Safe Mode
- [ ] 5. `git status` — review every modified file against §1's expected list
- [ ] 6. `git diff Packages/manifest.json` — record what Unity changed (§9)
- [ ] 7. Confirm `Packages/packages-lock.json` was created
- [ ] 8. Confirm `ProjectSettings/ProjectVersion.txt` gained a **real** revision
- [ ] 9. **Commit** `ProjectVersion.txt`, `packages-lock.json`, `manifest.json` — that is the migration's real completion
- [ ] 10. Force a full recompile (§2); confirm all four assemblies
- [ ] 11. Console clean of `error CS` (§3)
- [ ] 12. Test Runner → EditMode → **27/27 green** (§4)
- [ ] 13. **Validate Golden Seeds → 24/24 reproduce** (§5)
- [ ] 14. Confirm **no** unexpected diff under `Assets/CatchIfYouCan/Scripts/`
- [ ] 15. Only then: Android (§6), iOS (§7), cross-platform hashes (§8)

Steps 11–13 are the migration gate. Until all three pass, the migration is
**NOT** complete.

---

## 12. What to send back if anything fails

Send **raw text**, not screenshots — the exact string identifies the cause.

| Step | Send |
|---|---|
| **0** Hub lacks 6000.5.10f1 | The Hub version list and the exact dialog text |
| **1** Unexpected file changes | Full `git status` and `git diff --stat` |
| **2** Assemblies missing | `ls Library/ScriptAssemblies/` |
| **3** Compile errors | Every `error CS…` line **with file, line, column**, plus `grep -nE "error CS" <Editor.log>`. Send **all** — the first often causes the rest |
| **4** Tests fail | Failing test names + NUnit assertion message and stack trace (right-click → Copy), or `editmode-results.xml` |
| **4** Test assembly missing | Console text about `com.unity.test-framework`, plus `Packages/manifest.json` and `packages-lock.json` |
| **5** Golden mismatch | Every `seed <N> (<MAP>): expected …, got …` line. **Do not regenerate. Do not bump GenerationVersion.** Treat as a regression |
| **9** Package conflict | The Package Manager error text plus `manifest.json` and `packages-lock.json` |
| **6** Android build fails | The `[Android Development] Build failed` line + ~50 lines of preceding context |
| **7** iOS build fails | `Builds/Logs/ios_build_<timestamp>.log` |
| **8** Hash mismatch | The three `House generated` lines, *Compare Two Layouts* output, platform/backend/device each |
| **Runtime error** | The full `[CIYC]` block — a generation failure prints the whole `LayoutHash.ToReport()` |

Always include: **Help → About Unity** version string, OS and version, and
`git rev-parse HEAD`.

---

## Scope

This validates the existing determinism work on the new Editor baseline. It adds
no multiplayer, no NGO, no gameplay. The only edit it asks for is the temporary
seed pin in §8, which must be reverted.
