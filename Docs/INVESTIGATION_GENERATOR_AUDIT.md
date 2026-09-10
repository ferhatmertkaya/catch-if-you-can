# Der Hausgenerator, wie er heute ist — Bestandsaufnahme

**Stand:** Commit `2e94d77`. **Es wurde nichts am Generator geändert.** Die einzige Änderung in
diesem Durchgang ist ein `.gitignore`-Eintrag für ein gekauftes Paket.

**GEPRÜFT** heißt: im Code nachgelesen, Datei und Zeile genannt.
**NICHT PRÜFBAR** heißt: es hängt an Assets, die nicht im Repository liegen, oder an einem
Laufzeitzustand, für den hier kein Unity läuft. Ich sage jedes Mal dazu, was fehlt.

---

## 1. Die Pipeline von Seed zu GameObject — GEPRÜFT

```
SeedManager.CurrentSeed                     SeedManager.cs:31   (KnownGoodSeed = 424242)
      ↓
InvestigationBootstrap                      InvestigationBootstrap.cs   (1096 Zeilen, Einstieg)
      ↓  legt Runtime-Objekte an, parented VOR AddComponent
ProceduralHouseGenerator.Instantiate        ProceduralHouseGenerator.cs (784 Zeilen)
      ↓
HouseLayoutGraph  →  Stage A, engine-frei    HouseLayoutGraph.cs:98
      ↓  MapDefinition.HouseDefault + ContentSnapshot
   pro Raum:
      ├─ ModularRoomBuilder.Build(...)       ProceduralHouseGenerator.cs:286-289   ← Produktionsweg
      └─ PrimitiveRoomFactory.CreateRoom     ProceduralHouseGenerator.cs:329-333   ← nur im Editor/Dev-Build
      ↓
PropSpawner.TrySpawn                        PropSpawner.cs:51
      ↓
NavMeshRuntimeBuilder                       NavMeshRuntimeBuilder.cs
```

Die Weiche in Zeile 286: **ist `modularInteriorCatalog` gesetzt, läuft der modulare Weg.** Ist er
null oder schlägt der Bau fehl, fällt es hinter `#if UNITY_EDITOR || DEVELOPMENT_BUILD` auf die
Primitiv-Fabrik zurück — im ausgelieferten Spiel gibt es diesen Rückfall bewusst nicht.

`modularInteriorCatalog` wird in `ProceduralHouseGenerator.cs:766-767` aus
`InvestigationContentCatalog.ModularInterior` nachgezogen. Das Feld ist heute gesetzt
(guid `fe6e6059…`), also läuft bei dir **der modulare Weg**.

---

## 2. Die Möbel — die Ursache ist GEPRÜFT und eindeutig

`FURN_TABLE_UtilityRoom_7_7` entsteht so:

| Schritt | Datei:Zeile |
|---|---|
| Der Name wird zusammengesetzt aus `PropDefinitionId_Kategorie_RaumId_InstanzId` | `PropSpawner.cs:82` |
| `FURN_TABLE` ist ein **Fallback-Archetyp**, 1.40 × 0.80 × 0.90 m, **letzter Parameter `null`** | `Deterministic/ContentSnapshot.cs:240` |
| Der Fallback greift, wenn Räume **und** Props leer sind | `ContentSnapshotFactory.cs:60-61` |
| Ohne `definition.Prefab` wird ein **Cube** gebaut | `PropSpawner.cs:70-74` |
| Der Cube selbst | `PrimitiveRoomFactory.cs:499-513` |

Und die Ursache dahinter ist eine Zeile im Katalog:

```
Assets/CatchIfYouCan/Resources/CatchIfYouCan/InvestigationContentCatalog.asset
  PropDefinitions: []
  RoomDefinitions: []
  DoorPrefab: {fileID: 0}
```

**Es gibt genau vier Möbel im ganzen Spiel**, und keines hat ein Prefab:

| Archetyp | Art | Maße | Prefab |
|---|---|---|---|
| `PROP_CRATE` | Prop | 0.70 × 0.70 × 0.70 m | — |
| `PROP_LAMP` | Prop | 0.40 × 1.40 × 0.40 m | — |
| `FURN_SHELF` | Möbel | 1.60 × 2.00 × 0.50 m | — |
| `FURN_TABLE` | Möbel | 1.40 × 0.80 × 0.90 m | — |

Das ist kein Bug im Spawner. Der Spawner tut genau das Richtige. **Der Katalog ist leer**, weil
der Kenney-Bestand gelöscht wurde (CLAUDE.md Fehler 14) und nichts nachgekommen ist. „Die Räume
sind zu leer" und „die Möbel sind Kisten" sind **dasselbe Problem**: vier Archetypen, null Prefabs.

**Vollständige Liste der Laufzeit-Primitiven im Generierungspfad:**

| Objekt | Datei:Zeile | Typ | soll sein |
|---|---|---|---|
| jedes Prop/Möbelstück | `PrimitiveRoomFactory.cs:511` | Cube | echte Möbel-Prefabs |
| Boden | `PrimitiveRoomFactory.cs:229` | Cube | nur Rückfall |
| Decke | `PrimitiveRoomFactory.cs:239` | Cube | nur Rückfall |
| Wand | `PrimitiveRoomFactory.cs:283` | Cube | nur Rückfall |
| Türsturz | `PrimitiveRoomFactory.cs:324` | Cube | nur Rückfall |
| Ersatzboden ohne Welt | `InvestigationBootstrap.cs:532` | Plane | Diagnose |
| Van | `VanBuilder.cs:190` | Cube | Van-Prefab |

**Ersatz-Prefabs, die es schon gibt:** keine für Möbel. Die einzigen echten Möbel-Meshes im
Repository sind vier LFS-Objekte unter `Art/Environment/Props/` (Kerzenhalter, Standuhr,
Wählscheibentelefon, Tisch) und die Kerzen/Tür-Assets — **NICHT PRÜFBAR**, ob das HQ-Paket
brauchbare Möbel enthält, weil es gitignoriert ist. Das sagt dir `Alle HQ-Bauteile prüfen`.

