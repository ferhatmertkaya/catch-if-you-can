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

PASS=0
FAIL=0

ok ()   { PASS=$((PASS+1)); printf '  ok    %s\n' "$1"; }
bad ()  { FAIL=$((FAIL+1)); printf '  FAIL  %s\n' "$1"; [ $# -gt 1 ] && printf '        %s\n' "$2"; }

# Code with every full-line comment removed. A guard that greps a whole file is a guard a
# doc comment can satisfy, and one that forbids a call will match the comment warning
# against it - this project has been bitten by both directions.
code () { sed 's://.*::' "$1" | grep -v '^[[:space:]]*\*' ; }

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

# The silhouette. A rectangular distance field is exactly the bug being fixed, so the shader
# has to derive its mask from a length() and must not go back to the per-axis minimum.
if grep -qE 'float +r *= *length\(' "$SHADER"; then
  ok "the portal mask is an ellipse, not a box"
else
  bad "the portal mask is an ellipse, not a box" \
      "the rim must come from a radial distance field, not from min(uv, 1-uv)"
fi

if grep -qE 'min\(IN\.uv, *1\.0 *- *IN\.uv\)' "$SHADER"; then
  bad "the rectangular distance field is gone" \
      "min(uv, 1-uv) is a box; that is what made the portal a glowing rectangle"
else
  ok "the rectangular distance field is gone"
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
MISMATCH="$(python3 - "$SHADER" "$MAT" <<'PYEOF'
import re, sys
sh = open(sys.argv[1]).read()
block = sh[sh.index("Properties"):sh.index("SubShader")]
props = set(re.findall(r"^\s*(?:\[[^\]]*\]\s*)?(_\w+)\s*\(", block, re.M))
mat = set(re.findall(r"^\s*-\s*(_\w+):", open(sys.argv[2]).read(), re.M))
print(" ".join(sorted(props ^ mat)))
PYEOF
)"
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

# The effects emit on the oval. A Box the size of the doorway put wisps through the middle of
# the view, and a plain Circle is round around a 1.06 x 2.4 opening.
if code "$FX" | grep -qE 'ParticleSystemShapeType\.Circle' &&
   code "$FX" | grep -qE 'shape\.radiusThickness = 0f' &&
   code "$FX" | grep -qE 'shape\.scale'; then
  ok "particles emit on the oval contour, not in a box"
else
  bad "particles emit on the oval contour, not in a box" \
      "a Box emitter fires through the view; an unscaled Circle is round"
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
PERFRAME_LOG="$(python3 - "$ENV/LobbyPortal.cs" <<'PYEOF'
import re, sys
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
PYEOF
)"
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

echo
echo "  $PASS passed, $FAIL failed"
if [ "$FAIL" -ne 0 ]; then
  echo "UI AND PORTAL GUARD FAILED"
  exit 1
fi
echo "UI AND PORTAL GUARD PASSED"
