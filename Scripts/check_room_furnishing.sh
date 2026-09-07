#!/usr/bin/env bash
#
# Die Einrichtung stellt Moebel hin - und nichts davon darf in die Generierung zurueckgreifen,
# den Weg zur Tuer verstellen oder wie fertiger Inhalt aussehen, ohne einer zu sein.
#
# Der Anlass ist belegt und steht in Docs/INVESTIGATION_GENERATOR_AUDIT.md: die Raeume waren
# leer und enthielten trotzdem dunkle Bloecke. ContentSnapshotFactory.Create liefert bei leeren
# Katalogen einen eingebauten Ersatz-Snapshot, Stage A plant daraus Moebel und faltet die
# Platzierungen in den Layout-Hash, und Stage B fand fuer keine davon eine PropDefinition -
# beide Kataloge sind seit dem Entfernen der Kenney-Inhalte leer (CLAUDE.md Fehler 14). Der
# PropSpawner baute deshalb je Platzierung einen 1x1x1-Wuerfel im dunklen Trim-Material.
#
# Braucht nichts ausser einer Shell und python3.

set -u
set -o pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "== Room furnishing guard =="
echo

python3 - "$ROOT" <<'PY'
import io, os, re, sys

root = sys.argv[1]
passed = failed = 0

def ok(msg):
    global passed
    passed += 1
    print("  ok    %s" % msg)

def bad(msg, detail):
    global failed
    failed += 1
    print("  FAIL  %s" % msg)
    if isinstance(detail, str):
        detail = [detail]
    for d in detail:
        print("        %s" % d)

def read(rel):
    try:
        return io.open(os.path.join(root, rel), encoding="utf-8", errors="replace").read()
    except IOError:
        return None

def code(rel):
    """Ohne Kommentare. Ein Guard, den ein Doc-Kommentar zufriedenstellt, ist keiner -
    in diesem Projekt schon zweimal passiert (CLAUDE.md Fehler 8)."""
    body = read(rel)
    if body is None:
        return None
    body = re.sub(r"/\*.*?\*/", "", body, flags=re.S)
    return "\n".join(l for l in body.split("\n") if not l.lstrip().startswith("//"))

FURN = "Assets/CatchIfYouCan/Scripts/Procedural/Furnishing/"
furnisher = code(FURN + "RoomFurnisher.cs") or ""
footprint = code(FURN + "RoomFootprint.cs") or ""
rng       = code(FURN + "FurnishingRandom.cs") or ""
cache     = code(FURN + "FurniturePrefabCache.cs") or ""
profiles  = code(FURN + "FurnishingProfiles.cs") or ""
shell     = code(FURN + "RoomShellInfo.cs") or ""
catalog   = code("Assets/CatchIfYouCan/Scripts/Content/RoomFurnishingCatalog.cs") or ""
spawner   = code("Assets/CatchIfYouCan/Scripts/Procedural/PropSpawner.cs") or ""
prim      = code("Assets/CatchIfYouCan/Scripts/Procedural/PrimitiveRoomFactory.cs") or ""
gen       = code("Assets/CatchIfYouCan/Scripts/Procedural/ProceduralHouseGenerator.cs") or ""
report    = code("Assets/CatchIfYouCan/Scripts/Procedural/HouseContentReport.cs") or ""

if not furnisher:
    bad("die Einrichtung existiert", "RoomFurnisher.cs fehlt")
    print()
    print("  %d passed, %d failed" % (passed, failed))
    sys.exit(1)

# ---- 1. der Wuerfel ist weg, und er kann nicht zurueck ----------------------------------------

if "CreateFallbackProp" not in prim and "CreateFallbackProp" not in spawner:
    ok("kein Wuerfel-Ersatz fuer ein fehlendes Moebelstueck")
else:
    bad("kein Wuerfel-Ersatz fuer ein fehlendes Moebelstueck",
        "CreateFallbackProp ist zurueck; sie baute den dunklen Block in jedem Raum")

# Der Spawner ueberspringt und zaehlt, statt zu erfinden. Beide Haelften, denn stilles
# Ueberspringen waere nur die halbe Verbesserung: dann waere unklar, warum nichts da ist.
if re.search(r"if \(definition == null \|\| definition\.Prefab == null\)", spawner) and \
        "_skipped++" in spawner and "uebersprungen" in spawner:
    ok("eine Platzierung ohne Prefab wird uebersprungen und gezaehlt, nicht erfunden")
else:
    bad("eine Platzierung ohne Prefab wird uebersprungen und gezaehlt, nicht erfunden",
        "ein Platzhalter, der wie fertiger Inhalt aussieht, ist der Fehler dieses Projekts")

# ---- 2. Determinismus: die Einrichtung fasst keinen Generierungsstrom an -----------------------