---

## 3. Die Türhöhe — GEPRÜFT, mit einer offenen Frage

Zwei Wege, und sie sind unterschiedlich.

### Der Primitiv-Weg rechnet richtig

`PrimitiveRoomFactory.cs:324` setzt den Sturz auf
`wallCenter + up * (DoorHeight + headerHeight/2 − roomSize.y/2)`. Mit `wallCenter.y = size.y/2`
kürzt sich das zu **`DoorHeight + headerHeight/2`** = 2.20 + 0.40 = 2.60 m bei 3 m Raumhöhe. Der
Sturz spannt 2.20…3.00. **Korrekt.**

### Der modulare Weg setzt nach dem Mesh, nicht nach dem Pivot

`ModularRoomBuilder.cs:212-216` will die Tür bei `y = DoorHeight/2 = 1.30 m` in der Wand-Lokalen,
und die Wand steht ab `y = 0`. `AddInsert` (`ModularRoomBuilder.cs:280-283`) rechnet dann:

```csharp
go.transform.localPosition = target - placed.center;
```

Das ist die **richtige** Rechnung — und der Kommentar darüber beschreibt exakt dein Symptom als
bereits behoben: *„ein Türrahmen irgendwo an der Decke und ein Fenster darüber"*, verursacht
durch Pivots, die in diesem Paket 13 bis 40 m neben ihrer eigenen Geometrie liegen.

**Es gibt genau zwei Wege, auf denen das trotzdem hoch landet**, und beide loggen:

| Fall | Zeile | Logzeile |
|---|---|---|
| `TryMeasureInSpace` liefert nichts → Platzierung über den Pivot | `ModularRoomBuilder.cs:293-296` | *„keine sichtbare Geometrie messbar … Pivot bis zu 40 m daneben"* |
| `KeepOnlyInsertParts` behält 0 Teile → Einsatz wird abgeschaltet | `ModularRoomBuilder.cs:250-262` | *„kein Teil trägt eines der Materialien …"* |

**Welcher von beiden bei dir zutrifft, kann ich hier nicht entscheiden** — dafür brauche ich die
Unity-Konsole nach einer Generierung. Die beiden Meldungen unterscheiden sich eindeutig.

Die dritte Möglichkeit: `OrientUpright` dreht falsch. Die Pack-Meshes tragen ihre Höhe auf **Z**,
nicht auf Y, und der Aufrichter misst das am instanziierten Objekt. Schlägt er fehl, liegt die
Tür — sie steht nicht zu hoch. Dein Symptom passt besser zu Fall 1.

---

## 4. Maße und Koordinaten — GEPRÜFT

| | Wert | Quelle |
|---|---|---|
| Fertigfußboden | **y = 0** in der Raum-Lokalen | `PrimitiveRoomFactory.cs:231` (Boden mittig auf −0.10) |
| Raum | **6 × 3 × 6 m** | `PrimitiveRoomFactory.cs:10`, `MapDefinition.cs:46` |
| Lichte Höhe | **3.00 m** | dieselbe Konstante |
| Wandstärke | 0.20 m primitiv / **0.15 m modular** | `PrimitiveRoomFactory.cs:11`, `ModularRoomBuilder.cs:33` |
| Boden-/Deckenstärke modular | 0.20 m | `ModularRoomBuilder.cs:34-35` |
| Tür primitiv | 1.20 × **2.20 m** | `PrimitiveRoomFactory.cs:12-13` |
| Tür modular | 1.25 × **2.60 m** | `ModularRoomBuilder.cs:49-50` |
| Fenster modular | 2.05 × 0.90 m, Brüstung **1.55 m** | `ModularRoomBuilder.cs:59-61` |
| Spieler | 1.86 m Kapsel, 1.68 m Auge | `PlayerFactory.cs` |

**Zwei Befunde daraus:**

1. **Die beiden Wege sind sich über die Türhöhe nicht einig: 2.20 gegen 2.60 m.** Wer den Rückfall
   im Editor sieht und den modularen Weg im Build, sieht zwei verschiedene Häuser.
2. **Das Fenster spannt 1.55…2.45 m unter einer 3.00-m-Decke** — 0.55 m Luft darüber. Für einen
   Wohnraum ist die Brüstung mit 1.55 m sehr hoch; typisch sind 0.85…1.00 m. Das ist keine
   Fehlfunktion, aber es erklärt „Fenster sitzen zu weit oben".

Gemessen wird durchgängig über **Renderer-Bounds im Zielraum** (`TryMeasureInSpace`), nicht über
Transform-Positionen — das ist richtig und ist genau die Lehre aus CLAUDE.md Fehler 12.

---

## 5. Raumtypen — GEPRÜFT, und die Antwort ist unbequem

**15 Kategorien** (`Deterministic/RoomCategory.cs`): Entrance, Hallway, LivingRoom, Kitchen,
DiningRoom, Bedroom, Bathroom, Storage, Laundry, Office, KidsRoom, Garage, Basement, Attic,
UtilityRoom.

**Die Einrichtungsregeln pro Typ, die dein Auftrag abfragt, gibt es nicht.** Was existiert:

- Jede Kategorie bekommt **denselben** Archetyp `ARCH_<Kategorie>` mit **derselben** Größe
  6 × 3 × 6 m und Gewicht 1 (`ContentSnapshot.cs:226-234`).
- `PropArchetype.AllowedCategories` ist bei allen vier Fallback-Props **leer**, und leer heißt
  „passt überall" (`ContentSnapshot.cs:45-46`).

**Ergebnis: es gibt keine raumtypspezifische Möblierung.** Ein Bad, ein Kinderzimmer und ein
Heizungsraum bekommen aus demselben Topf von vier Objekten. Keine Mindest-/Höchstgröße pro Typ,
keine Pflichtmöbel, keine Deko-Regeln, keine Lichtregeln pro Typ.

---

## 6. Magenta — vier Ursachen, drei davon GEPRÜFT

