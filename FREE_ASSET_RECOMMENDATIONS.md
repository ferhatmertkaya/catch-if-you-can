# Free Asset Recommendations

Sources worth checking when a placeholder needs replacing in **CATCH IF YOU CAN**.

Every asset must still pass the art-direction and evaluation criteria in
[ASSET_USAGE.md](ASSET_USAGE.md). Free and license-compatible is the floor, not
the bar: a pack that does not fit the realistic Victorian horror look does not
go into a production environment, however good the price is.

**Verify each licence on its own source page before shipping a commercial
build. Do not invent licence claims.**

---

## Already in the repository

| Content | Status |
|---------|--------|
| Quaternius rigged monsters (4 models) | `Assets/External/Quaternius/Monsters/` — every ghost visual |
| Editor integration (`Integrate External Assets`) | Builds ghost prefabs, animator controllers and the content catalog |
| Primitive room fallback meshes | Runtime, fenced to Editor and development builds |
| URP runtime materials | `RuntimeMaterialFactory` |
| Custom shaders | Ghost Dissolve, UV Evidence, Spectral Grid, UI Slime, Electronic Glitch |
| Procedural house / ghost / equipment / mission data factories | Included |
| UI built at runtime | `RuntimeUIFactory` |

---

## Sources worth searching

### Poly Haven

| Asset type | URL | Suggested use |
|------------|-----|---------------|
| Furniture and props | https://polyhaven.com/models | Hero props, detailed furniture |
| PBR materials | https://polyhaven.com/textures | Walls, wood, concrete, fabric |
| HDRIs | https://polyhaven.com/hdris | Night exterior lighting only; low-res on mobile |

Poly Haven assets are typically CC0 — confirm per asset. This is the best fit
for the project's look: photogrammetry and realistic PBR.

### AmbientCG

| Asset type | URL | Suggested use |
|------------|-----|---------------|
| PBR materials | https://ambientcg.com | Plaster, wallpaper, floorboards, tile |

CC0. Useful for the surface materials the modular interior needs.

### Quaternius

| Asset type | URL | Suggested use |
|------------|-----|---------------|
| Rigged creatures | https://quaternius.com | Ghost silhouettes |

CC0. Already the source of every ghost mesh in the project. Stylised, so it
works for entities seen briefly in the dark and not for furniture.

### Unity Asset Store

Only packs that are **currently free** and license-compatible for the release.
Read the licence text, not the price tag.

---

## Import Checklist

1. Judge it against the art direction first — this is the step that gets skipped.
2. Enable ASTC compression for Android and iOS.
3. Disable mesh Read/Write unless something actually reads the mesh.
4. Add LOD Groups for large or repeated pieces.
5. Prefer primitive colliders.
6. Texture budget: 2048 architecture, 1024 props, 512 small props.
7. Record the licence in [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md).
8. Run `Scripts/check_asset_references.sh` — large binaries need a git-lfs rule.

---

## Brand Safety

Do **not** import assets that recreate:

- another ghost-hunting game's UI, logos, ghost models, map layouts or equipment silhouettes
- copyrighted audio without a licence

CIYC visual identity: black, white and grey with ectoplasm green `#57FF68` as
an accent, over realistic Victorian interiors.
