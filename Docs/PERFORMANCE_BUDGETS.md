# Performance budgets

Status: normative for **how performance is discussed**, not for any number in
it. Owner: **3 Performance**. Reviewer: **1 Main**.

---

## 1. The rule that matters most

> Every figure here is a **TARGET** until somebody profiles it on real hardware
> and marks it **MEASURED**, with the device and the build named.

There are currently **no MEASURED figures in this repository.** No Unity Editor
has run in the environment this project has been built in, no player build
exists, and no device has been profiled. Inventing a plausible frame time is
worse than having none: it is a number the next reader plans against.

A specialist who writes "60 fps on mobile" without a capture has fabricated
evidence, and QA rejects the handoff.

---

## 2. What gets tracked

**CPU** — player, ghost, equipment, physics, UI, audio, networking.
**GPU** — shadows, lights, mirror, transparency, fog, post-processing, ghost VFX.
**Memory** — textures, meshes, audio, RenderTextures.
**Network** — player state, ghost state, equipment state, world interactions.

---

## 3. Targets

| Target | Value | Status |
|---|---|---|
| Frame rate, mobile | 30 fps sustained | TARGET |
| Frame rate, desktop | 60 fps sustained | TARGET |
| Per-frame managed allocation, steady state | 0 B | TARGET |
| Server tick | 20 Hz — `MultiplayerProtocol.ServerTickHz` | CONTRACT |
| Session capacity | 8 — `MultiplayerProtocol.MaxPlayers` | CONTRACT |

CONTRACT means it is a fact about the code, not an aspiration.

---

## 4. Structural rules that need no profiler

These are not optimisations, they are defects, and they may be fixed on sight:

- A `Find`, `FindAnyObjectByType` or `FindObjectsByType` in `Update`,
  `LateUpdate` or `FixedUpdate`. Guarded for equipment by
  `check_equipment_catalog.sh`. `DoorHandle` once swept the whole scene per
  frame, per door.
- Allocating a `Material`, `RenderTexture`, array or closure per frame. The
  array-returning `GeometryUtility.CalculateFrustumPlanes` overload is a live
  example — use the one that fills a reused array.
- Rendering something nobody can see. The mirror renders only when the glass is
  inside the player's frustum.
- A property getter that reads itself. Guarded — it shipped once and was an
  uncatchable stack overflow.

Everything else needs evidence first. **Do not optimise on a hunch.**

---

## 5. Known costs, unmeasured

| Thing | Why it costs | Status |
|---|---|---|
| Planar mirror | A second render of the room at the player's FOV | TARGET — frustum-culled, distance-gated, quality-tiered, shadow-gated beyond 3 m |
| Ghost VFX / spectral reveal | Transparency and a presentation shell | TARGET |
| Room ambience and occlusion | Per-zone audio work | TARGET |
| Procedural generation | One burst at load, not per frame | TARGET |

---

## 6. How to add a MEASURED figure

State: the device, the OS, the build configuration (IL2CPP, release), the scene,
the quality profile, the capture tool, and the number. Anything short of that
stays TARGET.
