# Das Unity-Menü „Catch If You Can" — vollständige Bestandsaufnahme

**Stand:** Commit `8529c2c`. **Nichts wurde ausgeführt, hinzugefügt, umbenannt oder gelöscht.**
Diese Datei ist eine Lesefrucht: sie beschreibt, was da ist, nicht was sein soll.

## Wie diese Liste entstanden ist, und wo sie ungenau sein kann

Gesucht wurde nach jeder `[MenuItem(...)]`-Deklaration, auch nach denen, die den Pfad aus einer
Konstante zusammensetzen — **sechs Einträge tauchen bei einer Suche nach `MenuItem("` nicht auf**,
weil sie `MenuItem(MenuPath, …)` oder `MenuItem(MenuRoot + "…")` schreiben. Wer das Menü per
Textsuche prüft, übersieht sie.

Die Wirkungen jeder Datei sind durch Grep nach den schreibenden Unity-APIs bestimmt worden,
**mit vorher entfernten Kommentarzeilen** — sonst zählt ein Kommentar, der vor `AssetDatabase.Refresh`
warnt, als Aufruf.

**Die eine Ungenauigkeit, die man kennen muss:** Grep arbeitet pro *Datei*. Eine Datei mit vier
Menüpunkten, von denen einer schreibt, sieht wie vier schreibende Punkte aus. Wo das vorkommt,
habe ich pro Methode nachgesehen; diese Fälle sind unten mit **(pro Methode geprüft)** markiert.
Was nicht so markiert ist, ist eine Dateiaussage und kann für den einzelnen Punkt zu streng sein.

**Was Grep grundsätzlich nicht sieht:** eine Methode, die eine andere Klasse aufruft, die dann
schreibt. `Rebuild Nathan If Missing` ist genau das — die Datei selbst schreibt nichts, sie ruft
`NathanCharacterSetup` auf, und das schreibt sehr wohl.

## Wie viele es sind

| | |
|---|---|
| Menüpunkte unter `Catch If You Can` | **47** |
| Menüpunkte unter `Tools/Catch If You Can/Determinism` | **4** |
| Editor-Dateien insgesamt | 39 |
| davon ohne Menüpunkt (Postprocessors) | 3 |

Die vier Determinismus-Werkzeuge liegen in einem **anderen Wurzelmenü**. Wer nur unter
„Catch If You Can" nachsieht, findet sie nie.

## Risikoklassen

| Farbe | Bedeutung |
|---|---|
| 🟢 **GRÜN** | Reines Lesen. Ändert weder Szene noch Assets noch Importer noch Dateien. |
| 🔵 **BLAU** | Kontrollierte Editor-Änderung mit `Undo` und engem Umfang. |
| 🟡 **GELB** | Schreibt Assets, Szenendaten oder Importer-Einstellungen — gewollt und begrenzt. |
| 🔴 **ROT** | Kann in Masse ändern, neu importieren, löschen, überschreiben oder umbauen. |
| ⚪ **GRAU** | Alter Test, Migration, Forensik oder Provisorium. Normalerweise nicht mehr benutzen. |

---

## 1. Lobby (4 Punkte) — neu, September 6

| Menüpfad | Risiko | L/S | Was es tut | Was es ändert | Klicken? | Wann | Wann NICHT |
|---|---|---|---|---|---|---|---|
| `Lobby/Portalwand messen` | 🟢 | L | Misst, welche Collider die Portalöffnung überdecken, und wendet ResolveWalls drei Tests an | nichts | ja | wenn das Portal keine Wand findet | — |
| `Lobby/Raumgroesse messen` | 🟢 | L | Misst lichte Höhe, Außenmaß und jede Mesh-Quelle; schlägt einen Faktor vor | nichts | ja | vor jeder Maßstabsänderung | — |
| `Lobby/Raum skalieren` | 🔵 | S | Legt `HQ_ROOM_SCALE_ROOT` an, hängt die HQ-Wurzeln um, setzt einen Faktor, misst nach | Szenenhierarchie, Transform der Wurzel | ja, mit Undo | nach der Messung | wenn die Messung nicht 3.92 m ergab — es bricht dann selbst ab |
| `Main Menu/Lobby bearbeiten` | 🔵 | S | Schaltet `MainMenu_Lobby` zum Bearbeiten ein und baut Vorschauen der vier Laufzeit-Requisiten | nur das Aktiv-Flag; Vorschauen mit `HideFlags.DontSave` | ja | zum Einrichten des Raums | — |

