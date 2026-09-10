# Modelle dezimieren

Die Modelle dieses Projekts sind rohe Photogrammetrie-Exporte. Jeder Prop hat rund
1,5 Millionen Dreiecke — ein Kerzenhalter, ein Wählscheibentelefon, ein Tisch —
und `01_MainMenu` nennt zusammen 24,7 Millionen. Ein iPhone beendet den Prozess
beim Laden, statt das zu stemmen.

**Unity kann das nicht.** Der Model-Importer hat keine Polygonreduktion; es gibt
keine Einstellung in der `.meta`, die eine Dreieckszahl senkt. Deshalb Blender.

## Ausführen

```
blender --background --python Tools/Decimate/decimate_models.py
```

Kein Add-on, keine Szene, kein Handgriff. Blender ≥ 3.0 genügt; nur der
Dezimierer wird gebraucht.

Vorher committen — das Skript überschreibt die FBX an Ort und Stelle, und
`git checkout` ist dann die Rückfahrkarte.

## Was es anfasst und was nicht

Es überschreibt **nur** die `.fbx`. Die `.meta` daneben bleibt unberührt, also
bleibt die guid gleich, also zeigt jede Szene, jedes Prefab und jedes Material
danach auf genau dasselbe wie vorher. Eine `.meta` zu löschen, um einen Import zu
„erneuern", zerreißt genau diese Verbindung.

## Was es misst statt anzunehmen

Ein Umweg über Blender kann Maßstab oder Achse verändern, und ein Prop, der
hundertmal zu groß oder umgekippt zurückkommt, ist ein schlimmeres Problem als
das, was behoben werden sollte. Das Skript misst deshalb die Bounding Box vor und
nach der Dezimierung und **verweigert** das Schreiben, wenn sie um mehr als ein
Zehntelprozent driftet — die Datei bleibt dann unverändert, statt falsch
geschrieben zu werden. Dieses Projekt hat sich an einer ungeprüften Größe schon
zweimal die Finger verbrannt (CLAUDE.md Fehler 12 und 29).

## Die Ziele

Sie stehen als Tabelle oben im Skript und sind pro Modell einzeln einstellbar.
Ein Ziel auf `0` überspringt das Modell.

| Art | Ziel |
|---|---|
| Architektur, immer im Bild (Korridor) | 20 000 |
| Gerät in der Hand (EMF, DOTS, UV-Licht) | 6 000 |
| Raum-Prop auf 1–3 m | 2 000 – 6 000 |
| Geist (Silhouette, kurz und dunkel) | 8 000 |

Das wirkt wenig gegen 1,5 Millionen. Es ist es nicht: die Normal Map trägt genau
die Details weiter, die die Silhouette verliert, und alle diese Props haben eine.

Erwartetes Ergebnis für `01_MainMenu`:

```
24 658 861  ->  ~70 000 Dreiecke     (352x)
    846 MiB ->  ~10 MiB Vertexspeicher
```

## Danach

1. `Scripts/check_scene_budget.sh` — die neue Zahl je Szene ablesen
2. Die Zeilen in `Scripts/scene_budget.txt` auf die neuen Werte senken
   (der Wächter schlägt nur bei WACHSTUM an, das Senken ist Handarbeit)
3. Unity öffnen, reimportieren lassen, und **jedes Modell ansehen**, bevor
   committet wird. Eine Dezimierung kann ein Modell auch verunstalten, und das
   sieht man nur, indem man hinsieht.

Wenn ein Prop danach schlecht aussieht: sein Ziel im Skript erhöhen und nur ihn
noch einmal laufen lassen — die anderen Einträge auf `0` setzen.

## NICHT GETESTET

Dieses Skript ist hier geschrieben und nie gelaufen: auf dieser Maschine gibt es
weder Blender noch Unity. Der erste Durchlauf ist der erste Test.
