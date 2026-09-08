# Lobby-Politur — Audit und Eingriffe

Quelle: `Assets/CatchIfYouCan/Scenes/01_MainMenu.unity` auf Commit **93eaa10**, aus der
Szenendatei geparst. Keine Zahl in diesem Dokument stammt aus einem früheren Bericht.

Umfang der Datei: **439 Dokumente** — 98 GameObjects, 64 Prefab-Instanzen, 15 Lichter,
8 Collider, 42 MonoBehaviours.

---

## 1. Der Befund, der alles andere einordnet

**`MainMenu_Lobby` ist in der Szenendatei INAKTIV** (`m_IsActive: 0`).

Das ist kein Versehen und darf nicht „repariert" werden. `MainMenuModeController` schaltet die
eine Wurzel zur Laufzeit ein; wäre sie beim Laden schon aktiv, hätte ihr Mondlicht bereits die
Sonne der Szene beansprucht und ihre Emitter liefen über dem Menü — Unity legt nicht fest, ob
der Controller sein `Awake` vor oder nach ihnen bekommt.

Darin hängen 16 Objekte: `Lobby_MirrorCorner`, `Lobby_InvestigationBoard`, `Lobby_Portal`,
`Lobby_PlayerSpawn`, `Lobby_SafetyFloor`, `Lobby_KeyLight`, `Lobby_LampWarm`, `Lobby_FillCool`,
`Lobby_MoonShaft`, `Lobby_Exterior` (mit `Lobby_WindowMoonlight`), `Lobby_AntiqueTable`,
`Lobby_Armchair`, `Lobby_Window_Blocker`, `Lobby_PostProcessing`, `Lobby_Ambience`.

**Praktische Folge:** Jedes Werkzeug, das diese Objekte sucht, muss die Szene durchlaufen.
`GameObject.Find` überspringt inaktive Objekte — es fände genau nichts. Beide neuen Werkzeuge
benutzen `FindObjectsByType(FindObjectsInactive.Include, …)`.

## 2. Die von Hand gesetzten Objekte stehen auf Wurzelebene, nicht in der Lobby

| Objekt | Weltposition | Skalierung | Collider |
|---|---|---|---|
| `sofa1` | (11.53, 0.65, −3.15) | 0.09 | **keiner** |
| `sofa1 (1)` | (0.02, 0.04, 0.00) | 0.09 | **keiner** |
| `sofa1 (2)` | (0.02, 0.13, −0.00) | 0.09 | **keiner** |
| `floor lamp` | (9.53, 0.00, −2.98) | 1.0 | **keiner** |
| `table 4` | (17.96, 0.00, 0.81) | 1.0 | **keiner** |
| `cupboard 7 ` | (1.15, 0.00, −3.75) | 2.6 | **keiner** |
| `telescope` | (18.05, 0.00, 2.48) | 0.04 | **keiner** |
| `Carpet` | (11.27, 0.06, −1.31) | 0.08 | **keiner** |
| `newspapers` | (−1.48, −2.32, 4.01) | 1.0 | **keiner** |
| `Meshy_AI_cork_bulletin_board…` | (19.15, 1.77, −2.25) | 1.0 | **keiner** |

**Zwei Sofas stehen im Weltursprung** (`sofa1 (1)` und `(2)` bei ≈ 0,0,0) — 11 m neben dem
dritten. Nicht angefasst: der Auftrag erlaubt nur eine einzige Löschung, und das ist die
Stehlampe. Gemeldet, damit es nicht untergeht.

## 3. Kollision: acht Collider in der ganzen Szene, keiner an einer Wand

| Objekt | Typ | Bemerkung |
|---|---|---|
| `FLOOR_Lobby_01` | Box | 19.4 × 0.015 × 31.5 m — der Boden, trägt den Spawn |
| `Lobby_SafetyFloor` | Box | 40 × 1 × 40 m bei y = −3 |
| `HQ_CEILING_MEASURE` | Box | bei y = 11.92 |
| `Door_Green_Portal_Cube` | Box | Menü-Korridor |
| `Lobby_Window_Blocker` | Box | |
| `Lobby_Armchair` | Box | 0.87 × 0.86 × 0.88 |
| `Lobby_AntiqueTable` | Box | 0.66 × 0.30 × 0.36 |
| `Lobby_InvestigationBoard` | Box | 2.34 × 1.64 × **0.12** |

**Kein einziges Wandteil unter `HQ_ROOM_SCALE_ROOT` hat Kollision.** Der Spieler läuft durch
jede Wand und durch alle zehn oben genannten Möbel.

### Warum die Wandpositionen nicht aus den Transforms ablesbar sind

```
'2 '   Weltposition (7.28, 6.07, -61.64)
'3 '   Weltposition (7.57, 4.58, -59.75)
'12'   Weltposition (-2.91, 5.06, -71.97)
```

