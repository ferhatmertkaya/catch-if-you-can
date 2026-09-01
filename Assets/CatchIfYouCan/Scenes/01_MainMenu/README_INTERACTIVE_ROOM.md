# Interactive room (01_MainMenu)

The room the player walks into after TAP TO START. It lives in the menu scene but is a separate
place: `MainMenu_InteractiveRoom` sits at x = 20, far enough from the diorama that the two never
see each other, and it is inactive until `MainMenuModeController` hands over.

Interior is **10 m (X) × 3.6 m (Y) × 10 m (Z)**, x 15→25, y 0→3.6, z −5→+5, centred on
(20, 1.8, 0). It was 8 × 3 × 6; the floor area went from 48 m² to 100 m². Walls are 0.3 m thick.
Floor top is exactly y = 0, ceiling underside exactly y = 3.6. Spawn is (20, 0.05, −3.5),
unrotated, so the player starts at the back of the room looking across it at the door.

The north wall carries **one doorway**, 1.2 m wide and 2.4 m tall on the centre line, built from
three wall pieces (left, right, and a header above) with a closed door filling the opening and a
jamb and lintel framing it. The door has its own collider, so the doorway reads as an exit without
being one. Skirting runs round the base of every wall — five pieces, no colliders, because the
walls behind them already stop the player.

The shell is otherwise sealed, and a safety floor 3 m below catches anything that ever escapes.

**All the original BoxColliders were serialised disabled** and are now on. Every renderer was enabled, so
the room looked solid and was not: the player would have dropped through the floor, through the
safety floor, and out of the world. The safety floor's *renderer* stays off on purpose — it is a
catch plane, not a surface.

Every box collider is a unit cube scaled by its transform, so resizing the geometry moved the
collision with it — there is no second set of numbers to keep in step. Ten surfaces collide: floor,
ceiling, four wall pieces, the header, the door, and the safety floor.

Spawn clearance, with the CharacterController's 1.86 m height, 0.35 m radius and (0, 0.93, 0)
centre: the capsule bottom starts 5 cm above the floor, 5 m from either side wall, 1.5 m from the
wall behind, 1.74 m below the ceiling. Nothing to push out of.

## Materials

Three families, so the eye can tell floor from wall from ceiling. All are generated tileable 1K
maps — no wallpaper, wood or plaster texture existed anywhere in the project, and nothing was
downloaded. Base colour and normal only; smoothness is a scalar, which is one fewer texture per
surface to ship and enough for surfaces this rough.

| material | textures | tiling | smoothness | on |
|---|---|---|---|---|
| `MAT_Room_Wall` | `CIYC_Room_Wall_*` | 5.3 × 1.8 | 0.06 | west, east, south walls |
| `MAT_Room_Wall_Segment` | same | 2.275 × 1.8 | 0.06 | the two pieces beside the doorway |
| `MAT_Room_Wall_Header` | same | 0.6 × 0.6 | 0.06 | the panel above the door |
| `MAT_Room_Floor` | `CIYC_Room_Floor_*` | 5.3 × 5.3 | 0.28 | floor |
| `MAT_Room_Ceiling` | `CIYC_Room_Ceiling_*` | 4.24 × 4.24 | 0.05 | ceiling |
| `MAT_Room_Trim` | floor textures, darkened | 1 × 1 | 0.22 | door, jambs, lintel, skirting |

The three wall materials exist only because the surfaces are different widths. Unity's cube maps
0–1 UVs across each face, so one tiling value cannot be right on a 10.6 m wall and a 4.55 m one at
the same time — that is what made the old room look stretched. Sharing the textures and varying only
the tiling costs nothing in memory.

Every tile is authored at a real size: the wallpaper tile is 2 m with stripes 12.5 cm apart, the
floor tile is 2 m across 8 boards so boards land at 25 cm, and the ceiling tile is 2.5 m.

## Why it was black, and what fixed it

Three things, in order of how much each mattered.

