# Networking Specification

Status: normative for the sections marked **[Slice]**. Sections marked
**[Deferred]** are design intent for a later pass and must not be built yet.

Companion document: `Docs/DETERMINISM.md`. Read it first — the handshake in §5
is the reason that document exists.

---

## 1. Current state

The project has **no networking of any kind** at `aa8c431`:

- `Packages/manifest.json` contains no `com.unity.netcode.gameobjects`, no
  transport, no relay, no lobby, no multiplayer services package.
- No `NetworkBehaviour`, `NetworkVariable`, or RPC anywhere in
  `Assets/CatchIfYouCan/Scripts/**`. (`FurnitureAudioRelay.cs` matches a
  "Relay" grep; it is an audio component and unrelated.)
- `GameManager`, `MissionManager`, `EvidenceManager`, `EquipmentManager` and
  `SeedManager` are process-global singletons with no notion of ownership.
- `SeedManager` still holds **static mutable state** — a per-process seed. It no
  longer holds an RNG (streams are created on demand and owned by the caller),
  but the seed itself is still process-global, which blocks running host and
  client logic in one process. De-static it when netcode starts; the determinism
  suites already avoid it by passing seeds explicitly.

So the networking half is greenfield. The determinism half it depends on is
built — see §8.

---

## 2. Topology **[Slice]**

**Host-authoritative, client-predicted movement only.** Not lockstep, not
deterministic simulation.

| | |
|---|---|
| Model | One player is host (server + client in one process) |
| Transport | Unity Transport (UTP) |
| Connectivity | Relay for NAT traversal; Lobby for discovery |
| Max players | 4 |
| Tick | 20 Hz server tick, client-side interpolation |

Determinism buys us exactly one thing: **not having to replicate the house.**
A 30-room house with several hundred props is far too much to stream to a phone
on a cellular connection at session start. Both clients generate it locally from
a 4-byte seed instead. That is the entire justification, and it bounds the scope
of the determinism work — see `DETERMINISM.md` §2.3 for what deliberately stays
outside the deterministic set.

Everything *after* generation is ordinary authoritative netcode. We do not
attempt deterministic lockstep for gameplay: PhysX is not cross-platform
bit-reproducible, and iOS/Android/Editor float and libm behaviour differ
(`DETERMINISM.md` R10 and §7).

---

## 3. Session lifecycle **[Slice]**

```
Host                                     Client
────                                     ──────
1. Roll seed (host only)
   seed = CSPRNG int32, != 0
2. Compute contentHash (baked)
3. Broadcast MissionStart {
     seed, contentHash,
     missionId, ghostTierRange }
                              ───────▶
4. Generate house              4'. Generate house  (same code path)
5. layoutHash = Hash(house)    5'. layoutHash' = Hash(house)
                              ◀───────  6. LayoutAck { layoutHash', contentHash' }
7. Compare per client
   ─ match    → admit
   ─ mismatch → §5
8. Spawn ghost, evidence, players (host authority)
                              ───────▶
9. GameStart
```

Rules:

- **The seed is host-authoritative and replicated.** It is never rolled
  client-side. `MissionManager.RollMissionSeed` (`MissionManager.cs:140`) and
  `MissionSelectUI.cs:187` currently roll from `UnityEngine.Random` locally —
  both become host-only paths (`DETERMINISM.md` V6).
- **Ghost identity is rolled by the host and replicated, not derived from the
  seed.** Deriving it from the seed means any client can compute the answer to
  the round from data it is given at join time. The ghost type, traits and tier
  are host state, revealed only through evidence.
- No player, ghost, evidence, or equipment object is spawned before step 7
  passes for every client. A client that joins the house at the same moment it
  might be told to abort is a source of half-torn-down-session bugs.

### 3.1 Late join **[Deferred]**

Same handshake, plus a world-state delta (doors opened, evidence found,
equipment placed, objectives completed). Not in the first pass; a mid-session
joiner is a large surface area for little slice value.

---

## 4. Authority **[Slice]**

The default is host authority. Deviations must be justified here.

| System | Authority | Replication |
|---|---|---|
| House layout | Both (seed-derived) | Seed + hash only |
| Player position | Client-predicted, host-reconciled | 20 Hz + interpolation |
| Player look | Client | Unreliable, low rate |
| Fear / sanity | **Host** | Per-player `NetworkVariable` |
| Ghost transform | Host | Interpolated |
| Ghost AI decisions | Host | Not replicated (effects only) |
| Hunt start/stop | Host | Reliable event to all |
| Evidence discovery | Host validates client claim | Reliable, to all |
| Equipment state | Owner-predicted, host-authoritative | Per-item |
| Doors / interactables | Host | Reliable |
| Thrown-object physics | Host | Transform sync |
| Weather | Host (seed-derived, host confirms) | With `MissionStart` |
| Objectives | Host | Reliable |
| Contract / economy payout | Host | End of round |

