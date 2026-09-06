# Das Unity-Menü „Catch If You Can" — was gilt

**Normativ für die Menüstruktur.** Was vorher da war und warum umgebaut wurde, steht in
`Docs/EDITOR_MENU_INVENTORY.md`. Diese Datei sagt, was man klickt.

`Scripts/check_editor_menu.sh` prüft die Regeln unten und läuft in CI. 18 Prüfungen.

## Die eine Regel

**Jeder Menüpunkt trägt am Ende ein Etikett in eckigen Klammern.** Das Etikett sagt, was
passiert, nicht was das Werkzeug heißt.

| Etikett | Bedeutung |
|---|---|
| `[NUR LESEN]` | Ändert nichts. Klicken, ohne zu fragen. |
| `[EDITOR]` | Schaltet nur etwas für die Bearbeitung ein oder aus. |
| `[UNDO]` | Ändert die Szene, rückgängig machbar. |
| `[AENDERT SZENE]` | Ändert die Szene, nicht überall rückgängig. |
| `[OEFFNET SZENEN]` | Kann eine andere Szene öffnen — Ungespeichertes ist in Gefahr. |
| `[SCHREIBT ASSET]` `[SCHREIBT CODE]` `[SCHREIBT DATEIEN]` `[SCHREIBT SZENEN]` | Schreibt auf die Platte. |
| `[REIMPORT]` | Importiert Assets neu. Dauert. |
| `[MASSENAENDERUNG]` `[UEBERSCHREIBT]` | Kann große Teile des Projekts umschreiben. Fragt vorher. |

Nur die drei Build-Befehle tragen keins — ein Build schreibt ausschließlich in den Build-Ordner.

## Die sieben Gruppen

```
Catch If You Can/
├── Safe Inspection   14 Punkte, ausnahmslos [NUR LESEN]
├── Scene Authoring    6 Punkte, Szene bearbeiten
├── HQ Assets          5 Punkte, alles was das gekaufte Paket braucht
├── Portal             2 Punkte
├── Build              3 Punkte
├── Assets bauen      14 Punkte, schreibt Projekt-Assets
└── Debug and Legacy   7 Punkte, normalerweise nicht anfassen
```

**Sieben statt der sechs geforderten.** Für vierzehn Produktions-Werkzeuge, die Assets bauen
(Player-Prefab, Equipment, Geister, Audio, Nathan, Räume …), hatte keine der sechs Gruppen ein
Zuhause. Sie unter „Debug and Legacy" zu legen wäre falsch etikettiert, und im Wurzelmenü
liegenzulassen hätte den Zweck der Übung verfehlt. Also: `Assets bauen`.

**`Debug and Legacy`, nicht `Debug & Legacy`.** In einem Unity-`MenuItem` ist `&` das Zeichen für
den Alt-Modifikator einer Tastenkombination. Der Pfad hätte hier vermutlich funktioniert, aber ein
kaputtes Menü ist ein schlechterer Tausch als ein Buchstabe.

## Was man ohne Rückfrage klicken darf

Alles unter **Safe Inspection**. Das ist die einzige Zusage, auf die man sich verlassen muss, und
der Guard prüft sie: jeder Punkt dort trägt `[NUR LESEN]`, und keiner schreibt.

---

# Die Abläufe

## 1. Lobby bearbeiten

```
Scene Authoring/Lobby bearbeiten [EDITOR]
```

Schaltet `MainMenu_Lobby` ein, damit man den Raum sieht, und baut Vorschauen der vier Objekte,
die es im Editor sonst gar nicht gibt — Spiegel, Sessel, Tisch, Board werden erst in `Start()`
gebaut.

**Das ist nicht die Laufzeit-Initialisierung.** Zur Laufzeit schaltet
`MainMenuModeController.interactiveRoomRoots` den Raum ein und die vier Skripte bauen ihre
Geometrie selbst. Die Vorschau ruft **denselben** Builder, damit kein zweiter Raum entsteht, und
trägt `HideFlags.DontSave`, damit nichts davon in der Szenendatei landet. Beim Speichern und vor
Play wird der Raum wieder ausgeschaltet: **dass er in der Datei AUS ist, ist tragend** — sonst
läuft sein Mondlicht beim Szenenstart über dem Menü an.

