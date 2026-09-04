#!/usr/bin/env bash
#
# Every tag and layer the game names really exists.
#
# Unity treats these two names in opposite - and equally bad - ways. Assigning a tag that is
# not defined throws UnityException, and so does CompareTag against one; the exception
# propagates out of whatever was building at that line, so the rest of that build is silently
# lost. A layer name that is not defined does not throw at all: LayerMask.NameToLayer returns
# -1 and the object quietly lands on layer 0.
#
# Both had already happened here. "Environment" was assigned by two runtime factories, compared
# by the NavMesh source filter and assigned by the Kenney prop builder, while being defined
# nowhere - so the van floor, the primitive rooms, the NavMesh sources and five of the 120 prop
# prefabs all died at that line. "LightSwitch" was assigned one statement before the switch got
# its InteractiveLightSwitch component, which took the breaker box down with it. The editor's
# own Setup Project could not restore either, because neither was in its RequiredTags list.
#
# This is the same shape as the Resources.Load path that had the project name in it twice: a
# name that resolves nowhere, failing where nobody was looking. So it is checked, not trusted.
#
# Needs a shell and python3.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "== Project tag and layer guard =="
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
    if detail:
        print("        " + detail)

# Unity's seven built-in tags. They are always defined and are never stored in the tags
# array, so a project that lists one of them there gets a duplicate in the dropdown rather
# than a definition.
BUILTIN_TAGS = {"Untagged", "Respawn", "Finish", "EditorOnly",
                "MainCamera", "Player", "GameController"}

# Unity's eight built-in layers occupy slots 0-7 and are not editable. TagManager.asset
# still serializes them, so they are read from the file like any other.
TAG_MANAGER = os.path.join(root, "ProjectSettings", "TagManager.asset")

# ---------------------------------------------------------------- 1. the settings file

try:
    tm = io.open(TAG_MANAGER, encoding="utf-8").read()
except OSError as e:
    print("  FAIL  ProjectSettings/TagManager.asset is readable")
    print("        " + str(e))
    sys.exit(1)

def yaml_list(src, key):
    """The items of a flat `key:` sequence, up to the next unindented-by-two mapping key."""
    m = re.search(r"^  %s:\n((?:  - .*\n)*)" % re.escape(key), src, re.M)
    if m is None:
        return None
    return [line[4:] for line in m.group(1).splitlines()]

user_tags = yaml_list(tm, "tags")
layers = yaml_list(tm, "layers")

if user_tags is None:
    bad("TagManager.asset has a tags list", "no `  tags:` sequence found")
    user_tags = []
else:
    ok("TagManager.asset has a tags list (%d user tags)" % len(user_tags))

if layers is None:
    bad("TagManager.asset has a layers list", "no `  layers:` sequence found")
    layers = []
else:
    ok("TagManager.asset has a layers list (%d slots)" % len(layers))

defined_tags = BUILTIN_TAGS | set(user_tags)
defined_layers = set(l for l in layers if l.strip())

dupes = [t for t in set(user_tags) if user_tags.count(t) > 1]
if dupes:
    bad("no tag is listed twice", "listed more than once: " + ", ".join(sorted(dupes)))
else:
    ok("no tag is listed twice")

shadowed = sorted(set(user_tags) & BUILTIN_TAGS)
if shadowed:
    bad("no user tag shadows a Unity built-in",
        "already built in, so this only duplicates the dropdown entry: " + ", ".join(shadowed))
else:
    ok("no user tag shadows a Unity built-in")

# ---------------------------------------------------------------- 2. what the code names

def source_files():
    for base, dirs, files in os.walk(os.path.join(root, "Assets")):
        dirs[:] = [d for d in dirs if d not in (".git",)]
        for f in files:
            if f.endswith(".cs"):
                yield os.path.join(base, f)

def code(path):
    """The file with comments removed.

    A guard that greps a name must never be satisfied by a comment mentioning it, and the
    reverse bites just as hard: a doc comment warning `do not CompareTag("Foo")` would
    otherwise invent a requirement for a tag nothing actually uses.
    """
    out = []
    for line in io.open(path, encoding="utf-8", errors="replace"):
        s = line.split("//", 1)[0]
        if s.strip().startswith("*"):
            continue
        out.append(s)
    return "".join(out)

TAG_USE = re.compile(
    r'\.tag\s*=\s*"([^"]*)"'
    r'|CompareTag\(\s*"([^"]*)"'
    r'|FindWithTag\(\s*"([^"]*)"'
    r'|FindGameObjectWithTag\(\s*"([^"]*)"'
    r'|FindGameObjectsWithTag\(\s*"([^"]*)"')

LAYER_USE = re.compile(r'NameToLayer\(\s*"([^"]*)"')

tag_sites = {}    # tag -> [ "path:line", ... ]
layer_sites = {}

