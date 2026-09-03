using CatchIfYouCan.Art;
using UnityEngine;
using UnityEngine.AI;

namespace CatchIfYouCan.Ghost
{
    public static class GhostFactory
    {
        private static readonly Color NeonGreen = new Color(0.2f, 1f, 0.35f);

        public static GameObject Create(GhostDefinition definition, Vector3 position)
        {
            if (definition != null)
            {
                var resourcePrefab = LoadBundledPrefab(definition);
                if (resourcePrefab != null)
                    return SpawnFromPrefab(resourcePrefab, definition, position);
            }

            return CreatePrimitiveFallback(definition, position);
        }

        private static bool _warnedMissingVisual;

        /// <summary>
        /// The ghost's real model, or null.
        ///
        /// <para>
        /// The Resources path itself was fixed in V2 - it used to contain the project name
        /// twice and resolved to a folder that had never existed - but nothing has been
        /// authored at the corrected path either, so every lookup still misses and every ghost
        /// in the game is still the primitive capsule. The difference now is that it says so.
        /// A silent fallback is how the original bug survived the life of the project.
        /// </para>
        /// </summary>
        private static GameObject LoadBundledPrefab(GhostDefinition definition)
        {
            if (definition.Prefab != null)
                return definition.Prefab;

            var byId = Resources.Load<GameObject>(GhostVisualCatalog.GetPrefabResourcePath(definition.Id));
            if (byId != null)
                return byId;

            var byProfile = Resources.Load<GameObject>(
                GhostVisualCatalog.GetPrefabResourcePath(definition.VisualProfile));
            if (byProfile != null)
                return byProfile;

            // Once. A ghost spawns per mission, and a warning per spawn would be noise; a
            // warning that never fires is how a whole content pipeline goes missing unnoticed.
            if (!_warnedMissingVisual)
            {
                _warnedMissingVisual = true;
                Core.CIYCLog.Warn(
                    "No ghost visual at Resources/" +
                    GhostVisualCatalog.GetPrefabResourcePath(definition.Id) + " or Resources/" +
                    GhostVisualCatalog.GetPrefabResourcePath(definition.VisualProfile) +
                    ". Every ghost will be the DEV_PLACEHOLDER capsule. Build the prefabs with " +
                    "Catch If You Can > Ghosts > Build Ghost Visual Prefabs.");
            }

            return null;
        }

        private static GameObject SpawnFromPrefab(GameObject prefab, GhostDefinition definition, Vector3 position)
        {
            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = $"Ghost_{definition?.DisplayName ?? prefab.name}";

            var controller = instance.GetComponent<GhostController>() ?? instance.AddComponent<GhostController>();
            controller.EnsureManifestationRenderers();
            controller.Initialize(definition);
            return instance;
        }

        private static GameObject CreatePrimitiveFallback(GhostDefinition definition, Vector3 position)
        {
            // Named so a screenshot of it explains itself. The capsule is a development
            // fallback and must never be mistaken for the ghost.
            var root = new GameObject($"DEV_PLACEHOLDER_Ghost_{definition?.DisplayName ?? "Entity"}");
            root.transform.position = position;
            root.tag = "Ghost";

            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.35f;
            agent.baseOffset = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            var eye = new GameObject("EyePoint");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var silhouette = BuildSilhouette(root.transform);
            var renderers = new[] { silhouette.GetComponent<Renderer>() };

            root.AddComponent<GhostPerception>();
            var controller = root.AddComponent<GhostController>();
            controller.SetManifestationRenderers(renderers);

            var perception = root.GetComponent<GhostPerception>();
            perception.SetEyePoint(eye.transform);

            if (definition != null)
                controller.Initialize(definition);

            return root;
        }

        private static GameObject BuildSilhouette(Transform parent)
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = "Silhouette";
            mesh.transform.SetParent(parent, false);
            mesh.transform.localScale = new Vector3(0.72f, 1.15f, 0.72f);
            mesh.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            var collider = mesh.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            var renderer = mesh.GetComponent<Renderer>();
            if (renderer != null)
            {
                // The authored MAT_GhostDissolve if it is there, and the lit shader if the
                // dissolve shader was stripped. Standard used to be the last resort, which
                // under URP made the ghost a magenta capsule rather than an absent one.
                var mat = RuntimeMaterialFactory.GetGhostDissolve();
                if (mat == null)
                {
                    var shader = CiycShaders.Find(CiycShaders.GhostDissolve)
                                 ?? CiycShaders.FindLit();
                    if (shader != null)
                    {
                        mat = new Material(shader);
                        mat.color = new Color(NeonGreen.r, NeonGreen.g, NeonGreen.b, 0.85f);
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", NeonGreen * 2.5f);
                    }
                }

                renderer.sharedMaterial = mat;
                renderer.enabled = false;
            }

            return mesh;
        }

    }
}
