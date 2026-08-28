#!/usr/bin/env bash
# Doppelklick-Start für macOS (öffnet Anleitung + versucht Build)
cd "$(dirname "$0")"
chmod +x BuildIOS.sh OpenInUnity.sh 2>/dev/null || true

clear
cat <<'EOF'
╔══════════════════════════════════════════════════════╗
║         CATCH IF YOU CAN — iOS Deploy v2             ║
╚══════════════════════════════════════════════════════╝

Dieses Skript baut das Xcode-Projekt mit Unity.

Benötigt:
  • Unity Hub
  • Unity 6.x (idealerweise 6.3 LTS)
  • Modul: iOS Build Support
  • Xcode 15+

EOF

if [[ ! -d "/Applications/Unity Hub.app" ]] && [[ ! -d "/Applications/Unity/Hub" ]]; then
  echo "Unity Hub scheint nicht installiert zu sein."
  echo "Download: https://unity.com/download"
  echo ""
  open "https://unity.com/download" 2>/dev/null || true
  read -r -p "Enter drücken zum Beenden…"
  exit 1
fi

./BuildIOS.sh
STATUS=$?

if [[ $STATUS -ne 0 ]]; then
  echo ""
  echo "Automatischer Build fehlgeschlagen → GUI-Fallback wird vorbereitet."
  ./OpenInUnity.sh || true
fi

read -r -p "Enter drücken zum Schließen…"
exit "$STATUS"