**1. Das gerade importierte Hivemind-Paket. Wahrscheinlich die Hauptursache.**
Der Ordner heißt `Assets/Hivemind/FantasyCemetery/HDRP(Default)/`. **HDRP-Materialien finden in
einem URP-Projekt ihren Shader nicht und werden magenta gezeichnet.** Es ist jetzt gitignoriert
(Commit `2e94d77`), damit der Fehler nicht verteilt wird — auf deiner Platte bleibt es liegen.

**2. `InvestigationBootstrap.cs:532` — GEPRÜFT.**
```csharp
var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
```
Es folgt **keine Materialzuweisung**. `CreatePrimitive` bringt Unitys eingebautes
Standardmaterial mit, und das ist ein Built-in-Pipeline-Shader → magenta unter URP. Dieser Boden
erscheint, wenn keine Welt generiert wurde.

**3. `VanBuilder.cs:190` und `ApartmentShell.cs:203` — GEPRÜFT.**
Beide weisen nur zu, `if (material != null)`. Ist es null, bleibt Unitys Standardmaterial → magenta.

**4. Was NICHT die Ursache ist:** `PrimitiveRoomFactory` und `ProceduralHouseGenerator` behandeln
den Fall korrekt — ein Primitive ohne Material bekommt seinen **Renderer abgeschaltet** statt
magenta zu zeigen (`check_hq_environment` prüft das). Ein Raum, der dort scheitert, wird
**unsichtbar**, nicht magenta.

---

## 7. „Victorian Street" — der minimale sichere Ort

Der benutzersichtbare Kartenname lebt in **`Scripts/Missions/MissionDefinition.cs:16`**:

```csharp
public string MapName = "Suburban Home";
```

Das ist ein Feld einer ScriptableObject-Definition, kein Bezeichner. **Genau ein Wort ändern**,
nichts umbenennen.

**Nicht anfassen:** `MissionTheme.SuburbanHouse` (Zeile 7) ist ein Enum-Wert, den `MissionRuntime`
und `check_vertical_slice` benutzen; `MapDefinition.mapDefinitionId = "HOUSE_DEFAULT_A"` geht in
den Layout-Hash ein — es umzubenennen **bricht die Determinismus-Prüfung und jeden Golden Seed**.

---

## 8. Dateien, die eine Verbesserung anfassen müsste

| Datei | wofür |
|---|---|
| `Resources/CatchIfYouCan/InvestigationContentCatalog.asset` | die leere Prop-Liste — die Wurzel des Möbelproblems |
| `Deterministic/ContentSnapshot.cs` | die vier Fallback-Archetypen; **geht in den Layout-Hash ein** |
| `PropSpawner.cs` | Boden-relative Platzierung, Rotation, Überlappung |
| `ModularRoomBuilder.cs` | Fensterbrüstung, Einsatz-Diagnose |
| `PrimitiveRoomFactory.cs` | Türhöhe 2.20 → 2.60 angleichen |
| `InvestigationBootstrap.cs:532` | Material für den Ersatzboden |
| `VanBuilder.cs`, `ApartmentShell.cs` | Renderer aus statt magenta |
| `Missions/MissionDefinition.cs:16` | „Victorian Street" |
| Neu: Möbel-Prefabs + `PropDefinition`-Assets | das eigentliche Problem |

---

## 9. Vorschlag in Phasen — NICHT umgesetzt

| Phase | Inhalt | Risiko |
|---|---|---|
| **A** | Magenta: Material für den Ersatzboden, Renderer-Aus in Van und ApartmentShell. Hivemind-Paket klären | keins |
| **B** | Türhöhe der beiden Wege angleichen, Fensterbrüstung auf 0.90 m | **Layout-Hash unberührt**, reine Stage B |
| **C** | Konsolen-Log nach einer Generierung lesen → entscheiden, welcher der zwei Einsatz-Pfade fehlschlägt, und den beheben | keins |
| **D** | Möbel-Prefabs beschaffen und `PropDefinition`-Assets schreiben, Katalog füllen | **hoch: ändert den Content-Snapshot und damit den Layout-Hash** |
| **E** | Raumtypspezifische Regeln: `AllowedCategories`, Pflichtmöbel, Mindestabstände | **hoch: Stage A** |
| **F** | Boden-relative Platzierung, Wand-Abstand, Wegfreiheit | mittel |

## 10. Risiken

**Determinismus.** `ContentSnapshot` und `MapDefinition` gehen in den **Layout-Hash** ein.
Phase D und E ändern damit **jeden gespeicherten Seed** — dasselbe Saatgut baut ein anderes Haus.
`Scripts/check_determinism.sh` (148 Prüfungen) und die Golden Seeds schlagen an. Das ist kein
Grund, es nicht zu tun; es ist ein Grund, es **einmal** zu tun und die Golden Seeds danach neu zu
erzeugen.

**Navigation.** `NavMeshRuntimeBuilder` baut aus **Render-Meshes**, nicht aus Collidern, und
sammelt nach Tag `Environment` oder Namen mit `Floor`/`Wall`. Echte Möbel-Prefabs bringen viele
Meshes mit — ohne Filter wächst die NavMesh-Bauzeit deutlich, und ein Möbelstück ohne Tag wird
zum Loch im Netz.

**Multiplayer.** Die Generierung läuft auf jeder Maschine aus demselben Seed. Alles, was in
Phase D/E den Hash ändert, muss auf **allen** Clients gleichzeitig ankommen — ein Client mit
altem Katalog baut ein anderes Haus als der Host.

## 11. Was ich nicht sagen kann

- **Ob der modulare oder der primitive Weg bei dir lief.** Braucht die Konsole.
- **Welcher der beiden Einsatz-Pfade die Tür hochsetzt.** Braucht die Konsole.
- **Was das HQ-Paket an Möbeln enthält.** Braucht `Alle HQ-Bauteile prüfen` bei dir.
- **Welche Objekte konkret magenta sind.** Braucht die Auswahl im Editor.

---

# Phase A–C — umgesetzt

**Stand:** die Befunde oben sind Stand des Audits. Was hier steht, ist umgesetzt.
Der Möbel- und Raumtyp-Teil (Phasen D–F) ist **nicht** angefasst.

## Geänderte Dateien

