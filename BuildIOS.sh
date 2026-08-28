#!/usr/bin/env bash
# CATCH IF YOU CAN — macOS iOS / Xcode export (v2)
# Works with any installed Unity 6.x (prefers 6000.3 LTS).
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
    echo "ERROR: UNITY_BIN ist gesetzt, aber nicht ausführbar: $UNITY_BIN" >&2
    return 1
  fi

  local all
  all="$(list_unity_editors || true)"
  if [[ -z "$all" ]]; then
    return 1
  fi

  # Prefer Unity 6.3 LTS (6000.3.x)
  local preferred
  preferred="$(printf '%s\n' "$all" | grep '/6000\.3\.' | sort -V | tail -n 1 || true)"
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
Unity wurde nicht gefunden.
============================================================

1) Unity Hub öffnen:
   open -a "Unity Hub"

2) Installieren:
   - Unity 6.3 LTS (oder Unity 6.x)
   - Module: iOS Build Support
   - (empfohlen) Android Build Support nur falls nötig

3) Danach erneut:
   ./BuildIOS.sh

ODER manuell (GUI, ohne Script):
   - Unity Hub → Add → diesen CatchIfYouCan Ordner wählen
   - Mit Unity 6.x öffnen
   - Menü: Catch If You Can → Setup Project
   - Menü: Catch If You Can → Build iOS
   - Danach: open Builds/iOS

Falls Unity schon installiert ist, aber woanders liegt:
   UNITY_BIN="/Applications/Unity/Hub/Editor/XXXX/Unity.app/Contents/MacOS/Unity" ./BuildIOS.sh

Gefundene Unity-Installationen (falls vorhanden):
EOF
  list_unity_editors || echo "  (keine)"
}

UNITY="$(pick_unity || true)"
if [[ -z "${UNITY:-}" ]]; then
  print_install_help
  # Soft-fail: still open helpful docs / Hub if possible
  if [[ -d "/Applications/Unity Hub.app" ]]; then
    echo ""
    echo "Öffne Unity Hub…"
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

# Warn if not 6.3
if [[ "$UNITY_VERSION_DIR" != 6000.3.* ]]; then
  echo "WARNUNG: Empfohlen ist Unity 6000.3.x LTS. Gefunden: $UNITY_VERSION_DIR"
  echo "         Build wird trotzdem versucht."
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
  echo "ERROR: Kein .xcodeproj unter $OUT_DIR."
  echo "Häufige Ursachen:"
  echo "  - iOS Build Support Modul fehlt in Unity Hub"
  echo "  - Erster Projektimport braucht GUI (License / Package resolve)"
  echo ""
  echo "Fallback GUI-Build:"
  echo "  1) open -a \"Unity Hub\""
  echo "  2) Projekt hinzufügen: $PROJECT_DIR"
  echo "  3) Öffnen → Catch If You Can → Setup Project"
  echo "  4) Catch If You Can → Build iOS"
  echo ""
  if [[ -f "$LOG_FILE" ]]; then
    echo "----- letzte 60 Log-Zeilen -----"
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
echo "  1. Signing & Capabilities → Team"
echo "  2. Bundle ID: com.catchifyoucan.game"
echo "  3. iPhone anschließen → Product → Run"
