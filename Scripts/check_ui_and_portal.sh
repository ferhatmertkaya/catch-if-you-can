#!/usr/bin/env bash
#
# The menu palette, the input path and the lobby portal, enforced rather than trusted.
#
# Every check here corresponds to a bug this repository actually shipped:
#   - the brand green used as a border, a fill and a hover colour, so every screen was green;
#   - ColorBlock.fadeDuration left at Unity's 0.1 s default, so every button lagged a press;
#   - a Resources path in code with no file behind it (CLAUDE.md mistake 3);
#   - two screens each restoring the touch HUD on their way out, so it came back under a menu;
#   - START INVESTIGATION loading a scene directly, so the doorway was never asked to open.
#
# Needs nothing but a shell.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UI="$ROOT/Assets/CatchIfYouCan/Scripts/UI"
ENV="$ROOT/Assets/CatchIfYouCan/Scripts/Environment"
ART="$ROOT/Assets/CatchIfYouCan/Scripts/Art"
SCENE="$ROOT/Assets/CatchIfYouCan/Scenes/01_MainMenu.unity"
EDT="$ROOT/Assets/CatchIfYouCan/Editor"

PASS=0
FAIL=0

ok ()   { PASS=$((PASS+1)); printf '  ok    %s\n' "$1"; }
bad ()  { FAIL=$((FAIL+1)); printf '  FAIL  %s\n' "$1"; [ $# -gt 1 ] && printf '        %s\n' "$2"; }

# Code with every full-line comment removed. A guard that greps a whole file is a guard a
# doc comment can satisfy, and one that forbids a call will match the comment warning
# against it - this project has been bitten by both directions.
#
# Stripped ONCE PER FILE into a cache, not once per check. This ran sed and grep on every
# invocation, and with a hundred checks that is two hundred short-lived processes; under fork
# pressure some of them simply fail, and a check whose pipeline failed reports the code as
# broken. That is what made this guard fail on a different, random check each run while the
# project underneath it never changed.
CODE_CACHE_DIR="${TMPDIR:-/tmp}/ciyc_guard_cache_$$"
mkdir -p "$CODE_CACHE_DIR"

code () {
  local key
  key="$(printf '%s' "$1" | tr -c 'a-zA-Z0-9' '_')"
  local cached="$CODE_CACHE_DIR/$key"
  if [ ! -f "$cached" ]; then
    sed 's://.*::' "$1" | grep -v '^[[:space:]]*\*' > "$cached" || true
  fi
  cat "$cached"
}

echo "== UI and portal guard =="
echo

# ---- 1-6: the palette is black, white and grey -------------------------------------------

T="$UI/UITheme.cs"
if [ ! -f "$T" ]; then
  bad "UITheme.cs exists" "expected $T"
else
  grep -qE 'BackgroundDark[[:space:]]*=[[:space:]]*Hex\("#000000"\)' "$T" \
    && ok "background is pure black" \
    || bad "background is pure black" "UITheme.BackgroundDark must be #000000"

  grep -qE 'TextPrimary[[:space:]]*=[[:space:]]*Hex\("#FFFFFF"\)' "$T" \
    && ok "primary text is white" \
    || bad "primary text is white" "UITheme.TextPrimary must be #FFFFFF"

  if grep -E '^[[:space:]]*public static readonly Color Border[[:space:]]*=' "$T" \
       | grep -qiE '#(19D77B|57FF68|2E7A4B)'; then
    bad "the default border is neutral" "UITheme.Border must not be a brand green"
  else
    ok "the default border is neutral"
  fi

  grep -qE 'Overlay[[:space:]]*=[[:space:]]*new Color\(0f, 0f, 0f,' "$T" \
    && ok "the menu overlay is neutral black" \
    || bad "the menu overlay is neutral black" "UITheme.Overlay must not be tinted green"

  # The declaration, not the name: a comment saying "fadeDuration is zero" is not the fix.
  grep -qE '^[[:space:]]*colors\.fadeDuration[[:space:]]*=[[:space:]]*0f;' "$T" \
    && ok "buttons react on the same frame (fadeDuration = 0)" \
    || bad "buttons react on the same frame (fadeDuration = 0)" \
           "UITheme.ApplyButtonColors must assign colors.fadeDuration = 0f"

  if code "$T" | grep -qE 'normalColor[[:space:]]*=.*(Primary|Secondary)'; then
    bad "no button is filled with brand green" "ApplyButtonColors sets a green normalColor"
  else
    ok "no button is filled with brand green"
  fi
fi

# ---- 7: the factory does not fill a button with the accent -------------------------------

F="$UI/RuntimeUIFactory.cs"
if code "$F" | grep -qE 'img\.color[[:space:]]*=[[:space:]]*primary[[:space:]]*\?[[:space:]]*UITheme\.Secondary'; then
  bad "CreateButton uses a dark surface" "a primary button is filled with UITheme.Secondary"
else
  ok "CreateButton uses a dark surface"
fi

# ---- 8-10: the branded fonts are reachable at the paths the code names --------------------
#
# Parsed out of UITheme rather than written here, so renaming a path in code and forgetting
# to move the file fails this check instead of failing silently at runtime.

for CONST in TitleFontPath HeaderFontPath; do
  P="$(grep -E "public const string $CONST" "$T" 2>/dev/null | sed 's/.*"\(.*\)".*/\1/')"
  if [ -z "$P" ]; then
    bad "$CONST is declared" "no 'public const string $CONST' in UITheme.cs"
    continue
  fi
  ok "$CONST is declared ($P)"
  if ls "$ROOT/Assets/CatchIfYouCan/Resources/$P".* >/dev/null 2>&1; then
    ok "a font file exists at Resources/$P"
  else
    bad "a font file exists at Resources/$P" \
        "Resources.Load would return null and the interface would be unbranded"
  fi
done

# ---- 11-12: one owner suspends the gameplay HUD -------------------------------------------

G="$UI/MenuInputGate.cs"
if [ -f "$G" ] && grep -qE 'public static void Push\(string owner\)' "$G" \
                 && grep -qE 'public static void Pop\(string owner\)' "$G"; then
  ok "MenuInputGate declares Push and Pop"
else
  bad "MenuInputGate declares Push and Pop" "expected $G"
fi

OFFENDERS=""
for f in $(grep -rl "SetHudVisible(false)\|SetInputEnabled(false)" --include=*.cs \
             "$ROOT/Assets/CatchIfYouCan" 2>/dev/null); do
  case "$f" in
    */MenuInputGate.cs|*/PlayerSpawner.cs) ;;
    *) if code "$f" | grep -qE 'Set(HudVisible|InputEnabled)\(false\)'; then
         OFFENDERS="$OFFENDERS $f"
       fi ;;
  esac
done
if [ -n "$OFFENDERS" ]; then
  bad "only MenuInputGate suspends input and the HUD" "also suspended in:$OFFENDERS"
else
  ok "only MenuInputGate suspends input and the HUD"
fi

# ---- 13-16: START INVESTIGATION opens the doorway -----------------------------------------

M="$UI/MissionSelectUI.cs"
code "$M" | grep -qE 'LobbyPortal\.TryOpenForMission\(' \
  && ok "START INVESTIGATION asks the portal to open" \
  || bad "START INVESTIGATION asks the portal to open" \
         "MissionSelectUI must reach LobbyPortal, not SceneLoader, in the lobby"

code "$M" | grep -qE 'LobbyPortal\.Instance != null' \
  && ok "the direct scene load is reached only without a portal" \
  || bad "the direct scene load is reached only without a portal" \
         "the LoadInvestigation path must be behind a portal-absent test"

P="$ENV/LobbyPortal.cs"
MISSING=""
for S in Inactive MissionSelected Opening Open Entering Loading Closed; do
  grep -qE "^[[:space:]]*$S,?[[:space:]]*$" "$P" 2>/dev/null || MISSING="$MISSING $S"
done
if [ -n "$MISSING" ]; then
  bad "LobbyPortalState carries all seven states" "missing:$MISSING"
else
  ok "LobbyPortalState carries all seven states"
fi

# The refusal itself, not a <see cref> to it.
grep -qE 'CIYCLog\.Error\(LogTag \+ "Mission selected but portal controller missing' "$P" 2>/dev/null \
  && ok "a missing portal controller is reported, not silent" \
  || bad "a missing portal controller is reported, not silent" \
         "LobbyPortal.TryOpenForMission must log the error, not return quietly"

# ---- 17: the portal is actually in the lobby ----------------------------------------------

GUID="$(grep -E '^guid:' "$P.meta" 2>/dev/null | awk '{print $2}')"
if [ -n "$GUID" ] && grep -q "$GUID" "$SCENE" 2>/dev/null; then
  ok "the lobby scene carries a LobbyPortal"
else
  bad "the lobby scene carries a LobbyPortal" \
      "nothing in 01_MainMenu.unity references LobbyPortal, so no doorway can open"
fi

# ---- 18-20: nothing sweeps the scene every frame -------------------------------------------

for f in "$P" "$G" "$ART/PortalSurface.cs"; do
  N="$(basename "$f")"
  if code "$f" | grep -qE '(GameObject\.Find|FindAnyObjectByType|FindObjectsByType)'; then
    bad "$N does not search the scene" "a Find call in a per-frame path is a per-frame sweep"
  else
    ok "$N does not search the scene"
  fi
done


# ---- V7: exactly one mobile input path --------------------------------------------------
#
# RuntimeUIFactory used to build a second MobileInputController and a second MoveJoystick.
# Which of the two ended up bound to the controller depended on which ran first, so the
# orphan stayed on screen stealing touches and driving nothing - and it could not be
# suspended, because MenuInputGate hides the player's TouchHUD and that joystick was not
# part of it.

if code "$F" | grep -qE 'AddComponent<(MobileInputController|VirtualJoystick)>'; then
  bad "RuntimeUIFactory builds no movement input" \
      "the HUD screen is building a second controller or joystick again"
else
  ok "RuntimeUIFactory builds no movement input"
fi

CREATORS=""
for f in $(grep -rl "AddComponent<MobileInputController>" --include=*.cs \
             "$ROOT/Assets/CatchIfYouCan" 2>/dev/null); do
  case "$f" in
    */PlayerFactory.cs) ;;
    *) CREATORS="$CREATORS $f" ;;
  esac
done
if [ -n "$CREATORS" ]; then
  bad "only PlayerFactory creates the MobileInputController" "also created in:$CREATORS"
else
  ok "only PlayerFactory creates the MobileInputController"
fi

STICKS=""
for f in $(grep -rl "AddComponent<VirtualJoystick>" --include=*.cs \
             "$ROOT/Assets/CatchIfYouCan" 2>/dev/null); do
  case "$f" in
    */TouchHudFactory.cs) ;;
    *) STICKS="$STICKS $f" ;;
  esac
done
if [ -n "$STICKS" ]; then
  bad "only TouchHudFactory builds the movement joystick" "also built in:$STICKS"
else
  ok "only TouchHudFactory builds the movement joystick"
fi

# ---- V7: one transition fade -------------------------------------------------------------

TF="$UI/TransitionFade.cs"
if [ -f "$TF" ] && grep -qE 'public const int SortingOrder = 500;' "$TF"; then
  ok "TransitionFade owns the transition overlay"
else
  bad "TransitionFade owns the transition overlay" "expected $TF"
fi

FADES=""
for f in $(grep -rl "sortingOrder = 500" --include=*.cs "$ROOT/Assets/CatchIfYouCan" 2>/dev/null); do
  case "$f" in
    */TransitionFade.cs) ;;
    *) FADES="$FADES $f" ;;
  esac
done
if [ -n "$FADES" ]; then
  bad "there is one overlay at order 500" "a second one is built in:$FADES"
else
  ok "there is one overlay at order 500"
fi

# ---- V7: the portal shows the real mission world ------------------------------------------

P7="$ENV/LobbyPortal.cs"
if code "$P7" | grep -qE 'ReferenceApartment'; then
  bad "the portal destination is the mission world" \
      "LobbyPortal still builds a ReferenceApartment as its destination"
else
  ok "the portal destination is the mission world"
fi

code "$P7" | grep -qE 'MissionWorldLoader\.PrepareAsync\(' \
  && ok "the portal prepares the mission world" \
  || bad "the portal prepares the mission world" "LobbyPortal must call MissionWorldLoader"

W="$ROOT/Assets/CatchIfYouCan/Scripts/Missions/MissionWorldLoader.cs"
code "$W" | grep -qE 'LoadSceneAsync\(sceneName, LoadSceneMode\.Additive\)' \
  && ok "the mission world is loaded additively" \
  || bad "the mission world is loaded additively" "expected an additive load in MissionWorldLoader"

# One seed. The world loader must never roll its own - the seed belongs to MissionRuntime,
# rolled once in MissionManager.StartInvestigation before the portal opened.
if code "$W" | grep -qE 'SessionSeedSource\.Next\(|SeedManager\.SetSeed\('; then
  bad "the mission world rolls no seed of its own" \
      "MissionWorldLoader must read MissionRuntime.Seed, never roll one"
else
  ok "the mission world rolls no seed of its own"
fi

B="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"
MISSINGMODE=""
for M in Immediate Deferred; do
  grep -qE "^[[:space:]]*$M,?[[:space:]]*$" "$B" 2>/dev/null || MISSINGMODE="$MISSINGMODE $M"
done
if [ -n "$MISSINGMODE" ]; then
  bad "InvestigationBootstrap has both start modes" "missing:$MISSINGMODE"
else
  ok "InvestigationBootstrap has both start modes"
fi

# A prepared world is scenery. PrepareWorld builds the van and the house and nothing that
# would make it live - the player, the ghost, the objectives and the audio all belong to
# ActivateSequence, on the far side of the threshold.
if sed -n '/private bool PrepareWorld()/,/^        }$/p' "$B" \
     | grep -qE '(SpawnPlayer|SpawnGhost|WireSystems|InstallAudio|PlayIntro)\('; then
  bad "a prepared world is not a running one" \
      "PrepareWorld starts gameplay; the ghost would hunt while the player is in the lobby"
else
  ok "a prepared world is not a running one"
fi

code "$W" | grep -qE 'AudioListener' \
  && ok "the loaded world's audio listener is silenced" \
  || bad "the loaded world's audio listener is silenced" \
         "two enabled AudioListeners is a warning nobody reads"

# ---- V7: the loadout reaches the player's hands --------------------------------------------

code "$B" | grep -qE 'MissionEquipmentInstaller\.InstallLoadout\(' \
  && ok "the mission installs its loadout" \
  || bad "the mission installs its loadout" \
         "a loadout that is only data leaves every item but the torch unreachable"

E="$ROOT/Assets/CatchIfYouCan/Scripts/Equipment/EquipmentManager.cs"
MISSINGKIT=""
for K in Flashlight EmfDetector UvLight Thermometer; do
  sed -n '/public void GiveStarterLoadout/,/^        }$/p' "$E" \
    | grep -q "EquipmentIds.$K" || MISSINGKIT="$MISSINGKIT $K"
done
if [ -n "$MISSINGKIT" ]; then
  bad "the starter loadout carries the four slice items" "missing:$MISSINGKIT"
else
  ok "the starter loadout carries the four slice items"
fi

# ---- V7.1: the torch does not eat an investigation slot -----------------------------------
#
# Four tools, three slots. The torch used to take slot 0, so the third investigation device
# had nowhere to go and was dropped on the floor of a log line.

