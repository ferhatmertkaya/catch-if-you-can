# The purchased portal pack

**Normative for the seam.** What a bought portal asset can and cannot contribute to this
project, and how to adopt the part that can.

The pack in question is *Portal Effect: HDRP* (Knife Entertainment), imported locally to
`Assets/Knife/Portal HDRP/`. Nothing in this document is specific to that pack; it is the
contract for adopting any bought portal asset.

---

## 1. Why the pack cannot simply be dropped in

A portal asset is four kinds of thing in one folder, and they do not travel together.

| What | Crosses to URP? | Why |
|---|---|---|
| Shaders | **No** | Authored against HDRP's shader library. |
| Materials | **No** | Only as good as the shader they point at. |
| Textures | **Yes** | Images. Pipeline-independent. |
| Meshes, particle definitions | **Yes** | Though a particle still needs a URP material to be drawn with. |

URP and HDRP are **mutually exclusive** render pipelines — a project is one or the other, and
this one is URP. An HDRP shader in a URP project does not degrade gracefully: it fails to
compile, and Unity substitutes its magenta error shader. There is no import setting that
changes this.

The second reason is harder: **HDRP has no mobile support at all.** iOS and Android are this
game's primary target (`CLAUDE.md`, first paragraph), so switching the project to HDRP to use
the pack whole is not a trade-off, it is the end of the platform.

So the pack lends the portal its **look**. It cannot lend it its **shape**.

## 2. What "the shape" means, and why it is not negotiable

`Shaders/Portal.shader` derives the opening from a normalised radial field - exactly 1.0 on the
boundary - whose edge is chewed away by two layers of noise, gated so that a closed portal draws
no lit pixel at all. The shape is the owner's call and has changed once already (a torn rectangle
became a torn oval); what does not change is that it comes from one signed field, so the rim, the
view and the outer spill all stay derivable from a single number.
Those terms are what make the effect a hole in a wall rather than a picture of one, and
`check_ui_and_portal.sh` enforces each of them.

The adopted artwork therefore reaches **colour and heat only**. In the fragment shader it sits
inside `#ifdef _PORTAL_TEXTURED`, after the silhouette has been computed, and touches exactly
two locals:

```hlsl
energy = lerp(energy, energy * art * 2.0, _TexInfluence);
hot    = saturate(lerp(hot, hot * mask, _TexInfluence));
```

A guard fails the build if that block ever assigns `box`, `oval`, `fit`, `gate`, `alpha`,
`open`, `ragged`, `rd` or `r`. At `_TexInfluence = 0` the portal is the procedural one, pixel for pixel.

## 3. Cost when it is not used

`_PORTAL_TEXTURED` is a `shader_feature_local_fragment`. With it undefined the compiler removes
both samplers, so a project that never adopts a pack pays nothing for the slots existing. A
guard checks that every `SAMPLE_TEXTURE2D` of the artwork sits inside the `#ifdef` — sampling
two textures per portal pixel per frame on a phone to multiply by an influence of zero is a
cost paid for a result identical to not sampling at all.

The keyword is switched from `PortalSurface.PushArtwork()`, which runs from `PushStyle()` and
never per frame: a keyword change is a material variant switch.

## 3b. What the pack does on import, measured

Importing *Portal Effect: HDRP* into this URP project on Unity 6000.5.10f1 produced exactly two
kinds of failure. They are recorded here because they look alarming, only one of them blocks
anything, and both were re-diagnosed from scratch once already.

**Two C# errors — these block everything.**

```
Assets/Knife/Portal HDRP/Scripts/PortalTransition.cs(72,34):
  error CS0619: 'Object.GetInstanceID()' is obsolete: 'Use GetEntityId instead.'
```

`CS0619` is an error, not a warning, so `Assembly-CSharp` does not build — and a project that
does not compile cannot enter play mode at all, however healthy the rest of it is. Unity's
script updater fixes `SimpleTransient.cs` when consent is given but cannot fix this one.

**Thirty-odd shader errors — these block nothing, and they are the whole argument.**

```
Shader error in 'Knife/PortalView': Couldn't open include file
  'Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl'
```