Diese Teile stehen sichtbar in der Lobby, ihr Prefab-Ursprung aber bei z ≈ −60. Das ist der
gemessene Pivot-Versatz des Pakets (bis 40 m). Andere Teile — die ganze `4 `-Reihe bei
z = −4.73 und z = +5.47, in Schritten von 2.48 m — stehen wirklich dort, wo ihr Transform sagt.

**Deshalb hängt der neue Collider am Renderer, nicht am Prefab-Root.** `mesh.bounds` ist relativ
zu genau dem Transform, der den Collider trägt; die Weltlage muss nie bekannt sein. Damit
verschwindet das Pivot-Problem, statt umgangen zu werden.

## 4. Beleuchtung: vier Richtungslichter auf einmal

| Licht | Typ | I | Range | Farbe |
|---|---|---|---|---|
| `Directional Light` (die Sonne der Szene) | Directional | 0.05 | — | (0.74, 1.00, 0.92) |
| `Area Light` (heißt so, ist Directional) | Directional | 0.97 | — | (0.11, 0.08, 0.03) |
| `Lobby_KeyLight` | Directional | 0.50 | — | **(0.66, 0.64, 0.61) neutralgrau** |
| `Lobby_MoonShaft` | Directional | 1.60 | — | (0.38, 0.50, 0.82) |
| `Lobby_LampWarm` | Spot | **17.4** | 9 | **(1, 1, 1) reinweiß** |
| `Lobby_FillCool` | Point | 1.50 | **10.5** | **(0.48, 0.60, 1.00) kräftig blau** |
| `Lobby_WindowMoonlight` | Point | 0.60 | 6.5 | (0.34, 0.47, 0.72) |

Sobald die Lobby eingeschaltet wird, leuchten **vier Richtungslichter gleichzeitig**. Zusammen
mit einem blauen Point Light über 10.5 m ist das genau die flache, kühle Ausleuchtung, die der
Auftrag ausschließt — und `Lobby_LampWarm` ist trotz seines Namens reinweiß.

## 5. Die obsolete Stehlampe — bewiesen

**Sie steht in keiner Szene.** Die Suche über alle 98 GameObjects findet kein einziges Objekt,
dessen Name „Lamp" enthält, außer dem Licht `Lobby_LampWarm`.

Sie entsteht zur Laufzeit:

```
Assets/CatchIfYouCan/Scripts/Art/MirrorCorner.cs
  Zeile 172   [SerializeField] private bool buildLamp = true;
  Zeile 310   if (buildLamp)
  Zeile 311       BuildLamp(lit);
  Zeile 523   var lamp = new GameObject("Standing_Lamp");
```

`BuildLamp` baut drei `PrimitiveType.Cylinder` — `Base`, `Pole`, `Shade` — plus ein Kind `Bulb`
mit einem **Point Light, Intensität 6.5, Reichweite 7 m**, als Kind von `Lobby_MirrorCorner`.

**Und der Code-Default ist nicht der wirksame Wert.** Die Szene serialisiert `buildLamp: 1` auf
der MirrorCorner-Komponente; der Szenenwert schlägt den Feldinitialisierer. Beide wurden
geändert.

---

## 6. Was geändert wurde

### In der Szene — elf Zeilen, keine neuen Dokumente

Nachgezählt: vorher 439 Dokumente, nachher 439. Keine erfundene fileID, kein Graph-Umbau.

| Feld | vorher | nachher | Begründung |
|---|---|---|---|
| `buildLamp` (MirrorCorner) | `1` | `0` | die obsolete Stehlampe |
| `m_SkyboxMaterial` (RenderSettings) | `{fileID: 0}` | `MAT_Skybox_HauntedNight` | war leer → Unitys blauer Prozedurhimmel |
| `Lobby_KeyLight` Farbe | (0.66, 0.64, 0.615) | (0.55, 0.40, 0.24) | Neutralgrau ist das „Bürolicht" |
| `Lobby_KeyLight` Intensität | 0.5 | **0.18** | ein Richtungslicht im Innenraum darf kaum etwas tun |
| `Lobby_FillCool` Farbe | (0.477, 0.604, 1.0) | (0.30, 0.38, 0.62) | bleibt kühler Gegenpol, hört auf zu dominieren |
| `Lobby_FillCool` Intensität | 1.5 | **0.45** | |
| `Lobby_FillCool` Reichweite | 10.5 | **8** | füllte den ganzen Raum |
| `Lobby_LampWarm` Farbe | (1, 1, 1) | (1.0, 0.78, 0.52) | reinweiß ist ausdrücklich ausgeschlossen |
| `Lobby_LampWarm` Intensität | 17.4 | **12** | der Rest ist jetzt dunkler |

