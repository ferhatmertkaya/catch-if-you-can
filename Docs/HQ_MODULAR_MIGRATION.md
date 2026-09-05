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

`ModularRoomBuilder` is that assembler. It is built and wired; what it draws depends
entirely on what the catalog names.

## The first room

`Catch If You Can → Modular Interior → Build ONE Test Room` builds a single 6 × 3 × 6 cell
through the production path — the same `ModularRoomBuilder` the house generator calls — and
prints what it made: renderers, triangles, colliders, every material with its shader, its base
map size and its achieved tiling, and the hierarchy.

One room, deliberately. Processing the whole pack is what made the machine unusable, and
converting a whole house before a single room has been looked at is how one mistake is made
forty times. The tool scans nothing and imports nothing: it reads the catalog asset, which
holds object references rather than a folder to walk.

What the room contains: floor, ceiling, four walls, a doorway on the north wall at
1.20 × 2.20, and a window on one of the open walls at **2.05 × 0.90 on a 1.55 m sill** — the
pack's own measured window 7, whose head lands at 2.45 m with 0.55 m of wall left under a
3.00 m ceiling. Windows 6 and 8 fit the same way. Window 9 does not, and neither arch does.

Collision stays CIYC's: one box for a floor, one across a solid wall, one across a *window*
wall — a window is not a way through, and splitting it around the opening would let the player
climb out — and three (left, right, lintel) for a doorway, taken from the same rectangles the
mesh was built from. Every collider a vendor insert brings is switched off, and its renderers
stop casting shadows. Switched off rather than destroyed: `Destroy` is deferred and
`DestroyImmediate` is edit-mode only, and choosing between them by context is how this project
once got an editor house and a device house that differed.

## Surfaces: measured, never assumed

`ModularInteriorCatalog` carries three `SurfaceMaterial` entries — wall, floor, ceiling —
each a material plus **repeats per metre**. The second half is not decoration. Because the
pack normalises UVs per piece, a tiling of 1.5 is 0.38 repeats/m on the 3.95 m piece and 0.13
on the 11.90 m one; generated geometry writes its UVs in metres, so the density has to be
restated or the wallpaper is a different size on every surface.

`Audit Pack → 2. Katalog bauen` fills those three entries itself. Preference order is
`wallpaper3 → wallpaper1 → beton` for walls, `tile1 → beton → wallpaper1` for floors,
`white → beton → wallpaper1` for ceilings, and the commonest material if none of those names is
present — so a renamed pack still produces a textured room.

Three things about that measurement, each of which produced a visibly warped room when it was
wrong:

**World metres, not the mesh's own space.** The rule that says measure in the model's own space
answers a different question — what *local scale* reaches a wanted size — and it is the wrong
rule here, because what the texture is stretched across is the piece's real width. This pack's
own demo scales a Unity Plane by 1.45 to reach 14.35 m; read without its transform that piece
reports 10 m and every density taken from it is off by a third.

**The median of every piece, not the first one found.** The pack applies one material at
0.55 U/m on one piece and 0.10 on another — a spread of five and a half — so whichever prefab
happened to be enumerated first was setting the texture size for the whole house. Where the
spread is worse than 2×, the report says so: the median is then a *choice*, and a number that
was chosen must not look like a number that was found.

**Every map, not the colour map.** What is stored is the size the material is authored across,
because that is the divisor, and *every* texture property is divided by it. Rescaling the base
map and the normal and leaving the detail normal, the occlusion and the parallax where they were
does not read as a wrong size — it reads as a warped surface, because the bumps stop sitting on
the pattern they belong to. Divided rather than overwritten, so a detail map deliberately tiled
eight times finer keeps that relationship.

The vendor material is never edited. The divisor goes onto a **copy**, one per surface for the
whole house, so this is three materials rather than three per room.

## Wall UVs are projected, not counted from a corner