Prüfen, was von Hand da ist und was erst zur Laufzeit entsteht:
`Safe Inspection/Authored Lobby pruefen [NUR LESEN]`.

## 2. Von Hand aus HQ-Teilen bauen

```
1.  Safe Inspection/HQ-Paket pruefen (Fenster) [NUR LESEN]   -> was ist drin
2.  HQ Assets/Bauteile pruefen und setzen [UNDO]             -> Teile in die Szene
3.  HQ Assets/Testraum bauen [AENDERT SZENE]                 -> eine 6x3x6-Zelle zum Vergleich
4.  HQ Assets/Testraum entfernen [UNDO]                      -> danach wieder weg
```

Der Katalog, aus dem der Testraum baut, kommt aus **einem** Werkzeug:

```
HQ Assets/Katalog schreiben (geprueft) [SCHREIBT ASSET]
```

Das Prüf-Fenster schreibt ihn **nicht mehr**. Es tat es einmal, aus einer Klassifikation nach
Dateinamen — und die findet in diesem Paket 3 von 105 Prefabs, weil es seine Prefabs
durchnummeriert und sein Glas `Steklo` nennt. Ein Klick dort hat den geprüften Katalog durch
einen schlechteren ersetzt, ohne zu warnen. Der Bericht bleibt, die Schreibfunktion ist entfernt.

## 3. Raumgröße

```
1.  Safe Inspection/Raumgroesse messen [NUR LESEN]
2.  Scene Authoring/Raum skalieren [UNDO]
```

**Messen** liest lichte Höhe, Außenmaß und jede Mesh-Quelle mit ihrer gesetzten Größe und
schlägt einen Faktor vor — Ziel geteilt durch gemessen, gegen `PlayerFactory.CapsuleHeight`
(1.86 m) und `EyeHeight` (1.68 m), die Zahlen, aus denen der Spieler wirklich gebaut wird.

**Skalieren** legt `HQ_ROOM_SCALE_ROOT` an, hängt die Raumteile um, setzt **einen** gleichmäßigen
Faktor und schiebt die Wurzel senkrecht, bis die Fußboden-Oberkante auf Y = 0 liegt. Danach misst
es **nach** und schreibt „erreicht" oder „NICHT ERREICHT".

Es bricht ab, wenn die lichte Höhe nicht mehr die gemessene ist, und wenn die Wurzel schon
existiert — ein zweiter Lauf würde den Faktor quadrieren.

**Die Reihenfolge gegenüber `Hierarchie sortieren` spielt keine Rolle mehr.** Gesucht wird
rekursiv durch die ganze Szene, oberster Treffer je Zweig, inaktive eingeschlossen. Vorher lief
die Suche nur über Szenen-Wurzeln, und das Sortieren hätte den Raum unauffindbar gemacht.

## 4. Portal

```
Portal/Portalwand messen [NUR LESEN]                        -> diagnostiziert
Portal/Gekauftes Portal-Paket uebernehmen [SCHREIBT]        -> ändert
```

**Nur diese beiden.** Das Messen sagt, welche Collider die Öffnung überdecken und warum
`ResolveWall` sie annimmt oder ablehnt; es ändert nichts. Das Übernehmen kopiert Artwork aus dem
gekauften Paket ins Projekt und setzt Felder am `LobbyPortal` — es ändert die **Optik**, nie die
**Form**.

`Raumgroesse messen` meldet zusätzlich die Folge fürs Portal: die Öffnung ist auf 4.70 × 2.40 m
festgelegt und gehört nicht zum Raum, schrumpft also nicht mit — die Wandmodule schon.

## 5. HQ-Paket importieren und optimieren

```
HQ Assets/Pack Optimizer [REIMPORT]
```

Ändert **ausschließlich** Textur-Importer: `maxTextureSize` (Standard und pro Plattform) und
`isReadable`, dann Reimport. Er ändert **kein** Material, Mesh, Prefab, keine Szene, keinen
Shader und keine Texturdatei.

Es fragt vorher und nennt: was sich ändert, wie viele Assets, dass reimportiert wird, dass keine
Szene gespeichert wird, und dass Abbrechen die sichere Antwort ist.

**Der Plan ist bereits vollständig angewendet** (388 von 388 Texturen). Ein zweiter Lauf hat
nichts zu tun und kostet einen Reimport des ganzen Pakets.

