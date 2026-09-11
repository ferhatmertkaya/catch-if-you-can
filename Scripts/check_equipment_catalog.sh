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

# Kommentare zuerst weg. Diese Pruefung hat auf dem Kommentar angeschlagen, der vor genau
# diesem Aufruf warnt - CLAUDE.md Fehler 8, in seiner umgekehrten Richtung. Ein Waechter, den
# man nicht erklaeren darf, ohne ihn ausloesen, erzieht nur zum Schweigen.
if find Assets/CatchIfYouCan/Scripts -name '*.cs' -exec sed 's://.*::' {} + \
     | grep -q 'Shader\.Find("Standard")\|Shader\.Find("Particles/Standard'; then
  fail "a built-in Standard shader fallback was reintroduced"
else
  ok "no built-in shader fallback"
fi

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

# And EVERY other one, because the flashlight was never the only one.
#
# Three items have finished art now, not one, and the check above names exactly one of them.
# A path that resolves nowhere is CLAUDE.md mistake 3, and its whole character is that it is
# silent: Resources.Load returns null, the visual factory substitutes its honest capsule, and
# the item looks like one nobody has modelled yet rather than one whose path has a typo in it.
# That is indistinguishable by looking, which is why it survived the life of the project once
# already. Checked by resolving every path that exists rather than by naming them one at a
# time - a list of names has to be remembered, and the next item added will not be.
bad_paths=0
checked_paths=0
for call in $(sed 's://.*::' "$FACTORY_DEF" | tr -d '\n' | tr -s ' ' \
              | grep -oE 'ApplyModel\("[^"]+" *, *"[^"]+"' | tr -d ' '); do
  m=$(printf '%s' "$call" | sed 's/ApplyModel("//; s/",".*//')
  t=$(printf '%s' "$call" | sed 's/.*,"//; s/"//')
  checked_paths=$((checked_paths + 1))
  if ! ls "$RES/$m".* >/dev/null 2>&1; then
    fail "ApplyModel model path resolves: $m"
    printf '        looked for %s/%s.*\n' "$RES" "$m"
    bad_paths=$((bad_paths + 1))
  fi
  if [ -n "$t" ] && [ ! -f "$RES/$t.mat" ]; then
    fail "ApplyModel material path resolves: $t"
    printf '        looked for %s/%s.mat\n' "$RES" "$t"
    bad_paths=$((bad_paths + 1))
  fi
done
[ "$checked_paths" -ge 3 ] \
  && ok "every ApplyModel call names a model and a material that exist ($checked_paths call(s))" \
  || fail "every ApplyModel call names a model and a material that exist" \
       "found only $checked_paths call(s); the three finished items are the flashlight, the UV light and the projector"

# ---- X platziert, G schaltet, und beides ueber EINEN Eingabeweg ----------------------------
#
# MobileInputController ist die einzige Stelle im Spielcode, die eine Taste liest. Ein
# Input.GetKeyDown in fuenf Skripten waere genau der Zustand, den dieses Projekt vermeidet:
# fuenf Meinungen darueber, was eine Taste bedeutet.
MIC="Assets/CatchIfYouCan/Scripts/Input/MobileInputController.cs"
SGP="Assets/CatchIfYouCan/Scripts/Equipment/SpectralGridProjector.cs"
RTR="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentActionRouter.cs"
IC="Assets/CatchIfYouCan/Scripts/Interaction/InteractionController.cs"

if [ -f "$MIC" ] &&
   sed 's://.*::' "$MIC" | grep -qE 'KeyCode\.X' &&
   sed 's://.*::' "$MIC" | grep -qE 'xKey\.wasPressedThisFrame'; then
  ok "X liegt auf Interact, in beiden Eingabepfaden"
else
  fail "X liegt auf Interact, in beiden Eingabepfaden"
  printf '        ENABLE_INPUT_SYSTEM und der alte Pfad muessen dieselbe Taste kennen\n'
fi

# Kein zweiter Tastenleser. Der Router fragt den Controller, er liest nichts selbst.
if [ -f "$RTR" ] && ! sed 's://.*::' "$RTR" | grep -qE 'Input\.GetKey|Keyboard\.current'; then
  ok "der Aktionsrouter liest keine Taste selbst"
else
  fail "der Aktionsrouter liest keine Taste selbst"
  printf '        ein zweiter Tastenleser ist eine zweite Meinung darueber, was X bedeutet\n'
fi

# Ein Druck, eine Wirkung: waehrend gezielt wird, gehoert X der Platzierung. Sonst wird das
# Geraet gesetzt und im selben Frame wieder aufgehoben, was aussieht wie "nichts passiert".
if [ -f "$RTR" ] && [ -f "$IC" ] &&
   sed 's://.*::' "$RTR" | grep -qE 'ConsumedInteractThisFrame' &&
   sed 's://.*::' "$RTR" | grep -qE 'TryPlace\(\)' &&
   sed 's://.*::' "$IC" | grep -qE 'ConsumedInteractThisFrame'; then
  ok "ein Druck wird einmal verbraucht, nicht zweimal"
else
  fail "ein Druck wird einmal verbraucht, nicht zweimal"
fi

# G gehoert EINEM Geraet, nie zweien. Der Torch bietet den Druck ueber EquipmentPowerClaim
# an; nimmt ein montiertes Geraet ihn, schaltet die Fackel NICHT mit. Beide Signale
# bedingungslos zu setzen hiess: ein Druck, zwei Geraete, und keines davon das gemeinte.
if [ -f "$RTR" ] && [ -f "$MIC" ] &&
   sed 's://.*::' "$MIC" | grep -qE 'EquipmentPowerClaim' &&
   sed 's://.*::' "$MIC" | grep -qE 'if \(claim != null \&\& claim\(\)\)' &&
   sed 's://.*::' "$RTR" | grep -qE 'EquipmentPowerClaim = TryClaimPower' &&
   sed 's://.*::' "$RTR" | grep -qE 'reason=NotMounted'; then
  ok "G gehoert einem Geraet: montiert dem Projektor, sonst der Fackel"
else
  fail "G gehoert einem Geraet: montiert dem Projektor, sonst der Fackel"
  printf '        ein Druck darf nicht Projektor UND Fackel schalten\n'
fi

# Und der Anspruch faellt NUR durch, wenn gar kein Projektor gewaehlt ist.
#
# Vorher stand hier das Gegenteil: der nicht montierte Projektor gab G an die Fackel zurueck.
# Im Editor sah das so aus - ein Druck, zwei Zeilen, zwei Geraete:
#   [CIYC][DOTS] [BLOCKED] POWER reason=NotMounted
#   Flashlight: WrongState: not held or placed (World)
# Beide Meldungen stimmen fuer sich und das Paar ist falsch. Wer den Projektor gewaehlt hat,
# adressiert mit G den Projektor - ob der an der Wand haengt oder in der Hand liegt. Ein
# Anspruch heisst "der Druck war meiner", nicht "der Druck hat etwas bewirkt".
#
# Geprueft wird der Zweig, nicht die Datei: ein `return false` darf nur dort stehen, wo noch
# kein Projektor feststeht (kein Inventar, offenes Menue, anderes Geraet gewaehlt), und der
# Zweig fuer das nicht montierte Geraet muss `return true` liefern.
claim=$(sed 's://.*::' "$RTR" | sed -n '/private bool TryClaimPower/,/^        }$/p')
notmounted=$(printf '%s' "$claim" | sed -n '/if (!projector.IsPlaced)/,/^            }$/p')
if [ -f "$RTR" ] &&
   printf '%s' "$claim" | grep -qE 'is not SpectralGridProjector projector' &&
   printf '%s' "$notmounted" | grep -qE 'return true;' &&
   ! printf '%s' "$notmounted" | grep -qE 'return false;'; then
  ok "ein gewaehlter Projektor verbraucht G selbst, montiert oder nicht"
