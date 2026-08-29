#!/usr/bin/env bash
# Determinism guard (Docs/DETERMINISM.md T7).
#
# Static check plus the full suite. This is the part that holds the line after everyone
# has forgotten the design discussion: a reviewer will not catch a reintroduced
# UnityEngine.Random in a 400-line diff, but this will.
#
# Usage: Scripts/check_determinism.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

CORE="Assets/CatchIfYouCan/Scripts/Procedural/Deterministic"
STAGE_B=(
  "Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs"
  "Assets/CatchIfYouCan/Scripts/Procedural/PropSpawner.cs"
  "Assets/CatchIfYouCan/Scripts/Procedural/PrimitiveRoomFactory.cs"
  "Assets/CatchIfYouCan/Scripts/Procedural/ContentSnapshotFactory.cs"
  "Assets/CatchIfYouCan/Scripts/Procedural/HouseLayoutGraph.cs"
)

failures=0

# Strip // line comments, /* */ blocks and /// docs before matching, so the rules do not
# fire on the comments that explain them.
strip_comments() {
  sed -e 's://.*::' "$1" | sed -e 's:/\*.*\*/::'
}

check_pattern() {
  local label="$1" pattern="$2"
  shift 2
  local hit=0
  for file in "$@"; do
    [ -f "$file" ] || continue
    if strip_comments "$file" | grep -nE "$pattern" >/dev/null; then
      echo "  FAIL  $label"
      strip_comments "$file" | grep -nE "$pattern" | sed "s|^|        $file:|"
      hit=1
    fi
  done
  [ $hit -eq 0 ] && echo "  ok    $label"
  return $hit
}

core_files=()
while IFS= read -r f; do core_files+=("$f"); done < <(find "$CORE" -name '*.cs')

echo "== static determinism guard =="
check_pattern "core: no UnityEngine.Random"        'UnityEngine\.Random|(^|[^.[:alnum:]_])Random\.(Range|value|InitState|insideUnit|onUnitSphere|rotation|state)' "${core_files[@]}" || failures=1
check_pattern "core: no System.Random"             'System\.Random|new Random\(' "${core_files[@]}" || failures=1
check_pattern "core: no physics queries"           '\bPhysics\.|\bPhysics2D\.' "${core_files[@]}" || failures=1
check_pattern "core: no random-key sorts"          'OrderBy\(' "${core_files[@]}" || failures=1
check_pattern "core: no wall clock"                'DateTime\.(Now|UtcNow)|Environment\.TickCount' "${core_files[@]}" || failures=1
check_pattern "core: no engine time or frames"     '\bTime\.(time|deltaTime|frameCount|unscaledTime|realtimeSinceStartup)' "${core_files[@]}" || failures=1
check_pattern "core: no UnityEngine references"    '^\s*using UnityEngine' "${core_files[@]}" || failures=1
check_pattern "core: GetHashCode not used to hash" '=\s*[A-Za-z_][A-Za-z0-9_.]*\.GetHashCode\(\)' "${core_files[@]}" || failures=1

check_pattern "stage B: no generation RNG"         'UnityEngine\.Random|(^|[^.[:alnum:]_])Random\.(Range|value|InitState)|System\.Random' "${STAGE_B[@]}" || failures=1
check_pattern "stage B: no physics queries"        '\bPhysics\.(OverlapBox|OverlapSphere|Raycast|CheckBox|CheckSphere|ComputePenetration|BoxCast|SphereCast)' "${STAGE_B[@]}" || failures=1

echo
echo "== determinism suite =="
if command -v dotnet >/dev/null 2>&1; then
  if dotnet run --project Tools/DeterminismHarness --verbosity quiet -- test; then
    :
  else
    echo "  FAIL  determinism suite"
    failures=1
  fi
else
  echo "  SKIP  dotnet not available; run the suite in Unity (Window > General > Test Runner)"
fi

echo
if [ $failures -ne 0 ]; then
  echo "DETERMINISM GUARD FAILED"
  exit 1
fi

echo "DETERMINISM GUARD PASSED"