if "CiycRandom" not in furnisher and "CiycRandom" not in footprint and \
        "CiycRandom" not in rng and "CiycRandom" not in profiles:
    ok("die Einrichtung zieht keinen CiycRandom-Strom")
else:
    bad("die Einrichtung zieht keinen CiycRandom-Strom",
        "einen davon zu ziehen treibt ihn weiter und greift in die Grundrisserzeugung zurueck")

if "Fnv1a64.Create()" in rng and "WriteInt32(layoutSeed)" in rng and "WriteInt32(roomId)" in rng:
    ok("der Einrichtungszufall ist aus (Seed, Raum-ID) abgeleitet")
else:
    bad("der Einrichtungszufall ist aus (Seed, Raum-ID) abgeleitet",
        "ohne stabilen Subseed ergibt derselbe Seed zweimal verschiedene Raeume")

# Nichts, was zur Laufzeit anders sein kann. GetInstanceID ist die klassische Falle: stabil
# innerhalb einer Sitzung und zwischen zweien verschieden.
for name, body in (("RoomFurnisher", furnisher), ("FurnishingRandom", rng),
                   ("RoomFootprint", footprint), ("FurniturePrefabCache", cache)):
    if "GetInstanceID" in body or "UnityEngine.Random" in body or "Random.value" in body:
        bad("die Einrichtung haengt an nichts Instabilem (%s)" % name,
            "Instanz-IDs und UnityEngine.Random sind zwischen zwei Laeufen verschieden")
        break
else:
    ok("die Einrichtung haengt an nichts Instabilem")

# Und sie fragt die Physikszene nicht. Dieselbe Begruendung, aus der PropSpawner seine
# Overlap-Pruefung verloren hat: eine Abfrage der lebenden Szene haengt davon ab, wann
# SyncTransforms lief, also vom Zeitpunkt statt vom Seed.
if "Physics." not in furnisher and "Physics." not in footprint:
    ok("die Einrichtung fragt die Physikszene nicht")
else:
    bad("die Einrichtung fragt die Physikszene nicht",
        "eine Abfrage der lebenden Szene macht die Einrichtung vom Frame-Timing abhaengig")

if "AssetDatabase" not in furnisher and "AssetDatabase" not in cache:
    ok("zur Laufzeit wird nichts in der AssetDatabase gesucht")
else:
    bad("zur Laufzeit wird nichts in der AssetDatabase gesucht",
        "AssetDatabase gibt es im Build nicht, und ein Ordner-Scan je Generierung ist teuer")

# ---- 3. frei bleibt frei ----------------------------------------------------------------------

ctor = re.search(r"public RoomFootprint\(RoomShellInfo shell\).*?\n        \}", footprint, re.S)
cbody = ctor.group(0) if ctor else ""

# Die Reihenfolge IST die Aussage: erst messen, dann sperren, dann erst darf etwas hinein.
order = [cbody.find("BuildWalls("), cbody.find("ReserveDoorways("),
         cbody.find("ReserveWindows("), cbody.find("ReserveWalkways(")]
if all(i >= 0 for i in order) and order == sorted(order):
    ok("Sperrzonen werden im Konstruktor festgelegt, vor jeder Platzierung")
else:
    bad("Sperrzonen werden im Konstruktor festgelegt, vor jeder Platzierung",
        "hinterher abraeumen ergibt eine Einrichtung, in der man nicht durch die Tuer kommt")

# Der Schwenkbereich rechnet mit dem ECHTEN Blatt, nicht mit der Oeffnung: das Blatt ist
# schmaler als die Oeffnung und dick, und beides gehoert in den ueberstrichenen Viertelkreis.
if "ModularDoorFactory.LeafClearance" in footprint and "ModularDoorFactory.LeafThickness" in footprint:
    ok("der Tuerschwenkbereich rechnet mit dem echten Blatt")
else:
    bad("der Tuerschwenkbereich rechnet mit dem echten Blatt",
        "die Oeffnungsbreite allein laesst den Platz fuer die Blattdicke weg")

if "Player.PlayerFactory.CapsuleRadius" in footprint:
    ok("die Laufwegbreite kommt aus dem Spieler-Collider")
else:
    bad("die Laufwegbreite kommt aus dem Spieler-Collider",
        "eine getippte Zahl geht beim naechsten Aendern des Radius still auseinander")

# Ein Fenster ist eine HOEHEN-Grenze, kein Verbot: eine Kommode darunter ist richtig, ein
# Schrank davor ist ein zugestelltes Fenster.
if "MaxHeight" in footprint and "ModularRoomBuilder.WindowSill" in footprint:
    ok("vor einem Fenster ist die Hoehe begrenzt, nicht alles verboten")
