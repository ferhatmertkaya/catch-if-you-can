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

if [ ! -f "$PROJ" ]; then
  fail "SpectralGridProjection.cs existiert"
else
  pcode=$(sed 's://.*::' "$PROJ" | grep -v '^[[:space:]]*\*')
  PROJECTOR="$SGP"
  prcode=$([ -f "$PROJECTOR" ] && sed 's://.*::' "$PROJECTOR" | grep -v '^[[:space:]]*\*' || true)

  # 1. Es gibt ueberhaupt etwas, das zeichnet.
  if printf '%s' "$pcode" | grep -qE 'AddComponent<MeshRenderer>\(\)'; then
    ok "der Projektor baut ein Zeichenvolumen"
  else
    fail "der Projektor baut ein Zeichenvolumen"
  fi

  # 2. Und es ist KEIN Licht mehr. Ein gruenes Licht im Raum ist genau die Flut, die die
  #    Referenz nicht zeigt: ein dunkler Raum mit hellen Punkten darin.
  if printf '%s' "$pcode" | grep -qE 'AddComponent<Light>|LightType\.|\.intensity[[:space:]]*=[[:space:]]*lightIntensity'; then
    fail "die Projektion flutet den Raum nicht mit einem Licht"
  else
    ok "die Projektion flutet den Raum nicht mit einem Licht"
  fi

  # 3. Der Ursprung ist ein eigener Transform am Geraet - nicht die Kamera, nicht der Spieler,
  #    nicht der Weltursprung.
  if printf '%s' "$pcode" | grep -qE 'OriginChildName = "ProjectionOrigin"' \
     && printf '%s' "$pcode" | grep -qE 'ProjectionOrigin =>'; then
    ok "die Punkte kommen aus einem eigenen Linsen-Transform am Geraet"
  else
    fail "die Punkte kommen aus einem eigenen Linsen-Transform am Geraet"
  fi

  # 4. Und er wird in WELTKOORDINATEN an den Shader gegeben. Die Fassung davor rechnete im
  #    Objektraum und zeigte nichts, aus einem nie geklaerten Grund (Fehler 33). Ueber ein
  #    Uniform kann keine Verschachtelung und keine geerbte Skalierung das Ergebnis kippen.
  if printf '%s' "$pcode" | grep -qE '_OriginWS' \
     && printf '%s' "$pcode" | grep -qE '_AxisYWS'; then
    ok "Linse und Achsen gehen in Weltkoordinaten an den Shader"
  else
    fail "Linse und Achsen gehen in Weltkoordinaten an den Shader"
  fi

  # 5. Ein misslungener Zustand wird als ERROR gemeldet, nicht als Info. "0 von 0" in einer
  #    Info-Zeile hat in diesem Projekt schon einmal eine Sitzung lang niemand gelesen
  #    (Fehler 22).
  if printf '%s' "$pcode" | grep -qE 'CIYCLog\.Error\(block'; then
    ok "ein fehlgeschlagener Projektionszustand wird als Fehler gemeldet"
  else
    fail "ein fehlgeschlagener Projektionszustand wird als Fehler gemeldet"
  fi

  # 6. Das Volumen entsteht einmal, nicht je Frame.
  if printf '%s' "$pcode" | grep -qE 'Mathf\.Approximately\(_builtRange, projectionRange\)'; then
    ok "das Volumen wird einmal gebaut, nicht je Frame"
  else
    fail "das Volumen wird einmal gebaut, nicht je Frame"
  fi

  # 7. Ausgeschaltet ist ausgeschaltet: kein einziger Punkt.
  if printf '%s' "$pcode" | grep -qE '_renderer\.enabled[[:space:]]*=[[:space:]]*running'; then
    ok "SetRunning schaltet das Zeichenvolumen mit"
  else
    fail "SetRunning schaltet das Zeichenvolumen mit"
  fi

  # 8. Und der Klon bekommt kein zweites Volumen. Jedes Ausruestungsstueck erreicht die Welt als
  #    Instantiate einer lebenden Vorlage: die Kinder sind dann schon da, die privaten Felder,
  #    die auf sie zeigten, nicht. Zweimal dasselbe Muster ist doppelte Helligkeit (27 und 30).
  if printf '%s' "$pcode" | grep -qE 'transform\.Find\(OriginChildName\)' \
     && printf '%s' "$pcode" | grep -qE 'transform\.Find\(VolumeChildName\)'; then
    ok "ein geklonter Projektor bekommt kein zweites Volumen"
  else
    fail "ein geklonter Projektor bekommt kein zweites Volumen"
  fi

  # Und er zaehlt vom GERAET aus, nicht von sich selbst. Von sich selbst aus findet er nur
  # seine eigenen Kinder und meldet eine saubere 1, waehrend eine zweite Projektion nebenan
  # haengt - genau der Fall, den `BuildCarried` jahrelang gebaut hat.
  if printf '%s' "$pcode" | grep -qE 'projectionCountUnderProjector=' \
     && printf '%s' "$pcode" | grep -qE 'volumeCountUnderProjector=' \
     && printf '%s' "$pcode" | grep -qE 'GetComponentInParent<SpectralGridProjector>'; then
    ok "ein Projektor mit zwei Volumen meldet sich, vom Geraet aus gezaehlt"
  else
    fail "ein Projektor mit zwei Volumen meldet sich, vom Geraet aus gezaehlt"
  fi

  # Und er BAUT auch keine zweite. Jedes Ausruestungsstueck erreicht die Welt als Klon, und
  # `BuildCarried` legte bis eben unbedingt einen neuen ProjectorHead an - der Klon trug also
  # den geerbten samt Projektion und Volumen UND bekam ein zweites Paar daneben. Das geerbte
  # Paar ist stumm (ein nie eingeschalteter Renderer kommt ausgeschaltet mit), weshalb es sich
  # nie gemeldet hat. Fehler 30, eine Ebene unter der Stelle, an der er behoben wurde.
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

  # Dieselbe Regel in Attach, das den Kopf bekommt und nicht weiss, woher er stammt.
  if printf '%s' "$pcode" \
     | sed -n '/public static SpectralGridProjection Attach/,/^        }$/p' \
     | grep -qE 'GetComponentInChildren<SpectralGridProjection>'; then
    ok "Attach uebernimmt eine vorhandene Projektion, statt eine zweite anzuhaengen"
  else
    fail "Attach uebernimmt eine vorhandene Projektion, statt eine zweite anzuhaengen"
  fi

  # 9. Nichts alloziert je Frame. Der Property-Block wird einmal angelegt und danach nur noch
  #    beschrieben; ein `new MaterialPropertyBlock` in LateUpdate waere Muell je Bild.
  if printf '%s' "$pcode" | grep -qE 'private void LateUpdate' \
     && ! printf '%s' "$pcode" | sed -n '/private void LateUpdate/,/^        }$/p' \
          | grep -qE 'new [A-Za-z]'; then
    ok "die Bild-fuer-Bild-Aktualisierung alloziert nichts"
  else
    fail "die Bild-fuer-Bild-Aktualisierung alloziert nichts"
  fi

  # 10. Und die Reichweite ist einstellbar und endlich.
  if printf '%s' "$pcode" | grep -qE 'projectionRange = 5\.5f' \
     && printf '%s' "$pcode" | grep -qE 'SerializeField, Range\(2f, 10f\)\][[:space:]]*private float projectionRange'; then
    ok "die Reichweite ist einstellbar, endlich und steht auf 5,5 m"
  else
    fail "die Reichweite ist einstellbar, endlich und steht auf 5,5 m"
  fi
