#!/usr/bin/env bash
#
# Every asset a scene names really arrives on the machine that opens it.
#
# Unity resolves a prefab instance by GUID. If the GUID is declared by no .meta the scene
# shows "Missing Prefab with guid: ..." in red and the object is gone; if the .meta is there
# but the asset behind it did not import, the same red line appears, because a model that
# fails to import produces no objects for the reference to land on.
#
# Both had already happened here, in the shape this project keeps repeating: a name that
# resolves nowhere. First it was a Resources.Load path with the project name in it twice.
# Then it was four tags and a layer that TagManager.asset had never heard of. Then it was
# CIYC_HauntedRotaryPhone - the GUID, the .meta, the scene reference and the LFS pointer were
# all correct in the repository, and the working copy that opened the scene held a 134-byte
# text file where a 110 MB binary FBX belongs, because git-lfs had fetched some objects and
# not that one. Nothing in the repository was wrong, so nothing in the repository could say so.
#
# So the working copy is checked too, and the check is careful about which working copy it is
# looking at:
#
#   - every LFS object still a pointer  -> a checkout that never asked for LFS content
#                                          (CI does exactly this). Nothing to say; skip.
#   - every LFS object materialised     -> a developer machine after `git lfs pull`. Verify
#                                          the sizes.
#   - some of each                      -> a PARTIAL fetch. This is the failure. Name the
#                                          files that are still pointers, because those are
#                                          the ones Unity will show in red.
#
# Needs a shell, git and python3. Reads only; changes nothing.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "== Asset reference and LFS payload guard =="
echo

python3 - "$ROOT" <<'PY'
import fnmatch, io, os, re, subprocess, sys

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
def note(msg):
    print("  note  " + msg)

def git(*args):
    return subprocess.run(["git"] + list(args), cwd=root, check=True,
                          stdout=subprocess.PIPE).stdout

def human(n):
    return "%.2f MiB" % (n / 1048576.0) if n < 1073741824 else "%.2f GiB" % (n / 1073741824.0)

tracked = git("ls-files", "-z").decode("utf-8", "replace").split("\0")
tracked = [p for p in tracked if p]
tracked_set = set(tracked)

# ------------------------------------------------------------------ 1. the LFS rules

ATTR = os.path.join(root, ".gitattributes")
rules = []
if not os.path.isfile(ATTR):
    bad(".gitattributes is present")
else:
    for raw in io.open(ATTR, encoding="utf-8"):
        line = raw.strip()
        if not line or line.startswith("#") or "filter=lfs" not in line:
            continue
        rules.append(line.split()[0])
    if rules:
        ok(".gitattributes declares %d git-lfs rule(s)" % len(rules))
    else:
        bad(".gitattributes declares at least one git-lfs rule",
            "large binaries would be committed into the pack instead")

# A rule is a gitattributes pattern. The ones this project writes are either a full path or a
# single directory glob; expand both against the tracked set rather than against the disk, so
# a file that exists but was never added is caught as untracked rather than silently accepted.
lfs_paths = []
unmatched = []
for rule in rules:
    pattern = rule.lstrip("/")
    if any(c in pattern for c in "*?["):
        hit = [p for p in tracked if fnmatch.fnmatch(p, pattern)]
    else:
        hit = [pattern] if pattern in tracked_set else []
    if hit:
        lfs_paths.extend(hit)
    else:
        unmatched.append(rule)

if rules:
    if unmatched:
        bad("every git-lfs rule matches a tracked file",
            ["%s matches nothing - the asset moved and the rule did not" % r for r in unmatched])
    else:
        ok("every git-lfs rule matches a tracked file (%d file(s) in total)" % len(lfs_paths))

lfs_paths = sorted(set(lfs_paths))

# ------------------------------------------------------- 2. what git actually stored

POINTER = re.compile(rb"^version https://git-lfs\.github\.com/spec/v1\n"
                     rb"oid sha256:([0-9a-f]{64})\n"
                     rb"size (\d+)\n$")

pointers = {}      # path -> (oid, size)
raw_in_pack = []
unparsed = []
if lfs_paths:
    spec = "".join("HEAD:%s\n" % p for p in lfs_paths)
    proc = subprocess.run(["git", "cat-file", "--batch"], cwd=root, input=spec.encode(),
                          stdout=subprocess.PIPE, check=True)
    out = proc.stdout
    cursor = 0
    for path in lfs_paths:
        nl = out.index(b"\n", cursor)
        header = out[cursor:nl].decode()
        cursor = nl + 1
        parts = header.split()
        if len(parts) < 3:
            unparsed.append(path)
            continue
        size = int(parts[2])
        blob = out[cursor:cursor + size]
        cursor += size + 1
        m = POINTER.match(blob)
        if m:
            pointers[path] = (m.group(1).decode(), int(m.group(2)))
        elif size > 1024:
            raw_in_pack.append(path)
        else:
            unparsed.append(path)

    if raw_in_pack:
        bad("every git-lfs file is stored as a pointer, not as pack content",
            ["%s was committed before its rule existed" % p for p in raw_in_pack])
    else:
        ok("every git-lfs file is stored as a pointer, not as pack content")

    if unparsed:
        bad("every git-lfs pointer parses",
            ["%s is neither a valid pointer nor plausible content" % p for p in unparsed])
    else:
        ok("every git-lfs pointer parses (version, oid, size)")

