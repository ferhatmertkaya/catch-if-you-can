# Multiplayer / Networking / Crossplay Audit

Audited at `d2463e0` on branch `claude/multiplayer-scope-determinism-0mkvap`,
Unity `6000.5.10f1`. Read-only pass; findings below are quoted from the tree as
it stands, not from intent.

Companion documents: `Docs/NETWORKING.md` (normative architecture),
`Docs/DETERMINISM.md` (normative determinism contract). Where this audit and
those documents disagree, they win and the disagreement is recorded as a defect.

---

## 1. Current network stack

**There is none.** This is the single most important finding and it reframes the
task: there is nothing to stabilise, because nothing has been built.

Verified four independent ways:

| Check | Result |
|---|---|
| `Packages/manifest.json` | No netcode, transport, relay, lobby, services or multiplayer package |
| `Packages/packages-lock.json` | 33 resolved packages, none networking |
| Symbol sweep over `Assets/`, `Tools/` | Zero hits for `NetworkManager`, `NetworkBehaviour`, `NetworkObject`, `NetworkVariable`, `ServerRpc`, `ClientRpc`, `UnityTransport`, `INetworkSerializable`, `NetworkList`, `ConnectionApproval`, `OwnerClientId`, `IsServer`, `IsHost`, `IsOwner`, `CustomMessagingManager` |
| Third-party sweep | Zero hits for Photon, Mirror, FishNet, Steamworks, EOS, PlayFab, Nakama |

| Layer | Present | Intended (`NETWORKING.md` §2) |
|---|---|---|
| Framework | — | Netcode for GameObjects |
| Transport | — | Unity Transport (UTP) |
| Connectivity | — | Relay (NAT traversal) |
| Discovery | — | Lobby |
| Authentication | — | unspecified |
| Matchmaking | — | not in slice |
| Scene sync | — | not built |
| Player networking | — | not built |

No competing frameworks coexist, because none exist. `FurnitureAudioRelay.cs`
matches a naive `Relay` grep and is unrelated audio code — `NETWORKING.md` §1
already flags this false positive.

## 2. Current architecture

Single-player, singleton-driven. `GameManager`, `MissionManager`,
`EvidenceManager`, `EquipmentManager`, `SettingsManager` and `SeedManager` are
process-global with no notion of ownership or authority.

Session flow today: `MissionSelectUI` → `MissionManager.StartInvestigation` →
`GameManager.BeginMission` → `SceneLoader.LoadInvestigation` →
`SceneAutoSetup.EnsureInvestigation` → `InvestigationBootstrap` →
`ProceduralHouseGenerator` → `SpawnPlayer`.

Scene loading is plain `SceneManager`; there is no networked scene manager, no
readiness gate, and no concept of a remote peer anywhere in the tree.

## 3. Current player capacity

**There is no capacity value in the codebase at all.** A sweep for
`maxPlayers`, `MaxConnections`, `LobbyCapacity`, `MaxClients` and player-count
UI returns nothing.

| Source | Value |
|---|---|
| Configured max players | *does not exist* |
| UI max players | *does not exist* |
| Transport max | *no transport* |
| Gameplay-assumed limit | 1 (single-player spawn path) |
| Documented target | 4 — `NETWORKING.md` §2, prose only |

So there is no inconsistency to reconcile; there is an absence. The "4" lives
only in a Markdown table and is not reachable from code.

## 4. Current multiplayer features

Every feature in the brief's goal list is **Missing**. Recording them as
"partial" would be generous to the point of dishonesty:

Authentication, lobby create/join/leave, roster, capacity display, ready state,
connection state, ping/RTT, loadout confirm, authoritative match start,
networked scene load, player spawn ownership, movement replication, interaction
replication, equipment replication, ghost authority, disconnect handling,
reconnect, host migration, late join, invites, matchmaking, voice — **none
implemented**.

Implemented and relevant: the deterministic generation core (`V1`–`V8`, golden
seed table, canonical layout hash with per-section breakdown) which the
handshake in `NETWORKING.md` §3 is designed to consume.

## 5. Crossplay readiness

| Platform | Build target configured | Networking |
|---|---|---|
| Windows | yes | none |
| Android | yes (quality tier 1) | none |
| iOS | yes (quality tier 1, `6000.5.10f1`) | none |

Crossplay cannot be assessed as working or broken. What *can* be assessed is
whether the existing code would obstruct it — see §13.

## 6. Determinism integration

This is the strongest part of the project and the reason the intended
architecture is viable.

- **Seed ownership.** `SessionSeedSource.Next()` is a CSPRNG draw, correctly
  separated from consumption. `NETWORKING.md` §3 requires exactly one
  authoritative path. **Defect D1** below shows there are two.
- **Seed storage.** `SeedManager._currentSeed` is `private static int` —
  process-global mutable state, set by `ProceduralHouseGenerator` line 122.
  `NETWORKING.md` §1 already names this as a netcode blocker.
- **Generation timing.** `InvestigationBootstrap` generates then spawns in one
  synchronous flow. There is no readiness gate, because there are no peers to
  gate on yet.
- **Validation.** `LayoutHash` already carries `GenerationVersion`, `Seed`,
  `MapDefinitionId`, `ContentHash`, seven section hashes and `FinalHash` —
  precisely the §3 handshake payload. `ContentSnapshot.ContentHash` exists.
- **Risk.** `NETWORKING.md` §8 step 1 (**T4**, cross-platform hash equality on
  physical iOS and Android builds) is **not done**, and the document states
  plainly that it must complete *before* netcode work starts. That gate is
  unmet.

## 7. Player synchronization