INV="$ROOT/Assets/CatchIfYouCan/Scripts/Player/PlayerInventory.cs"
grep -qE 'public const int SlotCount = 3;' "$INV" \
  && ok "there are still three investigation slots" \
  || bad "there are still three investigation slots" \
         "SlotCount is what the HUD selector, the pickup rules and replication count on"

grep -qE 'public const int TorchSlotIndex = SlotCount;' "$INV" \
  && ok "the torch has a dedicated place outside the three" \
  || bad "the torch has a dedicated place outside the three" \
         "expected PlayerInventory.TorchSlotIndex"

# Ownership is claimed in exactly one file, dedicated place included.
CLAIMERS=""
for f in $(grep -rl "TryClaim(" --include=*.cs "$ROOT/Assets/CatchIfYouCan" 2>/dev/null); do
  case "$f" in
    */PlayerInventory.cs|*/EquipmentBase.cs|*/HeldEquipmentBase.cs) ;;
    *) CLAIMERS="$CLAIMERS $f" ;;
  esac
done
if [ -n "$CLAIMERS" ]; then
  bad "equipment ownership is claimed in one place" "also claimed in:$CLAIMERS"
else
  ok "equipment ownership is claimed in one place"
fi

# The installer must look at the torch's place too, or it hands out a second torch.
code "$ROOT/Assets/CatchIfYouCan/Scripts/Equipment/MissionEquipmentInstaller.cs" \
  | grep -qE 'PlayerInventory\.SelectableSlotCount' \
  && ok "the installer sees the torch's dedicated place" \
  || bad "the installer sees the torch's dedicated place" \
         "it would not recognise the torch already in the player's hand"

# ---- V7.1: nothing is silently discarded ---------------------------------------------------

MI="$ROOT/Assets/CatchIfYouCan/Scripts/Equipment/MissionEquipmentInstaller.cs"
code "$MI" | grep -qE 'CIYCLog\.(Info|Warn|Error)\(LogTag \+ "Loadout installed' \
  && ok "an item that does not fit is named, not dropped" \
  || bad "an item that does not fit is named, not dropped" \
         "the installer must report what it could not carry"

# ---- V7.1: the return to the lobby does not replay the cinematic ---------------------------

MM="$UI/MainMenuModeController.cs"
MISSINGENTRY=""
for M in Cinematic DirectLobby; do
  grep -qE "^[[:space:]]*$M,?[[:space:]]*$" "$MM" 2>/dev/null || MISSINGENTRY="$MISSINGENTRY $M"
done
if [ -n "$MISSINGENTRY" ]; then
  bad "the menu has both entry modes" "missing:$MISSINGENTRY"
else
  ok "the menu has both entry modes"
fi

grep -qE 'PendingEntryMode = MainMenuEntryMode\.Cinematic;' "$MM" \
  && ok "the entry mode resets itself after it is read" \
  || bad "the entry mode resets itself after it is read" \
         "a direct entry that failed would leave a cold boot skipping its own intro"

code "$UI/MissionResultUI.cs" | grep -qE 'PendingEntryMode = MainMenuEntryMode\.DirectLobby' \
  && ok "finishing a mission returns to the lobby directly" \
  || bad "finishing a mission returns to the lobby directly" \
         "MissionResultUI must state the intent before loading the menu scene"

# The direct route must not run the cinematic's music, phone or tap-to-start sequence.
if sed -n '/private IEnumerator DirectRoutine()/,/^        }$/p' "$MM" \
     | grep -qE '(FadeOutMenuMusic|FadeOutCinematicSources|TransitionRoutine)\('; then
  bad "the direct route replays no cinematic" "DirectRoutine runs the cinematic sequence"
else
  ok "the direct route replays no cinematic"
fi

# ---- V7.1: one world, generated once --------------------------------------------------------

B71="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"
code "$B71" | grep -qE '_generatedFor' \
  && ok "a mission cannot be generated twice" \
  || bad "a mission cannot be generated twice" \
         "a second generation means the preview and the played world only happen to match"

# ---- V7.1: the portal camera can never become the player's ----------------------------------

PS="$ART/PortalSurface.cs"
if code "$PS" | grep -qE 'AddComponent<AudioListener>|tag = "MainCamera"'; then
  bad "the portal camera is not a gameplay camera" \
      "it must carry no AudioListener and never claim the MainCamera tag"
else
  ok "the portal camera is not a gameplay camera"
fi

code "$PS" | grep -qE 'go\.tag = "Untagged"' \
  && ok "the portal camera is explicitly untagged" \
  || bad "the portal camera is explicitly untagged" \
         "an untagged second camera is what stops Camera.main finding it"

code "$PS" | grep -qE 'LocalPlayerService\.Register' \
  && bad "the portal camera never registers as the local view" \
     "PortalSurface must not touch LocalPlayerService registration" \
  || ok "the portal camera never registers as the local view"

# ---- V9: the production portal pass ---------------------------------------------------------
#
# The portal was a glowing RECTANGLE: its rim came from min(uv, 1-uv), which is a box, and at
# full opacity the whole quad including its corners showed the far room. These checks are the
# architectural invariants of the rebuild, not its visual taste.

SHADER="$ROOT/Assets/CatchIfYouCan/Shaders/Portal.shader"
MAT="$ROOT/Assets/CatchIfYouCan/Resources/Materials/MAT_Portal.mat"
FX="$ART/PortalEffects.cs"
STYLE="$ART/PortalStyle.cs"

for f in "$SHADER" "$MAT" "$FX" "$STYLE"; do
  [ -f "$f" ] || bad "$(basename "$f") exists" "the portal cannot be built without it"
done

# The silhouette is a TORN OVAL: a normalised radial field, exactly 1.0 on the boundary, with
# its edge chewed away by noise.
#
# This check used to demand a signed BOX field and reject an ellipse by name. That was the right
# rule against the bug of its day - a glowing rectangular frame - but it had hardened one
# particular answer into the contract, and the shape is the owner's call, not the guard's. What
# is actually invariant is that the breach comes from a signed field whose edge noise can move,
# so the three masks below it stay derivable from one number; the naked rectangle stays forbidden
# on its own line below.
if grep -qE 'float2 e = c / fit;' "$SHADER" && grep -qE 'float oval = length\(e\);' "$SHADER"; then
  ok "the breach is a normalised radial field"
else
  bad "the breach is a normalised radial field" \
      "the rim, the view and the spill all compare against 1.0 and need a field that is 1.0 " \
      "on the boundary"
fi

if grep -qE 'min\(IN\.uv, *1\.0 *- *IN\.uv\)' "$SHADER"; then
  bad "the naked rectangular field is gone" \
      "min(uv, 1-uv) is a clean frame; the breach must be torn"
else
  ok "the naked rectangular field is gone"
fi

# The edge has to be broken by noise, or it is a neat cut rather than something that came
# through the wall.
if grep -q '_TearAmount' "$SHADER" && grep -qE 'ragged *\* *_TearAmount' "$SHADER"; then
  ok "the breach edge is torn by noise"
else
  bad "the breach edge is torn by noise" "a straight-edged hole is a doorway, not a breach"
fi

# CLOSED MEANS NO HOLE. A collapsed box still measures zero distance at its own centre, so
# without an explicit gate one pixel burns on a wall that is supposed to be whole.
if grep -qE 'float +gate *= *smoothstep\(0\.0, *0\.02, *open\)' "$SHADER" &&
   grep -qE 'alpha *=.*\* *gate' "$SHADER"; then
  ok "a closed portal draws nothing at all"
else
  bad "a closed portal draws nothing at all" \
      "at _Open = 0 the wall must be whole, with no lit pixel anywhere"
fi

# Two noise layers on DIFFERENT frequencies. Identical frequencies read as one repeating
# pattern, which is the thing procedural energy is supposed to avoid.
if grep -q '_NoiseScale' "$SHADER" && grep -q '_SecondaryNoiseScale' "$SHADER"; then
  ok "the energy uses two independent noise layers"
else
  bad "the energy uses two independent noise layers" \
      "one scrolling layer is a texture on a ring"
fi

# Colour is data. A hard-coded blue or green in the shader body would mean the portal cannot
# be re-tinted without an edit, which is the one thing the brief was explicit about.
for prop in _CoreColor _EnergyColor _OuterColor _CoreIntensity _EnergyIntensity _RimWidth \
            _RimSoftness _NoiseStrength _NoiseSpeed _SecondaryNoiseSpeed _DistortionStrength \
            _RotationSpeed _PulseSpeed _PulseStrength _Opacity; do
  grep -q "$prop" "$SHADER" || bad "the shader exposes $prop" "artistic control must be serialized"
done
ok "every named energy control is a shader property"

# URP only. A Built-in or HDRP shader reached from here draws solid magenta under URP, which
# is CLAUDE.md mistake 2.
if grep -qE 'RenderPipeline"="UniversalPipeline' "$SHADER"; then
  ok "the portal shader declares the universal pipeline"
else
  bad "the portal shader declares the universal pipeline" \
      "without the tag URP will not pick this SubShader"
fi

if grep -qE 'Shader\.Find\("(Standard|Particles/|HDRenderPipeline|Hidden/)' "$SHADER" "$FX" "$ART/PortalSurface.cs" "$ENV/LobbyPortal.cs" 2>/dev/null; then
  bad "no built-in or HDRP shader is reached for" "a built-in shader under URP is magenta"
else
  ok "no built-in or HDRP shader is reached for"
fi

# The material carries the authored defaults AND keeps the shader out of the stripper.
if grep -q '_EnergyColor' "$MAT" && grep -q '_CoreColor' "$MAT"; then
  ok "MAT_Portal carries the authored energy colours"
else
  bad "MAT_Portal carries the authored energy colours" \
      "an empty material cannot be tuned and teaches the stripper nothing"
fi

# Every shader property really exists in the material, and vice versa. A name that only
# exists on one side is CLAUDE.md mistake 3 in shader form: it fails silently forever.
MISMATCH="$(python3 -c '
import re, sys
sh = open(sys.argv[1]).read()
block = sh[sh.index("Properties"):sh.index("SubShader")]
props = set(re.findall(r"^\s*(?:\[[^\]]*\]\s*)?(_\w+)\s*\(", block, re.M))
mat = set(re.findall(r"^\s*-\s*(_\w+):", open(sys.argv[2]).read(), re.M))
print(" ".join(sorted(props ^ mat)))
' "$SHADER" "$MAT")"
if [ -z "$MISMATCH" ]; then
  ok "shader and material agree on every property name"
else
  bad "shader and material agree on every property name" "only on one side:$MISMATCH"
fi

# Two fades, not one. The destination has to be able to be black behind a burning rim while
# the world is still being prepared - that is the whole "react on the press frame" promise.
code "$ART/PortalSurface.cs" | grep -qE 'public void SetViewOpacity\(' \
  && ok "the destination fades independently of the opening" \
  || bad "the destination fades independently of the opening" \
         "one opacity means the rim cannot burn over an unready world"

# Allocation policy. A RenderTexture or a Material created inside LateUpdate is a per-frame
# leak, and this file runs LateUpdate on every frame the portal is visible.
PERFRAME="$(code "$ART/PortalSurface.cs" | sed -n '/private void LateUpdate/,/^        }/p')"
if printf '%s' "$PERFRAME" | grep -qE 'new RenderTexture|new Material'; then
  bad "no buffer or material is allocated per frame" \
      "LateUpdate must reuse the texture and the material it already has"
else
  ok "no buffer or material is allocated per frame"
fi

if printf '%s' "$PERFRAME" | grep -qE 'FindObjectOfType|FindObjectsOfType|GameObject\.Find'; then
  bad "the portal does not search the scene per frame" "cache the camera, do not find it"
else
  ok "the portal does not search the scene per frame"
fi

FXUPDATE="$(code "$FX" | sed -n '/private void Update/,/^        }/p')"
if printf '%s' "$FXUPDATE" | grep -qE 'new Material|new GameObject|AddComponent'; then
  bad "the portal effects allocate nothing per frame" \
      "particle systems are configured once and driven by their emission rate"
else
  ok "the portal effects allocate nothing per frame"
fi

# The portal camera stays off when it cannot be seen. Without this the far world is rendered
# a second time every frame the player is anywhere in the lobby.
code "$ART/PortalSurface.cs" | grep -qE 'TestPlanesAABB' \
  && ok "the portal camera is culled when the opening is off screen" \
  || bad "the portal camera is culled when the opening is off screen" \
         "a second full render must not run while the opening is behind the player"

code "$ART/PortalSurface.cs" | grep -qE '_style\.renderDistance' \
  && ok "the portal camera is distance-gated" \
  || bad "the portal camera is distance-gated" "render distance must be honoured"

# One system, scaled - not three portals. The quality fraction is the project's shared
# convention; a private tier enum here would be a parallel quality system.
code "$STYLE" | grep -qE 'QualitySettings\.GetQualityLevel\(\)' \
  && ok "portal quality derives from the project's own quality level" \
  || bad "portal quality derives from the project's own quality level" \
         "do not invent a second tier system for the portal"

if code "$STYLE" | grep -qE 'enum +QualityTier|enum +PortalQuality'; then
  bad "the portal declares no parallel quality tiers" \
      "PLATFORM_QUALITY_TIERS.md: one system, scalable, not three portals"
else
  ok "the portal declares no parallel quality tiers"
fi

# Failure is seen, not merely logged, and it leaves nothing armed behind it.
code "$ENV/LobbyPortal.cs" | grep -qE 'private IEnumerator DestabiliseRoutine\(\)' \
  && ok "a failed preparation visibly destabilises the portal" \
  || bad "a failed preparation visibly destabilises the portal" \
         "hiding the surface silently is indistinguishable from a button that did nothing"

DESTAB="$(code "$ENV/LobbyPortal.cs" | sed -n '/private IEnumerator DestabiliseRoutine/,/^        }$/p')"
if printf '%s' "$DESTAB" | grep -qE '_threshold\.enabled = false' &&
   printf '%s' "$DESTAB" | grep -qE 'MenuInputGate\.Pop'; then
  ok "a collapsed portal leaves no live trigger and no held input"
else
  bad "a collapsed portal leaves no live trigger and no held input" \
      "a dead portal with an armed threshold or a held gate strands the player"
fi

# Exactly one portal controller and one surface type in the production path.
CONTROLLERS="$(grep -rlE 'class .*: *MonoBehaviour' "$ENV" "$ART" 2>/dev/null \
               | xargs grep -lE 'RenderTexture|targetTexture' 2>/dev/null \
               | xargs -r -n1 basename | sort -u | tr '\n' ' ')"
case "$CONTROLLERS" in
  *PortalSurface.cs*) ok "one portal surface owns the render texture ($CONTROLLERS)" ;;
  *) bad "one portal surface owns the render texture" "found: $CONTROLLERS" ;;
esac

# The effects emit on the EDGE of the breach, and the emitter has to be the SHAPE of the breach.
# It was BoxEdge, correctly, while the breach was a torn rectangle; the breach is an oval now, so
# a rectangular emitter puts sparks in the corners where there is no edge and thins them along
# the sides where the wall is actually broken. Circle with zero radius thickness is the contour;
# a non-zero thickness fills the disc and fires particles through the middle of the view.
if code "$FX" | grep -qE 'ParticleSystemShapeType\.Circle' &&
   code "$FX" | grep -qE 'shape\.radiusThickness = 0f' &&
   code "$FX" | grep -qE 'shape\.scale'; then
  ok "particles emit on the breach edge, not through the view"
