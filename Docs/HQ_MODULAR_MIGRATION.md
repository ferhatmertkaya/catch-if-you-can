# HQ Modular House — the measured contract

**Normative for the environment migration.** Every number here was measured from the
imported pack by `Catch If You Can → Modular Interior → Architecture Forensics`, not
estimated. The pack itself is local and gitignored, so this document is the only place
these numbers survive.

Where a number here disagrees with something in the code, the code is wrong.

## What the pack is, and what it is not

It is **not** a modular kit. It is a set of walls exported from one authored apartment
scene. Three independent measurements say so, and any one of them alone would be enough:

**The pivots are not at the pieces.** On 9 of 12 wall prefabs the origin sits 7 to 29
metres from the mesh it belongs to — 25.90 m for prefab 4, 24.65 m for prefab 11. That is
the world origin of the scene these were exported from. A kit meant for snapping has the
pivot on the piece; placing these by pivot at 0, 4, 8 puts them in three different rooms.

**The demo scene has no grid.** Mesh `5` appears 80 times at 66 distinct X positions, and
the commonest gap between them is 0.10 m. That is hand placement. The only repeating
structural distance anywhere in the scene is 14.20 m, and that is the spacing of the
floor planes described below.

**The UVs are per-piece.** Every wall maps its texture 0..1 across its own width, so the
same material appears at a different scale on every piece:

| prefab | width | 1 / width | measured U/m |
|---|---:|---:|---:|
| 5 | 3.95 m | 0.253 | 0.25 |
| 15 | 7.95 m | 0.126 | 0.15 |
| 16 | 11.90 m | 0.084 | 0.10 |
| 13 | 1.90 m | 0.526 | 0.55 |

A tiling kit has world-scale UVs so neighbouring pieces line up. These do not.

## Floors, ceilings, stairs

**There are none.** Zero floor or ceiling parts exist under `interior/`. The demo builds
both from Unity's built-in Plane — 10 × 10 units, 121 vertices — scaled 1.45 to reach
14.35 m, and flipped with a Y scale of −3.00 where it serves as a ceiling. 52 of them.
A scaled primitive is not a module.

No stairs. No railings. In any of the 105 prefabs.

## The openings, which are the usable part

Measured geometrically: every triangle projected onto the wall plane, glass and door
leaves excluded from the occupancy grid, largest enclosed empty rectangle taken as the
opening. A leaf named `door` is the panel that swings and is listed separately.

| prefab | kind | opening W × H | sill | lintel | wall W × H × T |
|---|---|---|---:|---:|---|
| 1 | door | **1.25 × 2.60** | 0.05 | 1.40 | 4.00 × 4.05 × 0.65 |
| 11 | door | **1.25 × 2.60** | 0.05 | 1.45 | 4.00 × 4.10 × 0.35 |
| 6 | window | 1.10 × 0.90 | 1.55 | 1.65 | 3.35 × 4.10 × 0.40 |
| 7 | window | 2.05 × 0.90 | 1.55 | 1.65 | 3.75 × 4.10 × 0.40 |
| 8 | window | 1.70 × 0.95 | 1.55 | 1.65 | 6.75 × 4.10 × 0.40 |
| 9 | window | 1.15 × 1.25 | 2.00 | 0.90 | 3.80 × 4.15 × 0.35 |
| 2 | arch | 2.95 × 3.05 | 0.00 | 1.05 | 4.00 × 4.05 × 0.40 |
| 3 | arch | 1.55 × 3.25 | 0.00 | 0.85 | 3.85 × 4.10 × 0.25 |

Door leaves: `door` 1.35 × 2.60 × 0.50 in prefab 1, `door001` 1.30 × 2.65 × 0.30 in 11.

### What fits a 3.00 m room

- **Doors: yes.** 1.25 × 2.60 against the project's 1.20 × 2.20 — five centimetres wider
  and forty taller, and a taller opening is still passable. The door socket at 1.10 m sits
  inside it either way.
- **Windows 6, 7, 8: yes.** Sill 1.55 plus 0.90–0.95 puts the head at 2.45–2.50 m, with
  half a metre of wall left below a 3.00 m ceiling.
- **Window 9: no.** Sill 2.00 plus 1.25 reaches 3.25 m.
- **Arches 2 and 3: no.** 3.05 and 3.25 m tall.

