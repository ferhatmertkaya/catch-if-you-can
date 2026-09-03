# Task router

Status: **normative for how work is assigned.** Read with
`Docs/AGENT_OWNERSHIP.md` (who owns what) and `Docs/AGENT_ROSTER.json` (the
same list, machine-readable).

This document answers one question: *a task just arrived — who does it, who
reviews it, and what has to pass before it is committed?*

---

## 1. Two axes, deliberately

| | Unit | Count | Where |
|---|---|---|---|
| **Teams** | a folder | 21 | `AGENT_OWNERSHIP.md` §2 |
| **Specialists** | a concern | 40 | `AGENT_ROSTER.json` |

They are not the same thing and neither replaces the other. Three specialists —
Equipment Architecture, Investigation Device, Equipment Presentation — all work
inside the Equipment **team's** folder. The team rule says *who may write the
file*. The specialist rule says *whose judgement the change needs*.

Every role's `team` field names the folder owner who must be involved when that
specialist writes.

---

## 2. Classifying a task

The Main Agent fills this in **before** any file is opened:

```
PRIMARY OWNER      one specialist. If you cannot name one, the task is two tasks.
SECONDARY OWNERS   specialists whose domain the change reaches into.
REVIEWERS          from the primary's required_reviewers, plus QA.
HOTSPOTS           from §4 of AGENT_OWNERSHIP.md. Name the invariant, not just the file.
DEV LAB            the primary's preferred_dev_lab, or "none applies".
VALIDATORS         the union of every involved role's validators.
RUNTIME TESTS      what a human must do in Unity. Say so even when nobody can.
PLATFORM IMPACT    none | mobile | desktop | console | all
NETWORK IMPACT     none | offline-only | contract change | wire format change
SAVE IMPACT        none | new field | format change (needs a migration)
```

**A wire-format change or a save-format change is never a one-specialist task.**

---

## 3. Worked examples

### "Make the EMF look and sound production-ready."
```
PRIMARY     10 Investigation Device
SECONDARY   12 Equipment Presentation, 26 Sound Design
REVIEWERS    9 Equipment Architecture, 25 Audio Architecture, 4 QA
HOTSPOTS    EquipmentPresentation.cs — the grip comes FROM PlayerBodyMotion,
            it is not recomputed. Do not add a second pose offset.
DEV LAB     DEV_EquipmentLab
VALIDATORS  check_equipment_catalog.sh, check_multiplayer_architecture.sh
NETWORK     none — a reading is local presentation until it becomes evidence
SAVE        none
```

### "Ghost hunts players through doors."
```
PRIMARY     14 Hunt System
SECONDARY   13 Ghost AI, 19 Interaction
REVIEWERS   34 Multiplayer Architecture, 4 QA
HOTSPOTS    GhostController.cs, GhostStateMachine.cs — every decision path is
            gated on SessionAuthority.CanSimulateGhost and must stay so.
DEV LAB     DEV_GhostLab
VALIDATORS  check_multiplayer_architecture.sh
NETWORK     contract change — opening a door is a world-state change, so it is
            a request to the authority, not a command
SAVE        none
```

### "Improve mirror realism."
```
PRIMARY     23 Mirror / Reflection
SECONDARY   22 Shader / Material, 3 Performance
REVIEWERS    4 QA
HOTSPOTS    MirrorCorner.cs, PlanarMirror.shader — the plane is captured once
            and never rotates toward the player. No per-frame allocation.
DEV LAB     DEV_LightingLab
VALIDATORS  static typecheck against the recorded baseline
NETWORK     none      SAVE none      PLATFORM all (reflection cost is tiered)
```

### "Build character selection."
```
PRIMARY      6 Character / Rig
SECONDARY   30 Menu / Lobby UX, 33 Save / Settings
REVIEWERS    7 Animation, 34 Multiplayer Architecture, 4 QA
HOTSPOTS    CharacterRigProfile.cs; catalog ORDER is the compact wire index.
DEV LAB     DEV_CharacterLab
VALIDATORS  check_determinism.sh, check_multiplayer_architecture.sh
NETWORK     contract change — the index travels; the host validates it
SAVE        new field — the chosen character id
```

