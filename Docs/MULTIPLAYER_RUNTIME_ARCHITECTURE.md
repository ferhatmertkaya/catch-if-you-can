# Multiplayer runtime architecture

Status: **normative for the boundaries**, descriptive for what is not built yet.

Written at V4. Companion documents: `Docs/NETWORKING.md` (the target
architecture and the handshake), `Docs/GHOST_EVIDENCE_AUTHORITY.md` (who decides
what evidence is true), `Docs/CROSSPLAY_PLATFORM_MATRIX.md` (which platforms and
what each needs), `Docs/DETERMINISM.md` (why the house is not replicated).

---

## 1. What exists, and what does not

**There is still no netcode package.** `Packages/manifest.json` contains no
Netcode for GameObjects, no transport, no relay, lobby, authentication or
multiplayer services package, and no Addressables. There is no `NetworkBehaviour`,
`NetworkVariable` or RPC anywhere in `Assets/`.

That is not an oversight, and it is not a decision either — it is a blocked
prerequisite. See §9.

What V4 built is everything that does **not** require the packages: the
authority model, the player registry, the session layer, the compact replication
state, and the request boundaries. The remaining work is an adapter, not a
redesign.

| Layer | State |
|---|---|
| Authority model | **Built.** `Core.SessionAuthority`, one provider |
| Player registry | **Built.** `Player.PlayerPresence` |
| Local vs remote player | **Built.** `PlayerController.DriveMode`, `RemotePlayerDriver` |
| Replication payload | **Built.** `Player.PlayerPresentationState` |
| Session contract | **Built.** `Session.*` |
| Join handshake | **Built.** `SessionGuard`, `JoinPayloadCodec` |
| Layout compare | **Built.** `LayoutSyncGuard` |
| Request boundary | **Built.** `Session.AuthorityRequests` |
| Mode selection | **Built.** `Session.SessionLauncher` — §6b |
| Online provider | **Seam only.** `IOnlineSessionProvider`, unimplemented — §9 |
| Character index | **Built.** `Deterministic.CharacterSelection` — §3b |
| Ghost presentation | **Built.** `Ghost.GhostPresentationState`, `RemoteGhostDriver` — §4b |
| Equipment ownership | **Built.** `Deterministic.EquipmentOwnership` — §5b |
| Connection readout | **Seam only.** `Session.ConnectionDiagnostics`, no probe — §8b |
| Reconnect | **Policy only. NOT PRODUCTION READY** — `Deterministic.ReconnectPolicy` — §8 |
| Transport | **Not built.** Blocked — §9 |
| Relay / Lobby / Auth | **Not built.** Blocked — §9 |
| NGO scene sync | **Not built.** Blocked — §9 |
| Host migration | **Not supported.** Deliberate — §8 |
| Dedicated server | **Not implemented.** Out of scope for V4 |

---

## 1b. Session mode and capacity (V4.1)

**Two products, one game.** The mode is chosen and then fixed.

| | Offline solo | Online |
|---|---|---|
| Players | exactly 1 | 1 to 8 |
| Topology | the local player | 1 host + up to 7 clients |
| Authentication | never | when implemented |
| Lobby / Relay / transport | never | when implemented |
| Internet | not required | required |
| Authority | local process | host |

`Session.SessionMode` is the choice. `SessionModeRules` answers what it permits —
capacity, whether online services may be initialised, whether a remote player can
exist at all.

**Mode is never inferred.** Not from the player count, not from whether a
NetworkManager exists, not from Relay or Lobby or Authentication state, not from
the scene, not from the platform, and above all not from whether the device
currently has a connection. `MultiplayerSessionService.Install` refuses to
replace a live session with one of the other mode, and says so.

The failure this prevents is specific: if mode followed connectivity, a solo
player whose Wi-Fi dropped mid-mission would have their session silently
reclassified, and a player who chose online and lost their connection would be
quietly told they are playing single player. Both are worse than an error
message.

`IsOffline` asks the mode, not the state. It previously read
`State == SessionState.Offline`, which conflated "the player chose single player"
with "no session has connected yet" — and every online session passes through the
second on its way up, so anything gated on it would have behaved as offline
during connection.

### Capacity has exactly one source

`MultiplayerProtocol.MaxPlayers = 8`. Lobby capacity, relay allocation,
connection approval, the development lab's spawn pads, `PlayerPresence`'s list
size and any player-count denominator derive from it. A second constant is a
second answer, and the two disagree the first time one is edited — which is
precisely what `NetworkLabInstaller` did with a serialized `playerPads = 4`
described as "the intended party size".

