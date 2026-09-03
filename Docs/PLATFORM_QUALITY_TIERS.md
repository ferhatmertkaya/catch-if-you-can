# Platform quality tiers

Status: normative for what may differ between platforms.
Owners: **38 Platform / Build** (profiles), **21 Lighting**, **22 Shader /
Material**, **23 Mirror**, **24 VFX** (what each tier costs), **3 Performance**
(whether a tier is affordable).

---

## 1. The rule

> **Gameplay is identical on every platform. Only presentation is tiered.**

The long-term target is **not** "mobile-looking graphics everywhere". Catch If
You Can targets high-end presentation on PC and console and a scaled profile on
phones. Architecture must support tiers rather than forcing the lowest common
denominator onto every target.

The inverse is equally binding: a tier may not change what a player can *do*.
Reflection resolution is a tier. Interaction reach is not. If two players on
different platforms would disagree about the world, it is not a tier — it is a
bug, and `CROSSPLAY_PLATFORM_MATRIX.md` governs it.

---

## 2. This already exists in code

`Scripts/Graphics/GraphicsManager.cs` defines `GraphicsProfile { Low, Medium,
High }` and applies quality level, render scale, shadows, target frame rate and
particle raycast budget. `Core/PlatformCapabilities.cs` is where a **capability**
is asked.

This document does not add a system. It says what the existing one is *for*, and
who decides each knob.

---

## 3. The tiers

| Tier | Targets |
|---|---|
| **Ultra / High** | Windows, macOS, capable console hardware |
| **Medium** | Lower-end desktop, and console profiles that need it |
| **Mobile** | iOS, Android |

Console tiers are **planned, not implemented**. No console code path exists and
none should be written speculatively — see `CROSSPLAY_PLATFORM_MATRIX.md` §4 for
where an adapter plugs in.

---

## 4. What may differ

| Knob | Owner | Notes |
|---|---|---|
| Shadow resolution and distance | 21 Lighting | Additional lights already cast no shadows in this project's URP asset. |
| Reflection resolution | 23 Mirror | Already tiered: `MirrorCorner` steps its buffer by `QualitySettings` level, capped, at screen aspect. |
| Reflection shadows | 23 Mirror | Already distance-gated. |
| Fog quality, volumetrics | 24 VFX | |
| Additional light count | 21 Lighting | The per-object budget is already spent on the room lamp, the fill, the mirror lamp and the torch. |
| Texture resolution | 39 Art Pipeline | Via import settings, not at runtime. |
| Post-processing stack | 22 Shader / Material | The reflection camera runs none, deliberately. |
| Ghost VFX density | 17 Ghost Visual / 24 VFX | |
| Particle raycast budget | 24 VFX | Already per-profile. |
| Render scale, target FPS | 38 Platform / Build | Already per-profile. |

## 5. What may never differ

- Player capacity. `MultiplayerProtocol.MaxPlayers = 8` is global — see
  `CROSSPLAY_PLATFORM_MATRIX.md` §2b.
- Generation. The same seed builds the same house everywhere, bit for bit.
- Evidence rules, hunt rules, interaction reach, equipment behaviour.
- Save format.
- Anything a player could gain an advantage from.

---

## 6. Not verified

No tier has been measured on any device. There is no Unity Editor in the
environment this was written in, and `T4` — proving identical hashes across
IL2CPP iOS-arm64 and Android-arm64 — is still outstanding. Every number in
`GraphicsManager` is an author's choice, not a measurement.
