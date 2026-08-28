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

        private static GameObject LoadBundledPrefab(GhostDefinition definition)
        {
            if (definition.Prefab != null)
                return definition.Prefab;

            var byId = Resources.Load<GameObject>(GhostVisualCatalog.GetPrefabResourcePath(definition.Id));
            if (byId != null)
                return byId;

            return Resources.Load<GameObject>(GhostVisualCatalog.GetPrefabResourcePath(definition.VisualProfile));
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
            var root = new GameObject($"Ghost_{definition?.DisplayName ?? "Entity"}");
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
            SetPrivateField(perception, "eyePoint", eye.transform);

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
                var shader = Shader.Find("CatchIfYouCan/GhostDissolve")
                             ?? Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                var mat = RuntimeMaterialFactory.GetGhostDissolve(shader);
                if (mat == null)
                {
                    mat = new Material(shader);
                    mat.color = new Color(NeonGreen.r, NeonGreen.g, NeonGreen.b, 0.85f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", NeonGreen * 2.5f);
                }

                renderer.sharedMaterial = mat;
                renderer.enabled = false;
            }

            return mesh;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
