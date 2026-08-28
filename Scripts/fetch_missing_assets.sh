#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$ROOT/Assets/External/Quaternius/Monsters/CreepCreature.glb"

if [[ -f "$DEST" ]]; then
  echo "CreepCreature.glb already present."
  exit 0
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "Downloading Quaternius CreepCreature.glb ..."
curl -L -o "$TMP/quaternius.zip" "https://github.com/511action/descent-3d-assets/archive/refs/heads/main.zip"
unzip -q "$TMP/quaternius.zip" -d "$TMP"
mkdir -p "$(dirname "$DEST")"
cp "$TMP/descent-3d-assets-main/models/CreepCreature.glb" "$DEST"
echo "Saved: $DEST"
