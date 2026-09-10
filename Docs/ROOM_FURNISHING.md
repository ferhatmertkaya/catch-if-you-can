# Semantische Innenausstattung — Victorian Street

**Normativ für die Einrichtung.** Wer entscheidet, was in einem Raum steht, in welcher Reihenfolge,
und was auf keinen Fall zugestellt werden darf.

Für die Hülle — Wände, Boden, Decke, Türen, Fenster — gilt weiterhin `HQ_MODULAR_MIGRATION.md`
und `INVESTIGATION_GENERATOR_AUDIT.md`. Dieses Dokument fängt da an, wo der Raum fertig gebaut ist.

---

## 1. Die belegte Ursache der leeren Räume

Zwei Beobachtungen mit einer gemeinsamen Ursache: die Räume waren leer **und** enthielten dunkle
Blöcke.

```
ContentSnapshotFactory.Create(roomDefinitions, propDefinitions)
    if (rooms.Count == 0 && props.Count == 0)
        return ContentSnapshot.CreateFallback();          // <-- hier
```

`CreateFallback()` erfindet vier Archetypen — `FURN_SHELF` (1.6 × 2.0 × 0.5 m), `FURN_TABLE`
(1.4 × 0.8 × 0.9), `PROP_CRATE` (0.7³), `PROP_LAMP` (0.4 × 1.4 × 0.4). Stage A plant daraus
ordentlich Möbel und faltet die Platzierungen in den Layout-Hash. **Aus Sicht der Generierung gab
es also Möbel.**

Stage B fand für keine davon eine `PropDefinition`: beide `InvestigationContentCatalog`-Assets
haben `PropDefinitions: []` und `RoomDefinitions: []`, seit die Kenney-Inhalte entfernt wurden
(CLAUDE.md Fehler 14). `PropSpawner.TrySpawn` fiel deshalb auf

```csharp
Vector3 size = definition != null ? definition.BoundsSize : Vector3.one;
instance = PrimitiveRoomFactory.CreateFallbackProp(propName, size, null);
```

— und mit `definition == null` heißt das **1 × 1 × 1 Meter**, nicht einmal die geplante Größe, im
dunklen Trim-Material. Mehrere pro Raum. Aus einem Meter Entfernung im First-Person füllt einer
davon das Bild.

Ein Größenfilter hätte ihn nie gefangen: ein Kleiderschrank ist größer als ein Meter. Was ihn
verrät, ist sein **Unity-Primitivmesh** — ein eingerichteter Raum enthält keine Würfel.

**Behoben:** `CreateFallbackProp` ist entfernt, `PropSpawner` überspringt und zählt statt zu
erfinden, und `HouseContentReport` meldet jedes Primitivmesh unabhängig von seiner Größe.

---

## 2. Warum die Einrichtung Stage B ist

Den Katalog zu füllen wäre der naheliegende Weg gewesen — und hätte den Layout-Hash bewegt:
`ContentSnapshotFactory.Create` liefert dann einen echten Snapshot statt des Fallbacks, Stage A
plant andere Möbel, und **jeder bestehende Golden Seed erzeugt ein anderes Haus.**

Die Einrichtung sitzt deshalb komplett in Stage B, an genau derselben Grenze, an der auch die
Wandvariantenwahl steht: sie *liest* den Grundriss und kann ihn nicht ändern.

| | Stage A | Stage B |
|---|---|---|
| Welche Räume, wie groß, welche Wand trägt eine Tür | ✔ im Layout-Hash | liest nur |
| Welche Tapete, welches Wandmesh | — | aus der Raumidentität abgeleitet |
| **Welche Möbel, wo, wie gruppiert** | — | **`FurnishingRandom`, eigener Subseed** |

`FurnishingRandom.ForRoom(layoutSeed, roomId, purpose)` hasht mit `Fnv1a64` und läuft als
xorshift64\* weiter. Er berührt **keinen** `CiycRandom`-Strom — einen davon zu ziehen würde ihn
weitertreiben und damit in die Grundrisserzeugung zurückgreifen.

Er hängt an nichts, was zur Laufzeit anders sein kann: keine Instanz-IDs, keine
Dateisystemreihenfolge, kein `UnityEngine.Random`, keine Physikabfrage. Kandidatenlisten werden
nach `Id` sortiert, nicht in Inspector-Reihenfolge gelesen.

**Golden Seeds: unverändert. `check_determinism.sh`: 148/148.**

---

## 3. Die Pipeline

### Schritt 1 — Raum analysieren

`RoomShellInfo` wird von `ModularRoomBuilder.Build` **geschrieben**, nicht später abgeleitet:
Größe, Türmaske, Fenstermaske, Kategorie, Raum-ID. Die Fenstermaske entsteht in der Hülle aus der
Raumidentität; sie ein zweites Mal zu berechnen wäre dieselbe Entscheidung an zwei Stellen
(CLAUDE.md Fehler 1).