fi

# ---- der Shader, der die Punkte macht ------------------------------------------------------
if [ ! -f "$SHDR" ]; then
  fail "SpectralGrid.shader existiert"
else
  scode=$(sed 's|//.*||' "$SHDR")

  # 11. Er rechnet aus dem Tiefenpuffer. Ohne das gaebe es keine Punkte AUF Flaechen, sondern
  #     Punkte in der Luft - und keine Verdeckung durch das, wovor man steht.
  if printf '%s' "$scode" | grep -qE 'SampleSceneDepth' \
     && printf '%s' "$scode" | grep -qE 'ComputeWorldSpacePosition'; then
    ok "der Shader rekonstruiert die Flaeche hinter jedem Pixel"
  else
    fail "der Shader rekonstruiert die Flaeche hinter jedem Pixel"
  fi

  # 12. In KUGELKOORDINATEN, also rundum. Ein Kegeltest hier waere wieder eine Taschenlampe.
  if printf '%s' "$scode" | grep -qE 'atan2\(local\.z, local\.x\)' \
     && printf '%s' "$scode" | grep -qE 'asin\(clamp\(local\.y'; then
    ok "das Raster liegt in Kugelkoordinaten um das Geraet"
  else
    fail "das Raster liegt in Kugelkoordinaten um das Geraet"
  fi

  # 13. Und es gibt keinen Kegelabbruch mehr, der die obere Halbkugel wegschneidet.
  if printf '%s' "$scode" | grep -qE 'coneRadius|_HalfAngle'; then
    fail "kein Kegeltest schneidet die obere Halbkugel weg"
  else
    ok "kein Kegeltest schneidet die obere Halbkugel weg"
  fi

  # 14. Rein additiv: ein Pixel ohne Punkt gibt Schwarz aus und aendert damit gar nichts. Genau
  #     das haelt einen dunklen Raum dunkel, statt ihn gruen zu waschen.
  if printf '%s' "$scode" | grep -qE 'Blend One One'; then
    ok "die Punkte addieren Licht und fluten den Raum nicht"
  else
    fail "die Punkte addieren Licht und fluten den Raum nicht"
  fi

  # 15. Ausserhalb der Reichweite passiert nichts. Ein Projektor ohne Grenze leuchtet das ganze
  #     Haus aus.
  if printf '%s' "$scode" | grep -qE 'dist >= _Range'; then
    ok "Geometrie ausserhalb der Reichweite bekommt nichts"
  else
    fail "Geometrie ausserhalb der Reichweite bekommt nichts"
  fi

  # 16. Der Himmel bekommt keine Punkte - ein Pixel ohne Tiefe ist unendlich weit weg.
  if printf '%s' "$scode" | grep -qE 'rawDepth <= 0\.0' \
     && printf '%s' "$scode" | grep -qE 'rawDepth >= 1\.0'; then
    ok "der Himmel bekommt keine Punkte"
  else
    fail "der Himmel bekommt keine Punkte"
  fi

  # 17. Und die Kantenglaettung ist GEKLAMMERT. fwidth explodiert an der Azimut-Naht und an den
  #     Polen, wo sich die Richtung zwischen zwei Pixeln um eine halbe Drehung aendert -
  #     ungeklammert frisst dieser eine Meridian jeden Punkt, der auf ihm liegt.
  if printf '%s' "$scode" | grep -qE 'clamp\(fwidth\(cellDist\)'; then
    ok "die Kantenglaettung ist an Naht und Polen geklammert"
  else
    fail "die Kantenglaettung ist an Naht und Polen geklammert"
  fi