**Eight includes the host.** One host plus seven clients. Reading it as
host-plus-eight produces nine players and is the single most likely misreading of
this contract, so the suite asserts `1 + 7 == MaxPlayers` directly.

`HasCapacityFor` refuses a negative population rather than clamping it: a
negative count cannot come from counting real players, so it means the caller is
confused, and treating −1 as "plenty of room" would admit peers into a session
nobody can describe.

---

## 2. The one authority

`CatchIfYouCan.Core.SessionAuthority` holds exactly one `IAuthorityProvider`.
Everything that must be decided once asks it. `Equipment.EquipmentAuthority`
still exists under its V3 name and forwards; it holds no state of its own, and
the multiplayer guard fails if it grows any.

Today the provider is `LocalAuthority`, whose every answer is yes. That is
correct rather than a stub: in a single-player process the local player really
does own the world.

| Question | Asked by |
|---|---|
| `IsHost` | `MatchConfigBuilder`, `AuthorityRequests`, `ObjectiveBase.MarkComplete` |
| `CanSimulateGhost` | `GhostController`, `GhostPerception`, `GhostEvidenceManager`, `GhostInteractionBrain` |
| `CanChangeWorldState` | `PlaceableEquipmentBase.TryPlace`, `HeldEquipmentBase.TryPickupPlaced` |
| `CanConfirmEvidence` | `EvidenceValidator.Decide` |

Installing a session sets the authority in the same call
(`MultiplayerSessionService.Install`). "I am the host" and "I decide" are one
fact stated twice, and keeping them in two places is how they disagree during a
disconnect.

---

## 3. Who owns what

### Host

Mission seed, match configuration, procedural layout authority, ghost spawning,
ghost AI, ghost state, hunt state, ghost interaction decisions, evidence truth,
objective completion, mission state, door state, breaker state, world equipment
existence, placement and drop state, weather gameplay state.

### Owner-predicted

Local movement input, look input, selected inventory slot, equipment use, and
interaction *intent*. These deliberately do **not** ask the authority. Making a
player wait a round trip to press a button is what makes a game feel broken, and
none of them changes anything another player can see until the moment of
placement or interaction, which is gated.

### Local only, never replicated

Camera, `AudioListener`, touch HUD, `MobileInputController`, local input devices,
camera idle motion, post-processing, first-person body visibility, UI navigation,
haptics.

`RemotePlayerDriver` **destroys** the camera, the `AudioListener` and
`PlayerLook` on a remote player rather than disabling them. A disabled camera is
a camera somebody re-enables, and two `AudioListener`s in one process is a Unity
warning and a broken mix.

### Replicated presentation

Everything in `Player.PlayerPresentationState`: yaw, pitch, the movement stick in
the player's own axes, speed, crouch depth, sprinting, crouching, grounded. Plus
the root position, ghost transform and state, hunt state, confirmed evidence and
objective progress.

---

## 3b. Which character each player is (V5)

The choice is a string id in a save file and a **compact index** on a wire — the
catalog's order, which is why that order is documented as meaningful and why the
catalog is an explicit list rather than a folder scan. A scan answers "whatever
happened to be imported", which is a different order on two machines and
therefore a different character on two clients.

`Deterministic.CharacterSelection` holds the rules and is pure, so the offline
harness exercises them — 19 checks, including every one of the 256 bytes a peer
can send resolving to an index inside a real catalog.

**An index from another machine is a claim, not a fact.** `Check` names what is
wrong with one (`Unset`, `OutOfRange`, `EmptyCatalog`, `CatalogTooLarge`) and
`Resolve` substitutes the default. Neither can return something that would index
outside the catalog, and `CharacterCatalog.ResolveIndex` goes through them rather
than touching its array. The guard fails if it stops doing so.

The encoding limit is a **content** limit: the index travels as one byte with the
top value reserved for "unset", so a 256th character would be unnameable and
silently become somebody else. `CharacterService` says so once, as an error.

**`CharacterService` holds exactly one character: the one this machine chose.**
`PlayerFactory` reads it once, in the local entry point, and passes it down;
`Create(position, rotation, character)` is how anybody else's player gets built.
Reading the service per part would give every remote player the local player's
face, body metrics and rig profile — the same mistake as asking
`LocalPlayerService` where the player is. The guard counts the reads.

---

## 4. Why the pose is not replicated

**No bone, finger, hand target or elbow hint is ever sent.**

A remote player is the same prefab, the same rig, the same rigged hands, the same
Animator and the same `PlayerBodyMotion` as the local one. Given a yaw, a pitch,
a stick direction and a speed, it rebuilds the entire pose locally — the arm
holding the torch, the head turn, the crouch, the walk — because it is running
the same code that produced it.

