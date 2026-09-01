#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Creates the night skybox material for the interactive room's window view.
    ///
    /// <para>
    /// Built here rather than checked in as hand-written YAML for a specific reason: the material
    /// needs Unity's built-in <c>Skybox/Panoramic</c> shader, and a built-in shader is referenced
    /// by a numeric file ID inside Unity's own resource bundle. Guessing that number is exactly
    /// the kind of mistake that produces a magenta sky and no way to tell why. Asking Unity for
    /// the shader by name and letting it write the reference cannot be wrong.
    /// </para>
    ///
    /// <para>
    /// The material goes into <c>Resources</c> on purpose. Anything in Resources is always built,
    /// so the shader is guaranteed to survive shader stripping on a mobile player — a runtime
    /// <c>Shader.Find</c> with nothing referencing the shader is the classic way to get a sky that
    /// works in the editor and is pink on the device.
    /// </para>
    ///
    /// <para>
    /// This step also owns the panorama's mobile import settings, for the reason set out in
    /// <see cref="NathanTextureImportSettings"/>: a stored iOS entry left on <c>Automatic</c>
    /// resolves against the platform's retired PVRTC default and warns in Unity 6.5. The committed
    /// .meta therefore carries no mobile entry at all, and the format is named here as a
    /// <see cref="TextureImporterFormat"/> constant so there is no enum value to get wrong.
    /// </para>
    /// </summary>
    public static class InteractiveRoomSkySetup
    {
        private const string SkyResourceFolder = "Assets/CatchIfYouCan/Resources/Sky";
        private const string PanoramaFolder =
            "Assets/CatchIfYouCan/Art/Environment/InteractiveRoom/Sky";

        /// <summary>
        /// The one sky the room uses. Named without the <c>CIYC_</c> prefix the panoramas carry
        /// because it is a material, and materials here are <c>MAT_*</c>.
        /// </summary>
        private const string SkyMaterialName = "MAT_Skybox_HauntedNight";

        private const string PanoramaName = "CIYC_HauntedNight_Panorama";

        /// <summary>
        /// 2048 on every platform. The source art is 1774 px wide, so nothing above 2048 would
        /// add detail, and 2048x1024 in ASTC 6x6 is about 1.2 MB with mips — affordable for the
        /// single texture that is the entire outside world.
        /// </summary>
        private const int PanoramaMaxSize = 2048;

        /// <summary>
        /// Sky materials this step used to create. Left behind in a project that has already run
        /// an older version of this tool they would still be picked up by
        /// <c>Resources.LoadAll</c> and could be chosen over the real sky, so they are removed.
        /// The source panoramas stay on disk under Art/, unreferenced and therefore not built.
        /// </summary>
        private static readonly string[] RetiredMaterials =
        {
            "CIYC_NightSky_Clear",
            "CIYC_NightSky_Cloudy",
        };

        [MenuItem("Catch If You Can/Environment/Build Interactive Room Sky", false, 30)]
        public static void Build()
        {
            var log = new StringBuilder();
            log.AppendLine("[CIYC] Interactive room sky");

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogError("[CIYC] Skybox/Panoramic not found. The window will fall back to a " +
                               "flat background colour.");
                return;
            }

            EnsureFolder(SkyResourceFolder);
            RemoveRetiredMaterials(log);

            string texPath = PanoramaFolder + "/" + PanoramaName + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (texture == null)
            {
                Debug.LogError("[CIYC] Missing sky panorama: " + texPath);
                return;
            }

            ApplyPanoramaImportSettings(texPath, log);

            string matPath = SkyResourceFolder + "/" + SkyMaterialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader);

            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            // Latitude-longitude, a full 360 sphere, not a 180 dome and not a stereo pair.
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_Layout", 0f);
            // The art is already authored for night: its mean luminance is 16/255. Unity's
            // neutral exposure is 1, so it is left neutral rather than pushed either way; the
            // runtime varies it slightly per session. 0.5 grey is this shader's neutral tint.
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));

            if (isNew)
                AssetDatabase.CreateAsset(material, matPath);
            else
                EditorUtility.SetDirty(material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine("  " + SkyMaterialName + " (Skybox/Panoramic, latlong 360)");
            log.AppendLine("  panorama " + PanoramaName + " at " + PanoramaMaxSize +
                           " PC/Android/iOS");
            Debug.Log(log.ToString());
        }

        /// <summary>True when the sky material is already there.</summary>
        public static bool IsBuilt()
        {
            return AssetDatabase.LoadAssetAtPath<Material>(
                       SkyResourceFolder + "/" + SkyMaterialName + ".mat") != null;
        }

        private static void RemoveRetiredMaterials(StringBuilder log)
        {
            for (int i = 0; i < RetiredMaterials.Length; i++)
            {
                string path = SkyResourceFolder + "/" + RetiredMaterials[i] + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                    continue;

                AssetDatabase.DeleteAsset(path);
                log.AppendLine("  retired placeholder sky " + RetiredMaterials[i]);
            }
        }

        private static void ApplyPanoramaImportSettings(string texPath, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null)
                return;

            importer.maxTextureSize = PanoramaMaxSize;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            // Repeat across U or the 360 wrap tears the moment the sky is rotated; clamp in V or
            // the poles bleed into each other.
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;

            // Desktop keeps Automatic: the BC family it resolves to is current.
            SetPlatform(importer, "Standalone", PanoramaMaxSize, TextureImporterFormat.Automatic);
            // ASTC 6x6 is about 3.6 bits per pixel. A night sky is mostly a smooth gradient, which
            // is what ASTC handles best, and the stars are single bright pixels rather than edges.
            SetPlatform(importer, "Android", PanoramaMaxSize, TextureImporterFormat.ASTC_6x6);
            SetPlatform(importer, "iPhone", PanoramaMaxSize, TextureImporterFormat.ASTC_6x6);

            importer.SaveAndReimport();
            log.AppendLine("  import: PC Automatic, Android/iOS ASTC_6x6, no entry left on " +
                           "Automatic so nothing resolves to PVRTC");
        }

        private static void SetPlatform(TextureImporter importer, string platform, int maxSize,
                                        TextureImporterFormat format)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
