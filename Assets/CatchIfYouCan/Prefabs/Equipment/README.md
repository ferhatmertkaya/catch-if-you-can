# Equipment prefabs

This folder is filled by **Catch If You Can > Equipment > Build Equipment Prefabs**
(`Assets/CatchIfYouCan/Editor/EquipmentAssetBuilder.cs`).

It writes:

- `PF_Equipment_Base.prefab` — the pickup trigger and `InteractivePickup`, and nothing about
  what an item does.
- `PF_Equipment_Flashlight.prefab` — a variant with `HeldFlashlight` on it. The only
  implemented item.
- `PF_Equipment_DEV_PLACEHOLDER_<Item>.prefab` — a variant per unimplemented item, carrying
  `DevPlaceholderEquipment`: a box that refuses to be used. They are named this way on
  purpose. The runtime factory used to hand every unimplemented id a working torch, and an
  unimplemented item that quietly works is one nobody ever finishes.

The prefabs are build products and are not checked in: a prefab is a graph of
cross-referencing YAML documents, and hand-writing one is how references get silently broken.
The definitions they are built from *are* checked in, under
`Assets/CatchIfYouCan/Definitions/Equipment`.