**It had no lights.** Every light in the scene belonged to the cinematic diorama, clustered around
x ≈ 2. The room was lit only by the two menu directional lights leaking through walls that do not
cast shadows — a dark brown one at 0.97 and a green one at 0.05. That is also exactly the
dependency that must not exist: those two are dimmed and restored by the phone, red-room and
ghost-closer events, so the room's readability was hostage to whichever horror beat had last
touched them.

**Ambient is off.** `m_AmbientMode` is Skybox and there is no skybox material, so ambient
contributes nothing. Anything not directly lit is absolute black. Raising ambient would have
brightened the cinematic menu too, which is not allowed, so the room gets a dim fill light instead.

**The walls were dark.** They used the corridor material, whose base map averages 0.26 sRGB —
about 0.055 linear — so nothing short of a lot of light made them read. They now use the wallpaper
above at roughly 0.32 linear, which is most of why the intensities below could come back down.

Fog was checked and left alone. It is exponential-squared at density 0.025, which at 10 m removes
about 6% — it was never the problem, and it belongs to the menu's look.

## The lights

All four objects are children of `MainMenu_InteractiveRoom`, so they switch on with the room and
cost nothing during the menu. **No horror event references any of them**, and none of them
references a horror event. Colours below are sRGB; Unity stores them linear in the scene file.

| name | type | colour | intensity | range | shadows | position |
|---|---|---|---|---|---|---|
| `InteractiveRoom_KeyLight` | Directional | (1.00, 0.89, 0.78) ≈ 4300 K | 0.9 | — | Soft, strength 0.75 | (20, 3.2, 0), euler (50, −25, 0) |
| `InteractiveRoom_LampWarm` | Point | (1.00, 0.80, 0.58) warm bulb | 7.0 | 14 | None | (17.5, 3.0, −2.5) |
| `InteractiveRoom_FillCool` | Point | (0.72, 0.80, 1.00) cool | 3.2 | 13 | None | (22.5, 2.8, 2.5) |

The key light came **down** from 2.2 when the room grew, which looks backwards until you notice the
walls changed too. The old corridor material averaged 0.26 sRGB — about 0.055 linear — and needed a
lot of light before it read at all. The new wallpaper is around 0.60 sRGB, roughly 0.32 linear, so
the same intensity would have blown it out. The point lights went up in range instead, because
range is what a bigger room actually needs.

The key light is a **Directional** and that is not an accident. `CIYC_URP.asset` has
`m_AdditionalLightShadowsSupported: 0` — point and spot lights cast no shadows in this project — so
a directional is the only thing that can put the player's shadow on the floor, which is the whole
point of letting the player look down at their own legs. At 0.9 it still outranks the menu's dark
0.97 brown once colour is taken into account and becomes URP's main light while the room is up;
during the menu it is inactive, so the menu's main light selection is untouched.

The two point lights carry the mood: a warm practical over the south half so the room reads as lit
from somewhere rather than flooded, and a dimmer cool one at the far end so the north-east corner
is dim instead of black. Neither requests shadows, both because the pipeline would ignore it and
because that keeps the cost at one shadow-casting light for the whole room. Three realtime lights,
one shadow caster, well inside `m_AdditionalLightsPerObjectLimit: 4`.

None of them flicker. That is on purpose for now — the room's job is to be reliably readable.

**The room surfaces receive shadows.** They shipped with `m_ReceiveShadows: 0`, which meant the
player's body cast onto nothing and the key light produced no contact shadow at all. They still do
not *cast*: the shell is sealed, and if the ceiling cast shadows the directional could not get in.

## Post-processing

The menu's global volume pulls **−0.3 EV with +20 contrast and −15 saturation**. That is right for
a near-black diorama and wrong for a room somebody has to walk around in, and being a global volume
it applies to any camera with post-processing on.

