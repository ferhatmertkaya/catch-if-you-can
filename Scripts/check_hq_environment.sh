#!/usr/bin/env bash
#
# The house interior comes from a modular catalog, and nothing can quietly put the old one
# back.
#
# The Kenney house interior was the production environment for the life of the project. It is
# gone, and the risk now is not that it is missing - it is that something REGENERATES it. The
# old integration tool built 130 assets from a hard-coded folder on one menu click; a single
# surviving call, or one RoomDefinition still naming a /Kenney/ prefab, and a build ships the
# old art while everyone believes the migration succeeded. That failure is invisible from the
# inside: the house builds, the rooms have walls, and the walls are the wrong ones.
#
# So the rule is not "Kenney is deleted" - it is "no production path can reach it, and no tool
# can write it back". Attribution comments may say the word; a call site may not.
#
# The second half guards the replacement: the deterministic layer must stay untouched by a
# change that is only about art. The builder reads the layout and never writes to it, derives
# its variants instead of drawing them, and lives outside the engine-free assembly.
#
# Needs a shell and python3.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "== HQ modular environment guard =="

echo

python3 - "$ROOT" <<'PY'
import io, os, re, sys

root = sys.argv[1]

passed = failed = 0
def ok(msg):
    global passed; passed += 1; print("  ok    " + msg)
def bad(msg, detail=""):
    global failed; failed += 1
    print("  FAIL  " + msg)
    for line in ([detail] if isinstance(detail, str) else detail):
        if line:
            print("        " + line)

def read(rel):
    try:
        return io.open(os.path.join(root, rel), encoding="utf-8", errors="replace").read()
    except OSError:
        return None

def code(rel):
    """The file with comment lines stripped. A guard that greps for a forbidden call will
    otherwise match the comment warning against it - that has bitten this project twice."""
    body = read(rel)
    if body is None:
        return None
    out = []
    for line in body.split("\n"):
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("///") or stripped.startswith("*"):
            continue
        out.append(re.sub(r"//.*$", "", line))
    return "\n".join(out)

GEN = "Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs"
BUILDER = "Assets/CatchIfYouCan/Scripts/Procedural/ModularRoomBuilder.cs"
CATALOG = "Assets/CatchIfYouCan/Scripts/Content/ModularInteriorCatalog.cs"
CONTENT = "Assets/CatchIfYouCan/Scripts/Content/InvestigationContentCatalog.cs"
PATHS = "Assets/CatchIfYouCan/Scripts/Content/ExternalAssetPaths.cs"

# ----------------------------------------------------- 1. nothing reaches the old art

# Every .cs, .asset and .prefab under Assets, minus comments, must not name a Kenney content
# path. The two humanoid meshes the ghosts use are the one allowed exception, and they are
# allowed by their FULL path, so a folder that merely starts the same way does not slip in.
GHOST_MESHES = (
    "Assets/External/Kenney/MiniDungeon/Models/character-human.fbx",
    "Assets/External/Kenney/MiniDungeon/Models/character-orc.fbx",
    "Assets/External/Kenney/MiniDungeon/Models",
)
KENNEY_CONTENT = re.compile(r"Assets/(?:CatchIfYouCan/Prefabs/(?:Rooms|Props)|External)/Kenney[^\"'\s]*")

offenders = []
for dirpath, dirnames, filenames in os.walk(os.path.join(root, "Assets")):
    for name in filenames:
        if not name.endswith((".cs", ".asset", ".prefab", ".unity")):
            continue
        rel = os.path.relpath(os.path.join(dirpath, name), root)
        body = code(rel) if name.endswith(".cs") else read(rel)
        if body is None:
            continue
        for hit in KENNEY_CONTENT.findall(body):
            if hit in GHOST_MESHES:
                continue
            offenders.append("%s names %s" % (rel, hit))

if offenders:
    bad("no production file names a Kenney content path", offenders[:10])
else:
    ok("no production file names a Kenney content path "
       "(the two ghost meshes are named by full path and allowed)")

# The deleted folders must actually be gone, not merely unreferenced.
still_there = [d for d in ("Assets/CatchIfYouCan/Prefabs/Rooms/Kenney",
                           "Assets/CatchIfYouCan/Prefabs/Props/Kenney",
                           "Assets/External/Kenney/FurnitureKit")
               if os.path.isdir(os.path.join(root, d))]
if still_there:
    bad("the Kenney house interior folders are gone", still_there)
else:
    ok("the Kenney house interior folders are gone")

# And no tool can write them back. The integrator built prop prefabs, prop definitions, room
# prefabs and a door; if any of those methods returns, one click recreates the pipeline.
integrator = code("Assets/CatchIfYouCan/Editor/ExternalAssetIntegrator.cs")
if integrator is None:
    ok("no integration tool can rebuild the Kenney house (the tool is gone)")
else:
    revived = [m for m in ("BuildAllPropPrefabs", "BuildPropDefinitions", "BuildDoorPrefab",
                           "KenneyRoomPrefabBuilder", "BuildStaticPropPrefab")
               if m + "(" in integrator]
    if revived:
        bad("no integration tool can rebuild the Kenney house",
            ["ExternalAssetIntegrator declares or calls " + ", ".join(revived)])
    else:
        ok("no integration tool can rebuild the Kenney house")

# ------------------------------------------------ 2. the production path is the modular one

gen = code(GEN)
if gen is None:
    bad("the generator is present")
else:
    if "ModularRoomBuilder.Build(" in gen:
        ok("the generator builds rooms from modular pieces")
    else:
        bad("the generator builds rooms from modular pieces",
            "nothing calls ModularRoomBuilder.Build")

    # The modular branch must come FIRST. Whichever branch runs first is the production path;
    # a whole-room prefab checked ahead of it would silently win wherever one is still wired.
    m = gen.find("ModularRoomBuilder.Build(")
    p = gen.find("GetPrefabVariant(")
    if m >= 0 and (p < 0 or m < p):
        ok("the modular path is tried before any whole-room prefab")
    else:
        bad("the modular path is tried before any whole-room prefab",
            "a finished room prefab would take precedence over the kit")

    # No silent stand-in. A primitive box is allowed only behind an editor/development guard.
    if "PrimitiveRoomFactory.CreateRoom(" in gen:
        block = gen[gen.find("if (roomGo == null)", gen.find("GetPrefabVariant(")):]
        if "UNITY_EDITOR || DEVELOPMENT_BUILD" in gen and \
           gen.find("UNITY_EDITOR || DEVELOPMENT_BUILD") < gen.find("PrimitiveRoomFactory.CreateRoom("):
            ok("the primitive stand-in is fenced off from a player build")
        else:
            bad("the primitive stand-in is fenced off from a player build",
                "a shipped house of grey boxes looks exactly like one that was never migrated")
    else:
        ok("the primitive stand-in is fenced off from a player build (it is gone entirely)")

    if re.search(r"CIYCLog\.Error\(", gen):
        ok("a room that cannot be built says so loudly")
    else:
        bad("a room that cannot be built says so loudly",
            "missing structural content must not be silent")

