#!/usr/bin/env sh
#
# Multi-agent development ownership guard.
#
# V6 put the development operating model in the repository: forty specialist roles, a
# machine-readable roster, a task router and an expanded hotspot table. None of that
# produces a compiler error when it rots. It produces a roster that says one thing and a
# document that says another, and a reader who trusts whichever they opened first.
#
# This checks the high-value invariants and deliberately not the prose. A guard that fails
# because somebody reworded a paragraph is a guard people delete.
#
# Needs a shell and python3 (already required by nothing else here, so the JSON checks
# degrade to a skip rather than a failure if it is absent).

set -eu

REPO="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO"

ROSTER="Docs/AGENT_ROSTER.json"
OWNERSHIP="Docs/AGENT_OWNERSHIP.md"
ROUTER="Docs/AGENT_TASK_ROUTER.md"
SCRIPTS="Assets/CatchIfYouCan/Scripts"

passed=0
failed=0

ok()   { passed=$((passed + 1)); printf '  ok    %s\n' "$1"; }
fail() { failed=$((failed + 1)); printf '  FAIL  %s\n' "$1"; }

printf '== agent architecture guard ==\n'

# ------------------------------------------------------------------ the documents exist

for f in "$ROSTER" "$OWNERSHIP" "$ROUTER"; do
  if [ -f "$f" ]; then
    ok "$f exists"
  else
    fail "$f is missing"
  fi
done

# ------------------------------------------------------------------ the roster is sound

if [ ! -f "$ROSTER" ]; then
  fail "cannot check the roster; it is missing"
elif ! command -v python3 >/dev/null 2>&1; then
  printf '  skip  python3 unavailable; roster contents not checked\n'
else
  python3 - "$ROSTER" <<'PY' || fail "the roster failed its structural checks"
import json, sys

roster = json.load(open(sys.argv[1]))
roles = roster["roles"]
ids = [r["id"] for r in roles]
bad = []

def check(label, condition):
    print(("  ok    " if condition else "  FAIL  ") + label)
    if not condition:
        bad.append(label)

check("the roster declares 40 roles", roster.get("roleCount") == 40)
check("the roster contains 40 roles", len(roles) == 40)
check("role ids are 1..40, unique and complete", sorted(ids) == list(range(1, 41)))

required = ["id", "name", "group", "status", "team", "mission", "owns", "may_read",
            "protected_files", "forbidden_changes", "required_reviewers",
            "preferred_dev_lab", "validators", "escalation_rules"]
missing = [(r.get("id"), k) for r in roles for k in required if k not in r]
check("every role carries every required field", not missing)

check("every required_reviewers entry names a real role",
      all(x in ids for r in roles for x in r["required_reviewers"]))

allowed = {"ACTIVE", "READY", "DORMANT", "BLOCKED"}
check("every status is one of the four legal values",
      all(r["status"] in allowed for r in roles))

by_id = {r["id"]: r for r in roles}

# The two roles the whole model rests on.
check("role 1 is the Main / Lead Architect",
      "Main" in by_id.get(1, {}).get("name", ""))
check("role 4 is QA / Validation",
      "QA" in by_id.get(4, {}).get("name", ""))
check("role 34 is Multiplayer Architecture",
      "Multiplayer" in by_id.get(34, {}).get("name", ""))

# Multiplayer Architecture must still own the capacity contract.
check("Multiplayer Architecture still owns MultiplayerProtocol",
      any("MultiplayerProtocol" in o for o in by_id.get(34, {}).get("owns", [])))
check("MultiplayerProtocol is a protected file of its owner",
      any("MultiplayerProtocol" in p for p in by_id.get(34, {}).get("protected_files", [])))

# Netcode and Online Services must not quietly become unblocked while no package exists.
for rid in (35, 36):
    role = by_id.get(rid, {})
    if role.get("status") == "BLOCKED":
        print(f"  ok    role {rid} ({role.get('name')}) is honestly marked BLOCKED")
    else:
        print(f"  ok    role {rid} ({role.get('name')}) is {role.get('status')} "
              f"- verify a real package is installed")

# Every role that names a lab must name one that exists as an installer.
labs = {"DEV_EquipmentLab": "EquipmentLabInstaller", "DEV_CharacterLab": "CharacterLabInstaller",
        "DEV_InteractionLab": "InteractionLabInstaller", "DEV_GhostLab": "GhostLabInstaller",
        "DEV_AudioLab": "AudioLabInstaller", "DEV_LightingLab": "LightingLabInstaller",
        "DEV_EnvironmentLab": "EnvironmentLabInstaller", "DEV_UIInputLab": "UIInputLabInstaller",
        "DEV_NetworkLab": "NetworkLabInstaller"}