else
  bad "particles emit on the breach edge, not through the view" \
      "the emitter must be the breach's shape: an oval contour, not a rectangle and not a disc"
fi

for system in Sparks EnergyStreaks AmbientWisps; do
  code "$FX" | grep -q "\"$system\"" \
    && ok "the portal builds $system" \
    || bad "the portal builds $system" "the brief names sparks, streaks and wisps"
done

code "$FX" | grep -qE 'trails\.enabled = true' \
  && ok "the streaks carry trails" \
  || bad "the streaks carry trails" "a discharge without a trail is a dot"

code "$FX" | grep -qE 'GradientAlphaKey\(0f, 1f\)' \
  && ok "particles fade on alpha rather than popping at zero size" \
  || bad "particles fade on alpha rather than popping at zero size"

# One light. The lobby already pays for a mirror and a portal camera.
LIGHTS="$(code "$FX" | grep -cE 'AddComponent<Light>')"
if [ "$LIGHTS" = "1" ]; then
  ok "the portal adds exactly one real-time light"
else
  bad "the portal adds exactly one real-time light" "found $LIGHTS"
fi

if code "$FX" | grep -qE 'LightShadows\.None'; then
  ok "the portal light casts no shadows"
else
  bad "the portal light casts no shadows" "a shadowed doorway light is the frame's most expensive object"
fi

# No second gameplay camera anywhere in the portal path.
CAMERAS="$(code "$FX" | grep -cE 'AddComponent<Camera>')"
if [ "$CAMERAS" = "0" ]; then
  ok "the effects add no camera of their own"
else
  bad "the effects add no camera of their own" "one portal camera, owned by PortalSurface"
fi

# The diagnostic exists, reports the pieces, and is not per frame.
code "$ENV/LobbyPortal.cs" | grep -qE 'private void ReportOpening\(\)' \
  && ok "the portal reports its state once when it opens" \
  || bad "the portal reports its state once when it opens"

# A log at the loop's OWN statement depth runs every frame; one nested deeper sits inside a
# condition. Indentation is the test rather than the mere presence of a log call, because the
# bind line genuinely does fire once - latched by `bound` - and forbidding it outright would
# push a useful diagnostic out of the only place it can be written.
PERFRAME_LOG="$(python3 -c '
import sys
src = open(sys.argv[1]).read().splitlines()
start = next((i for i, l in enumerate(src) if "while (t < openDuration" in l), None)
if start is None:
    print("NO-LOOP"); raise SystemExit
depth = len(src[start]) - len(src[start].lstrip())
body = depth + 4
bad = []
for line in src[start + 1:]:
    stripped = line.strip()
    if stripped == "}" and len(line) - len(line.lstrip()) == depth:
        break
    if "CIYCLog." in stripped or "Debug.Log" in stripped:
        if len(line) - len(line.lstrip()) <= body:
            bad.append(stripped[:60])
print(" | ".join(bad))
' "$ENV/LobbyPortal.cs")"
if [ -z "$PERFRAME_LOG" ]; then
  ok "the portal does not log every frame"
elif [ "$PERFRAME_LOG" = "NO-LOOP" ]; then
  bad "the portal does not log every frame" "the opening loop could not be found to check"
else
  bad "the portal does not log every frame" "unconditional in the loop: $PERFRAME_LOG"
fi

# ---- V9.1: entry is the player's, never a timer ---------------------------------------------
#
# The observed bug: press START INVESTIGATION, watch the portal form, and a couple of seconds
# later be teleported into a broken investigation without having walked anywhere. The cause was
# a failure fallback that called SceneLoader.LoadInvestigation(), reachable with no crossing at
# all. These checks are that class of bug made unrepeatable.

LP="$ENV/LobbyPortal.cs"

if code "$LP" | grep -qE 'LoadInvestigation\(|SceneLoader\.Instance'; then
  bad "the portal never loads the investigation by itself" \
      "any scene load reachable without a crossing is an automatic teleport"
else
  ok "the portal never loads the investigation by itself"
fi

# Entry commits from exactly one place, and that place is the crossing test.
COMMITS="$(code "$LP" | grep -cE 'BeginInvestigation\(\);')"
if [ "$COMMITS" = "1" ]; then
  ok "entry commits from exactly one call site"
else
  bad "entry commits from exactly one call site" "found $COMMITS"
fi

if code "$LP" | grep -qE 'private void LateUpdate\(\)' &&
   code "$LP" | grep -qE 'Vector3\.Dot\(probe - planePoint, planeNormal\)'; then
  ok "entry is decided by a plane-side crossing test"
else
  bad "entry is decided by a plane-side crossing test" \
      "a trigger volume cannot tell walking through from standing near"
fi

# The sign must actually change. Without this the test is just "is on the far side", which a
# player standing beyond the doorway satisfies forever.
if code "$LP" | grep -qE 'if \(previous <= 0f \|\| side > 0f\)'; then
  ok "the crossing needs the side sign to change"
else
  bad "the crossing needs the side sign to change" \
      "testing only the current side re-fires every frame past the plane"
fi

# Walking beside the frame is not entry.
if code "$LP" | grep -qE 'apertureTolerance' && code "$LP" | grep -qE 'Mathf\.Abs\(across\)'; then
  ok "the crossing is bounded by the aperture"
else
  bad "the crossing is bounded by the aperture" "brushing past the jamb would count"
fi

# No trigger component survives that could commit entry behind the test's back.
if code "$LP" | grep -qE 'OnTriggerEnter|class LobbyPortalThreshold'; then
  bad "no trigger volume can commit entry" \
      "OnTriggerEnter fires on touch, which is the bug this replaced"
else
  ok "no trigger volume can commit entry"
fi

# Entry requires a finished destination, checked at the crossing rather than assumed.
if code "$LP" | grep -qE 'CanBeEntered' && code "$LP" | grep -qE 'MissionWorldLoader\.WorldReady'; then
  ok "entry requires a prepared destination"
else
  bad "entry requires a prepared destination" "crossing into a half-built world is a race"
fi

# Open must mean "waiting for the player", so the state machine needs somewhere else to sit
# while the world is still building.
if grep -qE '^\s+PreparingDestination,' "$LP"; then
  ok "a charging portal has its own state"
else
  bad "a charging portal has its own state" \
      "without it Open covers both 'ready' and 'still building'"
fi

# A refused crossing gives the controls back rather than stranding the player behind a gate.
REFUSE="$(code "$LP" | sed -n '/private void BeginInvestigation/,/^        }$/p')"
if printf '%s' "$REFUSE" | grep -qE 'MenuInputGate\.Pop'; then
  ok "a refused crossing returns the player's controls"
else
  bad "a refused crossing returns the player's controls" \
      "pushing the gate then bailing out locks the player out of their own game"
fi

# ---- V9.1: the handover cannot leave the screen covered -------------------------------------
BOOT="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"

if code "$BOOT" | grep -qE 'fadeOverlay != null && fadeOverlay\.alpha > 0'; then
  ok "the intro overlay is cleared whatever happens"
else
  bad "the intro overlay is cleared whatever happens" \
      "PrepareWorld sets it opaque; a skipped fade leaves a black sheet over a working world"
fi

if code "$BOOT" | grep -qE 'private void ReportEntry\(\)'; then
  ok "entry reports its own state once"
else
  bad "entry reports its own state once" "a black screen with no report says nothing"
fi

# ---- V9.1: the first-person hand ------------------------------------------------------------
PBM="$ROOT/Assets/CatchIfYouCan/Scripts/Player/PlayerBodyMotion.cs"
HELD="$ROOT/Assets/CatchIfYouCan/Scripts/Equipment/HeldEquipmentBase.cs"
RIGB="$ROOT/Assets/CatchIfYouCan/Scripts/Player/PlayerRigBuilder.cs"

# The hand bone suffix has to be one that Nathan's bones actually end with. The old value was
# "_hand_r" and the bones are named "hand_r", so it matched nothing and every held item fell
# back to the anchor - which is why the torch was never in the hand.
SUFFIX="$(grep -oE 'handBoneSuffix = "[^"]*"' "$HELD" | head -1 | sed 's/.*"\(.*\)"/\1/')"
case "$SUFFIX" in
  _*) bad "the hand bone suffix can match a real bone" \
          "'$SUFFIX' starts with an underscore; Nathan's bones are hand_l / hand_r" ;;
  hand_l|hand_r) ok "the hand bone suffix can match a real bone ($SUFFIX)" ;;
  *) bad "the hand bone suffix can match a real bone" "found '$SUFFIX'" ;;
esac

# One side, agreed by all three. A hand target on one side and a fallback anchor on the other
# puts the item in the opposite hand from the arm holding it.
HT="$(grep -oE 'handTargetLocalPosition = new Vector3\(-?[0-9.]+f' "$PBM" | grep -oE '\(-?[0-9.]+' | tr -d '(')"
EH="$(grep -oE 'elbowHintLocalPosition = new Vector3\(-?[0-9.]+f' "$PBM" | grep -oE '\(-?[0-9.]+' | tr -d '(')"
# The elbow must end up BELOW the hand, which in root space means its height is under eye
# level. An elbow at or above the hand is the boxer-guard silhouette the brief rules out.
EHY="$(grep -oE 'elbowHintLocalPosition = new Vector3\([^)]*\)' "$PBM" | grep -oE ', *[0-9.]+f *,' | tr -d ' ,f')"
if [ -n "$EHY" ] && awk "BEGIN{exit !($EHY < 1.68)}"; then
  ok "the elbow hint sits below eye level (y=$EHY)"
else
  bad "the elbow hint sits below eye level" \
      "y=$EHY is at or above the 1.68 m eye line; the elbow would flare to shoulder height"
fi
HA="$(grep -oE 'handAnchor.transform.localPosition = new Vector3\(-?[0-9.]+f' "$RIGB" | grep -oE '\(-?[0-9.]+' | tr -d '(')"
# One side, whichever it is. The three must AGREE: a hand target on one side with a fallback
# anchor on the other puts the item in the opposite hand from the arm holding it. The bone
# suffix has to name that same side, or the item is parented to one hand while the IK drags
# the other - which is exactly the state this project shipped in.
side_of () { case "$1" in -*) echo L ;; *) echo R ;; esac; }
S_HT="$(side_of "$HT")"; S_EH="$(side_of "$EH")"; S_HA="$(side_of "$HA")"
case "$SUFFIX" in hand_l) S_BONE=L ;; hand_r) S_BONE=R ;; *) S_BONE="?" ;; esac

if [ "$S_HT" = "$S_EH" ] && [ "$S_HT" = "$S_HA" ] && [ "$S_HT" = "$S_BONE" ]; then
  ok "hand target, elbow hint, anchor and bone are all on one side ($S_HT: $HT / $EH / $HA / $SUFFIX)"
else
  bad "hand target, elbow hint, anchor and bone are all on one side" \
      "target=$S_HT hint=$S_EH anchor=$S_HA bone=$S_BONE - a split side puts the item in the other hand"
fi

# The fist must not sit inside the near clip plane. 0.06 m in front of the camera pivot is what
# folded the arm against the face and filled the corner of the screen with skin.
HTZ="$(grep -oE 'handTargetLocalPosition = new Vector3\([^)]*\)' "$PBM" | grep -oE '[0-9.]+f\)' | tr -d 'f)')"
if [ -n "$HTZ" ] && awk "BEGIN{exit !($HTZ >= 0.2)}"; then
  ok "the fist sits clear of the camera (z=$HTZ)"
else
  bad "the fist sits clear of the camera" \
      "z=$HTZ is inside the near clip plane; the arm folds into the face to reach it"
fi

# ---- V9.2: the probe room is a diagnostic, never a destination -------------------------------
PROBE="$ART/PortalProbeRoom.cs"
if [ -f "$PROBE" ]; then
  # It must sit far outside the playable world, or it becomes scenery somebody can reach.
  if code "$PROBE" | grep -qE 'new Vector3\(0f, -[0-9]{3}'; then
    ok "the probe room is built far outside the playable world"
  else
    bad "the probe room is built far outside the playable world" \
        "a diagnostic the player can walk into is level geometry"
  fi

  # Entry must still demand a real prepared world, so binding the probe cannot make the
  # threshold enterable.
  if code "$ENV/LobbyPortal.cs" | grep -qE 'CanBeEntered =>' &&
     code "$ENV/LobbyPortal.cs" | sed -n '/public bool CanBeEntered/,/;/p' \
       | grep -qE 'MissionWorldLoader\.WorldReady'; then
    ok "showing the probe room cannot make the portal enterable"
  else
    bad "showing the probe room cannot make the portal enterable" \
        "entry must still require a prepared mission world"
  fi
fi

# ---- V9.6: eine Nordwand, keine Sockelleisten ----------------------------------------------
# Die Nordwand ist EIN Quader ueber die volle Breite. Sie war einmal in Segmente mit einer
# Tuerluecke zerlegt, dazu ein Rahmen und ein Laufzeit-Flicken; nichts davon darf zurueck.
#
# Geprueft wird die INVARIANTE, nicht der Name: das Portal braucht EINE Wand, in die es sein
# Loch schneiden kann. Das ist entweder die gebaute Lobby_Wall_North oder ein ausdruecklich
# eingetragenes wallCollider auf der Portal-Komponente. Auf den Namen allein zu pruefen war zu
# eng - eine von Hand gebaute Wand aus gekauften Teilen erfuellt die Invariante genauso - und
# zugleich zu weit: ein umbenanntes Objekt haette die Pruefung bestanden, waehrend das Portal
# nichts mehr findet. ResolveWall sucht sonst per FORM, und ein Wandmodul, das schmaler ist
# als die Oeffnung, faellt dort durch, ohne dass irgendetwas anderes es sagt.
wall_named=$(grep -cE '^  m_Name: Lobby_Wall_North$' "$SCENE")
wall_wired=$(grep -E '^  wallCollider: \{fileID: [0-9]+' "$SCENE" \
             | grep -cvE '^  wallCollider: \{fileID: 0\}')
if [ "$wall_named" -gt 0 ] || [ "$wall_wired" -gt 0 ]; then
  ok "das Portal hat eine Wand, in die es schneiden kann"
else
  bad "das Portal hat eine Wand, in die es schneiden kann" \
      "weder Lobby_Wall_North in der Szene noch wallCollider am Portal gesetzt; ResolveWall \
sucht dann per Form und braucht EINEN Collider von mindestens der Oeffnungsbreite - ein \
Wandmodul des gekauften Pakets ist schmaler und faellt durch. Messen mit \
'Catch If You Can/Lobby/Portalwand messen'."
fi

# ---- die Portalwand wird GEMESSEN, nicht behauptet ------------------------------------------
# Solange die Wand nicht steht, ist die einzige ehrliche Antwort eine Messung. Das Werkzeug
# dafuer muss drei Dinge einhalten, sonst misst es etwas anderes als das, was zur Laufzeit
# passiert - und eine Messung, der man nicht trauen kann, ist schlimmer als keine.
PROBE="$EDT/LobbyPortalWallProbe.cs"
if [ ! -f "$PROBE" ]; then
  bad "es gibt ein Werkzeug, das die Portalwand misst" "erwartet $PROBE"
  bad "das Messwerkzeug aendert nichts" "Datei fehlt"
  bad "das Messwerkzeug liest das Portal ueber oeffentliche Zusagen" "Datei fehlt"
  bad "das Messwerkzeug sieht auch ausgeschaltete Objekte" "Datei fehlt"
  bad "das Messwerkzeug wendet dieselben drei Tests an wie ResolveWall" "Datei fehlt"
  bad "ein leeres Collider-Bounds gilt nicht als 'nichts da'" "Datei fehlt"