| Datei | was |
|---|---|
| `Scripts/Missions/MissionDefinition.cs` | `MapName` → „Victorian Street" |
| `Scripts/Art/PrimitiveSurface.cs` | **neu** — die eine Regel gegen Magenta |
| `Scripts/Procedural/ProceduralHouseGenerator.cs` | benutzt sie |
| `Scripts/Procedural/PrimitiveRoomFactory.cs` | benutzt sie; Türmaße von der modularen Seite |
| `Scripts/Procedural/InvestigationBootstrap.cs` | Ersatzboden bekommt ein Material |
| `Scripts/Procedural/VanBuilder.cs` | benutzt sie |
| `Scripts/Environment/ApartmentShell.cs` | benutzt sie |
| `Scripts/Procedural/ModularRoomBuilder.cs` | Brüstung 0.90; Einsatz instrumentiert und verweigert |
| `Scripts/check_hq_environment.sh` | 127 → 136 Prüfungen |

## Konstanten, alt gegen neu

| | vorher | jetzt |
|---|---|---|
| Türbreite, primitiv | 1.20 m | **1.25 m** (aus `ModularRoomBuilder`) |
| Türhöhe, primitiv | 2.20 m | **2.60 m** (aus `ModularRoomBuilder`) |
| Türbreite/-höhe, modular | 1.25 / 2.60 m | unverändert |
| Fensterbrüstung | 1.55 m | **0.90 m** |
| Fensterhöhe | 0.90 m | unverändert |
| Fenster-Oberkante | 2.45 m | **1.80 m** |
| Lichte Höhe | 3.00 m | unverändert |

Der primitive Weg liest die Türmaße jetzt **aus** `ModularRoomBuilder` statt eigene zu führen.
Zwei Zahlen für eine Öffnung waren die Ursache dafür, dass Editor und Build verschiedene Häuser
zeigten.

## Magenta: jede bekannte Stelle

`Art.PrimitiveSurface.Apply(go, material, expected)` ist **die eine Regel**: Material drauf, oder
**Renderer aus** plus eine Fehlerzeile mit Objekt, erwartetem Material und Grund. Der Collider
bleibt — ein unsichtbarer Boden trägt den Spieler noch.

| Stelle | vorher | jetzt |
|---|---|---|
| `ProceduralHouseGenerator` | eigene, korrekte Fassung | benutzt die Regel |
| `PrimitiveRoomFactory` | eigene, korrekte Fassung | benutzt die Regel |
| `InvestigationBootstrap.BuildEmptyFloor` | **keine Zuweisung → magenta** | URP-Lit oder Renderer aus |
| `VanBuilder.CreateCube` | `if (material != null)` → magenta | benutzt die Regel |
| `ApartmentShell.Box` | `if (material != null)` → magenta | benutzt die Regel |

Der Guard prüft die Regel **einmal** und dann jede der fünf Stellen einzeln — „die meisten
benutzen sie" war genau der Zustand vorher.

**Nicht angefasst:** kein globaler Konvertierungslauf, kein Material geändert, die Tapeten-Logik
unberührt, das Hivemind-Paket nicht importiert.

## Phase C: was der Einsatz jetzt meldet

Pro Tür und Fenster eine Zeile `[CIYC][House][Insert]` mit: Rolle, Prefab, behaltene Renderer,
ob gemessen werden konnte, Mesh-Größe, Mesh-Mittelpunkt vor der Korrektur, Pivot-Abstand,
Ziel, lokale und globale Endposition, Unter- und Oberkante, Soll-Unterkante und Soll-Oberkante.

**Der Pivot-Rückfall ist entfernt.** Konnte nicht gemessen werden, wird der Einsatz **abgelehnt**
(`REFUSED`, Objekt abgeschaltet, Prefab genannt) statt über einen Pivot gesetzt zu werden, der in
diesem Paket bis zu 40 m neben der eigenen Geometrie liegt. Die Öffnung bleibt dann leer — sichtbar
falsch, aber am richtigen Ort.

Zwei harte Prüfungen nach dem Setzen, gegen die **nachgemessene** Lage:
Unterkante ≈ Soll (Tür 0.00 m, Fenster 0.90 m) mit 5 cm Toleranz, und Oberkante ≤ 3.00 m.
Jede Verletzung ist eine Fehlerzeile mit beiden Zahlen.

## Determinismus

**Keine Datei unter `Scripts/Procedural/Deterministic/` wurde angefasst** — 0 geänderte Dateien
im Kern. `MapDefinition`, `ContentSnapshot`, `HouseLayoutGraph`, `SeedManager`, `LayoutHash` und
`GenerationVersion` sind unverändert. Alles Geänderte ist Stage B: es entscheidet, wie ein Raum
aussieht, nicht welcher Raum wo liegt. `check_determinism.sh` läuft mit 148 von 148.

`MissionTheme.SuburbanHouse`, `mapDefinitionId = "HOUSE_DEFAULT_A"` und die Golden Seeds sind
**nicht** angefasst.

## Testablauf in Unity — NICHT GETESTET, das ist deiner

1. `03_Investigation` öffnen, Play, ein Haus generieren lassen.
2. Konsole nach `[CIYC][House][Insert]` filtern. Erwartet pro Tür:
   `measured=YES`, `bottomLocalY≈0.000`, `topLocalY≈2.600`.
   Steht dort `measured=NO ... REFUSED`, ist die Ursache der hohen Türen gefunden — schick mir
   die Zeile mit dem `prefab=`-Namen.
3. Nach `[CIYC][WorldMaterial]` filtern. Jede Zeile ist ein Objekt, das jetzt **unsichtbar** statt
   magenta ist, mit dem erwarteten Material im Klartext.
4. Fenster ansehen: Brüstung auf Hüfthöhe, Oberkante knapp über Augenhöhe.
5. Tapeten vergleichen — an der Wand-Generierung wurde nichts geändert.
6. Missionsauswahl: der erste Ort heißt „Victorian Street".

---

# Nachtrag: was der erste echte Lauf gezeigt hat

