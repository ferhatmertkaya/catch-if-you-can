# Interactive room (01_MainMenu)

The room the player walks into after TAP TO START. It lives in the menu scene but is a separate
place: `MainMenu_InteractiveRoom` sits at x = 20, far enough from the diorama that the two never
see each other, and it is inactive until `MainMenuModeController` hands over.

Interior is **8 m (X) × 3 m (Y) × 6 m (Z)**, x 16→24, y 0→3, z −3→+3, centred on (20, 1.5, 0).
Floor top is exactly y = 0. Spawn is (20, 0.05, −1.5), unrotated, so the player faces +Z toward the
north wall with the room ahead of them.

There is no doorway. The shell is a sealed box of seven boxes — floor, ceiling, four walls and a
safety floor 2.5 m below that catches anything that ever escapes. That is deliberate, not missing:
the room is a lobby to stand in, and nothing beyond it exists yet.

**All seven BoxColliders were serialised disabled** and are now on. Every renderer was enabled, so
the room looked solid and was not: the player would have dropped through the floor, through the
safety floor, and out of the world. The safety floor's *renderer* stays off on purpose — it is a
catch plane, not a surface.

Spawn clearance, with the CharacterController's 1.8 m height, 0.35 m radius and (0, 0.9, 0) centre:
the capsule bottom starts 5 cm above the floor, 4 m from either side wall, 1.5 m from the wall
behind, 1.2 m below the ceiling. Nothing to push out of.

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

**The walls are dark.** They use the corridor material, whose base map averages 0.26 sRGB — about
0.055 linear. A room that dark needs real intensity before it reads, which is why the numbers below
look higher than the diorama's.

Fog was checked and left alone. It is exponential-squared at density 0.025, which at 10 m removes
about 6% — it was never the problem, and it belongs to the menu's look.

## The lights

All four objects are children of `MainMenu_InteractiveRoom`, so they switch on with the room and
cost nothing during the menu. **No horror event references any of them**, and none of them
references a horror event. Colours below are sRGB; Unity stores them linear in the scene file.

| name | type | colour | intensity | range | shadows | position |
|---|---|---|---|---|---|---|
| `InteractiveRoom_KeyLight` | Directional | (1.00, 0.89, 0.78) ≈ 4300 K | 2.2 | — | Soft, strength 0.8 | (20, 2.8, 0), euler (50, −25, 0) |
| `InteractiveRoom_LampWarm` | Point | (1.00, 0.80, 0.58) warm bulb | 6.0 | 10 | None | (18.4, 2.45, −0.6) |
| `InteractiveRoom_FillCool` | Point | (0.72, 0.80, 1.00) cool | 3.0 | 9 | None | (22.3, 2.1, 1.1) |

The key light is a **Directional** and that is not an accident. `CIYC_URP.asset` has
`m_AdditionalLightShadowsSupported: 0` — point and spot lights cast no shadows in this project — so
a directional is the only thing that can put the player's shadow on the floor, which is the whole
point of drawing the local body shadows-only. At 2.2 it also outranks the menu's 0.97 directional
and becomes URP's main light while the room is up; during the menu it is inactive, so the menu's
main light selection is untouched.

The two point lights carry the mood: a warm practical over the south half so the room reads as lit
from somewhere rather than flooded, and a dimmer cool one at the far end so the north-east corner
is dim instead of black. Neither requests shadows, both because the pipeline would ignore it and
because that keeps the cost at one shadow-casting light for the whole room. Three realtime lights,
one shadow caster, well inside `m_AdditionalLightsPerObjectLimit: 4`.

None of them flicker. That is on purpose for now — the room's job is to be reliably readable.

**The room surfaces now receive shadows.** All six had `m_ReceiveShadows: 0`, which meant the
shadows-only player body cast onto nothing and the key light produced no contact shadow at all.
They still do not *cast*: the shell is sealed, and if the ceiling cast shadows the directional
could not get in.

## Post-processing

The menu's global volume pulls **−0.3 EV with +20 contrast and −15 saturation**. That is right for
a near-black diorama and wrong for a room somebody has to walk around in, and being a global volume
it applies to any camera with post-processing on.

`InteractiveRoom_PostProcessing` is a second global volume, a child of the room so it is only live
once the room is, at **priority 10** so it outranks the menu's 0. Its profile overrides only three
things — post exposure back to +0.35, contrast to 5, vignette to 0.18 — and inherits Bloom and
Tonemapping from the menu profile, so the room is graded rather than ungraded.

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