`InteractiveRoom_PostProcessing` is a second global volume, a child of the room so it is only live
once the room is, at **priority 10** so it outranks the menu's 0. Its profile overrides only three
things — post exposure back to 0, contrast to 5, vignette to 0.18 — and inherits Bloom and
Tonemapping from the menu profile, so the room is graded rather than ungraded. Exposure is neutral
rather than lifted now that the surfaces are no longer nearly black; it only cancels the menu's
−0.3.

Worth knowing: `GraphicsManager.ApplyCameraSettings` turns post-processing on per camera according
to the graphics profile, and it runs over the cameras that exist at the time. The player camera is
built later, at handover, so it starts with URP's default (`renderPostProcessing` off) until a
profile is next applied. **The lighting above is set so the room reads correctly either way** —
with no grading it is a touch flatter, not dark. That is why the light intensities are not tuned to
depend on the +0.35 EV.

## What must stay true

- The room owns its lighting. If every menu light were deleted the room would still read.
- No horror event may take these lights. The events reach for `Area Light`, `Directional Light`,
  `Door_Green_Spot` and `Door_BackGlow`; keep it that way.
- The cinematic menu is unchanged before TAP TO START. Everything here is inactive until then.
- After handover: no phone, no horror event, no menu audio. `PhoneAudio` is the scene's only
  AudioSource and is in the controller's `cinematicAudioSources`; `RotaryPhoneRandomRing` has no
  scheduler of its own, so the phone event is the only thing that can ring it, and
  `CinematicModeEnded` latches that off for good.


## The player in the room

**Controls.** `TouchHudFactory` builds the HUD in code the moment the player is created, and the
mode controller shows it only after the screen has faded back in — so nothing appears during the
intro or the cinematic, and nothing appears over a black frame. A stick bottom-left, a hold-to-run
button beside it, and an invisible look area over the right 55% of the screen. Everything sits
inside a `SafeAreaFitter`, so a notch moves the controls rather than covering them.

The look area is a UI element, not a raw touch scan, and that is the fix for the thing that was
actually broken: `MobileInputController` used to ask *"is any pointer over UI?"* once a frame and
switch looking off if the answer was yes. The movement stick is UI, so holding it made looking
impossible — the exact opposite of moving and looking at once. Now the EventSystem routes each
finger to exactly one widget and the two thumbs cannot contend. Only the mouse is still blocked by
the cursor being over UI, which is what you want on desktop.

| | value |
|---|---|
| stick dead zone / radius | 0.14 / 96 px of a 300 px touch square |
| walk / run | 1.9 m/s / 3.8 m/s, hold the button |
| look sensitivity | 0.28°/px yaw, 0.22°/px pitch, at a 1080 px reference height |
| look smoothing | 0.02 s, carrying the remainder forward |
| pitch clamp | −80° to +80°, yaw unrestricted |

A 500 px thumb swipe — a comfortable one on a landscape phone — turns about 140°. Input is
normalised to a 1080 px reference height, so the same swipe turns the same amount on a 720p device
and a 1440p one.

**Look was dead on device before this, and the reason is worth writing down.** `LookDelta` was a
per-frame accumulator cleared at the top of `MobileInputController.Update`. Its only writer for
touch is a UI drag callback, which Unity dispatches from the *EventSystem's* `Update` — and the
order of two MonoBehaviour `Update`s is undefined. When the EventSystem ran first, the delta was
added and then wiped before `PlayerLook` read it in `LateUpdate`. Movement was immune because the
joystick reports persistent state that nothing can erase, which is exactly why the symptom was
"moving works, looking does not". The accumulator is now stamped with the frame it was gathered in
and never cleared: reading it on a later frame yields zero, and since `LateUpdate` always follows
every `Update`, the consumer sees the whole frame's delta whatever order the Updates ran in.

A second, quieter bug rode along with it. `PlayerLook` smoothed with `SmoothDamp` towards the raw
delta — but a per-frame delta drops back to zero the instant the thumb stops, so the filter never
caught up and swallowed part of every short swipe. It now drains a pending buffer instead, which
changes *when* rotation arrives but never *how much*.

