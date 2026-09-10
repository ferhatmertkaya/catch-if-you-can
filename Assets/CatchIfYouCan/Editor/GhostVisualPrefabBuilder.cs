using System.Collections.Generic;
using CatchIfYouCan.Ghost;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Builds the ghost visual prefabs the runtime looks for and has never found.
    ///
    /// <para>
    /// <see cref="GhostFactory"/> loads <c>Resources/Ghosts/{id}</c>, falling back to
    /// <c>Resources/Ghosts/profile_{profile}</c>. The path was wrong until V2 - it carried the
    /// project name twice and named a folder that had never existed - and since it was fixed,
    /// nothing has been authored at the corrected path either. So every ghost in the game is
    /// still the primitive capsule, and now says so at runtime.
    /// </para>
    ///
    /// <para>
    /// This is the tool that closes it. It is an editor builder rather than hand-written prefab
    /// YAML on purpose: a prefab is a graph of cross-referencing documents with GUIDs into an
    /// imported model, and hand-writing one is how references get silently broken. The model
    /// sources are the ones <see cref="GhostVisualCatalog"/> already names, and all six exist
    /// in the repository.
    /// </para>
    /// </summary>
    public static class GhostVisualPrefabBuilder
    {
        private const string ResourcesRoot = "Assets/CatchIfYouCan/Resources/Ghosts";

        [MenuItem("Catch If You Can/4. SPIELINHALT/Ghosts/Geist-Prefabs [SCHREIBT ASSET]", false, 420)]
        public static void BuildAll()
        {
            var definitions = GhostDefinitionFactory.CreateAllDefaultGhosts();
            if (definitions == null || definitions.Length == 0)
            {
                Debug.LogError("[CIYC] No ghost definitions to build visuals for.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
            {
                AssetDatabase.CreateFolder("Assets/CatchIfYouCan/Resources", "Ghosts");
            }

            var built = new List<string>();
            var skipped = new List<string>();

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                string modelPath = GhostVisualCatalog.GetModelAssetPath(definition.Id);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

                if (model == null)
                {
                    // Named, not swallowed. A model the catalog points at and the project does
                    // not contain is exactly the failure this whole path already had once.
                    skipped.Add(definition.Id + " (no model at " + modelPath + ")");
                    continue;
                }

                string prefabPath = ResourcesRoot + "/" + definition.Id + ".prefab";
                if (BuildOne(definition, model, prefabPath))
                    built.Add(definition.Id);
                else
                    skipped.Add(definition.Id + " (save failed)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CIYC] Ghost visuals built: " + built.Count + " of " + definitions.Length +
                      (built.Count > 0 ? " -> " + string.Join(", ", built) : ""));

            if (skipped.Count > 0)
                Debug.LogWarning("[CIYC] Ghost visuals skipped: " + string.Join("; ", skipped));
        }

        /// <summary>
        /// One ghost: the model under a root carrying the components the runtime expects, at
        /// the scale and offset the catalog specifies for that entity.
        ///
        /// <para>
        /// The components are added here rather than left to <c>GhostFactory.SpawnFromPrefab</c>
        /// so the prefab is complete on disk and a missing one is visible in the inspector
        /// rather than only at runtime. The factory's <c>?? AddComponent</c> calls stay as the
        /// safety net they are.
        /// </para>
        /// </summary>
        private static bool BuildOne(GhostDefinition definition, GameObject model, string prefabPath)
        {
            var root = new GameObject("Ghost_" + definition.Id);

            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (visual == null)
                    return false;

                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);

                float scale = GhostVisualCatalog.GetScaleMultiplier(definition.Id);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition =
                    new Vector3(0f, GhostVisualCatalog.GetVerticalOffset(definition.Id), 0f);

                // Where the ghost sees from. Set through the component's own public method;
                // the factory used to poke this private field by reflection.
                var eye = new GameObject("EyePoint");
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(0f, 1.5f * scale, 0f);

                var agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.height = 1.8f * scale;
                agent.radius = 0.35f * scale;
                agent.baseOffset = 0.1f;
                // Fully qualified because this file has no `using UnityEngine.AI`, and that is
                // where the enum lives - alongside NavMeshAgent, not in UnityEngine. The
                // comment that used to sit here claimed the opposite and was wrong; the
                // offline stub happened to encode the same mistake, so nothing here could
                // catch it. A real compiler on a real machine did.
                agent.obstacleAvoidanceType =
                    UnityEngine.AI.ObstacleAvoidanceType.HighQualityObstacleAvoidance;

                root.AddComponent<GhostPerception>().SetEyePoint(eye.transform);
                root.AddComponent<GhostController>().EnsureManifestationRenderers();

                root.tag = "Ghost";

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
