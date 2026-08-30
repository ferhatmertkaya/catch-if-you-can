# Multiplayer / Networking / Crossplay Implementation Report

Branch `claude/multiplayer-scope-determinism-0mkvap`, from `d2463e0`.
Companion: `Docs/MULTIPLAYER_NETWORK_AUDIT.md` (the read-only pass this follows).

---

## Executive Summary

**What was found.** The project has no networking of any kind — no package, no
code, no partial implementation. The audit verified this four independent ways
(§1 of the audit). `Docs/NETWORKING.md` already said so at `aa8c431` and it is
still true. So there was no stack to stabilise; the brief's premise of auditing
an existing multiplayer implementation did not hold.

`Docs/NETWORKING.md` §8 sets a build order and gates all netcode behind **T4** —
proving the layout hash is identical across physical iOS and Android builds —
with an explicit rationale: a divergence found in single player is cheap, and the
same divergence found through a relay and two devices is not. T4 has not been
run. Installing a netcode package additionally requires the Unity Editor to
resolve the registry, which this environment does not have.

**What was changed.** Rather than write netcode against a package that is not
installed and cannot be compiled — which would be precisely the mock
demonstration the brief forbids — this pass built the part of the milestone that
is transport-neutral and genuinely verifiable, and fixed the prerequisites that
do not depend on a transport:

- the **join handshake and mismatch protocol** of `NETWORKING.md` §3/§5, as pure
  engine-free C# with 16 tests running in the existing standalone harness;
- **one authoritative capacity constant**, where previously the number 4 existed
  only in Markdown prose;
- **the second client-side seed roll removed** — `SessionSeedSource.Next()` now
  has exactly one call site, which §3 requires and which was not true before.

**Confidence.** High for what was built: it compiles, it is covered by tests, and
the tests were shown to fail when the logic is deliberately broken. Zero for
anything requiring a transport, a device, or the Unity Editor — none of that was
touched, and none of it is claimed.

## Existing Architecture Before Changes

| Layer | State |
|---|---|
| Framework | none |
| Transport | none |
| Lobby | none |
| Relay | none |
| Authentication | none |
| Authority | none — process-global singletons, no ownership concept |
| Player capacity | not expressed in code |

Intended, per `NETWORKING.md` §2: NGO + UTP, Relay for NAT traversal, Lobby for
discovery, 4 players, 20 Hz host-authoritative with client-side interpolation.
Unchanged by this pass — no stack was chosen or installed.

## Files Changed

| Path | Reason | Behaviour change | Risk |
|---|---|---|---|
| `Scripts/UI/MissionSelectUI.cs` | **D1** — rolled an authoritative seed client-side, violating `NETWORKING.md` §3 | Menu start now ensures a `MissionManager` and routes through `StartInvestigation`, the single authoritative path. Side effect: the ghost is now picked, where the old branch passed `null` | Low. The replaced branch was the *live* one (`SceneAutoSetup` puts no `MissionManager` in the menu scene) and produced a ghost-less mission, so this fixes a live defect rather than a theoretical one |
| `Tools/DeterminismHarness/DeterminismSuite.cs` | Cover the new handshake | Adds `TestSessionHandshake()`; 28 existing assertions untouched | None — test-only |

## Files Created

| Path | Why necessary |
|---|---|
| `Scripts/Procedural/Deterministic/MultiplayerProtocol.cs` | **D3** — capacity, protocol version and tick rate had no representation in code. The UI had nothing to derive `X / MAX` from and connection approval had nothing to check |
| `Scripts/Procedural/Deterministic/MatchConfig.cs` | The `MissionStart` payload of §3: protocol version, generation version, seed, map id, content hash. Deliberately excludes ghost identity, objectives and evidence assignment per `DETERMINISM.md` §2.1b |
| `Scripts/Procedural/Deterministic/SessionCompatibility.cs` | **D4** — the §5 mismatch protocol. Two-stage by design: content mismatch aborts *before* generating, layout mismatch after, because §5 forbids falling through |
| `Docs/MULTIPLAYER_NETWORK_AUDIT.md` | Required audit output |
| `Docs/MULTIPLAYER_NETWORK_IMPLEMENTATION_REPORT.md` | This document |

The three source files sit in the existing `CatchIfYouCan.Procedural.Deterministic`
assembly (`noEngineReferences: true`). That placement is deliberate: they consume
`LayoutHash` and `ContentSnapshot`, `DETERMINISM.md` already draws the core →
multiplayer-handshake arrow, and being engine-free is what lets the standalone
harness execute the handshake without Unity. No new assembly was added.

## Files Deliberately Not Changed

- **`Packages/manifest.json`** — no networking package installed. Package
  resolution needs the Editor; code written against an unresolvable package
  cannot be compiled or trusted.
