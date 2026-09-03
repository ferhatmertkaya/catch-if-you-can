#!/usr/bin/env sh
#
# Multiplayer and ghost-authority architecture guard.
#
# V4 drew a set of boundaries that are invisible at compile time and expensive to
# rediscover: the deterministic assembly stays engine-free and transport-free, gameplay
# never learns what Relay is, a remote player never reads this machine's input, and each
# evidence type has exactly one device that can prove it.
#
# None of those produce an error when they are broken. They produce a build that works on
# one machine and disagrees with itself on two. This checks the text, so it needs nothing
# but a shell.

set -eu

REPO="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO"

SCRIPTS="Assets/CatchIfYouCan/Scripts"
DETERMINISTIC="$SCRIPTS/Procedural/Deterministic"
SESSION="$SCRIPTS/Session"

passed=0
failed=0

ok()   { passed=$((passed + 1)); printf '  ok    %s\n' "$1"; }
fail() { failed=$((failed + 1)); printf '  FAIL  %s\n' "$1"; }

printf '== multiplayer architecture guard ==\n'

# --------------------------------------------------- the deterministic assembly

# The whole determinism argument rests on this assembly producing the same house from the
# same seed everywhere. An engine reference or a netcode reference in it means it is no
# longer testable without Unity and no longer obviously portable.
if grep -rnE '^\s*using (UnityEngine|Unity\.Netcode|Unity\.Services|Unity\.Networking)' \
     "$DETERMINISTIC" --include='*.cs' >/dev/null 2>&1; then
  fail "the deterministic assembly imports UnityEngine, Netcode or Unity Services"
  grep -rnE '^\s*using (UnityEngine|Unity\.Netcode|Unity\.Services|Unity\.Networking)' \
       "$DETERMINISTIC" --include='*.cs' | sed 's/^/        /'
else
  ok "the deterministic assembly imports no engine or networking namespace"
fi

if grep -rqE 'NetworkBehaviour|NetworkVariable|ServerRpc|ClientRpc|NetworkObject' \
     "$DETERMINISTIC" --include='*.cs' 2>/dev/null; then
  fail "netcode types appear inside the deterministic assembly"
else
  ok "no netcode types inside the deterministic assembly"
fi

ASMDEF="$DETERMINISTIC/CatchIfYouCan.Procedural.Deterministic.asmdef"
if [ -f "$ASMDEF" ]; then
  grep -q '"noEngineReferences": true' "$ASMDEF" \
    && ok "the deterministic asmdef still declares noEngineReferences" \
    || fail "the deterministic asmdef no longer declares noEngineReferences"

  grep -q '"references": \[\]' "$ASMDEF" \
    && ok "the deterministic asmdef still references nothing" \
    || fail "the deterministic asmdef has gained references"
else
  fail "no deterministic asmdef at $ASMDEF"
fi

# ------------------------------------------------------- the session boundary

# Gameplay asks IMultiplayerSession whether it is the host. It does not allocate relays.
# A ghost that can reach a Relay API is a ghost that will, and then the transport cannot be
# replaced without touching the ghost.
leaks=$(grep -rlnE 'Unity\.Services\.(Relay|Lobby|Authentication)|RelayService|LobbyService|AuthenticationService' \
        "$SCRIPTS" --include='*.cs' 2>/dev/null | grep -v "^$SESSION/" | grep -v '/Networking/' || true)
if [ -n "$leaks" ]; then
  fail "Relay, Lobby or Authentication APIs are used outside the session and networking layers"
  printf '%s\n' "$leaks" | sed 's/^/        /'
else
  ok "no gameplay file reaches a Relay, Lobby or Authentication API"
fi

# ------------------------------------------------------ local versus remote

# A remote player is somebody else's. Reading this machine's input for it would drive four
# characters from one pair of thumbs.
for f in "$SCRIPTS/Player/RemotePlayerDriver.cs" "$SCRIPTS/Player/PlayerPresentationState.cs"; do
  [ -f "$f" ] || continue
  grep -q 'MobileInputController' "$f" \
    && fail "$(basename "$f") reads this machine's input for a remote player" || :
done
ok "remote player code does not read local input"

# ------------------------------------------------------------ ghost authority

# Every one of these decides something and then changes what other players see. A client
# running any of them is a second ghost wearing the same transform.
for pair in \
  "GhostController.cs:_stateMachine.Tick" \
  "GhostPerception.cs:UpdateLineOfSight" \
  "GhostEvidenceManager.cs:SpawnEvidenceForDefinition" \
  "GhostInteractionBrain.cs:TryRandomInteraction"; do
  file="${pair%%:*}"
  path="$SCRIPTS/Ghost/$file"
  [ -f "$path" ] || { fail "no $file to check"; continue; }
  grep -q 'SessionAuthority.CanSimulateGhost' "$path" \
    || fail "$file does not gate on SessionAuthority.CanSimulateGhost"