The same failure for `Knife/PortalView`, `Knife/Distortion`, `Knife/Portal Border`,
`Knife/Particle` and `Knife/Portal Alpha HDRP`, across every pass each of them declares. HDRP's
shader library is not in a URP project, so none of these shaders compile, and a shader that does
not compile is drawn magenta. This is section 1 as an error log rather than as a claim.

### The order that works

1. **Delete every script in the pack.** Not one folder - the pack ships two, and the second
   one uses types from the first, so removing only `Portal HDRP/Scripts/` turns two `CS0619`
   errors into four `CS0246` "type could not be found" errors out of
   `Portal HDRP/Demo with VFX/Scripts/` and the project still does not build. Select the pack
   folder, search `t:Script`, delete everything it finds. No script in the pack is used: the
   adapter reads materials and textures, and this project's portal has its own controller.
   The shader errors remain as console noise and stop nothing.
2. **Adopt the pack** (section 4) — *before* deleting anything else.
3. **Then delete the rest of `Assets/Knife/`.** The artwork is copied into the project by then.

Step 2 must come before step 3. The adapter learns a texture's role from the slot the pack's own
material binds it to, and a material resolves its property names **through its shader** — a
shader that fails to compile still declares its `Properties` block, but a shader that has been
deleted declares nothing. Delete the shaders first and the scan finds no bindings at all, which
looks identical to a pack that binds no textures.

## 4. Adopting a pack

**Catch If You Can → Portal → Adopt Purchased Portal Pack.**

1. Type the pack's folder. The tool scans **exactly that path** — it appends no suffix, searches
   no parent, and the report header names the path actually read. (A previous tool in this
   repository silently appended `/interior` and reported on a folder nobody asked about.)
2. **Scan** is read-only. It reports every texture the pack's materials bind, what slot each is
   bound as, and which material binds it.
3. **Adopt** copies the chosen images into `Assets/CatchIfYouCan/Resources/Portal/`, writes
   `MAT_Portal.mat`, and sets `usePurchasedArtwork` / `energyTexture` / `maskTexture` on every
   `LobbyPortal` in the open scenes. **Save the scene afterwards** or the style change is lost.

### Classification is by binding, never by filename

A texture's role comes from the slot the **pack's own material** binds it to — `_MainTex`,
`_BaseColorMap`, `_EmissiveColorMap` and friends mean energy; `_OpacityMask`, `_MaskMap`,
`_DissolveMap` and friends mean mask. `glow_02.png` is a guess; a binding to
`_EmissiveColorMap` is the pack telling you what the image is for. Classifying pack content by
its name or its shape is how a mirror once became a door in this repository.

### Nothing is written into the pack

Every file the adapter produces lands under `Assets/CatchIfYouCan/`. The purchased folder is
opened read-only.

## 5. Why the images are copied rather than referenced

`Assets/Knife/` is gitignored — it is a purchased Asset Store pack, and its licence does not
cover redistributing it in a repository.

A material referencing a texture *where the pack lies* would therefore resolve on exactly one
machine and be a missing texture on every other one, including CI. That is `CLAUDE.md`
mistake 15 — a file correct in the repository and absent on the machine — and
`check_asset_references.sh` exists because of it. Copying the two images the game actually uses
into the project makes them ordinary tracked assets, and the pack becomes optional again.

## 6. What is NOT adopted, and would have to be built

- **The pack's particle systems.** Their materials are HDRP. `PortalEffects` already builds
  sparks, streaks and wisps procedurally under URP; a pack's particle *textures* could be
  adopted the same way the energy texture is, but nothing does that today.
- **The pack's meshes.** The portal surface is a quad on purpose: the destination is sampled in
  **screen space**, and a screen-space sample wants a quad and a rectangular render texture.
  A pack's disc mesh would change the silhouette, which section 2 forbids.
- **The pack's scripts.** They drive HDRP materials and a different portal technique. This
  project's portal already does render-to-texture with oblique near-plane clipping, seamless
  crossing and frustum and distance culling — see `check_ui_and_portal.sh`.

## 7. Status

**NOT TESTED.** No Unity Editor is available where this was written. The shader change, the
adapter and the guards are checked by `check_ui_and_portal.sh` and by the offline typecheck
harness; none of that is a substitute for opening the project.