else
  fail "ein gewaehlter Projektor verbraucht G selbst, montiert oder nicht"
  printf '        sonst laeuft ein Druck durch den Projektor UND die Fackel\n'
fi

# Die Platzierung wird nur bei GUELTIGEM Ziel bestaetigt, und der Druck nur dann verbraucht.
if [ -f "$RTR" ] &&
   sed 's://.*::' "$RTR" | grep -qE 'if \(!aiming\.HasValidCandidate\)' &&
   sed 's://.*::' "$RTR" | grep -qE 'X_ROUTE=PLACE'; then
  ok "X platziert nur bei gueltigem Ziel und verbraucht sich nur dann"
else
  fail "X platziert nur bei gueltigem Ziel und verbraucht sich nur dann"
fi

# Und der Projektor zielt, sobald er in der Hand ist - kein AIM-Schritt, der ueber einen
# Knopf laeuft, den dieser Bildschirm nicht baut, und eine Taste, die niemand ausliest.
if [ -f "$SGP" ] &&
   sed 's://.*::' "$SGP" | grep -qE 'to == EquipmentLifecycleState\.Equipped' &&
   sed 's://.*::' "$SGP" | grep -qE 'TryBeginPlacement\(\)'; then
  ok "der Projektor zielt, sobald er in der Hand ist"
else
  fail "der Projektor zielt, sobald er in der Hand ist"
fi

# Und die Entwicklerhilfe teilt sich keine Taste mit einer echten Steuerung.
DIT="Assets/CatchIfYouCan/Scripts/Development/DebugItemTools.cs"
if [ -f "$DIT" ] && ! sed 's://.*::' "$DIT" | grep -qE 'TakeOrDropKey = KeyCode\.(X|E)\b'; then
  ok "die Entwicklerhilfe liegt nicht auf der Interakt-Taste"
else
  fail "die Entwicklerhilfe liegt nicht auf der Interakt-Taste"
  printf '        ein Druck lief sonst durch beide Pfade und warf das Geraet auf den Boden\n'
fi

# Ein Geraet an der Wand wird ENTFERNT, nicht aufgehoben: TryPickupPlaced schaltet es ab und
# raeumt den Platzierungszustand auf. AddItem allein liesse es sich fuer installiert halten,
# und HeldEquipmentBase weigert sich, ein Placed-Item in die Hand zu geben.
IP="Assets/CatchIfYouCan/Scripts/Interaction/InteractivePickup.cs"
if [ -f "$IP" ] &&
   sed 's://.*::' "$IP" | grep -qE 'itemComponent\.IsPlaced' &&
   sed 's://.*::' "$IP" | grep -qE 'TryPickupPlaced\(inventory\)' &&
   sed 's://.*::' "$IP" | grep -qE '\? "Remove"'; then
  ok "ein Geraet an der Wand wird entfernt und heisst auch so"
else
  fail "ein Geraet an der Wand wird entfernt und heisst auch so"
fi

# ---- der Projektor steht auf Boden UND Wand, und auf dem Boden LIEGT er --------------------
#
# Er war Wand-only, und der Grund dafuer stimmte damals: das Feld war ein 70-Grad-Kegel entlang
# +Y, ein Projektor auf dem Boden zeigte also an die Decke. Das Feld ist jetzt eine KUGEL - die
# Richtung entscheidet nicht mehr, was beleuchtet wird, und die Einschraenkung schuetzte vor
# einer Form, die der Effekt nicht mehr hat. Was bleibt: die Erlaubnis gehoert zum GERAET statt
# in einen Inspector-Wert, die Abfrage muss sie lesen, und auf dem Boden muss er LIEGEN.
SGP="Assets/CatchIfYouCan/Scripts/Equipment/SpectralGridProjector.cs"
PEB="Assets/CatchIfYouCan/Scripts/Equipment/PlaceableEquipmentBase.cs"
sgpcode=$(sed 's://.*::' "$SGP" 2>/dev/null | grep -v '^[[:space:]]*\*')

if [ -f "$SGP" ] && [ -f "$PEB" ] &&
   printf '%s' "$sgpcode" | grep -qE 'SurfaceOverride => PlacementSurface\.FloorAndWall' &&
   sed 's://.*::' "$PEB" | grep -qE 'Allowed = AllowedSurfaces'; then
  ok "der Projektor darf auf Boden und Wand, und die Abfrage liest das"
else
  fail "der Projektor darf auf Boden und Wand, und die Abfrage liest das"
  printf '        eine Ueberschreibung, die die Platzierungsabfrage nicht liest, aendert nichts\n'
fi

# Und auf dem Boden LIEGT er. Die Basis legt die Arbeitsachse eines Bodengeraets auf die
# Flaechennormale, stellt das Rohr also hochkant auf sein Ende. Dieselbe Vierteldrehung, die
# der Wandfall schon benutzt, kippt +Y auf das +Z der Bodendrehung - und das baut die
# Platzierungsabfrage aus der Blickrichtung des Spielers, flach in die Bodenebene gelegt.
if [ -f "$SGP" ] && printf '%s' "$sgpcode" \
     | grep -qE 'override Quaternion OrientForPlacement' \
   && printf '%s' "$sgpcode" | grep -qE 'result\.Rotation \* Quaternion\.Euler\(90f, 0f, 0f\)'; then
  ok "auf dem Boden liegt der Projektor, statt hochkant zu stehen"
else
  fail "auf dem Boden liegt der Projektor, statt hochkant zu stehen"
fi

# Und er liegt AUF dem Boden statt halb darin. Die Platzierung setzt den PIVOT auf den
# Auftreffpunkt, was fuer etwas auf seiner Standflaeche stimmt und in dem Moment falsch wird,
# in dem das Ding auf der Seite liegt. Wie weit darunter ist eine Tatsache ueber diesen Pivot,
# und die wird GEMESSEN statt hingeschrieben (Fehler 12 und 29) - ohne das Projektionsvolumen,
# denn das ist elf Meter gross und hat diesen Projektor schon einmal durch die Decke gehoben
# (Fehler 42).
if [ -f "$SGP" ] && printf '%s' "$sgpcode" | grep -qE 'result\.Position\.y - drawn\.min\.y' \
   && printf '%s' "$sgpcode" | grep -qE 'EffectVolume\.Encloses\(r\.transform\)'; then
  ok "der liegende Projektor wird auf die Flaeche gesetzt, gemessen statt geraten"
else
  fail "der liegende Projektor wird auf die Flaeche gesetzt, gemessen statt geraten"
fi