Der Log aus dem laufenden Spiel beantwortet die offene Frage aus Phase C — und die Antwort ist
**keiner der beiden Fehlerpfade**, die ich vorhergesagt hatte. Die Messung funktioniert:

```
role=Door   prefab=1 kept=2 measured=YES meshSize=(3.991, 4.054, 0.650) pivotOffset=2.05m
            target=(0.000, 1.300, 0.000) -> bottomLocalY=-0.727 topLocalY=3.327
role=Window prefab=7 kept=2 measured=YES meshSize=(3.764, 4.116, 0.402) pivotOffset=0.00m
            target=(0.000, 1.350, 0.000) -> bottomLocalY=-0.708 topLocalY=3.408
```

Die Instrumentierung hat also getan, wozu sie da war, und die Diagnose liegt eine Stufe höher:
`meshSize` ist **rund 4 m × 4 m — ein ganzes Vendor-Wandteil**, kein Türblatt (1.25 × 2.60) und
kein Fensterflügel (2.05 × 0.90). Ein 4.05 m hohes Objekt auf 1.30 m zu zentrieren **muss** die
Oberkante auf 3.33 m und die Unterkante 0.73 m unter den Boden legen. Die beiden Fehlerzeilen
waren die richtige Rechnung am falschen Objekt.

Ursache: `KeepOnlyInsertParts` hat die Wandschale mitbehalten, weil sie eines der genannten
Materialien trägt. `kept=2` sieht nach einer sauberen Auswahl aus und war Schale + Blatt. Das ist
auch die zweite Beobachtung aus demselben Lauf — „als Türrahmen nimmt er einmal
`WallWithDoorway_West`, einmal `Door_Insert`": die erzeugte Wand trägt ihre Öffnung samt Laibung
bereits, und das Insert hat eine zweite, wandgroße darübergestellt.

**Die Größe entscheidet jetzt.** Vor dem Setzen wird die gemessene Ausdehnung gegen die Öffnung
geprüft, die das Teil füllen soll, mit einer gemeinsamen Zugabe (`InsertOverhangAllowance`,
0.40 m) für Rahmen und Laibung. Was darüber liegt, ist ein Wandteil und wird mit genannten Zahlen
abgelehnt; die erzeugte Öffnung bleibt so, wie die Wand sie gebaut hat — eine saubere, richtig
bemaßte Tür. Materialnamen zu raten wäre Fehler 3 in neuen Kleidern; das Paket liegt hier nicht
vor, und eine Messung, die 4 m sagt, braucht keine Vermutung.

## „der fällt runter"

Der Lauf war ein **Direktstart** von `03_Investigation` (`InvestigationBootstrap.Start` →
`BootstrapSequence` im Stack), nicht der Portalweg. Auf diesem Weg spawnt `SpawnPlayer` am
`PlayerSpawn` des Vans:

```
entryAnchor=PlayerSpawn  playerAt=(0.0, 0.1, -9.6)     ... später: worldPosition=(-0.23, -9.57, -10.06)
```

`y = 0.1` ist die Oberkante des Van-Bodens (Slab 0.08 m). Der Van steht bei `VanAnchor (0, 0, -14)`,
sein Boden reicht bis `z = -10.15` — und **dieses Projekt hat kein Terrain**. Der Van ist eine
Insel im Nichts: hineingesetzt, hinausgelaufen, gefallen. Der Portalweg ist dem nie begegnet, weil
er auf `MissionEntryAnchor` landet, einem Punkt, der gegen echte Bodenkollision im Eingangsraum
**nachgewiesen** ist.

Der Direktstart benutzt jetzt denselben Anker. Beide Wege kommen damit an derselben Stelle an —
was den Direktstart überhaupt erst zu einem brauchbaren Test des Portalwegs macht. Der Van-Spawn
bleibt als Rückfall für einen Lauf ohne Haus (`generateWorld = false`) und wird dabei **gemeldet**,
denn „ich falle durch die Welt" und „es gab kein Haus zum Draufstehen" sind derselbe Bildschirm.

`ArrivalPoint` selbst ist **nicht** angefasst: das Portal bindet seine Ansicht daran, und
`check_ui_and_portal.sh` prüft die Definition.

## Der Audio-Layer war nie installiert

Hunderte `NullReferenceException` pro Sekunde aus `VanAudioController.UpdateRainOnMetal`
(`VanAudioController.cs:58`) waren nicht der Fehler, sondern sein Auspuff.

`GameManager.BeginMission` feuert `InvestigationStarted`, **während die Mission aufgelöst wird** —
in `PrepareWorld`, also bevor der Van gebaut, das Haus erzeugt und der Spieler gespawnt ist.
`InvestigationAudioBootstrap.HandleInvestigationStarted` lief dort, fand nichts, installierte
nichts gegen nichts, und setzte `_installed`. Der eigentliche Aufruf aus
`InvestigationBootstrap.InstallAudio` ein paar Sekunden später kehrte damit in seiner ersten Zeile
zurück: keine Schritte, kein Herzschlag, keine Sanity-Audio, keine Raumtöne, keine Türgeräusche,
keine Geisteraudio — und ein `VanAudioController`, dem ein `null`-Van übergeben wurde und der
seine nie erzeugten Quellen jeden Frame ansprach.

Zwei Änderungen, beide klein:

* Der Nachinstallierer **steht ab**, solange die Welt, gegen die er installieren würde, nicht
  existiert (kein Spieler, kein Van, kein Geist), und lässt `_installed` in Ruhe — der
  maßgebliche Aufruf zählt weiter.
* `VanAudioController` merkt sich, ob `SetupSources` gelaufen ist, und läuft ohne Quellen gar
  nicht erst. Ein Van, den es nicht gibt, wird einmal gemeldet statt 60-mal pro Sekunde geworfen.

## Was NICHT angefasst wurde

Portal, Lobby, Player, CameraRoot, CharacterController, Inventar, Equipment, Multiplayer,
Ghost-AI, Evidence, UI, die deterministische Stage A, `MissionTheme.SuburbanHouse`,
`HOUSE_DEFAULT_A`, die Golden Seeds, Phasen D–F.

