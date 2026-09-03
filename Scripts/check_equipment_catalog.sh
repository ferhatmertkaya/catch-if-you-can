#!/usr/bin/env sh
#
# Equipment catalog guard.
#
# Every content failure this project has had with equipment was silent: an id that
# resolved to nothing, a definition that was never in the catalog, four items that
# quietly became placeholders, and - before V2 - an unknown id that came back as a
# working flashlight. None of them threw. All of them shipped.
#
# This checks the text, so it needs nothing but a shell and runs anywhere Unity does
# not. Unity-side checks that need real object references live in
# EquipmentCatalogValidator; this is the half that can be enforced in CI.

set -eu

REPO="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO"

DEFS="Assets/CatchIfYouCan/Definitions/Equipment"
IDS="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentIds.cs"
FACTORY="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentRuntimeFactory.cs"
CATALOG="$DEFS/EquipmentCatalog.asset"

passed=0
failed=0

ok()   { passed=$((passed + 1)); printf '  ok    %s\n' "$1"; }
fail() { failed=$((failed + 1)); printf '  FAIL  %s\n' "$1"; }

printf '== equipment catalog guard ==\n'

# ---------------------------------------------------------------- canonical ids

CANONICAL="flashlight emf_detector uv_light thermometer evp_recorder
parabolic_microphone photo_camera spectral_grid video_camera warding_relic salt"

count=0
for id in $CANONICAL; do
  count=$((count + 1))
  if ! grep -q "\"$id\"" "$IDS"; then
    fail "EquipmentIds is missing the canonical id '$id'"
  fi
done
[ "$count" -eq 11 ] || fail "the canonical roster is $count ids, not 11"
[ "$failed" -eq 0 ] && ok "EquipmentIds declares all 11 canonical ids"

# Nothing may declare an id the roster does not know.
for declared in $(grep -oE 'public const string [A-Za-z]+ = "[a-z_]+"' "$IDS" \
                  | sed 's/.*"\(.*\)"/\1/'); do
  echo "$CANONICAL" | tr ' \n' '\n\n' | grep -qx "$declared" \
    || fail "EquipmentIds declares '$declared', which is not in the canonical roster"
done
ok "EquipmentIds declares no id outside the roster"

# --------------------------------------------------------------- definitions

if [ -d "$DEFS" ]; then
  for id in $CANONICAL; do
    hits=$(grep -l "^  Id: $id\$" "$DEFS"/Equipment_*.asset 2>/dev/null | wc -l | tr -d ' ')
    case "$hits" in
      1) : ;;
      0) fail "no definition asset has Id '$id'" ;;
      *) fail "$hits definition assets share Id '$id'; ids are the data identity" ;;
    esac
  done
  ok "each canonical id has exactly one definition asset"

  # Every definition the catalog lists must be a file that exists.
  if [ -f "$CATALOG" ]; then
    missing=0
    for guid in $(grep -oE 'guid: [0-9a-f]{32}, type: 2' "$CATALOG" | cut -d' ' -f2 | tr -d ','); do
      grep -rql "guid: $guid" "$DEFS" --include='*.meta' >/dev/null 2>&1 || missing=$((missing + 1))
    done
    [ "$missing" -eq 0 ] \
      && ok "every catalog entry resolves to a definition asset" \
      || fail "$missing catalog entries point at a definition that is not there"
  else
    fail "no EquipmentCatalog.asset at $CATALOG"
  fi
else
  fail "no definitions folder at $DEFS"
fi

# ------------------------------------------------------- runtime path mapping

# The factory declares which ids it can build, and separately switches on them.
# A switch cannot be asked what it handles, so the two are written down twice -
# and this is what stops them drifting.
declared=$(sed -n '/HashSet<string> RuntimeIds/,/};/p' "$FACTORY" \
           | grep -oE 'EquipmentIds\.[A-Za-z]+' | sort -u)
switched=$(sed -n '/GameObject prefab = definition.Id switch/,/};/p' "$FACTORY" \
           | grep -oE 'EquipmentIds\.[A-Za-z]+ =>' | sed 's/ =>//' | sort -u)

if [ "$declared" = "$switched" ]; then
  ok "the factory's declared runtime ids match its switch cases"
else
  fail "EquipmentRuntimeFactory's RuntimeIds set and switch cases disagree"
  printf '        declared: %s\n' "$(echo "$declared" | tr '\n' ' ')"
  printf '        switched: %s\n' "$(echo "$switched" | tr '\n' ' ')"
fi

# Ids with no runtime path yet. This list must only ever shrink: an entry here is
# an item that can currently be nothing but a DEV_PLACEHOLDER, and it is written
# down so that it is a known gap rather than a discovery.
UNMAPPED_ALLOWLIST="parabolic_microphone spectral_grid video_camera warding_relic"

