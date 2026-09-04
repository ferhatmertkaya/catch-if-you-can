# Third-Party Assets

V1 of **CATCH IF YOU CAN** includes redistributed CC0 asset packs for furniture, environment pieces, and rigged ghost/monster models.

| Asset Name | Source | URL | License | Author | Where Used |
|------------|--------|-----|---------|--------|------------|
| Unity Engine | Unity Technologies | https://unity.com | Unity Editor / Runtime license | Unity Technologies | Entire project |
| Universal RP | Unity Package Manager | `com.unity.render-pipelines.universal` | Unity Package license | Unity Technologies | Rendering |
| Input System | Unity Package Manager | `com.unity.inputsystem` | Unity Package license | Unity Technologies | Input |
| AI Navigation | Unity Package Manager | `com.unity.ai.navigation` | Unity Package license | Unity Technologies | Ghost / player pathing |
| uGUI / TextMeshPro | Unity Package Manager | `com.unity.ugui` / TMP | Unity Package license | Unity Technologies | UI |
| Kenney Mini Dungeon | OpenGameArt / Kenney.nl | https://kenney.nl/assets/mini-dungeon | CC0 (verify on source page) | Kenney | Ghost humanoid and orc meshes only — the house interior half of this kit was removed |
| Quaternius Ultimate Monsters (subset) | Quaternius / descent-3d-assets mirror | https://quaternius.com/packs/ultimatemonsters.html | CC0 (verify on source page) | Quaternius | Rigged ghost prefabs (Orc, Demon, BlueDemon, CreepCreature) |

## Imported model files (bundled)

- `Assets/External/Kenney/MiniDungeon/Models/character-human.fbx`, `character-orc.fbx` — the
  two ghost meshes. The other 19 models of that kit, and the whole Furniture Kit, were removed
  with the rest of the Kenney house interior; a purchased modular pack replaces them.
- `Assets/External/Quaternius/Monsters/*` — Demon.gltf, Orc.gltf, BlueDemon.gltf, CreepCreature.glb

When you import additional packs, append a row here with:

- Asset Name
- Source
- URL
- License (as stated on the download page)
- Author
- Where used in this project

Do not list assets you have not actually imported.

See also: `ASSETS_NUTZUNG.md` (German usage guide).
