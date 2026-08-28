# Asset-Nutzung — CATCH IF YOU CAN (Vollintegration)

Alle gebündelten CC0-Assets werden **automatisch** eingebunden — nicht nur eine Auswahl.

## Enthaltene Roh-Assets

| Pack | Anzahl | Ordner |
|------|--------|--------|
| Kenney Furniture Kit | **120 FBX** | `Assets/External/Kenney/FurnitureKit/Models/` |
| Kenney Mini Dungeon | **21 FBX** | `Assets/External/Kenney/MiniDungeon/Models/` |
| Quaternius Monster | **4 Modelle** (GLTF/GLB) | `Assets/External/Quaternius/Monsters/` |

---

## Ein-Klick: ALLES einbauen

In Unity 6.3 LTS:

```
Catch If You Can → Setup Project
```

oder nur Assets neu bauen:

```
Catch If You Can → Integrate External Assets
```

Optional vorher fehlende Dateien laden:

```
Catch If You Can → Download Missing External Assets
```

(lädt `CreepCreature.glb` automatisch von GitHub)

### Was dabei gebaut wird

| Output | Anzahl | Pfad |
|--------|--------|------|
| **Möbel-Prop-Prefabs** | ~130+ | `Prefabs/Props/Kenney/` |
| **PropDefinitions** | ~130+ | `ScriptableObjects/Props/` |
| **Raum-Prefabs (alle 15 Typen)** | 15 | `Prefabs/Rooms/Kenney/` |
| **RoomDefinitions** | 15 | `ScriptableObjects/Rooms/` |
| **Geister-Prefabs (10 Typen)** | 10 | `Prefabs/Ghost/Rigged/` |
| **Monster-Showcase-Prefabs** | 6+ | `Prefabs/Ghost/AllMonsters/` |
| **Tür-Prefab** | 1 | `Prefabs/Interactables/Door_Kenney.prefab` |
| **Content-Catalog** | 1 | `Resources/CatchIfYouCan/InvestigationContentCatalog.asset` |

---

## Im Spiel sichtbar

Nach Integration + Play in `03_Investigation`:

- **Echte Kenney-Räume** (Boden + Wände statt Würfel) für alle 15 Raumkategorien
- **Alle Möbel** passend zum Raumtyp (Küche, Bad, Schlafzimmer, …)
- **Dungeon-Requisiten** in Keller/Garage/Dachboden (Fässer, Truhen, Banner, …)
- **Rigged Geister** mit Idle/Walk/Run/Roar/Punch
- **Kenney-Tür** mit `InteractiveDoor`

---

## Geister-Modelle

| Geist | Modell |
|-------|--------|
| THE WANDERER, THE KNOCKER | Quaternius Orc |
| THE WHISPER, THE HOLLOW | Quaternius Demon |
| THE WATCHER, THE SHADEBORN | Blue Demon |
| THE MIMICER | Kenney Human |
| THE STATIC | Kenney Orc |
| THE CRAWLER, THE WEEPING ONE | Creep Creature |

Zusätzlich: alle Modelle als Showcase unter `Prefabs/Ghost/AllMonsters/`.

---

## Eigenes Asset hinzufügen

1. FBX/GLB nach `Assets/External/...` legen
2. Bei Möbeln: Dateiname bestimmt Raum-Tags automatisch (z.B. `kitchen*` → Kitchen)
3. **Integrate External Assets** erneut ausführen

---

## ZIP erstellen (Mac)

```bash
cd /pfad/zum/projekt
zip -r ~/Desktop/CATCH_IF_YOU_CAN_v4_FullAssets.zip CatchIfYouCan \
  -x "CatchIfYouCan/.git/*" -x "CatchIfYouCan/Library/*" -x "CatchIfYouCan/Temp/*"
```

Danach in Unity: **Setup Project** → **Play**.