Walk speed came down from 2.8. Nathan's only clip is authored at a measured 1.283 m/s, and at 2.8
the walk cycle had to play at 2.2× to keep the feet on the floor, which reads as a scurry. At 1.9
it plays at about 1.5×. Sprinting would need 2.96×, which is not a run; playback is capped at 2.0
and the feet slide, which is the honest fallback until there is a run clip. `PlayerVisualAnimator`
already writes an `IsRunning` bool if the controller declares one, so adding that clip later is an
Animator change and not a code change.

**Footsteps** come from distance travelled, not from a timer and not from input. That one choice
gives most of the required behaviour for free: easing off the stick thins the steps out, and
pushing into a wall stops them dead, because the controller reports the movement it achieved rather
than the movement you asked for. The previous version read `PlayerController.CurrentSpeed`, which is
`input × speed` and stays at full walking pace while you lean on a wall.

Strides are 0.66 m walking and 0.92 m running, so at the speeds above a step lands about every
0.35 s and 0.24 s. The first values, 0.82 m and 1.15 m, sounded half-speed against the legs: the
walk clip plays at 1.48× and its gait cycle carries two steps, so the visible cadence is about
0.38 s, which the old 0.43 s lagged behind. Four placeholder wood clips are generated — synthesised from a low board thud, a
mid knock and a short scuff, not taken from anywhere — and are chosen without repeating the last
one, with ±4% pitch and ±8% volume. Replace them by dropping real recordings into
`Resources/Audio/SFX/Footsteps`. One AudioSource is reused for every step.

The floor carries a `FootstepSurface` component saying Wood. That replaces reading
`collider.name.ToLowerInvariant()` on every step, which allocated a string, broke on rename, and
silently resolved anything unrecognised to wood anyway.

**The body.** Nathan is one skinned mesh with one material, so there is no head renderer to switch
off. `LocalPlayerBodyVisibility` collapses the head *bone* on the local instance instead: the skull,
jaw, eyes and hair fold into a point at the top of the neck and everything below the collar draws
normally, which is what puts a chest, hips, legs and shoes under the camera when the player looks
down. The camera sits at 1.78 m with a 5 cm near plane — the closest it gets to its own shoulder
looking straight down is about 25 cm.

The character stands about **1.93 m**: the measured 1.86 m model with a 1.04 scale on `VisualRoot`,
a 4% lift that stops the viewpoint feeling short without reading as a giant. The scale goes on the
visual root and nowhere else, so movement and collision keep their own numbers; the feet are at the
character's local origin and that sits at the player's, so scaling about it leaves them on the
floor. The capsule went to 1.86 m with its centre at 0.93, and the camera to 1.78 m to stay at the
scaled eye bones — the three move together, or the camera ends up in the chest. Ceiling clearance
is still 1.74 m.

Nothing about the shared prefab, mesh or skeleton changes; it is a runtime scale on one instance, so
a remote player's copy simply never enables the component and draws in full, head included. The cost
is that the local shadow is headless. `ShadowsOnlyBody` is kept for anyone who would rather have the
complete silhouette and no visible body.

### Why none of that was visible on the device

Everything in the paragraphs above was true of the code and false of the running game, and the
reason is worth writing down because the symptom pointed somewhere else entirely.

`PlayerFactory.AttachCharacterVisual` loads `Resources/Characters/Player_CharacterVisual`. That
prefab **does not exist in the repository** — `Assets/CatchIfYouCan/Resources/Characters/` holds a
folder `.meta` and nothing else, because the prefab is *generated* by the editor step
`Catch If You Can > Characters > Build Nathan Player Visual`, and generated assets are not checked
in. `Resources.Load` returned null, the method returned null, and it did so **silently**. Nothing
downstream complained either: `PlayerVisualAnimator` was never added, `LocalPlayerBodyVisibility`
was never added, and the player was a camera on a capsule — which is precisely what a screenshot of
a floor and a HUD with no body looks like.

