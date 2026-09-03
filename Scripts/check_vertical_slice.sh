#!/usr/bin/env bash
#
# The Suburban House slice stays solvable.
#
# The player is sent in with a flashlight, an EMF detector, a UV lamp and a thermometer, so the
# only evidence they can observe there is EMF surges, UV traces and freezing temperatures.
# Every ghost in the game needs three evidence types and not one of the ten can have all three
# of theirs found with that kit - so a location that may host any of them is a location where
# the answer cannot be deduced, only guessed.
#
# The fix is content, not a change to what counts as evidence: the mission names its own roster.
# This guard is what stops that roster silently becoming unsolvable again when a ghost's
# evidence is retuned or a new entity is added.
#
# Needs a shell and python3.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "== Vertical slice guard =="
echo

python3 - "$ROOT" <<'PY'
import io, re, sys

root = sys.argv[1]
ghosts_src = io.open(root + "/Assets/CatchIfYouCan/Scripts/Ghost/GhostDefinitionFactory.cs",
                     encoding="utf-8").read()
missions_src = io.open(root + "/Assets/CatchIfYouCan/Scripts/Missions/MissionDefinitionFactory.cs",
                       encoding="utf-8").read()
ids_src = io.open(root + "/Assets/CatchIfYouCan/Scripts/Ghost/GhostIds.cs", encoding="utf-8").read()

# The evidence the slice kit can actually observe. Flashlight, EMF, UV, thermometer.
OBSERVABLE = {"EMFSurge", "UVTraces", "FreezingTemperature"}

passed = failed = 0
def ok(msg):
    global passed; passed += 1; print("  ok    " + msg)
def bad(msg, detail=""):
    global failed; failed += 1
    print("  FAIL  " + msg)
    if detail:
        print("        " + detail)

# GhostIds.Wanderer -> "the_wanderer"
id_consts = dict(re.findall(r'public const string (\w+) = "([^"]+)";', ids_src))

# Every ghost and its three evidence types.
ghosts = {}
for gid, name, e1, e2, e3 in re.findall(
        r'Create\(\s*GhostIds\.(\w+),\s*"([^"]+)",\s*EvidenceType\.(\w+),'
        r'\s*EvidenceType\.(\w+),\s*EvidenceType\.(\w+),', ghosts_src):
    ghosts[gid] = (name, {e1, e2, e3})

if not ghosts:
    bad("the ghost roster could be parsed",
        "GhostDefinitionFactory no longer matches the shape this guard reads")
else:
    ok("the ghost roster could be parsed (%d entities)" % len(ghosts))

# The Suburban House block, and the roster it names.
block = re.search(r'MissionTheme\.SuburbanHouse.*?\}\),', missions_src, re.S)
if block is None:
    bad("the Suburban House mission could be found")
    roster = []
else:
    ok("the Suburban House mission could be found")
    eligible = re.search(r'eligibleGhosts:\s*new\[\]\s*\{(.*?)\}', block.group(0), re.S)
    roster = re.findall(r'GhostIds\.(\w+)', eligible.group(1)) if eligible else []

if not roster:
    bad("the Suburban House names its own entity roster",
        "with the whole roster in play the case cannot be deduced from the kit taken in")
else:
    ok("the Suburban House names its own entity roster (%d entities)" % len(roster))

# Every named entity exists.
unknown = [g for g in roster if g not in ghosts or g not in id_consts]
if unknown:
    bad("every entity in the roster exists", "unknown: " + ", ".join(unknown))
elif roster:
    ok("every entity in the roster exists")

# Each must leave at least one observable trace, and no two may leave the same set - otherwise
# the evidence narrows to a coin toss and the identification is a guess wearing a journal.
signatures = {}
empty = []
for g in roster:
    if g not in ghosts:
        continue
    name, evidence = ghosts[g]
    sig = frozenset(evidence & OBSERVABLE)
    if not sig:
        empty.append(name)
    signatures.setdefault(sig, []).append(name)

if empty:
    bad("every entity leaves evidence the kit can find",
        "invisible to this kit: " + ", ".join(empty))
elif roster:
    ok("every entity leaves evidence the kit can find")

clashes = {s: n for s, n in signatures.items() if len(n) > 1}
if clashes:
    detail = "; ".join(
        "{%s} -> %s" % (", ".join(sorted(s)) or "nothing", " / ".join(n))
        for s, n in clashes.items())
    bad("no two entities share an evidence signature", detail)
elif roster:
    ok("no two entities share an evidence signature")
    for s, n in sorted(signatures.items(), key=lambda kv: kv[1]):
        print("          %-22s %s" % (n[0], ", ".join(sorted(s))))

# The kit the mission recommends must be the kit the guard assumed.
if block is not None:
    kit = set(re.findall(r'EquipmentIds\.(\w+)', block.group(0)))
    expected = {"Flashlight", "EmfDetector", "UvLight", "Thermometer"}
    if kit == expected:
        ok("the mission recommends the four slice tools")
    else:
        bad("the mission recommends the four slice tools",
            "found: " + (", ".join(sorted(kit)) or "none"))

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY

status=$?
if [ "$status" -ne 0 ]; then
  echo "VERTICAL SLICE GUARD FAILED"
  exit 1
fi
echo "VERTICAL SLICE GUARD PASSED"
