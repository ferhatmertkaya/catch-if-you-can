# Nathan — player character

Renderpeople "rp_nathan_animated_003_walking". Delivered as two FBX files and five JPEGs, with
nothing else: no Unity import settings, no material, no prefab, no idle animation.

Everything below was measured out of the delivered files, not assumed. Where a number matters —
the frame the walk cycle actually starts on, the speed the clip was authored at, how tall the
character is — the measurement is recorded so the next person does not have to take it on trust.

---

## Which FBX is the real one

The two files are not two versions of the same thing, and the smaller one is not the Unity-ready
one despite the `u3d` in its name.

| | `rp_nathan_animated_003_walking.fbx` | `..._walking_u3d.fbx` |
|---|---|---|
| size | 24,646,304 B | 1,011,280 B |
| mesh | **yes** — 10,828 verts, 11,007 polys (10,499 quads + 508 tris) | **none** |
| skinning | **yes** — 1 Skin, 88 Cluster deformers, 20,930 weights | **none** |
| bind pose | yes — `skinCluster1`, 91 nodes | none |
| material | 1 (`rp_nathan_animated_003_mat`, phong) | none |
| skeleton | 88 LimbNodes, named `rp_nathan_animated_003_walking_*` | 88 LimbNodes, named `root`, `hip`, … |
| hierarchy | `<root>/…_CTRL/…_root/…_hip/…` | `<root>/root/hip/…` |
| animation | `Take 001`, 69 keys, 30 fps, 2.2667 s | identical |

**Use the large one. It is the only one that contains a character.** The `u3d` file is the
skeleton and the animation on their own — and its bones are named differently and sit at a
different depth, so its clip cannot even be retargeted onto the other rig by transform path. It is
imported inert (`importAnimation: 0`, `animationType: 0`) so it costs nothing and cannot be picked
up by mistake.

**Why the "generic" file is 24× larger, when it holds the same animation:** 22,825,645 of its
24,646,304 bytes — 93% — are a single embedded JPEG, the 8192×8192 diffuse, stored at a much
higher quality than the standalone `dif.jpg` (22.8 MB vs 5.3 MB for the same 8192×8192 image). The
mesh, skin and bind pose together account for under 0.8 MB. Material import is switched off so
Unity never extracts that copy into a `.fbm` folder; the standalone JPEG in `Textures/` is used
instead.

## Scale, orientation and pose

Measured from the bind pose matrices and the mesh vertex bounds:

- `UnitScaleFactor = 1.0`, so the file is in centimetres. Convert Units is left on, giving **1.86 m**
  in Unity (mesh bounds Y: −1.305 cm to 185.608 cm; `head_end` bone at 184.99 cm).
- Feet sit on the origin — the sole geometry reaches 1.3 cm below y = 0. No offset needed.
- Hip at 94.4 cm, eyes at 1.719 m. The player's camera sits at 1.6 m, which is 12 cm below the
  model's eyes — harmless while the body is drawn shadows-only, worth knowing if that changes.
- **Bind pose is a T-pose**: both hands at y ≈ 152.4 cm, x = ±72 cm, arm span 189 cm against a
  height of 187 cm.
- **The character faces +Z** in the file (eyes at z = +8.8, toes at z = +18.7, ankles at z = −1.8).
  Facing is nevertheless re-measured after import from the ankle-to-toe vector, because the
  importer's axis conversion is the kind of thing that should be checked rather than assumed.

## The animation

One take, `Take 001`, 69 keys at exactly 30 fps (every key spacing is 0.033333 s), 2.2667 s.

**Frame 0 is the bind pose, not part of the walk.** Every rotation channel on every bone is exactly
0.0000 at frame 0 and then jumps into a full walk pose at frame 1 — the left upper arm moves 69.19°,
the left lower leg 58.45° — while the very next step, frame 1 to frame 2, is 0.87° RMS across all
261 rotation channels. Frame 0 is 14.94° RMS away from frame 1 and equally far from every other
frame in the take. Looping the take as delivered snaps through a T-pose once per cycle.

So the clip is split **frames 1 → 68**:

- Pose autocorrelation puts the stride period at **34 frames (1.133 s)** — two full strides in the
  usable range.
- Frame 68 is frame 1's closest match in the whole take at **1.49° RMS**, which is the noise floor
  of the take itself (the lag-34 autocorrelation minimum is 1.52°). Loop Pose closes the rest.
- **Root motion: the clip travels.** The `..._root` bone carries 290.18 cm of +Z over the take;
  across frames 1–68 that is 2.866 m in 2.233 s, an authored walk speed of **1.283 m/s**. The rig
  is Generic with the Root node set to that bone, so the travel is lifted into root motion curves,
  and `applyRootMotion` is off, so the character animates in place while the CharacterController
  owns movement. The setup tool samples the imported clip and reports the residual root drift; if
  extraction ever stops working, that number goes non-zero and says so.