Movement, spawn, identity, ready and disconnect are all absent. Two concrete
hazards exist in the single-player code that would surface the moment a second
player did:

- `Player/PlayerFactory.cs:43` and `Core/SceneAutoSetup.cs:157` each add an
  `AudioListener`. Unity permits exactly one; a second player object means
  duplicate-listener warnings and broken spatial audio.
- `Procedural/InvestigationBootstrap.cs:189` `SpawnPlayer()` instantiates the
  player unconditionally with no ownership concept, and four `FindFirstObjectByType<AudioListener>()`
  call sites assume a single listener.

## 8. Shared gameplay synchronization

Interactions, equipment, ghost and mission state are entirely local. Ghost code
(`GhostController`, `GhostEventDirector`, `GhostStateMachine`, `HuntController`
and five more) uses `UnityEngine.Random` and `Time.time` heavily. Per
`NETWORKING.md` §4 that is **permitted** — ghost AI runs host-only and is never
hashed — but there is no host gate today, so the same code would run on every
client and diverge immediately.

## 9. Ping / network quality

Not implemented, and cannot be implemented meaningfully without a transport —
real RTT must come from the transport's own metrics. No fake or frame-derived
ping exists in the tree, which is the correct starting position.

## 10. Disconnect / reconnect

Not implemented. No host-loss behaviour, no reconnect, no host migration, no
late join. `NETWORKING.md` §3.1 marks late join **[Deferred]** deliberately.

## 11. Security / validation

No RPC surface exists, so no RPC trust holes exist. The trust posture is
specified in `NETWORKING.md` §6 and is sound. One latent issue: rewards flow
through `MissionManager.CalculateTotalReward` locally, which must become
host-side before any client can influence it.

## 12. Performance

No network traffic. The §7 budget (≤ 8 KB/s per client) is untested but the
arithmetic there is plausible for 4 players at 20 Hz.

## 13. Crossplay blockers

**Critical**

- **C1 — No networking stack.** Nothing to build on.
- **C2 — T4 unmet.** Cross-platform hash equality on physical iOS/Android builds
  is unverified. `NETWORKING.md` §8 gates netcode on it, for the stated reason
  that a divergence found through a relay costs several times more to diagnose.

**High**

- **D1 — Second client-side seed roll.** `UI/MissionSelectUI.cs:188` calls
  `SessionSeedSource.Next()` directly when `MissionManager.Instance == null`.
  `NETWORKING.md` §3 requires the seed to be host-authoritative and never rolled
  client-side, and states there is "exactly one authoritative selection path".
  There are two. The comment in `MissionManager.StartInvestigation` asserting
  that "MissionSelectUI and InvestigationBootstrap both arrive here" is false for
  this branch.
- **D2 — Process-global seed.** `SeedManager._currentSeed` static blocks host and
  client logic coexisting in one process, which is exactly how Multiplayer Play
  Mode and a listen-server host are tested.
- **D3 — No capacity source of truth.** Nothing to derive UI or connection
  approval from.
- **D4 — No protocol/content version check.** A mismatched build would join and
  desync later rather than being rejected early, which `NETWORKING.md` §5
  requires.

**Medium**

- **D5 — `Guid.NewGuid()` match identity.** `MissionRuntime.Create` line 40
  builds `CaseId` from a fresh GUID. Independently generated per machine, so
  every client would hold a different id for the same match.
- **D6 — Duplicate `AudioListener`.** Two creation sites, single-listener
  assumptions in four more.
- **D7 — No input abstraction.** `Input/` holds only `MobileInputController` and
  `VirtualJoystick`; there is no semantic action layer, so PC and mobile have no
  shared command vocabulary to replicate.

**Low**

- **D8 — Ghost systems ungated.** Correct per §4 once host-only, but nothing
  enforces host-only today.

## 14. Future console blockers

Architecturally the project is in good shape here, mostly by not having done
anything yet:

- No Steam, Apple, Google Play or platform identity coupling anywhere.
- No direct-IP matchmaking assumption.
- No platform-specific gameplay branches.
- `#if UNITY_ANDROID` / `UNITY_IOS` usage is confined to input and build config.

The one real risk is prospective: choosing a transport or identity provider that
embeds platform IDs into gameplay state. Adapter points should stay behind an
internal session identity.

## 15. Recommended implementation plan

`NETWORKING.md` §8 already sets the ordering and it is sound. Restated with
current status:

1. **T4 on real devices** — single player, no netcode. **Blocked here** (no
   Unity, no devices).
2. **NGO + UTP packages; host/client bootstrap; `SeedManager` de-staticked.**
   Package installation requires the Unity Editor to resolve the registry;
   **blocked here**. The `SeedManager` half is not blocked.
3. **Handshake (§3) and mismatch protocol (§5), no gameplay replication.** The
   *logic* of this step is transport-neutral and can be built and tested now;
   only the wire cannot.
4. Player movement replication.
5. Ghost replication + interest management.
6. Fear host-side.
7. Evidence, equipment, objectives.
8. Relay + Lobby on real devices.

### What this pass does

Steps 1 and 2's package half cannot be done in this environment, and writing NGO
code against a package that is not installed and cannot be compiled would
produce exactly the mock demonstration the brief forbids.

So this pass implements the **transport-neutral core of step 3** — the match
configuration, the compatibility verdict, and the capacity constant — as pure
engine-free C# in the existing deterministic assembly, where the standalone
harness can actually execute it. It also fixes D1, D2 and D3, which are
prerequisites that do not depend on a transport.

It deliberately does **not** install a networking package, choose a transport,
or write lobby/ping/movement code, because none of that can be verified here and
all of it is gated on decisions the project owner should make with a working
Editor.