else
  ok "es gibt ein Werkzeug, das die Portalwand misst"

  # Ein Diagnosewerkzeug, das etwas veraendert, ist keine Diagnose. Besonders nicht dieses:
  # es liefe ueber eine Szene, in der der Benutzer gerade von Hand gebaut hat.
  writes=""
  for call in 'EditorUtility.SetDirty' 'AssetDatabase.SaveAssets' 'AssetDatabase.CreateAsset' \
              'Undo\.' 'DestroyImmediate' 'Object.Instantiate' 'SetActive'; do
    code "$PROBE" | grep -qE "$call" && writes="$writes $call"
  done
  # Und es traegt insbesondere nicht selbst ein, was es messen soll.
  code "$PROBE" | grep -qE 'wallCollider[[:space:]]*=' && writes="$writes wallCollider="
  if [ -n "$writes" ]; then
    bad "das Messwerkzeug aendert nichts" "gefunden:$writes"
  else
    ok "das Messwerkzeug aendert nichts"
  fi

  # CLAUDE.md Fehler 4: Reflection in fremde private Felder kompiliert, liest sich sauber und
  # faellt beim naechsten Umbenennen still um. Das Portal sagt die drei Zahlen selbst zu.
  if code "$PROBE" | grep -qE 'BindingFlags|GetField\(|GetProperty\(' ; then
    bad "das Messwerkzeug liest das Portal ueber oeffentliche Zusagen" \
        "Reflection in private Felder - beim naechsten Umbenennen still falsch"
  elif code "$PROBE" | grep -qE 'portal\.OpeningSize' \
    && code "$PROBE" | grep -qE 'portal\.MaxWallThickness' \
    && code "$PROBE" | grep -qE 'portal\.AssignedWallCollider'; then
    ok "das Messwerkzeug liest das Portal ueber oeffentliche Zusagen"
  else
    bad "das Messwerkzeug liest das Portal ueber oeffentliche Zusagen" \
        "OpeningSize, MaxWallThickness und AssignedWallCollider muessen vom Portal kommen"
  fi

  # Der entscheidende Punkt. MainMenu_Lobby ist in der Datei AUS; zur Laufzeit ist sie AN,
  # bevor das Portal aufgeht. Ein Suchlauf ohne inaktive Objekte meldet den Raum als leer
  # und beschreibt damit genau den einen Moment nicht, um den es geht.
  if code "$PROBE" | grep -qE 'FindObjectsInactive\.Include'; then
    ok "das Messwerkzeug sieht auch ausgeschaltete Objekte"
  else
    bad "das Messwerkzeug sieht auch ausgeschaltete Objekte" \
        "ohne FindObjectsInactive.Include ist die schlafende Lobby unsichtbar und der " \
        "Bericht sagt 'nichts da' ueber einen Raum, der zur Laufzeit steht"
  fi

  # Dieselben drei Tests, nicht ungefaehr dieselben. Ein Werkzeug, das grosszuegiger misst
  # als ResolveWall, meldet eine Wand, die das Portal dann ablehnt.
  if code "$PROBE" | grep -qE 'thickness[[:space:]]*<=[[:space:]]*maxThickness' \
     && code "$PROBE" | grep -qE 'width[[:space:]]*>=[[:space:]]*opening\.x' \
     && code "$PROBE" | grep -qE 'height[[:space:]]*>=[[:space:]]*opening\.y'; then
    ok "das Messwerkzeug wendet dieselben drei Tests an wie ResolveWall"
  else
    bad "das Messwerkzeug wendet dieselben drei Tests an wie ResolveWall" \
        "Dicke gegen maxWallThickness, Breite und Hoehe gegen openingSize"
  fi

  # Auf einem inaktiven Objekt kann Collider.bounds eine leere Box liefern, und eine leere Box
  # schneidet nichts. Ungeprueft liest sich das als 'da ist nichts' - dasselbe Ergebnis wie
  # eine fehlende Wand, mit einer voellig anderen Ursache.
  if code "$PROBE" | grep -qE 'MeasuredBounds' \
     && code "$PROBE" | grep -qE 'b\.size[[:space:]]*!=[[:space:]]*Vector3\.zero'; then
    ok "ein leeres Collider-Bounds gilt nicht als 'nichts da'"
  else
    bad "ein leeres Collider-Bounds gilt nicht als 'nichts da'" \
        "eine leere Box schneidet nichts und liest sich als fehlende Wand"
  fi
fi

split=""
for obj in Lobby_Wall_North_Left Lobby_Wall_North_Right Lobby_Wall_North_Header \
           Lobby_Wall_North_Fill Lobby_DoorJamb_Left Lobby_DoorJamb_Right Lobby_DoorLintel; do
  grep -qE "^  m_Name: $obj\$" "$SCENE" && split="$split $obj"
done
if [ -n "$split" ]; then
  bad "die Nordwand ist nicht wieder in Teile zerlegt" "noch da:$split"
else
  ok "die Nordwand ist nicht wieder in Teile zerlegt"
fi

# Keine Sockelleisten mehr, an keiner Wand. Sie waren der letzte Ort, an dem die alte
# Tuerluecke noch als Form ueberlebt hat.
skirt=$(grep -oE '^  m_Name: Lobby_Skirting\w*' "$SCENE" | sed 's/^  m_Name: //' | tr '\n' ' ')
if [ -n "$skirt" ]; then
  bad "an keiner Wand haengt eine Sockelleiste" "noch da: $skirt"
else
  ok "an keiner Wand haengt eine Sockelleiste"
fi

# Die Ostwand bleibt zerlegt - da ist ein Fenster drin.
if grep -qE '^  m_Name: Lobby_Wall_East_North$' "$SCENE" \
   && grep -qE '^  m_Name: Lobby_Window_Glass$' "$SCENE"; then
  ok "die Fensterwand im Osten ist unangetastet"
else
  bad "die Fensterwand im Osten ist unangetastet" \
      "beim Aufraeumen darf das Fenster nicht mitgehen"
fi

if code "$ENV/LobbyPortal.cs" | grep -qE 'EnsureWallPlug|_wallPlug|wallPlugDepth'; then
  bad "kein Laufzeit-Flicken mehr in der Wand" \
      "die Wand ist durchgezogen; ein zweites Quad davor ist nur Z-Fighting"
else
  ok "kein Laufzeit-Flicken mehr in der Wand"
fi

# ---------------------------------------------------------------- the portal camera maths
#
# A portal view is the destination scene rendered from the player's eye carried through the
# pair, into an off-screen buffer, sampled in SCREEN space. Three things have to agree for
# that to be a window rather than a picture hanging on a wall, and each of them fails
# silently and differently.

SURF="$ART/PortalSurface.cs"
SHADER="$ROOT/Assets/CatchIfYouCan/Shaders/Portal.shader"

# 1. The image is sampled where the fragment is on screen, not where it is on the quad.
#    Sampling the mesh UV instead gives a texture pasted flat on the surface: it does not
#    shift with the head, which is the whole illusion.
if grep -q 'ComputeScreenPos' "$SHADER" && grep -qE 'screenPos\.xy */ *max\(.*screenPos\.w' "$SHADER"; then
  ok "the destination is sampled in screen space, not by the quad's own UV"
else
  bad "the destination is sampled in screen space, not by the quad's own UV" \
      "without the perspective divide on ComputeScreenPos the far room is a flat decal"
fi

# 2. The buffer is rendered at the shape it is sampled at. Screen-space sampling reads the
#    image as if it covered the screen, so a buffer rendered at a different aspect is
#    stretched - and ResolveTextureSize clamps the width, so the two can disagree.
if code "$SURF" | tr -d '\n' | tr -s ' ' \
     | grep -qE '_portalCamera\.aspect = _textureHeight > 0'; then
  ok "the portal camera's aspect comes from the buffer it renders into"
else
  bad "the portal camera's aspect comes from the buffer it renders into" \
      "taking it from the source camera leaves the render and the lookup different shapes"
fi

# 3. The oblique near plane keeps the far ROOM and clips the far room's own wall. Which half
#    that is depends on which way the destination Transform faces, and nothing constrains
#    that - so the side has to be derived from where the camera actually is. Assumed, it is a
#    coin flip, and the losing side clips the whole room away and leaves the sky: a black
#    interior behind a lit rim, which is exactly what was reported.
if code "$SURF" | grep -qE 'Mathf\.Sign\(Vector3\.Dot\(normal, cameraPosition - point\)\)'; then
  ok "the oblique clip side is derived from the camera, not assumed"
else
  bad "the oblique clip side is derived from the camera, not assumed" \
      "CalculateObliqueMatrix keeps the half its normal points into; passing destination.forward raw clips the room when that transform faces the other way"
fi

# The offset that lifts the plane off the destination wall has to follow the DERIVED normal.
# Following the raw one shaves the offset off the room instead of off the wall on the
# flipped case, which is the same bug wearing a 2 cm hat.
if code "$SURF" | grep -qE 'offsetPoint = point \+ kept \* clipPlaneOffset'; then
  ok "the clip-plane offset follows the derived normal"
else
  bad "the clip-plane offset follows the derived normal" \
      "offsetting along the raw normal pushes the plane into the room when the side is flipped"
fi

# The projection must be reset before it is skewed: CalculateObliqueMatrix reads the camera's
# CURRENT projection, so skewing an already-skewed matrix compounds every frame.
if code "$SURF" | tr -d '\n' | tr -s ' ' \
     | grep -qE 'ResetProjectionMatrix\(\); .*projectionMatrix = _portalCamera\.CalculateObliqueMatrix'; then
  ok "the projection is reset before it is made oblique"
else
  bad "the projection is reset before it is made oblique" \
      "CalculateObliqueMatrix reads the current projection; without a reset the skew accumulates"
fi

# ---------------------------------------------------------------- the portal camera, continued

# The camera object stays active and the COMPONENT is what is switched. Toggling the
# GameObject churns the hierarchy through OnDisable/OnEnable every time the player looks
# away, and it is also how a second camera ends up rendering on a frame its pose was never
# written for.
if code "$SURF" | grep -qE '_portalCamera\.gameObject\.SetActive'; then
  bad "the portal camera is gated by its component, not by its GameObject" \
      "SetActive on the camera object churns the hierarchy and decouples the render from the pose"
else
  ok "the portal camera is gated by its component, not by its GameObject"
fi

# It is enabled at the END of LateUpdate, after the pose, the aspect and the oblique plane are
# all written - so the frame Unity draws is the frame that was set up, never a stale one.
if code "$SURF" | tr -d '\n' | tr -s ' ' \
     | grep -qE 'CalculateObliqueMatrix\(_clipPlane\); _portalCamera\.enabled = true;'; then
  ok "the camera is enabled only after the pose and the clip plane are written"
else
  bad "the camera is enabled only after the pose and the clip plane are written" \
      "enabling it earlier lets Unity draw a frame whose projection was not set up yet"
fi

# Nothing that makes a camera the player's may be on it. An AudioListener in particular gives
# the scene two, and Unity then picks one at random and warns about it forever.
if code "$SURF" | grep -qE 'AddComponent<AudioListener>|AddComponent<Player'; then
  bad "the portal camera carries nothing that makes it the player's" \
      "no AudioListener, no PlayerLook, no input or HUD component belongs on it"
else
  ok "the portal camera carries nothing that makes it the player's"
fi

# The orientation convention is checked, not compensated for in a dozen places. One rule -
# local +Z out of the visible surface - and a destination that breaks it is named.
if code "$SURF" | grep -q 'refuseOnOrientationMismatch' \
   && code "$SURF" | grep -qE 'Vector3\.Dot\(destination\.forward,'; then
  ok "the source/destination orientation convention is validated"
else
  bad "the source/destination orientation convention is validated" \
      "a destination facing the wrong way renders a plausible view and traverses backwards"
fi

# The far room is re-rendered on a cadence the quality level chooses, so a phone can pay for
# it half as often. Zero means every frame; the check is that the seam exists at all.
if code "$ART/PortalStyle.cs" | grep -qE 'public float RefreshInterval\(\)' \
   && code "$SURF" | grep -qE '_style\.RefreshInterval\(\)'; then
  ok "the portal view has a render cadence the quality level drives"
else
  bad "the portal view has a render cadence the quality level drives" \
      "a second pass over a whole house every frame is not a mobile budget"
fi

# The far room must stay readable. The distortion is confined to the edge by the shader, and
# capped in magnitude by the range on the field rather than by whoever drags the slider.
if code "$ART/PortalStyle.cs" | tr -d '\n' | tr -s ' ' \
     | grep -qE '\[Range\(0f, 0\.01[0-5]f\)\] public float viewDistortionStrength'; then
  ok "the view distortion is capped at 1.5% of the screen"
else
  bad "the view distortion is capped at 1.5% of the screen" \
      "a portal whose centre wobbles is a screen effect, not an opening"
fi

# The bend is weighted to the boundary. Applied flat it drags the middle of the opening too.
if grep -qE 'bend = saturate\(1\.0 - view\)' "$SHADER"; then
  ok "the view distortion falls to zero toward the centre"
else
  bad "the view distortion falls to zero toward the centre" \
      "an unweighted offset moves the whole far room, not just its edge"
fi

# Unity's own projection data decides the render-target flip, not a platform name. A
# hand-written per-platform branch is how the portal ends up upside down on exactly one API.
if grep -qE '#if +UNITY_(STANDALONE|IOS|ANDROID|EDITOR_WIN|EDITOR_OSX)' "$SHADER"; then
  bad "no hand-coded per-platform flip in the portal shader" \
      "ComputeScreenPos already carries _ProjectionParams.x, which is the convention data"
else
  ok "no hand-coded per-platform flip in the portal shader"
fi

# Debug output exists and is off unless asked for.
if code "$SURF" | grep -qE 'private bool debugReadout' \
   && code "$SURF" | grep -qE 'if \(!debugReadout\)'; then
  ok "the portal debug readout exists and is opt-in"
else
  bad "the portal debug readout exists and is opt-in" \
      "a portal that logs its matrix every frame is a console nobody can read"
fi

# No recursion. One portal seen through another is not needed and doubles the cost silently.
if code "$SURF" | grep -qE 'maxRecursion|recursionDepth|RenderRecursive'; then
  bad "the portal does not render recursively" \
      "production default is one pass; recursion doubles the cost per level"
else
  ok "the portal does not render recursively"
fi

# ---------------------------------------------------------------- V8.5: one portal, one world

MWL="$ROOT/Assets/CatchIfYouCan/Scripts/Missions/MissionWorldLoader.cs"
BUDGET="$ART/SecondaryViewBudget.cs"

# The hand-built flat the portal used to show instead of the mission. It is gone; this is
# what stops it coming back as "something to look at while the world builds".
if [ -f "$ROOT/Assets/CatchIfYouCan/Scripts/Environment/ReferenceApartment.cs" ]; then
  bad "no ReferenceApartment stands in for the mission world" \
      "the portal shows the world the player will enter, or it shows the probe room and says so"
else
  ok "no ReferenceApartment stands in for the mission world"
fi

