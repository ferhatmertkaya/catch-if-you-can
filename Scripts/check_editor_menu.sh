#!/usr/bin/env bash
#
# The Unity editor menu, enforced rather than trusted.
#
# Fifty-one commands accumulated over ten days, and the user could no longer tell which ones
# only look and which rewrite the asset folder. Docs/EDITOR_MENU_INVENTORY.md wrote them all
# down; this keeps the structure that came out of it from drifting back.
#
# Needs nothing but a shell and python3.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1

echo "== editor menu guard =="
echo

python3 - <<'PY'
import io, os, re, sys, glob

passed = failed = 0

def ok(m):
    global passed; passed += 1; print("  ok    %s" % m)

def bad(m, d=None):
    global failed; failed += 1; print("  FAIL  %s" % m)
    if d:
        for line in (d if isinstance(d, list) else [d]):
            print("        %s" % line)

def read(rel):
    try:
        return io.open(rel, encoding="utf-8", errors="replace").read()
    except OSError:
        return None

def code(rel):
    """The file with full-line comments removed. A guard that greps a whole file is a guard a
    doc comment can satisfy, and one forbidding a call matches the comment warning against it."""
    t = read(rel)
    if t is None:
        return None
    out = []
    for line in t.split("\n"):
        s = line.strip()
        if s.startswith("//") or s.startswith("*") or s.startswith("/*"):
            continue
        out.append(re.sub(r"//.*$", "", line))
    return "\n".join(out)

EDITOR = "Assets/CatchIfYouCan/Editor"

# ------------------------------------------------------------------ 1. every command is placed

# Every MenuItem, including the ones built from a constant - six of them are invisible to a
# search for MenuItem(" and were missed by the first inventory pass because of it.
paths = []
for f in glob.glob("Assets/**/*.cs", recursive=True):
    t = code(f) or ""
    paths += re.findall(r'\[MenuItem\("([^"]+)"', t)
    for const in re.findall(r'const string MenuPath = "([^"]+)"', t):
        paths.append(const)
    root = re.search(r'const string MenuRoot = "([^"]+)"', t)
    if root:
        for leaf in re.findall(r'MenuItem\(MenuRoot \+ "([^"]+)"', t):
            paths.append(root.group(1) + leaf)

GROUPS = ("Safe Inspection", "Scene Authoring", "HQ Assets", "Portal",
          "Build", "Assets bauen", "Debug and Legacy")

ours = [p for p in paths if p.startswith("Catch If You Can/")]
stray = [p for p in paths if "Catch If You Can" in p and not p.startswith("Catch If You Can/")]
ungrouped = [p for p in ours if p.split("/")[1] not in GROUPS]

if not stray:
    ok("no command hides in another root menu")
else:
    bad("no command hides in another root menu",
        ["%s - looking under the project's own menu never finds it" % s for s in stray])

if not ungrouped:
    ok("every command sits in one of the seven groups (%d commands)" % len(ours))
else:
    bad("every command sits in one of the seven groups", sorted(ungrouped))

# ------------------------------------------------------------------ 2. the labels say what happens

# A command that changes something and does not say so is the whole problem this menu had.
unlabelled = []
for p in ours:
    leaf = p.split("/")[-1]
    group = p.split("/")[1]
    if group == "Build":
        continue                      # a build writes only into the build folder
    if not re.search(r"\[[A-Z ]+\]$", leaf):
        unlabelled.append(p)
if not unlabelled:
    ok("every command carries a risk tag")
else:
    bad("every command carries a risk tag", sorted(unlabelled))

# Safe Inspection is the one promise the user relies on: click without thinking.
mislabelled = [p for p in ours
               if p.split("/")[1] == "Safe Inspection" and not p.endswith("[NUR LESEN]")]
if not mislabelled:
    ok("everything under Safe Inspection is tagged read-only")
else:
    bad("everything under Safe Inspection is tagged read-only", sorted(mislabelled))

# ------------------------------------------------------------------ 3. one catalog writer

tools = code(EDITOR + "/ModularInteriorTools.cs") or ""
writes = [c for c in ("AssetDatabase.CreateAsset", "AssetDatabase.SaveAssets",
                      "AssetDatabase.Refresh", "EditorUtility.SetDirty") if c in tools]
if not writes:
    ok("the pack classifier cannot write the catalog any more")