A wall with an opening is four boxes: left, right, header and sill. Each face used to start its
UV at `(0,0)` and run to `(span, span)`, so every section restarted the pattern at zero — the
wallpaper jumped at every doorway, and the header showed a slice of pattern that lined up with
nothing beside it. `StructuralMeshFactory` now projects each vertex onto the face's own two
axes, so the whole wall shares one coordinate system and no section has an origin to disagree
from. The axes carry the same direction the corner-counted version had, so nothing mirrors or
rotates relative to before.

## Why none of the pack's art was on screen

Not a scaling problem. A **finding** problem, and it hid behind three plausible-looking
scaling problems.

The classifier matches English filenames — `wall`, `floor`, `doorway`. This pack is
Russian-authored: the forensics pass found mesh `5` used eighty times, prefabs named by
number, and the glass material called `Steklo`. So `RoleOf` returned null for essentially
every prefab. Three stragglers with an English word somewhere in their path were classified
instead, and one of them measures **36 × 57 × 36 m** — a demo assembly, not a floor module.

Everything downstream followed from that. The catalog got one wall variant and no door or
window insert at all, so nothing from the pack could be placed. And the surface density was
measured off those three objects: a wall material read from a fourteen-metre object reports
one texture tile covering nine metres, which on a six-metre wall is two thirds of a tile —
not a pattern, a smear that changes colour.

Three changes, in the order they matter:

**The pack is listed, not reasoned about.** `Audit Pack → 6. INVENTAR` prints the folders with
their prefab counts, then each piece with its size, its pivot offset from its own mesh and the
materials it carries, then every material with its tiling and base-map resolution. A kit piece
has its pivot *on* the piece; a pivot 7–29 m away is a scene export and cannot be snapped to
anything. That listing is how the real modules get named, instead of hoping their filenames
are in English.

**The pack folder is located rather than assumed.** The default was a hard-coded
`Assets/HQ Modular House`; the Asset Store calls this one *HQ Modular House Interior Pack*.
A folder that does not exist classifies zero prefabs and reports a perfectly calm zero, which
reads as an empty pack rather than as a wrong path.

**A measurement that cannot be right is refused.** The first run reported a wallpaper whose
pattern was 9.4 m across and 1.7 m tall — a ratio of five and a half on a square texture. No
wallpaper is shaped like that, so that number was not a surprising truth about the pack, it was
a wrong reading being believed. It is now rejected on aspect, on absolute size and on sign, and
the material is used exactly as its author tiled it. Since generated UVs are in metres, an
untouched tiling of 1.5 is one tile every 0.67 m — a believable wallpaper, and the honest
answer besides.

## The verified catalog

`Modular Interior → Katalog aus GEPRUEFTEN Pfaden schreiben` writes the catalog from paths that
were read off the inventory, not from filenames. No classification runs.

| role | asset | why |
|---|---|---|
| wall reference | `interior/moduls/walls prefabs/5.prefab` | 3.96 × 4.02, plain, `wallpaper3` + `white` |
| door | `1.prefab` (fallback `11.prefab`) | leaf on `blue` + `door detail` |
| window | `7.prefab` | frame on `1`, glass on `Steklo` |
| wall surface | `wallpaper3`, tiling 1.5, `3-diffuse3` 1024² | |
| floor surface | `planks 1`, 1024² | |
| ceiling surface | `white` as worn by prefab 5 | |
| not used yet | `2 `, `3 ` (arches, 3.05–3.25 m tall), `9 ` (sill 2.00 + 1.25 = 3.25 m) | taller than the room |
| not used yet | `22`, `23` (columns), `room1/2/3`, `moduls`, `furniture` | later, or demo assemblies |

**Materials are resolved on the piece that wears them.** The pack holds three materials called
`white`, three called `blue` and nineteen called `1`. A project-wide search by name is a coin
toss; asking prefab 5 which `white` it uses cannot pick the wrong one. Where a material appears
on no wall prefab — `planks 1` lives only in the demo rooms — the search is scoped to the pack
and an ambiguous name is refused rather than guessed at.