# Exactly one portal architecture. A second one always arrives named like this.
dupes=$(ls "$ART"/PortalSystem2.cs "$ART"/TruePortal*.cs "$ART"/AdvancedPortal*.cs \
           "$ART"/PortalCameraNew.cs "$ART"/PortalV2.cs 2>/dev/null || true)
if [ -n "$dupes" ]; then
  bad "there is exactly one portal implementation" "$dupes"
else
  ok "there is exactly one portal implementation"
fi

# The view is bound to the PREPARED world's arrival point - the same InvestigationBootstrap
# that EnterAsync activates - so what the player looks at is what they walk into.
#
# Bound THROUGH ResolveViewAnchor, which raises the anchor to the portal's own height: the
# arrival point is where a pair of feet goes and the portal's reference is the surface centre,
# and pairing those two directly put the portal camera 1.2 m too low, which reads as the far
# room's floor being too high. The anchor is a CHILD of the arrival point, which is what keeps
# it the prepared world's and not some other one's - so both halves are checked.
if code "$ENV/LobbyPortal.cs" \
     | grep -qE 'ResolveViewAnchor\(_pendingWorld\.MissionEntryAnchor\)' &&
   code "$ENV/LobbyPortal.cs" | grep -qE '_viewAnchor\.SetParent\(arrival, *false\)' &&
   ! code "$ENV/LobbyPortal.cs" | grep -qE '_pendingWorld\.ArrivalPoint'; then
  ok "the portal is aimed at the prepared world the player will enter"
else
  bad "the portal is aimed at the prepared world the player will enter" \
      "showing one world and loading another is the bug this whole flow exists to avoid"
fi

# One seed, one generation. The loader reuses the prepared bootstrap when the mission is the
# same object; rolling again would give the player a different house than the one on show.
if code "$MWL" | grep -qE 'ReferenceEquals\(InvestigationBootstrap\.Prepared\.Mission, mission\)'; then
  ok "a prepared world is reused rather than regenerated"
else
  bad "a prepared world is reused rather than regenerated" \
      "a second generation is a second house, and the portal was showing the first"
fi

# The world is prepared ADDITIVELY, behind the lobby, with the player still standing in it.
if code "$MWL" | grep -qE 'LoadSceneAsync\([^,]+, *LoadSceneMode\.Additive\)'; then
  ok "the mission world is prepared additively behind the lobby"
else
  bad "the mission world is prepared additively behind the lobby" \
      "a single-scene load replaces the lobby, which IS the teleport this flow removed"
fi

# Nothing in the portal may start a timer that ends in a handover. Entry is the player's.
if code "$ENV/LobbyPortal.cs" | grep -qE '(^|[^.])\bInvoke\("' \
   || code "$ENV/LobbyPortal.cs" | grep -qE 'InvokeRepeating\('; then
  bad "no timer can hand the player over" \
      "world-ready or animation-complete must never mean 'therefore teleport'"
else
  ok "no timer can hand the player over"
fi

# A preparation that fails has a state of its own. Reporting it as Inactive - which is also
# what a doorway nobody asked anything of reports - makes a failure invisible.
if code "$ENV/LobbyPortal.cs" | grep -qE '^ *Failed,' \
   && code "$ENV/LobbyPortal.cs" | grep -qE 'SetState\(LobbyPortalState\.Failed\)'; then
  ok "a failed preparation has a state of its own"
else
  bad "a failed preparation has a state of its own" \
      "the player stands in the lobby whether nothing was asked or everything went wrong"
fi

# ---------------------------------------------------------------- V8.5: the frame budget

if [ -f "$BUDGET" ]; then
  ok "the lobby's secondary views share an arbiter"
else
  bad "the lobby's secondary views share an arbiter" \
      "mirror and portal each culling correctly still means three renders when both are on screen"
fi

for view in "$SURF" "$ART/MirrorCorner.cs"; do
  vname="$(basename "$view" .cs)"
  if code "$view" | grep -qE 'SecondaryViewBudget\.MayRender\('; then
    ok "$vname asks the shared budget before rendering"
  else
    bad "$vname asks the shared budget before rendering" \
        "a view that never asks cannot be arbitrated with"
  fi
done

# The budget comes from the project's one quality signal. A parallel tier enum can disagree
# with the buffer sizes and the particle rates, and then nothing is describable.
if code "$BUDGET" | grep -qE 'PortalStyle\.QualityFraction01\(\)' \
   && ! code "$BUDGET" | grep -qE 'enum +[A-Za-z]*Tier'; then
  ok "the frame budget derives from the project's own quality level"
else
  bad "the frame budget derives from the project's own quality level" \
      "a second notion of how much machine this is can disagree with the first"
fi

# The buffer ladder has named ends. Defining the bottom as half the top means raising the
# desktop buffer silently raises the phone's.
if code "$ART/PortalStyle.cs" | grep -qE 'public int minViewResolution *(=|;)' \
   && ! code "$SURF" | grep -qE 'resolution \* 0\.5f'; then
  ok "the view buffer ladder has named ends, not a halved top"
else
  bad "the view buffer ladder has named ends, not a halved top" \
      "the lowest quality level must not be a function of the highest"
fi

# ---------------------------------------------------------------- der Ankunftspunkt

BOOT="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"

# Das Portal bindet seine Ansicht an ArrivalPoint. Bleibt der null, wird nie gebunden - die
# Oeffnungsroutine haelt das fuer eine fehlgeschlagene Vorbereitung und laesst die Tuer nach
# gut einer Sekunde zusammenfallen, mit dem Diagnoseraum darin. Er kam vom Van, und ohne Van
# gab es keinen. Also gibt es jetzt immer einen.
if code "$BOOT" | grep -qE 'private void EnsureFallbackArrival\(\)' \
   && code "$BOOT" | grep -qE '_van\.PlayerSpawnPoint : _fallbackArrival'; then
  ok "es gibt immer einen Ankunftspunkt, auch ohne Van"
else
  bad "es gibt immer einen Ankunftspunkt, auch ohne Van" \
      "ohne ihn bindet das Portal nie und faellt nach der Oeffnungsdauer zusammen"
fi

# Und er wird in BEIDEN Zweigen angelegt, nicht nur im generierten.
prep=$(code "$BOOT" | sed -n '/private bool PrepareWorld/,/^        private void EnsureFallbackArrival/p')
if [ "$(printf '%s' "$prep" | grep -c 'EnsureFallbackArrival();')" -ge 2 ]; then
  ok "der Ankunftspunkt entsteht in beiden Zweigen der Vorbereitung"
else
  bad "der Ankunftspunkt entsteht in beiden Zweigen der Vorbereitung" \
      "einer davon laesst das Portal wieder zusammenfallen"
fi

# ---- ein geschlossenes Portal zeichnet nichts ----------------------------------------------
# Nicht "der Shader rechnet alles durchsichtig", sondern der Renderer ist aus. Die
# Shader-Eigenschaft wird nur gesetzt, wenn der echte Portal-Shader gefunden wurde; wurde er
# es nicht, laeuft das Ersatzmaterial und zeichnet ein undurchsichtiges Viereck von der
# Groesse der Oeffnung mitten in die Wand. Das sah aus wie eine Tuer und war auch eine.
if code "$ART/PortalSurface.cs" | grep -qE '_surfaceRenderer\.enabled = _open > 0\.001f'; then
  ok "ein geschlossenes Portal schaltet seinen Renderer ab"
else
  bad "ein geschlossenes Portal schaltet seinen Renderer ab" \
      "auf Durchsichtigkeit im Shader zu bauen versagt, sobald das Ersatzmaterial laeuft"
fi

# Und der Ausgangszustand ist ZU. Eine Wand ist zu, bis jemand sie aufreisst.
if code "$ART/PortalSurface.cs" | grep -qE 'private float _open;'; then
  ok "die Portalflaeche faengt geschlossen an"
else
  bad "die Portalflaeche faengt geschlossen an" \
      "ein Standardwert von 1 stellt die Flaeche ab dem ersten Frame sichtbar in die Wand"
fi

# ---- V10: a shader that does not compile is drawn magenta ------------------------------------
#
# The portal shipped with `gate` USED one statement above the line that declares it. HLSL has no
# hoisting, so that is not a wrong pixel - it is a compile error, and Unity draws a shader that
# failed to compile with its magenta error shader. On screen that is indistinguishable from a
# built-in shader under URP (CLAUDE.md mistake 2) or from a pack imported for the wrong pipeline,
# and it was read as both before anyone read the shader.
#
# The old checks could not see it: they grepped for the declaration and for the use separately,
# and both were present - just in the wrong order. This reads the order. No compiler is available
# here, so nothing else in CI would.
ORDER="$(python3 - "$ROOT" <<'PYEOF'
import re, sys, pathlib

TYPES = r'(?:float|half|fixed|int|uint|bool|double)(?:[1-4](?:x[1-4])?)?'
DECL = re.compile(r'^\s*(?:const\s+|static\s+)*' + TYPES + r'\s+([A-Za-z_]\w*)\s*(=|;|\[)')
FUNC = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*(?:inline\s+)?(?:' + TYPES + r'|void|struct)\s+'
                  r'([A-Za-z_]\w*)\s*\([^;]*$')

def strip_comments(text):
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    return '\n'.join(re.sub(r'//.*$', '', ln) for ln in text.split('\n'))

findings = []
for path in sorted(pathlib.Path(sys.argv[1], 'Assets').rglob('*.shader')):
    raw = path.read_text(errors='replace')
    for m in re.finditer(r'(HLSLPROGRAM|CGPROGRAM)(.*?)(ENDHLSL|ENDCG)', raw, re.S):
        base = raw[:m.start(2)].count('\n') + 1
        lines = strip_comments(m.group(2)).split('\n')
        i = 0
        while i < len(lines):
            if FUNC.match(lines[i]) and 'struct' not in lines[i]:
                depth, j, started = 0, i, False
                while j < len(lines):
                    depth += lines[j].count('{') - lines[j].count('}')
                    if '{' in lines[j]:
                        started = True
                    if started and depth <= 0:
                        break
                    j += 1
                body = lines[i:j + 1]
                declared = {}
                for k, ln in enumerate(body):
                    d = DECL.match(ln)
                    if d:
                        declared.setdefault(d.group(1), k)
                for name, k in declared.items():
                    for k2 in range(k):
                        if re.search(r'(?<![.\w])' + re.escape(name) + r'(?![\w])', body[k2]):
                            findings.append("%s:%d '%s' used before its declaration at line %d"
                                            % (path.name, base + i + k2, name, base + i + k))
                            break
                i = j + 1
            else:
                i += 1
print("; ".join(findings))
PYEOF
)"
if [ -z "$ORDER" ]; then
  ok "every shader local is declared before it is used"
else
  bad "every shader local is declared before it is used" \
      "HLSL does not hoist; this does not compile and Unity draws it magenta: $ORDER"
fi

# ---- V10: a purchased pack may lend the portal its LOOK, never its SHAPE ----------------------
#
# An HDRP portal pack cannot be dropped into a URP project - its shaders resolve to the magenta
# error shader and HDRP has no mobile support at all, which is this game's only real target. What
# CAN cross is the artwork, so the shader takes two texture slots. The invariant is that they
# reach colour and heat ONLY: the signed box field, the tear and the closed-means-nothing gate
# are what make this a hole in a wall rather than a picture of one, and no adopted pack may move
# them.
SHADER="$ROOT/Assets/CatchIfYouCan/Shaders/Portal.shader"
ART_BLOCK="$(sed -n '/#ifdef _PORTAL_TEXTURED/,/#endif/p' "$SHADER")"

if [ -n "$ART_BLOCK" ]; then
  if printf '%s' "$ART_BLOCK" | grep -qE '^\s*(float2? +)?(box|oval|fit|gate|alpha|open|ragged|rd|r) *='; then
    bad "purchased artwork cannot move the breach" \
        "the artwork block assigns a silhouette term; a pack must change the look, not the hole"
  else
    ok "purchased artwork cannot move the breach"
  fi
else
  bad "purchased artwork cannot move the breach" "the _PORTAL_TEXTURED block is gone"
fi

# The samples must be INSIDE the keyword. Sampling two textures on every portal pixel of every
# frame to multiply by an influence of zero is a cost a phone pays for a result identical to not
# sampling at all.
OUTSIDE="$(grep -n 'SAMPLE_TEXTURE2D(_EnergyTex\|SAMPLE_TEXTURE2D(_MaskTex' "$SHADER" | wc -l | tr -d ' ')"
INSIDE="$(printf '%s' "$ART_BLOCK" | grep -c 'SAMPLE_TEXTURE2D(_EnergyTex\|SAMPLE_TEXTURE2D(_MaskTex' | tr -d ' ')"
if [ "$OUTSIDE" = "$INSIDE" ] && [ "$INSIDE" -gt 0 ] &&
   grep -q 'shader_feature_local_fragment _PORTAL_TEXTURED' "$SHADER"; then
  ok "the purchased-artwork samplers are compiled out when unused"
else
  bad "the purchased-artwork samplers are compiled out when unused" \
      "every SAMPLE of the artwork must sit inside #ifdef _PORTAL_TEXTURED ($INSIDE of $OUTSIDE)"
fi

# Ticking the box with no texture assigned must NOT switch the keyword on. With it on and the
# energy slot empty the shader samples the default black, multiplies the energy by it, and the
# portal goes dark - which reads as a broken portal rather than an unconfigured one.
if code "$ART/PortalStyle.cs" \
     | grep -qE 'ArtworkActive *=> *usePurchasedArtwork *&& *energyTexture != null'; then
  ok "adopting with no texture keeps the procedural portal"
else
  bad "adopting with no texture keeps the procedural portal" \
      "the keyword must need a real texture, not just the tick, or an empty slot blacks it out"
fi

# The adapter reads the purchased folder and writes only inside the project. A pack is
# deliberately outside version control, so anything written into it exists on one machine, and
# anything REFERENCED inside it is a missing asset everywhere else - CLAUDE.md mistake 15.
ADAPTER="$ROOT/Assets/CatchIfYouCan/Editor/PurchasedPortalAdapter.cs"
if [ -f "$ADAPTER" ]; then
  if code "$ADAPTER" | grep -qE 'DestinationFolder *= *"Assets/CatchIfYouCan/' &&
     code "$ADAPTER" | grep -qE 'AssetDatabase\.CopyAsset\('; then
    ok "the portal adapter copies the pack's artwork into the project"
  else
    bad "the portal adapter copies the pack's artwork into the project" \
        "referencing a texture where the pack lies resolves on exactly one machine"
  fi

  # The pack path is data, never a literal the tool falls back to mid-scan. A tool that
  # silently scans somewhere other than the path it was given reports on the wrong folder,
  # which this project has already shipped once.
  if code "$ADAPTER" | grep -qE 'ScannedPath *= *folder' &&
     ! code "$ADAPTER" | grep -qE 'folder *\+ *"/(interior|Portal|Shaders|Textures)"'; then
    ok "the portal adapter scans exactly the path it was given"
  else
    bad "the portal adapter scans exactly the path it was given" \
        "no suffix may be appended, and the report must name the path actually read"
  fi
else
  bad "the portal adapter exists" "PurchasedPortalAdapter.cs is how a bought pack gets adopted"
fi