else:
    bad("vor einem Fenster ist die Hoehe begrenzt, nicht alles verboten",
        "ein Verbot nimmt die Kommode unter dem Fenster mit, die richtig dort steht")

# Nachgemessen statt behauptet.
if "VerifyWalkways" in furnisher and "WalkwaysClear" in furnisher:
    ok("die Laufwege werden am Ende gegen das Ergebnis geprueft")
else:
    bad("die Laufwege werden am Ende gegen das Ergebnis geprueft",
        "dass jede Platzierung geprueft wurde, ist eine Behauptung ueber das Verfahren")

# ---- 4. gemessen, nicht behauptet -------------------------------------------------------------

# Im Katalog steht KEINE Groesse. Eine dort eingetippte Zahl waere eine zweite Wahrheit neben
# dem Modell, und beim ersten Reimport stehen sie verschieden da.
if not re.search(r"public Vector3 (Size|BoundsSize|Footprint)\s*;", catalog):
    ok("der Moebelkatalog enthaelt keine Masse")
else:
    bad("der Moebelkatalog enthaelt keine Masse",
        "eine getippte Groesse neben dem Modell geht beim ersten Reimport auseinander")

# Gemessen im EIGENEN Raum. Renderer.bounds ist eine Welt-AABB und bei einem gedrehten Objekt
# groesser als das Objekt - CLAUDE.md Fehler 12, in diesem Projekt schon zweimal.
# Auf die Invariante gepruef, nicht auf einen Ausdruck: gemessen wird ein MESH-Bounds, durch
# die Kette bis zur Wurzel des Moebelstuecks transformiert - und nirgends Renderer.bounds.
if "mesh.bounds" in cache and "worldToLocalMatrix" in cache and \
        "renderer.bounds" not in cache and "Renderer.bounds" not in cache:
    ok("Moebel werden im eigenen Raum gemessen, nicht als Welt-AABB")
else:
    bad("Moebel werden im eigenen Raum gemessen, nicht als Welt-AABB",
        "eine Welt-AABB eines gedrehten Objekts ist groesser als das Objekt")

# Einmal je Sorte, nicht je Exemplar.
if "_measured.TryGetValue" in cache and "_loaded.TryGetValue" in cache:
    ok("Modelle und ihre Masse werden je Sorte einmal aufgeloest")
else:
    bad("Modelle und ihre Masse werden je Sorte einmal aufgeloest",
        "ein Haus mit acht Schlafzimmern darf das Bett nicht achtmal laden")

# Gesetzt wird ueber die gemessene MITTE, nicht ueber den Pivot: dieses Paket legt Pivots bis
# zu 40 m neben die eigene Geometrie.
seat = re.search(r"private static void Seat\(.*?\n        \}", furnisher, re.S)
sbody = seat.group(0) if seat else ""
if sbody and "rotation * metrics.CentreOffset" in sbody:
    ok("ein Moebelstueck wird ueber seine gemessene Mitte gesetzt, nicht ueber den Pivot")
else:
    bad("ein Moebelstueck wird ueber seine gemessene Mitte gesetzt, nicht ueber den Pivot",
        "ein Pivot in diesem Paket liegt bis zu 40 m neben der eigenen Geometrie")

# ---- 5. Gruppen und Deko ----------------------------------------------------------------------

# Die Invariante ist "relativ zum Anker", nicht "diese beiden Aufzaehlungswerte kommen vor":
# der Around-Fall ist der default-Zweig und traegt seinen Namen deshalb nicht im Code.
group = re.search(r"private static bool SeatInGroup\(.*?\n        \}\n", furnisher, re.S)
gbody = group.group(0) if group else ""
if gbody and "anchor.Rect.Centre" in gbody and "anchor.Facing" in gbody and \
        "GroupPattern.InFront" in gbody:
    ok("Gruppenmitglieder stehen relativ zu ihrem Anker")
else:
    bad("Gruppenmitglieder stehen relativ zu ihrem Anker",
        "ein Stuhl irgendwo im Raum ist die Zufallsverteilung, die das hier ersetzt")

decor = re.search(r"private static void AddDecor\(.*?\n        \}\n", furnisher, re.S)
dbody = decor.group(0) if decor else ""
if dbody and "CanHoldDecor" in dbody and "surface.Height + metrics.Size.y * 0.5f" in dbody:
    ok("Deko liegt auf einer geprueften Flaeche, Unterkante genau auf deren Oberseite")
else:
    bad("Deko liegt auf einer geprueften Flaeche, Unterkante genau auf deren Oberseite",
        "ohne die Pivot-Korrektur schwebt oder versinkt jeder Gegenstand")

if dbody and "surface.Rect.Width * 0.8f" in dbody:
    ok("die Traegerflaeche muss den Gegenstand wirklich tragen")