**The inserts are extracted, not instantiated whole.** The pack ships no door leaf and no window
as objects of their own: each is a child of a complete 4 m wall prefab that carries its own
wallpaper. Instantiating one of those into a 3 m room puts a second wall through the ceiling, so
the prefab is reduced to the parts named by their materials — the child objects are numbered and
the materials are not. If nothing matches, the insert is switched off and the miss is reported:
inserting the whole vendor wall would be far worse than inserting nothing.

**Which way is up is measured.** The pack's wall meshes are about 4 × 4 × 0.1 with the height on
Z, the exporter's convention rather than Unity's. Whether a given prefab already corrects that is
not something a document can answer, so it is measured on the instantiated object and the
decision is logged.

**One measured density, the rest derived.** Prefab 5 is the anchor: its two largest extents
divided by `wallpaper3`'s tiling of 1.5 give a pattern of about 2.6 m, which at 1024² is roughly
390 px/m. Floor and ceiling get the same texel density, because the pack has no floor or ceiling
part to measure against and claiming a measurement there would be invention. The report says
which number was measured and which was derived.

**The doorway is now 1.25 × 2.60**, the pack's own measured opening, so its door leaf drops in at
authored scale instead of being squeezed. Both numbers are private to Stage B, outside the
engine-free assembly and absent from the layout hash — this document already marked them
negotiable. 2.60 under a 3.00 m ceiling still leaves 0.40 m of lintel.

## Inserts are placed by their mesh, not by their pivot

This is the one number in the inventory that decides everything about placement, and it is the
same one that says the pack is not a kit:

```
1   (3.99, 1.55, 4.05)   Pivot 32.5 m
7   (3.76, 2.23, 4.12)   Pivot 31.4 m
5   (3.96, 0.10, 4.02)   Pivot 23.9 m
```

Every piece kept the origin of the one apartment scene it was exported from, so its pivot sits
13 to 40 m from its own geometry. Setting `localPosition` puts the **pivot** at the opening,
which puts the door itself tens of metres away — on screen, a door frame up near the ceiling and
a window above it.

So the wanted point is where the kept geometry's *centre* has to end up, and the transform is
offset by whatever it takes to put it there. Measured after the orientation, because turning a
piece upright moves its centre too; measured on the **visible** parts only, because most of the
prefab has just been switched off and is the wall shell the door was separated from; and taken
from eight transformed corners rather than a centre and a size, because a rotated child's
axis-aligned size is not its size in the parent's frame. The correction is logged with the
distance it had to make up.

## Building a room by hand

`HQRoomAuthoring` — *Add Component → Catch If You Can → HQ Room (hand-built)*.

Set the size, tick which walls carry a door and which carry a window, drop the
`ModularInteriorCatalog` in, then right-click the component → **Raum bauen**.

It is not a second implementation. It calls the same `ModularRoomBuilder` the house generator
calls, so what appears is the production path: the same meshes, the same UVs in metres, the same
HQ materials, the same colliders, the same inserts. What it changes is who decides — a person
writing a room description instead of the layout deriving one.

The window choice is the one place the two paths differ, and deliberately: the generator derives
it from the room's identity (never from a `CiycRandom` stream, which would advance generation),
while a person names the walls outright. The derivation lives in one place and calls the explicit
overload, so a hand-built room cannot drift from what a mission builds.

What comes out is ordinary GameObjects. Move a wall, delete one, nudge the door, add a light —
it is a normal hierarchy, not a live preview that overwrites edits. Rebuilding replaces it, so
build first and edit after.

Leaving the catalog empty is useful too: neutral grey, no inserts, just the shape.

## Building by hand out of the purchased pieces

`Modular Interior → Bauteile pruefen und setzen` opens a browser over one folder of the pack. It
measures each piece and badges it — BEREIT, MODUL x2, PIVOT 32m, MATERIAL FEHLT, KEIN URP,
SZENEN-EXPORT — and places it, either plain or inside a CIYC wrapper whose origin sits at the
piece's bottom centre. It writes nothing: no reimport, no material edit, no scale change, and the
audit runs on a button rather than per repaint.

### The sizes are a ladder, not a scale error

