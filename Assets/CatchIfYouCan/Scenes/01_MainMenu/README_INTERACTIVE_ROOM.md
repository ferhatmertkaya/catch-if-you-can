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
