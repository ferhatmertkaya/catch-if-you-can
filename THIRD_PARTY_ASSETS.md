# Third-Party Assets

**CATCH IF YOU CAN** redistributes a small number of CC0 assets. Every row below is a pack
that is actually in this repository and actually used.

| Asset Name | Source | URL | License | Author | Where Used |
|------------|--------|-----|---------|--------|------------|
| Unity Engine | Unity Technologies | https://unity.com | Unity Editor / Runtime license | Unity Technologies | Entire project |
| Universal RP | Unity Package Manager | `com.unity.render-pipelines.universal` | Unity Package license | Unity Technologies | Rendering |
| Input System | Unity Package Manager | `com.unity.inputsystem` | Unity Package license | Unity Technologies | Input |
| AI Navigation | Unity Package Manager | `com.unity.ai.navigation` | Unity Package license | Unity Technologies | Ghost / player pathing |
| uGUI / TextMeshPro | Unity Package Manager | `com.unity.ugui` / TMP | Unity Package license | Unity Technologies | UI |
| Quaternius Ultimate Monsters (subset) | Quaternius / descent-3d-assets mirror | https://quaternius.com/packs/ultimatemonsters.html | CC0 (verify on source page) | Quaternius | Every ghost visual — Orc, Demon, BlueDemon, CreepCreature |

## Imported model files

- `Assets/External/Quaternius/Monsters/*` — Demon.gltf, Orc.gltf, BlueDemon.gltf, CreepCreature.glb

## Removed

The Kenney Furniture Kit and Mini Dungeon kit are no longer part of this project. Their
stylised look could not be reconciled with the realistic Victorian horror art direction, so
they were removed in full — including the last two Mini Dungeon character meshes, which had
survived as the visuals for THE MIMICER and THE STATIC and are now Quaternius monsters like
every other ghost. The decision is permanent unless explicitly revisited, and
`Scripts/check_hq_environment.sh` fails if any file names a Kenney path again.

When you import additional packs, append a row here with:

- Asset Name
- Source
- URL
- License (as stated on the download page)
- Author
- Where used in this project

Do not list assets you have not actually imported.

See also: `ASSET_USAGE.md` — art direction, asset structure, naming and the criteria a
pack has to meet before it is integrated.
