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
| Transport | **Not built.** Blocked — §9 |
| Relay / Lobby / Auth | **Not built.** Blocked — §9 |
| NGO scene sync | **Not built.** Blocked — §9 |
| Host migration | **Not supported.** Deliberate — §8 |
| Dedicated server | **Not implemented.** Out of scope for V4 |

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

**No gameplay component knows what Relay is.** The guard fails if one does.

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