done
ok "ghost decision paths are gated on simulation authority"

# One authority, not two. V3's EquipmentAuthority must forward rather than hold its own.
AUTH="$SCRIPTS/Equipment/EquipmentAuthority.cs"
if [ -f "$AUTH" ]; then
  grep -q 'IAuthorityProvider _provider' "$AUTH" \
    && fail "EquipmentAuthority holds a second provider instead of forwarding" \
    || ok "EquipmentAuthority forwards to the one authority"
fi

# --------------------------------------------------------- evidence authority

# Each evidence type has exactly one declared observing device, and that device must
# actually observe it. A table that says something no code does is worse than no table.
EVA="$SCRIPTS/Evidence/EvidenceAuthority.cs"
TYPES="$SCRIPTS/Evidence/EvidenceType.cs"
if [ -f "$EVA" ] && [ -f "$TYPES" ]; then
  missing=0
  for type in $(grep -oE '^\s{8}[A-Za-z]+,?$' "$TYPES" | tr -d ' ,'); do
    [ -n "$type" ] || continue
    grep -q "EvidenceType.$type," "$EVA" \
      || { fail "$type has no entry in EvidenceAuthority"; missing=1; }

    # And something, somewhere, must actually observe it.
    grep -rq "Observe(Evidence\?\.\?EvidenceType\.$type" "$SCRIPTS" --include='*.cs' 2>/dev/null \
      || grep -rq "EvidenceType\.$type," "$SCRIPTS/Equipment" --include='*.cs' 2>/dev/null \
      || { fail "$type is declared supported and nothing observes it"; missing=1; }
  done
  [ "$missing" -eq 0 ] && ok "every evidence type is declared and observed"
else
  fail "EvidenceAuthority or EvidenceType is missing"
fi

# ------------------------------------------------------------ the ghost roster

IDS="$SCRIPTS/Ghost/GhostIds.cs"
FACTORY="$SCRIPTS/Ghost/GhostDefinitionFactory.cs"
if [ -f "$IDS" ] && [ -f "$FACTORY" ]; then
  bad=0
  for const in $(grep -oE 'public const string [A-Za-z]+' "$IDS" | awk '{print $4}'); do
    grep -q "GhostIds.$const," "$FACTORY" \
      || { fail "GhostIds.$const is declared and no ghost uses it"; bad=1; }
  done
  [ "$bad" -eq 0 ] && ok "every declared ghost id is used by a definition"

  grep -qE '"the_[a-z_]+"' "$FACTORY" \
    && fail "the ghost factory still has literal ids" \
    || ok "the ghost factory names ghosts through GhostIds"
else
  fail "GhostIds or GhostDefinitionFactory is missing"
fi

# ------------------------------------------------------- self-referencing getters
#
# GhostSpawnManager.Player's getter tested the property rather than the backing field, so
# the first read recursed until the stack ran out - a crash that cannot be caught, sitting
# in the ghost spawn path. Cheap to check for, and it was not found by reading.
if command -v python3 >/dev/null 2>&1; then
  recursive=$(python3 - <<'PY'
import io, os, re
hits = []
for dp, _, fs in os.walk("Assets/CatchIfYouCan/Scripts"):
    for f in fs:
        if not f.endswith(".cs"):
            continue
        path = os.path.join(dp, f)
        src = io.open(path, encoding="utf-8").read()
        for m in re.finditer(
                r'(?:public|private|protected|internal)[\w\s]*?\s(\w+)\s*\n?\s*\{\s*\n\s*get\s*\n?\s*\{(.*?)\n\s{8}\}',
                src, re.S):
            name, body = m.group(1), m.group(2)
            if re.search(r'(?<![\w.])' + re.escape(name) + r'(?![\w])', body):
                hits.append(f"{path}: property '{name}' reads itself")
print("\n".join(hits))
PY
)
  if [ -n "$recursive" ]; then
    fail "a property getter reads itself and will recurse until the stack ends"
    printf '%s\n' "$recursive" | sed 's/^/        /'
  else
    ok "no property getter reads itself"
  fi
fi

printf '\npassed: %s   failed: %s\n\n' "$passed" "$failed"

if [ "$failed" -gt 0 ]; then
  printf 'MULTIPLAYER ARCHITECTURE GUARD FAILED\n'
  exit 1
fi

printf 'MULTIPLAYER ARCHITECTURE GUARD PASSED\n'