# ------------------------------------------------- 3. what this working copy holds

materialised, still_pointer, absent, wrong_size = [], [], [], []
for path in sorted(pointers):
    oid, want = pointers[path]
    full = os.path.join(root, path)
    if not os.path.exists(full):
        absent.append(path)
        continue
    have = os.path.getsize(full)
    if have == want:
        materialised.append(path)
    elif have < 1024:
        still_pointer.append((path, have, want))
    else:
        wrong_size.append((path, have, want))

if pointers:
    if absent:
        bad("every git-lfs file exists in the working copy",
            ["%s is tracked and not on disk" % p for p in absent])
    else:
        ok("every git-lfs file exists in the working copy")

    if wrong_size:
        bad("every materialised git-lfs file is the size its pointer claims",
            ["%s is %s, pointer says %s - a truncated fetch" % (p, human(h), human(w))
             for p, h, w in wrong_size])
    else:
        ok("every materialised git-lfs file is the size its pointer claims")

    if not materialised and still_pointer:
        ok("git-lfs content state is consistent (all %d still pointers - a checkout "
           "that did not ask for LFS content)" % len(still_pointer))
        note("run `git lfs pull` before opening the project in Unity")
    elif materialised and still_pointer:
        bad("git-lfs content state is consistent (%d materialised, %d still pointers)"
            % (len(materialised), len(still_pointer)),
            ["a PARTIAL fetch: Unity will show these as missing"]
            + ["%s (%d bytes on disk, %s expected)" % (p, h, human(w))
               for p, h, w in still_pointer]
            + ["fix with: git lfs fetch --all origin && git lfs checkout"])
    else:
        ok("git-lfs content state is consistent (all %d materialised)" % len(materialised))

    payload = sum(size for _, size in pointers.values())
    note("git-lfs payload: %s across %d file(s)" % (human(payload), len(pointers)))
    if payload > 1073741824:
        note("this is over GitHub's free 1 GiB LFS storage and bandwidth allowance; when the "
             "allowance runs out mid-fetch, git-lfs leaves the remaining objects as pointers, "
             "which is exactly the partial state checked above")
    biggest = sorted(pointers.items(), key=lambda kv: -kv[1][1])[:3]
    for path, (_, size) in biggest:
        note("  %9s  %s" % (human(size), path))

# ------------------------------------------------------------- 4. the GUID namespace

declared = {}                 # guid -> asset path (the .meta without its suffix)
no_guid = []
duplicate = {}
for path in tracked:
    if not (path.startswith("Assets/") and path.endswith(".meta")):
        continue
    guid = None
    try:
        for line in io.open(os.path.join(root, path), encoding="utf-8", errors="replace"):
            if line.startswith("guid: "):
                guid = line[6:].strip()
                break
    except OSError:
        pass
    if guid is None:
        no_guid.append(path)
        continue
    if guid in declared:
        duplicate.setdefault(guid, [declared[guid]]).append(path[:-5])
    else:
        declared[guid] = path[:-5]

if no_guid:
    bad("every .meta under Assets declares a guid",
        ["%s declares none - Unity will mint a new one and break every reference to it" % p
         for p in no_guid])
else:
    ok("every .meta under Assets declares a guid (%d in total)" % len(declared))

if duplicate:
    bad("no two assets share a guid",
        ["%s: %s" % (g, ", ".join(ps)) for g, ps in duplicate.items()])
else:
    ok("no two assets share a guid")

# An asset without a .meta gets a fresh GUID on the next import, on every machine
# independently, which is how a reference that resolved yesterday stops resolving today.
missing_meta = [p for p in tracked
                if p.startswith("Assets/") and not p.endswith(".meta")
                and (p + ".meta") not in tracked_set]
if missing_meta:
    bad("every tracked asset under Assets carries its .meta",
        ["%s has no .meta" % p for p in missing_meta[:12]]
        + (["... and %d more" % (len(missing_meta) - 12)] if len(missing_meta) > 12 else []))
else:
    ok("every tracked asset under Assets carries its .meta")

# A .meta whose asset is gone is harmless - Unity deletes it on import - but it is worth
# saying out loud, because the same listing is how a deleted asset is noticed.
orphan_meta = [p for p in tracked
               if p.startswith("Assets/") and p.endswith(".meta")
               and not os.path.exists(os.path.join(root, p[:-5]))]
if orphan_meta:
    note("%d .meta file(s) describe a path that is not in the working copy (empty folders "
         "git cannot store; Unity removes them on import)" % len(orphan_meta))

# --------------------------------------------- 5. every prefab instance resolves

REF = re.compile(r"(m_SourcePrefab|m_CorrespondingSourceObject): "
                 r"\{fileID: \d+, guid: ([0-9a-f]{32}), type: \d+\}")

