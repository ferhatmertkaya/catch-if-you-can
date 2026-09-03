using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Prop sizing, import orientation and vertex cost, against a known grid and a known human.</summary>
    [AddComponentMenu("Catch If You Can/Development/EnvironmentLabInstaller")]
    public sealed class EnvironmentLabInstaller : DevelopmentLabInstaller
    {
        [Tooltip("Resources folder scanned for props to lay out. Empty skips the shelf.")]
        [SerializeField] private string propResourceFolder = "Props";

        public override DevelopmentLab Lab => DevelopmentLab.Environment;

        private int _props;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(24f, 24f));
            BuildMetreGrid(Vector3.zero, 24);
            BuildHumanReference(new Vector3(-4f, 0f, 0f));
            BuildLabel("1.86 m", new Vector3(-4f, 2.0f, 0f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -8f));

            BuildSizeLadder();
            BuildOrientationGnomon();
            BuildPropShelf();
        }

        /// <summary>
        /// Cubes at 0.25, 0.5, 1, 2 and 3 metres. An imported prop is almost never the wrong
        /// shape; it is the wrong size, by a factor of a hundred, and this is what tells you
        /// which factor.
        /// </summary>
        private static void BuildSizeLadder()
        {
            float[] sizes = { 0.25f, 0.5f, 1f, 2f, 3f };
            float x = 2f;

            for (int i = 0; i < sizes.Length; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "DEV_Size_" + sizes[i] + "m";
                cube.transform.localScale = Vector3.one * sizes[i];
                cube.transform.position = new Vector3(x + sizes[i] * 0.5f, sizes[i] * 0.5f, 6f);

                BuildLabel(sizes[i] + " m", cube.transform.position + new Vector3(0f, sizes[i] * 0.5f + 0.2f, 0f));
                x += sizes[i] + 0.6f;
            }
        }

        /// <summary>
        /// A three-axis marker: red +X, green +Y, blue +Z. Half of importing a model is
        /// working out which way it thinks is forward, and an axis cross answers it without
        /// anyone having to remember whether the exporter negates X.
        /// </summary>
        private static void BuildOrientationGnomon()
        {
            var root = new GameObject("DEV_Gnomon");
            root.transform.position = new Vector3(-8f, 0f, 6f);

            BuildAxis(root.transform, "X", new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.06f, 0.06f));
            BuildAxis(root.transform, "Y", new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 1f, 0.06f));
            BuildAxis(root.transform, "Z", new Vector3(0f, 0f, 0.5f), new Vector3(0.06f, 0.06f, 1f));
        }

        private static void BuildAxis(Transform parent, string axis, Vector3 offset, Vector3 scale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "DEV_Axis_" + axis;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = offset;
            bar.transform.localScale = scale;

            BuildLabel("+" + axis, parent.position + offset * 2.2f);
        }

        /// <summary>
        /// Everything under Resources/Props, in a row, at its authored scale. A prop that is
        /// fine on its own and wrong next to the others is the common case.
        /// </summary>
        private void BuildPropShelf()
        {
            if (string.IsNullOrEmpty(propResourceFolder))
                return;

            var props = Resources.LoadAll<GameObject>(propResourceFolder);
            if (props == null || props.Length == 0)
                return;

            var shelf = new GameObject("DEV_PropShelf");
            for (int i = 0; i < props.Length; i++)
            {
                var position = new Vector3(-8f + i * 2f, 0f, -4f);
                var instance = Instantiate(props[i], position, Quaternion.identity, shelf.transform);
                instance.name = "DEV_Prop_" + props[i].name;
                BuildLabel(props[i].name, position + new Vector3(0f, 1.6f, 0f));
                _props++;
            }
        }

        protected override string DescribeState() =>
            "Floor 24x24, 1 m grid, 1.86 m reference, size ladder 0.25-3 m, axis gnomon, " +
            _props + " props from Resources/" + propResourceFolder + ".";
    }
}
