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
import glob, io, re, sys

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


# ---- the identification is a decision, not a search ------------------------------------
#
# The journal used to raise EntityDiscovered on the tap, so a wrong answer looked exactly
# like no answer and working down the list always won. One answer, taken once, and the
# player is told which it was.

def code(path):
    text = io.open(root + path, encoding="utf-8").read()
    return "\n".join(l for l in text.splitlines()
                     if not l.strip().startswith("//") and not l.strip().startswith("///"))

entity_ui = code("/Assets/CatchIfYouCan/Scripts/UI/EntityListUI.cs")
mission_mgr = code("/Assets/CatchIfYouCan/Scripts/Missions/MissionManager.cs")
result_ui = code("/Assets/CatchIfYouCan/Scripts/UI/MissionResultUI.cs")

if "GameEvents.EntityDiscovered(" in entity_ui:
    bad("selecting an entity is not an identification",
        "EntityListUI raises EntityDiscovered directly again")
else:
    ok("selecting an entity is not an identification")

if "SubmitIdentification(" in entity_ui:
    ok("the journal has an explicit confirm step")
else:
    bad("the journal has an explicit confirm step",
        "EntityListUI must go through MissionManager.SubmitIdentification")

if re.search(r"IdentificationSubmitted\s*\)\s*\n\s*return IdentificationResult\.AlreadySubmitted",
             mission_mgr) or "IdentificationResult.AlreadySubmitted" in mission_mgr:
    ok("a second identification is refused")
else:
    bad("a second identification is refused",
        "without it the journal can be brute-forced one row at a time")

if "IdentificationCorrect" in result_ui:
    ok("the reward is paid for being right, not for turning up")
else:
    bad("the reward is paid for being right, not for turning up",
        "MissionResultUI must read MissionRuntime.IdentificationCorrect")


# ---- lighting is presentation, not generation ---------------------------------------------
#
# The house is lit from the mission seed. If that ever came out of a CiycRandom stream it
# would either move the layout or grow a stream the layout hash has to account for, and a
# lighting tweak would become a determinism change.

lighting = code("/Assets/CatchIfYouCan/Scripts/Environment/HouseLightingDirector.cs")

if re.search(r"CiycRandom|SeedManager\.CreateRandom|CiycStream", lighting):
    bad("lighting draws from no generation stream",
        "HouseLightingDirector must derive from the seed locally, not from CiycRandom")
else:
    ok("lighting draws from no generation stream")

if "RenderSettings" in lighting:
    ok("lighting owns the scene's ambient and fog")
else:
    bad("lighting owns the scene's ambient and fog",
        "the house would keep whatever the lobby left behind")

boot = code("/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs")
if "HouseLightingDirector.Apply(" in boot:
    ok("the mission lights its house")
else:
    bad("the mission lights its house", "InvestigationBootstrap never calls the director")

# Applied on activation, never while the world is only being previewed - RenderSettings
# belongs to the active scene and the lobby is the active one during a preview.
if re.search(r"private bool PrepareWorld\(\)(.|\n)*?HouseLightingDirector", boot) and \
   boot.index("HouseLightingDirector") < boot.index("private IEnumerator ActivateSequence"):
    prepare = boot[boot.index("private bool PrepareWorld()"):boot.index("public IEnumerator ActivateForEntry")]
    if "HouseLightingDirector" in prepare:
        bad("lighting is applied on entry, not on preview",
            "writing RenderSettings during a preview repaints the lobby")
    else:
        ok("lighting is applied on entry, not on preview")
else:
    ok("lighting is applied on entry, not on preview")


# ---- the ghost prefab writer and reader agree -----------------------------------------------
#
# This bug was half-fixed once: the runtime lookup was corrected from "CatchIfYouCan/Ghosts/"
# to "Ghosts/" and the editor tool went on writing to the old folder, so every ghost the
# integrator built stayed unreachable and every ghost in the game stayed a primitive capsule.

catalog = code("/Assets/CatchIfYouCan/Scripts/Ghost/GhostVisualCatalog.cs")
integrator = code("/Assets/CatchIfYouCan/Editor/ExternalAssetIntegrator.cs")

