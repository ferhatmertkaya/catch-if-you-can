#!/usr/bin/env bash
# Opens the project in Unity Hub / Unity (GUI route, no batch mode).
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"

echo "Projekt: $PROJECT_DIR"

# Prefer Unity Hub add/open
if [[ -d "/Applications/Unity Hub.app" ]]; then
  echo "Opening Unity Hub..."
  open -a "Unity Hub" || true
fi

# Try to open project with latest Unity via Hub deep link style / open -a
# Listing editors
HUB="/Applications/Unity/Hub/Editor"
UNITY_APP=""
if [[ -d "$HUB" ]]; then
  # Required baseline 6000.5.10f1, then any 6000.5.x, then any 6000.x
  UNITY_APP="$(find "$HUB" -maxdepth 2 -type d -name 'Unity.app' | grep '/6000.5.10f1/' | sort -V | tail -n 1 || true)"
  if [[ -z "$UNITY_APP" ]]; then
    UNITY_APP="$(find "$HUB" -maxdepth 2 -type d -name 'Unity.app' | grep '/6000.5.' | sort -V | tail -n 1 || true)"
  fi
  if [[ -z "$UNITY_APP" ]]; then
    UNITY_APP="$(find "$HUB" -maxdepth 2 -type d -name 'Unity.app' | grep '/6000.' | sort -V | tail -n 1 || true)"
  fi
  if [[ -z "$UNITY_APP" ]]; then
    UNITY_APP="$(find "$HUB" -maxdepth 2 -type d -name 'Unity.app' | sort -V | tail -n 1 || true)"
  fi
fi

if [[ -n "$UNITY_APP" && -d "$UNITY_APP" ]]; then
  echo "Opening the project with: $UNITY_APP"
  open -a "$UNITY_APP" --args -projectPath "$PROJECT_DIR" || true
else
  echo "Kein Unity Editor gefunden."
  echo "In Unity Hub: Add → $PROJECT_DIR"
  open "$PROJECT_DIR" || true
fi

cat <<EOF

Then, in Unity:
  1) Catch If You Can -> 5. BUILD -> iOS
  2) Open the Builds/iOS folder in Xcode

  Ghost visuals are build products and are not committed. If ghosts spawn as
  placeholder capsules, run this once:
     Catch If You Can -> 9. ENTWICKLER - DEBUG -> Migration
                      -> Integrate External Assets

EOF
