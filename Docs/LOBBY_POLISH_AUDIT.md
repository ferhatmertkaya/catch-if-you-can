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
