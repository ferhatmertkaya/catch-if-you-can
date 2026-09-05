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

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