**Skybox-Pfad:** `Assets/CatchIfYouCan/Resources/Sky/MAT_Skybox_HauntedNight.mat`
(guid `018380ecde48a416aa034115625044b7`, Skybox/Panoramic). Kein neuer Himmel erzeugt, kein
Paket importiert, das Material selbst unverändert.

`Lobby_MoonShaft`, `Lobby_WindowMoonlight`, alle Kerzen-, Korridor- und Ereignislichter:
**unverändert**. Mondlicht durchs Fenster ist gewollt und lokal.

### Im Code

`MirrorCorner.buildLamp` Standardwert `true` → `false`, damit eine neu angelegte Komponente
die Lampe nicht zurückbringt. `buildFill` bleibt an — das ist nicht die Lampe, sondern das, was
ein Gesicht im Spiegelglas überhaupt lesbar macht.

### Zwei neue Editor-Werkzeuge

Beides erzeugt oder verschiebt Szenenobjekte, und das Licht wird Kind einer **Prefab-Instanz**.
Von Hand in die YAML geschrieben wären das erfundene fileIDs und eine
`m_AddedGameObjects`-Änderung — genau der Graph, vor dem CLAUDE.md warnt. Über `Undo` im Editor
ist es ein Schritt, den ein Strg+Z zurückholt.

**`1. LOBBY > Kollision pruefen [NUR LESEN]`** und **`Kollision setzen [UNDO]`**

* Architektur unter `HQ_ROOM_SCALE_ROOT` → **nicht-konvexer MeshCollider**. Eine Wand dieses
  Pakets ist *ein* Mesh mit einem Loch; die Türöffnung ist Teil der Geometrie. Eine Box darüber
  mauert die Tür zu — genau die Warnung im Auftrag. Nicht-konvex auf statischer Geometrie ohne
  Rigidbody kostet nichts pro Frame.
* Freistehende Möbel → **BoxCollider** aus `mesh.bounds`, im eigenen Raum gemessen mal
  `lossyScale`, nie als Welt-AABB (CLAUDE.md Fehler 12).
* Alles unter 0.30 m wird übersprungen.
* Objekte mit vorhandenem Collider werden **nicht** verdoppelt.
* **Die Portalsperrzone wird beim Portal erfragt**, nicht abgeschrieben:
  `LobbyPortal.OpeningSize` und `MaxWallThickness` sind die eine Quelle. Der Test läuft im Raum
  des Portals, nicht als weltachsenparallele Box — das Portal steht um 180° gedreht, und bei
  einer anderen Drehung wäre eine Weltbox entweder zu groß oder zu klein. Wird kein Portal
  gefunden, wird **nichts** gesetzt.

**`1. LOBBY > Sofa-Lampe beleuchten [UNDO]`**

Misst die `floor lamp`, hängt ein `SofaLamp_Practical` knapp unter ihre Oberkante — im Schirm,
nicht darüber. Point Light, warm (1.0, 0.72, 0.42) ≈ 2400 K, Intensität 2.6, Reichweite 4.5 m,
**ohne Schatten** (Mobile). Das Modell wird nicht bewegt. Ein zweiter Aufruf tut nichts.

**`1. LOBBY > Spiegel und Brett an die Wand [UNDO]`**

Strahl von der Objektposition entgegen seiner Blickrichtung, bis eine Wand getroffen wird; dann
so gesetzt, dass die **Rückseite** die Wand berührt (Spiegelrahmen 0.05 m, Brett 0.12 m aus
seinem BoxCollider). Drehung bleibt, wie der Nutzer sie gesetzt hat.

**Reihenfolge:** erst `Kollision setzen`, dann `Spiegel und Brett` — ohne Wandcollider findet
der Strahl nichts, und das Werkzeug sagt das dann auch.

---

## 7. Wo ich gestoppt habe

**Der Spiegel und das Brett wurden nicht verschoben.** Beide bauen ihre Geometrie in `Start()`,
haben im Editor also nichts zu messen, und die Wände haben noch keine Collider. Die Zahlen
stehen fest:

* `Lobby_MirrorCorner` — (15.02, 0.00, −2.00), rotY 0, Glas 0.72 × 1.40 bei lokal y = 1.25
* `Lobby_InvestigationBoard` — (15.02, 0.00, +2.00), rotY 180, Box 2.34 × 1.64 × 0.12 bei y = 1.55

Die nächsten Wandreihen liegen bei z ≈ −4.73 und z ≈ +5.47. Der Spiegel steht damit rund 2.7 m
frei im Raum, das Brett rund 3.5 m. Das Werkzeug richtet beides aus, sobald die Wände Kollision
haben — gemessen statt geraten.

**`check_ui_and_portal` bleibt bei 233/2, unverändert.** Beide Fehler bestanden schon vorher und
werden hier gemeldet statt versteckt:

1. *„das Portal hat eine Wand, in die es schneiden kann"* — `LobbyPortal.ResolveWall` sucht per
   `OverlapBox` einen Collider, der höchstens `maxWallThickness` dick und mindestens
   4.70 × 2.40 m groß ist. Ein Wandmodul des Pakets ist ~2.48 m breit und fällt durch. **Auch
   nach dem Setzen der Kollision bleibt das so** — die Module sind zu schmal. Das Portal
   braucht entweder ein zugewiesenes `wallCollider` oder eine durchgehende Wand.
2. *„die Fensterwand im Osten ist unangetastet"* — aus dem Lobby-Umbau, unverändert.

## 8. Was nicht angefasst wurde

Portal (Position, Öffnung, Kameras, Material, Handoff), Spiegel-Reflexionslogik,
Brett-Inhaltslogik, Player/Inventar/Netzwerk/Generator, Nathan-Materialien,
CorkBulletinBoard-Materialien, **jede gekaufte Quelldatei des HQ-Pakets**. Keine
Prefab-Verknüpfung getrennt, keine Prefab-Override auf ein Vendor-Prefab angewandt, keine
Vendor-Prefabs entpackt.

## 9. Status

| | |
|---|---|
| **Umgesetzt** | Skybox, drei Lichtwerte, obsolete Lampe (Szene + Code), zwei Werkzeuge |
| **Automatisiert geprüft** | Offline-Typecheck 19 Fehler = Baseline; alle Guards grün außer den zwei vorbestehenden `check_ui_and_portal`-Fehlern; Szene 439 → 439 Dokumente |
| **In Unity geprüft** | **nichts.** Kein Editor verfügbar. Jede visuelle und Play-Mode-Aussage ist NICHT GETESTET. |

### Testablauf — NICHT GETESTET, das ist deiner

1. `01_MainMenu` öffnen. Erwartet: keine Compile-Fehler, Hierarchie unverändert,
   `MainMenu_Lobby` weiterhin inaktiv.
2. `1. LOBBY > Kollision pruefen [NUR LESEN]` — nur lesen. Erwartet: eine Liste; besonders
   ansehen, was als „in die Portaloeffnung ragend" übersprungen wird.
3. Wenn die Liste stimmt: `Kollision setzen [UNDO]`. Play. Erwartet: nicht durch Wände, nicht
   durch den Boden, **durch die Türöffnungen kommt man weiterhin**.
4. `Sofa-Lampe beleuchten [UNDO]`. Play. Erwartet: warme Insel auf Sofa, Wand und etwas Boden;
   Ecken bleiben dunkel; kein ausgebrannter Schirm.
5. `Spiegel und Brett an die Wand [UNDO]`. Erwartet: der Dialog nennt für beide die getroffene
   Wand mit Abstand. Ansehen, dann behalten oder Strg+Z.
6. Play und nach oben schauen: Nachthimmel statt Blau.
7. Konsole nach `Standing_Lamp` filtern. Erwartet: nichts.


---

# Nachtrag: die drei übersprungenen Wandteile sind die Portalwand

Der Lauf des Werkzeugs auf der Maschine mit dem Paket:

```
101 Objekte brauchen Kollision, 6 haben schon eine.
  Architektur (MeshCollider, nicht konvex): 53
  Moebel (BoxCollider aus gemessenen Mesh-Bounds): 45
  UEBERSPRUNGEN, weil sie in die Portaloeffnung ragen (3):  4 (8)   4 (15)   4 (14)
  Zu klein oder ohne Mesh, uebersprungen: 4
```

53 + 45 + 3 = 101. Die Zahlen gehen auf.

**Eine Korrektur an meinem Audit:** ich hatte acht Collider aus der Szenendatei gezählt, das
Werkzeug meldet sechs. Kein Widerspruch — drei davon (`Lobby_Armchair`, `Lobby_AntiqueTable`,
`Lobby_InvestigationBoard`) haben im Editor keinen Renderer, weil sie ihre Geometrie erst in
`Start()` bauen, und das Werkzeug läuft über Renderer. Umgekehrt sieht es Collider, die **in**
einem Prefab stecken und in der Szenendatei gar nicht auftauchen. Für Kollision ist das Werkzeug
die verlässlichere Quelle, nicht die YAML.

## Was die drei Teile sind

`4 (8)`, `4 (14)` und `4 (15)` stehen laut Audit bei z = 5.03 bzw. 5.48 und x = 16…19. Das
Portal steht auf **(20.00, 0.00, 4.97)**. Das sind also nicht drei zufällige Wandstücke in der
Nähe der Öffnung — **das ist die Wand, in die das Portal schneidet.**

## Warum sie keinen eigenen Collider bekommen dürfen

`LobbyPortal` hält seine Wand in genau einem Feld und schaltet sie um:

```csharp
private void SetWallOpen(bool open)
{
    if (_wallSolid != null) _wallSolid.enabled = !open;   // EIN Collider
    if (_aperture  != null) _aperture.SetActive(open);    // vier Streifen um das Loch
}
```