m = re.search(r'PrefabAssetFolder\s*=\s*"([^"]+)"', catalog)
if not m:
    bad("the ghost prefab folder is declared once",
        "expected GhostVisualCatalog.PrefabAssetFolder")
else:
    asset_folder = m.group(1)
    ok("the ghost prefab folder is declared once (%s)" % asset_folder)

    # It has to sit directly under a Resources folder for the runtime path to resolve.
    r = re.search(r'PrefabResourceFolder\s*=\s*"([^"]+)"', catalog)
    resource_folder = r.group(1) if r else ""
    expected = "Assets/CatchIfYouCan/Resources/" + resource_folder.rstrip("/")
    if asset_folder == expected:
        ok("the write folder resolves to the lookup path")
    else:
        bad("the write folder resolves to the lookup path",
            "Resources.Load(\"%s\") reads %s, but prefabs are written to %s"
            % (resource_folder, expected, asset_folder))

if re.search(r'"Assets/CatchIfYouCan/Resources/[^"]*Ghosts', integrator):
    bad("the integrator writes through the shared constant",
        "a hard-coded ghost prefab path is back in ExternalAssetIntegrator")
else:
    ok("the integrator writes through the shared constant")


# ---- runtime visibility: things that were built and could not be seen -----------------------

visual_factory = code("/Assets/CatchIfYouCan/Scripts/Equipment/EquipmentDefinitionFactory.cs")
if "VisualProfile = BuildVisualProfile(" in visual_factory:
    ok("every code-built item gets a visual profile")
else:
    bad("every code-built item gets a visual profile",
        "a null profile means the placeholder capsule, for every item in the game")

if re.search(r'ApplyModel\(\s*"Props/CIYC_Flashlight"', visual_factory):
    ok("the flashlight points at its finished model")
else:
    bad("the flashlight points at its finished model",
        "expected Resources/Props/CIYC_Flashlight")

import os
for rel in ["Props/CIYC_Flashlight.fbx", "Props/MAT_Flashlight.mat"]:
    if os.path.exists(root + "/Assets/CatchIfYouCan/Resources/" + rel):
        ok("Resources/" + rel + " exists")
    else:
        bad("Resources/" + rel + " exists", "the profile would load nothing")

portal_surface = code("/Assets/CatchIfYouCan/Scripts/Art/PortalSurface.cs")
# A renderer with no material draws nothing, and an invisible portal is indistinguishable
# from a portal that was never created.
if re.search(r"if \(shader == null\)\s*\n\s*return;", portal_surface):
    bad("the portal never ends up without a material",
        "PortalSurface returns early on a missing shader, leaving an invisible quad")
else:
    ok("the portal never ends up without a material")

if "SetOpacity(" in portal_surface:
    ok("the portal opening can fade in")
else:
    bad("the portal opening can fade in", "expected PortalSurface.SetOpacity")

shader = io.open(root + "/Assets/CatchIfYouCan/Shaders/Portal.shader", encoding="utf-8").read()
if "_Opacity" in shader and "Blend SrcAlpha" in shader:
    ok("the portal shader is blendable")
else:
    bad("the portal shader is blendable", "an opaque portal can only be switched on, not opened")

# The HUD is drawn over the game. Nothing on it may be a solid sheet.
factory = code("/Assets/CatchIfYouCan/Scripts/UI/RuntimeUIFactory.cs")
hud = factory[factory.index("private static void WireHUD("):] if "private static void WireHUD(" in factory else ""
if hud and hud.count("MakeOverlay(") >= 2:
    ok("the HUD's own panels are overlays, not sheets")
else:
    bad("the HUD's own panels are overlays, not sheets",
        "a CreatePanel inside the HUD screen is a near-opaque band across the game")

settings = code("/Assets/CatchIfYouCan/Scripts/UI/SettingsUI.cs")
if settings.count("ApplyAudioSettings()") >= 7:
    ok("every volume slider reaches the mixer")
else:
    bad("every volume slider reaches the mixer",
        "a slider that only sets a field moves a number nothing is listening to")


# ---- the player: one crouch truth, and a rig that says when it cannot animate --------------

controller = code("/Assets/CatchIfYouCan/Scripts/Player/PlayerController.cs")