`PlayerVisualAnimator.clipAuthoredSpeed` is set to that measured 1.283 m/s. Note the mismatch worth
knowing about: `PlayerController.walkSpeed` is 2.8 m/s, so the feet only keep up with the floor at
about 2.2× playback. That is a fast cadence for a walk. The honest fixes are a lower walk speed or
a run clip; the ceiling is a serialized field either way.

**There is no idle clip in the delivery, and none anywhere else in the project.** A walk cycle also
contains no standing frame — one knee is bent in every one of the 68 — so the setup tool builds one:
it samples the frame where the two thighs line up (frame 50, where they are 0.14° apart) for the
upper body, and puts the hip and both leg chains back to the bind pose, where the legs are straight
and shoulder width apart. The result is assembled entirely from the model's own data. A purpose-made
idle would still be better.

## Textures

All five are 8192×8192 except `mask02` (2048×2048). None ship at that size.

| file | what it is | how it was determined | used as |
|---|---|---|---|
| `dif` | albedo | colour, channels differ | `_BaseMap`, sRGB |
| `norm` | tangent-space normal | R/G centred on 128, B mean 247 with 88% above 247 | `_BumpMap`, Texture Type **Normal map** |
| `gloss` | **glossiness, not metallic** | fully grayscale, mean 0.51, range 0.31–0.99; skin and eyes bright, denim dark | packed into `_MetallicGlossMap` alpha |
| `mask01` | **the t-shirt** | binary mask; composited over the diffuse it selects exactly the shirt panels, sleeves and hem | not used by the material |
| `mask02` | **the jeans** | binary mask; composites to the denim panels | not used by the material |

The masks are garment selection masks for recolouring clothing in a DCC app. They are not ambient
occlusion, opacity, metallic or smoothness, and plugging them into any of those slots would be
wrong. They stay in the folder as source; nothing references them, so nothing puts them in a build.

URP Lit has no standalone smoothness input, so `rp_nathan_animated_003_metallicsmoothness.png` is
generated from the gloss map: RGB = 0 (skin and cloth are dielectric — metallic must be zero, and
mapping a glossiness map onto metallic would make the character look like foil), alpha = gloss.
`_Smoothness` multiplies that alpha and is set to 0.75, which puts skin near 0.49 and denim near
0.30; at 1.0 the skin reads wet under point lights. That one float is the dial to turn.

Import sizes — no 8K reaches a device:

| texture | editor / PC | Android / iOS |
|---|---|---|
| `dif` | 2048 | 1024 |
| `norm` | 2048 | 1024 |
| `metallicsmoothness` | 1024 | 512 |
| `gloss`, `mask01`, `mask02` (source only) | 512 | 512 |

## Mesh and rig settings

- 4 bones per vertex. The mesh has up to 6 influences, but only 13 vertices of 10,828 use 6 and 110
  use 5; clamping costs nothing visible and is worth it on mobile.
- Blend shapes off (there are none), cameras and lights off (there are none).
- Quads triangulated: 10,499 quads + 508 triangles → 21,506 triangles.
- Tangents calculated (MikkTSpace); the file has normals but no tangents.
- Constant scale curves removed — the take has no scale curves at all, and of 264 translation
  curves only 5 actually vary.
- No secondary UVs; the character is not lightmapped.

## Setting it up

Run **Catch If You Can → Characters → Build Nathan Player Visual** once after opening the project.

Three things cannot be checked in as files: the Animator Controller, the two prefabs and the
generated idle clip all reference objects *inside* the imported FBX, and those file IDs are minted
by Unity's model importer at import time. Hand-writing them would mean guessing numbers only Unity
can produce, and a wrong guess is a reference that silently resolves to nothing. The clip split and
the material remap are applied the same way, through the importer API, because their serialised
shape varies between Unity versions.

The tool is idempotent and prints what it measured: height, floor alignment, facing, bone count,
empty material slots, collider count, and the walk clip's length and loop flag. Everything else —
the rig type, root motion bone, mobile texture budgets and the URP material — is authored as files
and applies on first import.

It produces:

```
Animations/Nathan_Idle.anim                  generated standing pose
Animations/Nathan_PlayerVisual.controller    Idle (default) <-> Walk on IsWalking
Prefabs/Nathan_PlayerVisual.prefab           model + Animator + material
Assets/CatchIfYouCan/Resources/Characters/Player_CharacterVisual.prefab
```

`PlayerFactory` loads that last path by name, hangs it under the player's `VisualRoot`, binds the
Animator to `PlayerVisualAnimator` on the player root, and leaves `LocalPlayerBodyVisibility` to
hide the body from the local camera. The body is switched to shadows-only rather than deleted, so
the same prefab can be drawn in full for a remote player later; the animator is set to
`AlwaysAnimate` so the shadow does not freeze when the renderer is culled.

The character carries **no collider**. The `CharacterController` on the player root is the collider;
a second animated shape on the body would fight it.

## Not verified

**PLAY MODE NOT TESTED.** Unity is not available in the environment this was set up in. Import
settings, the material and the measurements above come from the files themselves; shading under the
game's lighting, foot contact, and how the loop and the generated idle actually read still need
someone to look at them.