else:
    bad("die Traegerflaeche muss den Gegenstand wirklich tragen",
        "eine Standuhr auf einem Beistelltisch sieht aus wie Absicht und ist ein Fehler")

if "StripPhysics" in furnisher:
    ok("kleine Deko traegt weder Rigidbody noch aktiven Collider")
else:
    bad("kleine Deko traegt weder Rigidbody noch aktiven Collider",
        "ein Buch auf einem Tisch ist ein Bild und kein Koerper")

# ---- 6. Licht ---------------------------------------------------------------------------------

# Kein unsichtbares Hilfslicht: ein Practical entsteht nur an einer WIRKLICH platzierten Leuchte.
lights = re.search(r"private static void AddLights\(.*?\n        \}\n", furnisher, re.S)
lbody = lights.group(0) if lights else ""
if lbody and "PlaceAgainstWall(" in lbody and "AttachPractical(" in lbody:
    ok("ein Licht entsteht nur an einer platzierten Leuchte")
else:
    bad("ein Licht entsteht nur an einer platzierten Leuchte",
        "eine Lichtquelle ohne Lampe ist im Dunkeln der auffaelligste Fehler")

if lbody and "catalog.LightsPerRoom" in lbody:
    ok("die Zahl echter Lichtquellen kommt aus dem Budget")
else:
    bad("die Zahl echter Lichtquellen kommt aus dem Budget",
        "jede dekorative Lampe mit einem Echtzeitlicht kostet auf einem Telefon den Raum")

if "LightShadows.None" in furnisher:
    ok("die Raumleuchten werfen keine Echtzeitschatten")
else:
    bad("die Raumleuchten werfen keine Echtzeitschatten",
        "ein Punktlicht mit Schatten kostet mobil mehr als der ganze Raum")

# ---- 7. Reihenfolge im Generator --------------------------------------------------------------

inst = re.search(r"public GeneratedHouse Instantiate\(HouseLayout layout\).*?return house;", gen, re.S)
ibody = inst.group(0) if inst else ""
fi = ibody.find("FurnishRooms(")
ii = ibody.find("InstallRoomInteractables(")
ni = ibody.find("BuildNavigation(")

if fi >= 0 and ii > fi and ni > fi:
    ok("eingerichtet wird VOR den Interaktionsobjekten und VOR dem NavMesh")
else:
    bad("eingerichtet wird VOR den Interaktionsobjekten und VOR dem NavMesh",
        "danach findet die Lichtschaltersuche keine Lampe und das NavMesh keine Moebel")

# ---- 8. nichts bekommt heimlich ein fremdes Profil ---------------------------------------------

if "default: return Empty;" in profiles and "kein Profil fuer" in furnisher:
    ok("eine Raumart ohne Profil wird gemeldet statt umgewidmet")
else:
    bad("eine Raumart ohne Profil wird gemeldet statt umgewidmet",
        "einem Heizungsraum ein Wohnzimmer zu geben sieht nach Fortschritt aus")

# Eine verworfene Variante wird RESTLOS abgeraeumt. Halbe Reste, ueber die die naechste gelegt
# wird, sind zwei Einrichtungen uebereinander.
if "ClearPlaced(placed);" in furnisher:
    ok("eine verworfene Variante wird restlos abgeraeumt")
else:
    bad("eine verworfene Variante wird restlos abgeraeumt",
        "Reste unter der naechsten Variante sind zwei Einrichtungen uebereinander")

# ---- 9. die Huelle schreibt auf, was sie gebaut hat --------------------------------------------

mrb = code("Assets/CatchIfYouCan/Scripts/Procedural/ModularRoomBuilder.cs") or ""
if "shell.Configure(size, doorMask, windowMask" in mrb and "WindowMask" in shell:
    ok("die Huelle schreibt Groesse, Tueren und Fenster auf, statt sie ableiten zu lassen")
else:
    bad("die Huelle schreibt Groesse, Tueren und Fenster auf, statt sie ableiten zu lassen",
        "eine zweite Ableitung derselben Regel geht beim ersten Aendern auseinander")

# ---- 10. der Bericht faengt den Wuerfel, den die Groesse allein durchlaesst --------------------

if "IsUnityPrimitive" in report and '"Cube"' in report:
    ok("der Inhaltsbericht meldet Primitivmeshes unabhaengig von ihrer Groesse")
else:
    bad("der Inhaltsbericht meldet Primitivmeshes unabhaengig von ihrer Groesse",
        "der Wuerfel war 1 m gross und waere unter jeder Groessenschwelle durchgerutscht")

print()
print("  %d passed, %d failed" % (passed, failed))
sys.exit(1 if failed else 0)
PY