fi

# 18. Und die Dichte geht als GANZE Zahl hinein. Das Azimutraster laeuft von +PI nach -PI in
#     sich zurueck, und nur eine ganze Zahl von Zellen trifft sich dort wieder; eine gebrochene
#     legt eine sichtbare Naht ueber einen Meridian.
if [ -f "$PROJ" ] && printf '%s' "$pcode" \
     | grep -qE 'SetFloat\(DensityId, Mathf\.Round\(density\)\)'; then
  ok "die Dichte geht als ganze Zellenzahl in den Shader"
else
  fail "die Dichte geht als ganze Zellenzahl in den Shader"
fi


# ------------------------------------------------ the minimal rebuild, and the ladder in it
#
# The shader was cut back to the shortest path from a fragment to a green dot. The occlusion
# march, the glow halo and the ddx/ddy grazing term are GONE - three layers no camera had ever
# confirmed, stacked on a base nobody had confirmed either, on a shader that has never once been
# observed drawing a pixel. What replaces them is a five-rung ladder, and the checks below guard
# the ladder rather than the layers: order, parity and reachability, which are the three things
# no compiler runs in CI to catch (mistake 16).

# 24. The pattern STARTS coarse. A cell is 360/density degrees on both axes, so 144 is a 2.5
#     degree cell - 13 cm apart on a wall 3 m away, under a thousand in view. The old guard
#     demanded 200+ and that was the wrong direction: at 240 a mapping error and a correct
#     field look identical from across a room, so a fine grid that is wrong is a green wash
#     while a coarse one that is wrong is legible. Checked as a CEILING now, with the slider's
#     top end left free - tuning it up once it is right is the whole point of a slider.
if [ -f "$PROJ" ]; then
  dens=$(printf '%s' "$pcode" \
         | sed -n 's/.*private float density = \([0-9.]*\)f.*/\1/p' | head -1)
  if [ -n "$dens" ] && awk "BEGIN{exit !($dens >= 96 && $dens <= 144)}"; then
    ok "the dot grid starts coarse enough to be legible when it is wrong (density=$dens)"
  else
    fail "the dot grid starts coarse enough to be legible when it is wrong (density='$dens', wanted 96..144)"
  fi
