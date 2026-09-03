using CatchIfYouCan.Core;
using CatchIfYouCan.Core.SceneSetup;
using UnityEngine;

namespace CatchIfYouCan.Development
{
    /// <summary>
    /// What every development lab does, and the two rules that keep a lab from becoming a
    /// private fork of the game.
    ///
    /// <para>
    /// First: a lab never owns a persistent service. It calls the same
    /// <see cref="CiycServices"/> every production scene calls, so a system that behaves
    /// one way in the lab and another in the game cannot be explained by the lab having
    /// built its own manager.
    /// </para>
    ///
    /// <para>
    /// Second: a lab builds its fixtures in code rather than storing them in the scene.
    /// That keeps the scene asset almost empty, which means a lab costs nothing to merge,
    /// cannot rot into a second source of truth for room geometry, and can be regenerated
    /// from the tool that made it.
    /// </para>
    /// </summary>
    public abstract class DevelopmentLabInstaller : SceneInstallerBase
    {
        [Header("Development lab")]
        [Tooltip("Build the fixtures on entry. Off means an empty room, which is sometimes " +
                 "what you want while chasing something that the fixtures themselves affect.")]
        [SerializeField] protected bool buildFixtures = true;

        [Tooltip("Print what the lab set up. On by default; a lab that says nothing about " +
                 "its own state is a lab you cannot trust a measurement from.")]
        [SerializeField] protected bool logLabState = true;

        [Tooltip("Spawn the real player at DEV_PlayerSpawn. Off leaves the bootstrap camera " +
                 "looking at the room, which is what you want for a screenshot.")]
        [SerializeField] protected bool spawnPlayer = true;

        [Tooltip("Name of the marker the player is spawned on. Every lab builds one.")]
        [SerializeField] protected string playerSpawnMarker = PlayerSpawnMarkerName;

        /// <summary>Which lab this is, for logging and for the tooling that creates them.</summary>
        public abstract DevelopmentLab Lab { get; }

        /// <summary>The marker name every lab spawns the player on.</summary>
        public const string PlayerSpawnMarkerName = "DEV_PlayerSpawn";

        public sealed override void Install()
        {
            InstallSceneBasics();

            if (buildFixtures)
                BuildFixtures();

            if (spawnPlayer)
                SpawnLabPlayer();

            if (logLabState)
                CIYCLog.Info("Development lab '" + DevelopmentScenes.NameOf(Lab) + "' ready. " +
                             DescribeState());
        }

        /// <summary>Everything this lab puts in the room. Called once, after the basics.</summary>
        protected abstract void BuildFixtures();

        /// <summary>One line saying what was actually built, for the console.</summary>
        protected virtual string DescribeState() => "No fixtures declared.";

        // ---- shared fixture helpers -------------------------------------------------

        /// <summary>
        /// Spawns the real player on the lab's spawn marker, through the same
        /// <see cref="Player.PlayerSpawner"/> the game uses.
        ///
        /// <para>
        /// Deliberately not a lab-local player. The whole value of a lab is that what you are
        /// looking at is the shipping system with the room taken away; a lab that built its
        /// own simplified player would answer questions about the lab's player.
        /// </para>
        /// </summary>
        protected GameObject SpawnLabPlayer()
        {
            CiycServices.EnsureCore();

            var marker = GameObject.Find(playerSpawnMarker);
            if (marker == null)
            {
                CIYCLog.Warn("Lab '" + DevelopmentScenes.NameOf(Lab) + "' has no '" +
                             playerSpawnMarker + "', so the player was spawned at the origin.");
            }

            var result = marker != null
                ? Player.PlayerSpawner.Spawn(marker.transform)
                : Player.PlayerSpawner.Spawn(Vector3.zero, Quaternion.identity);

            return result?.Root;
        }

        /// <summary>
        /// A wall. Labs are built out of these rather than out of the production room, because
        /// a lab that shares the production room starts answering questions about it.
        /// </summary>
        protected static GameObject BuildWall(string name, Vector3 centre, Vector3 size,
                                              Transform parent = null)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = centre;
            wall.transform.localScale = size;
            return wall;
        }

        /// <summary>
        /// Four walls around a floor, with an optional gap in the north wall wide enough to
        /// walk through. The gap is what makes a two-room lab possible, and a doorway is the
        /// one piece of geometry audio occlusion and navigation both care about.
        /// </summary>
        protected static GameObject BuildRoomShell(string name, Vector3 centre, Vector2 size,
                                                   float height = 3f, float doorwayWidth = 0f)
        {
            var root = new GameObject(name);
            root.transform.position = centre;

            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;
            var t = new Vector3(0.2f, height, 0f);

            BuildWall(name + "_South", centre + new Vector3(0f, height * 0.5f, -halfZ),
                      new Vector3(size.x, height, t.x), root.transform);
            BuildWall(name + "_West", centre + new Vector3(-halfX, height * 0.5f, 0f),
                      new Vector3(t.x, height, size.y), root.transform);
            BuildWall(name + "_East", centre + new Vector3(halfX, height * 0.5f, 0f),
                      new Vector3(t.x, height, size.y), root.transform);

            if (doorwayWidth <= 0f)
            {
                BuildWall(name + "_North", centre + new Vector3(0f, height * 0.5f, halfZ),
                          new Vector3(size.x, height, t.x), root.transform);
                return root;
            }

            // Two piers and a header, so the opening is a doorway rather than a missing wall.
            float pier = (size.x - doorwayWidth) * 0.5f;
            float pierCentre = (doorwayWidth + pier) * 0.5f;
            BuildWall(name + "_NorthL", centre + new Vector3(-pierCentre, height * 0.5f, halfZ),
                      new Vector3(pier, height, t.x), root.transform);
            BuildWall(name + "_NorthR", centre + new Vector3(pierCentre, height * 0.5f, halfZ),
                      new Vector3(pier, height, t.x), root.transform);
            BuildWall(name + "_NorthHeader", centre + new Vector3(0f, height - 0.3f, halfZ),
                      new Vector3(doorwayWidth, 0.6f, t.x), root.transform);

            return root;
        }

