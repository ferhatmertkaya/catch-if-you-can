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


## The 01_MainMenu hierarchy

`Catch If You Can → Szene → Hierarchie sortieren` sorts the open scene's ROOT objects into
`00_SYSTEMS` … `08_UI`, and creates `05_HQ_MANUAL_HOUSE` with its eight empty categories so
hand-placed pieces have somewhere to go. It shows a plan first and moves nothing until Apply.

**Why reparenting is safe here, established rather than assumed.** Unity serialises object
references by fileID, not by hierarchy path, so every reference in this scene survives a move —
and there are many: `MainMenuModeController` points at `Lobby_PlayerSpawn`, `Lobby_Exterior`,
`Lobby_Ambience`, the `Main Camera` and its `AudioListener`; `MainMenuHorrorEventDirector` points
at three red lights, `CandleLight`, `PhoneAudio` and `MainMenu_GhostFloat`. None of that is
path-based. The two things that *would* have made a move unsafe were checked: no object in this
scene calls `DontDestroyOnLoad` on itself (only a scene root survives a scene change, and
`LobbyPortal`'s mention of it is a comment about the transition overlay), and the one name-based
lookup that touches this scene — `GameObject.Find("Door_Green_Fog")` in
`MainMenuAtmosphereBuilder` — is parent-independent but finds only **active** objects, which is
why every folder the tool creates is created active.

**What does not move.** Four objects are parented *inside* prefab instances — `Spot Light` under
`CIYC_HauntedGrandfatherClock`, `CandleFX` under `CIYC_HauntedCandleHolder`, `PhoneAudio` under
`CIYC_HauntedRotaryPhone`, `Area Light` under `CIYC_MainMenu_Corridor`. Pulling one out would
change that instance's override set, so they are not offered. `MainMenu_Lobby` moves as one
subtree: splitting it into `02_FLOOR` / `03_WALLS` / … is a separate decision, not a side effect
of tidying. A prefab instance whose role cannot be read off a component is offered **unticked**.

**Two findings the audit turned up that are not hierarchy problems:**

- `MainMenu_Lobby` is saved with `m_IsActive: 0` — the whole walkable lobby is switched off, and
  nothing in the project references it by name to switch it back on. The tool moves it and leaves
  it off; enabling it is a behaviour change and belongs to whoever turned it off.
- `Lobby_Portal`'s `surface` field is `None` in the saved scene.


## Why the lobby was only visible in Play — and why it stays that way in the file

Nothing builds it at runtime. Every one of its thirty-odd objects is authored in
`01_MainMenu.unity`: floor, four walls, ceiling, the whole window assembly, the safety floor,
three lights, the moon shaft, the exterior with its ridge, trees and ruins, the ambience, the
mirror corner, armchair, table, investigation board, player spawn and `Lobby_Portal`. The scene
also carries a `MeshFilter` and `MeshRenderer` on each — there is no runtime factory for any of
it.

It is invisible in Edit Mode for one reason: `MainMenu_Lobby` is saved with `m_IsActive: 0`, and
`MainMenuModeController.interactiveRoomRoots` holds exactly that one object.
`SetRoomActive(true)` runs when the player leaves the cinematic menu. Stop, and the scene reloads
from disk, dormant again.

**That dormancy in the file is load-bearing.** `MainMenuModeController.Awake` says so in its own
words: Unity does not define whether that `Awake` runs before or after the `Awake` and `OnEnable`
of everything under the room, so a room that is already active when the scene loads has had its
moon light claim the scene's sun and its emitters start — over the top of the menu. Its
`SetRoomActive(false)` is the belt to that braces, and it cannot undo what already ran.

So there is nothing to bake, and simply leaving the switch on would reintroduce the bug the
design exists to prevent.

`Catch If You Can → Main Menu → Lobby bearbeiten` toggles the room visible for editing and
puts it back where it matters: it is switched off in `sceneSaving` and restored in `sceneSaved`,
so the file always has it dormant; and switched off on `ExitingEditMode`, because entering Play
serialises what the editor is holding rather than what the file says. Only the active flag is
touched — nothing is added, moved, deleted or repainted.

`Authored Lobby pruefen` reports the object count, whether a second `MainMenu_Lobby` has
appeared, whether floor, ceiling, walls, spawn, portal and lighting are present, and whether
`05_HQ_MANUAL_HOUSE` has ended up *under* the lobby — where it would inherit the dormancy and
vanish from Edit Mode along with it. Keeping it as its own scene root is what makes hand-placed
pieces visible at all times and survive Play/Stop untouched.

**Runtime-created, and correctly so:** the Player (`PlayerSpawner.Spawn`), the persistent
services (`CiycServices`), the runtime UI canvas, the portal's RenderTexture and camera, and the
horror events' transient state. None of those belong in a scene file.


## Why "Lobby bearbeiten" showed only the shell

Two different causes, and only one of them is about visibility.

**The props were never hidden.** All ten prefab instances — grandfather clock, table, rotary
phone, candle holder, four Victorian doors, the ghost, the corridor — are scene ROOTS with no
`m_IsActive` override, so they use their prefab's default and are active. They are visible in
Edit Mode before any switch is touched, and toggling `MainMenu_Lobby` never affected them either
way.

**Four lobby children have no geometry at all.** `Lobby_MirrorCorner`, `Lobby_Armchair`,
`Lobby_AntiqueTable` and `Lobby_InvestigationBoard` carry a script and no MeshRenderer:
`MirrorCorner.Start()` builds the frame, glass, lamp, bulb and fill light from primitives *and a
reflection camera*; `RoomProp.Start()` instantiates its prefab; `LobbyInvestigationBoard.Start()`
builds frame, surface and rails. None has `ExecuteAlways`.

So in Edit Mode those four are empty transforms. No switch can reveal geometry that does not
exist yet, and building it here would mean running gameplay `Start()` code in the editor —
including allocating the mirror's camera and RenderTexture, which this task explicitly rules out.

`Authored Lobby pruefen` therefore **names** them, with their world positions, so they can be
built around. Turning them into authored scene objects is a real architecture change with a
different owner, and is not something a visibility switch should do quietly.

## May MainMenu_Lobby leave the scene root?

Checked rather than assumed:

- `MainMenuLobbyAuthoring.FindLobby` walks roots **and recurses** — it does not require root
  placement. The `sceneSaving`, `sceneSaved` and `ExitingEditMode` paths all go through it.
- `MainMenuModeController.interactiveRoomRoots` is a fileID reference and survives a reparent.
- `SetRoomActive` calls `SetActive` on the GameObject, which a parent does not change.

So it *works*. What changes is that the room's visibility would then also depend on `03_LOBBY`
staying active — a new condition where there is currently none, bought for tidiness alone. The
plan therefore offers the move as **CONDITIONAL and unticked**, with the recommendation to leave
it at the root.

## The four ambiguous roots

| object | components | children | referenced by | verdict |
|---|---|---|---|---|
| `MAIN_MENU_ROOT` | Transform only | 0 | **nothing** | UNCLEAR — a leftover; delete rather than file |
| `HallwayPlaceholders` | Transform only | 0 | **nothing** | UNCLEAR — same |
| `MainMenu_Atmosphere` | `MainMenuAtmosphereController` | 0 | two horror-event scripts, **by component** | PROVEN — a system; `GameObject.Find` by name also survives, and folders are created active |
| `CIYC_MainMenu_Corridor` | prefab instance (+ an added `Area Light`) | — | **nothing** | UNCLEAR — the cinematic set, not the walkable lobby, and no group is clearly right |

`EventSystem` is a third empty leftover: a Transform and no EventSystem component.


## Authoring previews for the runtime-built props

`Lobby bearbeiten` now also builds the four lobby objects that have no geometry until the game
runs, so the complete room can be decorated in Edit Mode.

It asks the **same builders**. `MirrorCorner`, `RoomProp` and `LobbyInvestigationBoard` each
expose one editor-only entry point through `IEditorPreviewBuildable`; there is no second,
editor-side reconstruction of the mirror or the board, because that would be a second
implementation of the same room and would drift the first time a measurement changed in one of
them. This project has made that mistake with two flashlights and two inventories already.

| object | builder | what the preview reuses | difference from runtime |
|---|---|---|---|
| `Lobby_AntiqueTable` | `RoomProp` | the same `Resources` prefab and fit | none |
| `Lobby_Armchair` | `RoomProp` | the same `Resources` prefab and fit | none |
| `Lobby_InvestigationBoard` | `LobbyInvestigationBoard` | the same frame, surface and rail build | none |
| `Lobby_MirrorCorner` | `MirrorCorner` | the same frame, glass, lamp and fill | **no reflection camera, no RenderTexture** |

The mirror is the only one that differs, and only there: `Build` took a `withReflection` flag,
so the order and the measurements stay in one method and the preview simply does not call
`BuildCamera`. Nothing renders per repaint and no RenderTexture is allocated.

**Previews can never become content.** Every object a preview creates is renamed with the prefix
`__EDITOR_PREVIEW_` and flagged `HideFlags.DontSave`, which keeps it out of the scene file no
matter how the scene is saved. They are also removed explicitly in `sceneSaving` — two locks, not
one — and rebuilt in `sceneSaved`.

**And they are gone before Play.** `DontSave` keeps an object out of the *file* but not out of
Play mode, and a preview that survived would stand beside the one the runtime builds: two
armchairs, two mirrors, the second built by an editor. `ExitingEditMode` removes them first, then
switches the room off; `EnteredEditMode` puts both back. Which objects are new is decided by
snapshotting the holder's children before and after, not by trusting the builders to follow a
naming convention they know nothing about.

Removal also calls `ForgetEditorPreview`, so the `_built` guard is cleared and switching the view
back on rebuilds rather than showing an empty holder again.


## Why a purchased piece draws white

Three causes, identical on screen, and only one of them is a fault.

**1. The material is meant to be white.** `arch big white` carries a real 1024² base map
(`1_arch big_AlbedoTransparency`) on `Universal Render Pipeline/Lit`. Painted trim beside
wallpaper is what an old apartment looks like. Replacing it would be breaking correct art.

**2. An empty slot, or a shader that does not draw.** Rare here, and the loudest kind: an empty
slot takes Unity's built-in default, which is magenta under URP.

**3. A lost material remapping** — the common one, and provable rather than guessable.

The pack names its textures `<fbx>_<slot>_AlbedoTransparency`, where `<slot>` is the FBX's own
material slot — which is also the name Unity gives the material it generates from that slot. So
the pack contains two materials for the same surface: the generated one, named after the slot and
carrying **no texture**, and the authored one, carrying the baked texture.

Measured against the pack inventory, **20 of the 30 textureless material names have a textured
counterpart named after them**:

| textureless | authored twin | the texture that proves it |
|---|---|---|
| `SHKAF3` | `commode1` | `4_SHKAF3_AlbedoTransparency` |
| `SHKAF 5` | `commode2` | `4_SHKAF 5_AlbedoTransparency` |
| `bachek` | `part1` | `5сб_bachek_AlbedoTransparency` |
| `rakovina` | `part2` | `5сб_rakovina_AlbedoTransparency` |
| `trubi` | `part3` | `5сб_trubi_AlbedoTransparency` |
| `unitaz` | `part5` | `5сб_unitaz_AlbedoTransparency` |
| `tumba 1` | `nighstand green` | `Tumba 1_tumba 1_AlbedoTransparency` |
| `Tumba 2` | `nighstand` | `Tumba 2_Tumba 2_AlbedoTransparency` |
| `BRA` | `sconce` | `BRA_BRA_AlbedoTransparency1` |
| `door base` | `blue` / `brown` | `5_door base_AlbedoTransparency` |
| `door detail` | `whote door detail` | `5_door detail_AlbedoTransparency` |
| `mirror`, `mirror base`, `SOAP`, `belie`, `detail`, `1`–`4` | … | same shape |

Ten have no twin — `5`, `6`, `WALL`, `DAMAGED FACE/BACK`, `wood pannel1/5`, `Zerkalo`, `No Name`,
and `beth`, whose texture is spelled `bath`. So the correspondence is a naming convention, not a
law, which is exactly why the tool proposes and never applies.

`Modular Interior → Material-Doktor (Auswahl pruefen)` runs on the **current selection** only. Per
renderer it reports submesh count against slot count, and per slot the material's asset path, its
shader and whether it is supported, its base map with resolution and texture path — then one of
three verdicts: **OK**, **VERLORENES MAPPING** with the proposed twin and the texture name that
proves it, or *no twin found, probably plain, do not replace*.

It edits no material, reassigns no renderer, writes no asset and triggers no reimport. The pack
index is built once per run from a single index query.

**The smallest safe correction, when a lost mapping is proven:** set the proposed material on the
**instance's renderer** — an override inside the CIYC wrapper, never on the purchased prefab and
never on the purchased material. That stays reversible and leaves the pack untouched.


## The first question is which source the object came from

A run of the doctor on a wall in the scene reported this, for every one of ten slots:

```
Material : Assets/HQ Modular House/interior/moduls/1.FBX
BaseMap  : KEINE - zeichnet einfarbig
```

Every material was **inside the FBX**. That is a model instance: the object was dragged from
`interior/moduls/1.FBX`, so its renderers use the materials embedded in the model — and this
pack's embedded materials carry no textures at all. They are Unity's, generated from the FBX slot
names.

The pack's finished wall parts sit **beside** that FBX in `interior/moduls/walls prefabs/`, and
they carry the authored materials: the inventory shows `1.prefab` using `blue`, `door detail`,
`wallpaper3` and `white` — none of which the FBX knows about.

So a grey wall is usually not a broken material assignment. It is the wrong source, and no
material swap fixes it: **drag the prefab from `walls prefabs/`, not the FBX from `moduls/`.**
The doctor now says so first, before it discusses any individual slot.

### A bare number proves nothing

That same run proposed the window materials `1`, `2`, `3`, `4` for the wall's slots of the same
names. Those materials are named after the *window* FBX (`window LP 1-2_1_AlbedoTransparency`),
and the wall's slots just happen to be numbered too. That is a name collision, not a
correspondence — the tool was taking its own naming convention for a law, on exactly the evidence
that is weakest.

A slot name now has to carry at least three letters before it can prove a match. `SHKAF3`,
`bachek` and `door base` still resolve; `1` through `6` no longer propose anything, and the
report says why.

`door base` and `door detail` remain correct matches — their textures are literally
`5_door base_AlbedoTransparency` and `5_door detail_AlbedoTransparency`, and the materials live
in `interior/customization/door materials/`. `door base` has four candidates (`blue`, `brown`,
`whire door base`, `whire v2 door base 1`), which is a colour choice rather than an ambiguity, and
the report lists them all.

## The pack is a build dependency, and the repository now says so

`01_MainMenu.unity` names 26 prefabs that live inside `Assets/HQ Modular House/`. That folder is
gitignored on purpose — the pack is licensed per seat, and the repository's git-lfs payload is
already 1.03 GiB against GitHub's free 1 GiB allowance. So on any machine without the pack those
26 guids resolve to nothing, and Unity draws the scene with 26 missing prefabs.

From inside the repository that is **indistinguishable** from CLAUDE.md mistake 14 — content
deleted without deleting what pointed at it. Both are "a guid nothing declares". The difference
is knowable only on a machine that has the pack installed, so that machine writes it down:

```
Scripts/write_vendor_manifest.sh
```

It walks the vendor roots — read from `.gitignore`, not repeated in the script, so a pack added
to the ignore file cannot quietly stop being covered — records `guid  path` for every asset whose
`.meta` declares one, and writes `Docs/VENDOR_ASSET_MANIFEST.txt`. It reads `.meta` files and
writes one text file: it imports nothing, changes no import setting, and touches nothing inside a
pack. A `.meta` whose asset is gone is skipped, because recording it would make the reference
guard report "the pack changed under us" on the very machine that has the pack.

Commit the result. `check_asset_references.sh` then reaches a three-way verdict instead of a
two-way one:

| the guid is… | and the pack is… | verdict |
|---|---|---|
| named by the manifest | not in this working copy | expected. A note, not a failure. |
| named by the manifest | installed here | **fail** — the pack moved under us |
| named by nobody | either | **fail** — mistake 14 |

The teeth are the second and third rows. A manifest that could excuse any missing asset would be
worse than no manifest at all; this one can only excuse an absence it can name, on a machine that
demonstrably does not have the folder.

What CI can no longer prove is anything about the contents of those 26 prefabs. That is the
honest price: **installing the pack is a prerequisite for opening the main menu scene and for
building the game.** It is not something CI can supply, and pretending otherwise by removing the
check would only move the discovery to a worse moment.

## The lobby shell was replaced by hand, and the portal lost its wall

The procedural lobby shell — `Lobby_Floor`, `Lobby_Ceiling`, seven wall segments and seven window
parts — was deleted from `01_MainMenu.unity` and replaced with hand-placed pieces from the pack.
Three consequences follow, and only the first is obvious.

**The portal has nothing to cut.** `Lobby_Wall_North` was the single solid box the tear was made
in. `LobbyPortal.wallCollider` is `{fileID: 0}`, so `ResolveWall` falls back to finding the wall
by SHAPE: one collider, at most `maxWallThickness` (1.00 m) across the opening's normal, and at
least as wide and as tall as the opening (4.70 × 2.40 m). The pack's wall module is 3.97 m wide.
Each module is its own collider, so two side by side do not add up to one wide enough — the
support function is evaluated per collider, not per wall run. Whether anything at the opening
passes depends on facts only the installed pack can answer, which is why
`Catch If You Can/Lobby/Portalwand messen` measures rather than argues.

**The guard now checks the invariant instead of the name.** "There is an object called
`Lobby_Wall_North`" was too narrow — a wall built by hand out of purchased pieces satisfies the
real requirement just as well — and at the same time too wide, because a renamed object would
have passed while the portal found nothing. What is checked is: either the authored north wall is
in the scene, or `wallCollider` is explicitly assigned.

**The window went with it.** `Lobby_Wall_East_North` and `Lobby_Window_Glass` are gone, while
`Lobby_Window_Blocker`, `Lobby_WindowMoonlight` and the whole `Lobby_Exterior` assembly remain.
The moonlight and the silhouettes now shine through an opening that has no frame and no glass.

## Measuring the portal wall

`LobbyPortalWallProbe` (menu: `Catch If You Can/Lobby/Portalwand messen`) reports and changes
nothing. It runs two passes because they answer different questions and disagree for a good
reason.

The **geometric** pass walks every collider in the open scenes, *inactive ones included*, and
applies exactly the three tests `ResolveWall` applies. Inactive included is the whole point:
`MainMenu_Lobby` is saved switched off, so in the editor its colliders are not in the physics
scene at all — while at runtime the room is switched on before the portal ever opens. A physics
query alone would report the room as empty and be wrong about the only moment that matters.

The **physics** pass runs the same `Physics.OverlapBox` the runtime runs, reported as what the
editor's physics scene can see right now, not as a verdict. Where the two disagree, the
difference is the finding.

It also lists renderers that overlap the opening and carry no collider, because "the wall is not
solid" and "the wall is too narrow" look identical from inside the game and need different
repairs. And it asks the portal for its opening, its thickness limit and its assigned wall
through public read-only properties rather than reflecting into private fields — CLAUDE.md
mistake 4, which compiles, reviews clean, and fails silently on the next rename.

One detail is load-bearing: on an inactive GameObject `Collider.bounds` can come back as an empty
box, and an empty box intersects nothing. Read without checking, that says "there is nothing
here" — the same output as a genuinely missing wall, from a completely different cause. The probe
falls back to the renderer's bounds on the same object, which is the same geometry and is what
the eye sees anyway.

## The room is too big for the player, and the reference is in code

The hand-built lobby reads as oversized. The reference it has to match is **not** a measuring
cube placed in the scene — it is `PlayerFactory.CapsuleHeight` = 1.86 m and
`PlayerFactory.EyeHeight` = 1.68 m, the numbers the game actually builds the player from. A cube
is a second source for a number that already has one, and the moment the two disagree the room
gets scaled to the cube while the player keeps the constant. So `HQRoomScaleAudit` reads the
constants; a cube, if there is one, is only reported.

What can be read from the scene file alone, without the pack:

| | |
|---|---|
| wall module spacing, north run | **3.3001 m**, six pieces, five identical gaps |
| wall module spacing, west run | 3.2972 m |
| room footprint from the wall runs | ≈ 16.8 × 13.6 m |
| `FLOOR_Lobby_01` (a scaled Unity cube) | 25.80 × 41.90 m, 0.02 m thick |

What cannot: the clear height, the door height and the window sill, because those live in meshes
inside the gitignored pack. Menu `Catch If You Can/Lobby/Raumgroesse messen` measures them in
Unity and proposes a factor. It changes nothing.

It derives the factor from the **clear height** alone — finished floor top to ceiling underside —
because that is the only room dimension measurable without guessing which piece is a door. A
floor piece is wide, flat and low; a ceiling piece is wide, flat and high; neither is decided by
name, since this pack numbers its prefabs. Every other source is listed with its placed size so
the door and window heights can be *read off* rather than inferred. If either surface is missing
the tool refuses to produce a number: a guessed clear height puts the whole room out by a
constant, which is the hardest kind of wrong to see.

World bounds are the right measurement here and are **not** CLAUDE.md mistake 12. That mistake
was dividing a wanted size by a world AABB to get a *local* scale, which double-applies every
ancestor's scale. What is wanted here is a world height in metres, and the factor is a *ratio* of
two world heights — the ancestor chain cancels out of a ratio.

### Scaling the room makes the portal wall worse, not better

The portal's opening is fixed at 4.70 × 2.40 m and is a child of `MainMenu_Lobby`, not of the
pack pieces, so it does **not** shrink with the room. The wall it has to be cut into does.
`ResolveWall` needs one collider at least as wide as the opening; the modules are 3.30 m and
already too narrow, and any factor below 1 makes them narrower still. At a factor of 0.75 they
are 2.48 m against a 4.70 m opening.

So the two problems have to be settled in the right order: measure the wall
(`Catch If You Can/Lobby/Portalwand messen`), decide how the portal gets a wall it can cut, and
only then scale — or accept that the portal wall becomes a dedicated object sized in metres,
independent of the module grid, which is what `wallCollider` is for.

## Applying the factor

Measured: clear height **3.92 m**. Wanted: **2.95 m**. Factor **2.95 / 3.92 = 0.752551**, which is
the 0.7526 the measurement produced — the quotient is written rather than the rounded result, so
the two numbers cannot drift apart later. Over the room's full height the difference is 0.2 mm.

`Catch If You Can/Lobby/Raum skalieren` creates `HQ_ROOM_SCALE_ROOT` at the origin, unrotated and
unscaled, parents every `HQ_*` root and `FLOOR_Lobby_01` under it, sets one uniform scale, and
then moves the root vertically so the finished floor top lands at world Y = 0. It refuses in two
cases and reports rather than claims in a third:

- **The room is no longer the room that was measured.** A factor is only valid for the
  measurement it came from. Applied to a room somebody has since rebuilt it is simply a wrong
  number, and a uniformly wrong room is the hardest kind of wrong to see. So it re-measures
  first and stops if the clear height has moved more than 5 cm from 3.92.
- **`HQ_ROOM_SCALE_ROOT` already exists.** A second run squares the factor: 0.7526 becomes
  0.5664, and the room ends up half its original height instead of three quarters.
- **The result is measured, not computed.** After scaling it measures the clear height and the
  floor top again and prints "erreicht" or "NICHT ERREICHT" against the target. Door and window
  heights are read off the per-source listing, not multiplied out of the factor.

Reparenting goes through `Undo.SetTransformParent`, which preserves the world transform, and
every root's world position, rotation and lossy scale is re-measured afterwards. A drift is
reported and never silently corrected — a manual correction hides the bad reparent that caused
it. Only the root is given a scale; scaling individual pieces is the distortion that was
explicitly ruled out. The scene is left dirty rather than saved.

### What the room shrinking leaves behind

The room gets smaller. The things standing *in* it do not, because they are excluded on purpose:
`Lobby_PlayerSpawn`, the three lights, `Lobby_MirrorCorner`, `Lobby_Armchair`,
`Lobby_AntiqueTable`, `Lobby_InvestigationBoard`, `Lobby_MoonShaft`, `Lobby_Window_Blocker` and
`Lobby_Portal` all sit under `MainMenu_Lobby`.

**No choice of pivot fixes this.** They are spread across the room, and one uniform factor can
hold exactly one point still. Keeping the spawn in place puts the portal in the wrong wall;
keeping the portal in place moves the spawn. The tool therefore prints, for every direct child of
`MainMenu_Lobby`, the position the same map would give it — and applies none of them. Moving the
spawn is a decision about where the player stands; moving the portal is a decision about a
doorway that has no wall to cut yet anyway.

## The branding canvas is one thing, not three

Rebuilding the lobby by hand deleted `MainMenuBrandingCanvas`, and with it `GameLogo_Baked` and
`TapToStartText`. The visible symptom is two missing things; the invisible one is a third.

`MainMenuTapToStart` reads the tap straight from `Input`, deliberately — a full-screen invisible
button would sit over the menu and swallow anything else the canvas might want later. So the
label was never wired to anything: tapping still works, there is simply nothing on screen saying
so. That is why nobody noticed until the menu was looked at.

The logo has a runtime fallback. `RuntimeUIFactory.WireMainMenu` calls
`GameObject.Find("GameLogo_Baked")` and, finding nothing, builds a `GameLogo` and loads the
branding sprite — disabling the Image if the sprite is null, because an Image with no sprite
draws a solid white quad. So the logo comes back as soon as the main-menu UI is built. The
*baked* one, which exists to avoid that work, does not.

The third thing is the one that would have been rebuilt wrong. The canvas is referenced from
`MainMenuModeController.cinematicUiRoots`, which is how the handover hides it. Deleting the
canvas left a `{fileID: 0}` in that array. **That null does not throw** — both loops in the
controller skip nulls — it just means nothing is hidden when the player enters the lobby. Restore
the canvas without restoring the reference and the logo and the label stay on screen, over the
room. Restoring three quarters of a thing is how the next bug gets built.

`MainMenuLogoBaker` therefore owns all four: canvas, logo, label, and the wiring. Three details
in it are not incidental:

- **The label comes from `RuntimeUIFactory.CreateText`**, not from hand-built TMP calls. The
  project guards TextMeshPro behind `#if TMP_PRESENT || UNITY_TEXTMESHPRO` because it is
  optional, and `CreateText` already makes the TMP-or-legacy decision and applies the branded
  face by `FontRole`. Writing that branch a second time is the two-flashlights mistake. One
  deliberate difference from the deleted original, stated rather than slipped in: the old label
  was set in TMP's default sans, this one in the project's Header face.
- **The wiring goes through a public method on the controller**, `EditorSetCinematicUiRoots`,
  guarded by `#if UNITY_EDITOR`. Reflection and a `SerializedProperty` looked up by string both
  keep compiling after the field is renamed and quietly stop doing anything — CLAUDE.md
  mistake 4. The result is read back afterwards, because "I set it" and "it is set" are
  different claims.
- **Objects are found by walking the scene, not with `GameObject.Find`**, which skips inactive
  ones. A canvas switched off at a handover and then saved is exactly that, and it would be
  rebuilt beside the one already there.
