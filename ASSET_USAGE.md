# Asset Usage — CATCH IF YOU CAN

CATCH IF YOU CAN uses a curated asset pipeline built around a realistic, dark
Victorian / paranormal horror look. Assets are chosen one at a time and judged
against that look; nothing is included merely because it is free and available.

---

## Art Direction

Production assets should match:

- realistic or semi-realistic horror
- Victorian / abandoned residential interiors
- dark paranormal atmosphere
- physically believable proportions
- mobile-conscious geometry and materials
- consistent PBR materials under URP

**Assets that visually conflict with this direction are not integrated into
production environments.** This is why the Kenney Furniture Kit and Mini
Dungeon kit were removed: the stylised, flat-shaded look could not be
reconciled with the realistic direction. That decision is permanent unless
explicitly revisited.

---

## External Assets

External assets are integrated **selectively**. No pack is adopted wholesale,
and being present in the repository is not the same as being production art.

Each asset is evaluated for:

| Criterion | Why |
|---|---|
| Visual compatibility | The single most common reason to reject a pack. |
| License | Must be compatible with a commercial release. |
| Polygon count | Mobile is the primary target. |
| Texture resolution | Memory budget, not visual ambition. |
| Material compatibility | Must survive as URP; HDRP-only shaders do not cross. |
| Mobile performance | Draw calls, overdraw, real-time light cost. |
| Collider requirements | A prop the player can walk through is not finished. |
| LOD requirements | Large or repeated meshes need them. |

### Currently in the repository

| Source | What | Where | Status |
|---|---|---|---|
| Quaternius | 4 rigged monster meshes (`Orc`, `Demon`, `BlueDemon`, `CreepCreature`) | `Assets/External/Quaternius/Monsters/` | **In use** — every ghost visual maps to one of these via `GhostVisualCatalog`. CC0. |
| Renderpeople (Nathan) | The player character rig and its animation | `Assets/CatchIfYouCan/Art/Characters/Nathan/` | **In use.** |
| Meshy (AI-generated) | Individual props, e.g. the cork bulletin board | `Assets/CatchIfYouCan/Art/Environment/Props/` | **In use**, per-prop. |
| HQ Modular House | Interior architecture modules | Not in the repository — see below | **In use on licensed machines.** |
| Purchased portal pack | Portal artwork | Not in the repository — see below | Optional; lends the portal its look, never its shape. |

Licences and attribution live in `THIRD_PARTY_ASSETS.md` and, for audio,
`AUDIO_ASSET_LICENSES.md`.

### Packs that are deliberately NOT in the repository

Some purchased packs are per-seat licensed and are `.gitignore`d. On a machine
without them, every GUID a scene names in those packs resolves to nothing —
which, from inside the repository, looks identical to a genuinely broken
reference.

`Scripts/write_vendor_manifest.sh`, run on a machine that has the packs, records
which GUIDs belong to them. `Scripts/check_asset_references.sh` then treats that
absence as a named, expected state and still fails on a GUID the manifest does
not cover. Do not "fix" a missing HQ Modular House reference by deleting it.

---

## Project Asset Structure

Production art lives under `Assets/CatchIfYouCan/Art/`:

```
Assets/CatchIfYouCan/Art/
    Characters/
        Ghosts/<AssetName>/{Materials,Models,Textures}
        Nathan/
    Environment/
        Doors/<AssetName>/{Materials,Models,Textures}
        InteractiveRoom/
        MainMenuCorridor/
        Props/<AssetName>/{Materials,Models,Prefabs,Textures}
    Equipment/
        DOTS/                      generated projector cookie masks
    Icons/
    Particles/
```

Third-party packs that ship with the repository live under `Assets/External/`.

Runtime-loadable content lives under `Assets/CatchIfYouCan/Resources/` —
everything in that folder is pulled into every build whether or not anything
references it, so only put things there that the game loads by path.

---

## Naming Convention

Models and materials:

```
CIYC_<AssetName>.fbx
CIYC_<AssetName>.prefab
MAT_<AssetName>.mat
```

Textures use one of two prefixes. `T_` is the newer convention and is preferred
for new art; `CIYC_` is used by older assets and is not being renamed, because
renaming a texture breaks the material that points at it for no visual gain:

```
T_<AssetName>_BaseColor.png
T_<AssetName>_Normal.png
T_<AssetName>_Metallic.png
T_<AssetName>_Roughness.png
T_<AssetName>_Emission.png
T_<AssetName>_MetallicSmoothness.png     packed, where the material wants it
```

Vendor assets keep their original filenames (the Nathan rig, Meshy exports).
Renaming a vendor file makes the next pack update a manual merge.

---

## Adding an Asset

1. Check it against the art direction and the evaluation table above.
2. Put it in its own folder under the matching `Art/` category.
3. Rename model, material and textures to the convention (vendor packs excepted).
4. Set import settings: correct scale, mobile-appropriate max texture size,
   normal maps marked as normal maps, masks marked linear.
5. Build a URP material. Never fall back to a built-in shader — `Shader.Find("Standard")`
   resolves everywhere and draws solid magenta under URP.
6. Add colliders. A prop without one is scenery the player walks through.
7. Record the licence in `THIRD_PARTY_ASSETS.md`.
8. Run `Scripts/check_asset_references.sh` before committing. Large binaries
   need a git-lfs rule; the repository is already close to GitHub's free 1 GiB
   LFS allowance, and `Scripts/lfs_debt.txt` records what is outside LFS.

---

## Unity

| | |
|---|---|
| Unity | 6.5 — `6000.5.10f1` |
| Render pipeline | Universal Render Pipeline (URP) 17.5.0, Forward+ |
| Scripting backend | IL2CPP |
| Primary targets | iOS, Android |