Sending the pose instead would be a hundred transforms a tick to say what eight
bytes already say, and it would mean the remote character had a second animation
path that could disagree with the local one.

`PlayerController.ApplyRemoteState` writes the received values onto exactly the
properties a local player computes. `PlayerBodyMotion` cannot tell whether a
thumb or a network produced them, which is why **its pose mathematics has zero
diff across the whole of V4**.

---

## 4b. The ghost a client draws (V5)

The decisions were already host-only — the state machine, room awareness and the
footsteps that disturb salt sit behind `SessionAuthority.CanSimulateGhost`. What
that left on a client was a ghost standing at its spawn point, dormant and
invisible. `GhostPresentationState` is the other half.

Four fields: position, yaw, `GhostState`, and whether it is manifested. **What is
not in it is the ghost's reasoning** — no destination, no roam target, no hunt
target, no remaining hunt time, no perception. Those are why it is doing
something, and a client that had them could draw an arrow to the ghost. The guard
fails on a field named for any of them.

The animation is rebuilt, not sent: `GhostRigController` picks its clip from
`GhostState` and nothing else, so one enum reproduces the whole performance
everywhere. Same argument as §4.

Visibility is **told rather than inferred**. A manifestation can be refused — the
roll happens on the host — so a client that derived visibility from
`Manifesting` would show a ghost the host decided not to show. For the same
reason, expiring a manifestation moved under the authority gate: the end time it
compares against is only ever set where the roll happened.

`GhostStateMachine.AdoptReplicatedState` sets what the state *is* without running
`EnterState`, because entering a state runs the host's decisions for it — picking
a roam target, performing an interaction, resetting the path. A client that ran
those would be a second ghost making its own choices behind the same transform.
The guard reads the method body and fails if it calls `EnterState`, and fails if
`RemoteGhostDriver` reaches for `ForceState` instead.

The spectral reveal is deliberately **not** replicated: how lit the ghost is by a
grid projector is a fact about the viewer's own equipment. Two players pointing
two projectors each see what their own reveals.

---

## 5. Requests, not commands

`Session.AuthorityRequests` is where intent becomes a change to the world.

Two players reaching for the same torch on the same frame is not an edge case. The
only way one of them loses is if exactly one machine decides. `TryPickup` checks
reach first and the lifecycle second — in that order, so an out-of-reach request
cannot consume the item's transition and make a legitimate nearer request fail as
"already owned". The loser of a genuine race gets `WrongState`, because the item
is no longer in the world by the time their request is looked at.

Every check is performed by the authority against the world it can see, never by
the asker. Distance especially: a client that measures its own reach is a client
that can be told to measure generously.

---

## 5b. Whose equipment is whose (V5)

The project had no answer to "whose torch is that". An item knew it was equipped
and knew which transform it was parented to — enough with one player, and exactly
the shape of the mistake this repository keeps making. `AlreadyTaken` existed as
a refusal reason with nothing to check it against.

`Deterministic.EquipmentOwnership` is that check, and it is pure: 22 harness
checks including a two-player contest played out in full.

**−1 is a player, not the absence of one.** `MultiplayerProtocol.LocalOnlyClientId`
is the offline player's own id, so "nobody" is a separate value
(`NoClientId`). One value asked to mean both would make an item the solo player
is carrying read as unowned, and the first person past could take it.
`PlayerPresence` derives its constant from the protocol rather than declaring a
second one; the guard fails if it declares its own.

| Hold | Who may take it | Who may use it |
|---|---|---|
| `InWorld` | anybody in reach | anybody in reach |
| `Placed` | anybody in reach — a camera left in the wrong room must be movable | anybody in reach |
| `Carried` | nobody but its carrier | nobody but its carrier |

`EquipmentBase.Hold` is derived from **ownership and placement, never from
`IsEquipped`**: a holstered item is not in a hand and is very much still carried,
and deriving it from the wrong flag would put every stowed item back on the floor
as far as a second player is concerned. The guard reads the getter body.

Ownership changes in exactly two places — `PlayerInventory.AddItem` claims,
dropping and removal release — because those are the moments an item enters or
leaves a bag. Equipping and holstering are about which of *my* three items is in
*my* hand and are not ownership events at all. Releasing inside `Unequip` would
hand everybody's spare equipment to the first person who walked past.

`TryClaim` asks `SessionAuthority.CanChangeWorldState` and `OwnerClientId` has a
private setter, so the authority is the only thing that can change it. Offline
every claim is granted and single player behaves exactly as it always has. An
inventory learns whose it is from the `PlayerPresence` on its own player, never
from `LocalPlayerService`.