# ------------------------------------------------- 3. the builder cannot change the layout

builder = code(BUILDER)
if builder is None:
    bad("the modular room builder is present")
else:
    # It must not draw from a generation stream. A draw advances the stream and changes every
    # later draw - the one way a visual pass can reach back into the layout.
    if "CiycRandom" in builder or "CiycStream" in builder:
        bad("the builder derives its variants instead of drawing them",
            "it touches a CiycRandom/CiycStream, which would shift the generation streams")
    else:
        ok("the builder derives its variants instead of drawing them")

    if "Fnv1a64.Create()" in builder:
        ok("the variant choice is derived from the room's identity (FNV, like the light director)")
    else:
        bad("the variant choice is derived from the room's identity",
            "without a stable derivation two players see different walls")

    # It reads the layout. It must not write one.
    writes = [w for w in ("new HouseLayout", "layout.Rooms.Add", "layout.Doors.Add",
                          "HouseLayoutBuilder") if w in builder]
    if writes:
        bad("the builder consumes the layout and never produces one", writes)
    else:
        ok("the builder consumes the layout and never produces one")

    # The wall a doorway goes in is decided by the layout, not by the art.
    if "room.HasDoor(direction)" in builder and "ModuleRole.WallWithDoorway" in builder:
        ok("a door connection places a doorway module, not a solid wall with a door in front")
    else:
        bad("a door connection places a doorway module",
            "a solid wall plus a door object leaves the opening blocked")

# The builder is engine-facing and must NOT be inside the deterministic assembly, which is
# engine-free by construction - that property is what makes it deterministic.
det_dir = os.path.join(root, "Assets/CatchIfYouCan/Scripts/Procedural/Deterministic")
if os.path.isfile(os.path.join(det_dir, "ModularRoomBuilder.cs")):
    bad("the builder stays outside the engine-free deterministic assembly")
else:
    ok("the builder stays outside the engine-free deterministic assembly")

# --------------------------------------------------------- 4. the catalog is a real contract

cat = code(CATALOG)
if cat is None:
    bad("the modular interior catalog type is present")
else:
    roles = ("Floor", "Ceiling", "WallSolid", "WallWithDoorway", "WallWithWindow", "Stairs")
    missing = [r for r in roles if ("ModuleRole." + r) not in cat and (r + " =") not in cat]
    if missing:
        bad("the catalog names every structural role", missing)
    else:
        ok("the catalog names every structural role (%s)" % ", ".join(roles))

    if "RequiredStructuralRoles" in cat and "TryValidate" in cat:
        ok("the catalog can say what it is missing before a house is attempted")
    else:
        bad("the catalog can say what it is missing before a house is attempted")

    # No vendor name may be baked into the type. The pack is data, not code.
    vendor = [v for v in ("Kenney", "Knife", "HQ Modular") if v in cat]
    if vendor:
        bad("the catalog names no vendor", vendor)
    else:
        ok("the catalog names no vendor - the pack is data, not code")

content = code(CONTENT)
if content and "ModularInteriorCatalog ModularInterior" in content:
    ok("the content catalog carries the modular interior through to the generator")
else:
    bad("the content catalog carries the modular interior through to the generator")

# --------------------------------------------------- 5. the deterministic layer is untouched

paths = code(PATHS)
if paths is None:
    bad("ExternalAssetPaths is present")
else:
    dead = [c for c in ("KenneyFurnitureModels", "KenneyDungeonModels",
                        "PropPrefabsRoot", "RoomPrefabsRoot") if c in paths]
    if dead:
        bad("ExternalAssetPaths names only folders that exist", dead)
    else:
        ok("ExternalAssetPaths names only folders that exist")

# ------------------------------- 6. the pack's URP conversion did not take our shaders

# A "Built-in to URP" conversion run over the whole project rewrites m_Shader on every
# material it can. Ours must not be among them: nine CIYC materials are driven by custom
# shaders, and a converted one silently becomes URP/Lit - the dissolve stops dissolving, the
# portal stops being a portal, and nothing errors. Each of these nine must still point at a
# .shader file under Assets/CatchIfYouCan/Shaders.
CUSTOM = {
    "Ghost_RiggedDissolve": "GhostDissolve",
    "MAT_GhostDissolve": "GhostDissolve",
    "MAT_ElectronicGlitch": "ElectronicGlitch",
    "MAT_PlanarMirror": "PlanarMirror",
    "MAT_Portal": "Portal",
    "MAT_SpectralGrid": "SpectralGrid",
    "MAT_SpectralReveal": "SpectralReveal",
    "MAT_UISlime": "UISlime",
    "MAT_UVEvidence": "UVEvidence",
}

guid_to_asset = {}
for dirpath, dirnames, filenames in os.walk(os.path.join(root, "Assets")):
    for name in filenames:
        if not name.endswith(".meta"):
            continue
        rel = os.path.relpath(os.path.join(dirpath, name), root)
        try:
            for line in io.open(os.path.join(root, rel), encoding="utf-8", errors="replace"):
                if line.startswith("guid: "):
                    guid_to_asset[line[6:].strip()] = rel[:-5]
                    break
        except OSError:
            pass

