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

        /// <summary>Which lab this is, for logging and for the tooling that creates them.</summary>
        public abstract DevelopmentLab Lab { get; }

        public sealed override void Install()
        {
            InstallSceneBasics();

            if (buildFixtures)
                BuildFixtures();

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