So the body was not being hidden by a render mode. It was never instantiated. `ShadowsOnlyBody` is
*not* the default and was not the cause: `LocalPlayerBodyVisibility.mode` defaults to
`FirstPersonBody`, which keeps every renderer on and only collapses the head bone. That component
never ran at all.

Two changes close this:

- The null branch in `AttachCharacterVisual` is now a `Debug.LogError` naming the missing path and
  the menu item that builds it. A missing character is not a normal state and must not be quiet.
- `NathanAutoSetup` (`[InitializeOnLoad]`, editor-only) runs the build step on first editor load if
  the prefab is absent, so a fresh clone produces a body without anyone having to know which menu
  item to click. It is guarded against batch mode, play mode, and repeat attempts within a session
  via `SessionState`, so it costs one check per editor session and nothing at runtime.

The build step is still the only thing that creates the prefab; the auto-setup just makes sure it
has been run.

## Auto-run

Holding the stick forward breaks into a run on its own, the way it does in Fortnite and PUBG
Mobile. Walking starts on the first frame the stick moves — the timer only decides when the *run*
takes over, so nothing is ever standing still waiting for a hold to complete.

| | value |
|---|---|
| `autoRunHoldDuration` | 0.7 s |
| `autoRunStickMagnitude` | 0.85 |
| `autoRunForwardDot` | 0.8, about a 37° cone |
| `speedBlendTime` | 0.15 s |

The direction test is a cone, not an axis check: forward-left and forward-right still count, a hard
strafe does not, and anything backward does not. The hold resets the instant the stick leaves the
cone, which is what makes releasing or pulling back cancel the run immediately rather than after a
delay. Crouching cancels it too.

Speed is `SmoothDamp`ed between walk and run rather than switched, so breaking into a run reads as
picking up pace instead of the world suddenly moving faster.

The manual sprint button is untouched and coexists — `IsSprinting` is the OR of the button, the
optional stick threshold, and auto-run. `MovementEnabled = false` and `SetHidden(true)` both call
`CancelAutoRun()`, so control is never handed back to the player already sprinting.

None of this touches look. Auto-run reads `MoveInput` only, which comes from the left-hand joystick
widget; the right-hand `TouchLookArea` is a separate UI element and the EventSystem routes each
finger to exactly one of them. Running with the left thumb and looking with the right is the case
this was built for.

## The window

The east wall has a real opening in it, 1.4 m wide × 1.6 m tall, sill at 0.95 m. It is a hole in
the geometry, not a texture: the old single `Wall_East` was replaced by five pieces —
`Wall_East_South`, `Wall_East_North`, `Wall_East_Sill` and `Wall_East_Header` around the gap, plus
`Window_Blocker`, a collider-only box (renderer off) that seals the opening so the player cannot
walk out through it. Colliders on room geometry are unit cubes scaled by their transform, so the
four wall pieces collide exactly as they draw.

`Window_Glass` is a thin box, 4 cm deep, in `MAT_Room_Glass`: URP Lit on the transparent surface
path (`_Surface: 1`, src `SrcAlpha` / dst `OneMinusSrcAlpha`, `ZWrite` off, queue 3000), base
alpha 0.17 and smoothness 0.88. That is one transparent quad, one draw call, no refraction, no
grab pass and no second camera — deliberately, because a mobile GPU pays for overdraw and this is
the whole cost of the effect. Jambs, head, sill and mullions are separate boxes in the room's trim
material, which is what makes the opening read as Victorian joinery rather than a cut rectangle.

## What is outside

**The sky is a per-camera skybox, not geometry.** The scene runs exp² fog at density 0.025, which
means anything beyond roughly 40 m is fog-coloured; a sky dome placed outside the window would be
erased before it was ever seen. Skyboxes are not fogged, so the sky survives and the near silhouettes
— which *are* geometry, and *are* fogged — sit in front of it with depth for free.

It is attached with a `Skybox` component **on the player camera only**, not the scene's lighting
settings, so the cinematic menu camera inherits nothing and the main menu before TAP TO START is
untouched. There is no `SkyCamera` and no `WindowCamera`; one shared `PlayerCamera` does the whole
job, with `clearFlags` set to `Skybox`.