refs = {}                      # guid -> set of documents naming it
for path in tracked:
    if not (path.startswith("Assets/") and path.endswith((".unity", ".prefab"))):
        continue
    try:
        body = io.open(os.path.join(root, path), encoding="utf-8", errors="replace").read()
    except OSError:
        continue
    for _, guid in REF.findall(body):
        refs.setdefault(guid, set()).add(path)

# A guid nothing declares has two very different causes, and they are indistinguishable from
# inside the repository:
#
#   1. CLAUDE.md mistake 14 - an asset was deleted and the things pointing at it were not. The
#      scene looks fine until it is opened.
#   2. It belongs to a purchased pack that is gitignored on purpose (Asset Store licence, and
#      the LFS payload is already over GitHub's free allowance). On a machine with the pack it
#      resolves; here it cannot.
#
# Telling them apart needs knowledge only a machine WITH the pack has, so that machine writes
# it down: Scripts/write_vendor_manifest.sh records guid -> path for every vendor asset, and
# the result is committed. Three-way verdict below. The teeth are kept: a guid the manifest
# does not name still fails, and so does one it names while the pack IS installed - that means
# the pack changed under us, which no absence excuses.
vendor_roots = []
try:
    for line in io.open(os.path.join(root, ".gitignore"), encoding="utf-8",
                        errors="replace").read().splitlines():
        line = line.strip()
        if line.startswith("/Assets/") and line.endswith("/"):
            vendor_roots.append(line[1:-1])
except OSError:
    pass

MANIFEST = "Docs/VENDOR_ASSET_MANIFEST.txt"
vendor_guids = {}
manifest_path = os.path.join(root, MANIFEST)
if os.path.exists(manifest_path):
    for line in io.open(manifest_path, encoding="utf-8", errors="replace").read().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split(None, 1)
        if len(parts) == 2 and re.match(r"^[0-9a-f]{32}$", parts[0]):
            vendor_guids[parts[0]] = parts[1]

installed = [r for r in vendor_roots if os.path.isdir(os.path.join(root, r))]

unresolved = sorted(g for g in refs if g not in declared)
absent_pack = []      # named by the manifest, pack not installed here - expected
changed_pack = []     # named by the manifest, pack IS installed - the pack moved under us
truly_missing = []    # named by nobody - mistake 14

for g in unresolved:
    if g not in vendor_guids:
        truly_missing.append(g)
        continue
    owner = vendor_guids[g].split("/")
    root_of = next((r for r in vendor_roots if vendor_guids[g].startswith(r + "/")), None)
    if root_of is not None and root_of in installed:
        changed_pack.append(g)
    else:
        absent_pack.append(g)

if truly_missing or changed_pack:
    detail = ["%s - named by %s" % (g, ", ".join(sorted(refs[g]))) for g in truly_missing]
    detail += ["%s - %s, and that pack IS installed here" % (g, vendor_guids[g])
               for g in changed_pack]
    if not vendor_guids:
        detail.append("no %s in this checkout - run Scripts/write_vendor_manifest.sh on the "
                      "machine that has the purchased packs and commit the result, so this "
                      "check can tell a missing pack from a deleted asset" % MANIFEST)
    bad("every prefab instance resolves, or is a known vendor asset", detail)
else:
    ok("every prefab instance resolves, or is a known vendor asset (%d guid(s))" % len(refs))

if absent_pack:
    note("%d guid(s) belong to a purchased pack that is not in this working copy; the manifest "
         "names them, so this is the expected state here and not a broken reference"
         % len(absent_pack))
    for r in vendor_roots:
        n = sum(1 for g in absent_pack if vendor_guids[g].startswith(r + "/"))
        if n:
            note("  %d from %s" % (n, r))
    note("  a scene naming them cannot be opened without the pack; installing it is a "
         "prerequisite for building, not something CI can supply")

# ------------------------------- 6. and the ones behind git-lfs actually arrived

lfs_by_asset = {}
for path in pointers:
    lfs_by_asset[path] = pointers[path]

pointer_paths = set(p for p, _, _ in still_pointer)
instanced_lfs = []
broken_instances = []
for guid, documents in refs.items():
    asset = declared.get(guid)
    if asset is None or asset not in lfs_by_asset:
        continue
    instanced_lfs.append(asset)
    if asset in pointer_paths or asset in absent:
        broken_instances.append((asset, sorted(documents)))

if not instanced_lfs:
    pass
elif not materialised and still_pointer:
    # The checkout never asked for LFS content, so every instanced model is a pointer and
    # saying so eight times says nothing. The state was already reported one check above.
    ok("every git-lfs asset a scene instances has arrived on this machine "
       "(not checked - this checkout holds no LFS content at all)")
else:
    if broken_instances:
        bad("every git-lfs asset a scene instances has arrived on this machine",
            ["%s is instanced by %s and is not here" % (a, ", ".join(d))
             for a, d in broken_instances])
    else:
        ok("every git-lfs asset a scene instances has arrived on this machine "
           "(%d asset(s))" % len(set(instanced_lfs)))

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