gap_ok=1
for id in $CANONICAL; do
  # The C# constant this id is declared as, matched on the declaration itself.
  constant=$(grep -oE "public const string [A-Za-z]+ = \"$id\";" "$IDS" | awk '{print $4}')
  if [ -z "$constant" ]; then
    fail "no EquipmentIds constant declares '$id'"
    gap_ok=0
    continue
  fi

  if echo "$switched" | grep -qx "EquipmentIds.$constant"; then
    continue
  fi

  if ! echo "$UNMAPPED_ALLOWLIST" | tr ' ' '\n' | grep -qx "$id"; then
    fail "'$id' has no runtime path and is not on the known-gap allowlist"
    gap_ok=0
  fi
done
[ "$gap_ok" -eq 1 ] && ok "every id either has a runtime path or is a declared known gap"

# --------------------------------------------------------- visual presentation

VIS="$DEFS/Visual"

if [ -d "$VIS" ]; then
  missing=0
  for def in "$DEFS"/Equipment_*.asset; do
    grep -q 'VisualProfile: {fileID: 11400000' "$def" || missing=$((missing + 1))
  done
  [ "$missing" -eq 0 ] \
    && ok "every definition resolves a visual profile" \
    || fail "$missing definitions have no visual profile, so they have no presentation entry"

  # An item with no art must say so. An unimplemented item that looks finished is one
  # nobody ever finishes, which is the whole reason this flag exists.
  bad=0
  for vis in "$VIS"/VisualProfile_*.asset; do
    model=$(grep '^  modelResourcePath:' "$vis" | sed 's/^  modelResourcePath: *//')
    prefab=$(grep '^  visualPrefab:' "$vis" | grep -c 'fileID: 0' || true)
    placeholder=$(grep '^  isDevPlaceholder:' "$vis" | awk '{print $2}')
    if [ -z "$model" ] && [ "$prefab" = "1" ] && [ "$placeholder" != "1" ]; then
      fail "$(basename "$vis") has no art but is not marked isDevPlaceholder"
      bad=$((bad + 1))
    fi
    if [ -n "$model" ] && [ "$placeholder" = "1" ]; then
      fail "$(basename "$vis") points at real art but is still marked isDevPlaceholder"
      bad=$((bad + 1))
    fi
  done
  [ "$bad" -eq 0 ] && ok "no placeholder is dressed up as production art, and none the other way"

  # The one item with real art must keep pointing at it.
  grep -q 'modelResourcePath: Props/CIYC_Flashlight' "$VIS/VisualProfile_Flashlight.asset" \
    && ok "the flashlight still resolves its real FBX" \
    || fail "the flashlight's visual profile no longer points at Props/CIYC_Flashlight"
else
  fail "no visual profile folder at $VIS"
fi

# Gameplay classes must not build their own production visual identity.
if grep -n 'CreatePrimitive' Assets/CatchIfYouCan/Scripts/Equipment/HeldFlashlight.cs >/dev/null 2>&1; then
  # The lens is a primitive on purpose - it is a light, not a model - so this only
  # catches the body/fallback construction moving back in.
  grep -q 'PrimitiveType.Capsule' Assets/CatchIfYouCan/Scripts/Equipment/HeldFlashlight.cs \
    && fail "HeldFlashlight builds a body primitive again; that is the visual factory's" \
    || ok "HeldFlashlight no longer builds its own body"
else
  ok "HeldFlashlight no longer builds its own body"
fi

# ------------------------------------------------------------- the never-agains

! grep -rq "class FlashlightEquipment" Assets/CatchIfYouCan/Scripts \
  && ok "FlashlightEquipment does not exist" \
  || fail "FlashlightEquipment is back; there is one flashlight and it is HeldFlashlight"

if grep -A2 '_ =>' "$FACTORY" | grep -qi 'flashlight'; then
  fail "the unknown-id branch mentions the flashlight"
else
  ok "an unknown id cannot become a flashlight"
fi

for member in ActiveInstance SetHandAnchor EquipByIndex CycleNext DropActive TryPlaceActive; do
  grep -q "$member" Assets/CatchIfYouCan/Scripts/Equipment/EquipmentManager.cs 2>/dev/null \
    && fail "EquipmentManager has regained runtime held-item authority ($member)" || :
done
ok "EquipmentManager is still loadout data only"

grep -rq 'Shader.Find("Standard")\|Shader.Find("Particles/Standard' Assets/CatchIfYouCan/Scripts \
  && fail "a built-in Standard shader fallback was reintroduced" \
  || ok "no built-in shader fallback"

grep -rqE 'using Unity\.Netcode|NetworkBehaviour|NetworkVariable|ServerRpc|ClientRpc' \
     Assets/CatchIfYouCan/Scripts 2>/dev/null \
  && fail "netcode types have appeared; V3 is single player" \
  || ok "no netcode types"

printf '\npassed: %s   failed: %s\n\n' "$passed" "$failed"

if [ "$failed" -gt 0 ]; then
  printf 'EQUIPMENT CATALOG GUARD FAILED\n'
  exit 1
fi

printf 'EQUIPMENT CATALOG GUARD PASSED\n'