**Datei:** `LobbyPortalWallProbe.cs`, `HQRoomScaleAudit.cs`, `HQRoomScaleApply.cs`,
`MainMenuLobbyAuthoring.cs`. Messung geteilt über `HQRoomMeasurement.cs` (kein Menüpunkt).

## 2. Main Menu (4 Punkte)

| Menüpfad | Risiko | L/S | Was es tut | Was es ändert | Klicken? |
|---|---|---|---|---|---|
| `Main Menu/Lobby bearbeiten` | 🔵 | S | siehe oben | Aktiv-Flag, Vorschauen | ja |
| `Main Menu/Authored Lobby pruefen` | 🟢 | L | Zählt auf, was im Raum steht und was erst zur Laufzeit gebaut wird | nichts | ja |
| `Main Menu/Bake Logo Into Scene` | 🟡 | S | Baut Branding-Canvas, Logo, TAP-Beschriftung und trägt sie in `cinematicUiRoots` ein | Szene — **und speichert sie selbst** | ja, aber Szene wird gespeichert |
| `Main Menu/Rebuild Door Atmosphere` | 🟡 | S | Baut Nebel, Licht und Glühen an der Menütür neu | Szenenobjekte, Materialien, mit `Undo` | ja |

⚠️ **`Bake Logo Into Scene` ruft `EditorSceneManager.SaveScene`.** Es ist der einzige Menüpunkt,
der die Szene ohne Rückfrage auf die Platte schreibt. Vorher speichern, was einem lieb ist.

## 3. Szene (1 Punkt)

| Menüpfad | Risiko | L/S | Was es tut | Klicken? |
|---|---|---|---|---|
| `Szene/Hierarchie sortieren` | 🔵 | S | Schlägt eine Ordnerstruktur vor; hakt nur an, was BEWIESEN ist | ja — nichts wird ohne Häkchen bewegt |

Reparentiert über `Undo.SetTransformParent`, misst Welttransformationen nach, meldet Abweichungen
statt sie von Hand zu korrigieren, markiert die Szene nur dirty.

## 4. Modular Interior (8 Punkte) — der unübersichtlichste Bereich

| Menüpfad | Risiko | L/S | Was es tut | Was es ändert |
|---|---|---|---|---|
| `Modular Interior/Audit Pack` | 🟢 | L | **Öffnet nur das Fenster.** Der Bericht läuft erst auf Knopfdruck | nichts |
| `Modular Interior/Architecture Forensics - Interior` | 🟢 | L | Misst `…/interior` rekursiv: Maße, Pivots, Ausrichtung, Collider, Raster | nichts |
| `Modular Interior/Architecture Forensics - Full HQ Package` | 🟢 | L | Dasselbe über das ganze Paket | nichts |
| `Modular Interior/Validate Environment` | 🟢 | L | Sagt, ob mit dem Bestand ein Haus baubar ist | nichts |
| `Modular Interior/Material-Doktor (Auswahl pruefen)` | 🟢 | L | Diagnostiziert, warum ein gekauftes Teil weiß aussieht | nichts |
| `Modular Interior/Bauteile pruefen und setzen` | 🔵 | S | Browser über alle Paketteile; setzt ein Teil in die Szene | erzeugt GameObjects mit `Undo` |
| `Modular Interior/Build ONE Test Room` | 🟡 | S | Baut eine 6×3×6-Zelle aus dem Katalog | erzeugt Szenenobjekte — **ohne Undo** |
| `Modular Interior/Remove Test Room` | 🟡 | S | Löscht den Testraum | `DestroyImmediate` — **ohne Undo** |
| `Modular Interior/Katalog aus GEPRUEFTEN Pfaden schreiben` | 🟡 | S | Schreibt den `ModularInteriorCatalog` aus verifizierten Asset-Pfaden | genau ein `.asset` |

⚠️ **`Remove Test Room` benutzt `GameObject.Find`.** Das überspringt inaktive Objekte. Ein
ausgeschalteter Testraum wird nicht gefunden und nicht entfernt — und der nächste `Build` stellt
einen zweiten daneben.

⚠️ **Die vier Fenster-Punkte oben sind grün, das Fenster selbst nicht.** Darin sitzt der Knopf
„2. Katalog bauen", und der schreibt (`AssetDatabase.CreateAsset` + `SaveAssets` + `Refresh`).
Das Menü ist harmlos, der Knopf im Fenster nicht.

## 5. External Content (4 Punkte)

