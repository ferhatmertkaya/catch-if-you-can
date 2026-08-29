# External Asset Import Guide

Third-party CC0 packs are **already bundled** in this project under `Assets/External/`.

## Bundled layout

```
Assets/External/
  Kenney/
    FurnitureKit/Models/     (120 FBX furniture pieces)
    MiniDungeon/Models/      (walls, door, characters)
  Quaternius/
    Monsters/                (Demon, Orc, BlueDemon, CreepCreature — rigged)
```

## One-click integration (Unity Editor)

1. Open the project in **Unity 6.5 (6000.5.10f1)**
2. Run **Catch If You Can → Integrate External Assets**  
   (or **Setup Project**, which includes integration)
3. Run **Catch If You Can → Validator**
4. Play scene `03_Investigation`

This generates:

- Prop prefabs + `PropDefinition` assets
- Door prefab with `InteractiveDoor`
- 10 rigged ghost prefabs + animator controllers
- `InvestigationContentCatalog` in Resources

Full usage guide (German): **`/ASSETS_NUTZUNG.md`**

## Replacement targets

| Project placeholder | Bundled replacement |
|---------------------|---------------------|
| Runtime box rooms | Kenney Mini Dungeon walls/floors (manual room prefabs) |
| Primitive furniture | Kenney Furniture Kit via PropSpawner |
| Capsule ghost | Quaternius rigged models via GhostDefinition.Prefab |
| Solid color materials | Kenney vertex-color materials on FBX |
| Procedural audio | Optional — see `AUDIO_ASSET_REQUIREMENTS.md` |

## Adding more assets

1. Download a license-compatible pack
2. Import under `Assets/External/...`
3. Extend `PropDefinitionFactory` and/or `GhostVisualCatalog`
4. Re-run **Integrate External Assets**
5. Document in `/THIRD_PARTY_ASSETS.md`

## Mobile optimization checklist

- Architecture textures ≤ 2048
- Props ≤ 1024
- Small props ≤ 512
- ASTC compression on Android/iOS
- Mesh Read/Write off when unused
- Primitive/box colliders preferred
- LOD on large meshes
- Limit realtime lights / shadow casters
