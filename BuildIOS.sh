#!/usr/bin/env bash
# CATCH IF YOU CAN — macOS iOS / Xcode export (v2)
# Works with any installed Unity 6.x (requires 6000.5.10f1; prefers it strictly).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"
OUT_DIR="$PROJECT_DIR/Builds/iOS"
LOG_DIR="$PROJECT_DIR/Builds/Logs"
mkdir -p "$OUT_DIR" "$LOG_DIR"

UNITY_BIN="${UNITY_BIN:-}"
ALLOW_ANY_UNITY="${ALLOW_ANY_UNITY:-1}"

list_unity_editors() {
  local hub="/Applications/Unity/Hub/Editor"
  local results=()

  if [[ -d "$hub" ]]; then
    while IFS= read -r -d '' app; do
      local bin="$app/Contents/MacOS/Unity"
      if [[ -x "$bin" ]]; then
        results+=("$bin")
      fi
    done < <(find "$hub" -maxdepth 2 -type d -name 'Unity.app' -print0 2>/dev/null)
  fi

  # Standalone / custom installs
  if [[ -x "/Applications/Unity/Unity.app/Contents/MacOS/Unity" ]]; then
    results+=("/Applications/Unity/Unity.app/Contents/MacOS/Unity")
  fi

  printf '%s\n' "${results[@]}" | awk 'NF' | sort -u
}

pick_unity() {
  if [[ -n "$UNITY_BIN" ]]; then
    if [[ -x "$UNITY_BIN" ]]; then
      echo "$UNITY_BIN"
      return 0
    fi
    echo "ERROR: UNITY_BIN is set but is not executable: $UNITY_BIN" >&2
    return 1
  fi

  local all
  all="$(list_unity_editors || true)"
  if [[ -z "$all" ]]; then
    return 1
  fi

  # Required baseline: Unity 6.5 == 6000.5.10f1 (exact).
  local preferred
  preferred="$(printf '%s\n' "$all" | grep '/6000\.5\.10f1/' | sort -V | tail -n 1 || true)"
  if [[ -n "$preferred" ]]; then
    echo "$preferred"
    return 0
  fi

  # Then any 6000.5.x - tolerated with a warning below, not endorsed.
  preferred="$(printf '%s\n' "$all" | grep '/6000\.5\.' | sort -V | tail -n 1 || true)"
  if [[ -n "$preferred" ]]; then
    echo "$preferred"
    return 0
  fi

  # Then any Unity 6 (6000.x)
  preferred="$(printf '%s\n' "$all" | grep '/6000\.' | sort -V | tail -n 1 || true)"
  if [[ -n "$preferred" ]]; then
    echo "$preferred"
    return 0
  fi

  if [[ "$ALLOW_ANY_UNITY" == "1" ]]; then
    printf '%s\n' "$all" | sort -V | tail -n 1
    return 0
  fi

  return 1
}

print_install_help() {
  cat <<'EOF'

============================================================
Unity was not found.
============================================================

1) Open Unity Hub:
   open -a "Unity Hub"

2) Install:
   - Unity 6.5 (6000.5.10f1) - exactly this version
   - Module: iOS Build Support
   - Android Build Support only if you also need it

3) Then run again:
   ./BuildIOS.sh

OR manually, through the GUI:
   - Unity Hub -> Add -> select this repository folder
   - Open it with Unity 6000.5.10f1
   - Menu: Catch If You Can -> 5. BUILD -> iOS
   - Then: open Builds/iOS

If Unity is installed somewhere else:
   UNITY_BIN="/Applications/Unity/Hub/Editor/XXXX/Unity.app/Contents/MacOS/Unity" ./BuildIOS.sh

Unity installations found on this machine, if any:
EOF
  list_unity_editors || echo "  (none)"
}

UNITY="$(pick_unity || true)"
if [[ -z "${UNITY:-}" ]]; then
  print_install_help
  # Soft-fail: still open helpful docs / Hub if possible
  if [[ -d "/Applications/Unity Hub.app" ]]; then
    echo ""
    echo "Opening Unity Hub..."
    open -a "Unity Hub" || true
  fi
  open "$PROJECT_DIR/DEPLOY_IOS.md" 2>/dev/null || true
  exit 1
fi

UNITY_VERSION_DIR="$(basename "$(dirname "$(dirname "$(dirname "$UNITY")")")")"
echo "Using Unity: $UNITY"
echo "Version dir: $UNITY_VERSION_DIR"
echo "Project:     $PROJECT_DIR"
echo "Output:      $OUT_DIR"

# Warn if this is not the pinned editor version
if [[ "$UNITY_VERSION_DIR" != "6000.5.10f1" ]]; then
  echo "WARNING: Unity 6000.5.10f1 is required. Found: $UNITY_VERSION_DIR"
  echo "         Another version rewrites ProjectVersion.txt and changes the"
  echo "         toolchain underneath the determinism baseline."
  echo "         Building anyway."
fi

# Clean previous iOS export
find "$OUT_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} + 2>/dev/null || true

LOG_FILE="$LOG_DIR/ios_build_$(date +%Y%m%d_%H%M%S).log"

set +e
"$UNITY" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$PROJECT_DIR" \
  -buildTarget iOS \
  -executeMethod CatchIfYouCan.EditorTools.CatchIfYouCanBuildMenu.BuildIOSBatch \
  -logFile "$LOG_FILE"
UNITY_EXIT=$?
set -e

echo "Unity exit code: $UNITY_EXIT"
echo "Unity log: $LOG_FILE"

XCODE_PROJ="$(find "$OUT_DIR" -name '*.xcodeproj' 2>/dev/null | head -n 1 || true)"
if [[ -z "$XCODE_PROJ" ]]; then
  echo ""
  echo "ERROR: no .xcodeproj under $OUT_DIR."
  echo "Common causes:"
  echo "  - the iOS Build Support module is missing in Unity Hub"
  echo "  - the first project import needs the GUI (licence / package resolve)"
  echo ""
  echo "GUI fallback:"
  echo "  1) open -a \"Unity Hub\""
  echo "  2) Add the project: $PROJECT_DIR"
  echo "  3) Open it, then: Catch If You Can -> 5. BUILD -> iOS"
  echo ""
  if [[ -f "$LOG_FILE" ]]; then
    echo "----- last 60 log lines -----"
    tail -n 60 "$LOG_FILE" || true
  fi
  exit 2
fi

echo ""
echo "=== SUCCESS ==="
echo "Xcode project: $XCODE_PROJ"

# Safe open (no zsh nomatch)
open "$XCODE_PROJ" || true

ZIP_OUT="$PROJECT_DIR/Builds/CATCH_IF_YOU_CAN_Xcode_iOS.zip"
rm -f "$ZIP_OUT"
(
  cd "$OUT_DIR"
  zip -r -q "$ZIP_OUT" .
)
echo "Xcode export zip: $ZIP_OUT"
echo ""
echo "In Xcode:"
echo "  1. Signing & Capabilities -> Team"
echo "  2. Bundle ID: com.catchifyoucan.game"
echo "  3. Connect the iPhone -> Product -> Run"
