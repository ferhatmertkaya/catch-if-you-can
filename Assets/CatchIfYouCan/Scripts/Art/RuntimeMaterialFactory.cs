using UnityEngine;

namespace CatchIfYouCan.Art
{
    public static class RuntimeMaterialFactory
    {
        private static Material _darkWall;
        private static Material _neonGreenEmissive;
        private static Material _ectoplasm;
        private static Material _floor;
        private static Material _ghostDissolve;
        private static Material _uvEvidence;

        public static Material GetDarkWall()
        {
            if (_darkWall == null)
            {
                _darkWall = CreateLit("CIYC_DarkWall", new Color(0.08f, 0.09f, 0.1f), 0f);
                _darkWall.SetFloat("_Smoothness", 0.15f);
            }

            return _darkWall;
        }

        public static Material GetNeonGreenEmissive()
        {
            if (_neonGreenEmissive == null)
            {
                _neonGreenEmissive = CreateLit("CIYC_NeonGreen", new Color(0.05f, 0.12f, 0.06f), 2.5f);
                _neonGreenEmissive.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.35f) * 2.5f);
                _neonGreenEmissive.EnableKeyword("_EMISSION");
            }

            return _neonGreenEmissive;
        }

        public static Material GetEctoplasm()
        {
            if (_ectoplasm == null)
            {
                _ectoplasm = CreateLit("CIYC_Ectoplasm", new Color(0.15f, 0.45f, 0.25f, 0.65f), 1.2f);
                ConfigureTransparent(_ectoplasm);
            }

            return _ectoplasm;
        }

        public static Material GetFloor()
        {
            if (_floor == null)
            {
                _floor = CreateLit("CIYC_Floor", new Color(0.12f, 0.11f, 0.1f), 0f);
                _floor.SetFloat("_Smoothness", 0.05f);
            }

            return _floor;
        }

        public static Material GetGhostDissolve(Shader shader)
        {
            if (_ghostDissolve == null && shader != null)
            {
                _ghostDissolve = new Material(shader);
                _ghostDissolve.name = "CIYC_GhostDissolve_Runtime";
                _ghostDissolve.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.3f, 0.85f));
                _ghostDissolve.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.4f) * 3f);
            }

            return _ghostDissolve;
        }

        public static Material GetUVEvidence(Shader shader)
        {
            if (_uvEvidence == null && shader != null)
            {
                _uvEvidence = new Material(shader);
                _uvEvidence.name = "CIYC_UVEvidence_Runtime";
                _uvEvidence.SetFloat("_UVReveal", 0f);
            }

            return _uvEvidence;
        }

        private static Material CreateLit(string name, Color color, float emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            if (shader != null && shader.name.Contains("Universal"))
            {
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.2f);
            }

            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
            }

            return mat;
        }

        private static void ConfigureTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
