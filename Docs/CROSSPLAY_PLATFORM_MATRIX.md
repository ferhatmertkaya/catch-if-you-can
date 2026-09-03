# Crossplay platform matrix

Status: **normative for the boundaries.** The platform columns are a plan;
the rules in §1 are not.

Companion document: `Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md`.

---

## 1. The three rules

1. **Gameplay never branches on platform.** A phone and a desktop in the same
   session play the same game by the same rules.
2. **The network protocol never branches on anything.** One protocol, one
   `MultiplayerProtocol.Version`, one handshake. There is no mobile protocol and
   no desktop protocol.
3. **Input differences end at the local player intent layer.** Touch, keyboard,
   mouse and gamepad all arrive at the same `MobileInputController` properties. A
   second path would be a second sensitivity, a second inversion setting and a
   second place to forget the pitch clamp.

If a platform ever needs a case inside the ghost, the equipment or the evidence
system, the seam was drawn in the wrong place.

The only platform branch in the tree is in `HapticManager`, and it is correct:
whether a device can buzz is a device fact, not a rule.

---

## 2. Capability, not platform

`Core.PlatformCapabilities` answers "is there a gamepad", not "is this a
console". A capability appears and disappears while the game runs — somebody
plugs in a controller, an iPad gets a keyboard — and a platform define cannot
describe that.

| Capability | Detected from |
|---|---|
| `Touch` | `Touchscreen.current` |
| `KeyboardMouse` | `Keyboard.current` or `Mouse.current` |
| `Gamepad` | `Gamepad.current` |

`WantsTouchControls` is `Touch && !KeyboardMouse`: a desktop with a touchscreen
keeps its keyboard and does not get a joystick drawn over the game.

---

## 3. The matrix

| Platform | Status | Input | Protocol | What it still needs |
|---|---|---|---|---|
| iOS | active target | touch, gamepad | shared | transport; background/foreground reconnect; Metal + IL2CPP validation |
| Android | active target | touch, gamepad | shared | transport; lifecycle handling; ARM64 + IL2CPP validation |
| Windows | active crossplay target | keyboard/mouse, gamepad | shared | transport; desktop network validation |
| macOS | active crossplay target | keyboard/mouse, gamepad | shared | transport; Apple Silicon validation |
| Xbox | **future adapter** | gamepad | shared | platform SDK, identity, store, certification |
| PlayStation | **future adapter** | gamepad | shared | platform SDK, identity, store, certification |
| Switch | **future adapter** | gamepad | shared | platform SDK, identity, store, certification |

**No proprietary console API is called, and none can be without that platform's
SDK.** The console rows are boundaries, not stubs — there is no TODO scattered
through gameplay waiting for them.

---

## 4. Where a console adapter plugs in

Two seams, and only two:

- **`Core.PlatformCapabilities`** — contributes an input capability. A pad is a
  pad; nothing downstream learns which console it came from.
- **`Session.IMultiplayerSession`** — contributes an identity and a session
  implementation. `MultiplayerSessionService.Install` takes it alongside an
  authority provider.

Everything else — the ghost, the equipment, the evidence, the mission, the HUD —
already asks those two and nothing more. That is enforced: the multiplayer guard
fails if a gameplay file reaches a session-service API directly.

---

## 5. What has actually been verified

**Nothing on a device.** Unity has not run during V4 — see
`MULTIPLAYER_RUNTIME_ARCHITECTURE.md` §9. Every row above is a static claim about
where the code branches, verified by reading and by the guard, not a build that
has been run on that platform.

The build validation matrix — iOS/Metal, Android/ARM64, Windows, macOS/Apple
Silicon, each under IL2CPP — is outstanding and is a prerequisite for the
transport work, not a follow-up to it. `DETERMINISM.md` §8 T4 says the same thing
about the cross-platform hash job for the same reason: a divergence found in
single player is cheap, and the same divergence found through a relay and two
devices is not.