# ---- V10: a particle with no texture is an opaque square -------------------------------------
#
# The sparks shipped as solid green rectangles drifting over the wall. `new Material(shader)` on
# URP's particle shader inherits the shader's defaults, and those are an OPAQUE surface with no
# base map - so every billboard drew the default white texture as a hard-edged quad, tinted
# green by the particle gradient.
#
# Both halves are needed and neither is sufficient. A texture on an opaque material is still a
# rectangle; additive blending with no texture is a brighter rectangle.
if code "$FX" | grep -qE 'private void ConfigureAdditive\(Material' &&
   code "$FX" | grep -qE 'ConfigureAdditive\(material\);'; then
  ok "the particle material is configured, not left at the shader defaults"
else
  bad "the particle material is configured, not left at the shader defaults" \
      "a freshly constructed URP particle material is opaque and untextured"
fi

if code "$FX" | grep -qE 'material\.SetFloat\("_Surface", *1f\)' &&
   code "$FX" | grep -qE 'material\.SetFloat\("_DstBlend", *\(float\)UnityEngine\.Rendering\.BlendMode\.One\)'; then
  ok "the sparks are transparent and additive"
else
  bad "the sparks are transparent and additive" \
      "without _Surface AND the blend factors the material renders opaque whatever it says"
fi

if code "$FX" | grep -qE 'material\.SetTexture\("_BaseMap", *SparkSprite\(\)\)'; then
  ok "the sparks are given something to draw"
else
  bad "the sparks are given something to draw" \
      "the default white texture on a billboard is a solid square"
fi

# The sprite is generated, so it must also be released. A Texture2D built at runtime is not
# collected with the GameObject that referenced it.
if code "$FX" | grep -qE 'private Texture2D SparkSprite\(\)' &&
   code "$FX" | sed -n '/private void OnDestroy/,/^        }$/p' | grep -qE 'Destroy\(_sparkSprite\)'; then
  ok "the generated spark sprite is released"
else
  bad "the generated spark sprite is released" \
      "a runtime Texture2D outlives the object that made it"
fi

# Ticking a purchased spark image in without one assigned must not clear the generated dot: an
# empty slot is the untextured square this whole path exists to remove.
ADAPTER="$ROOT/Assets/CatchIfYouCan/Editor/PurchasedPortalAdapter.cs"
if [ -f "$ADAPTER" ] && code "$ADAPTER" | grep -qE 'if \(spark != null\)'; then
  ok "a pack with no spark image leaves the generated dot alone"
else
  bad "a pack with no spark image leaves the generated dot alone" \
      "clearing sparkTexture swaps a soft dot for an opaque square"
fi

# ---- V10: the numbers have to be reachable ---------------------------------------------------
#
# Every tunable was unreachable in both directions. Before play there is no portal at all - the
# surface is built at runtime - and during play, editing the style did nothing, because the
# values are pushed exactly once when the surface is built. The only field that appeared to
# work was the material's on PortalSurface, and editing a material is editing the copy: the
# next PushStyle overwrites it. An artistic control you cannot turn while looking at the thing
# is not a control.
if code "$ENV/LobbyPortal.cs" | grep -qE 'private void OnValidate\(\)' &&
   code "$ENV/LobbyPortal.cs" | sed -n '/private void OnValidate/,/^        }$/p' \
     | grep -qE 'surface\.ApplyStyle\(style\)'; then
  ok "the portal style can be edited while the game runs"
else
  bad "the portal style can be edited while the game runs" \
      "without OnValidate the style is pushed once at build and never again"
fi

# ...and the size specifically, which needs geometry re-derived rather than a property written.
if code "$SURF" | grep -qE 'public void Rebuild\(\)' &&
   code "$SURF" | sed -n '/public void SetOpening/,/^        }$/p' | grep -qE 'Rebuild\(\);'; then
  ok "the opening can be resized after it is built"
else
  bad "the opening can be resized after it is built" \
      "SetOpening used to refuse and log, which made the width un-tunable"
fi

# The mesh, the captured plane and the culling bounds are one set. A mesh resized without its
# bounds culls itself at the old size; a plane left behind puts the crossing test somewhere the
# player cannot see.
REBUILD="$(code "$SURF" | sed -n '/public void Rebuild()/,/^        }$/p')"
MISSING=""
printf '%s' "$REBUILD" | grep -qE 'mesh\.RecalculateBounds\(\)' || MISSING="$MISSING mesh-bounds"
printf '%s' "$REBUILD" | grep -qE '_planePoint = _surface\.position' || MISSING="$MISSING plane"
printf '%s' "$REBUILD" | grep -qE '_openingBounds = _surfaceRenderer\.bounds' || MISSING="$MISSING cull-bounds"
if [ -z "$MISSING" ]; then
  ok "a resize re-derives the mesh, the plane and the bounds together"
else
  bad "a resize re-derives the mesh, the plane and the bounds together" "missing:$MISSING"
fi

# ---- V10: the drawn quad is bigger than the hole ---------------------------------------------
#
# The glow was cut off in a straight line across the top, because the quad WAS the opening and
# the outer spill reaches about 1.65x the oval's radius. The margin is what gives it somewhere
# to go, and the same margin has to divide _Fit or the hole comes out the wrong size.
if code "$STYLE" | grep -qE 'public Vector2 QuadSize\(\)' &&
   code "$STYLE" | grep -qE 'public Vector2 ResolveFit\(\)' &&
   code "$SURF" | grep -qE '_style\.QuadSize\(\)' &&
   code "$SURF" | grep -qE '_style\.ResolveFit\(\)'; then
  ok "the drawn quad is larger than the opening, and _Fit divides by the same margin"
else
  bad "the drawn quad is larger than the opening, and _Fit divides by the same margin" \
      "a quad sized by one formula and a _Fit by another is a breach that misses its geometry"
fi

# The margin has to be ENOUGH, computed from the authored numbers rather than trusted.
#
# This check was deleted once, on the reasoning that it rejected a configuration that looked
# right. That reasoning was wrong, and the deletion is why the rectangular clipping at the top
# and the upper corners then had to be reported by a human. Re-run against the config it was
# deleted over, the vertical reach was 1.353 - clipped, exactly where the clipping was later
# seen. It had not rejected a good case; it had found a bad one that nobody had looked at yet.
#
# The metric is the visible rim, not the theoretical extreme: the outer falloff at 1.6x the rim
# width, plus a quarter of the tear and a fifth of the noise wobble, which is the excursion value
# noise actually reaches.
FITS="$(python3 - "$ROOT" <<'PYEOF'
import re, sys, pathlib
style = pathlib.Path(sys.argv[1], "Assets/CatchIfYouCan/Scripts/Art/PortalStyle.cs").read_text()
scene = pathlib.Path(sys.argv[1], "Assets/CatchIfYouCan/Scenes/01_MainMenu.unity").read_text()

def authored(name, default):
    # The SCENE wins where it says anything: a serialized value beats a code default, which is
    # the whole reason this portal spent a week being tuned in a file nothing read.
    m = re.search(r'^\s*' + name + r': ([\d.]+)\s*$', scene, re.M)
    if m:
        return float(m.group(1))
    m = re.search(r'public float ' + name + r' = ([\d.]+)f', style)
    return float(m.group(1)) if m else default

margin = authored('glowMargin', 0.0)
tear   = authored('tearAmount', 0.22)
rim    = authored('rimWidth', 0.26)
noise  = authored('noiseStrength', 0.16)

fit   = 1.0 / (1.0 + margin)
reach = ((1.0 + rim * 1.6) + tear * 0.25 + noise * 0.2) * fit
print("%.3f" % reach)
PYEOF
)"
if [ -n "$FITS" ] && awk "BEGIN{exit !($FITS < 1.0)}"; then
  ok "the rim finishes inside the quad it is drawn on (reaches $FITS of the edge)"
else
  bad "the rim finishes inside the quad it is drawn on" \
      "reaches $FITS of the quad edge; at or over 1.0 the glow is cut off square"
fi


# ---- V10: one value must not mean two things -------------------------------------------------
#
# ArrivalPoint is the van's player spawn and sits on the FLOOR, because that is where feet go.
# The portal's own reference is the surface CENTRE, half the opening's height up the wall.
# Pairing them directly stood the portal camera 1.2 m too low in the far world, and a camera
# that low makes the far floor ride up into the opening - reported, reasonably, as "the room
# behind the portal is too high". CLAUDE.md mistake 13, in geometry.
VA="$(code "$ENV/LobbyPortal.cs" | sed -n '/private Transform ResolveViewAnchor/,/^        }$/p')"
if printf '%s' "$VA" | grep -qE 'style\.openingSize\.y \* 0\.5f' &&
   printf '%s' "$VA" | grep -qE '_viewAnchor\.SetParent\(arrival, *false\)'; then
  ok "the view anchor is raised to the portal's own height"
else
  bad "the view anchor is raised to the portal's own height" \
      "the arrival point is where feet land, not where the far camera stands"
fi

# The arrival point itself must NOT be moved: the player's feet still belong on the floor.
if printf '%s' "$VA" | grep -qE '^\s*arrival\.(position|localPosition|Translate)'; then
  bad "the arrival point itself is left alone" \
      "moving it up would spawn the player inside the ceiling of the far room"
else
  ok "the arrival point itself is left alone"
fi

# ---- V10: the tear has to cut the collision, not just the picture ----------------------------
#
# The portal was a picture on an intact wall. The lobby's north wall is one solid box - 10.6 x
# 3.6 x 0.3 - and its collider stayed whole, so the player walked into a wall they could see
# through. A portal you cannot step into is a screen.
LP="$ENV/LobbyPortal.cs"
if code "$LP" | grep -qE 'private void EnsureWallAperture\(\)' &&
   code "$LP" | sed -n '/private void SetState(/,/^        }$/p' | grep -qE 'SetWallOpen\('; then
  ok "the opening is cut out of the wall's collision"
else
  bad "the opening is cut out of the wall's collision" \
      "without it the tear is a picture and the wall still stops the player"
fi

# The RENDERER is not touched. One wall, one mesh: a second quad in front of it is z-fighting,
# which is the runtime patch this portal already had once and had removed.
AP="$(code "$LP" | sed -n '/private void EnsureWallAperture/,/^        }$/p')"
if printf '%s' "$AP" | grep -qE 'MeshRenderer|MeshFilter|sharedMesh|\.material'; then
  bad "cutting the collision leaves the wall's geometry alone" \
      "the hole is physics only; touching the mesh brings the z-fighting patch back"
else
  ok "cutting the collision leaves the wall's geometry alone"
fi

# Closed is the resting state. A wall with a hole in it that no portal is holding open is a bug
# you fall through, so only Open and Entering may open it.
if code "$LP" | sed -n '/private void SetState(/,/^        }$/p' \
     | grep -qE 'SetWallOpen\(next == LobbyPortalState\.Open \|\| next == LobbyPortalState\.Entering\)'; then
  ok "the wall is solid again whenever the portal is not open"
else
  bad "the wall is solid again whenever the portal is not open" \
      "a collapsed portal must not leave a doorway shaped like nothing"
fi

# The aperture is unparented so a scale on the portal cannot multiply its box sizes - CLAUDE.md
# mistake 12 - which means it does not go with this object and has to be destroyed by hand.
if code "$LP" | sed -n '/private void OnDestroy/,/^        }$/p' | grep -qE 'Destroy\(_aperture\)'; then
  ok "the unparented aperture is destroyed with the portal"
else
  bad "the unparented aperture is destroyed with the portal" \
      "colliders shaped like a wall, in a scene with no wall"
fi

# The wall is found by looking, never by name. A hard-coded object name that stops resolving
# fails silently and forever, which this repository has done three times.
if code "$LP" | grep -qE 'Lobby_Wall|"Wall_North"'; then
  bad "the wall is found by geometry, not by name" \
      "a name that stops resolving is CLAUDE.md mistakes 3 and 10"
else
  ok "the wall is found by geometry, not by name"
fi

# ---- V10: ONE authoritative opening size -----------------------------------------------------
#
# There were two. openingSize said how big the hole was, and breachHalfSize scaled the oval
# inside it as a FRACTION - so the glow grew with the opening while the part you could walk
# through stayed the fraction of an opening nobody had updated. A portal 4.7 m wide with a
# breach still 1.4 m wide, and both numbers looked deliberate.
if code "$STYLE" | grep -qE 'breachHalfSize'; then
  bad "the opening size has exactly one source" \
      "breachHalfSize is a second size that can disagree with openingSize"
else
  ok "the opening size has exactly one source"
fi

# ...and the shader's fit is derived from the margin alone, never from a second authored size.
if code "$STYLE" | sed -n '/public Vector2 ResolveFit()/,/^        }$/p' \
     | grep -qE 'return new Vector2\(1f / scale, 1f / scale\);'; then
  ok "the shader breach is the opening, not a fraction of it"
else
  bad "the shader breach is the opening, not a fraction of it" \
      "anything else here reintroduces a hole smaller than the hole"
fi

# Every consumer reads that one value: the drawn quad, the collision hole, the crossing test
# and the threshold volume.
LP="$ENV/LobbyPortal.cs"
MISSING=""
code "$STYLE" | sed -n '/public Vector2 QuadSize()/,/^        }$/p' \
  | grep -qE 'openingSize\.x\)? \* scale' || MISSING="$MISSING quad"
code "$LP" | sed -n '/private void EnsureWallAperture/,/^        }$/p' \
  | grep -qE 'style\.openingSize\.x \* 0\.5f' || MISSING="$MISSING wall-hole"
code "$LP" | grep -qE 'halfWidth = style\.openingSize\.x \* 0\.5f \* apertureTolerance' \
  || MISSING="$MISSING crossing"
code "$LP" | sed -n '/private void EnsureThreshold/,/^        }$/p' \
  | grep -qE '_threshold\.size = new Vector3\(style\.openingSize\.x, style\.openingSize\.y' \
  || MISSING="$MISSING threshold"
if [ -z "$MISSING" ]; then
  ok "quad, collision hole, crossing test and threshold all read openingSize"
else
  bad "quad, collision hole, crossing test and threshold all read openingSize" \
      "these size themselves from something else:$MISSING"
fi

# The threshold must not carry its own width and height. That was a third opening size, and it
# was still 1.2 x 2.4 while the portal was 4.7 wide.
if code "$LP" | grep -qE 'entryTriggerSize'; then
  bad "the threshold volume has no size of its own" \
      "a trigger that is not the size of the hole is a third opening size"
else
  ok "the threshold volume has no size of its own"
fi

# ---- V10: the scene must not carry a field the code no longer has ----------------------------
#
# A serialized value ALWAYS beats a code default, which is why editing PortalStyle.cs changed
# nothing here: 01_MainMenu.unity said 1.06 and went on saying it. An orphaned breachHalfSize
# left in the scene is the same trap set for the next person.
if grep -qE '^\s*breachHalfSize:' "$SCENE"; then
  bad "the lobby scene carries no orphaned breach size" \
      "a serialized field the code dropped is dead weight that reads as configuration"
else
  ok "the lobby scene carries no orphaned breach size"
fi

if grep -qE '^\s*entryTriggerSize:' "$SCENE"; then
  bad "the lobby scene carries no orphaned trigger size" \
      "entryTriggerSize was replaced by entryTriggerDepth"
else
  ok "the lobby scene carries no orphaned trigger size"
fi