        /// <summary>
        /// A floating label. A row of grey boxes tells you nothing about which is which, and a
        /// lab whose fixtures have to be identified from the hierarchy is a lab you use once.
        /// </summary>
        protected static GameObject BuildLabel(string text, Vector3 position, Transform parent = null,
                                               float size = 0.02f)
        {
            var go = new GameObject("DEV_Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.fontSize = 96;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.6f, 1f, 0.7f);

            return go;
        }

        /// <summary>
        /// Writes a serialized private field on a component the lab is wiring up.
        ///
        /// <para>
        /// Reflection, and confined to development code on purpose. The alternative is adding
        /// a public setter to every interactable, light controller and hide spot in the game
        /// so that a lab can build one - which would change shipping classes to suit a
        /// harness. If a rename breaks a lab, the lab logs it and the lab is the only thing
        /// that breaks.
        /// </para>
        /// </summary>
        protected static void WireLabField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null)
            {
                CIYCLog.Warn("Lab wiring: " + target.GetType().Name + " has no field '" +
                             fieldName + "'. It was renamed or removed, and this fixture is " +
                             "now only half built.");
                return;
            }

            field.SetValue(target, value);
        }

        /// <summary>The lab's on-screen readout, created on first use.</summary>
        protected DevelopmentLabReadout Readout() =>
            DevelopmentLabReadout.Ensure("CIYC " + DevelopmentScenes.NameOf(Lab));

        /// <summary>
        /// A surface that reports a footstep type. Footsteps are chosen by what is underfoot,
        /// so a lab floor with no surface component makes every step the default one.
        /// </summary>
        protected static GameObject BuildFootstepSurface(string name, Vector3 centre, Vector2 size,
                                                         Audio.SurfaceType surface, bool indoor = true)
        {
            var pad = BuildFloor(centre, size, name);
            var component = pad.AddComponent<Audio.FootstepSurface>();
            WireLabField(component, "surface", surface);
            WireLabField(component, "indoor", indoor);

            BuildLabel(surface.ToString().ToUpperInvariant(), centre + new Vector3(0f, 0.3f, 0f));
            return pad;
        }

        /// <summary>A labelled stand: a plinth with a caption, for putting one thing on.</summary>
        protected static Transform BuildPlinth(string label, Vector3 position, Transform parent = null)
        {
            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth.name = "DEV_Plinth_" + label;
            plinth.transform.SetParent(parent, false);
            plinth.transform.position = position + new Vector3(0f, 0.45f, 0f);
            plinth.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);

            BuildLabel(label, position + new Vector3(0f, 1.05f, 0f), plinth.transform);
            return plinth.transform;
        }


        /// <summary>
        /// A plain lit box to stand in. Built from primitives on purpose: a lab that shares
        /// the production room would start answering questions about the production room.
        /// </summary>
        protected static GameObject BuildFloor(Vector3 centre, Vector2 size, string name = "DEV_Floor")
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.position = centre + new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(size.x, 0.1f, size.y);
            return floor;
        }

        /// <summary>A labelled marker. Returns the transform so callers can parent to it.</summary>
        protected static Transform BuildMarker(string name, Vector3 position, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>
        /// A 1 m reference grid on the floor. Distances in a lab are only meaningful if
        /// something in shot has a known size.
        /// </summary>
        protected static GameObject BuildMetreGrid(Vector3 origin, int cells, Transform parent = null)
        {
            var root = new GameObject("DEV_MetreGrid");
            root.transform.SetParent(parent, false);
            root.transform.position = origin;

            for (int i = 0; i <= cells; i++)
            {
                MakeLine(root.transform, new Vector3(i - cells * 0.5f, 0.002f, 0f),
                         new Vector3(0.02f, 0.004f, cells));
                MakeLine(root.transform, new Vector3(0f, 0.002f, i - cells * 0.5f),
                         new Vector3(cells, 0.004f, 0.02f));
            }

            return root;
        }

        private static void MakeLine(Transform parent, Vector3 localPosition, Vector3 scale)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "GridLine";
            line.transform.SetParent(parent, false);
            line.transform.localPosition = localPosition;
            line.transform.localScale = scale;

            var collider = line.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        /// <summary>
        /// A 1.86 m capsule, the player's own capsule height. The single most useful object
        /// in an art lab, because "does this look right" is unanswerable without it.
        /// </summary>
        protected static GameObject BuildHumanReference(Vector3 position, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DEV_HumanReference_1m86";
            go.transform.SetParent(parent, false);
            go.transform.position = position + new Vector3(0f, 0.93f, 0f);
            go.transform.localScale = new Vector3(0.6f, 0.93f, 0.6f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            return go;
        }
    }
}