- **`SeedManager`** — still process-global. `NETWORKING.md` §1/§8 sequences the
  de-static *with* netcode start. There is no second session in-process today, so
  fixing it now would be a refactor of `WeatherSystem` and
  `ProceduralHouseGenerator` with no consumer and a real chance of disturbing
  generation. Named as the step-2 prerequisite it is.
- **Ghost systems** — `UnityEngine.Random` / `Time.time` usage is *permitted* by
  §4 (host-only, never hashed). Not touched.
- **`MissionRuntime.CaseId`, `EvidenceManager`, `PhotoResult`** — three
  `Guid.NewGuid()` sites (**D5**). Harmless single-player; they need an authority
  to be assigned from, which does not exist yet.
- **`AudioListener` creation sites** (**D6**) — three of them. Needs an ownership
  concept to fix correctly.
- **Everything else**: generation core, main menu, UI, graphics, audio, save,
  equipment, objectives.

## Multiplayer Feature Matrix

| Feature | Before | After | Tested |
|---|---|---|---|
| Authentication | none | none | — |
| Create lobby | none | none | — |
| Join lobby | none | none | — |
| Leave lobby | none | none | — |
| Player count | no value in code | single constant, 4 | harness |
| Roster | none | none | — |
| Ready | none | none | — |
| Start (authority) | client-side seed roll | single authoritative path | harness + static |
| Compatibility handshake | none | implemented, transport-neutral | harness (16 assertions) |
| Layout mismatch protocol | hash existed, no protocol | implemented | harness |
| Scene load | plain `SceneManager` | unchanged | — |
| Player spawn | local, unconditional | unchanged | — |
| Movement | none | none | — |
| Interactions | local | unchanged | — |
| Equipment | local | unchanged | — |
| Ghost state | local | unchanged | — |
| Ping | none | none | — |
| Disconnect | none | none | — |
| Reconnect | none | **explicitly unsupported** | — |
| Host loss | none | **explicitly unsupported** | — |
| Return to lobby | none | none | — |

## Crossplay Matrix

No transport exists, so no pairing can be verified end to end. The handshake
logic is platform-neutral C# with no `#if` branches and no platform types.

| Pairing | Status |
|---|---|
| Windows ↔ Windows | NOT TESTED — no transport |
| Windows ↔ Android | NOT TESTED — no transport |
| Windows ↔ iOS | NOT TESTED — no transport |
| Android ↔ Android | NOT TESTED — no transport |
| Android ↔ iOS | NOT TESTED — no transport |
| iOS ↔ iOS | NOT TESTED — no transport |

Handshake layer only: **SUPPORTED BY ARCHITECTURE** and **STATICALLY VERIFIED**
for all pairings — it is one code path with no platform-conditional logic. That
is a statement about the code's shape, not about two devices talking.

## Player Capacity

| | |
|---|---|
| Exact configured maximum | **4** |
| Where defined | `MultiplayerProtocol.MaxPlayers` — the only declaration in the codebase |
| How UI derives it | It does not yet; there is no lobby UI. When built, it reads this constant. A sweep confirms no other file declares a player count |
| Enforcement | `MultiplayerProtocol.HasCapacityFor(int)` and `SessionCompatibility.CheckJoin`, which returns `LobbyFull` before inspecting anything else |

## Authority Matrix

`NETWORKING.md` §4 is unchanged and remains normative. What this pass actually
enforces in code:

| System | Authority | Enforced now? |
|---|---|---|
| Session seed | Host | **Yes** — one call site |
| Match config (protocol/generation/content/map) | Host | **Yes** — `CreateAuthoritative` does not roll a seed, so no client call site can mint one by constructing a config |
| Lobby capacity | Host | **Yes** |
| Layout | Both, seed-derived | **Yes** — hash compared, mismatch aborts |
| Ghost identity / objectives / evidence assignment | Host, not seed-derived | Documented (`DETERMINISM.md` §2.1b); no netcode to enforce it |
| Everything else in §4 | Host | Not enforced — no netcode |

## Network Rates

Declared, not yet exercised: server tick **20 Hz** (`MultiplayerProtocol.ServerTickHz`,
matching §2). Transform send rate, ping interval, lobby and relay heartbeat are
transport concerns and remain unset — no transport.

## Determinism

| | |
|---|---|
| Tests before | 28 passed, 0 failed |
| Tests after | **44 passed, 0 failed** (28 original + 16 handshake) |
| Golden hashes changed | **None.** `GoldenSeedTable.cs` and `GenerationVersion.cs` are byte-identical |
| `GenerationVersion.Current` | 1, unchanged |
| Guard script | PASSED |
| Risk introduced | None to the generation path. The new files add types; they do not alter any draw, ordering, quantization or hash composition |