else
  fail "the dot grid starts coarse enough to be legible when it is wrong"
fi

# 25. Nothing unproven has crept back. These three came off because no camera had confirmed the
#     rung below them; a NEGATIVE check is the only thing that keeps them off, because each one
#     is individually reasonable and re-adding it costs nobody an argument. They come back when
#     a camera has said the dots draw, and this check is what has to be deleted to do it.
if [ -f "$SHDR" ]; then
  regrown=""
  if printf '%s' "$scode" | grep -qE '_OcclusionStrength|ProjectorOcclusion'; then regrown="$regrown occlusion"; fi
  if printf '%s' "$scode" | grep -qE '_GlowRadius|_GlowStrength'; then regrown="$regrown glow"; fi
  if printf '%s' "$scode" | grep -qE 'ddx\(|ddy\('; then regrown="$regrown screen-space-normal"; fi
  if [ -z "$regrown" ]; then
    ok "the unproven layers stay off until a camera has confirmed the one below them"
  else
    fail "the unproven layers stay off until a camera has confirmed the one below them (back:$regrown)"
  fi
else
  fail "the unproven layers stay off until a camera has confirmed the one below them"
fi

# 26. THE LADDER IS ORDERED, and every rung returns ABOVE the work the next one needs. That is
#     the whole value of it: a rung that sits below the thing it is meant to bisect goes dark
#     for a reason further along, and the reader then has a false finding rather than none -
#     which has already cost this project one session and six of them (mistake 44). So the
#     positions are read out of the file and compared, rather than trusted.
if [ -f "$SHDR" ]; then
  n() { printf '%s\n' "$scode" | { grep -n "$1" || true; } | head -1 | cut -d: -f1; }
  l_frag=$(n 'half4 frag(Varyings input)')
  l_s1=$(n 'if (stage == 1)')
  l_uv=$(n 'float2 screenUV = input.screenPos')
  l_s2=$(n 'if (stage == 2)')
  l_rawdepth=$(n 'SampleSceneDepth(screenUV)')
  l_s3=$(n 'if (stage == 3)')
  l_world=$(n 'ComputeWorldSpacePosition(screenUV')
  l_s4=$(n 'if (stage == 4)')
  l_origin=$(n '_OriginWS.xyz')
  l_s5=$(n 'if (stage == 5)')
  l_axis=$(n '_AxisXWS.xyz')
  l_s6=$(n 'if (stage == 6)')
  l_dot=$(n 'float dotMask')
  ladder_ok=1
  for v in "$l_frag" "$l_s1" "$l_uv" "$l_s2" "$l_rawdepth" "$l_s3" "$l_world" "$l_s4" \
           "$l_origin" "$l_s5" "$l_axis" "$l_s6" "$l_dot"; do
    [ -n "$v" ] || ladder_ok=0
  done
  if [ "$ladder_ok" -eq 1 ]; then
    prev="$l_frag"
    for v in "$l_s1" "$l_uv" "$l_s2" "$l_rawdepth" "$l_s3" "$l_world" "$l_s4" \
             "$l_origin" "$l_s5" "$l_axis" "$l_s6" "$l_dot"; do
      [ "$prev" -lt "$v" ] || ladder_ok=0
      prev="$v"
    done
  fi
  if [ "$ladder_ok" -eq 1 ]; then
    ok "each diagnostic rung returns above the work the next rung needs"
  else
    fail "each diagnostic rung returns above the work the next rung needs (frag=$l_frag s1=$l_s1 uv=$l_uv s2=$l_s2 rawDepth=$l_rawdepth s3=$l_s3 world=$l_world s4=$l_s4 origin=$l_origin s5=$l_s5 axis=$l_axis s6=$l_s6 dot=$l_dot)"
  fi
else
  fail "each diagnostic rung returns above the work the next rung needs"
fi

