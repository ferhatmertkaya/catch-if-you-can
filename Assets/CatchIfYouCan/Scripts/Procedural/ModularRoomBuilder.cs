using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Turns one logical room into modular geometry.
    ///
    /// It consumes the layout and never adds to it. Everything it needs - the room's size,
    /// its category, which of its four walls carry a door, which are open to the outside -
    /// is already in <see cref="LayoutRoom"/>, decided in Stage A and folded into the layout
    /// hash. This class reads those fields and places meshes. It cannot move a room, change a
    /// door, or pick a different neighbour, because it is never asked to.
    ///
    /// That is why the layout hash does not change when the art does.
    ///
    /// <para>
    /// Where a choice remains - which of three interchangeable wall meshes this particular
    /// wall gets - it is DERIVED, not rolled. Drawing from a CiycRandom stream would advance
    /// that stream and change every later draw, which is the one way a visual pass can reach
    /// back into generation. So the variant index comes from an FNV hash of the room's id,
    /// the role and the direction: stable for a given room, identical on every machine, and
    /// invisible to the streams. The house lighting director already works this way.
    /// </para>
    /// </summary>
    public static class ModularRoomBuilder
    {
        /// <summary>
        /// Thicknesses. Structural, not artistic: they decide where a collider stands, so they
        /// live here rather than in a catalog that art can edit.
        /// </summary>
        public const float WallThickness = 0.15f;
        public const float FloorThickness = 0.20f;
        public const float CeilingThickness = 0.20f;

        /// <summary>
        /// The doorway the project already builds, unchanged. The pack's own opening is
        /// 1.25 x 2.60 and would fit its door leaf untouched, but swapping to it is a later
        /// phase - this one changes how the structure is made, not what size it is.
        /// </summary>
        /// <summary>
        /// The pack's own measured door opening, 1.25 x 2.60. The project used to build 1.20 x
        /// 2.20 here; matching the pack means its door leaf drops in at authored scale instead
        /// of being squeezed, and 2.60 under a 3.00 m ceiling still leaves 0.40 m of lintel.
        /// Docs/HQ_MODULAR_MIGRATION.md marks both numbers negotiable: they are private to
        /// Stage B, outside the engine-free assembly, and absent from the layout hash.
        /// </summary>
        public const float DoorWidth = 1.25f;
        public const float DoorHeight = 2.60f;

        /// <summary>
        /// The window opening, taken from the pack's own measured window 7 (2.05 x 0.90, sill
        /// 1.55). Head height is 1.55 + 0.90 = 2.45 m, which leaves 0.55 m of wall under a
        /// 3.00 m ceiling. Windows 6 and 8 fit the same way; window 9 does not - its sill is at
        /// 2.00 and it reaches 3.25 m, straight through the ceiling. Docs/HQ_MODULAR_MIGRATION.md
        /// carries the measurements.
        /// </summary>
        public const float WindowWidth = 2.05f;
        public const float WindowHeight = 0.90f;
        // 0.90 m, not 1.55. The sill was set from the vendor wall the window was extracted
        // from, and that piece is 4 m tall - under this project's 3.00 m ceiling the opening
        // ran 1.55 to 2.45 and left 0.55 m of wall above it, which reads as a window near the
        // ceiling. A residential sill is 0.85 to 1.00 m; 0.90 puts the head at 1.80 m, just
        // above eye level at 1.68.
        public const float WindowSill = 0.90f;

        /// <summary>
        /// The clear height a generated room is built to, used to check that an insert does not
        /// reach through the ceiling. Read from the same place the rooms are sized from.
        /// </summary>
        public const float CeilingClearance = 3.00f;

        /// <summary>
        /// How far a vendor insert may stand proud of the opening it fills, per axis.
        ///
        /// <para>
        /// A door leaf carries a frame and an architrave, so it is legitimately wider and taller
        /// than the hole. A 4 m wall is not, and telling the two apart by measurement is the only
        /// way that survives a pack whose parts are named <c>1</c> and <c>Steklo</c>.
        /// </para>
        /// </summary>
        public const float InsertOverhangAllowance = 0.40f;

        /// <summary>
        /// Builds the room's shell from generated geometry.
        ///
        /// Everything it needs is already in the LayoutRoom, decided in Stage A and folded into
        /// the layout hash: the size, the category, which of the four walls carry a door. This
        /// reads those fields and makes meshes. It cannot move a room, change a door or pick a
        /// neighbour, because it is never asked to - which is why the layout hash does not move
        /// when the art does.
        /// </summary>
        public static GameObject Build(LayoutRoom room, Vector3 worldPosition, Transform parent,
            ModularInteriorCatalog catalog, out string error)
        {
            // Which outside walls get a window is DERIVED from the room's identity here, never
            // rolled: a draw from a CiycRandom stream would advance it and reach back into
            // generation. Hand authoring passes its own mask to the overload below instead.
            int windowMask = 0;
            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                var dir = Directions.Cardinal[d];
                if (!room.HasDoor(dir) && room.IsOpen(dir) && WantsWindow(room, dir))
                    windowMask |= LayoutRoom.DirectionMask(dir);
            }

            return Build(room, worldPosition, parent, catalog, windowMask, out error);
        }

        /// <summary>
        /// The same room, with the windows named explicitly rather than derived.
        ///
        /// This is the entry point for hand authoring: a person deciding which wall gets a
        /// window is making a choice, and a choice does not belong in a hash of anything.
        /// </summary>
        public static GameObject Build(LayoutRoom room, Vector3 worldPosition, Transform parent,
            ModularInteriorCatalog catalog, int windowMask, out string error)
        {
            error = null;

            var size = new Vector3(
                Quantize.Metres(room.SizeMm.X),
                Quantize.Metres(room.SizeMm.Y),
                Quantize.Metres(room.SizeMm.Z));

            if (size.x < 0.5f || size.y < 0.5f || size.z < 0.5f)
            {
                error = "die Raumgroesse " + size + " ist zu klein zum Bauen";
                return null;
            }

            var roomRoot = new GameObject($"Room_{room.Category}_{room.RoomId}");
            roomRoot.transform.SetParent(parent, false);
            roomRoot.transform.position = worldPosition;

            BuildFloor(roomRoot.transform, size, catalog);
            BuildCeiling(roomRoot.transform, size, catalog);

            for (int d = 0; d < Directions.Cardinal.Length; d++)
                BuildWall(roomRoot.transform, room, Directions.Cardinal[d], size, catalog, windowMask);

            var module = roomRoot.GetComponent<RoomModule>();
            if (module == null)
                module = roomRoot.AddComponent<RoomModule>();

            module.Configure(room.Category, new Bounds(Vector3.up * (size.y * 0.5f), size), room.RoomId);

            BuildLighting(roomRoot.transform, size, catalog);

            module.CollectSockets();

            // Was diese Huelle geworden ist, schreibt sie auf - Groesse, Tueren, Fenster.
            //
            // Die Einrichtung braucht das, und sie darf es nicht ein zweites Mal ableiten: die
            // Fenstermaske entsteht oben aus der Identitaet des Raums, und wer sie spaeter noch
            // einmal berechnet, hat zwei Implementierungen derselben Entscheidung (CLAUDE.md
            // Fehler 1). Die gehen genau dann auseinander, wenn jemand die Regel aendert und nur
            // eine Stelle findet.
            int doorMask = 0;
            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                if (room.HasDoor(Directions.Cardinal[d]))
                    doorMask |= LayoutRoom.DirectionMask(Directions.Cardinal[d]);
            }

            var shell = roomRoot.GetComponent<Furnishing.RoomShellInfo>();
            if (shell == null)
                shell = roomRoot.AddComponent<Furnishing.RoomShellInfo>();

            shell.Configure(size, doorMask, windowMask, room.Category, room.RoomId);

            return roomRoot;
        }

        // ------------------------------------------------------------------ light

        /// <summary>Wie weit die Gluehbirne unter der Decke haengt.</summary>
        public const float CeilingLampDrop = 0.34f;

        /// <summary>Die Farbe einer alten Gluehbirne. Warm, und mit sichtbar wenig Blau.</summary>
        private static readonly Color BulbWarm = new Color(1f, 0.78f, 0.52f);

        /// <summary>Das Restlicht. Waermer und viel schwaecher als die Birne.</summary>
        private static readonly Color GlowWarm = new Color(1f, 0.60f, 0.34f);

        /// <summary>
        /// Zwei Lichter je Raum, und sie beantworten verschiedene Fragen.
        ///
        /// <para>
        /// <b>Warum es das ueberhaupt gibt.</b> Raeume kamen aus diesem Bauer ganz ohne Licht.
        /// <c>PrimitiveRoomFactory</c> setzte an der Licht-Steckdose eines Raums eine Leuchte,
        /// dieser Bauer nie - und seit die Raeume von hier kommen, hat der Lichtregisseur nichts
        /// mehr zu regieren: sein eigener Bericht sagt "0 of 0 practicals". Was blieb, war der
        /// Umgebungsterm und ein Mond, also in jeder Ecke jedes Raums genau derselbe Wert. Ein
        /// Raum, der ueberall gleich hell ist, sieht nicht dunkel aus, sondern tot: keine
        /// Richtung, kein Abfall, kein Schatten. Genau das ist gemeldet worden.
        /// </para>
        ///
        /// <para>
        /// <b>Die Deckenleuchte</b> ist das Praktikal des Raums. Sie wird hier nur GESETZT;
        /// Farbtemperatur, Reichweite, Schatten und vor allem, ob sie ueberhaupt brennt, bestimmt
        /// <c>HouseLightingDirector</c> aus dem Missions-Seed, und der Lichtschalter an der Wand
        /// gehoert zu ihr. Die Rosette darueber ist der Koerper, an dem sie haengt, und sie
        /// leuchtet NICHT von selbst: eine Fassung, die glueht, waehrend ihr Licht aus ist, ist
        /// eine Lampe, die luegt.
        /// </para>
        ///
        /// <para>
        /// <b>Das Restlicht</b> ist kein Praktikal (<see cref="Environment.AmbientRoomLight"/>).
        /// Es hat keinen Schalter, wird nicht ausgewuerfelt und bleibt an, wenn die Deckenleuchte
        /// aus ist - schwach, warm, knapp ueber dem Boden und ohne Schatten. Es ersetzt die
        /// Taschenlampe nicht; es ersetzt den Unterschied zwischen einem dunklen und einem toten
        /// Raum. Ein <c>CandleFlicker</c> laesst es leise atmen, damit ein Standbild des Raums
        /// nicht dasselbe Bild ist wie eine Sekunde spaeter.
        /// </para>
        ///
        /// <para>
        /// Nichts hiervon zieht einen Zufallsstrom und nichts geht in den Layout-Hash ein. Alle
        /// Zahlen kommen aus der Raumgroesse, die schon feststeht - Stage B von Anfang bis Ende.
        /// </para>
        /// </summary>
        private static void BuildLighting(Transform parent, Vector3 size,
                                          ModularInteriorCatalog catalog)
        {
            // ---- das Praktikal --------------------------------------------------------------

            var lamp = new GameObject("RoomLight");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = new Vector3(0f, size.y - CeilingLampDrop, 0f);

            var bulb = lamp.AddComponent<Light>();
            bulb.type = LightType.Point;
            bulb.color = BulbWarm;
            bulb.intensity = 1.8f;
            bulb.range = Mathf.Max(6f, (size.x + size.z) * 0.75f);
            bulb.shadows = LightShadows.Soft;
            bulb.shadowStrength = 0.9f;

            // Die Rosette: an der Decke, nicht an der Birne, damit sie das Licht nicht nach unten
            // abschattet. Aus dem Zierleistenmaterial des Katalogs - kein eigenes, denn ein
            // zweites Material fuer dieselbe Rolle ist eine zweite Stelle, die falsch sein kann.
            var rose = Piece(parent, "RoomLight_Rose",
                             StructuralMeshFactory.Floor(0.42f, 0.42f, 0.05f),
                             TrimMaterial(catalog));
            rose.transform.localPosition = new Vector3(0f, size.y, 0f);

            // ---- das Restlicht --------------------------------------------------------------

            var glow = new GameObject("RoomGlow");
            glow.transform.SetParent(parent, false);

            // In eine Ecke, knapp ueber dem Boden: mittig gesetzt waere es ein zweites
            // Deckenlicht ohne Decke und der Raum saehe wieder gleichmaessig aus. Aus der Ecke
            // heraus bekommt jede Wand eine andere Helligkeit, und genau das ist der
            // Unterschied, den man sieht.
            glow.transform.localPosition = new Vector3(size.x * 0.34f, 0.45f, -size.z * 0.34f);

            var ember = glow.AddComponent<Light>();
            ember.type = LightType.Point;
            ember.color = GlowWarm;

            // Bewusst schwach. Es soll die Formen im Raum von der Wand abheben und sonst nichts;
            // ein Restlicht, nach dem man sich orientieren kann, waere die Taschenlampe.
            ember.intensity = 0.55f;
            ember.range = Mathf.Max(3.5f, Mathf.Min(size.x, size.z) * 0.7f);

            // Keine Echtzeitschatten. Es ist eine schwache Quelle unter Moebelhoehe; was sie an
            // Schatten wirft, sieht man kaum, und bezahlt wird es trotzdem - auf dem Telefon in
            // jedem Bild.
            ember.shadows = LightShadows.None;

            // Erst die Helligkeit, dann das Flackern: CandleFlicker merkt sich in Awake, was es
            // modulieren soll, und Awake laeuft in AddComponent. Danach gesetzt haette es eine
            // Zahl gemerkt, die es nie gab.
            glow.AddComponent<Art.CandleFlicker>();

            // Und die Auszeichnung, die den Unterschied traegt: kein Praktikal. Der Regisseur
            // laesst es in Ruhe, der Lichtschalter an der Wand gehoert ihm nicht, und es bleibt
            // an, wenn die Deckenleuchte aus ist.
            glow.AddComponent<Environment.AmbientRoomLight>();
        }

        // ------------------------------------------------------------------ surfaces

        private static void BuildFloor(Transform parent, Vector3 size, ModularInteriorCatalog catalog)
        {
            var mesh = StructuralMeshFactory.Floor(size.x, size.z, FloorThickness);
            var go = Piece(parent, "Floor", mesh, FloorMaterial(catalog));
            go.transform.localPosition = Vector3.zero;

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -FloorThickness * 0.5f, 0f);
            box.size = new Vector3(size.x, FloorThickness, size.z);
        }

        private static void BuildCeiling(Transform parent, Vector3 size, ModularInteriorCatalog catalog)
        {
            var mesh = StructuralMeshFactory.Ceiling(size.x, size.z, CeilingThickness);
            var go = Piece(parent, "Ceiling", mesh, CeilingMaterial(catalog));
            go.transform.localPosition = new Vector3(0f, size.y, 0f);

            // No collider. The player cannot reach it and every one that exists is one the
            // physics engine tests against for nothing.
        }

        // --------------------------------------------------------------------- walls

        private static void BuildWall(Transform parent, LayoutRoom room,
            SocketDirection direction, Vector3 size, ModularInteriorCatalog catalog, int windowMask)
        {
            // The layout decides what this wall is. A door connection means a real hole, not a
            // solid wall with a door drawn on it.
            bool hasDoor = room.HasDoor(direction);
            bool hasWindow = !hasDoor && (windowMask & LayoutRoom.DirectionMask(direction)) != 0;

            ModuleRole role = hasDoor ? ModuleRole.WallWithDoorway
                            : hasWindow ? ModuleRole.WallWithWindow
                            : ModuleRole.WallSolid;

            bool alongX = direction == SocketDirection.North || direction == SocketDirection.South;
            float span = alongX ? size.x : size.z;

            Mesh mesh;
            if (hasDoor)
                mesh = StructuralMeshFactory.WallWithOpening(span, size.y, WallThickness,
                    DoorWidth, DoorHeight, 0f);
            else if (hasWindow)
                mesh = StructuralMeshFactory.WallWithOpening(span, size.y, WallThickness,
                    WindowWidth, WindowHeight, WindowSill);
            else
                mesh = StructuralMeshFactory.SolidWall(span, size.y, WallThickness);

            var go = Piece(parent, role + "_" + direction, mesh, WallMaterial(catalog));

            // The wall's own space is centred on X across its span and centred on Z across its
            // thickness, rising from y = 0. So it goes on the wall line with no correction, and
            // the yaw turns its length along the right axis.
            switch (direction)
            {
                case SocketDirection.North:
                    go.transform.localPosition = new Vector3(0f, 0f, size.z * 0.5f - WallThickness * 0.5f);
                    go.transform.localRotation = Quaternion.identity;
                    break;
                case SocketDirection.South:
                    go.transform.localPosition = new Vector3(0f, 0f, -size.z * 0.5f + WallThickness * 0.5f);
                    go.transform.localRotation = Quaternion.identity;
                    break;
                case SocketDirection.East:
                    go.transform.localPosition = new Vector3(size.x * 0.5f - WallThickness * 0.5f, 0f, 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                default:
                    go.transform.localPosition = new Vector3(-size.x * 0.5f + WallThickness * 0.5f, 0f, 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
            }

            AddWallColliders(go, hasDoor, hasWindow, span, size.y);

            // ---- every opening has a NAME, and nothing is left as an unexplained hole -------
            //
            // Three roles and only three: a doorway, a window, or a wall. There is no fourth,
            // and an opening that could not be filled says which one it was and why - "there is
            // a rectangular hole in that wall" was, on the screen, the same picture for a
            // doorway waiting for a leaf, a window waiting for glass, and a wall that had been
            // cut for something nobody built.
            if (!hasDoor && !hasWindow)
                return;

            string opening = hasDoor ? "DOORWAY" : "WINDOW";

            // The pack's contribution first, if it has one that FITS. It is refused when it is a
            // whole vendor wall, which is what this pack actually ships - see AddInsert.
            bool filledByPack = hasDoor
                ? AddInsert(go.transform, catalog != null ? catalog.DoorInsert : default, "Door",
                            new Vector3(0f, DoorHeight * 0.5f, 0f))
                : AddInsert(go.transform, catalog != null ? catalog.WindowInsert : default, "Window",
                            new Vector3(0f, WindowSill + WindowHeight * 0.5f, 0f));

            if (filledByPack)
            {
                Core.CIYCLog.Info("[CIYC][House][Opening] " + opening + " " + direction +
                                  " filled by the pack's insert.");
                return;
            }

            // CIYC's own. Generated geometry with a project material, at the exact size of the
            // hole it is going into, because the hole and the thing filling it are computed from
            // the same four constants rather than measured off each other.
            if (hasDoor)
            {
                // The LINING only. The leaf hangs on the connection, not on the wall: a doorway
                // is one hole shared by two rooms, and both of those rooms build a wall here, so
                // a leaf per wall is two leaves per doorway swinging through each other. See
                // ProceduralHouseGenerator.CreateDoorAt.
                BuildDoorLining(go.transform, catalog);
                Core.CIYCLog.Info("[CIYC][House][Opening] DOORWAY " + direction +
                                  " lined; the leaf hangs on the connection.");
                return;
            }

            BuildWindowAssembly(go.transform, catalog);
            Core.CIYCLog.Info("[CIYC][House][Opening] WINDOW " + direction +
                              " glazed at sill " + WindowSill.ToString("F2") + " m.");
        }

        // ------------------------------------------------------------- doorway and window

        /// <summary>How far a lining or a frame overlaps the hole it sits in, per edge.</summary>
        public const float LiningOverlap = 0.03f;

        /// <summary>How far a lining or a frame stands proud of the wall, per face.</summary>
        public const float LiningProud = 0.02f;

        /// <summary>
        /// The four sticks of timber that turn a rectangular hole into a doorway.
        ///
        /// <para>
        /// Built per WALL, which is per side of the opening - a doorway between two rooms has a
        /// lining in each, which is what a real one has. The leaf is the part there is only one
        /// of, and it is built somewhere else for exactly that reason.
        /// </para>
        /// </summary>
        private static void BuildDoorLining(Transform wall, ModularInteriorCatalog catalog)
        {
            Material trim = TrimMaterial(catalog);
            float depth = WallThickness + LiningProud * 2f;
            float half = DoorWidth * 0.5f;

            AddFrameStick(wall, "DoorJamb_L", new Vector3(LiningOverlap * 2f, DoorHeight + LiningOverlap, depth),
                          new Vector3(-(half + LiningOverlap), (DoorHeight + LiningOverlap) * 0.5f, 0f), trim);
            AddFrameStick(wall, "DoorJamb_R", new Vector3(LiningOverlap * 2f, DoorHeight + LiningOverlap, depth),
                          new Vector3(half + LiningOverlap, (DoorHeight + LiningOverlap) * 0.5f, 0f), trim);
            AddFrameStick(wall, "DoorHead", new Vector3(DoorWidth + LiningOverlap * 4f, LiningOverlap * 2f, depth),
                          new Vector3(0f, DoorHeight, 0f), trim);
        }

        /// <summary>
        /// A frame in the opening and a pane of glass in the frame.
        ///
        /// <para>
        /// No collider on either. The wall already carries ONE box across its whole span for a
        /// window wall - a window is not a way through - so adding collision here would be a
        /// second answer to a question that already has one.
        /// </para>
        /// </summary>
        private static void BuildWindowAssembly(Transform wall, ModularInteriorCatalog catalog)
        {
            Material trim = TrimMaterial(catalog);
            float depth = WallThickness + LiningProud * 2f;
            float half = WindowWidth * 0.5f;
            float bottom = WindowSill;
            float top = WindowSill + WindowHeight;

            AddFrameStick(wall, "WindowJamb_L", new Vector3(LiningOverlap * 2f, WindowHeight + LiningOverlap * 4f, depth),
                          new Vector3(-(half + LiningOverlap), (bottom + top) * 0.5f, 0f), trim);
            AddFrameStick(wall, "WindowJamb_R", new Vector3(LiningOverlap * 2f, WindowHeight + LiningOverlap * 4f, depth),
                          new Vector3(half + LiningOverlap, (bottom + top) * 0.5f, 0f), trim);
            AddFrameStick(wall, "WindowHead", new Vector3(WindowWidth + LiningOverlap * 4f, LiningOverlap * 2f, depth),
                          new Vector3(0f, top + LiningOverlap, 0f), trim);
            AddFrameStick(wall, "WindowSill", new Vector3(WindowWidth + LiningOverlap * 4f, LiningOverlap * 2f, depth + 0.04f),
                          new Vector3(0f, bottom - LiningOverlap, 0f), trim);

            Material glass = catalog != null ? catalog.GlassMaterial : null;
            if (glass == null || !IsDrawable(glass, "Fensterglas"))
            {
                // No pane, and said out loud. A window with no glass is a hole in a wall, which
                // is precisely the thing this whole pass exists to stop being unexplained.
                Core.CIYCLog.Error("[CIYC][House][Opening] WINDOW: kein Glasmaterial im " +
                                   "ModularInteriorCatalog (GlassMaterial). Das Fenster " +
                                   "bekommt seinen Rahmen und bleibt eine OFFENE Oeffnung. " +
                                   "Ein Material ohne Shader waere magenta - deshalb lieber " +
                                   "sichtbar leer als sichtbar falsch.");
                return;
            }

            // The pane is a PANEL: its texture is stretched once across it rather than tiled by
            // the metre, because a pane of glass is one surface and not a wall of them.
            var pane = Piece(wall, "WindowGlass",
                             StructuralMeshFactory.Panel(WindowWidth, WindowHeight, 0.02f), glass);
            pane.transform.localPosition = new Vector3(0f, WindowSill, 0f);
        }

        /// <summary>One piece of frame: a box, tiled by the metre like the trim it is.</summary>
        private static void AddFrameStick(Transform wall, string name, Vector3 size,
                                          Vector3 centre, Material material)
        {
            var mesh = StructuralMeshFactory.SolidWall(size.x, size.y, size.z);
            var go = Piece(wall, name, mesh, material);

            // SolidWall rises from y = 0, so the centre asked for becomes a bottom here. One
            // conversion, in one place, rather than four call sites each subtracting half a
            // height and one of them getting it wrong.
            go.transform.localPosition = new Vector3(centre.x, centre.y - size.y * 0.5f, centre.z);
        }

        /// <summary>
        /// Puts one vendor piece into the opening this wall already has.
        ///
        /// <para>
        /// The pack ships no door leaf and no window as objects of their own: each is a child of
        /// a whole 4 m wall prefab that carries its own wallpaper. Instantiating one of those
        /// into a 3 m room would put a second wall through the ceiling, so the prefab is reduced
        /// to the parts that are the insert - identified by the MATERIALS they carry, because
        /// the child objects are numbered and the materials are named.
        /// </para>
        /// <para>
        /// Instantiated as a child of the generated wall, so it inherits the wall's placement
        /// and rotation and cannot drift from the hole it belongs to. Every collider it brings
        /// is switched off: gameplay collision is the generated boxes' job, and a MeshCollider
        /// across vendor geometry is the expensive way to get the same answer wrong. Shadow
        /// casting goes with it - a door leaf is not a wall.
        /// </para>
        /// </summary>
        /// <returns>True when a piece was actually placed in the opening.</returns>
        private static bool AddInsert(Transform wall, Content.StructuralInsert insert,
            string role, Vector3 localPosition)
        {
            if (!insert.IsSet)
                return false;

            GameObject go = Object.Instantiate(insert.Prefab, wall);
            go.name = role + "_Insert";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(insert.LocalEuler);

            int kept = KeepOnlyInsertParts(go, insert.KeepMaterials);
            if (kept == 0)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": am Prefab '" + insert.Prefab.name +
                                   "' traegt kein Teil eines der Materialien " +
                                   Join(insert.KeepMaterials) + ". Es waere die ganze " +
                                   "Vendor-Wand eingesetzt worden - das Teil wird stattdessen " +
                                   "weggelassen.");

                // Switched off, not destroyed. Destroy is deferred and refuses outright in edit
                // mode, which is exactly where the one-room test tool builds - so destroying
                // here would throw in the editor and merely take a frame in a build. One
                // behaviour, both modes.
                go.SetActive(false);
                return false;
            }

            OrientUpright(go.transform, role);

            // Placed by the MESH, never by the pivot.
            //
            // This pack's pivots sit 13 to 40 metres from the geometry they belong to - the
            // inventory reads "Pivot 32.5 m" for the door wall and "31.4 m" for the window -
            // because these were exported from one authored apartment and every piece kept that
            // scene's origin. Setting localPosition puts the PIVOT there, so the door itself
            // landed tens of metres away: on screen, a door frame somewhere near the ceiling and
            // a window above it.
            //
            // So the wanted point is where the kept geometry's centre has to end up, and the
            // transform is offset by whatever it takes to put it there. Measured after the
            // orientation, because turning the piece upright moves its centre too.
            Vector3 target = localPosition + insert.LocalOffset;
            string source = insert.Prefab != null ? insert.Prefab.name : "<null>";

            if (!TryMeasureInSpace(go.transform, wall, out Bounds placed))
            {
                // NOT placed by the pivot. This pack's pivots sit 13 to 40 m from their own
                // geometry, so falling back to one does not put the door roughly right - it puts
                // it tens of metres away, which on screen is a door frame near the ceiling. An
                // insert that cannot be measured is refused, and the prefab is named.
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role +
                                   " prefab=" + source +
                                   " measured=NO kept=" + kept +
                                   " -> REFUSED. Ohne Messung waere er ueber seinen Pivot " +
                                   "gesetzt worden, und der liegt in diesem Paket bis zu 40 m " +
                                   "neben der eigenen Geometrie. Die Oeffnung bleibt frei.");
                go.SetActive(false);
                return false;
            }

            // ---- it has to BE a door before it is placed like one --------------------------
            //
            // The measurement above works. What it measured was a whole 4 m vendor wall:
            // meshSize=(3.991, 4.054, 0.650) for the door and (3.764, 4.116, 0.402) for the
            // window, against openings of 1.25 x 2.60 and 2.05 x 0.90. Centring a 4.05 m object
            // on 1.30 m MUST put its top at 3.33 m and its bottom 0.73 m below the floor, so the
            // two height errors below were not a placement bug at all - they were the correct
            // arithmetic on the wrong object.
            //
            // The cause is upstream: KeepOnlyInsertParts matched the wall shell as well, because
            // the shell wears one of the named materials. kept=2 looked like a clean selection
            // and was a wall plus a leaf. And it is also what "there are two door frames" is -
            // the generated wall already carries the opening with its reveal, and the insert
            // added a second, wall-sized one over it.
            //
            // So the size is checked against the opening the piece is meant to fill. A frame or
            // an architrave legitimately stands a little proud of the hole; a whole wall does
            // not. A refusal leaves the generated opening exactly as it was - which is a clean,
            // correctly sized doorway, not a hole in the fiction.
            float openingWidth = role == "Door" ? DoorWidth : WindowWidth;
            float openingHeight = role == "Door" ? DoorHeight : WindowHeight;

            if (placed.size.x > openingWidth + InsertOverhangAllowance ||
                placed.size.y > openingHeight + InsertOverhangAllowance)
            {
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role +
                                   " prefab=" + source +
                                   " kept=" + kept +
                                   " measured=" + placed.size.ToString("F3") +
                                   " opening=" + openingWidth.ToString("F2") + " x " +
                                   openingHeight.ToString("F2") +
                                   " (+" + InsertOverhangAllowance.ToString("F2") + " m Ueberstand erlaubt)" +
                                   " -> REFUSED: das ist keine Tuer und kein Fenster, das ist ein " +
                                   "ganzes Wandteil. Die KeepMaterials " + Join(insert.KeepMaterials) +
                                   " treffen die Wandschale mit. Die erzeugte Wand traegt ihre " +
                                   "Oeffnung bereits - sie bleibt frei statt einen zweiten Rahmen " +
                                   "zu bekommen.");
                go.SetActive(false);
                return false;
            }

            go.transform.localPosition = target - placed.center;

            // Measured again AFTER the move, because what matters is where it ended up, not
            // what it was asked to do.
            float bottom, top;
            if (TryMeasureInSpace(go.transform, wall, out Bounds finalBounds))
            {
                bottom = finalBounds.min.y;
                top = finalBounds.max.y;
            }
            else
            {
                bottom = float.NaN;
                top = float.NaN;
            }

            float wantedTop = role == "Door" ? DoorHeight : WindowSill + WindowHeight;
            float wantedBottom = role == "Door" ? 0f : WindowSill;

            Core.CIYCLog.Info("[CIYC][House][Insert] role=" + role +
                              " prefab=" + source +
                              " kept=" + kept +
                              " measured=YES" +
                              " meshSize=" + placed.size.ToString("F3") +
                              " meshCenterBefore=" + placed.center.ToString("F3") +
                              " pivotOffset=" + placed.center.magnitude.ToString("F2") + "m" +
                              " target=" + target.ToString("F3") +
                              " finalLocal=" + go.transform.localPosition.ToString("F3") +
                              " finalWorld=" + go.transform.position.ToString("F3") +
                              " bottomLocalY=" + bottom.ToString("F3") +
                              " topLocalY=" + top.ToString("F3") +
                              " wantedBottom=" + wantedBottom.ToString("F2") +
                              " wantedTop=" + wantedTop.ToString("F2"));

            // The two claims worth checking, checked rather than assumed: the thing stands on
            // the finished floor, and it does not reach through the ceiling.
            const float Tolerance = 0.05f;
            if (!float.IsNaN(bottom) && Mathf.Abs(bottom - wantedBottom) > Tolerance)
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role + " prefab=" + source +
                                   " steht NICHT auf der gewollten Hoehe: Unterkante " +
                                   bottom.ToString("F3") + " statt " + wantedBottom.ToString("F2") +
                                   " (Abweichung " + Mathf.Abs(bottom - wantedBottom).ToString("F3") +
                                   " m).");

            if (!float.IsNaN(top) && top > CeilingClearance)
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role + " prefab=" + source +
                                   " ragt mit Oberkante " + top.ToString("F3") +
                                   " ueber die lichte Hoehe " + CeilingClearance.ToString("F2") +
                                   " - er schneidet die Decke.");

            return true;
        }

        /// <summary>
        /// Switches off every renderer whose material is not one of the wanted ones, and reports
        /// how many were kept. Zero kept means the naming is wrong, and inserting the whole
        /// vendor wall would be far worse than inserting nothing.
        /// </summary>
        private static int KeepOnlyInsertParts(GameObject go, string[] keepMaterials)
        {
            go.GetComponentsInChildren(true, _insertRenderers);

            if (keepMaterials == null || keepMaterials.Length == 0)
            {
                int all = _insertRenderers.Count;
                for (int i = 0; i < all; i++)
                    _insertRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                _insertRenderers.Clear();
                DisableColliders(go);
                return all;
            }

            int kept = 0;
            for (int i = 0; i < _insertRenderers.Count; i++)
            {
                Renderer renderer = _insertRenderers[i];
                if (Wanted(renderer, keepMaterials))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    kept++;
                }
                else
                {
                    renderer.enabled = false;
                }
            }

            _insertRenderers.Clear();
            DisableColliders(go);
            return kept;
        }

        private static bool Wanted(Renderer renderer, string[] keepMaterials)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                if (materials[m] == null)
                    continue;

                for (int k = 0; k < keepMaterials.Length; k++)
                {
                    if (string.Equals(materials[m].name, keepMaterials[k],
                                      System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Turns a piece that was authored Z-up onto its feet, decided by MEASUREMENT.
        ///
        /// <para>
        /// The pack's wall prefabs measure about 4 m wide, 4 m in Z and a tenth of a metre in Y:
        /// their height is on Z, which is the 3ds Max convention and not Unity's. Whether the
        /// prefab already corrects that is not something a document can say, so it is measured
        /// on the instantiated object: a piece taller in Z than in Y, and thin in Y, is lying
        /// down. Logged either way, because a silent rotation is impossible to argue with when
        /// the window ends up on the floor.
        /// </para>
        /// </summary>
        private static void OrientUpright(Transform insert, string role)
        {
            if (!TryMeasureInSpace(insert, insert, out Bounds bounds))
                return;

            Vector3 size = bounds.size;
            bool lyingDown = size.z > size.y * 2f && size.y < size.x * 0.5f;

            if (!lyingDown)
            {
                Core.CIYCLog.Info("[CIYC][House] " + role + "-Einsatz steht aufrecht: " +
                                  size.ToString("F2") + " - keine Drehung noetig.");
                return;
            }

            insert.localRotation = Quaternion.Euler(-90f, 0f, 0f) * insert.localRotation;
            Core.CIYCLog.Info("[CIYC][House] " + role + "-Einsatz lag flach (" +
                              size.ToString("F2") + ", Hoehe auf Z) und wurde um -90 Grad um X " +
                              "aufgerichtet.");
        }

        /// <summary>
        /// The bounds of the parts that are actually VISIBLE, expressed in <paramref name="space"/>.
        ///
        /// <para>
        /// Visible only. Most of a vendor wall prefab has just been switched off - it is the wall
        /// shell around the door - and measuring it would centre the door on the shell it was
        /// separated from.
        /// </para>
        /// <para>
        /// Eight corners per mesh rather than a centre and a size, because a rotated child's
        /// axis-aligned size is not its size in the parent's frame. This runs a handful of times
        /// per room, so correctness costs nothing worth saving.
        /// </para>
        /// </summary>
        private static bool TryMeasureInSpace(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default;
            root.GetComponentsInChildren(true, _insertRenderers);

            bool started = false;
            for (int i = 0; i < _insertRenderers.Count; i++)
            {
                Renderer renderer = _insertRenderers[i];
                if (!renderer.enabled)
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Bounds local = filter.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, Corner(c));
                    Vector3 point = space.InverseTransformPoint(
                        filter.transform.TransformPoint(corner));

                    if (!started)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        started = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            _insertRenderers.Clear();
            return started;
        }

        private static Vector3 Corner(int index)
        {
            return new Vector3((index & 1) == 0 ? -1f : 1f,
                               (index & 2) == 0 ? -1f : 1f,
                               (index & 4) == 0 ? -1f : 1f);
        }

        private static void DisableColliders(GameObject go)
        {
            // Switched off rather than destroyed. A disabled collider contributes no physics
            // geometry, which is the whole point, and it does it identically in the editor and
            // in a build - Destroy is deferred and DestroyImmediate is edit-mode-only, and
            // picking between them by context is exactly how this project once got an editor
            // house and a device house that differed.
            go.GetComponentsInChildren(true, _insertColliders);
            for (int i = 0; i < _insertColliders.Count; i++)
                _insertColliders[i].enabled = false;

            _insertColliders.Clear();
        }

        private static string Join(string[] values)
        {
            if (values == null || values.Length == 0)
                return "<keine genannt>";

            return "'" + string.Join("', '", values) + "'";
        }

        private static readonly System.Collections.Generic.List<Collider> _insertColliders =
            new System.Collections.Generic.List<Collider>(8);
        private static readonly System.Collections.Generic.List<Renderer> _insertRenderers =
            new System.Collections.Generic.List<Renderer>(8);

        /// <summary>
        /// Collision that matches the geometry, taken from the same sections the mesh was built
        /// from rather than computed a second time. A doorway that is open to the eye and shut
        /// to the body is the failure this exists to prevent, and computing the rectangles
        /// twice is how it happens.
        /// </summary>
        private static void AddWallColliders(GameObject go, bool hasDoor, bool hasWindow,
            float span, float height)
        {
            // Only a doorway is cut. A window is not a way through, so a window wall gets ONE
            // box across the whole span: correct, cheaper than three, and splitting it around
            // the opening would let the player climb through the window.
            bool oneBoxAcross = !hasDoor || hasWindow;
            if (oneBoxAcross)
            {
                var solid = go.AddComponent<BoxCollider>();
                solid.center = new Vector3(0f, height * 0.5f, 0f);
                solid.size = new Vector3(span, height, WallThickness);
                return;
            }

            var sections = StructuralMeshFactory.Sections(span, height, WallThickness,
                DoorWidth, DoorHeight, 0f);

            if (!sections.HasOpening)
            {
                var fallback = go.AddComponent<BoxCollider>();
                fallback.center = new Vector3(0f, height * 0.5f, 0f);
                fallback.size = new Vector3(span, height, WallThickness);
                return;
            }

            AddSectionCollider(go, sections.Left);
            AddSectionCollider(go, sections.Right);
            AddSectionCollider(go, sections.Header);
        }

        private static void AddSectionCollider(GameObject go, Bounds section)
        {
            if (section.size.x < 0.001f || section.size.y < 0.001f)
                return;

            var box = go.AddComponent<BoxCollider>();
            box.center = section.center;
            box.size = section.size;
        }

        // ----------------------------------------------------------------- materials

        // Three materials for the whole house, resolved once. One per wall would be forty
        // materials in a ten-room house and forty separate draw calls to go with them - so
        // these are shared across every room, and the density below is applied once rather
        // than per surface.
        private static Material _wall;
        private static Material _floor;
        private static Material _ceiling;
        private static Material _trim;

        private static Material WallMaterial(ModularInteriorCatalog catalog) =>
            Surface(ref _wall, catalog != null ? catalog.WallSurface : default,
                    catalog != null ? catalog.WallTuning : default,
                    new Color(0.72f, 0.70f, 0.67f), "CIYC_Wall");

        private static Material FloorMaterial(ModularInteriorCatalog catalog) =>
            Surface(ref _floor, catalog != null ? catalog.FloorSurface : default,
                    catalog != null ? catalog.FloorTuning : default,
                    new Color(0.42f, 0.39f, 0.36f), "CIYC_Floor");

        private static Material CeilingMaterial(ModularInteriorCatalog catalog) =>
            Surface(ref _ceiling, catalog != null ? catalog.CeilingSurface : default,
                    catalog != null ? catalog.CeilingTuning : default,
                    new Color(0.86f, 0.86f, 0.84f), "CIYC_Ceiling");

        /// <summary>
        /// Linings, frames and sills. A tiled material like any other, so it goes through the
        /// same resolution - including "no material means switch the renderer off", because a
        /// door frame drawn in Unity's built-in default is magenta under URP.
        /// </summary>
        private static Material TrimMaterial(ModularInteriorCatalog catalog)
        {
            if (_trim != null)
                return _trim;

            Material named = catalog != null ? catalog.TrimMaterial : null;
            if (named != null && IsDrawable(named, "CIYC_Trim"))
            {
                _trim = named;
                return _trim;
            }

            return Neutral(ref _trim, new Color(0.30f, 0.26f, 0.22f), "CIYC_RawTrim");
        }

        /// <summary>A fresh process has resolved nothing. Unity keeps statics across play mode.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSurfaces()
        {
            _wall = null;
            _floor = null;
            _ceiling = null;
            _trim = null;
        }

        /// <summary>
        /// The pack's material for this surface, at the density it was measured at - or the
        /// neutral stand-in if the catalog names none, or names one that cannot be drawn.
        ///
        /// <para>
        /// The vendor material is never modified. A COPY carries the tiling, because the pack
        /// normalises its UVs per piece while generated geometry writes them in metres: the two
        /// cannot agree unless one side is rescaled, and it must not be the side that is
        /// somebody's purchased asset. One copy per surface for the whole house, so this is
        /// three materials, not three per room.
        /// </para>
        /// </summary>
        private static Material Surface(ref Material slot, Content.SurfaceMaterial surface,
            Content.SurfaceTuning tuning, Color fallbackColour, string name)
        {
            if (slot != null)
                return slot;

            // ---- the override wins, and it is allowed to win outright ----------------------
            //
            // SurfaceMaterial records a MEASUREMENT. The wall's was measured; the floor's and
            // the ceiling's were DERIVED from it by texel parity, which is a reasonable guess
            // and was two wrong answers on screen - a parquet with hand-sized planks, and a
            // ceiling wearing a floor. A guess that can be overruled by a number somebody can
            // see is worth keeping; one that cannot is not.
            if (tuning.HasReplacement && IsDrawable(tuning.Replacement, name))
            {
                slot = ApplyTiling(tuning.Replacement, tuning, name);
                Core.CIYCLog.Info("[CIYC][House][Surface] " + name + " uses the project's own '" +
                                  tuning.Replacement.name + "'" + TilingSuffix(tuning) +
                                  " instead of the pack's.");
                return slot;
            }

            if (surface.IsSet && IsDrawable(surface.Material, name))
            {
                if (tuning.HasTiling)
                {
                    slot = ApplyTiling(surface.Material, tuning, name);
                    Core.CIYCLog.Info("[CIYC][House][Surface] " + name + " uses the pack's '" +
                                      surface.Material.name + "'" + TilingSuffix(tuning) +
                                      " (the measured density was overruled).");
                    return slot;
                }

                // An unknown size means "leave it alone": use the material exactly as authored
                // rather than inventing a number. Dividing by a zero would blow the texture up
                // to a single texel across the whole wall, which reads as a flat colour and
                // looks exactly like the missing texture this is meant to fix.
                if (!surface.HasDensity)
                {
                    slot = surface.Material;
                    return slot;
                }

                slot = new Material(surface.Material)
                {
                    name = name + "_" + surface.Material.name + "_perMetre"
                };

                RebaseToMetres(slot, surface.AuthoredAcrossMetres);
                Core.CIYCLog.Info("[CIYC][House][Surface] " + name + " uses the pack's '" +
                                  surface.Material.name + "' at its measured " +
                                  surface.AuthoredAcrossMetres.ToString("F2") + " m per tile.");
                return slot;
            }

            return Neutral(ref slot, fallbackColour, "CIYC_Raw" + name);
        }

        /// <summary>
        /// A COPY of the material at the wanted metres-per-tile, or the material itself when no
        /// size was asked for.
        ///
        /// <para>
        /// The vendor material is never edited - it is somebody's purchased asset and it is
        /// shared with whatever else uses it. One copy per surface for the whole house.
        /// </para>
        /// <para>
        /// The wanted density is reached by SCALING what the material already has rather than by
        /// writing an absolute number into every map. A URP Lit material carries up to eight
        /// textures and their tilings are deliberately different from each other - a detail
        /// normal is often eight times finer than the colour. Overwriting them all with one
        /// number flattens that on purpose-built materials; multiplying keeps it.
        /// </para>
        /// </summary>
        private static Material ApplyTiling(Material source, Content.SurfaceTuning tuning, string name)
        {
            if (!tuning.HasTiling)
                return source;

            var copy = new Material(source) { name = name + "_" + source.name + "_perMetre" };

            float wanted = 1f / tuning.MetresPerTile;
            Vector2 baseScale = BaseScaleOf(copy);
            var factor = new Vector2(
                baseScale.x > 0.0001f ? wanted / baseScale.x : wanted,
                baseScale.y > 0.0001f ? wanted / baseScale.y : wanted);

            ScaleAllMaps(copy, factor);
            return copy;
        }

        private static string TilingSuffix(Content.SurfaceTuning tuning) =>
            tuning.HasTiling ? " at " + tuning.MetresPerTile.ToString("F2") + " m per tile" : "";

        /// <summary>
        /// The tiling the material's MAIN map carries, which is the one the others are relative
        /// to. `_BaseMap` first because this project is URP; `_MainTex` after it, because a
        /// material converted from Built-in keeps that name and would otherwise read as 1.
        /// </summary>
        private static Vector2 BaseScaleOf(Material material)
        {
            if (material.HasProperty("_BaseMap"))
                return material.GetTextureScale("_BaseMap");

            if (material.HasProperty("_MainTex"))
                return material.GetTextureScale("_MainTex");

            string[] names = material.GetTexturePropertyNames();
            if (names != null && names.Length > 0 && material.HasProperty(names[0]))
                return material.GetTextureScale(names[0]);

            return Vector2.one;
        }

        /// <summary>
        /// Re-expresses a material whose UVs were normalised across one piece so that it reads
        /// correctly on geometry whose UVs are in metres.
        ///
        /// <para>
        /// EVERY texture property, not the colour map alone. A URP Lit material carries up to
        /// eight of them, and the measured wall materials use several - wallpaper3 has a detail
        /// normal and an occlusion map, beton adds a parallax map. Rescaling three of them and
        /// leaving the rest is not "mostly right": the colour moves and the surface detail
        /// stays, so the bumps stop sitting on the pattern they belong to. That is what a warped
        /// wall actually is, and it is much harder to recognise than a plainly wrong size.
        /// </para>
        /// <para>
        /// A single divisor for all of them, because they all shared one UV set to begin with.
        /// Relative differences between the maps - a detail map deliberately tiled eight times
        /// finer - survive, because each is divided rather than overwritten.
        /// </para>
        /// </summary>
        private static void RebaseToMetres(Material material, Vector2 authoredAcrossMetres)
        {
            ScaleAllMaps(material, new Vector2(1f / authoredAcrossMetres.x,
                                               1f / authoredAcrossMetres.y));
        }

        /// <summary>
        /// Multiplies EVERY texture's tiling by the same factor.
        ///
        /// <para>
        /// Every one, not the colour map alone. A URP Lit material carries up to eight, and the
        /// measured wall materials use several - wallpaper3 has a detail normal and an occlusion
        /// map, beton adds a parallax map. Rescaling three of them and leaving the rest is not
        /// "mostly right": the colour moves and the surface detail stays, so the bumps stop
        /// sitting on the pattern they belong to. That is what a warped wall actually is, and it
        /// is much harder to recognise than a plainly wrong size.
        /// </para>
        /// </summary>
        private static void ScaleAllMaps(Material material, Vector2 factor)
        {
            string[] names = material.GetTexturePropertyNames();
            if (names == null)
                return;

            for (int i = 0; i < names.Length; i++)
            {
                if (!material.HasProperty(names[i]))
                    continue;

                Vector2 authored = material.GetTextureScale(names[i]);
                material.SetTextureScale(names[i],
                    new Vector2(authored.x * factor.x, authored.y * factor.y));
            }
        }

        /// <summary>
        /// Whether this material will actually draw, rather than draw magenta.
        ///
        /// <para>
        /// Four ways a material reaches here unusable, and all four look identical on screen:
        /// a null shader, a shader the platform does not support, Unity's internal error shader
        /// (which IS the magenta), and an HDRP shader in a URP project. None of them throws and
        /// none of them logs, so each is checked and named. Refusing gives the neutral stand-in,
        /// which is a dull grey room - wrong, but legibly wrong.
        /// </para>
        /// </summary>
        private static bool IsDrawable(Material material, string role)
        {
            Shader shader = material.shader;

            if (shader == null)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Material '" + material.name +
                                   "' hat keinen Shader. Es wird NICHT benutzt.");
                return false;
            }

            string shaderName = shader.name ?? string.Empty;

            if (!shader.isSupported)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Shader '" + shaderName +
                                   "' wird auf dieser Plattform nicht unterstuetzt. Das " +
                                   "Material wird NICHT benutzt.");
                return false;
            }

            if (shaderName.IndexOf("InternalErrorShader", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.StartsWith("Hidden/", System.StringComparison.Ordinal))
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Shader '" + shaderName +
                                   "' ist Unitys Fehler-Shader - genau das, was als magenta " +
                                   "Flaeche erscheint. Das Material wird NICHT benutzt.");
                return false;
            }

            if (shaderName.IndexOf("HDRP", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("High Definition", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Shader '" + shaderName +
                                   "' ist HDRP. Dieses Projekt ist URP - HDRP-Shader zeichnen " +
                                   "hier magenta. Das Material wird NICHT benutzt.");
                return false;
            }

            return true;
        }

        private static Material Neutral(ref Material slot, Color colour, string name)
        {
            if (slot != null)
                return slot;

            // Never Shader.Find("Standard"): it resolves everywhere and draws solid magenta
            // under URP. Ask for the project's lit shader and accept null - a missing object is
            // better than a magenta one.
            var shader = Art.CiycShaders.FindLit();
            if (shader == null)
            {
                Core.CIYCLog.Error("[CIYC][House] Kein URP-Lit-Shader gefunden. Die Rohstruktur " +
                                   "bleibt ohne Material, statt magenta zu werden.");
                return null;
            }

            slot = new Material(shader) { name = name };
            slot.color = colour;
            return slot;
        }

        private static GameObject Piece(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            go.AddComponent<MeshRenderer>();

            // Through the ONE shared rule, not with an `if (material != null)` that quietly does
            // nothing. A MeshRenderer with no material assigned is not invisible - URP draws it
            // with its error shader, which is the same magenta this whole path exists to avoid.
            // PrimitiveSurface switches the renderer OFF instead and names what was missing; any
            // collider stays, so an invisible floor still holds the player up.
            Art.PrimitiveSurface.Apply(go, material, "Raumflaeche '" + name + "'");

            return go;
        }

        private static GameObject Pick(GameObject[] variants, int roomId, int role, int index)
        {
            if (variants == null || variants.Length == 0)
                return null;

            if (variants.Length == 1)
                return variants[0];

            var hash = Fnv1a64.Create();
            hash.WriteInt32(roomId);
            hash.WriteInt32(role);
            hash.WriteInt32(index);

            int pick = (int)(hash.Value % (ulong)variants.Length);
            return variants[pick];
        }

        private static bool WantsWindow(LayoutRoom room, SocketDirection direction)
        {
            if (room.Category == RoomCategory.Basement || room.Category == RoomCategory.Storage)
                return false;

            var hash = Fnv1a64.Create();
            hash.WriteInt32(room.RoomId);
            hash.WriteInt32((int)direction);
            hash.WriteString("window");
            return (hash.Value & 1UL) == 1UL;
        }

   }
}