# The camera drop must be a measured magnitude times the SHARED progress. MeasuredHeadDrop is
# the head's current drop, not a magnitude: applied unscaled it moved the view on a different
# curve from the capsule, ahead of it going down and behind it coming back.
if "MeasuredHeadDrop" in controller:
    bad("the crouch camera has one source of truth",
        "PlayerController reads MeasuredHeadDrop again; use cameraCrouchDrop * CrouchAmount01")
else:
    ok("the crouch camera has one source of truth")

if "_standingCameraHeight - cameraCrouchDrop * CrouchAmount01" in controller:
    ok("the camera returns to exactly standing height")
else:
    bad("the camera returns to exactly standing height",
        "the drop must be a multiple of CrouchAmount01 so zero crouch is zero drop")

animator_src = code("/Assets/CatchIfYouCan/Scripts/Player/PlayerVisualAnimator.cs")
if "ReportRigHealth()" in animator_src and "runtimeAnimatorController == null" in animator_src:
    ok("a rig that cannot animate says so")
else:
    bad("a rig that cannot animate says so",
        "a null controller holds the bind pose, which for Nathan is a T-pose")


# ---- built blind: a visual made before its definition arrived ------------------------------
#
# An item is created with AddComponent and told what it is on the NEXT line, and AddComponent
# runs Awake synchronously - so BuildCarried ran with a null definition, took the fallback
# profile, built the placeholder capsule, and its "already built" guard meant it never ran
# again. Every code-spawned item was a placeholder, the flashlight included.

held = code("/Assets/CatchIfYouCan/Scripts/Equipment/HeldEquipmentBase.cs")
if "public override void BindDefinition(" in held and "RebuildCarried()" in held:
    ok("a late definition rebuilds the visual")
else:
    bad("a late definition rebuilds the visual",
        "HeldEquipmentBase must rebuild when the profile arrives after Awake")

if "_dropCollider != null" in held.split("protected void RebuildCarried()")[-1][:600]:
    ok("a rebuild does not stack drop colliders")
else:
    bad("a rebuild does not stack drop colliders",
        "BuildDropCollider adds one unconditionally")

# ---- the doorway reacts before the world is ready -------------------------------------------
portal = code("/Assets/CatchIfYouCan/Scripts/Environment/LobbyPortal.cs")
routine = portal[portal.index("private IEnumerator OpenRoutine()"):] if "private IEnumerator OpenRoutine()" in portal else ""
if routine[:900].find("SetActive(true)") != -1 and routine[:900].find("PrepareAsync") == -1:
    ok("the opening starts before the world is prepared")
else:
    bad("the opening starts before the world is prepared",
        "gating the surface on PrepareAsync means a slow or failed prepare shows no portal")

if "Portal_Opening" in portal or "PortalSurface" in portal:
    ok("the portal owns a rendered surface")
else:
    bad("the portal owns a rendered surface")

# ---- the character's bound textures are imported at a usable size ---------------------------
import re as _re
tex = root + "/Assets/CatchIfYouCan/Art/Characters/Nathan/Textures/"
for name, floor in [("rp_nathan_animated_003_dif.jpg.meta", 4096),
                    ("rp_nathan_animated_003_norm.jpg.meta", 4096)]:
    meta = io.open(tex + name, encoding="utf-8").read()
    top = _re.search(r"^  maxTextureSize: (\d+)$", meta, _re.M)
    aniso = _re.search(r"^    aniso: (\d+)$", meta, _re.M)
    size = int(top.group(1)) if top else 0
    a = int(aniso.group(1)) if aniso else 0
    if size >= floor and a >= 4:
        ok("%s imports at %d with aniso %d" % (name.split('.')[0][-10:], size, a))
    else:
        bad("%s imports at a usable size" % name.split('.')[0][-10:],
            "found maxTextureSize=%d aniso=%d; the source is 8192 and the material binds it" % (size, a))

# ---- every model a visual profile names is really under Resources ---------------------------
# CLAUDE.md mistake 3: a Resources.Load path that has never existed misses silently for the
# life of the project. ApplyModel takes two of them, and the item that gets a path with no file
# behind it does not error at build time - it just holds the fallback capsule forever.
factory = code("/Assets/CatchIfYouCan/Scripts/Equipment/EquipmentDefinitionFactory.cs")
res = root + "/Assets/CatchIfYouCan/Resources/"
calls = _re.findall(r'ApplyModel\("([^"]+)",\s*"([^"]+)",\s*([0-9.]+)f,\s*new Vector3\(([^)]*)\)',
                    factory)