Two notes worth stating explicitly:

- **Fear is host-authoritative.** It gates hunts and it is the value a cheating
  client benefits most from lying about. `FearSystem` is currently a local
  `MonoBehaviour`; it must move server-side with the client receiving a
  read-only replicated value for presentation. Client-side prediction of the
  *audio-visual* response is fine and desirable; prediction of the *number* is
  not.
- **Ghost AI is not made deterministic.** `GhostController`,
  `GhostEventDirector`, `GhostInteractionBrain` and `GhostPerception` may keep
  using `UnityEngine.Random` and `Time.time` (they use both heavily today)
  because they run only on the host. This is a deliberate boundary: it is the
  reason the deterministic set in `DETERMINISM.md` §2.1 stays small enough to
  actually verify.

---

## 5. Mismatch protocol **[Slice]**

A layout or content hash mismatch **aborts the session for the mismatched
client**. It is never repaired in place.

There is no partial resync path, and this is a design decision rather than a
missing feature: a divergent client cannot be patched into agreement, because
neither side knows which of the thousands of generation decisions differed. The
only sound repairs are "send the whole world" — the cost we adopted determinism
to avoid — or "abort".

```
contentHash mismatch  → "This session is running a different game version."
                        Abort before generating. Do not attempt layout compare;
                        it will fail too and the message will be misleading.

layoutHash mismatch   → "Could not sync the house layout."
                        Abort after generating. Upload diagnostics
                        (DETERMINISM.md §9) including the per-section hash
                        breakdown. This is a bug in our generator, not a
                        network fault, and must be reported as one.
```

Host behaviour: if **any** client mismatches, log it and continue with the
remaining clients. If the *host itself* disagrees with all clients, abort the
whole session — that is a corrupt-content signal, not a per-peer fault.

Telemetry is not optional here. A layout mismatch in the wild is
unreproducible without the seed, both hashes, the section breakdown, and the
platform/build triple. Ship the reporting in the same change as the handshake.

---

## 6. Anti-cheat posture **[Slice]**

Scope: keep an honest client honest, and keep a casual cheater from ruining a
public lobby. Not a hardened threat model.

- The host is trusted. A cheating host can ruin its own lobby; accepted.
- Clients never assert evidence, fear, objective completion, or payout. They
  request; the host validates against its own state and decides.
- The ghost's identity and its position while non-manifested are never sent to
  clients that should not have them. A wallhack that only needs to read a
  replicated transform is the cheapest cheat there is; do not create it by
  replicating the ghost unconditionally.
- Interest management (§7) is therefore a correctness requirement, not only a
  bandwidth optimisation.

---

## 7. Bandwidth **[Slice]**

Target: **≤ 8 KB/s per client steady state**, mobile-first, assume cellular.

- House: 0 bytes after `MissionStart`. This is the whole point.
- Player state: 4 players × 20 Hz × ~20 B quantized ≈ 1.6 KB/s.
- Ghost: only when observable by that client.
- Equipment: on state change, not per tick.
- Audio: never replicate cosmetic audio. Replicate the *event*; each client
  plays its own variation (`DETERMINISM.md` §2.2 exists so this is legal).

---

## 8. Build order

**The determinism foundation is done.** V1–V8 are fixed, Stage A is engine-free
and pure, the canonical layout hash with per-section breakdown exists, and both
test suites are green (`Docs/DETERMINISM.md` §10, §11).

One prerequisite is still outstanding: **T4, the cross-platform hash job**, needs
Unity build agents CI does not have, so identical hashing across IL2CPP
iOS-arm64 and Android-arm64 is currently argued from the code's structure rather
than measured. Run the EditMode suite on a physical build of each platform before
netcode work starts — a divergence found there is single-player-cheap, and the
same divergence found through a relay and two devices is not.

That ordering held for a concrete reason, and it paid off: every violation in
the audit presented identically through a replication layer ("the other player's
house is subtly wrong"), and V5 — where the editor and a device build disagreed
with each other in single player — would have cost several times as much to
diagnose through a lobby.

Remaining slice ordering:

1. T4 on real devices — single player, no netcode
2. NGO + UTP packages; host/client bootstrap; `SeedManager` de-staticked
3. Handshake (§3) and mismatch protocol (§5) with **no gameplay replication** —
   two clients generate the same house, agree, and spawn nothing
4. Player movement replication
5. Ghost replication + interest management
6. Fear moved host-side
7. Evidence, equipment, objectives
8. Relay + Lobby, real devices, cellular

Step 3 is the milestone that proves the architecture. It is worth building on
its own and demoing on two physical devices — one iOS, one Android — before any
gameplay replication exists, because it is the only cheap moment to discover
that the whole approach does not hold.