# Die Id bleibt spectral_grid - sie haengt an EvidenceAuthority, EvidenceValidator,
# GhostEvidenceManager und der Auswertung -, der ANZEIGENAME ist DOTS Projector.
if sed 's://.*::' "$FACTORY_DEF" | grep -qE 'Create\("spectral_grid", "DOTS Projector"'; then
  ok "der Anzeigename ist DOTS Projector, die Id bleibt spectral_grid"
else
  fail "der Anzeigename ist DOTS Projector, die Id bleibt spectral_grid"
  printf '        die Id umzubenennen fasst den Beweiskontrakt an; der Name nicht\n'
fi

# ---- der Fackelplatz liegt AUSSERHALB des Dreierfeldes -------------------------------------
#
# _slots fasst die drei Ermittlungsplaetze. Der ausgewaehlte Index wird gegen
# SelectableSlotCount geprueft, und das sind VIER, weil die Fackel einen eigenen Platz mit
# Index 3 hat. Das Feld mit diesem Index anzusprechen warf IndexOutOfRangeException - bei
# jedem Druck auf die Fackeltaste, bei SelectTorch, und beim automatischen Rueckfall auf die
# Fackel, wenn die Tasche leer ist. Genau das tut die Lobby beim Start, wenn sie dem Spieler
# die Fackel aus der Hand nimmt.
#
# Und der Schaden war nicht "es passiert nichts": geworfen wurde NACH der Zuweisung und NACH
# EquipSelected(), die Ausnahme verliess also AddItem und InteractivePickup.Interact und liess
# den Rest des Aufhebens liegen. Jeder andere Zugriff in der Datei geht ueber GetSlot, das die
# Fackel kennt.
INV="Assets/CatchIfYouCan/Scripts/Player/PlayerInventory.cs"
if [ -f "$INV" ]; then
  # Jede rohe _slots[...]-Indizierung muss durch SlotCount begrenzt sein. Der ausgewaehlte
  # Index ist es nicht.
  if ! sed 's://.*::' "$INV" | grep -qE '_slots\[_selectedIndex\]'; then
    ok "der ausgewaehlte Index indiziert das Dreierfeld nicht roh"
  else
    fail "der ausgewaehlte Index indiziert das Dreierfeld nicht roh"
    printf '        _selectedIndex laeuft bis SelectableSlotCount (4), _slots hat SlotCount (3)\n'
  fi

  if sed 's://.*::' "$INV" | grep -qE 'OnSlotChanged\?\.Invoke\(_selectedIndex, GetSlot\(_selectedIndex\)\)'; then
    ok "der Fackelplatz wird ueber GetSlot gelesen, das ihn kennt"
  else
    fail "der Fackelplatz wird ueber GetSlot gelesen, das ihn kennt"
  fi

  # Und die drei Konstanten stehen weiter in dem Verhaeltnis, das die Pruefung oben annimmt.
  if sed 's://.*::' "$INV" | grep -qE 'TorchSlotIndex = SlotCount' &&
     sed 's://.*::' "$INV" | grep -qE 'SelectableSlotCount = SlotCount \+ 1'; then
    ok "der Fackelplatz liegt genau hinter den drei Ermittlungsplaetzen"
  else
    fail "der Fackelplatz liegt genau hinter den drei Ermittlungsplaetzen"
  fi
else
  fail "PlayerInventory.cs nicht gefunden"
fi

# ---- ein Klon baut nicht ein zweites Mal --------------------------------------------------
#
# Jedes Ausruestungsstueck erreicht die Welt als KLON: EquipmentRuntimeFactory haelt je Id eine
# lebende Vorlage und instanziiert sie. Instantiate kopiert GameObjects und Komponenten und
# traegt KEINE Referenz mit, die in einer Auto-Property (CarriedRoot) oder einem nicht
# oeffentlichen Feld (_dropCollider) steht - Unity serialisiert beide nicht. Der Klon kommt also
# mit Visual und Wurfkapsel VORHANDEN und beiden Referenzen NULL an, und genau das liest
# "if (CarriedRoot != null) return;" als "es wurde noch nichts gebaut".
#
# Gebaut wurde dann ein zweites Visual exakt auf dem ersten. Zwei deckungsgleiche Kopien
# desselben Meshes sehen aus wie ein Objekt - es war nie als Doppelung zu sehen, sondern als
# Gegenstand, den der Interakt-Strahl nicht aufloesen konnte. Ein Name reicht als Identitaet
# nicht: das Visual heisst wie der Gegenstand. Eine KOMPONENTE ueberlebt die Kopie.
HEB="Assets/CatchIfYouCan/Scripts/Equipment/HeldEquipmentBase.cs"
VF="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentVisualFactory.cs"
MARK="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentVisualRoot.cs"

if [ -f "$MARK" ] && [ -f "$VF" ] &&
   sed 's://.*::' "$VF" | grep -qE 'AddComponent<EquipmentVisualRoot>\(\)'; then
  ok "das gebaute Visual traegt eine Marke, die Instantiate ueberlebt"
else
  fail "das gebaute Visual traegt eine Marke, die Instantiate ueberlebt"
  printf '        eine Referenz ueberlebt den Klon nicht, ein GameObject schon\n'
fi

if [ -f "$HEB" ] &&
   sed 's://.*::' "$HEB" | grep -qE 'if \(AdoptClonedVisual\(\)\)' &&
   sed 's://.*::' "$HEB" | grep -qE 'GetComponentInChildren<EquipmentVisualRoot>\(true\)'; then
  ok "ein Klon uebernimmt sein Visual, statt ein zweites zu bauen"
else
  fail "ein Klon uebernimmt sein Visual, statt ein zweites zu bauen"
  printf '        zwei deckungsgleiche Meshes sehen aus wie eines und sind es nicht\n'
fi

# Und die Uebernahme greift VOR dem Bau. Danach ist sie wirkungslos.
ADOPT_LINE="$(sed 's://.*::' "$HEB" | grep -n 'AdoptClonedVisual()' | head -1 | cut -d: -f1)"
BUILD_LINE="$(sed 's://.*::' "$HEB" | grep -n 'CarriedRoot = EquipmentVisualFactory.Build(' | head -1 | cut -d: -f1)"
if [ -n "$ADOPT_LINE" ] && [ -n "$BUILD_LINE" ] && [ "$ADOPT_LINE" -lt "$BUILD_LINE" ]; then
  ok "die Uebernahme steht vor dem Bau, nicht dahinter"
else
  fail "die Uebernahme steht vor dem Bau, nicht dahinter"
fi

# The authored profiles carry the same two paths and are read in the editor, so a profile that
# drifts from the factory is a difference between what a build does and what the inspector
# shows - the worst kind, because the inspector is where somebody would go to check.
bad_prof=0
for prof in "$VIS"/VisualProfile_*.asset; do
  [ -f "$prof" ] || continue
  pm=$(grep '^  modelResourcePath:' "$prof" | sed 's/^  modelResourcePath: *//')
  pt=$(grep '^  modelMaterialPath:' "$prof" | sed 's/^  modelMaterialPath: *//')
  if [ -n "$pm" ] && ! ls "$RES/$pm".* >/dev/null 2>&1; then
    fail "$(basename "$prof") names a model that does not exist: $pm"
    bad_prof=$((bad_prof + 1))
  fi
  if [ -n "$pt" ] && [ ! -f "$RES/$pt.mat" ]; then
    fail "$(basename "$prof") names a material that does not exist: $pt"
    bad_prof=$((bad_prof + 1))
  fi