Geschlossen ist die Wand massiv; offen wird sie abgeschaltet und `Portal_WallAperture` mit
`Wall_Left`, `Wall_Right`, `Wall_Header`, `Wall_Sill` tritt an ihre Stelle. **Ein Portal kann
genau ein Collider öffnen.** Hätten die drei Module je ein eigenes, könnte das Portal sie nicht
abschalten — der Spieler stünde vor einer unsichtbaren Wand, obwohl die Tür sichtbar offen ist.
Das ist derselbe Fehler wie der alte Primitiv-Türblocker, nur an der Haustür.

Das Überspringen war also nicht Vorsicht, sondern die richtige Antwort.

## Und es ist derselbe Befund wie der rote Guard

`check_ui_and_portal` meldet seit dem Lobby-Umbau: *„das Portal hat eine Wand, in die es
schneiden kann"* — `ResolveWall` sucht per Form nach **einem** Collider, der höchstens
`maxWallThickness` dick und mindestens 4.70 × 2.40 m groß ist. Ein Wandmodul des Pakets ist
schmaler, und drei nebeneinander addieren sich nicht zu einem. Kein Fehler im Portal: eine Folge
des Umbaus von der erzeugten Hülle auf gekaufte Module.

## Der Handgriff

**`Catch If You Can > 3. PORTAL > Portalwand setzen [UNDO]`**

Das Werkzeug findet die Renderer, die die Öffnung überlappen — dieselbe Prüfung, mit der das
Kollisionswerkzeug sie übersprungen hat, damit beide dieselben Teile meinen —, misst ihre
gemeinsame Ausdehnung **in den Achsen des Portals** und baut daraus ein einziges
`Lobby_PortalWall_Solid`: ein BoxCollider ohne Renderer, als Geschwister des Portals, damit es
mit der Lobby ein- und ausgeschaltet wird. Dann trägt es sich als `wallCollider` am Portal ein.

Vorher laufen die drei Tests, die `ResolveWall` anwendet. Ist die Wand zu schmal, zu niedrig oder
zu dick — letzteres heißt: die Module stehen nicht in einer Ebene —, wird **nichts** gesetzt und
die Zahl genannt. Eine Wand, die das Portal ohnehin ablehnen würde, entsteht gar nicht erst.

`LobbyPortal.EditorAssignWallCollider` ist eine öffentliche `#if UNITY_EDITOR`-Methode, keine
Reflection in das private Feld. Sie ändert nichts an Lage, Öffnung, Kameras oder Material des
Portals — sie füllt genau das Feld, das das Portal in seiner eigenen Fehlermeldung verlangt.

## Reihenfolge

1. `1. LOBBY > Kollision setzen` — erledigt.
2. `3. PORTAL > Portalwand messen [NUR LESEN]` — was steht wirklich an der Öffnung.
3. `3. PORTAL > Portalwand setzen [UNDO]`.
4. `1. LOBBY > Spiegel und Brett an die Wand [UNDO]` — braucht die Wandcollider aus Schritt 1.
5. `1. LOBBY > Sofa-Lampe beleuchten [UNDO]`.

Nach Schritt 3 sollte `check_ui_and_portal` von 233/2 auf 234/1 gehen — der verbleibende Fehler
ist die gelöschte Ost-Fensterwand. **NICHT GETESTET**, das ist eine Vorhersage aus dem
Guard-Text, keine Messung.


---

# Nachtrag 2: alle Wände bekommen Kollision — und das Portal wird wieder sichtbar

Zwei Anweisungen, zwei getrennte Ursachen.

## „aktuell sieht man kein Portal" — das war meine letzte Änderung

Die Kette ist lückenlos, und ihr Ende sieht nach einem Portalfehler aus, während ihr Anfang ein
Schalter in einer ganz anderen Datei ist:

```
generateWorld = false                          (InvestigationBootstrap, von mir gesetzt)
  -> PrepareWorld baut kein Haus, _generatedHouse bleibt null
  -> EnsureMissionEntryAnchor gibt sofort auf: "No generated house"
  -> MissionEntryAnchor bleibt null
  -> LobbyPortal.OpenRoutine setzt 'bound' nie
  -> "if (_prepareFinished && !bound && t >= openDuration) break;"
  -> "if (!bound) DestabiliseRoutine()"   -> State = Failed
```

Auf dem Bildschirm: START INVESTIGATION gedrückt, und es erscheint kein Portal. Kein Fehler am
Portal — **ein Portal ist eine Tür, und eine Tür ohne Raum dahinter ist ein Bild.**