if not calls:
    bad("visual profiles name real models", "no ApplyModel call found at all")
for model, mat, length, axis in calls:
    name = model.split("/")[-1]
    if glob.glob(res + model + ".*"):
        ok("%s resolves to a model under Resources" % name)
    else:
        bad("%s resolves to a model under Resources" % name,
            "nothing matches Resources/%s.*" % model)
    if glob.glob(res + mat + ".mat"):
        ok("%s resolves to a material under Resources" % mat.split("/")[-1])
    else:
        bad("%s resolves to a material under Resources" % mat.split("/")[-1],
            "nothing matches Resources/%s.mat" % mat)
    # The axis is turned onto the carried root's +Y, which is where every emitter in the game
    # is hung. A zero vector silently becomes Vector3.up and lays the item across the hand.
    comps = [c.strip() for c in axis.split(",")]
    if len(comps) == 3 and any(c not in ("0f", "0") for c in comps):
        ok("%s declares a non-zero forward axis" % name)
    else:
        bad("%s declares a non-zero forward axis" % name, "found new Vector3(%s)" % axis)

# The art the factory names has to be real content or a proper LFS pointer to it. CI checks out
# without lfs:true, so a pointer here is expected and fine - what is NOT fine is a file that is
# neither, which is how a truncated or half-committed model imports as nothing at all.
for model, _mat, _l, _a in calls:
    hits = glob.glob(res + model + ".fbx")
    if not hits:
        continue
    name = model.split("/")[-1]
    head = io.open(hits[0], "rb").read(64)
    if head.startswith(b"version https://git-lfs"):
        ok("%s is an LFS pointer (content not needed here)" % name)
    elif head.startswith(b"Kaydara FBX"):
        ok("%s is a real FBX" % name)
    else:
        bad("%s is real content or an LFS pointer" % name,
            "the file is neither; it will import as an empty model")

# ---------------------------------------------------------------- die Generierung ist da

# generateWorld schaltet die Weltgenerierung ab, damit Charakter, Ausruestung und Portal ohne
# eine halb richtige Welt drumherum angesehen werden koennen. Der Schalter darf NIE bedeuten,
# dass die Generierung entfernt wurde: wieder anschalten muss denselben Ablauf und aus
# demselben Seed dasselbe Haus ergeben.
boot = io.open(root + "/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs",
               encoding="utf-8").read()
boot_code = "\n".join(l.split("//")[0] for l in boot.splitlines())

if "private bool generateWorld" in boot_code:
    ok("die Weltgenerierung hat einen Schalter")
else:
    bad("die Weltgenerierung hat einen Schalter")

if "BuildVan();" in boot_code and "GenerateHouse(_mission.Seed);" in boot_code:
    ok("die Weltgenerierung ist noch da, nur uebersprungen")
else:
    bad("die Weltgenerierung ist noch da, nur uebersprungen",
        "der Schalter darf nicht bedeuten, dass der Aufruf entfernt wurde")

if "BuildEmptyFloor" in boot_code:
    ok("ohne Generierung gibt es einen Boden statt eines Lochs")
else:
    bad("ohne Generierung gibt es einen Boden statt eines Lochs",
        "der Spieler faellt beim Betreten sonst durch die Welt")

# Der Boden gehoert ins Betreten, nicht ins Vorbereiten. Beim Vorbereiten gebaut haengt er
# als grosse leere Flaeche hinter dem Portal, wo der Spieler ihn weder braucht noch sehen
# soll - und vor dem Spawn, weil der Spawn per Strahl nach unten auf ihm einrastet.
seq = boot_code.split("private IEnumerator ActivateSequence")
if len(seq) > 1 and "BuildEmptyFloor();" in seq[1].split("SpawnPlayer();")[0]:
    ok("der Ersatzboden entsteht beim Betreten, vor dem Spawn")
else:
    bad("der Ersatzboden entsteht beim Betreten, vor dem Spawn",
        "beim Vorbereiten gebaut ist er Kulisse hinter dem Portal; nach dem Spawn faellt "
        "der Spieler durch")

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