`RoomFootprint` bildet daraus das nutzbare Rechteck (Innenmaß minus halbe Wandstärke minus 3 cm
Wandabstand) und vier `WallRun`s mit Länge, Innenrichtung und dem Gierwinkel, bei dem eine
Vorderseite in den Raum zeigt.

**Achsenparallel, und das ist eine Entscheidung:** Möbel werden nur in Vielfachen von 90° gedreht,
weil ein Haus so gebaut ist. Bei 90° *ist* die gedrehte Grundfläche wieder achsenparallel —
Breite und Tiefe tauschen. Der Footprint wird deshalb **lokal am Modell** gemessen und beim Drehen
getauscht, statt eine Welt-AABB zu nehmen, die bei jedem anderen Winkel zu groß wäre.

### Schritt 2 — Freizonen reservieren, **vor** allem anderen

Im Konstruktor von `RoomFootprint`, in dieser Reihenfolge:

| Zone | Größe | Höhengrenze |
|---|---|---|
| Türschwenkbereich | Öffnungsbreite + Blattbreite, so tief wie das Blatt lang ist, auf beiden Seiten | 0 (gesperrt) |
| Laufweg | von jeder Tür zur Raummitte, Breite = Spielerdurchmesser + 2 × 15 cm | 0 (gesperrt) |
| Fenster | Fensterbreite + 30 cm, 45 cm tief | **0.90 m** |

Das Fenster ist eine **Höhengrenze, kein Verbot**: eine Kommode darunter ist richtig eingerichtet,
ein Kleiderschrank davor ist ein zugestelltes Fenster. Dieselbe Fläche, zwei Antworten.

Der Laufweg geht **über die Mitte**, weil damit jede Tür mit jeder anderen verbunden ist, ohne
dass für n Türen n² Korridore nötig wären.

Die Laufwegbreite ist abgeleitet, nicht getippt:
`PlayerFactory.CapsuleRadius * 2 + 0.15 * 2 = 1.00 m`. Ändert sich der Radius, ändert sich das mit.

### Schritt 3 — Variante wählen

`FurnishingProfiles.For(category)` liefert die Varianten in stabiler Reihenfolge. Der Startindex
ist `(rng + roomId) % count` — **benachbarte Räume bekommen verschiedene Varianten**, weil die
Raum-IDs im Layout fortlaufend sind.

Eine Variante wird verworfen, wenn ihre Bodenfläche nicht reicht oder ein **Kernmöbel** fehlt. Ein
Bad ohne Waschbecken ist kein Bad mit einem Sessel darin. Verworfen heißt **restlos abgeräumt**:
halbe Reste, über die die nächste Variante gelegt wird, sind zwei Einrichtungen übereinander.

Eine Raumart ohne Profil bekommt ausdrücklich **keine** Einrichtung und wird gemeldet — ihr die
eines Wohnzimmers zu geben, sähe im Editor nach Fortschritt aus und im Spiel nach einem Fehler,
den niemand mehr zuordnen kann.

### Schritt 4 — Hauptmöbel

Wand-für-Wand (deterministisch durchmischt), Stützstellen von der Wandmitte nach außen. Das
geprüfte Rechteck enthält den **Bedienraum**: ein Schrank, dessen Tür gegen ein Bett schlägt,
steht formal frei und ist trotzdem falsch aufgestellt.

Gesetzt wird über die **gemessene Mitte**:

```csharp
instance.localPosition = wanted - rotation * metrics.CentreOffset;
```

Nicht über den Pivot — dieses Paket legt Pivots bis zu 40 m neben die eigene Geometrie. Dieselbe
Korrektur, die die Tür- und Fenstereinsätze schon benutzen.

Passt es nicht, wird der **nächste Kandidat** probiert, nicht das Möbelstück verkleinert.

### Schritt 5 — Gruppen

| Muster | Verwendung |
|---|---|
| `InFront` | Couchtisch vor dem Sofa, Stuhl vor dem Schreibtisch |
| `Beside` | Nachttische links und rechts vom Bett, Sessel neben dem Couchtisch |
| `Around` | Stühle rundum am Esstisch, die vier Seiten der Reihe nach |

Bei Platzmangel fällt zuerst das als `Optional` markierte Gruppenmitglied weg — ohne dafür ein
Kernmöbel zu verschieben.

### Schritt 6 — Licht und Dekoration

Ein `Practical` (Punktlicht, warm, Reichweite 6 m, **ohne Echtzeitschatten**) entsteht **nur an
einer wirklich platzierten Leuchte** und nur, solange `LightsPerRoom` es erlaubt.