# ---- V10: the wall has to be found, and found reliably ---------------------------------------
#
# It was not. EnsureWallAperture ran in Awake and resolved the wall with a physics query, and a
# physics query reads the PHYSICS scene - which is only brought into line with the transform
# hierarchy at the fixed step. Before any sync, OverlapBox returns nothing, so the portal
# reported "no wall collider found" about a wall that was plainly there and had never moved.
LP="$ENV/LobbyPortal.cs"
if code "$LP" | sed -n '/private void Awake/,/^        }$/p' | grep -qE 'EnsureWallAperture\(\)'; then
  bad "the wall is cut after physics exists, not in Awake" \
      "a physics query in Awake runs against an unsynced physics scene and finds nothing"
else
  ok "the wall is cut after physics exists, not in Awake"
fi

if code "$LP" | sed -n '/private void Start/,/^        }$/p' | grep -qE 'EnsureWallAperture\(\)' &&
   code "$LP" | sed -n '/private Collider ResolveWall/,/^        }$/p' \
     | grep -qE 'Physics\.SyncTransforms\(\)'; then
  ok "the wall query syncs the physics scene first"
else
  bad "the wall query syncs the physics scene first" \
      "without the sync the answer depends on which frame it is asked in"
fi

# A wall is recognised by SHAPE - thin across the opening, wide and tall across it - so a floor
# and a prop are excluded without either being named.
RW="$(code "$LP" | sed -n '/private Collider ResolveWall/,/^        }$/p')"
if printf '%s' "$RW" | grep -qE 'thickness > maxWallThickness' &&
   printf '%s' "$RW" | grep -qE 'width < style\.openingSize\.x \|\| height < style\.openingSize\.y'; then
  ok "the wall is recognised by shape, not by size alone"
else
  bad "the wall is recognised by shape, not by size alone" \
      "without the thickness test a floor overlapping the opening wins"
fi

# A failed resolve has to say what it DID see, or the next report is "it says there is no wall"
# with nothing to act on - which is exactly how this one arrived.
if printf '%s' "$RW" | grep -qE 'Colliders overlapping it' &&
   printf '%s' "$RW" | grep -qE 'seen\.Append'; then
  ok "a failed wall resolve names the colliders it rejected"
else
  bad "a failed wall resolve names the colliders it rejected" \
      "an error with no candidates in it cannot be acted on"
fi

# The report TESTS the one claim that matters instead of asserting it.
RA="$(code "$LP" | sed -n '/private void ReportAperture/,/^        }$/p')"
if printf '%s' "$RA" | grep -qE 'Physics\.CheckBox' &&
   printf '%s' "$RA" | grep -qE 'centerBlocked=' &&
   printf '%s' "$RA" | grep -qE 'passable='; then
  ok "the portal reports a measured passability, not a claimed one"
else
  bad "the portal reports a measured passability, not a claimed one" \
      "centerBlocked must come from a physics test of the middle of the hole"
fi

# ---- V11: the crossing is a handover, not a load ---------------------------------------------
#
# Crossing used to run EnterAsync: fade to black, DESTROY the lobby player, build a new one at
# the van's spawn point, fade back in. Every symptom followed from that - a black frame because
# the fade is deliberate, and a fall because the van's spawn had nothing under it and the code
# says so in a warning nobody had read. The world behind the opening is already standing; there
# is nothing to hide and nobody to rebuild.
MWL="$ROOT/Assets/CatchIfYouCan/Scripts/Missions/MissionWorldLoader.cs"
LP="$ENV/LobbyPortal.cs"

if code "$LP" | sed -n '/private void BeginInvestigation/,/^        }$/p' \
     | grep -qE 'MissionWorldLoader\.EnterSeamlessAsync\('; then
  ok "the crossing takes the seamless path"
else
  bad "the crossing takes the seamless path" \
      "EnterAsync fades to black and rebuilds the player; the portal must not call it"
fi

if code "$LP" | sed -n '/private void BeginInvestigation/,/^        }$/p' \
     | grep -qE 'MissionWorldLoader\.EnterAsync\('; then
  bad "the crossing does not take the loading path" \
      "the prepared-world route must never reach the fade-and-respawn path"
else
  ok "the crossing does not take the loading path"
fi

# No fade, no reload, no regeneration, no second player - checked inside the seamless routine
# rather than hoped for.
SEAM="$(code "$MWL" | sed -n '/public static IEnumerator EnterSeamlessAsync/,/^        }$/p')"
FORBIDDEN=""
printf '%s' "$SEAM" | grep -qE 'FadeTo\(' && FORBIDDEN="$FORBIDDEN fade"
printf '%s' "$SEAM" | grep -qE 'LoadScene|LoadSceneAsync' && FORBIDDEN="$FORBIDDEN scene-load"
printf '%s' "$SEAM" | grep -qE 'PrepareAsync|GenerateHouse|PrepareWorld' && FORBIDDEN="$FORBIDDEN regenerate"
printf '%s' "$SEAM" | grep -qE 'PlayerSpawner\.Despawn|PlayerSpawner\.Spawn' && FORBIDDEN="$FORBIDDEN respawn"
if [ -z "$FORBIDDEN" ]; then
  ok "the seamless crossing neither fades, reloads, regenerates nor respawns"
else
  bad "the seamless crossing neither fades, reloads, regenerates nor respawns" \
      "found:$FORBIDDEN"
fi

# The SAME player walks through, moved by the rig's own Teleport - which zeroes the velocity,
# and that velocity is the fall accumulator.
if printf '%s' "$SEAM" | grep -qE 'motor\.Teleport\(finalPosition, mappedRotation\)'; then
  ok "the lobby player is carried through, not rebuilt"
else
  bad "the lobby player is carried through, not rebuilt" \
      "destroying and recreating the player is a teleport with a curtain over it"
fi

# The ground is PROVEN before anything is committed, and a miss refuses rather than drops.
if printf '%s' "$SEAM" | grep -qE 'Physics\.RaycastAll\(probeFrom, Vector3\.down' &&
   printf '%s' "$SEAM" | grep -qE 'failureReason=NO_GROUND_IN_DESTINATION' &&
   printf '%s' "$SEAM" | grep -qE 'onResult\?\.Invoke\(false\)'; then
  ok "no ground under the arrival refuses the crossing"
else
  bad "no ground under the arrival refuses the crossing" \
      "arriving in a world with no floor is worse than not arriving"
fi

# A refusal gives the controls back. The player is standing in the lobby with a doorway in
# front of them and no input; leaving them there is the worse half of the bug.
if code "$LP" | sed -n '/private void OnSeamlessEntryResult/,/^        }$/p' \
     | grep -qE 'MenuInputGate\.Pop' &&
   code "$LP" | sed -n '/private void OnSeamlessEntryResult/,/^        }$/p' \
     | grep -qE 'SetState\(LobbyPortalState\.Open\)'; then
  ok "a refused crossing returns the controls and reopens the doorway"
else
  bad "a refused crossing returns the controls and reopens the doorway" \
      "a refusal must not leave the player frozen in front of a dead portal"
fi

# The pose is mapped through the SAME transform the camera is posed with, or the player arrives
# half an opening's height from where the view they walked into said they would.
if printf '%s' "$SEAM" | grep -qE 'destinationAnchor\.localToWorldMatrix \*' &&
   printf '%s' "$SEAM" | grep -qE 'sourcePlane\.worldToLocalMatrix \*' &&
   code "$LP" | grep -qE 'surface\.SurfacePlane'; then
  ok "the player is mapped through the portal pair, not snapped to a spawn point"
else
  bad "the player is mapped through the portal pair, not snapped to a spawn point" \
      "lateral position and facing have to survive the doorway"
fi

# The curtain must not be raised on this route at all. It is a full-screen opaque CanvasGroup
# set during PREPARATION, so by the time the player crosses it has been at 1 for seconds.
IB="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"
if code "$IB" | grep -qE 'fadeOverlay\.alpha = _seamlessEntry \? 0f : 1f'; then
  ok "the prepared world raises no curtain"
else
  bad "the prepared world raises no curtain" \
      "an opaque overlay raised at preparation IS the black frame"
fi

# ---- V11: the player must change SCENE, not just position ------------------------------------
#
# PlayerRigBuilder creates the rig as `new GameObject("Player")` with no parent, so the player is
# a ROOT OBJECT of the scene that was active when they were built - the lobby. Unloading a scene
# destroys every root object in it, so carrying the lobby player through the portal and then
# unloading the lobby destroys the player, their controller and their camera. On screen that is
# "Display 1 - No cameras rendering" over the last frame that did render, with controls that stop
# responding because there is nothing left to control.
MWL="$ROOT/Assets/CatchIfYouCan/Scripts/Missions/MissionWorldLoader.cs"
SEAM="$(code "$MWL" | sed -n '/public static IEnumerator EnterSeamlessAsync/,/^        }$/p')"

if printf '%s' "$SEAM" | grep -qE 'SceneManager\.MoveGameObjectToScene\(playerRoot, missionScene\)'; then
  ok "the carried player is moved out of the lobby before it is unloaded"
else
  bad "the carried player is moved out of the lobby before it is unloaded" \
      "unloading a scene destroys its root objects, and the player is one of them"
fi

# ...and the move has to happen BEFORE the unload, not merely somewhere in the method.
MOVE_LINE="$(printf '%s' "$SEAM" | grep -n 'MoveGameObjectToScene' | head -1 | cut -d: -f1)"
UNLOAD_LINE="$(printf '%s' "$SEAM" | grep -n 'UnloadSceneAsync' | head -1 | cut -d: -f1)"
if [ -n "$MOVE_LINE" ] && [ -n "$UNLOAD_LINE" ] && [ "$MOVE_LINE" -lt "$UNLOAD_LINE" ]; then
  ok "the scene move happens before the unload"
else
  bad "the scene move happens before the unload" \
      "move=$MOVE_LINE unload=$UNLOAD_LINE - the order is the whole fix"
fi

# A parented player cannot be moved between scenes at all, so that is a refusal rather than a
# silent destruction two lines later.
if printf '%s' "$SEAM" | grep -qE 'failureReason=PLAYER_NOT_A_SCENE_ROOT'; then
  ok "a parented player refuses the crossing instead of being destroyed by it"
else
  bad "a parented player refuses the crossing instead of being destroyed by it" \
      "MoveGameObjectToScene requires a scene root; anything else is a silent loss"
fi

# The handover counts the cameras that can actually reach Display 1 and shouts when that is
# zero. A camera rendering into a texture - the portal's own - is not one of them.
if code "$MWL" | grep -qE 'private static void LogHandoff' &&
   code "$MWL" | grep -qE 'camera\.targetTexture == null && camera\.targetDisplay == 0' &&
   code "$MWL" | grep -qE 'NO CAMERA CAN RENDER DISPLAY 1'; then
  ok "the handover counts the cameras that can render Display 1"
else
  bad "the handover counts the cameras that can render Display 1" \
      "by the time a human sees the overlay the frame that caused it is gone"
fi

# The controls are given back explicitly, not by relying on the portal's OnDestroy firing in a
# useful order while its scene is being torn down.
if printf '%s' "$SEAM" | grep -qE 'MenuInputGate\.Pop\("LobbyPortal"\)'; then
  ok "the input gate is released by the handover itself"
else
  bad "the input gate is released by the handover itself" \
      "a player standing in a finished world unable to move"
fi

# The case card is a full-screen presenter on the same overlay. On the direct load it is the
# opening of a mission; on the portal route it is a title card dropped over a room the player is
# already standing in - which is the "short loading animation" that outlives every other fade.
IB="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"
if code "$IB" | grep -qE '_introPresenter = _seamlessEntry \? null : CaseIntroPresenter\.Ensure'; then
  ok "no case card is presented over a seamless arrival"
else
  bad "no case card is presented over a seamless arrival" \
      "a full-screen intro on the portal route is a loading screen by another name"
fi

# ---- V11: the lobby's kit walks through the doorway -------------------------------------------
#
# The lobby is the preparation area: the player walks around it, picks equipment up and carries
# it through. On the DIRECT load there is no lobby, so the mission must hand them a kit; on the
# portal route doing that puts back every item they deliberately left on the shelf.
IB="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"
WIRE="$(code "$IB" | sed -n '/private void WirePlayerEquipment/,/^        }$/p')"

if printf '%s' "$WIRE" | grep -qE 'if \(_seamlessEntry\)' &&
   printf '%s' "$WIRE" | grep -qE 'MissionEquipmentInstaller\.InstallLoadout\('; then
  ok "the mission installs a loadout only where there was no lobby to take one from"
else
  bad "the mission installs a loadout only where there was no lobby to take one from" \
      "the direct load still needs the kit; the portal route must keep what was carried"
fi

# The skip has to come BEFORE the install, or it is decoration.
SKIP_LINE="$(printf '%s' "$WIRE" | grep -n 'if (_seamlessEntry)' | head -1 | cut -d: -f1)"
INSTALL_LINE="$(printf '%s' "$WIRE" | grep -n 'InstallLoadout(' | head -1 | cut -d: -f1)"
if [ -n "$SKIP_LINE" ] && [ -n "$INSTALL_LINE" ] && [ "$SKIP_LINE" -lt "$INSTALL_LINE" ]; then
  ok "the carried loadout is kept before any starter kit is installed"
else
  bad "the carried loadout is kept before any starter kit is installed" \
      "skip=$SKIP_LINE install=$INSTALL_LINE"
fi

# The hand anchor is still wired on both routes - it is what holstered items hang from, and an
# unwired anchor drops everything the player was carrying on the floor of a scene being unloaded.
if printf '%s' "$WIRE" | grep -qE 'inventory\?\.SetHandAnchor\(handAnchor\)'; then
  ok "the hand anchor is wired on every route"
else
  bad "the hand anchor is wired on every route" \
      "held and holstered items hang from it; unwired, they are lost with the lobby"
fi

# Arriving empty-handed is allowed and indistinguishable from equipment lost in the handover, so
# it is said out loud rather than discovered in a dark house.
if code "$IB" | grep -qE 'private static void ReportCarriedEquipment' &&
   code "$IB" | grep -qE 'Handoff\] EQUIPMENT carried='; then
  ok "what the player walked in with is reported"
else
  bad "what the player walked in with is reported" \
      "an empty bag and a lost bag look identical without it"
fi

# ---- V12: thrown objects go through the doorway too -------------------------------------------
#
# A dropped item already flies - HeldEquipmentBase gives it a Rigidbody and throws it. What it
# could not do was arrive: the portal is a hole in one scene showing a second scene loaded beside
# it, so a thrown EMF reader landed on the lobby floor behind the picture, and was then destroyed
# with the lobby because Unequip unparents to world space and a dropped item is a scene ROOT.
OT="$ROOT/Assets/CatchIfYouCan/Scripts/Environment/PortalObjectTransfer.cs"
PT="$ROOT/Assets/CatchIfYouCan/Scripts/Environment/PortalTransferable.cs"

for f in "$OT" "$PT"; do
  [ -f "$f" ] || bad "$(basename "$f") exists" "thrown objects cannot cross without it"
done

# The SAME object changes scene. Destroying and rebuilding it would reset battery, tier, on/off
# state and ownership - every runtime value survives here by never being touched.
if code "$OT" | grep -qE 'SceneManager\.MoveGameObjectToScene\(go, to\)' &&
   ! code "$OT" | grep -qE 'Instantiate\(|Destroy\(go'; then
  ok "a crossing object is moved, never rebuilt"
else
  bad "a crossing object is moved, never rebuilt" \
      "rebuilding it resets every runtime value it was carrying"
fi