## 6. Wenn ein gekauftes Teil weiß aussieht

```
Safe Inspection/HQ Material-Doktor (Auswahl) [NUR LESEN]
```

Objekt auswählen, klicken. Es schlägt vor und wendet nichts an. Die erste Frage, die es stellt,
ist **welche Quelle** — ein Objekt, dessen Materialien alle in einer Modelldatei liegen, ist eine
FBX-Instanz, und deren eingebettete Materialien tragen in diesem Paket keine Textur. Die fertigen
Teile liegen daneben in `walls prefabs/`.

## 7. Die vier, die vorher fragen

```
HQ Assets/Pack Optimizer [REIMPORT]
Debug and Legacy/Integrate External Assets [MASSENAENDERUNG]
Debug and Legacy/Setup Project [MASSENAENDERUNG]
Debug and Legacy/Alle Lab-Szenen neu bauen [UEBERSCHREIBT]
```

Alle vier gehen durch **dieselbe** Bestätigung (`DangerousCommandGate`), die immer dieselben fünf
Dinge nennt: was sich ändert, wie viele Assets (oder dass die Zahl nicht vorher bekannt ist), ob
reimportiert wird, ob Szenen gespeichert werden, und dass Abbrechen die sichere Antwort ist.

Eine Einschränkung ehrlichkeitshalber: Unitys `DisplayDialog` lässt den Aufrufer **nicht**
bestimmen, welcher Knopf den Fokus hat. „Abbrechen ist Standard" ist deshalb so umgesetzt, wie es
wirklich hält — **jede** Antwort außer der ausdrücklichen Zustimmung bricht ab, und der
zustimmende Knopf heißt nach der Handlung („Ja, Import-Einstellungen ändern"), nicht „OK".

Für jeden künftigen Massen-Reimport gilt dieselbe Bestätigung. Der Guard prüft, dass es genau
**eine** Implementierung davon gibt.

---

# Regeln für weitere Werkzeuge

Diese gelten ab jetzt, und `check_editor_menu.sh` setzt die ersten drei durch:

1. **Kein neuer Menüpunkt außerhalb der sieben Gruppen.**
2. **Jeder Menüpunkt trägt ein Etikett.** Ohne Etikett fällt der Guard.
3. **Unter `Safe Inspection` steht nur, was nichts ändert.**
4. **Ein Teilsystem hat einen Produktionsweg.** Lieber ein bestehendes Werkzeug erweitern als ein
   zweites danebenstellen — dieses Projekt hatte schon zwei Taschenlampen, zwei Inventare und
   zwei Katalogschreiber.
5. **Ein Diagnosewerkzeug wird von Anfang an als solches gekennzeichnet**, nicht später
   einsortiert.
6. **Kein Werkzeug speichert die offene Szene selbst.** Es markiert sie als geändert und sagt es.

---

# Nachtrag: das Menü in seiner heutigen Form

Die sechs Gruppen oben sind ersetzt. Es sind jetzt diese, in dieser Reihenfolge:

```
Catch If You Can/
├── 1. LOBBY                    7   den Menü-Raum bearbeiten und messen
├── 2. HQ MODULAR HOUSE         7   alles, was das gekaufte Paket braucht
├── 3. PORTAL                   2
├── 4. SPIELINHALT             14   Characters · Equipment · Ghosts · Content
├── 5. BUILD                    3
└── 9. ENTWICKLER - DEBUG      20   Forensik, Determinismus, Migration, Legacy
```

53 Befehle. Zwei davon sind neu (`HQ-Massstab pruefen`, `Alle HQ-Bauteile auf Spielmass bringen`),
51 sind dieselben wie vorher, nur woanders.

**Zwei Punkte aus der Vorgabe gibt es nicht, und ich habe sie nicht erfunden:** „Portal prüfen"
und „Portal reparieren". Hinter „Portal prüfen" stünde dasselbe Werkzeug wie hinter
„Portalwand messen" — ein Duplikat. Ein „Portal reparieren" gibt es nicht: die fehlende Portalwand
ist ausdrücklich als eigene Aufgabe zurückgestellt, und ein Menüpunkt, hinter dem nichts steht,
ist schlimmer als keiner.

---

# Der HQ-Maßstab

## Die Zahl

**2.95 / 3.92 = 0.752551.** Gemessen, nicht gewählt: die von Hand gebaute Lobby stand auf 3.92 m
lichter Höhe und sollte 2.95 haben. Im Code steht der **Quotient**, nicht die gerundete Zahl, an
genau einer Stelle: `HQScale`. Der Raum-Skalierer, die Maßstabsprüfung, die Migration und der
Setz-Knopf lesen alle von dort. Vier Kopien einer Messung wären sich heute einig und liefen beim
nächsten Nachmessen auseinander — lautlos, weil jedes Werkzeug für sich stimmig bliebe.

## Architektur oder Möbel?

**Nach Ordner, nicht nach Dateiname.** Ein Namens-Klassifikator wurde an diesem Paket gemessen und
fand **3 von 105** Prefabs: es nummeriert seine Teile durch und nennt sein Glas `Steklo`. Die
Ordnerstruktur ist das, worin das Paket konsistent ist.

| gilt als Architektur | gilt als Möbel |
|---|---|
| `/moduls/`, `/walls prefabs/`, `/walls/`, `/architecture/`, `/wall panel`, `/plinth`, `/customization/` | `/props/`, `/furniture/`, `/library/`, `/decor/` |

Die Form ist **Bestätigung, nicht Entscheidung**: dünn in einer Achse, groß in den beiden anderen.
Wo Ordner und Form sich widersprechen — ein Bücherregal ist auch dünn, hoch und breit — heißt das
Ergebnis **UNKLAR** und das Teil wird nicht angefasst. Ein Möbelstück kann schon Realmaß haben;
eines zu verkleinern, das richtig war, sieht man nie.

**Das Portal ist ausgenommen.** Seine Öffnung ist eine Spielmaß-Größe, keine architektonische.

## Doppelskalierung

Entschieden wird auf **`lossyScale`**, nie auf `localScale`. Ein Vendor-Teil mit `localScale` 1 in
einem korrigierten Wrapper **ist bereits auf Spielmaß** — sein eigenes Feld sagt das Gegenteil.

Fünf Urteile:

| | |
|---|---|
| `[KORREKT ]` | trägt den Faktor selbst oder steht unter einer korrigierten Wurzel |
| `[ORIGINAL]` | Vendor-Größe, Ordner sagt Architektur, Form passt — **das wird umgestellt** |
| `[MOEBEL  ]` | wird nie automatisch verkleinert |
| `[UNKLAR  ]` | genannt, nicht geraten |
| `[DOPPELT?]` | unter einer korrigierten Wurzel mit abweichendem Maßstab — **bricht die Migration ab** |

Findet die Migration auch nur ein `[DOPPELT?]`, wendet sie **gar nichts** an: solange unklar ist,
was dort schon einmal angewendet wurde, ist jede weitere Anwendung geraten.

## Die drei Setz-Knöpfe

```
2. HQ MODULAR HOUSE/Bauteile ansehen und setzen [UNDO]
```

| Knopf | wofür |
|---|---|
| **Setzen auf CIYC-Spielmaß** | der Produktionsweg. Wrapper mit 0.752551, Pivot unten mittig |
| Setzen + Pivot fix (Vendor-Größe) | Wrapper mit Pivot-Korrektur, ohne Maßstab |
| Setzen ORIGINAL (nur zum Vergleichen) | das nackte Prefab. Forensik — passt nicht zum Raum |

Der Maßstab sitzt **immer auf dem Wrapper**. Das gekaufte Prefab darin behält seine eigenen
lokalen Werte und seine Prefab-Verbindung; nichts wird auf das Paket zurückgeschrieben.

## Der Ablauf für neue Teile

```
1.  2. HQ MODULAR HOUSE/HQ-Massstab pruefen [NUR LESEN]
2.  Teile setzen mit "Setzen auf CIYC-Spielmass"
3.  2. HQ MODULAR HOUSE/Alle HQ-Bauteile auf Spielmass bringen [UNDO]   für Altbestand
```

Schritt 3 misst **immer zuerst** und druckt die vollständige Tabelle in die Konsole. Erst danach
fragt es, mit den Stückzahlen im Dialog. Nichts ändert sich, bevor die Zahlen gelesen werden
konnten.