Reach is deliberately **not** part of this contract: how far away a claimant is
depends on positions and a tick of latency, and it belongs with the other spatial
checks in `AuthorityRequests` — against positions the authority can see, never a
distance the asker computed.

---

## 6. The session layer

`CatchIfYouCan.Session`, in `Scripts/Session`. Depends inward on the
deterministic contract; nothing depends outward on it.

- **`MatchConfigBuilder`** — the only place a session seed is rolled. Refuses on
  a client. Never returns zero. Does not use `UnityEngine.Random`, which is a
  global stream shared with gameplay: a session identity drawn from it would
  depend on how much wandering happened in the menu.
- **`JoinPayloadCodec`** — fixed binary layout, magic number, one bounded
  variable-length field, total size cap, big-endian, decoder that returns false
  rather than throwing. It is the first thing an untrusted peer sends and is
  decoded before any check has passed.
- **`SessionGuard`** — wraps `SessionCompatibility.CheckJoin`. Maps all eight
  `JoinVerdict` cases to an approval and a reason.
- **`LayoutSyncGuard`** — wraps `SessionCompatibility.CheckLayout`. Reports a
  mismatch as a determinism violation, not a network fault.
- **`IMultiplayerSession`** — the only multiplayer surface gameplay may see.
  `OfflineSession` is a real implementation of it, so single player asking "am I
  the host" gets "yes".
- **`SessionLauncher`** — the one place a session begins. See §6b.

**No gameplay component knows what Relay is.** The guard fails if one does.

---

## 6b. Choosing a mode (V5)

`MultiplayerSessionService.Install` used to have no callers. The process held an
offline session because nothing had replaced it, which made online unreachable in
the running game and made offline a default rather than a decision — two
different bugs with one symptom, a build that looks fine because the only
reachable mode is the one that needs nothing.

`SessionLauncher` is the entry point for both, and the only caller of `Install`.
The guard fails if anything else installs a session, because installing one also
sets the process authority, and two places doing that is how two parts of the
game end up disagreeing about who the host is.

```
PLAY
 ├── SOLO / OFFLINE  → SessionLauncher.BeginOfflineSolo()
 └── ONLINE          → SessionLauncher.BeginOnline(OnlineHost | OnlineJoin, joinTarget)
```

**Nothing runs at boot.** There is no `RuntimeInitializeOnLoadMethod` on the
launcher and nothing calls it from a scene load, so no online service is
attempted merely because the process started and airplane mode is a non-event.
The guard greps for a `Begin` call near a boot hook.

**Online is served through `IOnlineSessionProvider`,** which nothing implements
yet. Everything an online launch needs — signing in, allocating, connecting,
becoming host or client — happens behind that one call, so the menu asks for
online without knowing Authentication, Lobby, Relay or a transport exist. With no
provider registered, `BeginOnline` returns `LaunchStatus.NoOnlineProvider` and
changes nothing.

**An online launch that fails does not become an offline one.** Every refusal
path returns a status and leaves the current session alone — including the case
where a provider returns something whose `Mode` is not `Online`, which is refused
rather than installed. Falling back would hand a player who chose online a
single-player mission and no error. That is the failure mode §7b forbids, and the
guard counts the launcher's `new OfflineSession(` call sites so a fallback cannot
be slipped into a failure path.

`HasOnlineProvider` is what the play screen asks before offering online at all.
Offering a button that can only fail is worse than not offering it.

---

## 7. The deterministic boundary

`CatchIfYouCan.Procedural.Deterministic` imports no engine namespace and no
networking namespace, declares `noEngineReferences`, and references nothing. The
session layer adapts it; it does not know the session layer exists.

`GhostCatalogValidator.ComputeCatalogHash` deliberately does **not** fold into
`ContentSnapshot.ContentHash`. Doing that would change what an existing
ContentHash means, which is a deterministic-contract semantic change that would
silently make every current build incompatible with itself. Folding it in
requires a `MultiplayerProtocol.Version` bump, in one place, with the version
change that makes it honest.

---

## 7b. Offline is not a degraded online

Offline solo is a first-class product path, not multiplayer with the network
switched off. It does not create a session object that pretends to be a host, it
does not initialise Authentication, and it never reaches a transport.

**Gameplay has one implementation.** There is no `OfflineGhostController` beside
an `OnlineGhostController`, no `OfflineDoor` beside a `NetworkDoor`, no second
evidence manager. The ghost, the evidence, the mission, the objectives, the
equipment, the interactions, the character and the procedural generation are the
same code in both modes. Only the authority provider and the session
implementation differ, which is what §2 exists to make possible.

