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
        private static Material _spectralGrid;
        private static Material _electronicGlitch;
        private static Material _uiSlime;

        public static Material GetDarkWall()
        {
            if (_darkWall == null)
            {
                _darkWall = CreateLit("CIYC_DarkWall", new Color(0.08f, 0.09f, 0.1f), 0f);
                _darkWall?.SetFloat("_Smoothness", 0.15f);
            }

            return _darkWall;
        }

        public static Material GetNeonGreenEmissive()
        {
            if (_neonGreenEmissive == null)
            {
                _neonGreenEmissive = CreateLit("CIYC_NeonGreen", new Color(0.05f, 0.12f, 0.06f), 2.5f);
                if (_neonGreenEmissive != null)
                {
                    _neonGreenEmissive.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.35f) * 2.5f);
                    _neonGreenEmissive.EnableKeyword("_EMISSION");
                }
            }

            return _neonGreenEmissive;
        }

        public static Material GetEctoplasm()
        {
            if (_ectoplasm == null)
            {
                _ectoplasm = CreateLit("CIYC_Ectoplasm", new Color(0.15f, 0.45f, 0.25f, 0.65f), 1.2f);
                if (_ectoplasm != null)
                    ConfigureTransparent(_ectoplasm);
            }

            return _ectoplasm;
        }

        public static Material GetFloor()
        {
            if (_floor == null)
            {
                _floor = CreateLit("CIYC_Floor", new Color(0.12f, 0.11f, 0.1f), 0f);
                _floor?.SetFloat("_Smoothness", 0.05f);
            }

            return _floor;
        }

        /// <summary>Resources folder holding one material per custom shader.</summary>
        private const string MaterialResourceFolder = "Materials/";

        /// <summary>
        /// The authored material for one of this project's own shaders.
        ///
        /// <para>
        /// These exist so the shaders exist. A shader that no material references is stripped
        /// from a player build, and the code that then asked for it by name got null and fell
        /// back to a built-in shader - which draws magenta under URP. One material per shader,
        /// under Resources so it is included whether or not a scene happens to reference it,
        /// removes the whole failure.
        /// </para>
        /// </summary>
        private static Material LoadShared(ref Material cache, string materialName)
        {
            if (cache != null)
                return cache;

            cache = Resources.Load<Material>(MaterialResourceFolder + materialName);
            if (cache == null)
            {
                Core.CIYCLog.Warn("No material at Resources/" + MaterialResourceFolder +
                                  materialName + ". Its shader is very likely not in this " +
                                  "build either.");
            }

            return cache;
        }

        /// <summary>The ghost's dissolve material, or null if it was not imported.</summary>
        public static Material GetGhostDissolve() =>
            LoadShared(ref _ghostDissolve, "MAT_GhostDissolve");

        /// <summary>
        /// The UV-reveal material used for fingerprints and salt trails. It is not set to
        /// hidden here: _UVReveal defaults to 0 in the shader, and writing to a shared
        /// material asset at runtime edits the asset itself in the editor.
        /// </summary>
        public static Material GetUVEvidence() =>
            LoadShared(ref _uvEvidence, "MAT_UVEvidence");

        /// <summary>The spectral grid projector's material.</summary>
        public static Material GetSpectralGrid() =>
            LoadShared(ref _spectralGrid, "MAT_SpectralGrid");

        /// <summary>The electronic-interference glitch material.</summary>
        public static Material GetElectronicGlitch() =>
            LoadShared(ref _electronicGlitch, "MAT_ElectronicGlitch");

        /// <summary>The slime effect used on UI graphics.</summary>
        public static Material GetUISlime() =>
            LoadShared(ref _uiSlime, "MAT_UISlime");

        private static Material CreateLit(string name, Color color, float emission)
        {
            // No Standard fallback. It resolves everywhere and draws magenta under URP, so
            // it turned "this shader is missing" into "this object is bright pink" - which is
            // much harder to trace back to a shader.
            var shader = CiycShaders.FindLit();
            if (shader == null)
                return null;

            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);

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
