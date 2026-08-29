#!/usr/bin/env bash
# Öffnet das Projekt in Unity Hub / Unity (GUI-Pfad ohne Batchmode)
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"

echo "Projekt: $PROJECT_DIR"

# Prefer Unity Hub add/open
if [[ -d "/Applications/Unity Hub.app" ]]; then
  echo "Öffne Unity Hub…"
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
  echo "Öffne Projekt mit: $UNITY_APP"
  open -a "$UNITY_APP" --args -projectPath "$PROJECT_DIR" || true
else
  echo "Kein Unity Editor gefunden."
  echo "In Unity Hub: Add → $PROJECT_DIR"
  open "$PROJECT_DIR" || true
fi

cat <<EOF

Danach in Unity:
  1) Catch If You Can → Setup Project
  2) Catch If You Can → Generate Placeholder Prefabs  (optional)
  3) Catch If You Can → Build iOS
  4) Ordner Builds/iOS in Xcode öffnen

EOF
