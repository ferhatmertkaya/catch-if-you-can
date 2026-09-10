#!/usr/bin/env bash
# Finder double-click entry point for macOS: shows the requirements, then builds.
cd "$(dirname "$0")"
chmod +x BuildIOS.sh OpenInUnity.sh 2>/dev/null || true

clear
cat <<'EOF'
╔══════════════════════════════════════════════════════╗
║         CATCH IF YOU CAN — iOS Deploy v2             ║
╚══════════════════════════════════════════════════════╝

This script builds the Xcode project with Unity.

Requires:
  - Unity Hub
  - Unity 6.5 (6000.5.10f1) - exactly this version
  - Module: iOS Build Support
  - Xcode 15 or newer

EOF

if [[ ! -d "/Applications/Unity Hub.app" ]] && [[ ! -d "/Applications/Unity/Hub" ]]; then
  echo "Unity Hub does not appear to be installed."
  echo "Download: https://unity.com/download"
  echo ""
  open "https://unity.com/download" 2>/dev/null || true
  read -r -p "Press Enter to quit... "
  exit 1
fi

./BuildIOS.sh
STATUS=$?

if [[ $STATUS -ne 0 ]]; then
  echo ""
  echo "Headless build failed - falling back to opening the Unity GUI."
  ./OpenInUnity.sh || true
fi

read -r -p "Press Enter to close... "
exit "$STATUS"