named = {r["preferred_dev_lab"] for r in roles if r["preferred_dev_lab"]}
check("every lab a role prefers is a lab that exists", named <= set(labs))

sys.exit(1 if bad else 0)
PY
fi

# ------------------------------------------------- the roster and the document agree

if [ -f "$ROSTER" ] && [ -f "$OWNERSHIP" ]; then
  # Drift check. The prose is free to be reworded; the ROLE NAMES are not, because they
  # are how a reader crosses from one document to the other.
  drift=0
  if command -v python3 >/dev/null 2>&1; then
    missing_names=$(python3 - "$ROSTER" "$OWNERSHIP" <<'PY'
import json, sys
roles = json.load(open(sys.argv[1]))["roles"]
doc = open(sys.argv[2]).read()
# The ownership document lists roles as "<id> <Name>" in its group table.
missing = [f'{r["id"]} {r["name"]}' for r in roles
           if f'{r["id"]} {r["name"]}' not in doc]
print("\n".join(missing))
PY
)
    if [ -n "$missing_names" ]; then
      fail "roles in the roster that AGENT_OWNERSHIP.md does not list"
      printf '%s\n' "$missing_names" | sed 's/^/        /'
      drift=1
    fi
  fi
  [ "$drift" = "0" ] && ok "the roster and the ownership document name the same roles"

  grep -q 'AGENT_ROSTER.json' "$OWNERSHIP" \
    && ok "the ownership document points at the machine-readable roster" \
    || fail "AGENT_OWNERSHIP.md does not reference AGENT_ROSTER.json"

  grep -q 'AGENT_TASK_ROUTER.md' "$OWNERSHIP" \
    && ok "the ownership document points at the task router" \
    || fail "AGENT_OWNERSHIP.md does not reference the task router"
fi

# ------------------------------------------------------------------ policy still stated

if [ -f "$OWNERSHIP" ]; then
  grep -q 'A protected hotspot is not' "$OWNERSHIP" \
    && ok "the protected-hotspot policy is stated" \
    || fail "the protected-hotspot policy has been removed from AGENT_OWNERSHIP.md"

  # The hotspot table is the point of section 4. An empty one is worse than none.
  rows=$(awk '/^## 4\./,/^## 5\./' "$OWNERSHIP" | grep -c '^| `' || true)
  if [ "$rows" -ge 15 ]; then
    ok "the hotspot table still lists $rows entries"
  else
    fail "the hotspot table has shrunk to $rows entries; V6 recorded 19"
  fi
fi

if [ -f "$ROUTER" ]; then
  grep -q 'PRESERVED INVARIANTS' "$ROUTER" \
    && ok "the specialist handoff contract still demands preserved invariants" \
    || fail "the handoff contract no longer demands PRESERVED INVARIANTS"

  grep -q 'Do not write fake netcode' "$ROUTER" \
    && ok "the blocked-domain rule is still stated" \
    || fail "the router no longer forbids writing fake netcode"
fi

# --------------------------------------------- agents are a process, not a runtime thing

# The one way this whole model could damage the game: somebody builds it into the game.
runtime=$(grep -rlE 'class (AgentManager|AgentService|AgentRegistry|AgentBehaviour)\b' \
          "$SCRIPTS" --include='*.cs' 2>/dev/null || true)
if [ -n "$runtime" ]; then
  fail "an agent runtime object exists; agents are development roles, not game objects"
  printf '%s\n' "$runtime" | sed 's/^/        /'
else
  ok "no agent runtime object exists in the shipped game"
fi

# Nothing under Assets/ may depend on the roster file.
if grep -rq 'AGENT_ROSTER' "$SCRIPTS" --include='*.cs' 2>/dev/null; then
  fail "runtime code reads the agent roster; it is a development document"
else
  ok "no runtime code reads the agent roster"
fi

# ------------------------------------------------------- the capacity contract holds

PROTOCOL="$SCRIPTS/Procedural/Deterministic/MultiplayerProtocol.cs"
if [ -f "$PROTOCOL" ]; then
  grep -qE 'MaxPlayers\s*=\s*8\s*;' "$PROTOCOL" \
    && ok "MaxPlayers is still 8 and still centralised" \
    || fail "MultiplayerProtocol.MaxPlayers is no longer 8 in its one source"
else
  fail "MultiplayerProtocol.cs is missing"
fi

printf '\npassed: %s   failed: %s\n\n' "$passed" "$failed"

if [ "$failed" -gt 0 ]; then
  printf 'AGENT ARCHITECTURE GUARD FAILED\n'
  exit 1
fi

printf 'AGENT ARCHITECTURE GUARD PASSED\n'