`generateWorld` steht wieder auf `true`. Der Schalter bleibt, er ist der Weg, Charakter,
Ausrüstung und Menü ohne Welt anzusehen — er ist nur mit einem Portal in der Lobby nicht
vereinbar. Der Zusammenhang steht jetzt als Kommentar am Feld, damit er beim nächsten Umlegen
nicht wieder gesucht werden muss.

## „die Wände brauchen alle eine Kollision ohne wenn und aber"

Umgesetzt — und der Grund, aus dem ich die drei zunächst übersprungen habe, ist mit behoben,
statt übergangen zu werden.

**Vorher:** `LobbyPortal` hielt seine Wand in *einem* Feld. Bekommen die drei Module in der
Öffnung je ein eigenes Collider, kann das Portal nur eines abschalten. Die anderen bleiben
massiv → unsichtbare Wand vor der offenen Tür.

**Jetzt:** `LobbyPortal.CollectExtraWallColliders()` sammelt beim Bauen der Apertur **jedes
weitere** Collider, das in der Öffnung steht, und `SetWallOpen` schaltet sie mit:

```csharp
if (_wallSolid != null) _wallSolid.enabled = !open;
for (int i = 0; i < _wallExtra.Count; i++)
    if (_wallExtra[i] != null) _wallExtra[i].enabled = !open;
if (_aperture != null) _aperture.SetActive(open);
```

Drei Feinheiten, jede aus einem Fehler, den sie verhindert:

* **Einmal beim Bauen der Apertur, nicht pro Frame.** Ein `OverlapBox` in `Update` wäre eine
  Physikabfrage je Bild für eine Lobby, die zwischen zwei Portalöffnungen stillsteht.
* **Ein Boden unter der Schwelle wird nicht mitgenommen.** Ihn abzuschalten, weil er die Schwelle
  streift, lässt den Spieler beim Durchgehen durch die Welt fallen — dieselbe Fehlerklasse wie
  die unsichtbare Wand, nur nach unten.
* **Die Nebencollider bekommen kein eigenes Loch.** Das schneidet die Apertur aus `_wallSolid`;
  zwei Löcher an derselben Stelle wären zwei Antworten auf dieselbe Frage.

Das Kollisionswerkzeug überspringt die drei deshalb nicht mehr. Es **nennt** sie weiterhin, weil
ihre Durchlässigkeit an einer Zusage hängt, die eine andere Datei einhält — und genau die Art
Verbindung bricht still.

**Drei neue Prüfungen** in `check_ui_and_portal.sh` (235 → 238), zwei Zahnproben bestanden.

## Erwartete Lage nach dem nächsten Lauf

| | |
|---|---|
| `1. LOBBY > Kollision setzen` erneut | die drei Wandteile bekommen jetzt auch Kollision |
| `3. PORTAL > Portalwand setzen` | nötig, damit `_wallSolid` überhaupt existiert und die Apertur entsteht |
| START INVESTIGATION | Portal sollte wieder erscheinen, da `generateWorld` wieder an ist |

**NICHT GETESTET** — kein Unity hier. Ohne `_wallSolid` gibt es keine Apertur und
`CollectExtraWallColliders` kehrt sofort zurück; die Portalwand ist also weiterhin der Schritt,
ohne den die Tür geschlossen bleibt.

---

# Nachtrag 3: ein kleiner Raum statt des Hauses, mit Rückweg

Auftrag: kein generiertes Haus, stattdessen ein kleiner Raum weit weg von der Lobby, durch den
das Portal führt — und wieder zurück.

## Zwei Fragen, zwei Felder

`generateWorld` hatte angefangen, zwei Dinge zu bedeuten. Jetzt beantwortet es nur noch eine:

| Feld | Frage |
|---|---|
| `generateWorld` | Wird überhaupt eine Welt gebaut? |
| `worldKind` | Welche — `TestRoom` (neu, Standard) oder `House`? |

„Nichts bauen, um den Charakter anzusehen" und „einen kleinen Raum statt des Hauses" sind
verschiedene Absichten; ein einzelner Schalter für beide hätte je nach Stellung zwei Bedeutungen.

## Der Raum ist ein Haus mit einem Raum

Das ist der ganze Trick und der Grund, warum kaum Code dazukam:

```csharp
_generatedHouse = new GeneratedHouse { Seed = …, Root = root.transform };
_generatedHouse.Rooms.Add(instance);
_generatedHouse.Entrance = instance;
```

Danach arbeitet **alles Bestehende unverändert weiter**: `EnsureMissionEntryAnchor` misst sich
gegen echte Bodenkollision in diesem Raum, `HouseLightingDirector` findet ihn,
`BuildNavigation` backt ihn, `HouseContentReport` läuft über ihn, und `EnterSeamlessAsync` trägt
den Spieler hinein wie in jeden anderen Raum. Ein Sonderfall wäre ein zweiter Weg durch dieselbe
Kette — und der eine, der seltener läuft, ist der, der kaputtgeht.