done
[ "$bad_prof" -eq 0 ] \
  && ok "every authored visual profile resolves the art it names" \
  || fail "$bad_prof authored visual profile path(s) resolve to nothing"

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

# ---------------------------------------------------------------- ein Build, definitionsfest

HEB="Assets/CatchIfYouCan/Scripts/Equipment/HeldEquipmentBase.cs"
EVF="Assets/CatchIfYouCan/Scripts/Equipment/EquipmentVisualFactory.cs"

# Awake darf nicht mehr blind bauen. AddComponent fuehrt Awake synchron aus, also ist die
# Definition dort bei jedem per Code gebauten Gegenstand noch null - und ein blind gebautes
# Visual muss danach weggeworfen und neu gebaut werden. Jeder Schritt in diesem Tanz ist eine
# Gelegenheit, das Ergebnis zu verlieren, und genau das ist wiederholt passiert.
if sed 's://.*::' "$HEB" | tr -d '\n' | tr -s ' ' \
     | grep -qE 'if \(definition != null\) BuildCarried\(\);'; then
  ok "Awake baut das Visual nur mit Definition"
else
  fail "Awake baut das Visual nur mit Definition" \
       "ein blinder Build erzwingt ein spaeteres Wegwerfen und Neubauen"
fi

# BindDefinition baut direkt, wenn noch nichts da ist - das ist der Weg ohne Destroy.
if sed 's://.*::' "$HEB" | tr -d '\n' | tr -s ' ' \
     | grep -qE 'if \(CarriedRoot == null\) \{ BuildCarried\(\); OnCarriedRebuilt\(\);'; then
  ok "BindDefinition baut das Visual direkt, ohne Umweg"
else
  fail "BindDefinition baut das Visual direkt, ohne Umweg"
fi

# Und ein Netz, das nichts ohne Visual stehen laesst.
if sed 's://.*::' "$HEB" | grep -qE 'protected virtual void Start\(\)'; then
  ok "ein Gegenstand ohne Definition bekommt sein Visual spaetestens in Start"
else
  fail "ein Gegenstand ohne Definition bekommt sein Visual spaetestens in Start"
fi

# Produktionskunst bekommt keinen Ersatz. Ein Platzhalter statt eines Ladefehlers sieht aus
# wie ein halbfertiger Gegenstand, und dann sucht niemand nach dem Pfad.
if sed 's://.*::' "$EVF" | tr -d '\n' | tr -s ' ' \
     | grep -qE 'if \(!profile\.IsDevPlaceholder\) \{ measuredLength = profile\.Length; return carried;'; then
  ok "ein fehlgeschlagener Modell-Ladevorgang bekommt keinen stillen Platzhalter"
else
  fail "ein fehlgeschlagener Modell-Ladevorgang bekommt keinen stillen Platzhalter" \
       "echte Kunst muss laut scheitern statt als Kapsel weiterzulaufen"
fi

# Die Diagnosepose darf nie eingeschaltet ausgeliefert werden.
if sed 's://.*::' "$FL" | grep -qE 'private bool forceFlashlightDebugPose *= *true'; then
  fail "die Diagnosepose ist ausgeliefert ausgeschaltet"
else
  ok "die Diagnosepose ist ausgeliefert ausgeschaltet"
fi

# Und sie muss wirklich jede automatische Platzierung abschalten, sonst misst sie die andere.
if sed 's://.*::' "$HEB" | tr -d '\n' | tr -s ' ' | grep -qE 'if \(SuppressAutomaticPose\) return;' \
   && sed 's://.*::' "$FL" | grep -qE 'override bool SuppressAutomaticPose'; then
  ok "die Diagnosepose schaltet jede automatische Platzierung ab"
else
  fail "die Diagnosepose schaltet jede automatische Platzierung ab"
fi

# ---------------------------------------------------------------- gemessen wird lokal

# Renderer.bounds ist eine Welt-AABB. Sie zu lesen und daraus eine LOKALE Skalierung zu
# berechnen stimmt nur, solange jeder Vorfahre Skalierung 1 hat - sonst wird die Elternkette
# ein zweites Mal angewendet. Das ist zweimal in diesem Projekt passiert und sah beide Male
# vollkommen verschieden aus: eine Taschenlampe mit 2 mm Laenge in der Hand, und Waende
# hundertmal zu gross. Dieselbe Zeile, zwei Dateien.
if sed 's://.*::' "Assets/CatchIfYouCan/Scripts/Equipment/EquipmentVisualFactory.cs" \
     | grep -qE 'TryMeasureLocal\(model, renderers, out Bounds bounds\)'; then
  ok "das Ausruestungs-Visual wird im eigenen Raum des Modells gemessen"
else
  fail "das Ausruestungs-Visual wird im eigenen Raum des Modells gemessen" \
       "eine Welt-AABB haengt davon ab, wo der Spieler steht und was ueber dem Objekt haengt"
fi

# Und die Skalierung wird nachgemessen statt geglaubt.
if sed 's://.*::' "Assets/CatchIfYouCan/Scripts/Equipment/EquipmentVisualFactory.cs" \
     | grep -qE 'achieved < target \* 0\.5f \|\| achieved > target \* 2f'; then
  ok "die erreichte Groesse wird gegen die gewuenschte geprueft"
else
  fail "die erreichte Groesse wird gegen die gewuenschte geprueft" \
       "ein Gegenstand, der als Millimeter ankommt, sieht aus wie eine leere Hand"
fi

# Der Raumbauer, der frueher hier geprueft wurde, ist mit dem Kenney-Hausbestand entfernt
# worden. Eine allgemeine Fassung dieser Pruefung - "keine Methode rechnet eine lokale
# Skalierung aus einer Welt-AABB" - wurde versucht und wieder verworfen: sie meldet
# NathanCharacterSetup, EquipmentVisualFactory und HeldFlashlight, und alle drei lesen
# Renderer.bounds nur, um zu MESSEN und zu berichten, nie um dadurch zu teilen. Ob eine
# Groesse als Divisor dient, laesst sich mit grep nicht entscheiden, und ein Waechter, der
# falsch anschlaegt, ist schlechter als keiner.
#
# Die Deckung gegen Fehler 12 bleibt trotzdem bestehen, und zwar dort, wo der Code noch steht:
# zwei Zeilen weiter oben wird geprueft, dass EquipmentVisualFactory im eigenen Raum des
# Modells misst (TryMeasureLocal) und dass die erreichte Groesse gegen die gewuenschte
# geprueft wird. Das sind die beiden Zeilen, die der Fehler damals gebrochen hat.

# ------------------------------------------------------- die Projektion des DOTS-Projektors
#
# Die Projektion ist ein WINKELRASTER um einen Ursprung. Ein Shader rekonstruiert je Pixel die
# Weltposition der Flaeche DAHINTER aus dem Tiefenpuffer, nimmt die Richtung von der Linse zu
# diesem Punkt und fragt, ob sie auf einen Punkt eines Kugelrasters faellt. Die Punkte gehoeren
# also den Flaechen, auf die sie fallen - Boden, Wand, Decke, Moebel -, und im Szenengraph gibt
# es keinen einzigen Punkt: kein GameObject, kein Partikel, kein Licht je Punkt.
#
# Zwei Vorgaenger waren geformt wie der Emitter statt wie der Raum: ein Spot mit 70 Grad, also
# eine Taschenlampe, und danach fuenf breitere Spots, also eine Taschenlampe mit Begleitung.
# Keiner davon konnte eine Kugel abdecken, weil ein Kegel das nicht kann. Und beide brauchten
# ein LICHT, das den Raum aufhellt; dieses hier addiert nur dort etwas, wo ein Punkt ist.
#
# Jede Zeile hier deckt einen Zustand ab, in dem der Projektor eingeschaltet ist und trotzdem
# nichts zu sehen ist - und die sehen alle gleich aus.

