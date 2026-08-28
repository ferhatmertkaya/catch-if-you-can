# Free Asset Recommendations

Recommended free / CC0 sources to replace primitive placeholders in **CATCH IF YOU CAN**.

Verify each license on the source page before shipping a commercial build. Do not invent license claims.

## Included Automatically

| Content | Status |
|---------|--------|
| **Kenney Furniture Kit (120 FBX)** | Bundled under `Assets/External/Kenney/FurnitureKit/` |
| **Kenney Mini Dungeon (21 FBX)** | Bundled under `Assets/External/Kenney/MiniDungeon/` |
| **Quaternius rigged monsters (4 models)** | Bundled under `Assets/External/Quaternius/Monsters/` |
| Editor integration (`Integrate External Assets`) | Included — builds prefabs + PropDefinitions + ghost rigs |
| Primitive room / van fallback meshes | Included (runtime + PrefabFactory) |
| URP-compatible runtime materials | Included (`RuntimeMaterialFactory`) |
| Custom shaders (Ghost Dissolve, UV Evidence, Spectral Grid, UI Slime, Electronic Glitch) | Included |
| Procedural house / ghost / equipment / mission data factories | Included |
| UI built at runtime | Included (`RuntimeUIFactory`) |

## Manual Downloads (Recommended)

### Kenney.nl

| Asset | URL | Suggested use | Notes |
|-------|-----|---------------|-------|
| Furniture Kit | https://kenney.nl/assets/furniture-kit | Beds, tables, chairs, cabinets | CC0 — verify on page |
| Modular Buildings | https://kenney.nl/assets | Exterior walls / modular blocks | Check exact pack + license |
| UI Pack | https://kenney.nl/assets | Optional icon bases (recolor to neon green) | Do not keep stock Kenney look; restyle |

### Poly Haven

| Asset type | URL | Suggested use |
|------------|-----|---------------|
| Furniture / props | https://polyhaven.com/models | Hero props, detailed furniture |
| PBR materials | https://polyhaven.com/textures | Walls, wood, concrete, fabric |
| HDRIs | https://polyhaven.com/hdris | Night exterior lighting only (mobile: low-res) |

Poly Haven assets are typically CC0 — confirm per asset.

### Unity Asset Store

Only use packs that are **currently free** and license-compatible for your release. Prefer Kenney / Poly Haven for clarity.

## Import Mapping

| Placeholder | Replace with |
|-------------|--------------|
| `Prefabs/Props/Bed` | Kenney / Poly Haven bed |
| `Prefabs/Props/Wardrobe` | Wardrobe / cabinet |
| `Prefabs/Props/Table`, `Chair` | Dining / desk sets |
| `Prefabs/Equipment/*` | Custom low-poly gear (keep unique CIYC silhouette) |
| Wall/floor materials | Poly Haven PBR (max 2048 arch / 1024 props / 512 small) |

After import:

1. Enable ASTC on Android/iOS
2. Disable Read/Write on meshes when unused
3. Add LOD Groups for large pieces
4. Prefer primitive colliders
5. Re-run **Catch If You Can → Validator**

## Brand Safety

Do **not** import assets that recreate:

- Other ghost-hunting games' UI, logos, ghost models, map layouts, or equipment silhouettes
- Copyrighted audio without license

CIYC visual identity: dark anthracite, ectoplasm green `#57FF68`, teal highlights, research-device UI.
