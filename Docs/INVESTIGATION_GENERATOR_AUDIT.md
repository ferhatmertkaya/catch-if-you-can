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