PROJ="Assets/CatchIfYouCan/Scripts/Equipment/SpectralGridProjection.cs"
SHDR="Assets/CatchIfYouCan/Shaders/SpectralGrid.shader"

# ============================================================ the DOTS field, as real geometry
#
# The shader used to reconstruct the world position of the surface behind each pixel from the
# scene depth texture. The technique is right and the file compiled - the diagnostic ladder
# proved both, with a magenta rung that drew and a screen-UV rung that drew a correct gradient.
# The rung below them read the RAW value out of SampleSceneDepth with nothing on top of it and
# came back flat blue: exactly 0.0 at every pixel on the screen. No depth texture reaches that
# pass in this project, on this platform, in this Unity version.
#
# So the effect is geometry now. Rays go out of the lens in every direction and one quad is laid
# flat on each surface they hit, all of them in ONE mesh on ONE renderer. The checks below guard
# that shape: no sampled frame-buffer state anywhere (there is nothing left to be unbound), a
# covering that is a sphere by construction rather than by a shader working, and a cost that is
# proportional to how much the device MOVES rather than to how many pixels it covers.

if [ ! -f "$PROJ" ]; then
  fail "SpectralGridProjection.cs existiert"
else
  pcode=$(sed 's://.*::' "$PROJ" | grep -v '^[[:space:]]*\*')
  PROJECTOR="$SGP"
  prcode=$([ -f "$PROJECTOR" ] && sed 's://.*::' "$PROJECTOR" | grep -v '^[[:space:]]*\*' || true)

  # 1. There is something that draws, and exactly one of it.
  if printf '%s' "$pcode" | grep -qE 'AddComponent<MeshRenderer>\(\)' \
     && printf '%s' "$pcode" | grep -qE 'AddComponent<MeshFilter>\(\)'; then
    ok "die Projektion baut genau einen Renderer und ein Mesh"
  else
    fail "die Projektion baut genau einen Renderer und ein Mesh"
  fi

  # 2. Kein Objekt je Punkt. Das ist die eine Grenze, die diese Technik ueberhaupt bezahlbar
  #    macht: tausende Punkte sind tausende Vierecke in EINEM Mesh, nicht tausende GameObjects,
  #    Partikel, Lichter oder Decals.
  build=$(printf '%s\n' "$pcode" | sed -n '/private void Rebuild(/,/^        }$/p')
  if [ -n "$build" ] \
     && ! printf '%s' "$build" | grep -qE 'new GameObject|Instantiate|AddComponent|new Light'; then
    ok "kein GameObject, kein Partikel und kein Licht je Punkt"
  else
    fail "kein GameObject, kein Partikel und kein Licht je Punkt"
  fi

  # 3. Das Feld ist eine KUGEL, weil die Richtungen eine sind. Eine Fibonacci-Kugel laeuft y von
  #    +1 nach -1 und deckt damit alle 4-pi Steradiant gleichmaessig ab - Boden, Decke und jede
  #    Wand. Keine Drehung des Geraets kann eine Halbkugel entfernen, sie dreht das Muster nur.
  #    Zwei fruehere Anlaeufe waren Kegel (Fehler 39), und ein Kegel kann keine Kugel abdecken.
  if printf '%s' "$build" | grep -qE 'float y = 1f - \(i \+ 0\.5f\) \* 2f / wanted' \
     && printf '%s' "$build" | grep -qE 'GoldenAngle'; then
    ok "die Richtungen decken die ganze Kugel ab, nicht einen Kegel"
  else
    fail "die Richtungen decken die ganze Kugel ab, nicht einen Kegel"
  fi

  # 4. Und die Drehung des Geraets dreht das Muster, statt es zu beschneiden.
  if printf '%s' "$build" | grep -qE '_origin\.TransformDirection\(localDir\)'; then
    ok "die Geraetedrehung dreht das Muster, statt eine Halbkugel zu entfernen"
  else
    fail "die Geraetedrehung dreht das Muster, statt eine Halbkugel zu entfernen"
  fi

  # 5. Der Strahl startet AUSSERHALB des eigenen Gehaeuses, und ein Treffer am eigenen Geraet
  #    wird verworfen. Beginnt er in der Linse, trifft er sofort das Geraet selbst: das ganze
  #    Feld waere eine Handvoll Punkte auf dem eigenen Gehaeuse - sichtbar und voellig falsch.
  if printf '%s' "$build" | grep -qE 'dir \* RayStartOffset' \
     && printf '%s' "$build" | grep -qE 'IsChildOf\(deviceRoot\)'; then
    ok "ein Strahl startet ausserhalb des Geraets und trifft es nicht selbst"
  else
    fail "ein Strahl startet ausserhalb des Geraets und trifft es nicht selbst"
  fi

  # 6. Die Strahlen laufen NICHT je Bild. Das ist die ganze Leistungsstrategie: der teure Teil
  #    haengt daran, wie weit sich das Geraet bewegt, und ein aufgestelltes Geraet bewegt sich
  #    nicht. Ohne die Schwelle waeren es tausende Raycasts je Bild - genau der Fehler, den
  #    dieses Geraet schon einmal hatte.
  late=$(printf '%s\n' "$pcode" | sed -n '/private void LateUpdate/,/^        }$/p')
  if printf '%s' "$late" | grep -qE 'rebuildMinInterval' \
     && printf '%s' "$late" | grep -qE 'rebuildMoveThreshold' \
     && printf '%s' "$late" | grep -qE 'rebuildTurnThreshold'; then
    ok "die Strahlen laufen auf Bewegung, nicht je Bild"
  else
    fail "die Strahlen laufen auf Bewegung, nicht je Bild"
  fi

  # 7. Und je Bild alloziert nichts. UVs und Indizes stehen nach dem ersten Wachsen fest, ein
  #    Neuaufbau schreibt nur Positionen und Farben, und der Property-Block wird einmal angelegt.
  if ! printf '%s' "$build" | grep -qE 'new (Vector3|Vector2|Color32|int)\[' \
     && ! printf '%s' "$late" | grep -qE 'new [A-Za-z]'; then
    ok "ein Neuaufbau alloziert keine Felder"
  else
    fail "ein Neuaufbau alloziert keine Felder"
  fi

  # 8. Der ungenutzte Rest des Meshes wird eingeklappt statt stehengelassen. Ein Slot, der nicht
  #    beschrieben wurde, haelt noch das Viereck der letzten Runde - an einer Stelle, an der
  #    diesmal nichts getroffen wurde. Zusammengefaltet auf die Linse hat er keine Flaeche.
  upload=$(printf '%s\n' "$pcode" | sed -n '/private void UploadMesh/,/^        }$/p')
  if printf '%s' "$upload" | grep -qE 'for \(int i = placed; i < _capacity' ; then
    ok "die nicht belegten Mesh-Plaetze werden eingeklappt, nicht stehengelassen"
  else
    fail "die nicht belegten Mesh-Plaetze werden eingeklappt, nicht stehengelassen"
  fi

  # 9. Die Grenzen werden GESETZT statt berechnet. RecalculateBounds laeuft ueber jeden Vertex,
  #    und die Antwort steht ohnehin fest: weiter als die Reichweite kann das Feld nicht kommen.
  if printf '%s' "$upload" | grep -qE '_mesh\.bounds = new Bounds' \
     && ! printf '%s' "$upload" | grep -qE 'RecalculateBounds'; then
    ok "die Mesh-Grenzen werden gesetzt statt ueber jeden Vertex berechnet"
  else
    fail "die Mesh-Grenzen werden gesetzt statt ueber jeden Vertex berechnet"
  fi

  # 10. Ausgeschaltet ist ausgeschaltet, und eingeschaltet baut nicht neu auf.
  if printf '%s' "$pcode" | grep -qE '_renderer\.enabled = running'; then
    ok "SetRunning schaltet den Renderer mit, statt das Rig neu zu bauen"
  else
    fail "SetRunning schaltet den Renderer mit, statt das Rig neu zu bauen"
  fi

  # 11. Ein Klon uebernimmt, was er schon mitbringt. Jedes Ausruestungsstueck erreicht die Welt
  #     als Instantiate einer lebenden Vorlage: die Kinder sind dann schon da, die privaten
  #     Felder, die auf sie zeigten, nicht (Fehler 27, 30 und 46).
  if printf '%s' "$pcode" | grep -qE 'transform\.Find\(OriginChildName\)' \
     && printf '%s' "$pcode" | grep -qE 'Find\(RigChildName\)' \
     && printf '%s' "$pcode" | sed -n '/public static SpectralGridProjection Attach/,/^        }$/p' \
          | grep -qE 'GetComponentInChildren<SpectralGridProjection>'; then
    ok "ein geklonter Projektor uebernimmt Linse, Rig und Projektion"
  else
    fail "ein geklonter Projektor uebernimmt Linse, Rig und Projektion"
  fi

  # 12. Und er MELDET, wenn doch zwei da sind - vom Geraet aus gezaehlt, nicht von sich selbst.
  #     Von sich selbst aus findet er nur seine eigenen Kinder und meldet eine saubere 1, waehrend
  #     die zweite Projektion nebenan haengt.
  if printf '%s' "$pcode" | grep -qE 'projectionCountUnderProjector=' \
     && printf '%s' "$pcode" | grep -qE 'volumeCountUnderProjector=' \
     && printf '%s' "$pcode" | grep -qE 'GetComponentInParent<SpectralGridProjector>'; then
    ok "ein Projektor mit zwei Projektionen meldet sich, vom Geraet aus gezaehlt"
  else
    fail "ein Projektor mit zwei Projektionen meldet sich, vom Geraet aus gezaehlt"
  fi

  # 13. Dasselbe eine Ebene hoeher: BuildCarried uebernimmt den geerbten Kopf, statt einen
  #     zweiten zu bauen. Das geerbte Paar ist stumm, weshalb es sich nie gemeldet hat.
  if [ -f "$PROJECTOR" ]; then
    built=$(printf '%s\n' "$prcode" | sed -n '/protected override void BuildCarried/,/^        }$/p')
    adoptline=$(printf '%s\n' "$built" | { grep -n 'GetComponentInChildren<SpectralGridProjection>' || true; } | head -1 | cut -d: -f1)
    newline=$(printf '%s\n' "$built" | { grep -n 'new GameObject("ProjectorHead")' || true; } | head -1 | cut -d: -f1)
    if [ -n "$adoptline" ] && [ -n "$newline" ] && [ "$adoptline" -lt "$newline" ]; then
      ok "ein geklonter Projektor uebernimmt seinen Kopf, statt einen zweiten zu bauen"
    else
      fail "ein geklonter Projektor uebernimmt seinen Kopf, statt einen zweiten zu bauen (adopt=$adoptline new=$newline)"
    fi
  else
    fail "ein geklonter Projektor uebernimmt seinen Kopf, statt einen zweiten zu bauen"
  fi

  # 14. Das Rig ist als WIRKUNG markiert, nicht als Koerper. Das Punktemesh umspannt die ganze
  #     Reichweite; mitgemessen hob die Lobby einen 0,25-m-Projektor ueber die Decke (Fehler 42).
  if printf '%s' "$pcode" | grep -qE 'EffectVolume\.Mark\(_rig\.gameObject\)'; then
    ok "das Punktemesh ist als Wirkung markiert, nicht als Koerper"
  else
    fail "das Punktemesh ist als Wirkung markiert, nicht als Koerper"
  fi

  # 15. Der Bericht nennt die ZAHL DER PUNKTE zuerst. Diese eine Zahl trennt die beiden Fehler,
  #     die dieses Geraet sein Leben lang verwechselt hat: nie geworfen (null Punkte - rundherum
  #     ist keine Geometrie) und geworfen und nicht gezeichnet (tausende Punkte, schwarzer Schirm).
  if printf '%s' "$pcode" | grep -qE 'dotsPlaced=' \
     && printf '%s' "$pcode" | grep -qE 'NOT ONE RAY HIT ANYTHING'; then
    ok "der Bericht nennt die Zahl der gesetzten Punkte und den Fall null gesondert"
  else
    fail "der Bericht nennt die Zahl der gesetzten Punkte und den Fall null gesondert"
  fi

  # 16. Getragen wirft weniger Strahlen als aufgestellt. Ein getragener Projektor wird staendig
  #     neu geworfen; ein aufgestellter einmal. Gleiche Zahl fuer beides heisst, eine der beiden
  #     ist falsch.
  dep=$(printf '%s' "$pcode" | sed -n 's/.*private int deployedDotCount = \([0-9]*\).*/\1/p' | head -1)
  car=$(printf '%s' "$pcode" | sed -n 's/.*private int carriedDotCount = \([0-9]*\).*/\1/p' | head -1)
  if [ -n "$dep" ] && [ -n "$car" ] && [ "$car" -lt "$dep" ] && [ "$dep" -ge 2000 ]; then
    ok "getragen wirft weniger Strahlen als aufgestellt (getragen=$car aufgestellt=$dep)"
  else
    fail "getragen wirft weniger Strahlen als aufgestellt (getragen='$car' aufgestellt='$dep')"
  fi

  # 17. Die Reichweite ist endlich, einstellbar und steht auf 5,5 m.
  if printf '%s' "$pcode" | grep -qE 'Range\(2f, 10f\)\] private float projectionRange = 5\.5f'; then
    ok "die Reichweite ist einstellbar, endlich und steht auf 5,5 m"
  else
    fail "die Reichweite ist einstellbar, endlich und steht auf 5,5 m"
  fi