# Momentum survives. A thrown object has to keep going on the far side rather than stopping at
# the plane, and its direction is expressed in the source portal's frame.
if code "$OT" | grep -qE 'body\.linearVelocity = through\.MultiplyVector\(velocity\)' &&
   code "$OT" | grep -qE 'body\.angularVelocity = through\.MultiplyVector\(spin\)'; then
  ok "velocity and spin are rotated through the pair, not zeroed"
else
  bad "velocity and spin are rotated through the pair, not zeroed" \
      "an object that stops at the plane did not go through it"
fi

# A CROSSING, not an overlap, and not a plane that extends past the hole.
if code "$OT" | grep -qE 'if \(previous <= 0f \|\| side > 0f\)' &&
   code "$OT" | grep -qE 'private bool InsideAperture'; then
  ok "objects cross on a sign change inside the opening"
else
  bad "objects cross on a sign change inside the opening" \
      "an infinite plane carries objects through solid wall two metres away"
fi

# Ping-pong suppression: an object straddling the plane must not bounce between scenes.
if code "$OT" | grep -qE 'Time\.time - item\.LastTransferTime < CooldownSeconds' &&
   code "$OT" | grep -qE 'duplicate-cross suppressed'; then
  ok "a straddling object is not carried twice"
else
  bad "a straddling object is not carried twice" \
      "without a cooldown an object resting in the hole flips scenes every step"
fi

# Eligibility is DECLARED, never inferred from having a Rigidbody. Sweeping for bodies near the
# opening eventually picks up lobby furniture, a particle, or the portal's own wall colliders.
if code "$OT" | grep -qE 'PortalTransferable\.All' &&
   ! code "$OT" | grep -qE 'FindObjectsByType<Rigidbody>|GetComponentsInChildren<Rigidbody>'; then
  ok "only declared objects are carried"
else
  bad "only declared objects are carried" \
      "an object teleported into a house because it happened to have a body"
fi

# The mark goes on when the item is thrown and comes off when it is caught - an item in a hand
# travels with the player and must not be carried a second time.
HE="$ROOT/Assets/CatchIfYouCan/Scripts/Equipment/HeldEquipmentBase.cs"
if code "$HE" | sed -n '/private void StartPhysics/,/^        }$/p' \
     | grep -qE 'PortalTransferable\.Mark\(gameObject\)' &&
   code "$HE" | sed -n '/protected void ReleasePhysics/,/^        }$/p' \
     | grep -qE 'PortalTransferable\.Unmark\(gameObject\)'; then
  ok "an item is transferable only while it is in flight"
else
  bad "an item is transferable only while it is in flight" \
      "a held item would be carried through twice, once in a hand and once by itself"
fi

# The lobby must not be unloaded on top of something that crossed into it.
MWL="$ROOT/Assets/CatchIfYouCan/Scripts/Missions/MissionWorldLoader.cs"
SEAM="$(code "$MWL" | sed -n '/public static IEnumerator EnterSeamlessAsync/,/^        }$/p')"
CHECK_LINE="$(printf '%s' "$SEAM" | grep -n 'CountStillOwnedBy' | head -1 | cut -d: -f1)"
UNLOAD_LINE="$(printf '%s' "$SEAM" | grep -n 'UnloadSceneAsync' | head -1 | cut -d: -f1)"
if [ -n "$CHECK_LINE" ] && [ -n "$UNLOAD_LINE" ] && [ "$CHECK_LINE" -lt "$UNLOAD_LINE" ]; then
  ok "stranded objects are counted before the lobby is unloaded"
else
  bad "stranded objects are counted before the lobby is unloaded" \
      "check=$CHECK_LINE unload=$UNLOAD_LINE - after the unload there is nothing left to count"
fi

# A portal carries only what is on ITS side. PortalTransferable's registry is global, so without
# a scene test an object that has already gone through keeps being measured against the plane it
# left - and its position in the destination world has nothing to do with that plane, so it
# eventually reads as a second crossing and is carried again, from a place it is not.
if code "$OT" | grep -qE 'Scene sourceScene = _sourcePlane\.gameObject\.scene' &&
   code "$OT" | grep -qE 'item\.gameObject\.scene != sourceScene'; then
  ok "a portal only carries objects from its own scene"
else
  bad "a portal only carries objects from its own scene" \
      "an object already through would be measured against the plane it left"
fi

# ---- V13: the portal opens onto a ROOM, not onto the van --------------------------------------
#
# ArrivalPoint is the van's player spawn: it stands OUTSIDE, next to the vehicle, because on the
# direct load you arrive at the van and walk to the house. There is no terrain in this project,
# so there is no floor under it - and the portal was aimed at it. A player carried there dropped
# forever past a skybox, which is the mountain view and then the fall.
IB="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"

if code "$IB" | grep -qE 'public Transform MissionEntryAnchor => _missionEntry;' &&
   code "$ENV/LobbyPortal.cs" | grep -qE '_pendingWorld\.MissionEntryAnchor'; then
  ok "the portal opens onto the mission entry anchor"
else
  bad "the portal opens onto the mission entry anchor" \
      "the van's spawn is outdoors on ground that does not exist"
fi

# ONE transform feeds the preview camera and the crossing. Two would let what is shown and what
# is walked into diverge, which is the failure this whole flow exists to prevent.
BINDS="$(code "$ENV/LobbyPortal.cs" | grep -c 'ResolveViewAnchor(_pendingWorld.MissionEntryAnchor)')"
if [ "$BINDS" -ge 1 ] && ! code "$ENV/LobbyPortal.cs" | grep -qE 'SetDestination\([^)]*ArrivalPoint'; then
  ok "preview and entry cannot diverge"
else
  bad "preview and entry cannot diverge" \
      "one anchor feeds both, or the image is a promise the crossing does not keep"
fi

# The anchor is PROVEN against collision, not taken from a transform's name. A room's Root is
# the room's origin, which is not necessarily its floor.
# Both halves: the room chain and the floor probe it delegates to. Scoping this to one method
# made three checks fail when the probe moved into TryAnchorInRoom, with the invariant unchanged.
EMA="$(code "$IB" | sed -n '/private void EnsureMissionEntryAnchor/,/private void SpawnGhost/p')"
if printf '%s' "$EMA" | grep -qE 'Physics\.RaycastAll\(root \+ Vector3\.up \* 3f, Vector3\.down' &&
   printf '%s' "$EMA" | grep -qE 'Physics\.SyncTransforms\(\)'; then
  ok "the entry anchor is measured against real floor collision"
else
  bad "the entry anchor is measured against real floor collision" \
      "a transform named PlayerSpawn is a name, not a floor"
fi

# A miss leaves the anchor NULL, and null refuses the crossing. Placing it anyway would be the
# fall with extra steps.
if printf '%s' "$EMA" | grep -qE 'no mission entry ' &&
   ! printf '%s' "$EMA" | grep -qE 'go\.transform\.position = root;'; then
  ok "no floor under the entrance leaves the anchor null rather than guessed"
else
  bad "no floor under the entrance leaves the anchor null rather than guessed" \
      "an anchor placed without a floor under it is the fall with extra steps"
fi

# ---- V13: ground must belong to the world being entered ---------------------------------------
#
# Physics queries are GLOBAL across every loaded scene, and during a crossing both are loaded.
# A plain cast therefore accepts the lobby's own floor - and the lobby carries a 40 x 40 m
# safety floor that sits under very nearly anywhere. The crossing was validated against a
# surface that was unloaded seconds later, so the player landed and then fell.
MWL="$ROOT/Assets/CatchIfYouCan/Scripts/Missions/MissionWorldLoader.cs"
IB="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/InvestigationBootstrap.cs"
SEAM="$(code "$MWL" | sed -n '/public static IEnumerator EnterSeamlessAsync/,/^        }$/p')"

if printf '%s' "$SEAM" | grep -qE 'candidate\.collider\.gameObject\.scene != destinationScene' &&
   printf '%s' "$SEAM" | grep -qE 'failureReason=NO_GROUND_IN_DESTINATION'; then
  ok "the crossing only stands on ground in the destination scene"
else
  bad "the crossing only stands on ground in the destination scene" \
      "the lobby's safety floor validates almost any arrival and is then unloaded"
fi

# Both halves: the room chain and the floor probe it delegates to. Scoping this to one method
# made three checks fail when the probe moved into TryAnchorInRoom, with the invariant unchanged.
EMA="$(code "$IB" | sed -n '/private void EnsureMissionEntryAnchor/,/private void SpawnGhost/p')"
if printf '%s' "$EMA" | grep -qE 'hits\[i\]\.collider\.gameObject\.scene != here'; then
  ok "the entry anchor only stands on ground in its own scene"
else
  bad "the entry anchor only stands on ground in its own scene" \
      "anchoring the doorway to the lobby's floor moves the hole to a scene that is unloaded"
fi

# Nearest hit wins. RaycastAll does not sort, and standing on the roof of something is not
# standing on the floor under it.
if printf '%s' "$SEAM" | grep -qE 'candidate\.distance < ground\.distance' &&
   printf '%s' "$EMA" | grep -qE 'hits\[i\]\.distance < floor\.distance'; then
  ok "the nearest valid surface is chosen, not the first one returned"
else
  bad "the nearest valid surface is chosen, not the first one returned" \
      "RaycastAll is unsorted; the first hit can be a roof"
fi

# ---- V13: an unbound destination collapses the doorway -----------------------------------------
#
# OpenRoutine reads "no destination bound by the end of the opening" as a failed preparation and
# answers by destabilising - correctly, because a doorway onto nothing should not stay open. So
# anything that can leave MissionEntryAnchor null makes the portal close a second after opening,
# and the cause is a room without a floor rather than anything in the portal itself.
#
# The anchor therefore tries EVERY room, entrance first, instead of the entrance alone.
if printf '%s' "$EMA" | grep -qE '_generatedHouse\.Rooms\[i\]' &&
   printf '%s' "$EMA" | grep -qE 'candidates\.Add\(_generatedHouse\.Entrance\)'; then
  ok "the entry anchor tries every room, entrance first"
else
  bad "the entry anchor tries every room, entrance first" \
      "one room without a floor must not collapse the doorway"
fi

# A total failure is still a refusal, and it names what it tried - "the portal closed again" is
# not a report anybody can act on.
if printf '%s' "$EMA" | grep -qE 'No room in the generated house has a floor' &&
   printf '%s' "$EMA" | grep -qE 'Tried:'; then
  ok "a failed anchor names every room it rejected"
else
  bad "a failed anchor names every room it rejected" \
      "without it the only symptom is a doorway that shuts itself"
fi

# ---- V14: the house the portal opens onto belongs to the MISSION scene -------------------------
#
# `new GameObject` puts the object in the ACTIVE scene, which is not the scene the component
# creating it lives in. While the lobby portal prepares the mission world, the investigation
# scene is loaded ADDITIVELY and the lobby stays active - so the generator's own house root was
# created in the LOBBY, and every room in the house hung off it.
#
# One cause, two faces, and neither of them looks like a scene-ownership bug:
#   - the entry anchor proves its floor by asking whether the collider belongs to its own scene
#     (it must: a physics query is global across every loaded scene, and the lobby's 40 x 40 m
#     safety floor sits under nearly anywhere). Every room failed that test, the anchor stayed
#     null, and the doorway read a null destination as a failed preparation and shut itself.
#   - had it opened, unloading the lobby would have taken the house with it, so the player
#     would have arrived in a room and then fallen through the world.
GEN="$ROOT/Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs"

if code "$GEN" | tr '\n' ' ' | tr -s ' ' | grep -qE 'MoveGameObjectToScene\( ?houseRoot\.gameObject, gameObject\.scene\)'; then
  ok "the generated house root is moved into the generator's own scene"
else
  bad "the generated house root is moved into the generator's own scene" \
      "a root created with new GameObject lands in the ACTIVE scene, which during a portal preparation is the lobby"
fi

# Only a scene ROOT can be moved, and only one this class owns - a houseRoot wired in the
# inspector is already inside a hierarchy and must be left alone.
if code "$GEN" | tr '\n' ' ' | grep -qE 'houseRoot\.parent == null'; then
  ok "only an unparented root this class created is relocated"
else
  bad "only an unparented root this class created is relocated" \
      "moving a wired houseRoot would tear it out of the hierarchy it was authored in"
fi

# Awake alone is not enough: it runs INSIDE AddComponent, before the caller has re-parented the
# generator into the mission scene, so the scene it compared against then was the wrong one.
if code "$GEN" | sed -n '/public GeneratedHouse Instantiate(HouseLayout layout)/,/ClearExisting();/p' \
   | grep -qE 'EnsureRoots\(\);'; then
  ok "the scene of the house root is re-checked when a layout is built, not only in Awake"
else
  bad "the scene of the house root is re-checked when a layout is built, not only in Awake" \
      "Awake runs inside AddComponent, one line above the SetParent that decides the scene"
fi

# The same ordering trap, at the call site. SetParent must come BEFORE AddComponent, because
# Awake runs synchronously inside AddComponent and anything it creates lands in the active scene.
EM="$(code "$IB" | sed -n '/private void EnsureManagers/,/PlayerFactory\.EnsureMobileInput/p')"
GEN_PARENT="$(printf '%s\n' "$EM" | grep -n 'SetParent(runtimeParent, false)' | head -1 | cut -d: -f1)"
GEN_ADD="$(printf '%s\n' "$EM" | grep -n 'AddComponent<ProceduralHouseGenerator>' | head -1 | cut -d: -f1)"
if [ -n "$GEN_PARENT" ] && [ -n "$GEN_ADD" ] && [ "$GEN_PARENT" -lt "$GEN_ADD" ]; then
  ok "the generator is parented into the world root before its component is added"
else
  bad "the generator is parented into the world root before its component is added" \
      "AddComponent runs Awake synchronously; a root it creates lands in the ACTIVE scene, which is the lobby"
fi

# The ghost spawn manager was the second one: created unparented, so it belonged to the lobby and
# was destroyed by the unload the moment the player arrived.
GSM_PARENT="$(printf '%s\n' "$EM" | grep -n 'SetParent(runtimeParent, false)' | sed -n '2p' | cut -d: -f1)"
GSM_ADD="$(printf '%s\n' "$EM" | grep -n 'AddComponent<GhostSpawnManager>' | head -1 | cut -d: -f1)"
if [ -n "$GSM_PARENT" ] && [ -n "$GSM_ADD" ] && [ "$GSM_PARENT" -lt "$GSM_ADD" ]; then
  ok "the ghost spawn manager is parented into the world root before its component is added"
else
  bad "the ghost spawn manager is parented into the world root before its component is added" \
      "an unparented runtime manager is owned by the lobby and dies with it"
fi

# A rejected collider is NAMED with the scene it belongs to. "No floor at (0,0,0)" was reported
# for rooms that plainly had one, and the message could not distinguish "the ray hit nothing" from
# "the ray hit the house, and the house is in the wrong scene". That ambiguity is what cost the
# time, not the bug.
if printf '%s' "$EMA" | grep -qE 'rejected as foreign' &&
   printf '%s' "$EMA" | grep -qE 'gameObject\.scene\.name'; then
  ok "a rejected floor is named together with the scene that owns it"
else
  bad "a rejected floor is named together with the scene that owns it" \
      "'no floor' covers both an empty ray and a house in the wrong scene, and they need different fixes"
fi

echo
echo "  $PASS passed, $FAIL failed"
if [ "$FAIL" -ne 0 ]; then
  echo "UI AND PORTAL GUARD FAILED"
  exit 1
fi
echo "UI AND PORTAL GUARD PASSED"