# 26b. AND NO RUNG SITS BELOW AN INVISIBLE EARLY-OUT. This is worth more than the ordering
#      itself. The first version of the ladder returned transparent black for sky ABOVE rung 2
#      and for out-of-range ABOVE rung 3, so ONE unbound depth texture would have blacked out
#      three rungs at once - and three rungs failing for one cause is not a bisect, it is the
#      same false finding printed three times. Above the last rung every condition that would
#      have returned nothing has to return a NAMED COLOUR instead; the invisible returns belong
#      to stage 0, which is the effect and must add nothing where there is no dot.
if [ -f "$SHDR" ]; then
  lastrung=$(printf '%s\n' "$scode" | { grep -n 'if (stage == 6)' || true; } | head -1 | cut -d: -f1)
  early=$(printf '%s\n' "$scode" | { grep -n 'return half4(0, 0, 0, 0);' || true; } | cut -d: -f1)
  above=""
  if [ -n "$lastrung" ]; then
    for l in $early; do
      # The last rung's own body is BELOW its `if` line, so its transparent return is not one
      # of these - what this catches is a return that would swallow a rung above it.
      [ "$l" -lt "$lastrung" ] && above="$above $l"
    done
  fi
  if [ -n "$lastrung" ] && [ -z "$above" ]; then
    ok "no diagnostic rung sits below an invisible early-out"
  else
    fail "no diagnostic rung sits below an invisible early-out (lastRung=$lastrung invisible returns above it:$above)"
  fi
else
  fail "no diagnostic rung sits below an invisible early-out"
fi

# 26c. AND THE RAW-DEPTH RUNG CLASSIFIES NOTHING. "Sky" is an interpretation of the depth
#      value, and an interpretation cannot be trusted to report on the number it interprets: the
#      first ladder answered "is there depth here" with the sky test's own verdict, so an unbound
#      texture and a correct one full of sky were the same picture. The rung has to sit ABOVE the
#      line that computes isSky, and the shader has to reach it without ever asking.
if [ -f "$SHDR" ]; then
  l_raw=$(printf '%s\n' "$scode" | { grep -n 'if (stage == 3)' || true; } | head -1 | cut -d: -f1)
  l_sky=$(printf '%s\n' "$scode" | { grep -n 'bool isSky' || true; } | head -1 | cut -d: -f1)
  if [ -n "$l_raw" ] && [ -n "$l_sky" ] && [ "$l_raw" -lt "$l_sky" ]; then
    ok "the raw-depth rung reports the value rather than the sky test's opinion of it"
  else
    fail "the raw-depth rung reports the value rather than the sky test's opinion of it (rung=$l_raw isSky=$l_sky)"
  fi
else
  fail "the raw-depth rung reports the value rather than the sky test's opinion of it"
fi

# 26d. And the effect DECLARES that it needs a depth texture, on the cameras that render to a
#      display. The pipeline asset asks for one globally, but a camera can override that, and the
#      player's camera is created at RUNTIME - so the one link in the chain that no file in this
#      repository can read is exactly the one upstream of a shader that reconstructs every dot
#      from depth. A requirement that is only true by default is not declared.
if [ -f "$PROJ" ]; then
  req=$(printf '%s\n' "$pcode" | sed -n '/private void RequestSceneDepth/,/^        }$/p')
  if printf '%s' "$req" | grep -qE 'depthTextureMode \|= DepthTextureMode\.Depth' \
     && printf '%s' "$req" | grep -qE 'targetTexture != null' \
     && printf '%s' "$pcode" | sed -n '/public void SetRunning/,/^        }$/p' \
          | grep -qE 'RequestSceneDepth\(\)'; then
    ok "the projection declares its need for a scene depth texture, skipping buffer cameras"
  else
    fail "the projection declares its need for a scene depth texture, skipping buffer cameras"
  fi
else
  fail "the projection declares its need for a scene depth texture, skipping buffer cameras"
fi