### "Implement real multiplayer once NGO is available."
```
PRIMARY     35 Netcode / Transport            ← currently BLOCKED
SECONDARY   36 Online Services (BLOCKED), 34 Multiplayer Architecture
REVIEWERS    1 Main, 4 QA
FIRST STEP  verify the installed package and the real API signatures.
            If they cannot be verified, STOP THIS DOMAIN and say so.
            Do not write fake netcode. Other domains continue.
```

---

## 4. Cross-agent review matrix

| Change touches | Primary | Must also review |
|---|---|---|
| Equipment gameplay + hand pose | 9 or 10 | 8 Hands / IK |
| Ghost AI + replicated ghost state | 13 | 34 Multiplayer |
| Door / interactable + networking | 19 | 34 Multiplayer |
| Ghost event + sound | 15 | 26 Sound Design |
| Mirror + shader | 23 | 22 Shader / Material |
| Save + any online sync | 33 | 36 Online Services |
| Procedural generation + multiplayer | 20 | 34 Multiplayer |
| Character replication | 6 | 7 Animation, 34 Multiplayer |
| Anything that changes a mixer bus | 26 or 27 | 25 Audio Architecture |
| Anything that adds a service | any | 2 Core Architecture |
| Anything that changes a guard | any | 4 QA |

The **Main Agent** resolves disagreements. There is no vote.

---

## 5. Specialist handoff contract

A specialist returns **all** of these. `"Done."` is not a handoff and is
rejected.

```
TASK
OWNER
SECONDARY AGENTS
FILES READ
FILES CHANGED
FILES ADDED
FILES DELETED
ARCHITECTURE ASSUMPTIONS
IMPLEMENTATION
PRESERVED INVARIANTS      ← what you did NOT break, stated explicitly
CROSS-DOMAIN IMPACT
RISKS
STATIC VALIDATION         ← diffed against the recorded baseline, not just a count
UNITY VALIDATION          ← "NOT TESTED" when Unity did not run
NOT TESTED
BLOCKERS
RECOMMENDED NEXT STEP
```

**PRESERVED INVARIANTS is the field that catches the mistakes this repository
actually makes.** Naming what you kept forces you to have looked.

---

## 6. Main Agent integration contract

```
TASK
AGENTS USED
WHY THESE AGENTS
IMPLEMENTATION ORDER
CONFLICTS FOUND
CONFLICTS RESOLVED
FILES CHANGED
HOTSPOTS TOUCHED
ARCHITECTURE STATUS
STATIC VALIDATION
PLAY MODE VALIDATION
REGRESSION STATUS
KNOWN RISKS
COMMIT
```

---

## 7. Context rule

A specialist does **not** read the repository by default. The Main Agent hands
over: the task, the relevant architecture docs, the owned files, directly
related files, the invariants to preserve, and the validators to run. The
specialist widens scope only when it must, and says so in ARCHITECTURE
ASSUMPTIONS.

This is not politeness. A specialist that reads everything starts *changing*
everything, and that is how this project got two flashlights.

---

## 8. No parallel writes to one hotspot

Parallel **reads** are fine. Parallel **writes to unrelated domains** are fine.
Two specialists writing the same protected hotspot at once is not.

When work overlaps:

1. Main Agent fixes the order.
2. The first specialist changes the shared contract.
3. Main Agent integrates.
4. The second specialist starts from the integrated state.

---

## 9. Blocked domains

A blocked domain stops; it does not improvise, and it does not stop anybody
else. Roles 35 and 36 are **BLOCKED** today: every Unity package host and
`docs.unity3d.com` return a 403 policy denial in this environment, and no Unity
Editor is available, so no package version or API signature can be verified.

The rule for those two roles is absolute — before writing any
`NetworkBehaviour`, `NetworkObject`, `NetworkVariable`, `ServerRpc`,
`ClientRpc`, `Unity.Netcode`, `UnityTransport`, Relay, Lobby or Authentication
call, **verify the installed package**. If it cannot be verified: stop that
subtask, report it, and let every other domain carry on.

The seams they will plug into already exist and are transport-neutral:
`Session/SessionLauncher.cs` (`IOnlineSessionProvider`) and
`Session/ConnectionDiagnostics.cs` (`IConnectionProbe`).