| Menüpfad | Risiko | L/S | Was es tut |
|---|---|---|---|
| `External Content/Validate Environment Content` | 🟢 | L | Zählt den Raum- und Prop-Bestand |
| `External Content/Audit Imported HQ Modular Pack` | 🟢 | L | Öffnet dasselbe Fenster für einen frisch importierten Ordner |
| `Download Missing External Assets` | 🟡 | S | Lädt fehlende externe Assets herunter, schreibt Dateien, `Refresh` |
| `Integrate External Assets` | 🔴 | S | **Das breiteste Werkzeug im Projekt** |

🔴 **`Integrate External Assets`** ruft: `AssetImporter` + `SaveAndReimport` in einer Schleife,
`AssetDatabase.CreateAsset`, `DeleteAsset`, `MoveAsset`/`CopyAsset`, `SaveAssets`, Prefab-Speichern,
erzeugt und zerstört GameObjects, reparentiert, setzt Transforms und tauscht Materialien. Es kann
in einem Klick den halben Asset-Ordner umschreiben. **Nicht klicken, ohne vorher zu fragen.**

## 6. HQ Pack Optimizer (1 Punkt)

| Menüpfad | Risiko | L/S |
|---|---|---|
| `HQ Pack Optimizer` | 🔴 | S |

**Was es ändert:** ausschließlich **Textur-Importer-Einstellungen** des gekauften Pakets —
`maxTextureSize` (Standard und pro Plattform über `SetPlatformTextureSettings`), `isReadable`.
Danach `SaveAndReimport` pro Textur und ein `AssetDatabase.Refresh`. Es schreibt außerdem eine
Berichtsdatei (`File.WriteAllText`).

**Was es niemals ändert:** kein Material, kein Mesh, kein Prefab, keine Szene, keinen Shader,
keine Textur*datei*. Es fasst nur an, wie importiert wird — nie, was importiert wird.

⚠️ **Der Plan ist laut Audit bereits vollständig angewendet** (388 von 388 Texturen). Ein
erneuter Lauf hat nichts zu tun, kostet aber einen Reimport des ganzen Pakets: 388 Texturen,
121 Modelle. Das ist eine Kaffeepause und ein Risiko ohne Gegenwert.

## 7. Portal (1 Punkt)

| Menüpfad | Risiko | L/S | Was es tut |
|---|---|---|---|
| `Portal/Adopt Purchased Portal Pack` | 🟡 | S | Übernimmt Artwork aus dem gekauften Knife-Paket in das Projekt |

Es **kopiert** die Bilder (`CopyAsset` + `ImportAsset`), löscht dabei ein vorhandenes Ziel
(`DeleteAsset` auf den Zielpfad, nicht auf das Paket) und setzt Felder am `LobbyPortal`
(`SetDirty`). Es ändert die **Optik**, nie die **Form** des Portals.

**Diagnose gegen Änderung, für alle Portal-Werkzeuge:**

| nur messen | ändert Portalzustand |
|---|---|
| `Lobby/Portalwand messen` | `Portal/Adopt Purchased Portal Pack` |
| `Lobby/Raumgroesse messen` (meldet die Folge fürs Portal) | — |

## 8. Characters (5 Punkte)

| Menüpfad | Risiko | L/S | Was es tut |
|---|---|---|---|
| `Characters/Validate Nathan Player Visual` | 🟢 | L | Lädt das Prefab und prüft es *(pro Methode geprüft)* |
| `Characters/Build Nathan Player Visual` | 🟡 | S | Baut Nathans Visual-Prefab *(pro Methode geprüft)* |
| `Characters/Build Character Assets` | 🟡 | S | Schreibt Charakterdefinitionen und Rig-Profile |
| `Characters/Fix Nathan Texture Import Settings` | 🟡 | S | Setzt Nathans Textur-Importer und reimportiert sie |
| `Characters/Rebuild Nathan If Missing` | 🟡 | S | Setzt ein Session-Flag zurück und lässt das Sicherheitsnetz erneut bauen |

⚠️ `Rebuild Nathan If Missing` sieht bei einer Dateisuche **read-only** aus. Es ist es nicht: es
ruft `TryBuild()`, das über `NathanCharacterSetup` Assets schreibt und reimportiert.

## 9. Build (3 Punkte)

| Menüpfad | Risiko | L/S |
|---|---|---|
| `Build Android Development` | 🟡 | S |
| `Build Android Release` | 🟡 | S |
| `Build iOS` | 🟡 | S |