# 26e. And it REPORTS whether one exists, from outside the shader. Rung 3 shows flat blue for
#      two different causes - never produced, or produced and not given to this pass - and no
#      amount of reading the shader separates them. Shader.GetGlobalTexture answers the first
#      half in one line. It must not lean on Camera.main alone: this scene runs a portal camera
#      and a mirror camera, and the tagged one is not necessarily the one drawing the view.
if [ -f "$PROJ" ]; then
  rep=$(printf '%s\n' "$pcode" | sed -n '/private void ReportDepth/,/^        }$/p')
  if printf '%s' "$rep" | grep -qE 'GetGlobalTexture\("_CameraDepthTexture"\)' \
     && printf '%s' "$rep" | grep -qE 'Camera\.allCameras' \
     && printf '%s' "$rep" | grep -qE 'depthTextureAvailable='; then
    ok "switch-on reports whether a scene depth texture exists, across every display camera"
  else
    fail "switch-on reports whether a scene depth texture exists, across every display camera"
  fi
else
  fail "switch-on reports whether a scene depth texture exists, across every display camera"
fi

# 27. And rung 1 is the FIRST thing the fragment shader does. It answers "does this pass
#     rasterise at all", so anything above it can take the answer away and make a live pass
#     read as a dead one. Nothing but the stage read itself may come first.
if [ -f "$SHDR" ]; then
  # From the opening brace of frag to the magenta return, comments already stripped.
  pre=$(printf '%s\n' "$scode" | sed -n '/half4 frag(Varyings input)/,/if (stage == 1)/p' \
        | grep -vE 'half4 frag|^[[:space:]]*\{[[:space:]]*$|if \(stage == 1\)' \
        | grep -vE '^[[:space:]]*$' \
        | grep -vE 'int stage = \(int\)round\(_DebugMode\);' || true)
  if [ -z "$pre" ]; then
    ok "the magenta rung is the first statement in the fragment shader"
  else
    fail "the magenta rung is the first statement in the fragment shader (before it: $(printf '%s' "$pre" | tr '\n' ';'))"
  fi
else
  fail "the magenta rung is the first statement in the fragment shader"
fi

# 28. THE C# AND THE SHADER NAME THE SAME PROPERTIES, in both directions. This is the check that
#     would have caught the whole class rather than one instance of it: a SetFloat into a
#     uniform the shader no longer declares writes nowhere and says nothing, and a uniform
#     nothing pushes sits at whatever the authored material happens to hold. Both are silent,
#     both survive review, and both look on screen exactly like "the effect does not work" -
#     which is how a slider that moved nothing produced six false findings.
if [ -f "$SHDR" ] && [ -f "$PROJ" ]; then
  # What C# actually PUSHES - not what it has an id for. Reading the PropertyToID lines alone
  # proves the id exists, which is a weaker claim than this check's name: deleting the
  # SetFloat while leaving the id behind kept it green on the first tooth test. So each id is
  # mapped to its property name and then required to appear in a real _block.Set... call.
  pushed=$(printf '%s\n' "$pcode" \
           | sed -n 's/.*int \([A-Za-z0-9_]*\) = Shader\.PropertyToID("\([A-Za-z_][A-Za-z0-9_]*\)").*/\1 \2/p' \
           | while read -r idvar propname; do
               if printf '%s\n' "$pcode" | grep -qE "_block\.Set[A-Za-z]+\([[:space:]]*$idvar[[:space:]]*,"; then
                 printf '%s\n' "$propname"
               fi
             done | sort -u)
  # What the shader declares: the Properties block plus the uniforms outside the CBUFFER.
  declared=$( { printf '%s\n' "$scode" \
                  | sed -n '/^    Properties$/,/^    }$/p' \
                  | sed -n 's/^[[:space:]]*\(\[[A-Za-z]*\][[:space:]]*\)\?\(_[A-Za-z0-9_]*\)[[:space:]]*(.*/\2/p'
                printf '%s\n' "$scode" \
                  | sed -n 's/^[[:space:]]*float4[[:space:]]*\(_[A-Za-z0-9_]*\);.*/\1/p'; } | sort -u)
  onlypush=$(comm -23 <(printf '%s\n' "$pushed") <(printf '%s\n' "$declared") | tr '\n' ' ')
  onlydecl=$(comm -13 <(printf '%s\n' "$pushed") <(printf '%s\n' "$declared") | tr '\n' ' ')
  if [ -z "$(printf '%s' "$onlypush$onlydecl" | tr -d ' ')" ]; then
    ok "every property the C# pushes is declared by the shader, and every one it declares is pushed"
  else
    fail "every property the C# pushes is declared by the shader, and every one it declares is pushed (pushed-only:$onlypush declared-only:$onlydecl)"
  fi
else
  fail "every property the C# pushes is declared by the shader, and every one it declares is pushed"