## Testablauf in Unity — NICHT GETESTET, das ist deiner

1. `03_Investigation` öffnen, Play. Erwartet: der Spieler steht **im Eingangsraum**, nicht im Van.
   Die Zeile `[CIYC][Portal][Handoff] mission entry anchor at ...` nennt den Punkt.
2. Konsole nach `[CIYC][House][Insert]` filtern. Erwartet pro Tür/Fenster jetzt
   `-> REFUSED: ... das ist ein ganzes Wandteil` mit `measured=` und `opening=`. Die Türöffnung ist
   dann **einfach** gerahmt (nur `WallWithDoorway_*`), nicht doppelt.
3. Konsole nach `NullReferenceException` filtern. Erwartet: keine aus `VanAudioController`.
4. Konsole nach `[CIYC][Audio]` filtern. Steht dort „stood down", ist die Reihenfolge wie
   beschrieben und der maßgebliche Aufruf hat installiert.

---

# Nachtrag 2: Decken, Dielen, Öffnungen, Türen

Auftrag: Decken, Boden-UV, offene Wandlöcher, überdimensionierte Platzhalter-Würfel und
interaktive Türen. Audit zuerst, dann bauen.

## Was der Audit ergeben hat

### CEILING_01 — Ursache

Die Decke wird **einmal** pro Raum gebaut (`ModularRoomBuilder.BuildCeiling`, ein Aufruf, eine
`BoxCollider`-freie Fläche bei `y = size.y`). Es gibt **keine** doppelte Deckenlage und keine
Vendor-Decke darüber — das Paket enthält *null* Deckenteile, das ist gemessen und steht in
`HQ_MODULAR_MIGRATION.md`.

Die Ursache ist das **Material**. `CeilingSurface` zeigt auf ein Paket-Material, und seine
`AuthoredAcrossMetres` von 2.6415 × 2.6415 wurde nicht an ihm gemessen, sondern aus der
Wandmessung per Texel-Parität **abgeleitet**. Das ist eine vernünftige Schätzung und war auf dem
Bildschirm eine falsche Antwort: eine Decke, die aussieht wie ein Fußboden, in einer Kachelgröße,
die zu nichts gehört. Die Wandmessung selbst ist echt (Prefab 5, 3.95 × 4.00 m); Boden und Decke
waren geraten.

### FLOOR_UV_01 — Ursache

Nicht das Material und nicht die Geometrie. Die UVs der erzeugten Flächen laufen in **Metern**
(`StructuralMeshFactory.UvUnitsPerMetre = 1`), also ist die Dielenbreite ausschließlich die
Kachelung des Materials — und die kam aus derselben abgeleiteten 2.6415, die auch die Decke
bekommen hat. Eine Kachel alle 2.64 m auf einer Parketttextur mit vielen Dielen ergibt
handbreite Dielen.

Weil die UVs in Metern liegen, ist die Dielenbreite **schon jetzt** in einem 3-m-Raum und in
einem 6-m-Raum dieselbe. Diese Forderung war bereits erfüllt; falsch war nur die Zahl.

### OPENING_01 / WINDOW_01 — Ursache, und sie ist nicht die, nach der es aussieht

Zwei getrennte Dinge, die dasselbe Bild ergeben.

1. **Es war nichts da, was die Öffnung füllt.** Die Wand schneidet ein echtes Loch
   (`WallWithOpening`), und das einzige, was hineingesetzt wurde, war das Vendor-Insert — das
   seit dem letzten Durchgang zu Recht abgelehnt wird, weil es ein ganzes 4-m-Wandteil ist. Also
   blieb das Loch leer. Es gab keine CIYC-eigene Tür, keinen Rahmen und keine Scheibe.

2. **Was man durch das Loch sah, war Unitys Standardhimmel.** `03_Investigation.unity` hat
   `m_SkyboxMaterial: {fileID: 0}` — **kein Skybox-Material**. Ohne eines zeichnet Unity ihren
   eingebauten Prozedur-Himmel, und der ist **taghellblau**. Das ist das „plain blue rectangle"
   wörtlich: nicht ein Debug-Material, nicht ein fehlendes Fensterglas, sondern der Himmel.
   `MAT_Skybox_HauntedNight` existiert seit Aufgabe 26 und war in keiner Szene zugewiesen.

### PLACEHOLDER_01 — **nicht bestimmbar aus dem Repository, deshalb jetzt gemessen**

`InvestigationContentCatalog.asset` hat `PropDefinitions: []` und `RoomDefinitions: []`. Der
Möbel-Fallback, der früher Würfel erzeugte, spawnt also **gar nichts**. Die einzigen Primitive,
die der Generator baut, sind der Lichtschalter (0.12 × 0.18 × 0.04 m) und der Sicherungskasten
(0.35 × 0.50 × 0.12 m) — beide zu klein, um das zu sein, was auf dem Screenshot steht.