`BuildPipeline.BuildPlayer`. Schreibt nur in den Build-Ordner, ändert nichts am Projekt — aber ein
Build blockiert den Editor lange und kann die Build-Liste anfassen.

## 10. Der Rest

| Menüpfad | Risiko | L/S | Was es tut |
|---|---|---|---|
| `Setup Project` | 🔴 | S | Legt Projektstruktur an: `CreateAsset`, `ImportAsset`, `Refresh`, Dateien |
| `Validator` | 🟡 | L→S | Prüffenster — **kann Szenen öffnen** (`OpenScene`) und damit Ungespeichertes verwerfen |
| `Audio Debugger` | 🟢 | L | Play-Mode-Fenster für den laufenden Audiozustand |
| `Audio/Build Audio Mixer` | 🟡 | S | Baut das Mixer-Asset |
| `Audio/Generate Default Audio Events` | 🟡 | S | Schreibt Standard-Audioereignisse *(pro Methode geprüft)* |
| `Player/Build Player Prefab` | 🟡 | S | Schreibt das Spieler-Prefab |
| `Content/Create Content Registry` | 🟡 | S | Schreibt die Content-Registry *(pro Methode geprüft)* |
| `Equipment/Build Equipment Prefabs` | 🟡 | S | Baut die elf Ausrüstungs-Prefabs |
| `Ghosts/Build Ghost Visual Prefabs` | 🟡 | S | Baut die Geist-Prefabs |
| `Rooms/Build Room Definitions From Folder` | 🟡 | S | Schreibt Raumdefinitionen aus einem Ordner |
| `Environment/Build Candle Flame Material` | 🟡 | S | Schreibt das Kerzenflammen-Material |
| `Environment/Build Interactive Room Sky` | 🟡 | S | Baut Himmel und Silhouetten, reimportiert Texturen |
| `Generate Placeholder Prefabs` | ⚪🟡 | S | Erzeugt Platzhalter-Prefabs |
| `Generate 100 Houses` | ⚪🔵 | S | Generiert 100 Häuser aus Seeds 0–99 und berichtet |
| `Development/Create Missing Lab Scenes` | 🟡 | S | Legt fehlende `DEV_`-Szenen an *(pro Methode geprüft)* |
| `Development/Rebuild All Lab Scenes` | 🔴 | S | **Überschreibt jede `DEV_`-Szene.** Handarbeit darin ist weg. Fragt vorher |

**In einem anderen Wurzelmenü, leicht zu übersehen:**

| Menüpfad | Risiko | L/S |
|---|---|---|
| `Tools/Catch If You Can/Determinism/Generate Golden Seeds` | 🟡 | S — schreibt eine `.cs`-Datei und ruft `Refresh` |
| `Tools/…/Determinism/Validate Golden Seeds` | 🟢 | L *(pro Methode geprüft)* |
| `Tools/…/Determinism/Compare Two Layouts` | 🟢 | L |
| `Tools/…/Determinism/Print Layout Report` | 🟢 | L |

---

## Abhängigkeitskarte: wer benutzt dasselbe darunter

```
ModularInteriorCatalog (.asset)
├── geschrieben von: Modular Interior/Katalog aus GEPRUEFTEN Pfaden schreiben   (HQVerifiedCatalog)
├── geschrieben von: Fenster-Knopf "2. Katalog bauen"                           (ModularInteriorTools)
└── gelesen von:     Build ONE Test Room, HQRoomAuthoring, ProceduralHouseGenerator

InvestigationContentCatalog (Resources)
├── geschrieben von: Content/Create Content Registry
├── Feld ModularInterior gesetzt von: beide Katalog-Schreiber oben
└── vier Raum-Materialien gelesen von: PrimitiveRoomFactory (Ersatzraum)

HQRoomMeasurement  (eine Messung, zwei Werkzeuge)
├── Lobby/Raumgroesse messen
└── Lobby/Raum skalieren

LobbyPortal
├── gelesen von:    Lobby/Portalwand messen, Lobby/Raumgroesse messen
└── geschrieben von: Portal/Adopt Purchased Portal Pack

01_MainMenu.unity
├── Main Menu/Bake Logo Into Scene      (schreibt UND speichert)
├── Main Menu/Rebuild Door Atmosphere
├── Main Menu/Lobby bearbeiten          (nur Aktiv-Flag + Vorschauen)
├── Szene/Hierarchie sortieren
└── Lobby/Raum skalieren

Textur-Importer des HQ-Pakets
├── HQ Pack Optimizer          (setzt sie)
├── HQPieceBrowser             (LIEST sie nur, zur Anzeige)
└── Integrate External Assets  (setzt sie, projektweit)
```