Negative control, per the brief: the §5 check ordering in `SessionCompatibility`
was deliberately inverted; the suite failed with exactly one failure
(`handshake: protocol outranks content when both differ`) and no others. The file
was restored and verified byte-identical to the backup, and the suite returned to
44/44.

## Performance

No network traffic exists to measure. The new code allocates nothing per frame —
`MatchConfig` is a `readonly struct`, the verdict path is branch-only, and
`ConfigHash()` uses the project's FNV-1a rather than `GetHashCode`, whose string
hashing is per-process randomised and therefore useless across peers. Handshake
work happens once per join, not per tick.

## Security

No RPC surface exists, so no RPC trust holes were introduced or fixed. Two trust
improvements landed:

- A client can no longer mint an authoritative session seed — the only remaining
  roll site is `MissionManager.StartInvestigation`, which is the site to gate on
  host when netcode arrives.
- `MatchConfig.CreateAuthoritative` deliberately takes a seed rather than drawing
  one, so constructing a config is not a way to become the authority.

`NETWORKING.md` §6's posture (host trusted, clients request and never assert) is
unchanged and not yet enforceable.

## Remaining Risks

1. **No networking exists.** Everything from lobby to ghost replication is
   unbuilt. This pass moved the prerequisites, not the feature.
2. **T4 is unmet** and is the documented gate on all netcode. Cross-platform hash
   equality on physical iOS and Android is argued from code structure, not
   measured. This is the highest-value next action and needs no netcode.
3. **`SeedManager` still process-global** — blocks host and client in one process,
   which is how Multiplayer Play Mode tests a listen server.
4. **D5 / D6 / D7 open** — per-client GUIDs, duplicate `AudioListener`, and no
   semantic input abstraction. All become real the moment a second player exists.
5. **The handshake has no wire.** It is correct logic with no transport calling
   it; a future integration could still call it in the wrong order. The two-stage
   API and `AbortsBeforeGeneration` exist to make that hard, not impossible.
6. **Stack choice is still open.** NGO+UTP+Relay+Lobby is documented intent, not
   a committed decision, and nothing here forecloses another choice.

## Manual Unity Tests Required

Nothing multiplayer can be tested — there is no multiplayer. What *should* be
verified about this change:

1. Open `01_MainMenu`, enter Play Mode, open mission select, start a mission.
   Expect: an investigation starts with a **non-null ghost** (previously null on
   this path), one `MissionManager` in the hierarchy, no duplicate.
2. Console: no `NullReferenceException` from `MissionSelectUI`, no
   "multiple MissionManager" warning.
3. Run the EditMode determinism suite. Expect all green, no golden hash change.
4. Repeat the mission start twice in one session; expect no second
   `MissionManager` and a different seed each time.

## Real Device Tests Required

**T4 first — this is the documented gate and it needs no netcode.**

1. Build `01_MainMenu` + investigation to a physical **iOS arm64** device (IL2CPP).
2. Build the same commit to a physical **Android arm64** device (IL2CPP).
3. On each, run the EditMode determinism suite (or a build-time harness scene)
   over the golden seed table.
4. Compare `LayoutHash.FinalHash` per seed across Editor, iOS and Android.
5. **They must be identical.** A difference is a determinism bug to investigate —
   never a reason to update the golden values.

Only once T4 is green does `NETWORKING.md` §8 step 2 (packages, bootstrap,
`SeedManager` de-static) become the right next move.

## Future Console Readiness

**Ready:** no Steam / Apple / Google Play identity anywhere; no direct-IP
matchmaking assumption; no platform IDs in gameplay state; no platform-specific
gameplay branches; `#if UNITY_ANDROID` / `UNITY_IOS` confined to input and build
config. `MatchConfig` carries no platform field, so no console port has to
serialise around one.

**Adapters still needed:** identity provider (Unity Authentication / Steam /
Apple / Google Play / future XBL / PSN) behind an internal session identity;
platform invite resolving into the same join-code flow; platform presence. None
implemented — correctly, since the brief forbids unused abstractions.

## Git Diff Summary

```
 Assets/CatchIfYouCan/Scripts/Procedural/Deterministic/MatchConfig.cs           | new
 Assets/CatchIfYouCan/Scripts/Procedural/Deterministic/MultiplayerProtocol.cs   | new
 Assets/CatchIfYouCan/Scripts/Procedural/Deterministic/SessionCompatibility.cs  | new
 Assets/CatchIfYouCan/Scripts/UI/MissionSelectUI.cs                             | modified
 Tools/DeterminismHarness/DeterminismSuite.cs                                   | modified
 Docs/MULTIPLAYER_NETWORK_AUDIT.md                                              | new
 Docs/MULTIPLAYER_NETWORK_IMPLEMENTATION_REPORT.md                              | new
```

No packages added or removed. No scene, prefab, material or asset touched. No
file deleted.
