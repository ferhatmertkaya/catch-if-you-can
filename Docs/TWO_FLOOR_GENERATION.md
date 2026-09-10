# Two-floor generation — the audited path

Status: **audit and plan. No generator code has been changed.**
Owner: **20 Procedural Generation.** Reviewers: **34 Multiplayer Architecture**, **4 QA**.

`Docs/DETERMINISM.md` is normative and outranks this document.

---

## 1. Why this is a document and not a commit

Procedural generation is a protected hotspot. A house that is generated from a
seed must come out bit-identical on every machine, or two players walk through
different walls. The reference apartment was built by hand precisely so that
nobody had to touch the generator to get a two-storey interior on screen.

**Nothing here has been implemented.** What follows is what the audit found and
what the clean path looks like.

---

## 2. What the model already supports

| Piece | State |
|---|---|
| `GridCell.Y` | **Already the floor level.** Its own doc comment says so: *"Y is the floor level, so basements and attics can be expressed without changing the layout model."* |
| `RoomCategory` | Already has `Basement` and `Attic` alongside 13 others. |
| `LayoutRoom.PositionMm` | A `Vec3i`, so a room already carries a real height. |
| `LayoutHash` | Already folds `PositionMm` in, **including its Y**. |

That last row is the important one, and it cuts both ways.

**The good half:** height is already hashed, so a second storey needs no change
to the hashing rules and no new hash section.

**The half to be careful about:** because Y is already hashed, any change that
moves an *existing* room in Y changes that seed's layout hash and breaks the
golden seed table. Generation must stay byte-identical for every layout that is
single-storey today.

---

## 3. What is missing

| Gap | Consequence |
|---|---|
| The builder never emits `Y != 0` | Every generated house is one storey. |
| `SocketDirection` is horizontal only — North, East, South, West | There is no way to express "this room connects to the one above it". **This is the real blocker.** |
| No stair concept anywhere in `Scripts/Procedural/` | Two floors with no way between them. |
| `NavMeshRuntimeBuilder` | Not audited for multi-level links; a NavMesh across storeys needs off-mesh links or a carefully built surface. |

---

## 4. The clean path

**Step 1 — extend `SocketDirection` by appending, never inserting.**
Add `Up` and `Down` *at the end* of the enum. Appending leaves every existing
member's numeric value untouched, so no serialised asset and no existing hash
changes. Inserting them in the middle silently renumbers North/East/South/West
and rewrites every layout in the project.

**Step 2 — a stair room archetype.** A room category or archetype that owns the
vertical connection, so a staircase is a room the layout knows about rather than
geometry bolted on afterwards. The reference apartment's stairwell — a straight
flight of 14 steps climbing `StoreyPitch` in a 1.9 × 3.4 m footprint — is the
shape to reproduce.

**Step 3 — floor-aware placement.** The builder places floor 0, then floor 1
constrained to sit above it, with at least one vertical connection. Determinism
rule: floor 1 must be drawn from the **same stream** in a fixed order, never from
a second RNG, or the two floors will disagree about their own seed.

**Step 4 — validation before generation.** `HouseValidator` gains: every floor
reachable from the entrance, every stair connects exactly two consecutive floors,
no room overlapping a stairwell void.

**Step 5 — navigation.** Multi-level NavMesh, or off-mesh links at each flight.
Not a layout concern, but a ghost that cannot climb stairs is a ghost that never
leaves the ground floor.

---

## 5. GenerationVersion

**A bump is required only if an existing single-storey seed produces a different
layout than it does today.** The rule to hold to:

> Adding the *capability* of a second floor is free. Changing what an existing
> seed produces is not.

If two-floor generation is gated behind a `MapDefinition` flag and single-storey
maps take exactly the path they take now, existing golden seeds stay valid and
`GenerationVersion` does not move. If the placement algorithm changes for
everybody, it moves, and every existing build becomes incompatible with itself —
which is a decision, not a side effect.

`Scripts/check_determinism.sh` (158 checks) and the golden seed table are the
gate. Neither may be rewritten to make a change pass.

---

## 6. Relationship to the reference apartment

```
ReferenceApartment  →  defines scale, room sizes, stair pitch, storey height
        ↓
   these become the generator's constraints
        ↓
Two-floor generator  →  reproduces those rules from a seed
```

The apartment is **not** a prototype of the generator and shares no code with it.
It is the thing the generator's output will be judged against. Its constants —
`StoreyHeight` 2.9 m, `SlabThickness` 0.3 m, `WallThickness` 0.18 m, door
1.05 × 2.15 m — are the numbers a generated house should also produce.