## Materials

All 105 prefabs are on `Universal Render Pipeline/Lit`; none is left on a built-in shader.
The wall family uses:

| material | tiling | maps |
|---|---|---|
| `wallpaper3` | 1.50 | BaseMap, BumpMap, DetailNormalMap, OcclusionMap |
| `wallpaper1` | 3.00 | BaseMap, BumpMap, DetailNormalMap, OcclusionMap |
| `beton` | 1.50 | BaseMap, BumpMap, SpecGloss, Parallax, AO, MetallicGloss |
| `tile1` | 1.06 × 1.20, offset 2.10 / 0.63 | BaseMap, BumpMap, SpecGloss |
| `white` | 1.00 | BaseMap, BumpMap, SpecGloss, MetallicGloss |
| `Steklo` (glass) | 1.30 | BaseMap |

Because the UVs are per-piece rather than world-scale, a material cannot simply be put on
generated geometry — it would appear at a different size than on the piece beside it. It
CAN be matched exactly, because generated geometry has UVs we write ourselves. Taking
prefab 5 as the reference for `wallpaper3`:

```
pattern density  U = 1.5 / 3.95 = 0.3797 per metre
                 V = 1.5 / 4.00 = 0.3750 per metre

generated wall W x H  ->  U span = 0.3797 * W,  V span = 0.3750 * H
6.00 x 3.00 m         ->  U 2.278,  V 1.125
```

That is exact. No stretching, no seam.

## Colliders

100 of 105 prefabs carry no collider; 5 carry MeshColliders, all in demo assemblies.
Vendor prefabs are never modified, so collision is generated on CIYC-owned wrappers:

| role | collider |
|---|---|
| solid wall | one BoxCollider |
| door wall | **three** boxes — left, right, lintel — so the opening stays passable |
| window wall | one box, split around the opening only if the window must block |
| floor | one BoxCollider |
| ceiling | none, unless the head must stop |
| stairs | one inclined box; a ramp is cheaper than a staircase and the controller prefers it |

Never a MeshCollider across vendor geometry.

## NavMesh

`NavMeshRuntimeBuilder.ShouldInclude` currently tests `CompareTag("Environment")` or a name
containing "Floor" or "Wall", evaluated on the MeshFilter's own GameObject — vendor child
objects, whose names we do not assign. The builder knows what it created; it should say so
rather than let the collector guess. The contract is a CIYC component carrying role and
walkability, and the collector reading that component. A component rather than a tag:
Unity throws on an undefined tag, and a component can carry the role as well.

## Compatibility with the 6 × 3 × 6 cell: **C — adapter fit**

Not A or B: there is no module and no snap grid to tile with. Not D: nothing is scaled.
Not E: the logical cell does not change.

It is an adapter fit with the roles reversed from what one would expect. CIYC generates
the structure at exact dimensions with computed UVs; the pack supplies the materials and
the small pieces that are genuinely compatible — the door leaves and the window inserts.

What may and may not move:

- **Negotiable:** `PrimitiveRoomFactory.DoorWidth` (1.2) and `DoorHeight` (2.2). Private
  constants, Stage B, absent from the engine-free assembly and from `LayoutHash`.
- **Not negotiable:** the 6 × 3 × 6 cell (`SizeMm`) and `RoomSocketLayout.DoorHeightMm`
  (1100). Both are in the deterministic assembly and both are written into the hash, along
  with `DoorMask`, `OpenMask`, `Cell`, `VariantIndex`, `RotationIndex` and `PositionMm`.

## The production path

```
HouseLayout                          deterministic, untouched
    v
HQModularRoomAssembler
    v
6 x 3 x 6 CIYC structural cell
    +-- generated wall / floor / ceiling, exact size, computed UVs
    +-- HQ materials: wallpaper3, wallpaper1, beton, tile1, white
    +-- HQ inserts: door leaf from 1 or 11, window from 6 / 7 / 8
    +-- opening 1.25 x 2.60, socket at 1.10
    +-- generated BoxColliders
    +-- StructuralSurface registration
          v
       NavMesh
```

The assembler consumes the layout and never produces one. That is checked by
`Scripts/check_hq_environment.sh`.