converted = []
absent = []
for material, shader in sorted(CUSTOM.items()):
    found = None
    for dirpath, dirnames, filenames in os.walk(os.path.join(root, "Assets/CatchIfYouCan")):
        if material + ".mat" in filenames:
            found = os.path.relpath(os.path.join(dirpath, material + ".mat"), root)
            break

    if found is None:
        absent.append(material)
        continue

    body = read(found) or ""
    m = re.search(r"m_Shader: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
    target = guid_to_asset.get(m.group(1)) if m else None
    if target is None or not target.endswith(shader + ".shader"):
        converted.append("%s -> %s (expected %s.shader)" % (material, target or "no project shader", shader))

if absent:
    bad("every CIYC material with a custom shader still has it", ["missing: " + ", ".join(absent)])
elif converted:
    bad("every CIYC material with a custom shader still has it",
        ["a URP conversion pass rewrote these:"] + converted)
else:
    ok("every CIYC material with a custom shader still has it (%d checked)" % len(CUSTOM))

# ------------------------------------- 7. nothing fabricates a blocker in a doorway

# The doorway was open to the eye and shut to the body. With the Kenney door prefab gone,
# CreateDoorAt fell back to building two cubes in the opening: a frame with its collider
# removed and a leaf with its collider kept. The leaf is what the player walked into, and
# both carried Unity's built-in default material, which is a Built-in-pipeline shader and
# draws magenta under URP - the magenta surface in the doorway was the frame.
#
# Two rules follow, and both are checked because both were violated at once.

gen_raw = read(GEN) or ""

if "BuildPrimitiveDoor" in gen_raw:
    bad("no primitive door is fabricated when the door prefab is missing",
        "BuildPrimitiveDoor is back; it puts a collider in every doorway in the house")
else:
    ok("no primitive door is fabricated when the door prefab is missing")

if gen and re.search(r"if \(doorPrefab == null\)", gen):
    ok("a missing door prefab leaves the opening clear and says so")
else:
    bad("a missing door prefab leaves the opening clear and says so",
        "without the early return the fallback path returns")

# Every primitive in the generator goes through the one helper that gives it a URP material.
# GameObject.CreatePrimitive on its own is the magenta.
primitive_calls = len(re.findall(r"GameObject\.CreatePrimitive\(", gen or ""))
if primitive_calls <= 1:
    ok("every generated primitive gets its material from one place (%d call site)" % primitive_calls)
else:
    bad("every generated primitive gets its material from one place",
        "%d GameObject.CreatePrimitive calls - each one without a material draws magenta"
        % primitive_calls)

gv = read("Assets/CatchIfYouCan/Scripts/Procedural/Deterministic/GenerationVersion.cs")
m = re.search(r"Current\s*=\s*(\d+)", gv or "")
if m:
    ok("GenerationVersion is unchanged at %s - the layout contract did not move" % m.group(1))
else:
    bad("GenerationVersion is readable")

# ---- a primitive with no material is MAGENTA, not neutral ------------------------------------
#
# GameObject.CreatePrimitive arrives carrying Unity's built-in default material - a
# Built-in-pipeline shader that draws solid magenta under URP. The neutral-material helper only
# ASSIGNED when it had a material and did nothing when the shader lookup failed, so a missing
# shader produced a magenta house and not one line of log. CLAUDE.md mistake 2, in the one place
# that builds every fallback room.
gen = read("Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs") or ""
m = re.search(r"private static GameObject CreateNeutralPrimitive.*?\n        \}", gen, re.S)
body = m.group(0) if m else ""
if "renderer.enabled = false" in body and "[CIYC][WorldMaterial]" in body:
    ok("a primitive with no material is hidden, not left magenta")
else:
    bad("a primitive with no material is hidden, not left magenta - skipping the "
        "assignment leaves Unity's built-in default, which is magenta under URP")

# ---- the room shell a fallback builds is TEXTURED, and its materials survive a build ---------
#
# Every room in the house was falling back to PrimitiveRoomFactory, and that factory painted flat
# colours: an untextured grey box. On screen that is indistinguishable from a migration that never
# happened, so the stand-in is now textured with the project's own room materials.
#
# Reached through the content catalog, not by path. The catalog lives under Resources, so a
# material it references is pulled into the build with it; an AssetDatabase or Resources path
# pointing into Assets/.../Materials works in the editor and finds nothing on a device - which is
# CLAUDE.md mistake 3 wearing the art department's clothes.
prf = read("Assets/CatchIfYouCan/Scripts/Procedural/PrimitiveRoomFactory.cs") or ""

if "public static void ConfigureSurfaces" in prf:
    ok("the room shell is TOLD its materials rather than looking them up")
else:
    bad("the room shell is TOLD its materials rather than looking them up",
        "a Resources path into an Assets folder resolves in the editor and nowhere else")

if not re.search(r"Resources\.Load|AssetDatabase\.", prf):
    ok("the room shell loads nothing by path")
else:
    bad("the room shell loads nothing by path",
        "content reaches this class through the catalog it is handed")

gen2 = read("Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs") or ""
if re.search(r"PrimitiveRoomFactory\.ConfigureSurfaces\(", gen2):
    ok("the generator hands the room shell its materials before building")
else:
    bad("the generator hands the room shell its materials before building",
        "without the call the fallback rooms stay flat colours")

# The catalog declared WallMaterial and FloorMaterial and NOBODY READ THEM: two fields that look
# like settings and change nothing. Reading them is the point of having them.
apply = re.search(r"public void ApplyContentCatalog.*?\n        \}", gen2, re.S)
apply = apply.group(0) if apply else ""
missing = [f for f in ("WallMaterial", "FloorMaterial", "CeilingMaterial", "TrimMaterial")
           if "catalog." + f not in apply]
if not missing:
    ok("all four room surface materials are read from the catalog")
else:
    bad("all four room surface materials are read from the catalog",
        "unread: " + ", ".join(missing) + " - a field nothing reads is a setting that lies")

# A cube's UVs run 0..1 per face whatever its size, so one shared material stretches a single
# tile across a whole wall. The authored room materials tile once per metre (MAT_Room_Wall carries
# a scale of 5.3 over the 5.3 m wall it was made for) and the generated shell has to match, or the
# wallpaper is one enormous smear that reads as "no texture" just as loudly as no texture.
if (re.search(r"private const float TilesPerMetre\s*=", prf) and
        re.search(r"\*\s*TilesPerMetre", prf) and "SetTextureScale" in prf):
    ok("generated surfaces are tiled per metre rather than left at a cube's 0..1 UVs")
else:
    bad("generated surfaces are tiled per metre rather than left at a cube's 0..1 UVs",
        "one tile stretched over a 6 m wall looks exactly like an untextured wall")

# Same lesson as the generator's own primitives: skipping the assignment does not leave a plain
# surface, it leaves Unity's built-in default, which is magenta under URP.
mm = re.search(r"private static GameObject CreatePrimitive\(PrimitiveType type.*?\n        \}", prf, re.S)
mbody = mm.group(0) if mm else ""
if "renderer.enabled = false" in mbody and "[CIYC][WorldMaterial]" in mbody:
    ok("a room primitive with no material is hidden, not left magenta")
else:
    bad("a room primitive with no material is hidden, not left magenta",
        "GameObject.CreatePrimitive arrives carrying the Built-in default shader")

# One call site, so there is exactly one place that can forget.
prf_calls = len(re.findall(r"= GameObject\.CreatePrimitive\(", prf))
if prf_calls <= 1:
    ok("every room primitive gets its material from one place (%d call site)" % prf_calls)
else:
    bad("every room primitive gets its material from one place",
        "%d GameObject.CreatePrimitive calls - each one is a chance to ship magenta" % prf_calls)

# And the catalog's references RESOLVE. A guid naming a file that is not there looks exactly like
# a working reference until the asset is opened - CLAUDE.md mistake 3, which cost this project the
# ghost prefab for its whole life.
cat_path = "Assets/CatchIfYouCan/Resources/CatchIfYouCan/InvestigationContentCatalog.asset"
cat = read(cat_path) or ""
guids = dict(re.findall(r"(WallMaterial|FloorMaterial|CeilingMaterial|TrimMaterial): \{fileID: \d+, guid: ([0-9a-f]{32})", cat))

known = {}
for dirpath, _dirs, files in os.walk(os.path.join(root, "Assets/CatchIfYouCan")):
    for f in files:
        if not f.endswith(".mat.meta"):
            continue
        meta = os.path.join(dirpath, f)
        try:
            text = io.open(meta, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        g = re.search(r"^guid: ([0-9a-f]{32})", text, re.M)
        if g and os.path.exists(meta[:-5]):
            known[g.group(1)] = meta[:-5]

unresolved = [f for f in ("WallMaterial", "FloorMaterial", "CeilingMaterial", "TrimMaterial")
              if f not in guids or guids[f] not in known]
if not unresolved:
    ok("all four room materials named by the catalog resolve to files that exist")
else:
    bad("all four room materials named by the catalog resolve to files that exist",
        "unresolved: " + ", ".join(unresolved) + " - a guid pointing nowhere reads as wired")

# ---- the pack supplies the SURFACE, not the structure -----------------------------------------
#
# Measured, not assumed (Docs/HQ_MODULAR_MIGRATION.md): the pack has ZERO floor and ceiling parts
# - its own demo builds both from a scaled Unity Plane - and its walls are not a kit, with pivots
# up to 29 m from the mesh they belong to and UVs normalised per piece. So CIYC generates the
# structure at exact size and the pack is asked for the material and the small pieces that fit.
mrb = read("Assets/CatchIfYouCan/Scripts/Procedural/ModularRoomBuilder.cs") or ""

# Every "is this call present" test below reads the file with its COMMENTS STRIPPED. The comments
# in this file name the exact things the guard forbids - an HDRP shader, DestroyImmediate - so a
# grep over the raw text is satisfied by the warning against the bug. That has bitten this
# project twice, in both directions.
mrb_code = code("Assets/CatchIfYouCan/Scripts/Procedural/ModularRoomBuilder.cs") or ""

# The builder must ask the CATALOG for its surfaces. It used to make three flat grey materials
# and ignore the catalog entirely, so the modular path produced exactly the same untextured room
# as the primitive fallback it was supposed to replace.
if re.search(r"catalog\.WallSurface", mrb_code) and re.search(r"catalog\.FloorSurface", mrb_code) \
        and re.search(r"catalog\.CeilingSurface", mrb_code):
    ok("the room shell takes wall, floor and ceiling materials from the catalog")
else:
    bad("the room shell takes wall, floor and ceiling materials from the catalog",
        "flat neutral colours make the modular path look exactly like the fallback it replaces")

# A material that cannot be drawn must be REFUSED, not assigned. Four ways in, one appearance on
# screen: null shader, unsupported shader, Unity's internal error shader, and an HDRP shader in a
# URP project. Each is checked by name so the console says which one happened.
drawable = re.search(r"private static bool IsDrawable.*?\n        \}", mrb_code, re.S)
dbody = drawable.group(0) if drawable else ""
# The STATEMENT, never the word. Each of these strings also appears in the error message that
# reports the failure, so a needle of "HDRP" alone stays green with the test deleted - the
# message keeps the word. Grep the test that decides, not the sentence that explains.
checks = {
    "null shader": "shader == null",
    "unsupported shader": "!shader.isSupported",
    "internal error shader": 'IndexOf("InternalErrorShader"',
    "HDRP shader": 'IndexOf("HDRP"',
}
absent = [name for name, needle in checks.items() if needle not in dbody]
if dbody and not absent:
    ok("an undrawable material is refused, and the four ways in are named apart")
else:
    bad("an undrawable material is refused, and the four ways in are named apart",
        ("IsDrawable is missing" if not dbody
         else "unchecked: " + ", ".join(absent) + " - all four look identical on screen"))

# The vendor material is never edited. A COPY carries the tiling, because the pack's UVs are
# normalised per piece and generated UVs are in metres - one side has to be rescaled, and it must
# not be the purchased asset.
surface = re.search(r"private static Material Surface\(ref Material slot.*?\n        \}", mrb_code, re.S)
sbody = surface.group(0) if surface else ""
if "new Material(surface.Material)" in sbody and "RebaseToMetres(slot" in sbody:
    ok("the density is applied to a copy, never to the vendor material")
else:
    bad("the density is applied to a copy, never to the vendor material",
        "rescaling the pack's own material edits somebody's purchased asset")

# EVERY map, not the colour map alone. Rescaling three properties and leaving the detail normal,
# the occlusion and the parallax where they were does not read as a wrong size - it reads as a
# warped surface, because the bumps stop sitting on the pattern they belong to.
rebase = re.search(r"private static void RebaseToMetres.*?\n        \}", mrb_code, re.S)
rbody = rebase.group(0) if rebase else ""
if rbody and "GetTexturePropertyNames()" in rbody and "GetTextureScale(names[i])" in rbody:
    ok("every texture map is rebased, not just the colour")
else:
    bad("every texture map is rebased, not just the colour",
        "a detail normal left at the authored tiling puts the bumps on a pattern that moved")

# Divided, never overwritten, so a map deliberately tiled finer than the base keeps that
# relationship instead of being flattened onto one value.
if rbody and "authored.x * divisor.x" in rbody:
    ok("maps are divided by one shared divisor, keeping their relative tiling")
else:
    bad("maps are divided by one shared divisor, keeping their relative tiling",
        "overwriting each map with one absolute tiling destroys deliberate detail scales")

# Three materials for the whole house, not three per room. The slots are static and resolved once.
if re.search(r"private static Material _wall;", mrb_code) and "if (slot != null)" in sbody:
    ok("the surface materials are shared across every room, resolved once")
else:
    bad("the surface materials are shared across every room, resolved once",
        "one material per room is forty materials and forty draw calls in a ten-room house")

# A density of zero means UNKNOWN and must leave the material as authored. Applying a zero
# collapses the texture to a single texel, which reads on screen as a flat colour - the exact
# symptom of the missing texture this whole pass is about.
if "if (!surface.HasDensity)" in sbody:
    ok("an unknown density leaves the material as authored instead of collapsing it")
else:
    bad("an unknown density leaves the material as authored instead of collapsing it",
        "dividing by zero blows the texture up to one texel across the whole wall")

# A vendor insert brings no collision and casts no shadow. Gameplay collision is the generated
# boxes' job, and a MeshCollider across vendor geometry is the expensive way to get it wrong.
# Scoped to the three methods that place an insert. The work moved out of AddInsert when the
# insert stopped being "the whole vendor prefab" and became "the parts of it that are the door",
# so a check pinned to one method name would report a broken invariant that is not broken.
insert = re.search(r"private static void AddInsert.*?\n        \}", mrb_code, re.S)
keep = re.search(r"private static int KeepOnlyInsertParts.*?\n        \}", mrb_code, re.S)
disable = re.search(r"private static void DisableColliders.*?\n        \}", mrb_code, re.S)
ibody = "".join(m.group(0) for m in (insert, keep, disable) if m)

icode = ibody
if ibody and "_insertColliders[i].enabled = false" in ibody and "ShadowCastingMode.Off" in ibody:
    ok("a vendor insert brings no collider and casts no shadow")
else:
    bad("a vendor insert brings no collider and casts no shadow",
        "vendor prefabs carry MeshColliders; gameplay collision is the generated boxes' job")

# The vendor prefab is a whole 4 m WALL with the door or window as children. Only the named
# parts come through: instantiating the shell puts a second wall through a 3 m ceiling.
if keep and "Wanted(renderer, keepMaterials)" in ibody and "renderer.enabled = false" in ibody:
    ok("only the named parts of a vendor wall are kept, not the wall itself")
else:
    bad("only the named parts of a vendor wall are kept, not the wall itself",
        "the pack ships no door or window on its own - each is a child of a 4 m wall prefab")

# Zero parts matched means the material names are wrong, and inserting the whole vendor wall is
# far worse than inserting nothing. It must say so and stand down.
if insert and "if (kept == 0)" in insert.group(0) and "go.SetActive(false)" in insert.group(0):
    ok("an insert that matched nothing is switched off and reported, not left as a whole wall")
else:
    bad("an insert that matched nothing is switched off and reported, not left as a whole wall",
        "a silent miss puts a 4 m vendor wall through the ceiling of a 3 m room")

# Which way is up is MEASURED on the instantiated object. The pack's wall meshes are about
# 4 x 4 x 0.1 with the height on Z - the exporter's convention, not Unity's - and whether the
# prefab already corrects that is not something a document can answer.
if "private static void OrientUpright" in mrb_code and "size.z > size.y * 2f" in mrb_code:
    ok("an insert's orientation is measured rather than assumed")
else:
    bad("an insert's orientation is measured rather than assumed",
        "a Z-up piece dropped in unrotated lies flat on the floor")

# Disabled rather than destroyed: Destroy is deferred and DestroyImmediate is edit-mode only, and
# choosing between them by context is how this project got an editor house and a device house
# that differed.
if icode and "DestroyImmediate" not in icode and "Object.Destroy(" not in icode:
    ok("insert colliders are switched off rather than destroyed by context")
else:
    bad("insert colliders are switched off rather than destroyed by context",
        "editor-only destruction is how the editor and the device stopped agreeing")

# A window is not a way through. One box across the span; only a doorway is cut into three.
if re.search(r"bool oneBoxAcross = !hasDoor \|\| hasWindow;", mrb_code):
    ok("a window wall keeps one collider across its span")
else:
    bad("a window wall keeps one collider across its span",
        "splitting a window wall around the opening lets the player climb through it")

# The window has to FIT. Measured window 7 is 2.05 x 0.90 on a 1.55 sill: head at 2.45 m under a
# 3.00 m ceiling. Window 9 (sill 2.00, height 1.25) reaches 3.25 m and would cut the ceiling.
m = re.search(r"WindowSill\s*=\s*([0-9.]+)f", mrb_code)
h = re.search(r"WindowHeight\s*=\s*([0-9.]+)f", mrb_code)
if m and h and float(m.group(1)) + float(h.group(1)) <= 3.0:
    ok("the window opening fits under a 3 m ceiling (head at %.2f m)"
       % (float(m.group(1)) + float(h.group(1))))
else:
    bad("the window opening fits under a 3 m ceiling",
        "sill plus height must stay under the room height or the opening cuts the ceiling")

# Floor and Ceiling must NOT be required of the pack: it has none, so requiring them made every
# catalog built from it report itself invalid forever. A validator that cries wolf is not read.
cat2 = read("Assets/CatchIfYouCan/Scripts/Content/ModularInteriorCatalog.cs") or ""
req = re.search(r"RequiredStructuralRoles\s*=\s*\{(.*?)\};", cat2, re.S)
rbody = req.group(1) if req else ""
if rbody and "ModuleRole.Floor" not in rbody and "ModuleRole.Ceiling" not in rbody:
    ok("the catalog does not demand floor and ceiling modules the pack does not have")
else:
    bad("the catalog does not demand floor and ceiling modules the pack does not have",
        "zero floor and zero ceiling parts exist in the pack; requiring them is a false failure")

# The test room builds ONE room and scans nothing. Processing the whole pack is what made the
# machine unusable, and converting the whole house before looking at one room is how a mistake
# gets made forty times.
tool = read("Assets/CatchIfYouCan/Editor/HQTestRoomTool.cs") or ""
if tool and "FindAssets" not in tool and "ImportAsset" not in tool and "Refresh()" not in tool:
    ok("the test-room tool scans, imports and refreshes nothing")
else:
    bad("the test-room tool scans, imports and refreshes nothing",
        "a pack-wide scan or reimport is the thing that made the editor unusable")

if re.search(r"new Vec3i\(6000, 3000, 6000\)", tool):
    ok("the test room is the logical 6 x 3 x 6 cell, not a size of its own")
else:
    bad("the test room is the logical 6 x 3 x 6 cell, not a size of its own",
        "a room at any other size is testing something the game will never build")

# ---- a wall's sections share ONE texture coordinate system ------------------------------------
#
# A wall with an opening is four boxes: left, right, header and sill. Every face used to start
# its UV at (0,0) and run to (span, span), so each section restarted the pattern at zero - the
# wallpaper jumped at every doorway, and the header showed a slice that lined up with nothing
# beside it. Projecting each vertex onto the face's own two axes instead means no section has an
# origin of its own and they cannot disagree.
smf = code("Assets/CatchIfYouCan/Scripts/Procedural/StructuralMeshFactory.cs") or ""

if "AddProjectedUv" in smf and "Vector3.Dot(vertex, uAxis)" in smf:
    ok("wall UVs are projected from each vertex, so sections cannot restart the pattern")
else:
    bad("wall UVs are projected from each vertex, so sections cannot restart the pattern",
        "a per-face 0..span UV restarts the wallpaper at every doorway and window")

if not re.search(r"_uv\.Add\(new Vector2\(uSpan", smf):
    ok("no face counts its UV from its own corner any more")
else:
    bad("no face counts its UV from its own corner any more",
        "the corner-counted version is what made every section start at zero")

# ---- the density is measured in WORLD metres, and no single piece decides it -------------------
#
# The pack's demo scales a Unity Plane by 1.45 to reach 14.35 m, so a mesh read without its
# transform is off by nearly half. And the same material appears at 0.55 U/m on one piece and
# 0.10 on another - a spread of five and a half - so whichever prefab was enumerated first
# decided the texture size for the whole house.
tools = code("Assets/CatchIfYouCan/Editor/ModularInteriorTools.cs") or ""

# Scoped to the method that measures. ModularInteriorTools is two thousand lines and the
# forensics pass reads lossyScale of its own, so a file-wide grep for the word stays green with
# the measurement gutted - which is exactly what it did on the first try.
collect = re.search(r"private static void CollectSurfaces.*?\n        \}", tools, re.S)
cbody = collect.group(0) if collect else ""

if cbody and "lossyScale" in cbody:
    ok("the surface density is measured in world metres, transform included")
else:
    bad("the surface density is measured in world metres, transform included",
        "mesh bounds alone ignore a scaled piece, and this pack scales its pieces")

if "private static Vector2 Median(" in tools and "Median(candidate.Sizes)" in tools:
    ok("the density is the median of every piece, not whichever came first")
else:
    bad("the density is the median of every piece, not whichever came first",
        "one absurd piece must not set the texture size for the whole house")

if "hi > lo * 2f" in tools:
    ok("an inconsistent pack is reported rather than silently averaged")
else:
    bad("an inconsistent pack is reported rather than silently averaged",
        "a number that was chosen must not be presented as a number that was found")

# ---- a measurement that cannot be right is REFUSED, not shipped -------------------------------
#
# The first run of the surface pass reported a wallpaper whose pattern was 9.4 m across and
# 1.7 m tall: a ratio of five and a half on a square texture. That is not a surprising truth
# about the pack, it is a wrong reading being believed - and on a 6 m wall it is two thirds of
# one tile, which is not a pattern, just a smear that changes colour. A measurement is not
# automatically better than no measurement.
if "private static string Implausible(" in tools:
    ok("an impossible density is refused rather than written into the catalog")
else:
    bad("an impossible density is refused rather than written into the catalog",
        "a wrong reading believed is worse than a material left as its author tiled it")

impl = re.search(r"private static string Implausible.*?\n        \}", tools, re.S)
ibody2 = impl.group(0) if impl else ""
if ibody2 and "MaxPatternAspect" in ibody2 and "MaxPatternMetres" in ibody2:
    ok("both an impossible aspect and an impossible size are caught")
else:
    bad("both an impossible aspect and an impossible size are caught",
        "a square texture cannot repeat 9 m across and 2 m up, and no pattern is wall-sized")

# The refusal leaves the material AS AUTHORED rather than falling back to grey. UVs are in
# metres, so an untouched tiling of 1.5 is one tile every 0.67 m - a believable wallpaper.
rep = re.search(r"private static SurfaceMaterial Report.*?\n        \}", tools, re.S)
rpbody = rep.group(0) if rep else ""
if rpbody and "return new SurfaceMaterial { Material = candidate.Material };" in rpbody:
    ok("a refused density keeps the material, only dropping the number")
else:
    bad("a refused density keeps the material, only dropping the number",
        "dropping the material too would put the room back to neutral grey")

# ---- the density is sampled from the PACK, not from whatever the classifier caught -----------
#
# The classifier matches English filenames and this pack numbers its prefabs, so three
# stragglers were classified - one of them 36 x 57 m, a demo assembly - and the density was
# measured off those.
cs = re.search(r"private static string ChooseSurfaces.*?\n        \}", tools, re.S)
csbody = cs.group(0) if cs else ""
if csbody and 'FindAssets("t:Prefab"' in csbody:
    ok("surface materials are sampled across the pack, not across the classified handful")
else:
    bad("surface materials are sampled across the pack, not across the classified handful",
        "a filename classifier finds nothing in a pack whose prefabs are numbered")

# ---- the pack folder is FOUND, not assumed ---------------------------------------------------
#
# The default was a hard-coded path. A folder that does not exist classifies zero prefabs and
# reports a calm zero, which reads as an empty pack rather than as a wrong path.
if "private static string LocatePack()" in tools and "IsValidFolder(candidates[i])" in tools:
    ok("the pack folder is located rather than assumed")
else:
    bad("the pack folder is located rather than assumed",
        "a wrong path reports an empty pack, which looks like a pack with nothing in it")

# ---- and there is a way to SEE what is in the pack --------------------------------------------
#
# Everything above went wrong because the pack was reasoned about from a document instead of
# looked at. The inventory lists folders, pieces with their size and pivot offset, and the
# materials - so the real modules can be named by reading them.
if "private static string Inventory(" in tools and "Pivot" in tools:
    ok("the pack can be listed - folders, pieces, pivots and materials")
else:
    bad("the pack can be listed - folders, pieces, pivots and materials",
        "without it the kit is identified by guessing at filenames")

# ---- the catalog is written from VERIFIED paths, not from guessed names -----------------------
#
# The automatic classifier matched English words against a pack that numbers its prefabs: three
# of a hundred and five were classified, one of them a 36 x 57 m demo assembly, and the surface
# density was measured off those.
vc = code("Assets/CatchIfYouCan/Editor/HQVerifiedCatalog.cs") or ""

if vc and "walls prefabs/" in vc and "5.prefab" in vc:
    ok("the catalog is written from explicit verified asset paths")
else:
    bad("the catalog is written from explicit verified asset paths",
        "a filename classifier finds nothing in a pack whose prefabs are numbered")

# Materials are resolved ON the reference prefab, because the pack holds three materials called
# "white", three called "blue" and nineteen called "1". Asking the piece that wears it is the
# only lookup that cannot pick the wrong one.
if "private static Material MaterialOn(" in vc and "GetComponentsInChildren<MeshRenderer>" in vc:
    ok("a surface material is resolved on the piece that wears it, not by a project search")
else:
    bad("a surface material is resolved on the piece that wears it, not by a project search",
        "the pack has three materials called 'white' and nineteen called '1'")

# A name that is not unique is refused rather than guessed at.
if "MEHRDEUTIG" in (read("Assets/CatchIfYouCan/Editor/HQVerifiedCatalog.cs") or ""):
    ok("an ambiguous material name is refused rather than picked from")
else:
    bad("an ambiguous material name is refused rather than picked from",
        "picking the first of several identically named materials is a coin toss")

# One measured anchor, everything else derived FROM it and said to be derived. The pack has no
# floor or ceiling part to measure a density against, so claiming one would be invention.
# Scoped to the method that writes the number. The file's explanatory paragraph also contains
# the word "ABGELEITET", so a file-wide grep stays green while the line that labels the value
# claims it was measured - which is the failure this check exists to prevent.
vcraw = read("Assets/CatchIfYouCan/Editor/HQVerifiedCatalog.cs") or ""
parity = re.search(r"private static SurfaceMaterial ByTexelParity.*?\n        \}", vcraw, re.S)
pbody = parity.group(0) if parity else ""
if pbody and "ABGELEITET" in pbody:
    ok("floor and ceiling density is derived by texel parity and reported as derived")
else:
    bad("floor and ceiling density is derived by texel parity and reported as derived",
        "the pack has no floor or ceiling part, so a measured density there would be invented")

# The measurement uses the two LARGEST extents. The pack's wall meshes are about 4 x 4 x 0.1 and
# which axis carries the height depends on whether the prefab corrects the exporter's Z-up.
if "largest.x + largest.y + largest.z - a - c" in vc:
    ok("the pattern is measured across the two largest extents, not across X and Y")
else:
    bad("the pattern is measured across the two largest extents, not across X and Y",
        "the thin axis is never the one the texture spans, and it is not always Y")

# The doorway matches the pack's own measured opening, so the door leaf drops in at authored
# scale instead of being squeezed.
m = re.search(r"DoorWidth\s*=\s*([0-9.]+)f", mrb_code)
h = re.search(r"DoorHeight\s*=\s*([0-9.]+)f", mrb_code)
if m and h and abs(float(m.group(1)) - 1.25) < 0.001 and abs(float(h.group(1)) - 2.60) < 0.001:
    ok("the doorway is the pack's own measured 1.25 x 2.60")
else:
    bad("the doorway is the pack's own measured 1.25 x 2.60",
        "a squeezed door leaf is the one thing the pack was chosen to avoid")

# And it still fits under the ceiling with a lintel left over.
if h and float(h.group(1)) < 3.0:
    ok("the doorway leaves a lintel under a 3 m ceiling (%.2f m)" % float(h.group(1)))
else:
    bad("the doorway leaves a lintel under a 3 m ceiling",
        "a door as tall as the room has no header and no wall above it")

# ---- a vendor insert is placed by its MESH, never by its pivot --------------------------------
#
# This pack's pivots sit 13 to 40 m from the geometry they belong to: the inventory reads
# "Pivot 32.5 m" for the door wall and "31.4 m" for the window, because every piece kept the
# origin of the one apartment scene they were exported from. Setting localPosition puts the
# PIVOT at the opening, so the door itself lands tens of metres away - on screen, a door frame
# up near the ceiling and a window above it.
if "TryMeasureInSpace(go.transform, wall, out Bounds placed)" in mrb_code and \
        "target - placed.center" in mrb_code:
    ok("an insert is positioned by its mesh, correcting a pivot that is metres away")
else:
    bad("an insert is positioned by its mesh, correcting a pivot that is metres away",
        "placing by pivot puts a door 30 m from the doorway in this pack")

# Measured on the VISIBLE parts. Most of a vendor wall prefab has just been switched off - it is
# the shell around the door - and including it would centre the door on the shell.
measure = re.search(r"private static bool TryMeasureInSpace.*?\n        \}", mrb_code, re.S)
mbody = measure.group(0) if measure else ""
if mbody and "if (!renderer.enabled)" in mbody:
    ok("only the visible parts are measured, not the wall shell that was switched off")
else:
    bad("only the visible parts are measured, not the wall shell that was switched off",
        "the disabled shell is most of the prefab and would drag the centre onto itself")

# Eight corners, because a rotated child's axis-aligned size is not its size in the parent frame.
if mbody and "Corner(c)" in mbody and "space.InverseTransformPoint" in mbody:
    ok("bounds are taken from transformed corners, so rotation cannot skew them")
else:
    bad("bounds are taken from transformed corners, so rotation cannot skew them",
        "centre-plus-size ignores rotation and the insert lands off the opening")

# ---- a room can be built by hand, through the same builder ------------------------------------
#
# Hand authoring must not become a second implementation of the room. It hands the production
# builder a room description written by a person; everything else is identical.
hand = code("Assets/CatchIfYouCan/Scripts/Procedural/HQRoomAuthoring.cs") or ""

if hand and "ModularRoomBuilder.Build(" in hand:
    ok("the hand-built room goes through the production builder")
else:
    bad("the hand-built room goes through the production builder",
        "a second room implementation is the mistake this project has already made twice")

if hand and "StructuralMeshFactory" not in hand and "CreatePrimitive" not in hand:
    ok("the hand-built room builds no geometry of its own")
else:
    bad("the hand-built room builds no geometry of its own",
        "it describes a room; making one is the builder's job")

# The window choice is explicit for a person and derived for the generator - and the derivation
# stays in one place, so hand authoring cannot drift from what a mission builds.
if "int windowMask, out string error)" in mrb_code and "WantsWindow(room, dir)" in mrb_code:
    ok("windows are explicit for hand authoring and derived for the generator, from one place")
else:
    bad("windows are explicit for hand authoring and derived for the generator, from one place",
        "two copies of the rule is how a hand-built room stops matching a generated one")

# ---- the piece browser LOOKS, and does not touch ----------------------------------------------
#
# Hand-building means dragging purchased pieces in, and the three questions that keep coming back
# are: why is that one so much bigger, why is that one white, and where is it relative to its own
# origin. The browser answers them by measuring. It must not answer them by editing the pack -
# a pack-wide reimport is what made the machine unusable, and scaling a piece to make it match is
# how a mismatch becomes a mystery.
browser = code("Assets/CatchIfYouCan/Editor/HQPieceBrowser.cs") or ""

for forbidden, why in [
        ("AssetDatabase.Refresh(", "a refresh can trigger a pack-wide reimport"),
        ("AssetDatabase.ImportAsset(", "reimporting a vendor asset is a pack edit"),
        ("SetTextureScale(", "retiling a vendor material edits a purchased asset"),
        ("SaveAssets(", "the browser writes nothing at all")]:
    if browser and forbidden not in browser:
        ok("the piece browser does not call %s" % forbidden.rstrip("("))
    else:
        bad("the piece browser does not call %s" % forbidden.rstrip("("), why)

# Nothing is scaled. The audit found the wall pieces share one height and a module ladder, so
# there is no scale to correct - and correcting one without proof is the rule this pass exists
# under.
if browser and "localScale =" not in browser:
    ok("the piece browser never rescales a purchased piece")
else:
    bad("the piece browser never rescales a purchased piece",
        "scaling to make pieces match hides whether they were ever mismatched")

# A multiple of the module width is a DESIGNED size. Prefab 15 is exactly twice the module and
# 16 exactly three times; marking those oversized and shrinking them would break a wall that was
# right.
if "ModuleMultiple" in browser and "Mathf.Abs(multiple - nearest) < 0.04f" in browser:
    ok("a piece that is a whole multiple of the module is read as designed, not as oversized")
else:
    bad("a piece that is a whole multiple of the module is read as designed, not as oversized",
        "15 and 16 are exactly 2x and 3x the module width")

# The module width is the pack's own median, not a number this project picked.
if "widths[widths.Count / 2]" in browser:
    ok("the module width is the pack's own median rather than a hard-coded 4 m")
else:
    bad("the module width is the pack's own median rather than a hard-coded 4 m",
        "a constant stops being true for the next pack")

# The audit runs on a button and is cached. Scanning inside OnGUI would re-open every prefab on
# every repaint.
if "private List<Piece> _pieces;" in browser and "Audit(_folder, out _moduleWidth)" in browser:
    ok("the audit runs on demand and is cached, not per repaint")
else:
    bad("the audit runs on demand and is cached, not per repaint",
        "opening every prefab each frame is what made the editor unusable")

# The vendor prefab goes in as a prefab INSTANCE - link, materials and geometry as purchased -
# and only the wrapper around it carries the corrected origin.
if "PrefabUtility.InstantiatePrefab(prefab, wrapper.transform)" in browser and \
        "instance.transform.localPosition = -grip" in browser:
    ok("the wrapper corrects the origin and the vendor prefab goes in untouched")
else:
    bad("the wrapper corrects the origin and the vendor prefab goes in untouched",
        "a piece whose origin is 30 m away cannot be placed by typing a position")

# White has three causes and they are not guessable from the screen, so the material line says
# which one applies: no base map at all, versus a base map that is simply white paint.
# Scoped to the method that writes the line. The closing paragraph of the report explains what
# "KEINE BaseMap" means, so a file-wide grep stays green while the line that would say it is
# gone - which is the failure this check exists to prevent, for the third time in this file.
browserraw = read("Assets/CatchIfYouCan/Editor/HQPieceBrowser.cs") or ""
desc = re.search(r"private static MaterialInfo Describe.*?\n        \}", browserraw, re.S)
dbody2 = desc.group(0) if desc else ""
if dbody2 and "KEINE BaseMap" in dbody2:
    ok("a material with no base map is named as such, not left to look like a lost material")
else:
    bad("a material with no base map is named as such, not left to look like a lost material",
        "this pack ships textureless FBX duplicates beside its textured materials")

# ---- sorting the hierarchy is a MOVE, and nothing else ----------------------------------------
#
# Reparenting is safe in this scene because Unity serialises references by fileID rather than by
# path, nothing here calls DontDestroyOnLoad on itself (the one thing that forces an object to
# stay a scene root), and the only name-based lookup - GameObject.Find("Door_Green_Fog") - is
# parent-independent. What would NOT be safe is a tidy-up that quietly changed something else.
hier = code("Assets/CatchIfYouCan/Editor/MainMenuHierarchyTool.cs") or ""

if hier and "Undo.SetTransformParent" in hier:
    ok("the hierarchy tool reparents through Undo.SetTransformParent")
else:
    bad("the hierarchy tool reparents through Undo.SetTransformParent",
        "a hand-set m_Father cannot be undone and does not preserve the world transform")

# Verified, not assumed. And lossyScale rather than localScale: what must not change is the size
# on screen.
if "target.lossyScale" in hier and "Quaternion.Angle(rotBefore" in hier:
    ok("world position, rotation and lossyScale are re-measured after every move")
else:
    bad("world position, rotation and lossyScale are re-measured after every move",
        "'SetParent preserves the transform' is a claim until it is checked")

# A drift is REPORTED, never corrected. Correcting a child by hand hides the one thing worth
# knowing.
if "ABWEICHUNG" in (read("Assets/CatchIfYouCan/Editor/MainMenuHierarchyTool.cs") or "") and \
        "localPosition =" not in hier:
    ok("a transform drift is reported rather than compensated for")
else:
    bad("a transform drift is reported rather than compensated for",
        "manually fixing a child after a bad reparent hides the bad reparent")

# Hierarchy only. No component added or removed, nothing switched on or off, no prefab unpacked.
for forbidden, why in [
        ("AddComponent", "adding a component is not a hierarchy change"),
        ("DestroyImmediate", "a tidy-up must not delete anything"),
        ("SetActive", "switching an object on or off is a behaviour change"),
        ("UnpackPrefabInstance", "unpacking breaks the link to the purchased asset")]:
    if hier and forbidden not in hier:
        ok("the hierarchy tool never calls %s" % forbidden)
    else:
        bad("the hierarchy tool never calls %s" % forbidden, why)

# Folders are created at the origin with identity rotation and unit scale. A folder with a
# transform of its own silently moves everything put into it later, and "everything shifted a
# bit after the tidy-up" is hard to trace back.
if "SetPositionAndRotation(Vector3.zero, Quaternion.identity)" in hier:
    ok("an organisational folder sits at the origin, unrotated and unscaled")
else:
    bad("an organisational folder sits at the origin, unrotated and unscaled",
        "a folder with a transform moves everything parented into it")

# A plan first, and anything the audit could not clear stays unticked.
if "m.Do = false;" in hier and "ROLLE UNKLAR" in (read("Assets/CatchIfYouCan/Editor/MainMenuHierarchyTool.cs") or ""):
    ok("what the audit could not classify is offered unticked rather than moved")
else:
    bad("what the audit could not classify is offered unticked rather than moved",
        "safety beats a tidy hierarchy - an unclear object stays where it is")

# The scene is left dirty, not saved. Saving is the user's decision after looking.
if "MarkSceneDirty" in hier and "SaveScene" not in hier:
    ok("the scene is marked dirty and left for the user to save")
else:
    bad("the scene is marked dirty and left for the user to save",
        "saving on the user's behalf removes their chance to look first")

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
