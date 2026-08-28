# Determinism Specification

Status: normative. Any change that violates a rule in this document is a bug,
regardless of whether it looks correct in the Editor.

Applies to: `Assets/CatchIfYouCan/Scripts/Procedural/**`, plus every system
listed in the [Deterministic set](#2-the-deterministic-set).

---

## 1. The invariant

> Given the same **seed** and the same **content revision**, every client in a
> session must construct a bit-identical world before any player is spawned.

"Bit-identical" is defined by the [layout hash](#6-the-layout-hash), not by
eyeballing. A layout that looks the same but hashes differently is a failure.

Two properties follow, and both matter:

- **Safety.** A mismatch must be *detected*, not tolerated. A client that
  generated a different house is not "slightly off"; its ghost room, hide
  spots and evidence positions refer to geometry other players do not have.
- **Detectability.** A divergence that leaves the RNG stream in sync is far
  more dangerous than one that desyncs it, because nothing downstream notices.
  See [violation V4](#94-v4-prop-placement-reads-the-live-physics-scene) — the
  bug currently in the tree is exactly this shape.

---

## 2. The deterministic set

Determinism is expensive. Buy it only where it is load-bearing.

### 2.1 Must be deterministic (generation-time, hashed)

| System | Entry point |
|---|---|
| Room graph | `HouseLayoutGraph.Build` |
| Room instancing and placement | `ProceduralHouseGenerator.GenerateInternal` |
| Door / opening resolution | `ConnectDoors`, `SealUnusedOpenings` |
| Prop selection and placement | `PropSpawner.SpawnProps` |
| Ghost room assignment | `AssignGhostRoom` |
| Hide-spot set | `CollectHideSpots`, `EnsureMinimumHideSpot` |
| Interactable installation | `InstallRoomInteractables` |
| Ghost type, traits and tier | mission roll (host) |
| Objective set and evidence assignment | mission roll (host) |
| Weather selection | `WeatherSystem` |

### 2.2 Must NOT be forced deterministic (cosmetic, client-local)

Audio jitter, footstep shuffle bags, UI flicker, loading tips, particle noise,
camera shake, `PsychologicalAudioDirector`, `HorrorSilenceSystem`.

These may keep using `UnityEngine.Random` **provided rule R2 holds** — they must
never draw from a stream that generation depends on, and must never feed a
hashed value.

### 2.3 Replicated, not recomputed

Ghost AI decisions, hunt start/stop, interaction outcomes, physics of thrown
objects, and NavMesh pathing are **not** made deterministic. They are simulated
on the host and replicated. See `Docs/NETWORKING.md` §4.

Rationale: PhysX is not cross-platform bit-reproducible (`m_EnableEnhancedDeterminism: 0`
in `ProjectSettings/DynamicsManager.asset:34`, and enhanced determinism is
same-binary-only in any case), and runtime NavMesh bakes carry no cross-platform
output guarantee. Attempting lockstep here is the single fastest way to burn the
project's schedule.

---

## 3. Hard rules

Numbered so review comments can cite them.

**R1 — One PRNG, and we ship it.**
Generation code must use `CiycRandom` ([§4](#4-the-prng-contract)). `System.Random`
is banned in the deterministic set: .NET documents its algorithm as
implementation-defined and changed it in .NET Core 3.0. Mono and IL2CPP agree
today by accident of a shared corlib, not by contract, and a Unity upgrade may
silently change layouts for existing seeds.

**R2 — `UnityEngine.Random` is banned in the deterministic set, including seeding it.**
It is a single process-global stream shared with ~100 cosmetic call sites whose
draw *count* depends on frame rate, audio settings and how long a loading screen
was visible. Nothing that consumes it can be reproducible. `SeedManager.SetSeed`
must stop calling `UnityEngine.Random.InitState`; seeding it creates the false
impression that the cosmetic systems are now safe to hash.

**R3 — Every ordered decision iterates an ordered collection.**
`Dictionary<K,V>` and `HashSet<T>` enumeration order is unspecified and varies
with insertion history and hash codes. Reading them is fine; *iterating* them to
drive a decision or to feed the hash is not. Use `List<T>`, arrays, or sort with
an explicit, culture-invariant comparer (`StringComparer.Ordinal`, or an integer
key). Never `string.CompareTo` / `StringComparer.CurrentCulture`.

**R4 — Shuffle with Fisher–Yates, never with a random sort key.**
`OrderBy(_ => rng.Next())` is banned. LINQ does not specify how many times or in
what order it invokes a key selector, and duplicate keys are resolved by
implementation-defined stability. Use `CiycRandom.Shuffle`.

**R5 — No physics queries during generation.**
`Physics.OverlapBox`, `Raycast`, `CheckSphere`, `ComputePenetration` and friends
read the live PhysX scene, whose contents depend on frame timing, deferred
`Object.Destroy`, and whether `Physics.SyncTransforms` has run
(`m_AutoSyncTransforms: 0`, `DynamicsManager.asset:23`). Placement collision must
be resolved analytically against the generator's own occupancy grid / AABB list.

**R6 — No physics simulation during generation.**
Nothing settles. Props are placed at computed transforms and are kinematic until
generation completes and the hash is agreed.

**R7 — No engine or wall-clock state as generation input.**
Banned: `Time.time`, `Time.deltaTime`, `Time.frameCount`, `DateTime.Now`,
`Application.targetFrameRate`, `Screen.*`, `SystemInfo.*`, quality level,
`Application.isEditor`, `#if UNITY_EDITOR` branches that change output.

**R8 — Generation is synchronous and frame-atomic.**
No coroutines, `await`, or `yield return` inside the generation call tree. If the
frame cost becomes unacceptable, split by *seeded work unit* (each unit taking an
explicit sub-stream), never by "spread over N frames".

**R9 — Cleanup must be immediate.**
Generation must not depend on `Object.Destroy`'s deferred teardown. Either
destroy immediately or, better, generate into a fresh root and swap. Retry
attempts must observe a clean slate; see [V5](#95-v5-generation-retries-observe-the-previous-attempts-leftovers).

**R10 — No transcendental math in hashed values.**
IEEE-754 makes `+ - * / sqrt` bit-exact given a fixed evaluation order, but
`Sin`, `Cos`, `Atan2`, `Pow`, `Exp`, `Log` route to platform libm and are not
bit-identical across Mono/IL2CPP/ARM/x64. Compilers may also contract `a*b+c`
into an FMA with different rounding. Therefore: derive layout geometry from
integer grid cells and axis-aligned direction enums (the generator already
works this way), and never hash a raw float — see R11.

**R11 — Quantize before hashing.**
Positions and rotations enter the hash as fixed-point integers
(`(int)MathF.Round(v * 1000f)` per component; rotations as one of four cardinal
indices). This keeps the hash robust against last-bit float noise while still
catching every difference that a player could perceive.

**R12 — Content parity is part of determinism.**
A seed is meaningless without the asset set it indexes. `propDefinitions` is a
`[SerializeField]` array (`ProceduralHouseGenerator.cs:20`) — its *inspector
order* selects which prop `PickWeighted` returns. Reordering it, or shipping a
client with a different `Assets/External` payload, changes layouts. The content
hash ([§6.2](#62-the-content-hash)) covers this and is compared in the same
handshake as the seed.

---

## 4. The PRNG contract

`CiycRandom` is a PCG32 (`pcg_oneseq_32`). It is pure 64-bit integer arithmetic:
identical on Mono, IL2CPP, ARM64 and x64, with no dependence on compiler
settings, and it is not a security primitive and does not need to be.

```csharp
// Assets/CatchIfYouCan/Scripts/Procedural/CiycRandom.cs
using System.Collections.Generic;

public struct CiycRandom
{
    private const ulong Mult = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _inc;

    public CiycRandom(ulong seed, ulong stream)
    {
        _state = 0UL;
        _inc = (stream << 1) | 1UL;   // must be odd
        NextUInt();
        unchecked { _state += seed; }
        NextUInt();
    }

    public uint NextUInt()
    {
        unchecked
        {
            ulong old = _state;
            _state = old * Mult + _inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }
    }

    /// Unbiased [0, bound). Rejection sampling — deterministic draw count
    /// for a given stream position, which modulo folding would not be.
    public uint NextUInt(uint bound)
    {
        uint threshold = (uint)((0x1_0000_0000UL - bound) % bound);
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold) return r % bound;
        }
    }

    public int NextInt(int minInclusive, int maxExclusive) =>
        minInclusive + (int)NextUInt((uint)(maxExclusive - minInclusive));

    /// [0,1) with an exact 24-bit mantissa — one multiply, no rounding ambiguity.
    public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

    public float NextFloat(float min, float max) => min + (max - min) * NextFloat();

    public void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = (int)NextUInt((uint)(i + 1));
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
```

Constants are frozen. Changing `Mult`, the seeding sequence, or `NextFloat`'s
scale invalidates every stored seed and every golden test — treat it as a
content revision bump ([§6.2](#62-the-content-hash)).

---

## 5. Stream separation

A single shared stream makes every subsystem's draw count a global dependency:
adding one `NextFloat()` to prop placement silently relocates the ghost room.
Each subsystem therefore gets its own stream from the same session seed.

```csharp
public enum CiycStream : ulong
{
    Layout       = 1,
    Rooms        = 2,
    Doors        = 3,
    Props        = 4,
    Interactables= 5,
    GhostRoom    = 6,
    HideSpots    = 7,
    Weather      = 8,
    GhostIdentity= 9,
    Objectives   = 10,
}

var rng = new CiycRandom((ulong)sessionSeed, (ulong)CiycStream.Props);
```

Rules:

- Stream ids are append-only. Never renumber, never reuse a retired id.
- Retry attempts vary the **seed**, not the stream:
  `new CiycRandom((ulong)seed + (ulong)attempt * 0x9E3779B97F4A7C15UL, stream)`.
  The current `seed + attempt * 7919` (`ProceduralHouseGenerator.cs:63`) works but
  collides across nearby seeds; the golden-ratio constant does not.
- A subsystem must not read another subsystem's stream.

---

## 6. The layout hash

### 6.1 What is hashed

FNV-1a 64-bit over a **canonically ordered** byte stream. The order is fixed by
this spec, not by whatever order the generator happens to produce.

```
seed                              : int32
contentHash                       : uint64      (§6.2)
roomCount                         : int32
for each room, ordered by NodeId ascending:
    nodeId                        : int32
    category                      : int32
    gridCell.x, gridCell.y        : int32, int32
    moduleId                      : int32       (stable id, NOT prefab name)
    doorMask                      : uint8       (N/E/S/W bitfield)
edgeCount                         : int32
for each edge, ordered by (min(NodeAId,NodeBId), max(...)) ascending:
    nodeAId, nodeBId              : int32, int32
    directionFromA                : int32
propCount                         : int32
for each prop, ordered by (nodeId, socketIndex) ascending:
    nodeId, socketIndex           : int32, int32
    propDefinitionId              : int32       (stable id, NOT array index)
    qx, qy, qz                    : int32       (position, R11 quantization)
    cardinalRotation              : int32       (0..3)
ghostRoomNodeId                   : int32
hideSpotCount                     : int32
for each hide spot, ordered by (nodeId, qx, qy, qz):
    nodeId, qx, qy, qz            : int32 x4
weatherType                       : int32
```

`moduleId` and `propDefinitionId` must be **stable authored ids** on the
definition assets. Array indices and prefab names are not stable across content
edits and will produce false mismatches on every reorder.

### 6.2 The content hash

Computed once at build time and baked into a generated `ContentRevision.cs`:
FNV-1a over, in sorted order, every `(stableId, propName, weight, boundsSize
quantized, roomTags)` tuple in the prop and room definition sets, plus a
`FORMAT_VERSION` constant that is bumped by hand whenever this spec's hash
layout or the PRNG constants change.

A content-hash mismatch is a *different error* from a layout-hash mismatch and
must be reported as such: it means the clients are running different builds, and
no amount of seed agreement will help.

### 6.3 When it is computed

Immediately after `GenerateInternal` returns and `HouseValidator.Validate`
passes, before any player, ghost or equipment is spawned.

---

## 7. The mismatch protocol

1. Host generates, hashes, and broadcasts `(seed, contentHash, layoutHash)`.
2. Each client generates from the received seed and computes its own hashes.
3. Client compares. On any difference it must **abort the session, not repair it.**
   There is no partial-resync path: the divergent client cannot be patched into
   agreement because it does not know which of its thousands of decisions differed.
4. The aborting client uploads a diagnostic bundle:
   - `seed`, both content hashes, both layout hashes
   - platform, Unity version, IL2CPP/Mono, device model
   - a per-section hash breakdown (rooms / edges / props / ghostRoom / hideSpots)
     so the failing *stage* is identifiable without a repro

Per-section hashes are cheap and are the difference between a five-minute fix and
a week of bisecting. Emit them always, not just on failure.

Development builds additionally dump both full layout descriptors to disk for
diffing. Release builds send hashes only.

---

## 8. Required tests

These gate the vertical slice. A phase is not complete without them.

- **T1 — Repeat.** 1000 seeds, generate twice in the same process, hashes equal.
  Catches shared-mutable-state bugs.
- **T2 — Interleave.** Generate seed A, then B, then A again; the two A hashes
  must match. Catches leftover-state bugs (V5) — T1 alone will not.
- **T3 — Golden.** 100 fixed seeds with committed expected hashes. Any change to
  generation must either leave these untouched or bump `FORMAT_VERSION`
  deliberately in the same commit.
- **T4 — Cross-platform.** T3 executed in CI on an IL2CPP iOS-arm64 build, an
  IL2CPP Android-arm64 build, and the Mono Editor. Same hashes, or the build
  fails. This is the only test that would have caught V1/V2, and it must run
  before Phase 15, not after.
- **T5 — Stream isolation.** Adding a draw to one stream must not change any
  other stream's output. Assert per-subsystem sub-hashes.
- **T6 — Frame-rate independence.** Generate under a forced 5 fps and a forced
  200 fps; hashes equal. Directly targets V4/V5.
- **T7 — Static analysis.** A CI grep failing the build on
  `UnityEngine.Random`, `System.Random`, `OrderBy(`, `Physics.`, `DateTime.Now`,
  or `Time.` inside `Scripts/Procedural/**`. Cheap, and it holds the line after
  everyone has forgotten this document.

---

## 9. Current violations

Audited at `aa8c431`. Every item below is live in the tree today. Ordered by how
quietly it breaks.

### 9.1 V1 — Layout PRNG is `System.Random`
`SeedManager.cs:12,27`; used by `HouseLayoutGraph.Build`, `ProceduralHouseGenerator`,
`PropSpawner`, `RoomDefinition.PickPrefab`. Violates **R1**.

### 9.2 V2 — `UnityEngine.Random` is seeded alongside it
`SeedManager.cs:20`. 103 cosmetic call sites share that global stream —
`GhostEventDirector` and `PsychologicalAudioDirector` draw from it on `Time.time`
schedules, so its position depends on frame rate. `WeatherSystem.cs:52` and
`GhostController` then make *gameplay* decisions from it. Violates **R2**.

### 9.3 V3 — Random-key sort
`HouseLayoutGraph.cs:256`: `Nodes.OrderBy(_ => rng.Next()).ToList()`. Violates **R4**.

### 9.4 V4 — Prop placement reads the live physics scene
`PropSpawner.cs:91`: `Physics.OverlapBox(...)` gates every prop spawn. Violates **R5**.

This is the highest-severity item in the audit, for a reason that is easy to miss:
the RNG draws (`rng.NextDouble()`, `PickWeighted`) happen *before* the overlap
test, so a client whose overlap test disagrees still has an identically-positioned
RNG stream. **The layout diverges while every RNG-based consistency check passes.**
Nothing downstream can detect it. This is precisely the failure mode a layout hash
exists to catch, and precisely the one that will not be caught by any check
weaker than a full hash.

Compounding it: with `m_AutoSyncTransforms: 0` and generation running
synchronously inside one frame, freshly `Instantiate`d rooms and props are never
written into the PhysX broadphase during generation. The overlap test therefore
queries a scene containing only *pre-existing* colliders — which is not what the
code reads as intended, and which brings us to V5.

### 9.5 V5 — Generation retries observe the previous attempt's leftovers
`ProceduralHouseGenerator.cs:61-75` retries up to `MaxGenerationAttempts`, each
attempt calling `ClearExisting()` (`:555`) → `DestroyImmediateSafe` (`:573`).
At runtime that branch is `Object.Destroy`, which is **deferred to end of frame**;
all attempts run inside one frame. So attempt *N*'s `Physics.OverlapBox` sees the
colliders of attempts *0..N-1*, still in the scene. Violates **R9** (and R5).

Consequences, all of which are real today:
- Attempt 1 does not produce the same house as a fresh generation with the same
  `attemptSeed`.
- The Editor takes the `DestroyImmediate` branch and *does* get a clean scene, so
  **the Editor and a player build generate different houses from the same seed.**
  Every Editor-side golden hash would be wrong on device.
- Regenerating within a session differs from generating after a scene load.

### 9.6 V6 — Session seed comes from `UnityEngine.Random`
`MissionManager.cs:140` and `MissionSelectUI.cs:187`. Not a determinism bug on its
own — a seed only has to be *agreed*, not reproducible — but the seed must become
host-authoritative and replicated before multiplayer. See `Docs/NETWORKING.md` §3.

### 9.7 V7 — Wall clock in the generation path
`InvestigationBootstrap.cs:308`: `System.DateTime.Now`. Currently feeds UI text
only, so it is not yet a hash input. Fence it off before it becomes one. **R7**.

### 9.8 V8 — Prop identity is an array index
`ProceduralHouseGenerator.cs:20` `[SerializeField] private PropDefinition[] propDefinitions`,
consumed in order by `PropSpawner.FilterProps`/`PickWeighted`. Inspector order is
layout-affecting content. Needs stable ids per **R12** before the hash in §6.1 can
be trusted.

### 9.9 Not a violation

`PropDefinitionFactory.AppendFromFolder` (`:37-38`) enumerates the filesystem, but
`Array.Sort(files, StringComparer.OrdinalIgnoreCase)` canonicalizes the order, and
the only callers are in `Assets/CatchIfYouCan/Editor/ExternalAssetIntegrator.cs`
— it runs at author time, not on device. Correct as written; keep it that way.

---

## 10. Migration order

Each step is independently shippable and testable. Do not reorder: the hash is
worthless before the physics dependency is gone, and the tests are worthless
before the hash exists.

| # | Work | Clears |
|---|---|---|
| 1 | Add `CiycRandom` + `CiycStream`; unit-test against PCG32 reference vectors | — |
| 2 | Port the generator to `CiycRandom`; drop `InitState`; Fisher–Yates | V1, V2, V3 |
| 3 | Replace `Physics.OverlapBox` with an analytic occupancy grid | V4 |
| 4 | Generate into a fresh root and swap; make retries observe a clean slate | V5 |
| 5 | Stable ids on prop/room definitions; build-time `ContentRevision` | V8, R12 |
| 6 | Implement the layout hash (§6) with per-section breakdown | — |
| 7 | Tests T1–T3, T5–T7 | — |
| 8 | CI cross-platform hash job (T4) | — |
| 9 | Seed replication + handshake — `Docs/NETWORKING.md` | V6 |

Steps 1–8 are single-player work and carry no networking dependency. They should
land **before** any netcode goes in: debugging a determinism bug through a
replication layer costs several times what it costs here.