The sky is `CIYC_HauntedNight_Panorama.png`, supplied as artwork and resampled from its native
1774×887 to **2048×1024**. The source is already an exact 2:1, so nothing is stretched; it is
resampled only to reach a power of two, which is what lets ASTC and BC compress it without a
non-power-of-two fallback and makes the mip chain exact. 2048 is also the ceiling worth having —
the source carries no detail above 1774 px, so a larger import would cost memory for nothing.

Its measured properties: mean luminance 16/255 (genuinely dark, so exposure sits at Unity's neutral
1 rather than being pulled down), the moon at u = 0.311 / v = 0.406, and the horizon at 60.2% of
image height rather than 50%. That last one matters — in a latlong projection the vertical centre
is eye level, so this horizon sits about **18° below** eye level and the player looks slightly down
into the valley. It reads as a room above a valley, which is what the room is, but it is why the
foreground silhouettes are switched off (below).

**Seam and poles.** The supplied edges did not join: the left-to-right discontinuity measured 2.98
against a typical adjacent-column difference of 1.85, so about 1.6× a normal step — mild, but real.
A 6 px circular cross-fade (about 1° of longitude, far too narrow to ghost content) brings it to
0.00, so columns 0 and 2047 are now identical and the wrap is continuous under any rotation. The
poles are dark and low-variance (top mean 12.9 σ 4.0, bottom mean 4.9 σ 3.3), so neither pinches
into a visible swirl at the zenith or nadir. `wrapU` is Repeat — required, or rotating the sky
tears at the seam — and `wrapV` is Clamp.

The skybox *material*, `MAT_Skybox_HauntedNight`, is built by the editor step
`Catch If You Can > Environment > Build Interactive Room Sky` rather than checked in as hand-written
YAML, for a specific reason: `Skybox/Panoramic` is a built-in shader and built-in shaders are
referenced by a numeric file ID inside Unity's own resource bundle. Guessing that number is exactly
how you get a magenta sky with no way to tell why. Asking Unity for the shader by name and letting
it write the reference cannot be wrong. The materials land in `Resources/Sky`, which also guarantees
the shader survives shader stripping in a mobile player — a runtime `Shader.Find` with nothing
referencing the shader is the classic way to get a sky that works in the editor and is pink on the
device.

That step also owns the panorama's mobile import settings, for the reason set out in
`NathanTextureImportSettings`: a stored iOS entry left on Automatic resolves against the platform's
retired PVRTC default and warns in Unity 6.5, so the committed `.meta` carries no mobile entry at
all and the format is named in code as a `TextureImporterFormat` constant. Desktop stays Automatic
(the BC family it picks is current); Android and iOS are ASTC 6×6 at 2048, about 1.2 MB with mips
for the single texture that is the entire outside world.

**The interior lighting is unaffected by any of this**, and that is a property of the architecture
rather than a tuning exercise. Ambient light comes from `RenderSettings.skybox`, which is still
empty; a `Skybox` *component* overrides the sky for one camera and does not feed the ambient probe.
So the bright moon in the panorama cannot brighten the room, and the room's authored lighting stays
authoritative without anything having to be dialled back.

In front of the sky: `Exterior_Ridge` (3 boxes) and two variant groups, `Exterior_Trees` (5) and
`Exterior_Ruins` (5), all in `MAT_Exterior_Silhouette` — an almost-black matte base at
(0.020, 0.026, 0.038). **These are switched off by default** (`useForegroundSilhouettes`). They
were massed against the flatter placeholder sky they were built for, and the Haunted Night panorama
already contains its own forest, ridge line, valley and distant house; crude boxes in front of
painted ones would subtract rather than add. Turning the toggle on restores near-field parallax,
which is what most strongly stops a window reading as a picture — expect to re-tune their heights
against the new horizon first, since it is 18° lower than the one they were placed against.
`WindowMoonlight` is a cold point light at (24.2, 1.9, 0), colour
(0.34, 0.47, 0.72) as stored, which is linear, range 5, shadows off, which spills onto the sill and floor so the window is a
light source in the room rather than a picture on the wall.

