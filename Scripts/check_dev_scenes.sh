#!/usr/bin/env bash
# Development scenes must never reach a shipping build.
#
# The editor-side guards (CatchIfYouCanBuildMenu rejects them, the validator flags them)
# only run when somebody opens Unity. This one runs on every push, needs no licence, and
# catches the case those cannot: a build list committed with a lab ticked on.
#
# Usage: Scripts/check_dev_scenes.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

BUILD_SETTINGS="ProjectSettings/EditorBuildSettings.asset"
failures=0

echo "== development scene guard =="

if [ ! -f "$BUILD_SETTINGS" ]; then
  echo "  FAIL  $BUILD_SETTINGS not found"
  exit 1
fi

# Each entry is a two-line pair: "- enabled: N" then "path: ...". Pair them up so a
# disabled lab is reported as a warning rather than a failure - it ships nothing, but it
# is still a tick away from doing so.
enabled_hits=0
disabled_hits=0
while IFS= read -r line; do
  case "$line" in
    *"enabled:"*) last_enabled="${line##*enabled: }" ;;
    *"path:"*)
      path="${line##*path: }"
      case "$path" in
        Assets/CatchIfYouCan/Scenes/Development/*|*/DEV_*)
          if [ "${last_enabled:-0}" = "1" ]; then
            echo "  FAIL  development scene ENABLED in build list: $path"
            enabled_hits=$((enabled_hits + 1))
          else
            echo "  warn  development scene present but disabled: $path"
            disabled_hits=$((disabled_hits + 1))
          fi
          ;;
      esac
      ;;
  esac
done < "$BUILD_SETTINGS"

[ $enabled_hits -gt 0 ] && failures=1

if [ $enabled_hits -eq 0 ]; then
  echo "  ok    no development scene is enabled in the build list"
fi

# The production scene list is derived from CiycScenes; a DEV_ name appearing there would
# mean the two lists have been merged, which is the mistake the split exists to prevent.
if grep -q 'DEV_' Assets/CatchIfYouCan/Scripts/Core/CiycScenes.cs 2>/dev/null; then
  echo "  FAIL  CiycScenes names a DEV_ scene; production and development lists must stay apart"
  failures=1
else
  echo "  ok    production scene identity contains no development scenes"
fi

echo
if [ $failures -ne 0 ]; then
  echo "DEVELOPMENT SCENE GUARD FAILED"
  exit 1
fi

echo "DEVELOPMENT SCENE GUARD PASSED"