Aus dem Code allein lässt sich der Block nicht benennen, und ihn zu raten wäre genau der Fehler,
den der Auftrag verbietet („do not scale the huge cube until it looks acceptable"). Deshalb gibt
es jetzt `HouseContentReport`: nach jeder Generierung wird **jeder Renderer in jedem Raum**
gemessen und alles, was größer als ein Kleiderschrank ist (2.60 m in irgendeiner Achse), mit
Name, Raum, Größe, Position, Mesh, Material, Shader, `enabled` und **Hierarchie-Pfad** genannt.
Struktur ist nach *Namen* ausgenommen, nicht nach Größe — ein Boden *ist* sechs Meter breit.

Gemessen wird im **Eigenraum** des Objekts mal `lossyScale`, nie `Renderer.bounds`: das ist eine
weltachsenparallele Box und bei einem gedrehten Objekt größer als das Objekt (CLAUDE.md
Fehler 12).

## Was gebaut wurde

### Decke

`ModularInteriorCatalog` hat jetzt pro Fläche eine `SurfaceTuning`: ein **Ersatzmaterial** und
eine **Kachelgröße in Metern**, beides optional, beides im Inspector sichtbar. Ein Material dort
ersetzt das Paket-Material vollständig; eine Kachelgröße dort überstimmt die gemessene Dichte.

* **Finales Deckenmaterial:** `MAT_Room_Ceiling`
  (`Assets/CatchIfYouCan/Art/Environment/InteractiveRoom/Materials/`, Textur
  `CIYC_Room_Ceiling_BaseColor.png`) — projekteigener viktorianischer Putz, URP Lit, seit
  Aufgabe 12 im Projekt und bisher nur im InteractiveRoom benutzt.
* **Finale Kachelgröße Decke:** **1.42 m pro Kachel.** Das ist genau die Dichte, in der das
  Material authored wurde (4.24 Kacheln über 6 m = 1.415 m), nur jetzt in Metern statt in
  Raumbreiten ausgedrückt.

### Boden

Material **unverändert** — der Auftrag sagt, es ist richtig.

* **Finale Kachelgröße Boden:** **4.00 m pro Kachel** (vorher abgeleitete 2.6415). Das Muster
  wird damit um Faktor 1.51 größer.

Diese Zahl ist ein **Startwert, kein Messergebnis**: das HQ-Paket liegt hier nicht vor, seine
Texturen sind nicht lesbar, und eine Dielenbreite ist ohnehin eine Designentscheidung. Sie steht
deshalb offen im Inspector (`ModularInteriorCatalog > Floor Tuning > Metres Per Tile`) — größer =
breitere Dielen.

Die Umrechnung skaliert **jede** Map des Materials um denselben Faktor, relativ zur Kachelung der
Basismap. Ein Detail-Normal, das absichtlich achtmal feiner läuft, bleibt achtmal feiner. Eine
absolute Zahl in jede Map zu schreiben hätte das plattgemacht.

### Öffnungen

Jede Öffnung hat jetzt eine **Rolle** und wird als solche geloggt (`[CIYC][House][Opening]`).
Drei gibt es und keine vierte: DOORWAY, WINDOW, oder die Wand ist geschlossen und macht gar keine
Öffnungsarbeit.

* **Türlaibung** (`BuildDoorLining`): zwei Pfosten und ein Sturz aus dem Trim-Material, 3 cm
  Überdeckung, 2 cm vor der Wandfläche — **pro Wand**, denn eine Tür zwischen zwei Räumen hat in
  jedem Raum eine Laibung.
* **Fenster** (`BuildWindowAssembly`): vier Rahmenhölzer plus eine Scheibe.
  * **Brüstung 0.90 m, Oberkante 1.80 m** — die Konstanten `WindowSill` und `WindowHeight`, aus
    denen auch das Loch geschnitten wird. Loch und Füllung kommen aus derselben Zahl, sie können
    also nicht auseinanderlaufen.
  * **Glasmaterial:** `MAT_Room_Glass` (projekteigen, URP).
  * **Kein Collider am Glas.** Die Fensterwand trägt bereits **eine** Box über ihre ganze Breite
    — ein Fenster ist kein Durchgang — also wäre das eine zweite Antwort auf eine schon
    beantwortete Frage.

### Türen

* **Blatt:** erzeugte Geometrie, `StructuralMeshFactory.Panel`, **1.19 × 2.56 × 0.045 m** in
  einer Öffnung von 1.25 × 2.60. Material: `MAT_VictorianHauntedDoor`
  (`Art/Environment/Doors/CIYC_VictorianHauntedDoor/Materials/`), URP Lit mit BaseColor, Normal,
  Roughness und Metallic.
* **Unterkante 0.00 m, Oberkante 2.56 m**, lichte Höhe 3.00 m — beides wird zur Laufzeit
  **nachgemessen** und geloggt, samt einer Fehlerzeile, wenn das Blatt die Decke schneidet.
* **Ein Blatt pro Verbindung**, gebaut in `ProceduralHouseGenerator.CreateDoorAt` aus der
  Türliste des Layouts. Nicht pro Wand: eine Tür zwischen zwei Räumen ist *ein* Loch, um das
  *beide* Räume eine Wand bauen, und ein Blatt pro Wand wären zwei Blätter, die durcheinander
  schwingen.
* **Das Scharnier ist ein Wrapper**, kein Pivot am Blatt: ein Kindobjekt auf dem linken Pfosten,
  das Blatt hängt um seine halbe Breite versetzt daran. Das Blatt um seine eigene Mitte zu drehen
  hätte die Hälfte davon in die Wand geschwenkt.
* **Der Collider sitzt am Blatt** und schwingt mit. Geschlossen versperrt er die Schwelle, offen
  steht er an der Wand. Ein Collider an der Wurzel wäre die unsichtbare Wand in jeder Schwelle —
  genau der alte Fehler.

**Warum das keine Rückkehr des verbotenen Primitiv-Türblatts ist:** die alte Notlösung baute zwei
Würfel **ohne Material**, was unter URP eine magenta Fläche im Rahmen und einen unsichtbaren
Blocker davor bedeutet. Die Regel war nie „keine erzeugte Tür", sondern „nichts Erfundenes ohne
Material". Ist `DoorLeafMaterial` leer, wird **gar nichts** gebaut und die Öffnung bleibt frei —
dieselbe Regel, nicht eine Ausnahme davon. `check_hq_environment.sh` prüft beide Hälften.

### Interaktion mit E

**Kein zweites Framework.** Der Weg existierte vollständig und war nur nicht angeschlossen:

```
MobileInputController      Input.GetKeyDown(KeyCode.E)   → InteractPressed
InteractionController      Raycast 2.75 m                → IInteractable am Treffer
InteractiveDoor            IInteractable                 → Interact() → SetOpen()
```

Gefehlt hat nur ein Objekt mit `InteractiveDoor` **und** einem Collider im Strahl. Beides bringt
das erzeugte Blatt jetzt mit. Die Mobile-Taste läuft durch dieselbe `InteractPressed`-Eigenschaft
und ist damit automatisch mit dabei.

`InteractiveDoor.Configure(Transform hinge, float openAngle)` ist eine **öffentliche Methode**,
keine Reflection in das private `hinge`-Feld (CLAUDE.md Fehler 4). Öffnungswinkel 92°.

### Himmel

`HouseLightingDirector.ApplyEnvironment` setzt jetzt `RenderSettings.skybox` auf
`MAT_Skybox_HauntedNight`. Dort und nicht in der Szenendatei, weil `RenderSettings` zur **aktiven**
Szene gehört — und während das Portal diese Welt vorbereitet, ist die Lobby aktiv. Genau diese
Einschränkung gilt für Ambient und Nebel in derselben Methode schon.

Für die Helligkeit ist das **neutral**: das Ambient ist `Flat` aus `NightAmbient` und kommt nicht
vom Himmel. Es ändert sich, was man durch eine Öffnung *sieht*, nicht wie viel Licht im Raum ist.

Der Resources-Pfad steht jetzt einmal in `Art/CiycSky` statt zweimal (CLAUDE.md Fehler 3).

### Kein stilles Magenta mehr in der Raumhülle

`ModularRoomBuilder.Piece` hatte ein `if (material != null)`, das ohne Material nichts tat — und
ein `MeshRenderer` ohne zugewiesenes Material ist nicht unsichtbar, URP zeichnet ihn mit dem
Fehler-Shader. Jetzt geht jede erzeugte Fläche durch `Art.PrimitiveSurface`: Material fehlt →
Renderer **aus** und genannt, Collider bleibt.

## Determinismus

`Scripts/Procedural/Deterministic/` unverändert, 0 geänderte Dateien. `check_determinism.sh`
148/148. `MissionTheme.SuburbanHouse`, `HOUSE_DEFAULT_A` und die Golden Seeds nicht angefasst.
Alles hier ist Stage B: wie ein Raum aussieht, nicht welcher Raum wo liegt.

## Performance

Kein Reimport, kein `AssetDatabase`-Scan, kein Reflection-Render. Die Rahmenhölzer benutzen
`StructuralMeshFactory.SolidWall`, das nach Maßen cached — vier Hölzer teilen sich im ganzen Haus
eine Handvoll Meshes. Drei Flächenmaterialien plus Trim für das ganze Haus, einmal aufgelöst.
Türblatt-Mesh ebenfalls gecached. `HouseContentReport` läuft **einmal** nach der Generierung.

## Geänderte Dateien

| Datei | Was |
|---|---|
| `Scripts/Procedural/StructuralMeshFactory.cs` | `Panel()` mit 0..1-UVs; `AddBoxNormalised` mit `finally` |
| `Scripts/Procedural/ModularRoomBuilder.cs` | Öffnungsrollen, Laibung, Fenster, `SurfaceTuning`, `ScaleAllMaps`, `Piece` ohne stilles Magenta |
| `Scripts/Procedural/ModularDoorFactory.cs` | **neu** — Blatt, Scharnier, Collider, Verweigerung |
| `Scripts/Procedural/HouseContentReport.cs` | **neu** — misst jeden Renderer, nennt jeden Übergroßen |
| `Scripts/Procedural/ProceduralHouseGenerator.cs` | erzeugte Tür statt leerer Öffnung; Bericht am Ende |
| `Scripts/Content/ModularInteriorCatalog.cs` | `SurfaceTuning`, Tür-, Trim- und Glasmaterial |
| `Scripts/Interaction/InteractiveDoor.cs` | `Configure(hinge, openAngle)` |
| `Scripts/Environment/HouseLightingDirector.cs` | Himmel beim Betreten |
| `Scripts/Art/CiycSky.cs` | **neu** — ein Pfad für den Nachthimmel |
| `Scripts/Art/LobbyExterior.cs` | benutzt diesen Pfad |
| `ScriptableObjects/Content/ModularInteriorCatalog.asset` | die Zahlen und Materialien oben |
| `Scripts/check_hq_environment.sh` | 139 → 160 Prüfungen |

## NICHT GETESTET — das ist deiner

Kein Unity hier. Nichts unten ist eine Behauptung über den Bildschirm.

1. `03_Investigation` öffnen, Play.
2. **Konsole nach `[CIYC][House][Content]` filtern.** Das ist die Antwort auf den großen dunklen
   Block. Steht dort `oversized=0`, ist er weg; steht dort mehr, nennt jede Zeile Name, Raum,
   Größe, Mesh, Material, Shader und Hierarchie-Pfad — **schick mir die Zeilen**, dann ist es
   eine Ein-Zeilen-Korrektur statt einer Vermutung.
3. **`[CIYC][House][Surface]`** — drei Zeilen: Wand, Boden, Decke, jede mit Materialnamen und
   Kachelgröße. Sind die Dielen immer noch zu klein, ist die Zahl
   `ModularInteriorCatalog > Floor Tuning > Metres Per Tile`; größer machen, neu generieren.
4. **`[CIYC][House][Opening]`** — pro Wand mit Loch eine Zeile. Keine Zeile bei einem sichtbaren
   Loch heißt: die Wand hält sich für geschlossen, und das ist Stage A, nicht Stage B.
5. **`[CIYC][House][Door]`** — pro Tür Blattmaß, Material, Unter- und Oberkante. Erwartet:
   `bottomY` auf Bodenhöhe, `topY` rund 2.56 m.
6. **Vor eine Tür stellen, E drücken.** Erwartet: Prompt „Open Door", Blatt schwingt ~92° um die
   Kante, offen kann man durch, geschlossen nicht.
7. **Nach oben schauen.** Erwartet: viktorianischer Putz, keine Dielen, eine Fläche.
8. **Durch ein Fenster schauen.** Erwartet: Nachthimmel-Panorama, kein Blau.
9. **`NullReferenceException` filtern.** Erwartet: keine.

Sollte etwas an der Tür 90° verdreht in der Wand stehen: das Blatt wird mit
`Quaternion.Euler(0, RotationIndex * 90, 0)` gesetzt, derselben Konvention, mit der
`ModularRoomBuilder` die Wände dreht (Nord/Süd unrotiert, Ost/West 90°). Die beiden *sollten*
übereinstimmen, verifiziert ist das nur im Code, nicht auf dem Bildschirm.