## Exterior randomisation

Cosmetic only, and deliberately unable to reach anything that matters. `InteractiveRoomExterior`
carries its own `System.Random`, seeded from `DateTime.UtcNow.Ticks` mixed with the instance's hash
— it does not touch `SeedManager`, the procedural house RNG, the deterministic map RNG, or any
network seed, and nothing it decides is ever read back by gameplay. Multiplayer and procedural
determinism are untouched by this change.

Per session it picks a sky rotation of 143° ± 6°, an exposure in 0.92–1.06, and a moonlight
intensity in 0.25–0.75. The sky material is instantiated at runtime before rotation and exposure
are written, so the asset on disk is never mutated — without that copy the randomisation would
dirty the material and persist into the next session.

The rotation is no longer a free 0–360°. This sky is a painting with a moon in it, and spinning it
freely would as often as not point the window at empty horizon. 143° is derived rather than
guessed: with `_Rotation` at 0 the panoramic shader puts u = 0.5 toward −Z, so the moon at
u = 0.311 sits at longitude −68°; the window faces +X, which is +90°; bringing the moon to about
+75° — just off centre, framed against the ridge rather than dead ahead — is a 143° shift. Its
elevation, +16.9°, puts it comfortably above the horizon. **The sign convention is the one thing
here that wants an eye on it:** if the moon ends up behind the player rather than in the window,
the value is 360 − 143 = **217°**, and `skyRotation` is serialized on the component precisely so
that is a one-field change rather than a code change.

## Cost

One transparent quad, one skybox draw, one extra point light (which casts no shadows — the URP
asset has additional-light shadows off), and at most 8 silhouette boxes sharing one material (Lit, but metallic 0 and
smoothness 0.02, so it costs the shader and not the lighting).
The wall split adds four box renderers where there was one. Nothing here allocates per frame and
nothing here adds a camera.

## The candle

`CandleFX` holds three flames and one light:

```
CandleFX
├── Flame_Left     ParticleSystem + CandleFlameFlicker
├── Flame_Center   ParticleSystem + CandleFlameFlicker
├── Flame_Right    ParticleSystem + CandleFlameFlicker
└── CandleLight    Point Light    + CandleFlicker
```

**One real-time light, not three.** Three point lights 1.6 cm apart cannot produce three
distinguishable pools of light — they sum to one slightly wider pool at three times the per-pixel
cost, on a renderer configured for four additional lights per object. The three flames are
independent where independence is actually visible (the flame itself), and share a light where it
is not. `CandleLight` casts no shadows, because the URP asset has additional-light shadows off
project-wide.

### Why the flames could not be seen

`MAT_CandleFlame` was authored on `Universal Render Pipeline/Particles/Lit` with `_EmissionColor`
at black and no `_EMISSION` keyword. A lit material can only show what the room reflects onto it,
and this room's ambient is black by design (`m_AmbientMode: Skybox` with no skybox material), so
the flame had nothing to be lit *by*. Additive blending was the only reason anything appeared at
all. It was never a flicker problem, and the flame was never hidden — it was being drawn almost
black.

`CandleFlameSetup` moves the material to `Particles/Unlit`, which is both correct (a flame is a
light source, it should not be shaded by the room) and cheaper on a phone, and gives it a warm
base colour above 1. The shader is resolved with `Shader.Find` rather than by writing a GUID, for
the same reason the skybox is built in code; if the unlit shader cannot be resolved the material
keeps the one it has and emission is switched on there instead, which is worse but never invisible.
The material is upgraded **in place**, so its GUID is unchanged and the three renderers still point
at one shared asset.

