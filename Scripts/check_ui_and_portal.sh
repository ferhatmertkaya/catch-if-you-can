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

echo
echo "  $PASS passed, $FAIL failed"
if [ "$FAIL" -ne 0 ]; then
  echo "UI AND PORTAL GUARD FAILED"
  exit 1
fi
echo "UI AND PORTAL GUARD PASSED"