Fehlt ein Leuchten-Prefab, wird die Lücke gemeldet und **nichts** gebaut. Ein unsichtbares
Hilfslicht an einer Stelle, an der keine Lampe steht, wäre eine Lichtquelle ohne Ursache.

Deko liegt auf einer **geprüften** Fläche: 0.35 m ≤ Höhe ≤ 1.35 m, und die Fläche muss den
Gegenstand tragen (Breite und Tiefe je höchstens 80 % der Trägerfläche). Die Unterkante liegt
exakt auf der Oberseite, Pivot-Versatz herausgerechnet. Kleine Deko verliert Rigidbody und
Collider — ein Buch auf einem Tisch ist ein Bild und kein Körper.

### Schritt 7 — Prüfen

`VerifyWalkways` misst am Ende nach, ob eine gesperrte Zone belegt wurde. Dass jede Platzierung
geprüft wurde, ist eine Behauptung über das Verfahren; das hier ist eine über das Ergebnis.

---

## 4. Reihenfolge im Generator

```
InstantiateRoom …
ConnectDoors
SealUnusedOpenings
FurnishRooms          <-- neu
InstallRoomInteractables   (findet jetzt Lampen und baut Lichtschalter)
SpawnProps                 (überspringt, was keine Definition hat)
…
BuildNavigation            (bäckt die Möbel ins NavMesh)
HouseContentReport
```

**Das behebt „0 von 0 practicals".** `InstallRoomInteractables` sucht je Raum ein `Light` und baut
nur dann einen `InteractiveLightSwitch` dazu. Solange nichts Lampen aufstellte, fand es keins —
das war kein Fehler der Suche, sondern das ehrliche Ergebnis eines Hauses ohne Lampen.

**NavMesh:** Möbel stehen vor dem Backen, werden also als Hindernisse eingebacken. Keine
Neuberechnung pro Möbelstück, kein `NavMeshObstacle` an Dekoration.

**Multiplayer:** Die Einrichtung ist eine reine Funktion aus (Seed, Raum-ID, Katalog). Jeder
Client baut dasselbe, ohne dass etwas repliziert wird — dieselbe Eigenschaft, auf der die
Hausgenerierung schon beruht. Es entstehen **keine** neuen NetworkObjects. Das ist ausdrücklich
*keine* vollständige Netzwerksynchronisierung: verschöbe ein Spieler später einen Stuhl, wäre das
ein eigener Vertrag.

---

## 5. Was tatsächlich verfügbar ist

### Sofort, ohne Editor und ohne Paket

Im Repository liegen unter `Resources/Props/` genau zwei brauchbare Möbelmodelle:

| Pfad | Rolle | Material |
|---|---|---|
| `Props/CIYC_Armchair` | `Armchair` | `MAT_Armchair` |
| `Props/CIYC_AntiqueTable` | `CoffeeTable`, `SideTable` | `MAT_AntiqueTable` |

Damit greift im Wohnzimmer die Variante **„Kleiner Salon"** (Kern: Sessel, Gruppe: Beistelltisch).
Die Variante „Sitzgruppe am Kamin" scheitert korrekt am fehlenden Sofa und fällt darauf zurück.

### Nach einem Klick, auf einer Maschine mit dem Paket

`Catch If You Can > 4. SPIELINHALT > Content > Moebelkatalog aus HQ-Paket [SCHREIBT ASSET]`

trägt ~50 Prefabs aus `Assets/HQ Modular House/props/` nach — Sofa, Betten, Kommoden,
Nachttische, Küchenmöbel, Sanitärobjekte, Schreibtische, Regale, Stehlampen, Bilder.

**Warum ein Werkzeug und keine handgeschriebene Referenz:** eine Prefab-Referenz ist
`{fileID, guid, type}`, und die fileID ist die des Wurzelobjekts *im* Prefab — eine Zahl, die nur
Unity kennt. Sie zu raten ergibt eine Referenz, die auf nichts zeigt und im Inspector genauso
aussieht wie eine, die es tut (CLAUDE.md Fehler 3 und 15). Das Werkzeug löst jeden Pfad über die
`AssetDatabase` auf und trägt nur ein, was aufgegangen ist; der Rest wird namentlich gemeldet.

Die Pfadtabelle stammt aus `Docs/VENDOR_ASSET_MANIFEST.txt`, geschrieben auf der Maschine mit dem
installierten Paket. Sie ist **bewusst nicht vollständig**: Einträge, bei denen aus dem Namen
nicht sicher hervorgeht, was das Stück ist, bleiben weg. Ein falsch zugeordnetes Möbelstück ist
schlimmer als ein fehlendes — das fehlende wird gemeldet, das falsche sieht nach Absicht aus.