## Doppelte und überlappende Werkzeuge

**1. Zwei Katalogschreiber.** `Katalog aus GEPRUEFTEN Pfaden schreiben` (HQVerifiedCatalog,
5. Sept.) und der Knopf „2. Katalog bauen" im Modular-Interior-Fenster (ModularInteriorTools,
4. Sept.) schreiben **dasselbe Asset** auf zwei Wegen. Der eine geht von verifizierten Pfaden aus,
der andere von einer Namensklassifikation — und die Klassifikation hat in diesem Paket
nachweislich 3 von 105 Prefabs erwischt. **Wer den falschen klickt, überschreibt den guten
Katalog mit einem schlechteren, ohne Warnung.** Das ist die schärfste Überlappung im Menü.

**2. Zwei Paket-Prüfer.** `Modular Interior/Audit Pack` und
`External Content/Audit Imported HQ Modular Pack` öffnen verschiedene Fenster mit ähnlichem Zweck.
Beide sind grün, aber es ist nicht erkennbar, welches was kann.

**3. Drei Wege, dasselbe Paket zu vermessen.** `Audit Pack` (zählt und klassifiziert),
`Kit vermessen` (Knopf im Fenster: Maße, Pivots, Collider, Raster), `Architecture Forensics`
(liest genau den eingetragenen Ordner rekursiv). Alle drei lesen nur. Der Unterschied:

| | liest was | antwortet auf |
|---|---|---|
| **Audit Pack** | zählt Prefabs/Materialien/Texturen | „Was ist überhaupt drin?" |
| **Kit vermessen** | Maße, Pivots, Ausrichtung, Collider | „Passt das zusammen?" |
| **Architecture Forensics** | genau ein Ordner, rekursiv, ohne Umleitung | „Was steht wirklich in DIESEM Ordner?" |
| **Validate Environment** | den Projektbestand, nicht das Paket | „Kann ich damit ein Haus bauen?" |

**Architecture Forensics** existiert, weil die Klassifikation nach Dateinamen bei diesem Paket
versagt hat — es nummeriert seine Prefabs und nennt sein Glas `Steklo`. Die Forensik umgeht jede
Klassifikation und liest stur den Ordner. **Sie sollte weiter benutzt werden**, sobald eine Frage
mit „was ist da wirklich" anfängt.

**4. Zwei Umbau-Werkzeuge für dieselbe Szene.** `Szene/Hierarchie sortieren` und
`Lobby/Raum skalieren` hängen beide Objekte in `01_MainMenu` um. Nacheinander in falscher
Reihenfolge ausgeführt, kann das eine die Annahme des anderen brechen: `Raum skalieren` sucht
`HQ_*` als **Szenen-Wurzeln**. Sortiert man vorher die Hierarchie und schiebt sie unter einen
Ordner, findet es nichts mehr. **Erst skalieren, dann sortieren.**

## Nur für Debugging oder Migration gebaut

| Menüpfad | warum |
|---|---|
| `Generate 100 Houses` | Testet den Generator über 100 Seeds. Ein Diagnosewerkzeug, kein Arbeitswerkzeug |
| `Generate Placeholder Prefabs` | Aus der Zeit vor echten Assets |
| `Integrate External Assets` | Migrationswerkzeug für den alten Kenney-Bestand — der ist gelöscht |
| `Download Missing External Assets` | Gehört zu derselben alten Pipeline |
| `Modular Interior/Build ONE Test Room` + `Remove Test Room` | Für die eine Frage „sieht ein Raum aus dem Paket richtig aus" gebaut |
| `Architecture Forensics` (beide) | Als Antwort auf eine fehlgeschlagene Klassifikation gebaut — aber weiterhin nützlich |
| `HQ Pack Optimizer` | Der Plan ist angewendet. Bis das Paket wechselt, hat es nichts zu tun |

## Agenten-Kollisionen

Aus der Git-Historie, nach Datum der Ersteinführung:

