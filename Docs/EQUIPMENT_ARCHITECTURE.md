# Equipment architecture

Status: normative for `Assets/CatchIfYouCan/Scripts/Equipment/**` and for anything
that holds, places or reads a piece of equipment.

Eleven items, one lifecycle, one grip contract, one place that decides whether an
observation is evidence.

---

## 1. The roster

`EquipmentIds.All` is the definitive list, in catalogue order — which is also the
index a compact network encoding would send:

```
flashlight  emf_detector  uv_light  thermometer  evp_recorder
parabolic_microphone  photo_camera  spectral_grid  video_camera
warding_relic  salt
```

Ids are stable strings because saves and a future wire format depend on them. They
are declared once, as constants, because they used to be retyped in the definition
factory, the runtime factory, the loadout, the player factory and the labs — and a
typo in any of them produced an id that resolved to nothing.

---

## 2. Who owns what

| Concern | Owner | Never |
|---|---|---|
| Which items exist, what they cost, what they can do | `EquipmentDefinition` / `EquipmentCatalog` | — |
| Which slot the player is holding | `PlayerInventory` | Anything else. It is the sole runtime slot authority. |
| What the player *brought on this run* | `EquipmentManager` | It must never regain equip, cycle, place or drop. That split is what made two systems each believe they knew what was in the player's hand. |
| Being carried: pickup, equip, holster, use, place, drop | `HeldEquipmentBase` | Not one implementation per item. |
| The arithmetic of laying an item on a hand | `EquipmentPresentation` | — |
| Where the arm and hand are | `PlayerBodyMotion` | Equipment must never rewrite pose maths or touch Nathan's bones. |
| How *this character's* hand is shaped | `CharacterRigProfile` | It must never gain per-item entries. That would make it a second equipment database. |
| How *this item* sits in any hand | `EquipmentGripProfile` | — |
| Whether an observation is evidence | `EvidenceManager` and the evidence boundary | An item pressing Use must never be able to grant evidence by itself. |

### The three offset stores, resolved

There were three descriptions of where a held object goes, and they did not agree
on what they were describing:

- `EquipmentDefinition.HandLocalPosition/Rotation` — a local transform on the
  anchor. **Deprecated.** Migrate with `EquipmentGripProfile.FromLegacyHandPose`,
  check the item once in the lab, then zero the fields.
- `HeldEquipmentBase`'s serialized offsets — in the hand's *measured* axes. These
  become the shared default, because they are the flashlight's, and the
  flashlight's grip is the only one in this project ever tuned against a real
  character.
- `CharacterRigProfile.GripPositionOffset/Rotation` — read by nothing at all.
  Now defined as the **character-wide** correction: the same for everything that
  character holds.

The split is by *what the offset is a property of*. Nathan's fist is a fact about
Nathan; where the torch sits in a fist is a fact about the torch.
`EquipmentPresentation` is the only thing that composes the two.

---

## 3. The lifecycle

One `EquipmentLifecycleState` instead of `IsEquipped`, `IsPlaced` and a device flag.
Those three booleans could express states that cannot exist — equipped *and* placed —
and could not express one that does: in a slot the player is not currently holding.
A torch in the bag and a torch on the floor were the same pair of falses.

```
World ──pickup──> Holstered <──holster── Equipped ──use──> Using
                       │                    │  │
                       └────select──────────┘  ├─begin──> PlacementPreview ──place──> Placed
                                               └─drop───> World            <──pickup──┘
```

Not every item reaches every state. `Definition.CanUse`, `CanPlace` and `CanDrop`
decide, and the lifecycle refuses the rest **with a reason**.

### Every verb returns a reason

`IHeldEquipment` is all `Try*` methods returning `EquipmentActionResult`. That is
not politeness — it is the networking seam. `NotAllowedByDefinition`, `NoBattery`,
`Blocked`, `NoValidSurface` and `NoInventorySpace` used to be the same
`return false`, so neither the lab nor a player could tell which had happened.

`EquipmentActionStatus.NoAuthority` is unused in single player and exists so a later
host-authoritative layer answers the same calls without any of the eleven
implementations changing. **V3 contains no netcode and must not.**

---

## 4. Content

Gameplay classes do not build their production visual identity out of primitives.
An item resolves a prefab through its definition; an item with no final art gets a
`PF_Equipment_<Item>` carrying a clearly identifiable `DEV_PLACEHOLDER` visual.
Swapping placeholder art for a final FBX changes a content reference, not code.

Only the flashlight has real art (`Resources/Props/CIYC_Flashlight.fbx`). The other
ten are placeholders and are **not production-ready**; anything that says otherwise
is wrong.

An unknown id logs an error and gets `DevPlaceholderEquipment` — an inert box. It
must never get a flashlight.

---

## 5. Evidence

> Equipment observes. Evidence validation decides truth.

An item produces an *observation*. Confirmation checks that the active ghost's
profile permits that `EvidenceType`, and applies dwell and cooldown where relevant.
One noisy frame is not evidence, and neither is pressing Use.

---

## 6. Enforcement

- `Scripts/check_equipment_catalog.sh` — 11 canonical ids declared and defined
  exactly once, catalog entries resolve, the runtime factory's declared id set
  matches its own switch cases, no `FlashlightEquipment`, no unknown-id flashlight,
  `EquipmentManager` still has no runtime authority, no built-in shader fallback,
  no netcode types. Runs in CI.
- `EquipmentCatalogValidator` — the half that needs real object references:
  missing icons, impossible battery configurations, ids with no runtime path.
- The guard's `UNMAPPED_ALLOWLIST` names ids that can currently only be
  placeholders. **It may only ever shrink.**