### Bekannte Lücken

| Fehlt | Folge |
|---|---|
| Sofa im Repository | Wohnzimmer nutzt bis zum Katalog-Klick den „Kleinen Salon" |
| Deckenleuchte in jeder Quelle | `CeilingLamp` wird gemeldet; Räume mit `WallLamp`/`FloorLamp` sind versorgt |
| Bad-Waschbecken als eigenes Prefab | `Bathroom/faucet` steht ersatzweise als `WashBasin` |
| Vorhänge, Teppiche | keine Rolle vergeben — die Profile fragen nicht danach |

---

## 6. Die wichtigsten einstellbaren Werte

| Wert | Fundstelle |
|---|---|
| Möbelbudget je 10 m² | `RoomFurnishingCatalog.MajorPiecesPerTenSquareMetres` (4) |
| Dekobudget je Raum | `RoomFurnishingCatalog.DecorPerTenSquareMetres` (3) |
| Lichtbudget je Raum | `RoomFurnishingCatalog.LightsPerRoom` (1) |
| Platzierungsversuche je Wand | `RoomFurnishingCatalog.PlacementAttempts` (12) |
| Ausführliche Diagnose | `RoomFurnishingCatalog.VerboseDiagnostics` (aus) |
| Bedienraum je Möbelstück | `FurnitureEntry.ClearanceFront` |
| Auswahlgewicht | `FurnitureEntry.Weight` |
| Wandabstand | `RoomFootprint.WallGap` (0.03 m) |
| Laufwegmarge | `RoomFootprint.WalkwaySafetyMargin` (0.15 m) |
| Höhenfenster für Deko-Trägerflächen | `RoomFurnisher.DecorSurfaceMin/Max` (0.35 / 1.35 m) |
| Raumprofile und Varianten | `FurnishingProfiles` (Code, absichtlich) |

**Warum die Profile Code sind und kein Asset:** das sind Regeln, keine Inhalte. Dass ein Esszimmer
einen Tisch mit Stühlen ringsum hat, ändert sich nicht mit dem Paket — und eine Regel im Inspector
ist eine Regel, die jemand versehentlich leeren kann, ohne dass es auffällt. Genau das ist in
diesem Projekt schon passiert (CLAUDE.md Fehler 18).

---

## 7. Diagnose

Standardmäßig **eine Zeile je Generierung**:

```
[CIYC][House][Furnish] rooms=10 furnished=7 majorPieces=14 decor=9 lights=5
```

Räume mit einem Problem — fehlendes Kernmöbel, blockierter Laufweg, gar keine Einrichtung —
werden einzeln angehängt. Kein Eintrag für jeden fehlgeschlagenen Platzierungsversuch: ein Stuhl,
der an der ersten Wand nicht passt und an der zweiten schon, ist der Normalfall dieses Verfahrens.

`VerboseDiagnostics` schaltet die Zeile je Raum samt verworfenen Kandidaten frei.

---

## 8. Status

| | |
|---|---|
| **Implementiert** | Pipeline, Profile für 12 Raumarten, Katalog, Editor-Werkzeug, Diagnose, Würfel-Entfernung |
| **Automatisiert geprüft** | `check_room_furnishing.sh` 28/28 (4 Zahnproben bestanden), `check_determinism` 148/148, alle übrigen Guards grün, Offline-Typecheck 19 Fehler = Baseline |
| **In Unity getestet** | **NICHTS.** Kein Editor verfügbar. |

### Testablauf in Unity — NICHT GETESTET, das ist deiner

1. `03_Investigation` öffnen, Play.
2. Konsole nach **`[CIYC][House][Furnish]`** filtern. Erwartet: `furnished` > 0 und `majorPieces` > 0.
3. Konsole nach **`[CIYC][House][Content]`** filtern. Erwartet: `flagged=0`. Steht dort mehr,
   nennt jede Zeile Name, Raum, Größe, Mesh und Hierarchie-Pfad.
4. Ins Wohnzimmer gehen: ein Sessel an der Wand, ein Beistelltisch daneben.
5. **Durch jede Tür gehen.** Erwartet: kein Möbelstück im Schwenkbereich, kein Weg versperrt.
6. Menü: `Catch If You Can > 4. SPIELINHALT > Content > Moebelkatalog aus HQ-Paket`, dann neu
   generieren. Erwartet: Betten in Schlafzimmern, Küchenmöbel an Küchenwänden, Stühle am Tisch.
7. Konsole nach **`practical`** filtern. Erwartet: nicht mehr `0 of 0`, sobald Leuchten im Katalog sind.
8. Denselben Seed zweimal laufen lassen und die `[Furnish]`-Zeilen vergleichen. Erwartet: identisch.
