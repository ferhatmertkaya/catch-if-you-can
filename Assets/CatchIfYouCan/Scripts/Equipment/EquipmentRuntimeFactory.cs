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
                EquipmentIds.ParabolicMicrophone,
                EquipmentIds.SpectralGrid,
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
                EquipmentIds.Flashlight => BuildSelfPresenting<HeldFlashlight>(definition),
                EquipmentIds.EmfDetector => BuildSelfPresenting<EMFDetector>(definition),
                EquipmentIds.UvLight => BuildSelfPresenting<UVLight>(definition),
                EquipmentIds.Thermometer => BuildSelfPresenting<ThermometerEquipment>(definition),
                EquipmentIds.EvpRecorder => BuildSelfPresenting<EVPRecorder>(definition),
                EquipmentIds.ParabolicMicrophone => BuildSelfPresenting<ParabolicMicrophone>(definition),
                EquipmentIds.SpectralGrid => BuildSelfPresenting<SpectralGridProjector>(definition),
                EquipmentIds.PhotoCamera => BuildSelfPresenting<PhotoCameraEquipment>(definition),
                EquipmentIds.Salt => BuildPrimitiveEquipment<SaltEquipment>(definition, new Vector3(0.2f, 0.15f, 0.2f)),
                _ => BuildDevPlaceholder(definition)
            };

            prefab.name = $"Runtime_{definition.Id}";
            PrefabCache[definition.Id] = prefab;
            definition.Prefab = prefab;
        }

        /// <summary>
        /// An item that builds its own appearance.
        ///
        /// <para>
        /// Anything on <see cref="HeldEquipmentBase"/> gets its visual from its definition's
        /// visual profile and adds whatever a mesh cannot be - the torch's beam, the UV lamp's
        /// cone - itself. So this hands it an empty object and gets out of the way. It used to
        /// wrap a primitive cube around each of them and then reflect a light into a private
        /// field, which for the UV lamp meant writing a field that no longer exists.
        /// </para>
        /// </summary>
        private static GameObject BuildSelfPresenting<T>(EquipmentDefinition definition)
            where T : HeldEquipmentBase
        {
            var root = new GameObject(definition.DisplayName);
            root.tag = "Equipment";

            var equipment = root.AddComponent<T>();
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

    }
}