for path in sorted(source_files()):
    rel = os.path.relpath(path, root)
    body = code(path)
    for lineno, line in enumerate(body.splitlines(), 1):
        for m in TAG_USE.finditer(line):
            name = next(g for g in m.groups() if g is not None)
            tag_sites.setdefault(name, []).append("%s:%d" % (rel, lineno))
        for m in LAYER_USE.finditer(line):
            layer_sites.setdefault(m.group(1), []).append("%s:%d" % (rel, lineno))

if not tag_sites:
    bad("the code names at least one tag",
        "nothing matched - this guard would pass vacuously, so the scan is wrong")
else:
    ok("the code names %d distinct tags" % len(tag_sites))

for name in sorted(tag_sites):
    where = tag_sites[name]
    if name in defined_tags:
        ok('tag "%s" is defined (%d use%s)' % (name, len(where), "" if len(where) == 1 else "s"))
    else:
        bad('tag "%s" is defined' % name,
            "assigning or comparing an undefined tag throws; used at " + ", ".join(where[:4])
            + ("" if len(where) <= 4 else " and %d more" % (len(where) - 4)))

for name in sorted(layer_sites):
    where = layer_sites[name]
    if name in defined_layers:
        ok('layer "%s" is defined (%d use%s)' % (name, len(where), "" if len(where) == 1 else "s"))
    else:
        bad('layer "%s" is defined' % name,
            "NameToLayer returns -1 for an undefined layer and says nothing; used at "
            + ", ".join(where[:4]))

# ---------------------------------------------------------------- 3. Setup Project restores them

setup_path = os.path.join(root, "Assets", "CatchIfYouCan", "Editor", "CatchIfYouCanProjectSetup.cs")
setup = code(setup_path)

def string_array(src, field):
    m = re.search(r"string\[\]\s+%s\s*=\s*\{(.*?)\}\s*;" % re.escape(field), src, re.S)
    if m is None:
        return None
    return set(re.findall(r'"([^"]*)"', m.group(1)))

required_tags = string_array(setup, "RequiredTags")
required_layers = string_array(setup, "RequiredLayers")

if required_tags is None:
    bad("CatchIfYouCanProjectSetup declares RequiredTags")
else:
    ok("CatchIfYouCanProjectSetup declares RequiredTags (%d)" % len(required_tags))
    needed = set(tag_sites) - BUILTIN_TAGS
    missing = sorted(needed - required_tags)
    if missing:
        bad("Setup Project would restore every tag the code uses",
            "used by the game but absent from RequiredTags: " + ", ".join(missing))
    else:
        ok("Setup Project would restore every tag the code uses")

    shadow = sorted(required_tags & BUILTIN_TAGS)
    if shadow:
        bad("RequiredTags does not ask for a built-in tag",
            "Setup Project would insert a duplicate: " + ", ".join(shadow))
    else:
        ok("RequiredTags does not ask for a built-in tag")

    undefined = sorted(required_tags - defined_tags)
    if undefined:
        bad("every tag Setup Project maintains is present in TagManager.asset",
            "in RequiredTags but not defined: " + ", ".join(undefined))
    else:
        ok("every tag Setup Project maintains is present in TagManager.asset")

if required_layers is None:
    bad("CatchIfYouCanProjectSetup declares RequiredLayers")
else:
    ok("CatchIfYouCanProjectSetup declares RequiredLayers (%d)" % len(required_layers))
    undefined = sorted(required_layers - defined_layers)
    if undefined:
        bad("every layer Setup Project maintains is present in TagManager.asset",
            "in RequiredLayers but not defined: " + ", ".join(undefined))
    else:
        ok("every layer Setup Project maintains is present in TagManager.asset")

# ---------------------------------------------------------------- 4. the call sites that broke

# Named individually, because each one is a build that silently stopped halfway and nobody
# noticed for the life of the project.
WITNESSES = [
    ("Assets/CatchIfYouCan/Scripts/Procedural/PrimitiveRoomFactory.cs", "Environment",
     "the primitive room factory tags its geometry"),
    ("Assets/CatchIfYouCan/Scripts/Procedural/VanBuilder.cs", "Environment",
     "the van floor is tagged"),
    ("Assets/CatchIfYouCan/Scripts/Procedural/NavMeshRuntimeBuilder.cs", "Environment",
     "the NavMesh source filter compares against a tag"),
    ("Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs", "LightSwitch",
     "the light switch is tagged before it gets its component"),
    ("Assets/CatchIfYouCan/Editor/KenneyRoomPrefabBuilder.cs", "Environment",
     "the Kenney prop builder tags what it scales"),
]

for rel, tag, what in WITNESSES:
    full = os.path.join(root, rel)
    if not os.path.isfile(full):
        bad(what, rel + " is gone")
        continue
    body = code(full)
    if ('"%s"' % tag) not in body:
        # Not a failure of the settings - the call site simply moved on. Say so rather than
        # pretending the tag is still needed there.
        ok(what + " (no longer names \"%s\")" % tag)
    elif tag in defined_tags:
        ok(what + ' — "%s" is defined' % tag)
    else:
        bad(what, '"%s" is used here and defined nowhere' % tag)

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
