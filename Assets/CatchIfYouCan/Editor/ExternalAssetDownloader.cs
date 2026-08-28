using System.Collections.Generic;
using System.IO;
using CatchIfYouCan.Content;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CatchIfYouCan.EditorTools
{
    public static class ExternalAssetDownloader
    {
        private const string CreepCreatureUrl =
            "https://raw.githubusercontent.com/511action/descent-3d-assets/main/models/CreepCreature.glb";

        public static bool EnsureBundledAssetsPresent()
        {
#if UNITY_EDITOR
            bool ok = true;
            ok &= EnsureCreepCreature();
            AssetDatabase.Refresh();
            return ok;
#else
            return File.Exists($"{ExternalAssetPaths.QuaterniusMonsters}/CreepCreature.glb");
#endif
        }

#if UNITY_EDITOR
        [MenuItem("Catch If You Can/Download Missing External Assets")]
        public static void DownloadMissingMenu()
        {
            bool ok = EnsureBundledAssetsPresent();
            EditorUtility.DisplayDialog(
                "Download Missing Assets",
                ok ? "All required external assets are present." : "Some downloads failed — see Console.",
                "OK");
        }

        private static bool EnsureCreepCreature()
        {
            string dest = $"{ExternalAssetPaths.QuaterniusMonsters}/CreepCreature.glb";
            if (File.Exists(dest))
                return true;

            EnsureFolder(ExternalAssetPaths.QuaterniusMonsters);

            using (var request = UnityWebRequest.Get(CreepCreatureUrl))
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Downloading CreepCreature.glb",
                            "Fetching from GitHub…",
                            op.progress))
                    {
                        request.Abort();
                        EditorUtility.ClearProgressBar();
                        return false;
                    }
                }

                EditorUtility.ClearProgressBar();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[ExternalAssetDownloader] CreepCreature download failed: {request.error}");
                    return false;
                }

                File.WriteAllBytes(dest, request.downloadHandler.data);
                Debug.Log($"[ExternalAssetDownloader] Saved {dest} ({request.downloadHandler.data.Length} bytes)");
                return true;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
#endif
    }
}
