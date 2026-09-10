# External Assets

Third-party packs that ship inside this repository live here. Purchased,
per-seat licensed packs do **not** — see `/ASSET_USAGE.md`.

## Current layout

```
Assets/External/
  Quaternius/
    Monsters/     Demon.gltf, Orc.gltf, BlueDemon.gltf, CreepCreature.glb (rigged, CC0)
```

That is all of it. The Kenney Furniture Kit and Mini Dungeon kit were removed:
their stylised look could not be reconciled with the realistic Victorian horror
direction. That decision is permanent unless explicitly revisited, and
`Scripts/check_hq_environment.sh` fails if anything names a Kenney path again.

## Integration

**Catch If You Can → 9. ENTWICKLER - DEBUG → Migration → Integrate External Assets**

What it still does:

- configures import settings on the Quaternius models
- builds the rigged ghost prefabs and their animator controllers
- builds the monster showcase prefabs
- wires the ghost definitions
- writes `InvestigationContentCatalog` into `Resources`

What it no longer does: prop prefabs, `PropDefinition` assets, room prefabs and
the door. Those were built from the furniture kit. The purchased modular pack
that replaces them is integrated through **2. HQ MODULAR HOUSE** instead.

Both this command and **Setup Project** are bulk operations that rewrite
project state, and both ask for confirmation before running.

## Adding a pack

1. Check it against the art direction and evaluation criteria in `/ASSET_USAGE.md`.
2. Import under `Assets/External/<Vendor>/<Pack>/`.
3. Extend `GhostVisualCatalog` (ghosts) or the modular interior catalog (architecture).
4. Record the licence in `/THIRD_PARTY_ASSETS.md`.
5. Run `Scripts/check_asset_references.sh` — large binaries need a git-lfs rule.

## Mobile optimisation checklist

- Architecture textures ≤ 2048, props ≤ 1024, small props ≤ 512
- ASTC compression on Android and iOS
- Mesh Read/Write off unless something actually reads the mesh
- Primitive and box colliders in preference to mesh colliders
- LODs on large or repeated meshes
- Keep real-time lights and shadow casters to a minimum
