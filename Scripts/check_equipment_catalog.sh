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
UNMAPPED_ALLOWLIST=""

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

# ------------------------------------------------------------------- evidence

# Equipment observes; the validator decides. A device that can call RegisterEvidence
# directly is a device that can prove anything by firing once - including evidence the
# ghost in the house does not exhibit.
if grep -rn 'RegisterEvidence' Assets/CatchIfYouCan/Scripts/Equipment/*.cs \
     | grep -v '^\s*//' | grep -qv 'used to call'; then
  fail "equipment calls EvidenceManager.RegisterEvidence directly; go through EvidenceValidator"
else
  ok "no equipment registers evidence directly"
fi

# Stronger than it used to be. AH made the journal submit an observation instead of
# registering directly; V4 gives every evidence type exactly one declared observing
# device, so a journal entry - which has no device - has no standing at all. The
# journal records; it does not prove.
if grep -q 'EvidenceValidator' Assets/CatchIfYouCan/Scripts/Evidence/EvidenceManager.cs; then
  fail "EvidenceManager's journal path can still start an evidence confirmation"
else
  ok "the journal records rather than proves"
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

# The types above can be absent while the packages are present, and a package that is
# in the manifest is a package somebody is about to use.
if grep -qiE '"com\.unity\.(netcode|transport|services\.relay|services\.lobby|services\.authentication|services\.multiplayer|addressables)' \
     Packages/manifest.json 2>/dev/null; then
  fail "a netcode, relay, lobby, authentication or addressables package is in the manifest"
else
  ok "no netcode, relay, lobby, authentication or addressables package"
fi

# The seam is allowed to exist and is not allowed to be a networking layer.
AUTHORITY="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentAuthority.cs"
if [ -f "$AUTHORITY" ]; then
  grep -qE 'Unity\.Netcode|Socket|UnityWebRequest|NetworkManager' "$AUTHORITY" \
    && fail "the authority seam has grown a networking dependency" \
    || ok "the authority seam contains no networking"
fi

# ------------------------------------------------------------ evidence back-doors
#
# Three separate paths used to announce evidence with nothing found: devices calling
# RegisterEvidence, the journal registering whatever a caller claimed, and the ghost's
# own evidence manager firing EvidenceDetected on a timer for all three of its types -
# which completed objectives forty-five seconds into a mission with no player involved.

raisers=$(grep -rn 'GameEvents\.EvidenceDetected(' Assets/CatchIfYouCan/Scripts \
          --include=*.cs | grep -v '/Core/GameEvents\.cs:' | wc -l | tr -d ' ')
if [ "$raisers" -eq 1 ] && \
   grep -q 'GameEvents\.EvidenceDetected(' \
        Assets/CatchIfYouCan/Scripts/Evidence/EvidenceManager.cs; then
  ok "EvidenceDetected is raised only by EvidenceManager.RegisterEvidence"
else
  fail "EvidenceDetected is raised from $raisers places; only RegisterEvidence may raise it"
  grep -rn 'GameEvents\.EvidenceDetected(' Assets/CatchIfYouCan/Scripts --include=*.cs \
    | grep -v '/Core/GameEvents\.cs:' | sed 's/^/        /'
fi

# RegisterEvidence itself: the validator, and the DEV lab's deliberately labelled bypass.
callers=$(grep -rn '\.RegisterEvidence(' Assets/CatchIfYouCan/Scripts --include=*.cs \
          | grep -v '/Missions/MissionRuntime\.cs:' | wc -l | tr -d ' ')
if [ "$callers" -le 2 ] && \
   grep -q 'manager\.RegisterEvidence' \
        Assets/CatchIfYouCan/Scripts/Evidence/EvidenceValidator.cs; then
  ok "RegisterEvidence is reached through the validator"
else
  fail "RegisterEvidence has $callers callers; it belongs to EvidenceValidator"
fi

# ------------------------------------------------------------------ item shape
#
# Every one of the eleven is a held item. Two classes may sit directly on EquipmentBase:
# HeldEquipmentBase itself, and the DEV placeholder an unknown id becomes.
strays=$(grep -rn ': EquipmentBase' Assets/CatchIfYouCan/Scripts --include=*.cs \
         | grep -v 'HeldEquipmentBase : EquipmentBase' \
         | grep -v 'DevPlaceholderEquipment : EquipmentBase' \
         | grep -v 'where T : EquipmentBase' | wc -l | tr -d ' ')
if [ "$strays" -eq 0 ]; then
  ok "every equipment class is a held item"
else
  fail "$strays equipment classes derive straight from EquipmentBase and cannot be carried"
  grep -rn ': EquipmentBase' Assets/CatchIfYouCan/Scripts --include=*.cs \
    | grep -v 'HeldEquipmentBase : EquipmentBase' \
    | grep -v 'DevPlaceholderEquipment : EquipmentBase' \
    | grep -v 'where T : EquipmentBase' | sed 's/^/        /'
