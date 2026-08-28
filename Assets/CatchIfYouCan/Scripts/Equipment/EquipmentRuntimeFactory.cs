using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public static class EquipmentRuntimeFactory
    {
        private static readonly System.Collections.Generic.Dictionary<string, GameObject> PrefabCache =
            new System.Collections.Generic.Dictionary<string, GameObject>();

        public static void EnsureRuntimePrefab(EquipmentDefinition definition)
        {
            if (definition == null || definition.Prefab != null)
                return;

            if (PrefabCache.TryGetValue(definition.Id, out var cached) && cached != null)
            {
                definition.Prefab = cached;
                return;
            }

            GameObject prefab = definition.Id switch
            {
                "flashlight" => BuildFlashlight(definition),
                "emf_detector" => BuildPrimitiveEquipment<EMFDetector>(definition, new Vector3(0.15f, 0.08f, 0.25f)),
                "uv_light" => BuildUvLight(definition),
                "thermometer" => BuildPrimitiveEquipment<ThermometerEquipment>(definition, new Vector3(0.05f, 0.15f, 0.05f)),
                "evp_recorder" => BuildPrimitiveEquipment<EVPRecorder>(definition, new Vector3(0.12f, 0.08f, 0.18f)),
                "photo_camera" => BuildPrimitiveEquipment<PhotoCameraEquipment>(definition, new Vector3(0.12f, 0.1f, 0.18f)),
                "salt" => BuildPrimitiveEquipment<SaltEquipment>(definition, new Vector3(0.2f, 0.15f, 0.2f)),
                _ => BuildPrimitiveEquipment<FlashlightEquipment>(definition, new Vector3(0.12f, 0.08f, 0.2f))
            };

            prefab.name = $"Runtime_{definition.Id}";
            PrefabCache[definition.Id] = prefab;
            definition.Prefab = prefab;
        }

        private static GameObject BuildFlashlight(EquipmentDefinition definition)
        {
            var root = BuildBody(definition, new Vector3(0.08f, 0.08f, 0.22f));
            var equipment = root.GetComponent<FlashlightEquipment>() ?? root.AddComponent<FlashlightEquipment>();
            equipment.BindDefinition(definition);

            var lightGo = new GameObject("Spotlight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            lightGo.transform.localRotation = Quaternion.identity;

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 14f;
            light.spotAngle = 55f;
            light.intensity = 2f;
            light.enabled = false;

            SetPrivateField(equipment, "spotlight", light);
            return root;
        }

        private static GameObject BuildUvLight(EquipmentDefinition definition)
        {
            var root = BuildBody(definition, new Vector3(0.1f, 0.08f, 0.2f));
            var equipment = root.GetComponent<UVLight>() ?? root.AddComponent<UVLight>();
            equipment.BindDefinition(definition);

            var lightGo = new GameObject("UVLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.12f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.45f, 0.2f, 1f);
            light.range = 8f;
            light.spotAngle = 40f;
            light.intensity = 1.5f;
            light.enabled = false;

            SetPrivateField(equipment, "uvLight", light);
            return root;
        }

        private static GameObject BuildPrimitiveEquipment<T>(EquipmentDefinition definition, Vector3 scale)
            where T : EquipmentBase
        {
            var root = BuildBody(definition, scale);
            var equipment = root.GetComponent<T>() ?? root.AddComponent<T>();
            equipment.BindDefinition(definition);
            return root;
        }

        private static GameObject BuildBody(EquipmentDefinition definition, Vector3 scale)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = definition.DisplayName;
            root.transform.localScale = scale;
            root.tag = "Equipment";

            var collider = root.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            return root;
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
