using CatchIfYouCan.Art;
using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Forward+ behaviour, the mirror, post-processing, and the shader board that catches stripping before a device build does.</summary>
    [AddComponentMenu("Catch If You Can/Development/LightingLabInstaller")]
    public sealed class LightingLabInstaller : DevelopmentLabInstaller
    {
        [Tooltip("How many point lights to put in one place. Forward+ handles many local " +
                 "lights, and 'many' is a number worth being able to turn up.")]
        [SerializeField, Min(0)] private int lightCount = 8;

        public override DevelopmentLab Lab => DevelopmentLab.Lighting;

        private int _shadersPresent;
        private int _shadersMissing;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(24f, 16f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -6f));
            BuildHumanReference(new Vector3(-6f, 0f, 0f));

            BuildSurfaceLadder();
            BuildLightCluster();
            BuildShaderBoard();
        }

        /// <summary>
        /// Five spheres from black to white. "Is this too dark" is unanswerable without
        /// knowing what the renderer does to a known albedo, and a mid-grey next to a white
        /// answers it in one look.
        /// </summary>
        private static void BuildSurfaceLadder()
        {
            var shader = CiycShaders.FindLit();
            float[] values = { 0.03f, 0.18f, 0.4f, 0.7f, 0.95f };

            for (int i = 0; i < values.Length; i++)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "DEV_Albedo_" + Mathf.RoundToInt(values[i] * 100f);
                ball.transform.position = new Vector3(-4f + i * 2f, 1f, 4f);

                if (shader == null)
                    continue;

                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(values[i], values[i], values[i]));
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.25f);
                ball.GetComponent<Renderer>().sharedMaterial = mat;
            }

            BuildLabel("ALBEDO LADDER 3% - 95%", new Vector3(0f, 2.2f, 4f));
        }

        /// <summary>
        /// A cluster of point lights in one corner. Forward+ is chosen for exactly this case,
        /// and the case only shows itself when the lights overlap.
        /// </summary>
        private void BuildLightCluster()
        {
            var root = new GameObject("DEV_LightCluster");
            root.transform.position = new Vector3(6f, 0f, 0f);

            for (int i = 0; i < lightCount; i++)
            {
                float angle = i / Mathf.Max(1f, lightCount) * Mathf.PI * 2f;
                var go = new GameObject("DEV_PointLight_" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition =
                    new Vector3(Mathf.Sin(angle) * 1.5f, 2f, Mathf.Cos(angle) * 1.5f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 6f;
                light.intensity = 1.4f;
                light.color = Color.HSVToRGB(i / Mathf.Max(1f, lightCount), 0.5f, 1f);
                // Additional-light shadows are off in the URP asset; asking for them here
                // would cost the sort and give nothing back.
                light.shadows = LightShadows.None;
            }

            BuildLabel(lightCount + " OVERLAPPING POINT LIGHTS", new Vector3(6f, 3.2f, 0f));
        }

        /// <summary>
        /// One quad per project shader, each either drawing or visibly absent.
        ///
        /// <para>
        /// This is the lab's reason to exist. A shader nothing references is stripped from a
        /// player build, and the failure shows up as a magenta surface on a device weeks
        /// later. Here it shows up as a missing quad and a line in the console, in the editor,
        /// today.
        /// </para>
        /// </summary>
        private void BuildShaderBoard()
        {
            string[] names =
            {
                CiycShaders.Lit, CiycShaders.GhostDissolve, CiycShaders.UVEvidence,
                CiycShaders.SpectralGrid, CiycShaders.ElectronicGlitch,
            };

            for (int i = 0; i < names.Length; i++)
            {
                var position = new Vector3(-4f + i * 2f, 1.5f, -4f);
                var shader = CiycShaders.Find(names[i]);
                string label = names[i].Substring(names[i].LastIndexOf('/') + 1);

                if (shader == null)
                {
                    _shadersMissing++;
                    BuildLabel(label + "\nMISSING", position);
                    continue;
                }

                _shadersPresent++;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "DEV_Shader_" + label;
                quad.transform.position = position;
                quad.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = new Material(shader);

                BuildLabel(label, position + new Vector3(0f, 1f, 0f));
            }
        }

        protected override string DescribeState() =>
            "Floor 24x16, albedo ladder, " + lightCount + " overlapping point lights, shader " +
            "board: " + _shadersPresent + " present, " + _shadersMissing + " MISSING.";
    }
}