Gebaut wird mit demselben `ModularRoomBuilder`, den auch das Haus und `HQRoomAuthoring` benutzen,
aus einem `LayoutRoom` mit `archetypeId: "TEST_ROOM"`.

| | |
|---|---|
| Größe | 8 × 3 × 8 m (`testRoomSize`) |
| Position | **(0, 0, −150)** (`testRoomPosition`) |
| Kategorie | `LivingRoom` — damit die Einrichtung ein Profil findet |
| Layout-Hash | **unberührt.** Stage B von Anfang bis Ende |

**Warum so weit weg:** Während das Portal vorbereitet, sind Lobby und Mission *gleichzeitig*
geladen, und eine Physikabfrage ist global über alle geladenen Szenen. Zwei Räume, die sich
überlappen, sind zwei Böden untereinander — und die Lobby bringt einen 40 × 40 m
`Lobby_SafetyFloor` mit, der unter fast allem liegt. 132 m Abstand zur Lobby-Ausdehnung.

**Und in der richtigen Szene:** Der Raum hängt unter `worldRoot`, also in der Missionsszene.
`new GameObject` landet in der *aktiven* Szene, und die ist während einer Vorbereitung die Lobby —
der Raum hätte an einem Lobby-Objekt gehangen und wäre mit ihr entladen worden (CLAUDE.md
Fehler 17).

## Der Rückweg ist die Route, die es schon gibt

An der Nordwand steht ein leuchtendes Panel mit `LobbyReturnPoint`. **E** darauf löst genau die
zwei Zeilen aus, mit denen auch eine abgeschlossene Untersuchung zurückkehrt:

```csharp
MainMenuModeController.PendingEntryMode = MainMenuEntryMode.DirectLobby;
SceneLoader.Instance.LoadMainMenu();
```

Der Menü-Controller kommt dann **direkt im Raum** hoch — kein Kino, kein „tap to start". Eine
eigene Rückkehr zu bauen wäre ein zweiter Weg in dieselbe Szene, und die beiden gingen beim
ersten Umbau auseinander.

Drei Feinheiten:

* **E, kein Trigger.** Ein Trigger vor einem Rückweg heißt, dass jeder Schritt rückwärts die
  Szene wechselt — und die Nordwand ist genau die, vor der man nach dem Durchgang steht.
* **Einmal.** Zweimal drücken während des Ladens wäre ein zweiter Ladevorgang auf eine Szene, die
  gerade verschwindet.
* **Ohne `SceneLoader` wird die Absicht zurückgenommen**, sonst überspränge sie bei irgendeinem
  späteren Ladevorgang ein Intro, das jemand sehen wollte.

Das Panel geht durch `Art.PrimitiveSurface`: fehlt der Shader, wird der Renderer abgeschaltet
statt Unitys Built-in-Material zu zeichnen, das unter URP magenta ist.

## Neun neue Prüfungen

`check_ui_and_portal.sh` 238 → **247**, vier Zahnproben bestanden (Szenenzugehörigkeit, Abstand
zur Lobby, zurückgenommene Absicht, kein Magenta).

## Status

| | |
|---|---|
| **Umgesetzt** | `worldKind`, Testraum, Rückweg-Panel, neun Guards |
| **Automatisiert geprüft** | Typecheck 19 = Baseline; alle Guards grün außer den zwei vorbestehenden `check_ui_and_portal`-Fehlern |
| **In Unity geprüft** | **nichts** |

### Testablauf — NICHT GETESTET

1. `3. PORTAL > Portalwand setzen [UNDO]` — **weiterhin nötig**, sonst gibt es keine Apertur und
   die Tür bleibt zu.
2. Play, START INVESTIGATION. Erwartet: Portal öffnet und zeigt einen kleinen Raum.
3. Durchgehen. Erwartet: du stehst in einem 8 × 8 m Raum bei z ≈ −150.
4. Vor das leuchtende Panel an der Nordwand, **E**. Erwartet: zurück in der Lobby, direkt im
   Raum, ohne Kino.
5. Konsole nach `[CIYC][TestRoom]` filtern — Größe, Position und Szenenname stehen dort.

Das Haus ist nicht weg: `worldKind` auf `House` stellen, und aus demselben Seed entsteht exakt
dasselbe Haus wie vorher.

---

# Nachtrag 4: das Portal an die richtige Wand, und ein Brett statt zweier

## Warum die Portalwand abgelehnt wurde — die Messung war richtig

```
Wandteile an der Oeffnung: 2      4 (15)   4 (8)
Breite 2.56 m   (Oeffnung 4.70 m)
Hoehe  3.04 m   (Oeffnung 2.40 m)
Dicke  2.56 m   (erlaubt bis 1.00 m)
```

Beides stimmt und beides hat dieselbe Ursache. Aus dem Audit:

