# Nathan player character — import checklist

The asset is **not in the repository**. `free3d.com` is blocked by this
environment's network policy (the proxy refused CONNECT with 403 before any
Free3D page was reached), so the archive could not be fetched here and no
substitute character was used.

Source requested: Nathan Animated 003 Walking, FBX package
`https://free3d.com/dl-files.php?p=5eba91a126be8b2b228b4567&f=3`

Download it with a normal authenticated browser session and drop the contents
in as below. Everything on the code side is already in place and picks the
character up automatically — see "How it hooks up".

## Where files go

    Models/       the FBX only (skip any Max/Maya/C4D versions in the archive)
    Textures/     diffuse, normal, alpha as shipped — do not rename them
    Materials/    materials extracted from the FBX (Materials tab -> Extract Materials)
    Animations/   only if you extract the walk clip to its own .anim
    Prefabs/      Nathan_PlayerVisual.prefab

Keep any LICENSE/readme files the archive ships with, at this folder's root.

## Import settings to check

**Model** — verify real-world height. The FBX's own unit scale decides this;
do not assume Scale Factor 1. The character should measure roughly 1.7–1.8 m
against the interactive room, whose ceiling is at y=3 and whose floor is y=0.

**Rig** — try Animation Type = Humanoid and confirm the Avatar is valid. If the
skeleton will not map cleanly, leave it Generic rather than forcing it; the
animation driver does not care which one is used.

**Animation** — find the real clip name in the FBX, do not guess it. Tick
**Loop Time**. Leave root motion off (the driver forces
`applyRootMotion = false` anyway, because the CharacterController owns movement).

**Textures** — set the normal map's Texture Type to *Normal map*. Give the
alpha texture whatever the material genuinely needs; prefer Alpha Clipping over
full transparency, and only on the material that needs it (usually hair or
eyelashes, not the body).

**Mobile** — iOS/Android overrides: Max Size 1024 for body/clothing, 512 for
smaller masks, compressed. The character is roughly a screen-height at most, so
2K+ everywhere is wasted memory on device.

**Materials** — shader `Universal Render Pipeline/Lit`. Diffuse into Base Map,
normal into Normal Map. If anything renders magenta the shader is still the
built-in one and needs converting.

## Animator

Create an Animator Controller under `Prefabs/` (or `Animations/`) with:

- parameters: `Speed` (float) and `IsWalking` (bool)
- states: Idle and Walk
- Idle -> Walk when `IsWalking` is true, Walk -> Idle when it is false

The archive ships a walking animation only. **There is no Idle clip in this
project** — no `.anim` or `.controller` assets exist anywhere in it yet — so
Idle needs either a separate idle animation or, as a stopgap, the walk clip on
a state with speed 0. A stopgap frozen pose is visibly wrong and should not
ship; a real idle clip is still required.

A blend tree on `Speed` is the better shape once backward/strafe/run clips
exist. `Speed` is fed in metres per second so the thresholds are real units.

## How it hooks up

Build the prefab as `Nathan_PlayerVisual.prefab`, then place a copy (or a
variant) at:

    Assets/CatchIfYouCan/Resources/Characters/Player_CharacterVisual.prefab

`PlayerFactory` loads that path if it exists and does nothing if it does not, so
the player stays fully playable until the character is in. On load it:

- parents the visual under `Player/VisualRoot`
- adds `PlayerVisualAnimator` to the **player root** and binds the Animator
- adds `LocalPlayerBodyVisibility` to the visual

`PlayerVisualAnimator` drives `Speed`/`IsWalking` from
`CharacterController.velocity` — the movement that actually happened, not the
input — so walking into a wall stops the animation, and the mobile joystick,
gamepad and keyboard all drive it identically.

Check the model faces +Z. If the FBX forward axis is wrong, rotate
`VisualRoot`'s child (the prefab root), never individual bones. If the feet sit
above or below the floor, offset the same prefab root; do not move the
CharacterController.
