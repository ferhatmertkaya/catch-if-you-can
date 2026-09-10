# Ghost and evidence authority

Status: **normative.** Any change to what counts as evidence, or to who decides
it, is a change to this document first.

Companion documents: `Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md`,
`Docs/NETWORKING.md` §4 and §6.

---

## 1. Observation is not confirmation

Two different acts, with two different owners.

A device **observes**: "I measured this, this strongly, here." Any device may do
that at any time, and it costs nothing to be wrong.

The host **confirms**: it turns an observation into a fact in the journal that
completes objectives and decides a contract. Nothing else may.

This distinction is the whole architecture, and it exists because the project
kept losing it. Three separate paths used to announce evidence with nothing
found:

1. Equipment calling `EvidenceManager.RegisterEvidence` directly — a device that
   fired once had proved something.
2. `AddJournalEntry` registering whatever a caller claimed — which is how the EVP
   recorder proved EVP Response against ghosts that do not make one.
3. `GhostEvidenceManager` raising `EvidenceDetected` on a 45-second timer for all
   three of the ghost's types — so three objectives completed themselves three
   quarters of a minute into a mission, with no device, no room and no player.

All three are closed and all three are guarded.

---

## 2. The confirmation table

`Evidence.EvidenceAuthority` declares, for every `EvidenceType`, the single
equipment id whose `Observe` call is the legal way it enters the system.

| Evidence | Observer | What is actually measured |
|---|---|---|
| EMF Surge | `emf_detector` | a field left where the ghost has been |
| UV Traces | `uv_light` | prints and disturbed salt, under ultraviolet |
| Spectral Grid | `spectral_grid` | a body standing in the projected point field |
| EVP Response | `evp_recorder` | an answer on playback |
| Ghost Orb | `video_camera` | a mote visible only down a camera feed |
| Freezing Temperature | `thermometer` | air the ghost has taken the heat out of |
| Parabolic Anomaly | `parabolic_microphone` | a sound with a direction and no source |
| Electronic Distortion | `flashlight` | current pulled out of a torch near the entity |
| Physical Disturbance | `photo_camera` | a photograph of something the ghost moved |

**One id, not a list.** Two devices able to prove the same thing is two places
for the rule to drift, and the drift is invisible until somebody proves Ghost
Orbs with a thermometer. `EvidenceValidator` refuses a device that is not the
declared observer, however correct its measurement was.

---

## 3. What a confirmation has to survive

In order, in `EvidenceValidator.Decide`:

1. **Authority.** `SessionAuthority.CanConfirmEvidence`. Clients never assert
   evidence (`NETWORKING.md` §6); a client forwards an observation and the host
   decides.
2. **The declared observer.** §2 above.
3. **Not already found.**
4. **A ghost exists** to prove something about.
5. **The ghost's own profile.** `GhostDefinition.HasEvidence`. This is the check
   whose absence let a device prove something the entity does not do.
6. **Strength floor.** 0.15.
7. **Dwell.** Per type, from 0 s for a deliberate single act to 3 s for freezing
   temperature. Elapsed time only — it used to be elapsed time *plus* one
   `Time.deltaTime` per submission, so a per-frame submitter dwelled in half the
   seconds asked of it.

A device that fires is a device that has an opinion.

---

## 4. The journal records; it does not prove

`EvidenceManager.AddJournalEntry` does not touch evidence truth at all. It has no
device, and with a declared observer per type it therefore has no standing. The
device that measured the thing has already said so through `Observe`; the journal
writes down that it happened.

The guard fails if `EvidenceManager` so much as references `EvidenceValidator`.

---

## 5. Ghost simulation is host-only

Four paths decide something and then change what other players can see. Each
gates on `SessionAuthority.CanSimulateGhost`, and the guard fails if one stops:

| Path | What it decides |
|---|---|
| `GhostController.Update` | state machine, room awareness, footsteps that disturb salt |
| `GhostPerception.Update` | who is seen, who is hunted, who is killed |
| `GhostEvidenceManager.Update` | which manifestations exist and where |
| `GhostInteractionBrain.TryRandomInteraction` | doors, lights, thrown objects |

Manifestation *visibility* is deliberately outside the gate: presentation runs on
every peer, because a client still has to draw the ghost it can see.

`UnityEngine.Random` may stay in host-only ghost behaviour — that is
`DETERMINISM.md`'s deliberate boundary, and it is what keeps the deterministic
set small enough to verify. The gates are what make "host-only" true rather than
assumed.

---

## 6. The ghost sees every player

`Player.PlayerPresence` is the registry of who is in the house. `GhostPerception`
picks the nearest from it; `GhostSpawnManager` rejects a spawn point in front of
any of them.

Before V4 both asked `LocalPlayerService`, which holds exactly one player: the
one on this machine. Correct in single player, and silently wrong the moment
there is a second — the ghost would roam toward the host, hunt the host, and
treat three other people as furniture.

`LocalPlayerService` keeps its own job: which of these is mine, for the camera,
the listener, the HUD and the input. "Which is mine" and "who is here" are
different questions, and conflating them is what produced the bug.

---

## 7. The roster has to be solvable

`GhostCatalogValidator` checks three things that run perfectly and mean a player
gathers everything the game offers and still cannot answer the question it asked:

- a ghost exhibiting an evidence type nothing can observe;
- two ghosts with the same three types, who cannot be told apart;
- a ghost listing a type twice, which has two findings rather than three.

As of V4 the roster is 10 ghosts with 10 distinct triples.
`PhysicalDisturbance` is exhibited by **The Knocker** alone — it was exhibited by
nobody before V4, which meant it could never have been confirmed against any
entity in the game regardless of which device observed it.
