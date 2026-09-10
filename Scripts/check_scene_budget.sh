#!/usr/bin/env bash
#
# Scene mesh budget.
#
# Every asset a scene REFERENCES is loaded when that scene loads, whether the object
# carrying it is active or not. So a scene's cost is not what is switched on in it; it is
# the sum of what it names.
#
# This exists because the app booted on an iPhone, played its intro, and closed - with no
# managed exception, because iOS had killed it. 01_MainMenu names 24.7 million triangles in
# eleven models, about 846 MiB of vertex and index buffers before a single texture, and every
# prop in it - a candle holder, a rotary phone, a table - is roughly 1.5 million triangles on
# its own. These are raw photogrammetry exports that were never decimated.
#
# The numbers below are a BASELINE, not a target. They are far over any mobile budget and the
# fix is to decimate the models, which needs the Unity Editor. What this guard does until then
# is stop the number growing and keep it visible - a number nobody records is a number nobody
# can tell has moved.
set -eu

REPO="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO"

BASELINE="Scripts/scene_budget.txt"

python3 - "$BASELINE" <<'PY'
import glob, os, re, struct, sys

baseline_path = sys.argv[1]

passed = failed = 0
def ok(m):
    global passed; passed += 1; print("  ok    %s" % m)
def bad(m, d=""):
    global failed; failed += 1; print("  FAIL  %s" % m)
    if d: print("        %s" % d)

print("== scene mesh budget ==\n")

# ---------------------------------------------------------------- guid -> asset we own
guid_to_asset = {}
for meta in glob.glob("Assets/CatchIfYouCan/**/*.meta", recursive=True):
    asset = meta[:-5]
    if not os.path.isfile(asset):
        continue
    try:
        for line in open(meta, errors="ignore"):
            if line.startswith("guid:"):
                guid_to_asset[line.split()[1].strip()] = asset
                break
    except OSError:
        pass

# ---------------------------------------------------------------- measure one binary FBX
def triangles(path):
    """Vertices and triangles of a binary FBX, read from the file rather than guessed."""
    try:
        data = open(path, "rb").read()
    except OSError:
        return None
    if data[:21] != b"Kaydara FBX Binary  \x00":
        return None                      # ASCII or another format: not measured, not counted
    wide = struct.unpack("<I", data[23:27])[0] >= 7500
    stats = {"Vertices": 0, "PolygonVertexIndex": 0}

    def node(off):
        if wide:
            end, nprops, plen = struct.unpack("<QQQ", data[off:off + 24]); off += 24
        else:
            end, nprops, plen = struct.unpack("<III", data[off:off + 12]); off += 12
        nlen = data[off]; off += 1
        name = data[off:off + nlen].decode("ascii", "replace"); off += nlen
        if end == 0:
            return 0
        props_end = off + plen
        if name in stats and nprops == 1 and chr(data[off]) in "dfil":
            stats[name] += struct.unpack("<III", data[off + 1:off + 13])[0]
        off = props_end
        while off < end - (25 if wide else 13):
            off = node(off)
        return end

    off = 27
    sys.setrecursionlimit(100000)
    while True:
        nxt = node(off)
        if nxt == 0 or nxt <= off:
            break
        off = nxt
    return stats["Vertices"] // 3, stats["PolygonVertexIndex"] // 3

# ---------------------------------------------------------------- the build scenes
build = open("ProjectSettings/EditorBuildSettings.asset", errors="ignore").read()
scenes = re.findall(r'enabled: 1\s*\n\s*path: (\S+)', build)

measured = {}
for scene in scenes:
    if not os.path.isfile(scene):
        continue
    used = set(re.findall(r'guid: ([0-9a-f]{32})', open(scene, errors="ignore").read()))
    verts = tris = 0
    for g in used:
        asset = guid_to_asset.get(g)
        if not asset or not asset.lower().endswith(".fbx"):
            continue
        if os.path.getsize(asset) < 1048576:      # under a MiB cannot move this number
            continue
        r = triangles(asset)
        if r:
            verts += r[0]; tris += r[1]
    # 48 bytes a vertex (position, normal, tangent, uv) plus a 32-bit index each
    mib = (verts * 48 + tris * 3 * 4) / 1048576.0
    measured[os.path.basename(scene)] = (verts, tris, mib)
    print("  %-28s %13s verts %13s tris %9.1f MiB"
          % (os.path.basename(scene), f"{verts:,}", f"{tris:,}", mib))

print()

# ---------------------------------------------------------------- against the baseline
recorded = {}
if os.path.exists(baseline_path):
    for line in open(baseline_path, errors="ignore"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split()
        if len(parts) >= 3:
            recorded[parts[0]] = (int(parts[1]), int(parts[2]))

if not recorded:
    bad("a baseline is recorded", "write Scripts/scene_budget.txt")
else:
    ok("a baseline is recorded (%d scene(s))" % len(recorded))

grown = []
for name, (verts, tris, mib) in measured.items():
    if name not in recorded:
        grown.append("%s is not in the baseline at all" % name)
        continue
    base_v, base_t = recorded[name]
    if tris > base_t:
        grown.append("%s: %s tris, baseline %s (+%s)"
                     % (name, f"{tris:,}", f"{base_t:,}", f"{tris - base_t:,}"))

if grown:
    bad("no build scene got heavier than its baseline", "; ".join(grown))
else:
    ok("no build scene got heavier than its baseline")

# The one number that matters for the platform this game targets.
MOBILE_TRIS = 400_000
over = [(n, v[1]) for n, v in measured.items() if v[1] > MOBILE_TRIS]
if over:
    print()
    print("  note  %d build scene(s) are over the %s-triangle mobile working figure:"
          % (len(over), f"{MOBILE_TRIS:,}"))
    for n, t in sorted(over, key=lambda x: -x[1]):
        print("  note    %-26s %13s tris  (%.0fx)" % (n, f"{t:,}", t / MOBILE_TRIS))
    print("  note  This is NOT a failure - it is the recorded state. It cannot be fixed by")
    print("  note  an import setting: Unity's model importer has no polygon reduction. The")
    print("  note  models have to be decimated, or given LODs, in the Editor or a DCC tool.")

print("\n  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY

status=$?
if [ "$status" -ne 0 ]; then
  echo "SCENE BUDGET GUARD FAILED"
  exit 1
fi
echo "SCENE BUDGET GUARD PASSED"
