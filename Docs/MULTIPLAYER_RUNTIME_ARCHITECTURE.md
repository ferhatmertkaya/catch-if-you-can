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