| Teil | Position |
|---|---|
| `4 (8)` | (18.89, −0.05, **5.03**) |
| `4 (15)` | (18.65, −0.04, **5.48**) |
| `4 (12)` | (11.20, −0.05, **5.47**) |
| `4 (13)` | (13.68, −0.04, **5.48**) |

`4 (8)` steht **45 cm vor** der Wandebene — daher „Dicke 2.56". Und zwei Teile, von denen eines
quer steht, sind zusammen nur 2.56 m breit statt der nötigen 4.70.

`4 (12)` und `4 (13)` liegen dagegen **beide** auf z ≈ 5.47, 2.48 m auseinander, und decken
zusammen rund 9.9 bis 15.0 m — knapp 5 m, genug für die Öffnung. Das ist die Wand.

Das Portal steht auf x = 20; die Mitte zwischen `4 (12)` und `4 (13)` liegt bei x ≈ 12.4. Es
stand also rund 7.5 m zu weit außen.

## `3. PORTAL > Portal an ausgewaehlte Wand setzen [UNDO]`

**Die Wand wählt der Mensch, die Zahlen misst das Werkzeug.** Welche Wand ein Portal bekommt, ist
eine Entscheidung über den Raum — vor welchem Fenster man stehen soll, wohin man blickt. Das steht
in keiner Datei. Wo diese Wand steht, wie dick sie ist und wohin sie schaut, steht in der
Geometrie.

Ablauf: `4  (12)` und `4  (13)` im Hierarchie-Fenster auswählen, Menüpunkt aufrufen. Das Werkzeug

* misst die gemeinsamen Bounds und nimmt die **dünnste Achse** als Wandnormale,
* lehnt ab, wenn die dünnste Achse die **Höhe** ist — das wäre ein Boden, und ein Portal im Boden
  ist ein Loch,
* bestimmt die **Innenseite am `Lobby_PlayerSpawn`**: eine Normale hat zwei Richtungen, und die
  falsche dreht das Portal nach außen — genau das, was passiert ist,
* setzt das Portal mittig auf Bodenhöhe, 2 cm vor die Wandfläche,
* und ruft direkt `Portalwand setzen` auf, damit die Öffnung nicht ohne Loch dasteht.

## Das Ermittlungsbrett ist die Funktion, nicht das Modell

`LobbyInvestigationBoard` baute bisher **beides**: eine Platzhalter-Tafel aus drei Quadern *und*
den Zugang zu `LobbyBoardUI` — Einzelspieler, Mehrspieler, Missionswahl. Das Modell ist ersetzbar
und ersetzt; der Zugang ist es nicht.

Neu: `useExistingModel`. Ist es an, wird **weder** Platzhaltergeometrie gebaut **noch** ein
Prefab instanziiert — die Komponente macht nur noch das, was sie wirklich beiträgt. Der
Unterschied zu `boardPrefab` ist wichtig: dort wird ein Prefab *als Kind* erzeugt, hier **ist**
das Objekt schon das Brett. Beides zu verwechseln ergibt zwei Bretter am selben Fleck, und das
zweite verdeckt das erste gerade so weit, dass es wie ein Materialfehler aussieht.

**`1. LOBBY > Ermittlungsbrett auf das Korkbrett [UNDO]`** setzt die Komponente auf das
ausgewählte Objekt (sonst auf das mit `Meshy_AI_cork_bulletin_board` im Namen), schaltet
`useExistingModel` ein und **löscht das alte Objekt** — es ist ein leerer Halter, der seine Tafel
erst in `Start()` baut; ohne die Komponente stünde dort ein unsichtbares Objekt mitten im Raum.
Ein zweites Brett wird abgelehnt: zwei Komponenten wären zwei Zugänge zum selben Panel.

Der Interaktionskörper entsteht um die **gemessenen** Renderer und wird in deren eigenen Raum
zurückgerechnet — `BoxCollider.center` und `.size` sind lokal, eine Weltgröße dort wäre durch die
Objektskalierung ein zweites Mal skaliert (CLAUDE.md Fehler 12). Bringt das Modell schon einen
Collider mit, wird keiner dazugebaut.

## Sechs neue Prüfungen

`check_ui_and_portal.sh` 245 → **251**, drei Zahnproben bestanden.

## Reihenfolge zum Testen — NICHT GETESTET

1. `4  (12)` und `4  (13)` auswählen → `3. PORTAL > Portal an ausgewaehlte Wand setzen [UNDO]`.
   Der Dialog nennt die gemessene Breite, Höhe und Dicke; erwartet: Breite ≈ 5 m, Dicke < 1 m.
2. `1. LOBBY > Ermittlungsbrett auf das Korkbrett [UNDO]`.
3. Play → START INVESTIGATION → Portal an der richtigen Wand, Testraum dahinter.
4. Vor das Korkbrett stellen, **E** → Lobby-Panel öffnet.