Measured from the inventory, against prefab 5's 3.97 m as the module:

| piece | width | multiple |
|---|---:|---|
| 16 | 11.90 m | **exactly 3×** |
| 15 | 7.94 m | **exactly 2×** |
| 1, 2, 5, 10, 11, 12, 14, 24 | 3.96–4.01 m | 1× |
| 3 | 3.84 m | 1× |
| 8 (wide window) | 6.73 m | special |
| 4, 6, 7, 9 | 3.36–3.78 m | special |
| 13, 22, 23 | 0.73–1.88 m | special |

Every one of them is 4.02–4.13 m tall — a spread of eleven centimetres across nineteen pieces.
There is no unit mismatch and no per-FBX scale difference among the wall prefabs: 15 and 16 are
double- and triple-width walls, to the centimetre. Scaling them down to match the others would
break a wall that was already right, which is why the browser badges them `MODUL x2` rather than
`OVERSIZED`, and why nothing here rescales anything.

The genuinely huge objects — `room1` 48×24×51, `room2`, `room3`, `moduls` 102×18×69, `furniture`
80×24×33 — are scene exports. They badge as `SZENEN-EXPORT` and are not building material.

### White has three causes, and the browser says which

1. **The material is meant to be white.** `arch big white` carries a real 1024² base map
   (`1_arch big_AlbedoTransparency`) on `Universal Render Pipeline/Lit`. Painted trim beside
   wallpaper is what an old apartment looks like; nothing is wrong with it.
2. **The material has no base map at all.** The pack ships textureless duplicates beside its
   textured materials — `door base`, `door detail`, `mirror`, `beth`, `SOAP`, `1` and others
   appear twice, once with a texture and once without. A prefab pointing at the untextured one
   draws flat. That is the FBX-embedded material rather than the one the pack authored.
3. **A null slot, or a shader that does not draw.**

Only the third is a fault in the strict sense; the second is a wrong reference and the first is
correct art. The browser prints the base map, its size and the material's asset path for every
slot, so which of the three applies is read rather than guessed.

**One thing to check with it:** `HQVerifiedCatalog.DoorParts` names `door base` and `door detail`,
and both exist in the pack in a textured and an untextured version. If the door leaf comes through
flat, that is the pair to look at first.

## If the pattern size still looks wrong

It is two numbers, and the loop is short. Open `ModularInteriorCatalog.asset`, change
**Authored Across Metres** on the surface in question — larger makes the pattern bigger — and
press `Modular Interior → Build ONE Test Room` again. The tool prints the achieved tiling of
every material, so the number on screen and the number in the asset can be compared directly.

A material that cannot be drawn is refused rather than assigned, and the four ways in are
reported apart because all four look identical on screen: a null shader, a shader the platform
does not support, Unity's internal error shader (which *is* the magenta), and an HDRP shader in
a URP project. Refusing gives the neutral grey stand-in — wrong, but legibly wrong.

A density of zero means *unknown* and leaves the material exactly as authored. Applying a zero
would collapse the texture to a single texel, which reads on screen as a flat colour: the very
symptom this work exists to remove.

## Two material sources, and why that is not a duplicate

`InvestigationContentCatalog.WallMaterial` and friends dress the **primitive fallback** with
the project's own Victorian room textures. `ModularInteriorCatalog.WallSurface` and friends
dress the **modular shell** with the pack's. They are not two implementations of one thing:
one is the stand-in that runs when the pack is absent, the other is the production path. Do
not merge them — a single field would mean the fallback silently becomes the production look,
and a migration that never happened would once again be indistinguishable from one that did.

## What is required of the pack, and what is not

`RequiredStructuralRoles` no longer names `Floor` or `Ceiling`. That is a measurement, not a
preference: the pack contains zero of each, so requiring them made every catalog built from it
report itself invalid forever — and a validator that cries wolf is one nobody reads. CIYC
generates both surfaces at exact size with its own UVs. What the pack is asked for is the
surface material and the pieces that genuinely fit: the door leaf and the window insert.