fi

# 29. And the two agree on HOW MANY rungs there are. An Inspector that offers a stage the shader
#     does not implement falls through to the full effect and reads as "that stage is broken" -
#     which is a false finding about working code, and the expensive direction (mistake 26).
if [ -f "$SHDR" ] && [ -f "$PROJ" ]; then
  sh_top=$(printf '%s' "$scode" | sed -n 's/.*_DebugMode[^R]*Range(0,[[:space:]]*\([0-9]*\)).*/\1/p' | head -1)
  cs_top=$(printf '%s' "$pcode" | sed -n 's/.*Range(0,[[:space:]]*\([0-9]*\))\][[:space:]]*private int debugStage.*/\1/p' | head -1)
  impl=$(printf '%s\n' "$scode" | { grep -cE 'if \(stage == [0-9]+\)' || true; })
  if [ -n "$sh_top" ] && [ "$sh_top" = "$cs_top" ] && [ "$impl" -eq "$sh_top" ]; then
    ok "the shader and the Inspector offer the same $sh_top diagnostic rungs, and all of them exist"
  else
    fail "the shader and the Inspector offer the same diagnostic rungs (shader=$sh_top cs=$cs_top implemented=$impl)"
  fi
else
  fail "the shader and the Inspector offer the same diagnostic rungs"
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
if [ -f "$PROJ" ] && printf '%s' "$pcode" | grep -qE 'EffectVolume\.Mark\(host\.gameObject\)'; then
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

# 30. Und die Stufendiagnose steht auf 0 - in C# UND im Shader. Eine davon eingeschaltet
#     ausgeliefert malt den Raum magenta oder flach gruen. Ein Werkzeug, das laeuft, waehrend
#     jemand spielt, diagnostiziert nicht mehr, sondern erzeugt (Fehler 23).
dbg_cs=0
dbg_sh=0
if [ -f "$PROJ" ] && printf '%s' "$pcode" | grep -qE 'Range\(0, 6\)\] private int debugStage = 0'; then
  dbg_cs=1
fi
if [ -f "$SHDR" ] && printf '%s' "$scode" | grep -qE '_DebugMode \("Debug Stage \(0 = off\)", Range\(0, 6\)\) = 0'; then
  dbg_sh=1
fi
if [ "$dbg_cs" -eq 1 ] && [ "$dbg_sh" -eq 1 ]; then
  ok "die Diagnosestufe steht in C# UND im Shader auf 0"
else
  fail "die Diagnosestufe steht in C# UND im Shader auf 0 (cs=$dbg_cs shader=$dbg_sh)"
fi


# 31. Ein Regler, der erreicht, was er benennt. Die Werte wurden EINMAL gepusht, beim
#     Einschalten, und nie wieder: eine Aenderung im Inspector waehrend des Spiels landete
#     nirgends. Das ist nicht klein - es ist der Grund, warum eine sechsstufige Diagnose mit
#     sechs identischen Stufen zurueckkam. Alle sechs waren Stufe 0. Ein Regler, der das nicht
#     bewegen kann, was er benennt, ist schlimmer als keiner, weil er BEWEISE erzeugt.
if [ -f "$PROJ" ] && printf '%s' "$pcode" | grep -qE '_propertiesDirty = true' \
   && printf '%s' "$pcode" | sed -n '/private void LateUpdate/,/^        }$/p' \
        | grep -qE 'PushProperties\(\)'; then
  ok "eine Aenderung im Inspector erreicht den lebenden Renderer"
else
  fail "eine Aenderung im Inspector erreicht den lebenden Renderer"
fi

# 32. Und beim Einschalten wird gemeldet, ob die Kamera IM Volumen steht. Davon haengt ab,
#     welche Seiten des Kastens gezeichnet werden, und es ist die eine Tatsache, die man einem
#     Screenshot von etwas Unsichtbarem nicht ansieht. Lieber eine Zeile als ein Streit.
if [ -f "$PROJ" ] && printf '%s' "$pcode" | grep -qE 'volumeContainsCamera=' \
   && printf '%s' "$pcode" | grep -qE 'cameraMaskIncludesVolume='; then
  ok "beim Einschalten wird gemeldet, ob die Kamera im Volumen steht"
else
  fail "beim Einschalten wird gemeldet, ob die Kamera im Volumen steht"
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