else:
    bad("the pack classifier cannot write the catalog any more",
        "still writing: " + ", ".join(writes) +
        " - it found 3 of 105 prefabs and could silently replace the verified catalog")

verified = code(EDITOR + "/HQVerifiedCatalog.cs") or ""
if "AssetDatabase.CreateAsset" in verified:
    ok("the verified writer is the one that remains")
else:
    bad("the verified writer is the one that remains", "nothing writes the catalog at all now")

# ------------------------------------------------------------------ 4. inactive-safe removal

room = code(EDITOR + "/HQTestRoomTool.cs") or ""
if "GameObject.Find(" in room:
    bad("removing the test room finds an inactive one",
        "GameObject.Find skips inactive objects; a switched-off room is not removed and the "
        "next Build puts a second one beside it")
elif "GetComponentsInChildren<Transform>(true)" in room:
    ok("removing the test room finds an inactive one")
else:
    bad("removing the test room finds an inactive one", "the scene must be walked, inactive included")

if "Undo.DestroyObjectImmediate" in room:
    ok("removing the test room can be undone")
else:
    bad("removing the test room can be undone", "DestroyImmediate without Undo is a lost afternoon")

# ------------------------------------------------------------------ 5. discovery survives tidying

meas = code(EDITOR + "/HQRoomMeasurement.cs") or ""
if "GetRootGameObjects" in meas and "CollectTopmost" in meas:
    ok("the room is found through the whole scene, not only across its roots")
else:
    bad("the room is found through the whole scene, not only across its roots",
        "the hierarchy tool offers to move these under a folder; a root-only search then "
        "reports an empty room")

if "name != ScaleRootName" in meas:
    ok("the scaling root is not counted as part of the room it contains")
else:
    bad("the scaling root is not counted as part of the room it contains",
        "it starts with the same prefix; counting it makes the room contain its own container")

apply_src = code(EDITOR + "/HQRoomScaleApply.cs") or ""
if "CollectRoomObjects" in apply_src:
    ok("the scaling tool uses the same discovery as the measurement")
else:
    bad("the scaling tool uses the same discovery as the measurement",
        "two ways of finding the room drift apart silently")

# ------------------------------------------------------------------ 6. nothing saves a scene behind you

# One command used to call SaveScene, which wrote out every unrelated edit anyone had open.
savers = []
for f in glob.glob(EDITOR + "/*.cs"):
    t = code(f) or ""
    if "EditorSceneManager.SaveScene(" in t:
        savers.append(os.path.basename(f))
allowed = {"DevelopmentLabBuilder.cs"}          # it creates the scene files it saves
rogue = [s for s in savers if s not in allowed]
if not rogue:
    ok("no authoring command saves the open scene behind the user")
else:
    bad("no authoring command saves the open scene behind the user", sorted(rogue))

# ------------------------------------------------------------------ 7. the dangerous four ask first

gate = code(EDITOR + "/DangerousCommandGate.cs") or ""
if all(f in gate for f in ("Betroffene Assets", "Reimport", "Szenen speichern", "Abbrechen")):
    ok("the confirmation states scope, count, reimport, saving and the way out")
else:
    bad("the confirmation states scope, count, reimport, saving and the way out",
        "a dialog that does not say what changes teaches the user to click through it")

for name, f in (("HQ Pack Optimizer", "HQPackOptimizer.cs"),
                ("Integrate External Assets", "ExternalAssetIntegrator.cs"),
                ("Setup Project", "CatchIfYouCanProjectSetup.cs"),
                ("Rebuild All Lab Scenes", "DevelopmentLabBuilder.cs")):
    t = code(EDITOR + "/" + f) or ""
    if "DangerousCommandGate.Confirm" in t:
        ok("%s asks before it runs" % name)
    else:
        bad("%s asks before it runs" % name, "it can rewrite large parts of the project")

# One gate, not four hand-written dialogs that say four different things.
gates = sum(1 for f in glob.glob(EDITOR + "/*.cs")
            if "class DangerousCommandGate" in (code(f) or ""))
if gates == 1:
    ok("there is exactly one confirmation implementation")
else:
    bad("there is exactly one confirmation implementation", "%d found" % gates)

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
status=$?
echo
if [ $status -eq 0 ]; then echo "EDITOR MENU GUARD PASSED"; else echo "EDITOR MENU GUARD FAILED"; fi
exit $status