fi

# ------------------------------------------------------------------ und der Shader selbst
if [ ! -f "$SHDR" ]; then
  fail "SpectralGrid.shader existiert"
else
  scode=$(sed 's|//.*||' "$SHDR")

  # 18. NICHTS aus dem Framebuffer. Das ist der ganze Grund fuer den Umbau: eine Tiefentextur,
  #     die diesen Pass nicht erreicht, hat drei Fassungen dieses Effekts unsichtbar gemacht, und
  #     es gab keine Moeglichkeit, das von innen zu sehen. Was nicht gelesen wird, kann nicht
  #     fehlen - jede Eingabe ist jetzt ein Vertex-Attribut oder eine Material-Eigenschaft.
  if ! printf '%s' "$scode" | grep -qE 'SampleSceneDepth|_CameraDepthTexture|DeclareDepthTexture|ComputeWorldSpacePosition|UNITY_MATRIX_I_VP|ComputeScreenPos|_CameraOpaqueTexture'; then
    ok "der Shader liest nichts aus dem Framebuffer, also kann nichts davon fehlen"
  else
    fail "der Shader liest nichts aus dem Framebuffer, also kann nichts davon fehlen"
  fi

  # 19. Der Punkt ist GERECHNET, nicht gesampelt. Fehler 35: dieselbe Zeichnung kam als Pillen
  #     an, weil drei Importer-Voreinstellungen fuer eine Maske alle drei falsch sind. Ein
  #     Abstand vom eigenen Mittelpunkt hat keinen Sampler, der ihn verformen koennte.
  if printf '%s' "$scode" | grep -qE 'length\(input\.uv - 0\.5\)' \
     && ! printf '%s' "$scode" | grep -qE 'SAMPLE_TEXTURE2D|sampler_'; then
    ok "ein Punkt ist gerechnet statt gesampelt, also kann kein Filter ihn zur Pille ziehen"
  else
    fail "ein Punkt ist gerechnet statt gesampelt, also kann kein Filter ihn zur Pille ziehen"
  fi

  # 20. Rein additiv. Wo kein Punkt ist, kommt Schwarz heraus und aendert exakt nichts - das ist
  #     es, was einen dunklen Raum dunkel laesst, statt ihn gruen zu waschen (Fehler 40).
  if printf '%s' "$scode" | grep -qE 'Blend One One' \
     && printf '%s' "$scode" | grep -qE 'ZWrite Off'; then
    ok "die Punkte addieren Licht und fluten den Raum nicht"
  else
    fail "die Punkte addieren Licht und fluten den Raum nicht"
  fi

  # 21. Kein eingebauter Ersatz-Shader. Ein FallBack zeichnet unter URP Magenta (Fehler 2).
  if printf '%s' "$scode" | grep -qE 'FallBack Off'; then
    ok "der Shader hat keinen eingebauten Ersatz, der magenta zeichnen koennte"
  else
    fail "der Shader hat keinen eingebauten Ersatz, der magenta zeichnen koennte"
  fi

  # 22. Die Diagnose steht auf 0 - in C# UND im Shader. Eingeschaltet ausgeliefert malt sie jeden
  #     Punkt magenta. Ein Werkzeug, das laeuft, waehrend jemand spielt, diagnostiziert nicht
  #     mehr, sondern erzeugt (Fehler 23).
  dbg_cs=0
  dbg_sh=0
  if [ -f "$PROJ" ] && printf '%s' "$pcode" | grep -qE 'Range\(0, 1\)\] private int debugStage = 0'; then
    dbg_cs=1
  fi
  if printf '%s' "$scode" | grep -qE '_DebugMode \("Debug Stage \(0 = off\)", Range\(0, 1\)\) = 0'; then
    dbg_sh=1
  fi
  if [ "$dbg_cs" -eq 1 ] && [ "$dbg_sh" -eq 1 ]; then
    ok "die Diagnosestufe steht in C# UND im Shader auf 0"
  else
    fail "die Diagnosestufe steht in C# UND im Shader auf 0 (cs=$dbg_cs shader=$dbg_sh)"
  fi

  # 23. Jede Eigenschaft, die C# schiebt, deklariert der Shader - und umgekehrt. Gelesen aus den
  #     echten _block.Set-Aufrufen statt aus den PropertyToID-Zeilen: eine Id zu HABEN beweist
  #     nicht, dass geschoben wird, und genau daran blieb die erste Fassung dieser Pruefung gruen.
  if [ -f "$PROJ" ]; then
    pushed=$(printf '%s\n' "$pcode" \
             | sed -n 's/.*int \([A-Za-z0-9_]*\) = Shader\.PropertyToID("\([A-Za-z_][A-Za-z0-9_]*\)").*/\1 \2/p' \
             | while read -r idvar propname; do
                 if printf '%s\n' "$pcode" | grep -qE "_block\.Set[A-Za-z]+\([[:space:]]*$idvar[[:space:]]*,"; then
                   printf '%s\n' "$propname"
                 fi
               done | sort -u)
    declared=$(printf '%s\n' "$scode" \
                 | sed -n '/^    Properties$/,/^    }$/p' \
                 | sed -n 's/^[[:space:]]*\(\[[A-Za-z]*\][[:space:]]*\)\?\(_[A-Za-z0-9_]*\)[[:space:]]*(.*/\2/p' | sort -u)
    onlypush=$(comm -23 <(printf '%s\n' "$pushed") <(printf '%s\n' "$declared") | tr '\n' ' ')
    onlydecl=$(comm -13 <(printf '%s\n' "$pushed") <(printf '%s\n' "$declared") | tr '\n' ' ')
    if [ -z "$(printf '%s' "$onlypush$onlydecl" | tr -d ' ')" ]; then
      ok "jede geschobene Eigenschaft ist deklariert, und jede deklarierte wird geschoben"
    else
      fail "jede geschobene Eigenschaft ist deklariert, und jede deklarierte wird geschoben (nur geschoben:$onlypush nur deklariert:$onlydecl)"
    fi
  else
    fail "jede geschobene Eigenschaft ist deklariert, und jede deklarierte wird geschoben"
  fi
