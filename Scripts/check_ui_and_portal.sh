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

echo
echo "  $PASS passed, $FAIL failed"
if [ "$FAIL" -ne 0 ]; then
  echo "UI AND PORTAL GUARD FAILED"
  exit 1
fi
echo "UI AND PORTAL GUARD PASSED"
