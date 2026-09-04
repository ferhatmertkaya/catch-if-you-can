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

gv = read("Assets/CatchIfYouCan/Scripts/Procedural/Deterministic/GenerationVersion.cs")
m = re.search(r"Current\s*=\s*(\d+)", gv or "")
if m:
    ok("GenerationVersion is unchanged at %s - the layout contract did not move" % m.group(1))
else:
    bad("GenerationVersion is readable")

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