fi


# ---------------------------------------------------- an effect is not a body
#
# The DOTS projector draws its dots by giving a fragment shader pixels to run on: the mesh
# it renders is an eleven-metre box around the lens, and it is not the device. Everything
# that asks "how big is this item" has to leave that box out.
#
# Unguarded it did not: the lobby lays an item down by measuring its box and lifting it
# until its underside rests on the floor, and with the volume measured in, the 0.25 m
# projector was raised 5.19 m and came to rest at y = 6.49 - above a three-metre ceiling
# over a floor at zero. From inside the game that is not a floating object; it is an item
# that never spawned, which is the same report as a build failure and a different bug.

MARKER="Assets/CatchIfYouCan/Scripts/Equipment/EffectVolume.cs"
HELD="Assets/CatchIfYouCan/Scripts/Equipment/HeldEquipmentBase.cs"
TABLE="Assets/CatchIfYouCan/Scripts/Environment/LobbyEquipmentTable.cs"

# 19. The marker is a COMPONENT. Every equipment item reaches the world as a clone of a
#     living template, and a component is the one thing Instantiate brings with it - a name,
#     a tag, a layer or a private field is not (mistake 30).
if [ -f "$MARKER" ] && grep -qE 'class EffectVolume[[:space:]]*:[[:space:]]*MonoBehaviour' "$MARKER"; then
  ok "an effect volume is marked by a component, which survives Instantiate"
else
  fail "an effect volume is marked by a component, which survives Instantiate"
fi