| Zeitraum | Bereich | Dateien |
|---|---|---|
| 28.–29. Aug | Projektgerüst | ProjectSetup, BuildMenu, Validator, AudioMixer, ExternalAsset*, PrefabFactory, HouseGeneratorTestTool, DeterminismTools |
| 1. Sept | Charakter + Umgebung | Nathan*(3), InteractiveRoomSkySetup, CandleFlameSetup, MainMenuLogoBaker |
| 3. Sept | Gameplay-Systeme | PlayerPrefabBuilder, EquipmentAssetBuilder, GhostVisualPrefabBuilder, CharacterAssetBuilder, DevelopmentLabBuilder |
| 4. Sept | Modulares Interieur | ModularInteriorTools (11 Commits), EnvironmentContentTools, RoomDefinitionFromFolder, HQPackOptimizer |
| 5. Sept | HQ-Paket + Szene | HQPieceBrowser, HQTestRoomTool, HQVerifiedCatalog, MainMenuHierarchyTool, MainMenuLobbyAuthoring, PurchasedPortalAdapter |
| 6. Sept | Diagnose + Maßstab | HQMaterialDoctor, LobbyPortalWallProbe, HQRoomScaleAudit/Apply/Measurement |

**Wo sich Verantwortung überschneidet:**

1. **`ModularInteriorCatalog`** hat zwei Schreiber aus zwei Tagen (4. und 5. Sept.). Der jüngere
   entstand, *weil* der ältere falsch klassifizierte — der ältere ist aber nie stillgelegt worden.
2. **`01_MainMenu.unity`** wird von fünf Werkzeugen aus vier Tagen angefasst (LogoBaker vom
   1. Sept., AtmosphereBuilder vom 30. Aug., HierarchyTool und LobbyAuthoring vom 5. Sept.,
   RoomScaleApply vom 6. Sept.). Keines weiß von den anderen.
3. **Textur-Importer des HQ-Pakets**: `HQ Pack Optimizer` setzt sie, `Integrate External Assets`
   setzt sie projektweit ebenfalls. Der zweite kann den ersten überschreiben.
4. **`InvestigationContentCatalog`**: `Create Content Registry` schreibt es, und beide
   Katalog-Schreiber greifen in dasselbe Asset. Genau hier sind am 6. Sept. vier
   Material-Referenzen auf null gefallen, ohne dass etwas gemeldet hätte.

## Vorschlag für ein aufgeräumtes Menü — NICHT umgesetzt

```
Catch If You Can/
├── 1 Prüfen (ändert nichts)
│     Raumgroesse messen · Portalwand messen · Material-Doktor
│     Paket prüfen · Architektur-Forensik · Umgebung prüfen
│     Authored Lobby prüfen · Nathan prüfen · Golden Seeds prüfen
├── 2 Szene bearbeiten (mit Undo)
│     Lobby bearbeiten · Hierarchie sortieren · Raum skalieren
│     Bauteile prüfen und setzen · Tür-Atmosphäre neu bauen
├── 3 Assets schreiben (schreibt, begrenzt)
│     Katalog schreiben · Content-Registry · Player-Prefab
│     Equipment-Prefabs · Geist-Prefabs · Raumdefinitionen
│     Audio-Mixer · Audio-Ereignisse · Materialien · Logo backen
├── 4 HQ-Paket (braucht das gekaufte Paket)
│     Pack Optimizer · Testraum bauen · Testraum entfernen
├── 5 Build
│     Android Development · Android Release · iOS
└── 9 Alt / Debug (normalerweise nicht anfassen)
      Integrate External Assets · Download Missing External Assets
      Generate Placeholder Prefabs · Generate 100 Houses
      Setup Project · Rebuild All Lab Scenes
```

Die vier Determinismus-Punkte gehören unter dasselbe Dach — sie stehen ohne erkennbaren Grund in
einem anderen Wurzelmenü.

## Die Kurzantwort: was darf man ohne Rückfrage klicken

**Immer sicher (🟢, ändert nichts):** alle vier `Architecture Forensics`/`Audit Pack`/
`Validate Environment`/`Material-Doktor`, beide `External Content`-Prüfer, beide `Lobby`-Messungen,
`Authored Lobby pruefen`, `Validate Nathan Player Visual`, `Audio Debugger`, die drei lesenden
Determinismus-Punkte.

**Sicher mit Undo (🔵):** `Lobby bearbeiten`, `Hierarchie sortieren`, `Raum skalieren`,
`Bauteile pruefen und setzen`.

**Erst fragen (🔴):** `Integrate External Assets`, `HQ Pack Optimizer`, `Setup Project`,
`Rebuild All Lab Scenes`.

**Der stillste Fallstrick:** `Bake Logo Into Scene` **speichert die Szene selbst**. Es ist gelb,
nicht rot, aber es ist der einzige Punkt, nach dem Ungespeichertes weg ist.