fi

# ------------------------------------------------------------------- hot paths
#
# A scene sweep inside Update, LateUpdate or TickEquipped walks every object in the
# house, every frame, to find one thing. The EMF reader, the thermometer, the UV lamp,
# the audio wiring, the salt and the evidence validator were each doing it.
sweeps=$(awk '
  /void (Update|LateUpdate|FixedUpdate)\(\)|void TickEquipped\(float/ { inhot = 1; depth = 0; next }
  inhot {
    depth += gsub(/{/, "{") - gsub(/}/, "}")
    if ($0 ~ /FindObjectsByType|FindAnyObjectByType|GameObject\.Find|Camera\.main/)
      print FILENAME ":" FNR ": " $0
    if (depth <= 0 && NR > 1) inhot = 0
  }
' $(find Assets/CatchIfYouCan/Scripts/Equipment \
         Assets/CatchIfYouCan/Scripts/Ghost \
         Assets/CatchIfYouCan/Scripts/Evidence -name '*.cs') 2>/dev/null)

if [ -z "$sweeps" ]; then
  ok "no scene sweeps inside Update, LateUpdate or TickEquipped"
else
  fail "a scene sweep runs every frame"
  printf '%s\n' "$sweeps" | sed 's/^/        /'
fi

# ----------------------------------------------------------------- meta files
#
# A .cs, .shader, .mat or .asset with no .meta is a new GUID on the next import, and
# every reference to it silently breaks.
# Read with a null separator: two of this project's post-processing profiles have a
# space in the filename, and a for-loop over $(find) splits them into two halves that
# each look like a file with no .meta.
missing_meta=0
while IFS= read -r asset; do
  [ -f "$asset.meta" ] || { missing_meta=$((missing_meta + 1)); printf '        %s\n' "$asset"; }
done <<EOF
$(find Assets/CatchIfYouCan \
       \( -name '*.cs' -o -name '*.shader' -o -name '*.mat' -o -name '*.asset' \))
EOF
[ "$missing_meta" -eq 0 ] \
  && ok "every asset has a .meta" \
  || fail "$missing_meta assets have no .meta; their GUIDs would be regenerated on import"

# --------------------------------------------------------- placeholder honesty
#
# A profile that says its art is final has to point at some. One that says it is a
# placeholder is fine and is counted, because "how much art is left" should be a number
# somebody can read rather than an impression.
VIS="$DEFS/Visual"
if [ -d "$VIS" ]; then
  placeholders=0
  lying=0
  for prof in "$VIS"/VisualProfile_*.asset; do
    [ -f "$prof" ] || continue
    if grep -q '^  isDevPlaceholder: 1$' "$prof"; then
      placeholders=$((placeholders + 1))
      continue
    fi
    grep -qE '^  (visualPrefab: \{fileID: [1-9]|modelResourcePath: .+)' "$prof" \
      || { lying=$((lying + 1)); printf '        %s\n' "$prof"; }
  done

  [ "$lying" -eq 0 ] \
    && ok "every profile claiming final art points at some ($placeholders still placeholder)" \
    || fail "$lying profiles claim final art and reference none"
fi

# ---------------------------------------------------------------- ids are constants

# An equipment id looked up by string literal is a rename waiting to happen, and the
# failure is silent in the worst possible way: GetById returns null, the caller's
# `if (definition != null)` steps over it, the item keeps the placeholder it built before
# the definition arrived, and PlayerInventory.IsTorch - which compares against the same
# constant - stops recognising the torch, so it takes an investigation slot instead of
# the player's hand. Comment lines are stripped first, or the comment explaining this
# rule would break it.
literals=$(grep -rn 'GetById("' Assets --include=*.cs 2>/dev/null \
           | grep -v '^\s*//' | sed 's|:.*//.*||' | grep 'GetById("' || true)
if [ -n "$literals" ]; then
  fail "no equipment id is looked up by string literal"
  printf '%s\n' "$literals" | sed 's/^/        /'
else
  ok "no equipment id is looked up by string literal"
fi

# The torch the player spawns with is the one case where a missed lookup is invisible:
# every following line still runs. It has to say so.
PF="Assets/CatchIfYouCan/Scripts/Player/PlayerFactory.cs"
# Newlines squeezed out first: the call is wrapped across two lines, and a guard that
# cannot read wrapped source is a guard that fails on formatting.
if [ -f "$PF" ] && sed 's://.*::' "$PF" | tr -d '\n' | tr -s ' ' \
     | grep -q 'GetById( *Equipment.EquipmentIds.Flashlight'; then
  ok "PlayerFactory asks for the torch by its declared constant"
else
  fail "PlayerFactory asks for the torch by its declared constant"
fi

if [ -f "$PF" ] && grep -q 'CIYCLog.Error' "$PF" \
   && sed 's://.*::' "$PF" | grep -q 'No definition for'; then
  ok "a missing torch definition is reported rather than stepped over"
else
  fail "a missing torch definition is reported rather than stepped over"
fi

# ---------------------------------------------------------------- held items reach the hand

# HeldEquipmentBase.LateUpdate is the fallback that calls PlaceInHand for any frame the body
# motion's pose callback did not already place. A subclass that declares its own LateUpdate
# without `override` HIDES it - Unity dispatches the message to the most-derived declaration
# by name, so the base never runs and the item is built correctly and then left wherever it
# was parented. That is a hand holding nothing, and it is what happened to the flashlight:
# nine subclasses, one private LateUpdate, one item not in the hand. C# calls it CS0108 and
# the offline typecheck harness was not printing warnings.
hiders=""
for held in $(grep -rl ": HeldEquipmentBase" Assets/CatchIfYouCan/Scripts --include=*.cs); do
  # Comment lines stripped first, or this paragraph would trip the check it documents.
  if sed 's://.*::' "$held" | grep -qE '^[[:space:]]*(private|public)?[[:space:]]*void[[:space:]]+(LateUpdate|Update)[[:space:]]*\('; then
    hiders="$hiders $held"
  fi
done
if [ -n "$hiders" ]; then
  fail "no held item hides HeldEquipmentBase's per-frame methods"
  for h in $hiders; do printf '        %s\n' "$h"; done
else
  ok "no held item hides HeldEquipmentBase's per-frame methods"
fi

# And the flashlight's own override must actually chain, or the fix is cosmetic.
FL="Assets/CatchIfYouCan/Scripts/Equipment/HeldFlashlight.cs"
if sed 's://.*::' "$FL" | tr -d '\n' | tr -s ' ' \
     | grep -qE 'protected override void LateUpdate\(\) \{ base\.LateUpdate\(\);'; then
  ok "the flashlight's LateUpdate chains to the base"
else
  fail "the flashlight's LateUpdate chains to the base"
fi

# ---------------------------------------------------------------- the flashlight's own art

# The visual profile names a model and a material by Resources path. A path with no file
# behind it is this project's oldest mistake, and it fails silently in exactly the same way
# as everything else here.
FACTORY_DEF="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentDefinitionFactory.cs"
RES="Assets/CatchIfYouCan/Resources"

# Both arguments off the one ApplyModel call that names the flashlight model. Read from the
# call itself rather than by counting lines from the id: there is a paragraph of comment
# between them, and a guard that depends on how long a comment is will break when someone
# edits the comment.
fl_call=$(sed 's://.*::' "$FACTORY_DEF" | tr -d '\n' | tr -s ' ' \
          | grep -oE 'ApplyModel\("Props/CIYC_Flashlight" *, *"[^"]+"' | head -1)
fl_model=$(printf '%s' "$fl_call" | sed 's/ApplyModel("//; s/".*//')
fl_mat=$(printf '%s' "$fl_call" | sed 's/.*, *"//; s/"//')

if [ -n "$fl_model" ] && ls "$RES/$fl_model".* >/dev/null 2>&1; then
  ok "the flashlight model path resolves to a real file ($fl_model)"
else
  fail "the flashlight model path resolves to a real file"
  printf '        looked for %s/%s.* \n' "$RES" "${fl_model:-<unparsed>}"
fi

if [ -n "$fl_mat" ] && [ -f "$RES/$fl_mat.mat" ]; then
  ok "the flashlight material path resolves to a real file ($fl_mat)"
else
  fail "the flashlight material path resolves to a real file"
  printf '        looked for %s/%s.mat\n' "$RES" "${fl_mat:-<unparsed>}"
fi

# A Resources path is relative to a Resources folder and carries no extension. All three of
# these wrong shapes have shipped in this repository before.
if [ -n "$fl_model" ] && ! printf '%s' "$fl_model" \
     | grep -qE '(^Assets/|^Resources/|\.fbx$|\.prefab$|\.mat$)'; then
  ok "the flashlight Resources path has no folder prefix and no extension"
else
  fail "the flashlight Resources path has no folder prefix and no extension" 
fi

# The torch is finished art, not a grey box. If this ever becomes a placeholder it means the
# real profile stopped being reached.
if sed 's://.*::' "$FACTORY_DEF" | grep -A4 'EquipmentIds.Flashlight,' | grep -q 'ApplyDevPlaceholder'; then
  fail "the flashlight uses its real model rather than the DEV placeholder"
else
  ok "the flashlight uses its real model rather than the DEV placeholder"
fi

printf '\npassed: %s   failed: %s\n\n' "$passed" "$failed"

if [ "$failed" -gt 0 ]; then
  printf 'EQUIPMENT CATALOG GUARD FAILED\n'
  exit 1
fi

printf 'EQUIPMENT CATALOG GUARD PASSED\n'
