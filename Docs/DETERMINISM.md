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
  The original V4 defect had exactly this shape — a physics query decided whether
  a prop spawned, while the RNG draws happened before the test, so the layout
  diverged with the RNG stream still perfectly in step. It is fixed
  ([§11](#11-resolved-violations)); the shape is worth remembering because only a
  full layout hash catches it.

---

## 2. The deterministic set

Determinism is expensive. Buy it only where it is load-bearing.

### 2.1 Deterministic / seed-derived (hashed)

Everything below is decided in Stage A from `(generationVersion, mapDefinitionId,
seed)` and contributes to the layout hash. All of it lives in
`HouseLayoutBuilder`; Stage B only instantiates the result.

| System | Where it is decided |
|---|---|
| House topology (room graph) | `HouseLayoutBuilder.Build` → `TryExpand`, `ForceRequiredRoom` |
| Room archetype and variant selection | `HouseLayoutBuilder.Assemble` (`Rooms`, `RoomVariants` streams) |
| Room placement | `HouseLayoutBuilder.Assemble` (grid cell → `PositionMm`) |
| Door / opening resolution | `BuildConnections`, `BuildDoors`; `OpenMask` per room |
| Socket layout | `RoomSocketLayout` (shared by Stage A planning and Stage B building) |
| Prop and furniture selection and placement | `PlaceProps` → `PlacePass` + `OccupancyGrid` |
| Ghost **room** assignment | `BuildGhostCandidates` (`GhostRoomCandidates` stream) |
| Hide-spot set | `BuildHideSpots` |
| Evidence **interaction points** (geometry) | `BuildEvidencePoints` |
| Equipment spawn anchors | `BuildEquipmentSpawns` |
| Weather selection | `HouseLayoutBuilder.Assemble` (`Weather` stream) |
| Generation-time content ordering | `ContentSnapshot` (sorted by stable id) |

If it feeds `LayoutHash`, it is in this table. If it is in this table, it must
obey every rule in [§3](#3-hard-rules).

### 2.1b Host-authoritative / replicated — NOT seed-derived

These are hidden round-answer and gameplay state. They are rolled by the
authoritative host, replicated to clients, and **never independently rolled by a
client**. They are deliberately **not** derivable from the session seed and are
**not** in the layout or content hash.

| System | Where it is decided |
|---|---|
| Ghost type | `MissionManager.PickGhost` |
| Ghost traits and tier | `MissionManager.ApplyDifficultyModifiers`, ghost definition |
| Mission selection | `MissionManager.SelectRandomMission` |
| Objective set | `ObjectiveManager` |
| Evidence **assignment** (which evidence this ghost yields) | mission / ghost definition |

The reason is not convenience. The session seed is public to every client at join
time: anything derived from it can be computed by a client before the round
starts. Deriving ghost identity from the seed would hand every player the answer
to the round. So these stay host-rolled and replicated, and may keep using
`UnityEngine.Random` on the host under [§2.2](#22-must-not-be-forced-deterministic-cosmetic-client-local)'s
constraint that they never feed a hashed value.

Note the split within "evidence": the *geometry* of evidence interaction points is
seed-derived (§2.1, hashed — every client must agree where they are), while *which
evidence a ghost yields* is host state (§2.1b, replicated — it is the answer).

Ghost **room** is likewise seed-derived and hashed: it is geometry every client
must agree on, and it is not by itself the round's answer.

This boundary is normative and matches `Docs/NETWORKING.md` §3 and §4. Should a
future protocol need any §2.1b item hashed, that requires an explicit protocol
change, not an implementation drift.

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
attempts must observe a clean slate — the V5 defect
([§11](#11-resolved-violations)) was exactly this, and it made the editor and a
player build disagree from the same seed.

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
A seed is meaningless without the asset set it indexes. The generator's
`propDefinitions` and `roomDefinitions` are `[SerializeField]` arrays, so their
*inspector order* is authored data. `ContentSnapshot` therefore sorts by stable
id, making authoring order irrelevant, and **rejects duplicate stable ids** —
duplicates would make that sort non-total and hand the ordering back to the input
order (see [§6.4](#64-the-content-hash)). Shipping a client with a different
`Assets/External` payload also changes layouts; the content hash covers that and
is compared in the same handshake as the seed.

---

## 4. The PRNG contract

`CiycRandom` (`Scripts/Procedural/Deterministic/CiycRandom.cs`) is a PCG32
(`pcg_oneseq_32`). It is pure 64-bit integer arithmetic: identical on Mono,
IL2CPP, ARM64 and x64, with no dependence on compiler settings. It is not a
security primitive and does not need to be.

The implementation is verified against the published PCG32 reference vectors
(seed 42, sequence 54) by both test suites. That check matters more than it
looks: without it, "deterministic" would only mean "consistently whatever this
code happens to do", and a future rewrite could silently change every stored
seed.

```csharp
public struct CiycRandom
{
    public CiycRandom(ulong seed, ulong stream);

    public static CiycRandom ForStream(int seed, CiycStream stream);
    public static CiycRandom ForStream(int seed, CiycStream stream, int attempt);

    public uint  NextUInt();
    public uint  NextUInt(uint bound);          // rejection sampling, unbiased
    public int   NextInt(int min, int maxExcl);
    public bool  NextBool();
    public float NextFloat();                   // [0,1), exact 24-bit mantissa
    public float NextFloat(float min, float max);
    public void  Shuffle<T>(IList<T> items);    // Fisher-Yates
    public int   PickWeightedIndex(IReadOnlyList<float> weights);
}
```

Notes on the choices that are easy to get wrong:

- `NextUInt(bound)` uses **rejection sampling**, not `% bound`. Modulo folding
  biases the low values, and the bias changes with `bound`.
- `NextFloat` is `(NextUInt() >> 8) * (1f / 16777216f)`: one shift and one
  multiply by a power of two, so there is no rounding ambiguity anywhere.
- Retry attempts vary the **seed**, never the stream, so streams stay isolated
  across attempts:
  `seed + attempt * 0x9E3779B97F4A7C15`. A small linear step (the old
  `seed + attempt * 7919`) collides between nearby seeds.

Constants and the seeding sequence are frozen and are part of
[`GenerationVersion`](#8-generation-version).

---

## 5. Stream separation

A single shared stream makes every subsystem's draw count a global dependency:
adding one `NextFloat()` to prop placement would silently relocate the ghost
room. Each subsystem draws from its own stream, derived from the same session
seed.

```csharp
public enum CiycStream : ulong
{
    Layout              = 1,   // room graph expansion
    Rooms               = 2,   // room archetype selection
    Corridors           = 3,   // hallway bridging for forced rooms
    Doors               = 4,   // reserved
    Furniture           = 5,   // furniture selection and placement
    Props               = 6,   // small prop selection and placement
    EvidenceSpawns      = 7,   // reserved
    GhostRoomCandidates = 8,   // ghost room scoring jitter
    HidingSpots         = 9,   // reserved
    EquipmentSpawns     = 10,  // reserved
    Weather             = 11,  // weather selection
    RoomVariants        = 12,  // prefab variant selection
}
```

Streams marked **reserved** are declared but not yet drawn from: doors, hide
spots, evidence points and equipment spawns are currently fully determined by
the room graph and need no randomness. They are numbered now so that adding
randomness to them later cannot renumber anything else.

Rules:

- Stream ids are append-only. Never renumber, never reuse a retired id.
- A subsystem must not read another subsystem's stream.
- `SeedManager.CreateRandom(stream)` is the entry point; there is no unnamed
  stream to fall into by accident.

---

## 6. The layout hash

### 6.1 Structure

`LayoutHasher.Compute` returns a `LayoutHash` carrying seven section hashes plus
the final composite. All are FNV-1a 64-bit.

```
Identity        generationVersion, seed, mapDefinitionId, contentHash, algorithmId
Rooms           roomId, archetypeId, category, gridCell, rotationIndex,
                positionMm, sizeMm, variantIndex, doorMask, openMask
Connections     connectionId, roomAId, roomBId, directionFromA
Doors           doorId, roomAId, roomBId, socketASlot, socketBSlot,
                positionMm, rotationIndex
Furniture       propInstanceId, propDefinitionId, kind, roomId, slot,
                positionMm, rotationIndex
Props           (same shape as Furniture)
GameplaySpawns  entranceRoomId, ghostRoomId, weatherIndex,
                hideSpots[], equipmentSpawns[], evidencePoints[],
                ghostRoomCandidates[] (roomId, scoreFixed)

FINAL           FNV-1a over the seven section hashes, in the order above
```

`LayoutHash.ToReport()` renders this as the diagnostic block a mismatch carries,
and `DescribeDifference` names the first differing section.

### 6.2 Canonical ordering

The hasher **re-sorts every collection into a local buffer before writing it**
rather than trusting the order the builder produced. Trusting the caller would
make the hash silently sensitive to a refactor nobody would think to re-test.
This is what test G asserts: reversing or rotating every collection must not
change the hash.

Sort keys: rooms by `roomId`; connections by `(roomAId, roomBId, direction)`;
doors by `(roomAId, roomBId, socketASlot)`; props by
`(roomId, slot, propDefinitionId, positionMm)`; anchors by
`(roomId, slot, positionMm)`; ghost candidates by `(score desc, roomId)`.

Connections are additionally stored with the lower room id always as A, so the
same adjacency hashes identically regardless of which side discovered it.

### 6.3 Identity, not indices

`ArchetypeId` and `PropDefinitionId` are **stable authored strings**
(`RoomDefinition.ResolveStableId`, `PropDefinition.ResolveStableId`), never array
indices or prefab names. The generator's `propDefinitions` array is
inspector-ordered: an index would renumber on every reorder and change layouts
for stored seeds.

### 6.4 The content hash

`ContentSnapshot.ContentHash` covers, in stable-id order, every room archetype
(`id, category, sizeMm, variantCount, weightFixed`) and prop archetype
(`id, kind, boundsMm, weightFixed, allowedCategories`), plus the algorithm id
and generation version.

Stable ids must be **unique**. `ContentSnapshot` rejects duplicates with
`DuplicateStableIdException` rather than tie-breaking them: the id sort is
single-key, and `List<T>.Sort` is an unstable introsort, so two entries sharing an
id would take their relative order from the authoring order — reintroducing
exactly the inspector-order dependence R12 exists to remove. A tie-break key would
produce a stable ordering while leaving two assets claiming one identity in the
project, hidden; rejection is the honest fix. `ContentSnapshotFactory` performs the
same check on the Unity side, where it can name the colliding *assets* rather than
just the id.

A content-hash mismatch is a **different error** from a layout-hash mismatch: it
means the clients are running different builds, and no amount of seed agreement
will help. Report it as such.

### 6.5 Hashing implementation

`Fnv1a64`, explicitly implemented in the repo. `string.GetHashCode()` and
`object.GetHashCode()` are banned from the hash path: .NET randomises string
hashing per process by default, so neither is a persistence or network contract.
Multi-byte values are written little-endian explicitly rather than through
`BitConverter`, whose byte order follows the host architecture. Strings are
length-prefixed UTF-8, so `"ab"+"c"` cannot collide with `"a"+"bc"`.

---

## 7. Quantization

`Quantize` is the single conversion contract; do not duplicate these rules
anywhere else.

| Quantity | Representation |
|---|---|
| Positions, sizes | integer millimetres (`Vec3i`), scale 1000 |
| Grid coordinates | integer `GridCell` (X, Y=floor, Z) |
| Rotations | cardinal index 0..3 (N, E, S, W) |
| Weights | fixed point, scale 1000 |

In practice Stage A goes further than the rule requires: it does **all** position
math in integer millimetres end to end, so there is no float in the geometry path
to quantize. `Quantize.Millimetres` exists for the two boundaries — authored
content coming in, and Unity world space going out.

Distance scoring uses `IntMath.Sqrt`, an exact integer square root, rather than
`Mathf.Sqrt`, keeping the whole ghost-room scoring path in integers.

---

## 8. Generation version

`GenerationVersion.Current` (currently **1**, algorithm id
`ciyc-house-gen-v1-pcg32`). A layout's identity is
`(generationVersion, mapDefinitionId, seed)`.

Increment it whenever any of these change:

- `CiycRandom` constants or the seeding sequence
- the order or count of draws in any stream
- stream id assignments
- the canonical hash layout in `LayoutHasher`
- `Quantize` scales
- any rule that alters which layout a seed produces

Bumping it invalidates the golden seed table; regenerate it **in the same
commit**, deliberately. Regenerating goldens to make a failing test pass erases
the only evidence that layouts changed for every stored seed.

`MapDefinition` carries the map identity and its tunables (`HOUSE_DEFAULT_A`,
`HOUSE_TRAINING_A`).

---

## 9. The mismatch protocol

1. Host generates, hashes, and broadcasts `(seed, contentHash, layoutHash)`.
2. Each client generates from the received seed and computes its own hashes.
3. Client compares. On any difference it must **abort the session, not repair
   it.** There is no partial-resync path: the divergent client cannot be patched
   into agreement because it does not know which of its thousands of decisions
   differed.
4. The aborting client reports `LayoutHash.ToReport()` — seed, both content
   hashes, and the per-section breakdown that names the failing stage — plus
   platform, Unity version, scripting backend and device model.

Section hashes are always computed, not only on failure: they are the difference
between a five-minute fix and a week of bisecting.

`ProceduralHouseGenerator` implements the local half of this today. On a Stage A
validation failure it logs an error with the full hash report and raises
`ProceduralHouseGenerator.GenerationFailed`; it does **not** silently substitute
a different seed. The previous code fell back to `KnownGoodSeed`, which is
exactly the silent repair that would desync a session — one client would quietly
have built a different house from everyone else.

Networking is not implemented; see `Docs/NETWORKING.md`.

---

## 10. Tests

Two suites assert the same properties.

| | |
|---|---|
| `Assets/CatchIfYouCan/Tests/EditMode/DeterminismTests.cs` | Unity EditMode, via Test Runner |
| `Tools/DeterminismHarness` | plain .NET, no Unity licence — what CI runs |

Both exist on purpose: the harness runs in CI, while the EditMode tests
additionally prove that `UnityEngine.Random` cannot perturb generation inside the
real engine, and that Unity's own toolchain produces the same hashes.

| Test | Asserts |
|---|---|
| PCG32 vectors | the RNG matches the published reference stream |
| **A** | same seed, 100 generations, one hash (3 seeds) |
| **B** | interleaving unrelated generation changes nothing |
| **C** | variable elapsed time and work between runs changes nothing |
| **D** | retry attempts are reproducible and uncontaminated by earlier attempts; consecutive attempts do explore different layouts |
| **E** | heavy `UnityEngine.Random` use cannot perturb generation, **and** generation does not advance `UnityEngine.Random` |
| **F** | 24 golden seeds (12 seeds × 2 maps) reproduce their recorded hashes |
| **G** | reversing or rotating every collection does not change the hash |
| Stream isolation | draining one stream does not move the layout; two streams from one seed are uncorrelated |
| Validity | 200 sampled seeds all produce valid layouts |
| Doorways | no prop is ever placed outside its room or in a door approach zone |
| Section hashes | moving one prop changes only the Props section |
| Quantization | symmetric, exact, rotation wrapping |
| FNV-1a | stable, and length-prefixed against boundary collisions |

### The static guard

`Scripts/check_determinism.sh` fails the build on a reintroduced
`UnityEngine.Random`, `System.Random`, `Physics.*`, `OrderBy(`, `DateTime.Now`,
`Time.*`, `using UnityEngine`, or a `GetHashCode()` used for hashing anywhere in
the deterministic core, and on RNG or physics queries in the Stage B files. It
comments-strips first so it does not fire on the comments that explain the rules.
It then runs the full suite.

`.github/workflows/determinism.yml` runs it on every push and pull request.

This is the part that holds the line after everyone has forgotten the design
discussion: a reviewer will not catch a reintroduced `Random.Range` in a
400-line diff, but the guard will.

### Still outstanding

**T4 cross-platform hashing is not yet automated.** Proving that an IL2CPP
iOS-arm64 build and an IL2CPP Android-arm64 build produce identical hashes needs
Unity build agents, which CI does not have.

A second, smaller blocker sits in front of T4: `ProjectSettings/ProjectVersion.txt`
records `m_EditorVersionWithRevision: 6000.3.0f1 (catchifyoucan)`. The revision
field holds the literal string `catchifyoucan`, not a Unity revision hash. The
Editor tolerates this, but any CI that provisions Unity by revision — which is how
most Unity build actions pin a version — cannot resolve it. The true revision is
not recoverable from anything in this repository and **must not be guessed**:
substituting a plausible-looking hash would produce a CI job that silently builds
on the wrong Unity patch, which is precisely the kind of environment drift a
cross-platform determinism test exists to detect. Recover it from whoever created
the project, or pin by version string if the chosen CI action supports it. The core is engine-free integer
arithmetic, which is the strongest structural argument available, and the
`noEngineReferences` assembly flag enforces it at compile time — but that is an
argument, not a measurement. Until a device job exists, run the EditMode suite on
a physical iOS and Android build before shipping a generation change.

---

## 11. Resolved violations

All eight violations from the original audit are fixed.

File and line references in the **Was** column below describe the code *as
audited at commit `aa8c431`*. They are a historical record and will not resolve
against the current tree — that is intentional, so the audit stays readable as
what it was. Every other reference in this document names current code.

| | Was | Now |
|---|---|---|
| **V1** | Layout PRNG was `System.Random` (`SeedManager.cs:12,27`) | `CiycRandom` (PCG32), verified against reference vectors |
| **V2** | `SeedManager.SetSeed` also seeded `UnityEngine.Random`, a global shared with ~100 cosmetic call sites | `InitState` removed entirely; `SeedManager` hands out named streams only. Test E asserts the isolation in both directions |
| **V3** | `Nodes.OrderBy(_ => rng.Next())` (`HouseLayoutGraph.cs:256`) | Removed outright. Candidates are built in canonical order and picked uniformly — the shuffle never affected the distribution, so it needed deleting, not replacing |
| **V4** | `Physics.OverlapBox` gated every prop spawn (`PropSpawner.cs:91`) | `OccupancyGrid`: integer AABB occupancy over data the layout already owns, including reserved door approach zones |
| **V5** | Retries instantiated, destroyed and re-instantiated inside one frame with deferred `Object.Destroy`, so attempt N saw attempts 0..N-1 — and the editor and a player build disagreed | Stage A retries on pure data; nothing is instantiated until a layout validates. Stage B builds into a fresh root and swaps |
| **V6** | Session seed from `UnityEngine.Random` | `SessionSeedSource.Next()`, a cryptographic source, host-authoritative in multiplayer. **Corrected in a later pass:** the first fix landed on `RollMissionSeed`, which had zero callers, while the live path (`MissionManager.StartInvestigation`) still used `UnityEngine.Random`. The live path now uses `SessionSeedSource` and the dead method is deleted, so there is one authoritative seed-selection path |
| **V7** | `DateTime.Now` in the generation path | Fenced to presentation-only case text, with an invariant culture |
| **V8** | Prop identity was an array index into an inspector-ordered array | Stable authored ids; `ContentSnapshot` sorts by id so inspector order cannot influence generation |

Two further fixes fell out of the work:

- `HouseLayoutGraph.cs` referenced `UnityEngine.Vector2Int` without importing
  `UnityEngine` and with no local definition — **the file did not compile**. It
  is now a projection over `HouseLayout` using the pure `GridCell`.
- `PrimitiveRoomFactory` walked a `HashSet<SocketDirection>` to decide which
  walls to seal: unspecified enumeration order deciding geometry. It now
  iterates the frozen cardinal order from the layout's masks.

---

## 12. Architecture

```
seed + generationVersion + mapDefinitionId + ContentSnapshot
                      |
                      v
        STAGE A   HouseLayoutBuilder          (engine-free, pure)
                      |
                      +--> LayoutValidator --> retry on failure (pure data)
                      |
                      v
                  HouseLayout                 (immutable, authoritative)
                      |
                      +--> LayoutHasher ----> LayoutHash (7 sections + final)
                      |                             |
                      |                             +--> multiplayer handshake
                      |                             +--> golden seed tests
                      |                             +--> LayoutDiff / compare window
                      v
        STAGE B   ProceduralHouseGenerator.Instantiate
                      |
                      +--> rooms, doors, props, lights, hide spots
                      +--> colliders            (an OUTPUT, never an input)
                      +--> NavMesh bake         (host-authoritative, not hashed)
```

Stage A makes every decision. Stage B makes none: it has no RNG, performs no
physics queries, and reads nothing from the scene. That split is what makes the
determinism testable — the whole of Stage A runs in a plain .NET console app.

### Where the code lives

| Path | Role |
|---|---|
| `Scripts/Procedural/Deterministic/` | the pure core; own assembly, `noEngineReferences: true` |
| `Scripts/Procedural/ProceduralHouseGenerator.cs` | Stage B instantiation |
| `Scripts/Procedural/ContentSnapshotFactory.cs` | the one boundary where authored floats become generation input |
| `Scripts/Procedural/SeedManager.cs` | session seed and named streams |
| `Tests/EditMode/DeterminismTests.cs` | Unity suite |
| `Tools/DeterminismHarness/` | standalone suite, golden generation |
| `Editor/DeterminismTools.cs` | Tools > Catch If You Can > Determinism |
| `Scripts/check_determinism.sh` | static guard + suite |

### Editor tooling

**Tools > Catch If You Can > Determinism**

- *Generate Golden Seeds* — rewrites the committed table, behind a confirmation
  that explains when it is legitimate
- *Validate Golden Seeds* — checks every entry, reports failures to the Console
- *Compare Two Layouts* — generates two layouts and reports the **first**
  authoritative difference (`room 8: grid (4,0,8) vs (5,0,8)`) plus both section
  breakdowns
- *Print Layout Report* — hash report for the current session seed

---

## 13. Remaining risks

Honest list of what is still not proven.

1. **Cross-platform hashing is argued, not measured.** See T4 above.
2. **Unity's `float` behaviour is avoided rather than trusted.** Stage A uses
   integers for geometry, but weighted selection still accumulates `float`
   weights in a fixed order. IEEE-754 addition is exact given fixed order, and
   the ordering is canonical, so this is sound — but it is the one remaining
   float in a decision path. Moving weights to fixed-point integers would close
   it completely.
3. **`ContentSnapshot` is built at runtime from ScriptableObjects.** If two
   builds ship different assets, the content hash catches it at the handshake —
   but only at the handshake. A build-time baked content revision would catch it
   earlier.
4. **The NavMesh is not hashed.** Deliberate: a runtime bake carries no
   cross-platform bit-identity guarantee. Ghost pathing must therefore stay
   host-authoritative; if it were ever made client-predicted, this would become a
   divergence source.
5. **Room prefab variants are content.** `GetPrefabVariant` indexes
   `PrefabVariants` by the layout's variant index, so reordering that array
   changes which prefab a seed produces. `VariantCount` is in the content hash,
   but the array *order* is not. Reordering variants without changing the count
   would not be detected.
