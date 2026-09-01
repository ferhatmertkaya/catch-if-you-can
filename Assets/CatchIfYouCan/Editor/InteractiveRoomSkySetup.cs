#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Creates the night skybox materials from the generated panoramas.
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
    /// The materials go into <c>Resources</c> on purpose. Anything in Resources is always built,
    /// so the shader is guaranteed to survive shader stripping on a mobile player — a runtime
    /// <c>Shader.Find</c> with nothing referencing the shader is the classic way to get a sky that
    /// works in the editor and is pink on the device.
    /// </para>
    /// </summary>
    public static class InteractiveRoomSkySetup
    {
        private const string SkyResourceFolder = "Assets/CatchIfYouCan/Resources/Sky";
        private const string PanoramaFolder =
            "Assets/CatchIfYouCan/Art/Environment/InteractiveRoom/Sky";

        private static readonly string[] Panoramas =
        {
            "CIYC_NightSky_Clear",
            "CIYC_NightSky_Cloudy",
        };

        [MenuItem("Catch If You Can/Characters/Build Interactive Room Sky", false, 30)]
        public static void Build()
        {
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogError("[CIYC] Skybox/Panoramic not found. The window will fall back to a " +
                               "flat background colour.");
                return;
            }

            EnsureFolder(SkyResourceFolder);

            int made = 0;
            for (int i = 0; i < Panoramas.Length; i++)
            {
                string texPath = PanoramaFolder + "/" + Panoramas[i] + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (texture == null)
                {
                    Debug.LogWarning("[CIYC] Missing sky panorama: " + texPath);
                    continue;
                }

                string matPath = SkyResourceFolder + "/" + Panoramas[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                bool isNew = material == null;
                if (isNew)
                    material = new Material(shader);

                material.shader = shader;
                material.SetTexture("_MainTex", texture);
                // Latitude-longitude, not a 180 degree dome, and no stereo split.
                material.SetFloat("_Mapping", 1f);
                material.SetFloat("_ImageType", 0f);
                material.SetFloat("_Layout", 0f);
                material.SetFloat("_Rotation", 0f);
                // Half exposure: these panoramas are authored for night and the default doubles
                // them, which would make the moon glow read as daylight through the glass.
                material.SetFloat("_Exposure", 0.55f);
                material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));

                if (isNew)
                    AssetDatabase.CreateAsset(material, matPath);
                else
                    EditorUtility.SetDirty(material);

                made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CIYC] Interactive room sky: " + made + " skybox material(s) in " +
                      SkyResourceFolder);
        }

        /// <summary>True when the sky materials are already there.</summary>
        public static bool IsBuilt()
        {
            for (int i = 0; i < Panoramas.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(
                        SkyResourceFolder + "/" + Panoramas[i] + ".mat") == null)
                    return false;
            }
            return true;
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