Offline authority is `SessionAuthority.LocalAuthority`, whose every answer is
yes — correct rather than a stub, because in a single-player process the local
player really does own the world. Every gate in the game asks the same question
it always did and gets the same answer.

Progression is unaffected: the local save path has no online dependency and must
not acquire one. If cloud sync arrives later it sits above local save, never
replacing it. No internet must never mean no progression.

---

## 8. Reconnect and host migration

**Host migration is NOT SUPPORTED in V4**, and is not faked. When the host
leaves, the session ends cleanly with a stated reason.

**Reconnect is a seam, not a feature.** `SessionState` distinguishes
`Connecting`, `Connected`, `Disconnecting` and `Failed`, and
`MultiplayerSessionService.Reset` returns to offline with local authority so a
game that lost its connection is still a game. Nothing more is claimed.

True mid-mission reconnect would require restoring, for the returning player:
their inventory contents and per-item battery and durability, their placed items
and those items' state, the evidence they had personally observed but not yet
confirmed, their objective progress, and their position. None of that is
serialised anywhere today. It is a real feature, not a flag, and it is not in V4.

**Dedicated server is NOT IMPLEMENTED.** The topology is listen-server: one
player is host.

V5 added `Deterministic.ReconnectPolicy`, which is **the policy, not the
mechanism, and NOT PRODUCTION READY**: nothing calls it and it has never
reconnected anything. What it is for is that the policy questions get answered
once, where they can be tested, rather than invented inside a retry loop later —
four attempts over ~15 seconds of doubling backoff, a seat held for 45 so a
player who uses every attempt still has somewhere to land, and two distinct
failures (`GaveUp`, `SeatLost`) because "the session carried on without you" is
not "we could not reach the host". The seat is checked before the attempt count:
telling somebody "attempt 3 of 4" once the host has filled their place is a
message that is wrong in a way they cannot see. 13 harness checks; the guard
fails if the NOT PRODUCTION READY warning is deleted.

The list above of what a true mid-mission reconnect would have to restore is
unchanged, and none of it is serialised yet.

---

## 8b. What the connection readout may claim (V5)

`Session.ConnectionDiagnostics` is the seam a transport plugs into —
`IConnectionProbe`, one method, which may say no. Nothing implements it.

**It never returns zero milliseconds.** With no probe it reports
`ConnectionQuality.Unknown`, and an unmeasured round trip is
`ConnectionRating.NoMeasurement` (−1), not 0. A confident "0 ms" on a game that
has never sent a packet is worse than no readout at all: it is the readout
somebody trusts while debugging why nothing arrives. The guard fails if the
out-parameter is ever initialised to zero.

**Offline and unmeasured are different answers.** Offline solo has no connection
and never will (`NotApplicable`); an online session that has not measured yet
will (`Unknown`). One value for both would leave "measuring" in the corner of a
single-player screen forever.

A host reports `Unknown` for itself rather than `Good` — a host's latency to
itself is not a fact about the session, and showing it as perfect is how a host
concludes the network is fine while everybody else is at 900 ms.

The bands live in `Deterministic.ConnectionRating`, pure and tested (8 checks),
so two readouts cannot end up with two sets of thresholds. They are chosen for a
co-operative game where nothing is contested frame by frame — what latency costs
here is a door opening late — and are not the bands a shooter would pick.

---

## 9. Why the transport is not here

Phase AN of the V4 plan was to install Netcode for GameObjects, Unity Transport,
Authentication, Lobby and Relay. It could not be done honestly in the environment
this work was carried out in:

- **No Unity Editor.** Packages cannot be resolved, and no code written against
  the real API could be compiled against it.
- **Unity's package and services endpoints are unreachable.** `packages.unity.com`,
  `download.packages.unity.com`, `api.unity.com` and `services.api.unity.com` all
  return a policy denial. Package versions compatible with `6000.5.10f1` could not
  be verified, and the phase's own stop condition is "required Unity packages
  cannot resolve".

Writing `NetworkBehaviour` subclasses against a hand-written stub would have
produced code that compiles in the offline harness and fails against the real
package — which is mistake 5 in `CLAUDE.md`, at scale.

**What is left to do** is an adapter that implements `IMultiplayerSession` and
`SessionAuthority.IAuthorityProvider` over NGO, forwards the request boundaries in
`AuthorityRequests` and `InteractionController.Request` as RPCs, and sends
`PlayerPresentationState` on the 20 Hz tick. The call sites exist. No gameplay
class changes.