The tint is close to white on purpose. `CIYC_CandleFlame.png` already runs from a pale yellow core
(254, 250, 120) through orange to a dark red tip, and the particle's own start colour multiplies a
second orange over it; a third would have produced a red blob. The brightness above 1 is what makes
the core read as hot — additive blending clips the centre toward white while the thinner edges stay
orange. **This does not depend on bloom:** the project's Bloom override is authored at intensity 0,
so nothing here relies on a glow that is switched off. Raising it to about 0.15 would add a soft
halo, but that is a global post setting and would change the main menu too, so it is left alone.

### Independent flicker

Each flame owns its scale, roll and brightness; the light owns its own intensity. All of it is
two-layer Perlin, never `Random.Range` per frame — white noise reads as a failing bulb because
consecutive frames are uncorrelated.

| | Center | Left | Right |
|---|---|---|---|
| seed | 11 | 27 | 43 |
| scale X / Y | 0.08 / 0.16 | 0.07 / 0.14 | 0.09 / 0.18 |
| scale speed | 2.2 | 2.6 | 1.9 |
| roll ° / speed | 5 / 1.8 | 4 / 2.1 | 6 / 1.5 |
| brightness amount / speed | 0.16 / 2.40 | 0.14 / 2.90 | 0.18 / 2.05 |

Brightness runs on its own noise offset and its own speed, so a flame is not at its brightest
exactly when it is at its tallest. Brightness is clamped to 0.72–1.20 of the authored colour, so a
flame never goes out. The slowest flame swings widest, which is how a real wick behaves.

Simulating those rates over 60 s at 60 fps gives pairwise correlations of −0.016, −0.078 and +0.009
on brightness and −0.006, −0.036, +0.026 on scale: uncorrelated in practice. The closest pair of
brightness rates beats every 1.18 s, so there is no slow drift into unison either. (That is a check
of the maths, not a Unity run — Unity's `PerlinNoise` is not the generator used for it.)

Position is never written. That is what keeps the base of each flame welded to its wick.

### Why the flame's colour is not the particle's start colour

Each flame is **one** particle: `maxNumParticles: 1`, `startLifetime: 3600`, prewarmed. It is
emitted once and then simply persists. `main.startColor` applies to particles at emission, so
writing it afterwards changes nothing that is on screen — which means the red event's flame
recolouring, which did exactly that, had never actually worked. The lights turned red and the
flames stayed orange.

`CandleFlameFlicker` writes a `MaterialPropertyBlock` instead. A property block is read at draw
time, so it reaches the particle that is actually burning, and it does not instance the material —
all three flames still share one asset. The block is allocated once in `Awake`; `Update` writes
into it and nothing allocates. `sharedMaterial` is read once for the baseline colour, never
`material`, which would clone the asset per flame and defeat the point.

### Ownership, and the horror events

One writer per property, which is the rule that keeps events and flicker from fighting:

| property | sole writer | how events reach it |
|---|---|---|
| `Light.intensity` | `CandleFlicker` | `ApplyEventModulation(scale, turbulence)` |
| `Light.color` | the red event | written directly; nothing else touches colour |
| flame transform | `CandleFlameFlicker` | not touched by events |
| flame colour + brightness | `CandleFlameFlicker` | `ApplyEventModulation` / `ApplyEventColour` |

Event values are **assigned, never accumulated**, and both components restore themselves on
`ClearEventModulation`, so an interrupted event cannot leave the candle permanently dimmed or
permanently red. The flame's envelope opens up with the turbulence an event asks for on exactly the
same curve the light uses, so the flame and the pool of light it casts never disagree about how
hard the candle is guttering.

The colour wins and the flicker rides underneath it: a red flame still gutters rather than sitting
flat. Because the particle's own start colour stays orange and the property block multiplies over
it, pulling the base toward (0.85, 0.10, 0.06) suppresses green to about 0.07 and blue to 0.01 —
a deep red flame rather than a muddy one.

All three events now drive the flames, not just the red one. That is a consequence of the flames
becoming visible: the ghost event dims the candle to 0.03 for its blackout, and a blackout with
three bright flames still burning in it would read as the candle being lit by something else.
