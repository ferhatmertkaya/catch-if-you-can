using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Builds throwaway runtime stand-ins for equipment definitions that have no authored
    /// prefab yet, so a definition is never left with a null <c>Prefab</c>.
    ///
    /// <para>
    /// Nothing calls this at the moment: the loadout is data, and the only equipment the player
    /// actually carries is built by <see cref="Player.PlayerFactory"/>. It is kept as the seam
    /// that real authored equipment prefabs will replace, and because leaving its
    /// unknown-ID fallback in place would have been worse than deleting it - see
    /// <see cref="BuildDevPlaceholder"/>.
    /// </para>
    /// </summary>
    public static class EquipmentRuntimeFactory
    {
        private static readonly System.Collections.Generic.Dictionary<string, GameObject> PrefabCache =
            new System.Collections.Generic.Dictionary<string, GameObject>();

        /// <summary>
        /// The ids this factory can actually build. Declared rather than inferred, because a
        /// switch statement cannot be asked what it handles - and "which items have a runtime
        /// path" is exactly the question four items were silently answering no to.
        ///
        /// <para>
        /// <c>Scripts/check_equipment_catalog.sh</c> compares this set against the switch's own
        /// case labels, so the two cannot drift apart without CI saying so.
        /// </para>
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> RuntimeIds =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                EquipmentIds.Flashlight,
                EquipmentIds.EmfDetector,
                EquipmentIds.UvLight,
                EquipmentIds.Thermometer,
                EquipmentIds.EvpRecorder,
                EquipmentIds.PhotoCamera,
                EquipmentIds.Salt,
            };

        /// <summary>
        /// Whether this id can become a real object rather than a DEV_PLACEHOLDER. Asked by
        /// <see cref="EquipmentCatalogValidator"/>, which is the only reason an item without one
        /// is now visible before someone picks it up and finds a grey box.
        /// </summary>
        public static bool HasRuntimePath(string equipmentId) =>
            !string.IsNullOrEmpty(equipmentId) && RuntimeIds.Contains(equipmentId);

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
                EquipmentIds.Flashlight => BuildFlashlight(definition),
                EquipmentIds.EmfDetector => BuildPrimitiveEquipment<EMFDetector>(definition, new Vector3(0.15f, 0.08f, 0.25f)),
                EquipmentIds.UvLight => BuildUvLight(definition),
                EquipmentIds.Thermometer => BuildPrimitiveEquipment<ThermometerEquipment>(definition, new Vector3(0.05f, 0.15f, 0.05f)),
                EquipmentIds.EvpRecorder => BuildPrimitiveEquipment<EVPRecorder>(definition, new Vector3(0.12f, 0.08f, 0.18f)),
                EquipmentIds.PhotoCamera => BuildPrimitiveEquipment<PhotoCameraEquipment>(definition, new Vector3(0.12f, 0.1f, 0.18f)),
                EquipmentIds.Salt => BuildPrimitiveEquipment<SaltEquipment>(definition, new Vector3(0.2f, 0.15f, 0.2f)),
                _ => BuildDevPlaceholder(definition)
            };

            prefab.name = $"Runtime_{definition.Id}";
            PrefabCache[definition.Id] = prefab;
            definition.Prefab = prefab;
        }

        /// <summary>
        /// The torch. There is exactly one flashlight implementation now, and it builds its own
        /// body, lens and beam, so this hands it an empty object and gets out of the way rather
        /// than wrapping a primitive cube around a second, parallel torch.
        /// </summary>
        private static GameObject BuildFlashlight(EquipmentDefinition definition)
        {
            var root = new GameObject(definition.DisplayName);
            root.tag = "Equipment";

            var equipment = root.AddComponent<HeldFlashlight>();
            equipment.BindDefinition(definition);
            return root;
        }

        /// <summary>
        /// What an unrecognised equipment ID gets: a labelled, inert box and a loud complaint.
        ///
        /// <para>
        /// This branch used to build a flashlight. Every unimplemented item in the catalogue -
        /// the thermometer, the EVP recorder, the spirit box, the crucifix - therefore came out
        /// of the factory as a working torch, which reads as "implemented" to anyone testing it
        /// and hides the fact that the item does not exist.
        /// </para>
        /// </summary>
        private static GameObject BuildDevPlaceholder(EquipmentDefinition definition)
        {
            Debug.LogError(
                $"[Equipment] No runtime implementation for equipment id '{definition.Id}'. " +
                "Building a DEV_PLACEHOLDER that does nothing. Add a case to " +
                "EquipmentRuntimeFactory, or author a prefab on the definition.");

            var root = BuildBody(definition, new Vector3(0.12f, 0.12f, 0.12f));
            root.name = $"DEV_PLACEHOLDER_{definition.Id}";
            root.AddComponent<DevPlaceholderEquipment>().BindDefinition(definition);
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