# 20. And marking one asks GetComponent BEFORE AddComponent. This runs on the template and
#     on every clone of it; AddComponent on a [DisallowMultipleComponent] type that is
#     already there returns null, and the next line dereferences it (mistake 27).
if [ -f "$MARKER" ]; then
  # The window is deliberately narrow. Asking only "does a GetComponent appear earlier in
  # the file" passes on the one in Encloses twenty lines above, which is a different
  # question - a guard that is satisfied by an unrelated line is mistake 8 in a shell.
  mcode=$(sed 's://.*::' "$MARKER" | grep -v '^[[:space:]]*\*')
  addline=$(printf '%s\n' "$mcode" | grep -n 'AddComponent<EffectVolume>()' | head -1 | cut -d: -f1)
  near=""
  if [ -n "$addline" ]; then
    from=$((addline - 4)); [ "$from" -lt 1 ] && from=1
    near=$(printf '%s\n' "$mcode" | sed -n "${from},${addline}p" \
           | grep 'GetComponent<EffectVolume>()' || true)
  fi
  if [ -n "$addline" ] && [ -n "$near" ]; then
    ok "marking an effect volume asks GetComponent before AddComponent"
  else
    fail "marking an effect volume asks GetComponent before AddComponent"
  fi
else
  fail "marking an effect volume asks GetComponent before AddComponent"
fi

# 21. The projector marks its own volume. Unmarked, nothing below can tell it from the
#     device: it is a MeshRenderer on a child, exactly like the model.
if [ -f "$PROJ" ] && printf '%s' "$pcode" | grep -qE 'EffectVolume\.Mark\(_rig\.gameObject\)'; then
  ok "the projector marks its projection volume as an effect volume"
else
  fail "the projector marks its projection volume as an effect volume"
fi

# 22. The body the interaction ray aims at is measured WITHOUT it. Measured in, the thing
#     the player aims at is an eleven-metre box rather than the device in front of them.
if [ -f "$HELD" ] && sed 's://.*::' "$HELD" | grep -v '^[[:space:]]*\*' \
     | grep -qE 'EffectVolume\.Encloses\(r\.transform\)'; then
  ok "the pickup trigger is measured without the item's effect volumes"
else
  fail "the pickup trigger is measured without the item's effect volumes"
fi

# 23. And so is the lobby's "sit it on the floor" measurement, in BOTH its passes -
#     colliders first, renderers as the fallback - because either one reaching the volume
#     is the 6.49 m lift again.
if [ -f "$TABLE" ]; then
  tcode=$(sed 's://.*::' "$TABLE" | grep -v '^[[:space:]]*\*')
  tcoll=$(printf '%s' "$tcode" | grep -cE 'EffectVolume\.Encloses\(c\.transform\)' || true)
  trend=$(printf '%s' "$tcode" | grep -cE 'EffectVolume\.Encloses\(r\.transform\)' || true)
  if [ "$tcoll" -ge 1 ] && [ "$trend" -ge 2 ]; then
    ok "the lobby measures an item's resting box without its effect volumes"
  else
    fail "the lobby measures an item's resting box without its effect volumes (colliders=$tcoll renderers=$trend, wanted >=1 and >=2)"
  fi
else
  fail "the lobby measures an item's resting box without its effect volumes"
fi


# 29. Die Verdeckung ist WEG - auch aus dem C#. Sie stand voreingestellt auf aus, und ein
#     Regler, den niemand aufdreht, ist trotzdem eine Zeile, die jeder Leser fuer erprobt
#     haelt. Der Marsch ist ein SCREEN-SPACE-Test: er kann nur fragen, was die KAMERA an einer
#     Stelle sieht, und fuer einen Punkt, der meterweit von der beleuchteten Flaeche in der
#     Luft haengt, ist das oft eine naehere Flaeche, die mit der Linse nichts zu tun hat - ein
#     falscher Verdecker, der einen Punkt loescht, den es geben muesste. Er kommt zurueck, wenn
#     eine Kamera bestaetigt hat, dass ueberhaupt Punkte gezeichnet werden, und nicht davor.
#     Geprueft wird hier das C#: der Shader hat seine eigene Pruefung (25), und ein Feld, das
#     ins Leere pusht, ist genau die Stille, an der eine Sitzung verlorenging (Fehler 44).
if [ -f "$PROJ" ] && ! printf '%s' "$pcode" | grep -qE 'occlusionStrength|occlusionBias|glowRadius|glowStrength|facingStrength|nearBoost|nearFade'; then
  ok "die unbestaetigten Regler sind auch aus dem C# verschwunden, nicht nur abgedreht"
else
  fail "die unbestaetigten Regler sind auch aus dem C# verschwunden, nicht nur abgedreht"
fi

# 31. Ein Regler, der erreicht, was er benennt. Die Werte wurden EINMAL gepusht, beim
#     Einschalten, und nie wieder: eine Aenderung im Inspector waehrend des Spiels landete
#     nirgends, und eine sechsstufige Diagnose kam mit sechs identischen Stufen zurueck. Alle
#     sechs waren Stufe 0. Ein Regler, der das nicht bewegen kann, was er benennt, ist schlimmer
#     als keiner, weil er BEWEISE erzeugt (Fehler 44). Hier schiebt OnValidate direkt und setzt
#     ausserdem die Wurfschwelle zurueck, damit auch Zahlen ankommen, die das Feld neu werfen.
if [ -f "$PROJ" ] && printf '%s' "$pcode" | sed -n '/private void OnValidate/,/^        }$/p' \
     | grep -qE 'PushProperties\(\)'; then
  ok "eine Aenderung im Inspector erreicht den lebenden Renderer"
else
  fail "eine Aenderung im Inspector erreicht den lebenden Renderer"
fi

# 33. Die Dauerbatterie ist eine ENTWICKLERhilfe und aus einem Auslieferungsbuild gezaeunt.
#     Die Batteriearchitektur selbst bleibt unangetastet: gezaeunt wird auf DIESEM Geraet,
#     nicht in EquipmentBase.
#     Der Zaun wird IN der Methode gesucht, nicht irgendwo in der Datei: dieselbe Zeile steht
#     dort noch dreimal um Log-Ausgaben herum, und ein Waechter, den ein unbeteiligter Treffer
#     zufriedenstellt, ist Fehler 8 in neuer Kleidung - beim ersten Zahntest blieb er gruen,
#     nachdem der Zaun um genau diese Methode entfernt worden war.
drain=$(printf '%s\n' "$sgpcode" | sed -n '/protected override void DrainBattery/,/^        }$/p')
if [ -f "$SGP" ] && printf '%s' "$drain" | grep -qE 'developmentInfiniteBattery' \
   && printf '%s' "$drain" | grep -qE '#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD' \
   && printf '%s' "$drain" | grep -qE 'base\.DrainBattery\(\)'; then
  ok "die Dauerbatterie ist Entwicklerhilfe und aus dem Auslieferungsbuild gezaeunt"
else
  fail "die Dauerbatterie ist Entwicklerhilfe und aus dem Auslieferungsbuild gezaeunt"
fi

printf '\npassed: %s   failed: %s\n\n' "$passed" "$failed"

if [ "$failed" -gt 0 ]; then
  printf 'EQUIPMENT CATALOG GUARD FAILED\n'
  exit 1
fi

printf 'EQUIPMENT CATALOG GUARD PASSED\n'
