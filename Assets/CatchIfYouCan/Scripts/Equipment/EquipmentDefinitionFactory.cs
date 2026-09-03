using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Where the rest of the game asks for an equipment definition.
    ///
    /// <para>
    /// It prefers the authored <see cref="EquipmentCatalog"/> reached through the content
    /// registry, and falls back to building the same eleven definitions in code when the
    /// catalog has not been authored or imported yet. The fallback is a fallback: two callers
    /// asking for "flashlight" used to get two different ScriptableObjects, so nothing could
    /// compare definitions by reference and a battery charge written onto one was invisible to
    /// the next caller. The catalog hands back the same asset every time, and the fallback is
    /// now built once and cached so that it does too.
    /// </para>
    /// </summary>
    public static class EquipmentDefinitionFactory
    {
        private static EquipmentDefinition[] _fallback;

        /// <summary>
        /// Every definition, authored if there is a catalog and code-built if there is not.
        /// This is what a shop or a content hash should read.
        /// </summary>
        public static EquipmentDefinition[] All()
        {
            var catalog = Catalog();
            if (catalog != null && catalog.Count > 0)
                return catalog.Equipment;

            return CachedFallback();
        }

        /// <summary>The authored catalog, or null while the registry has not been created.</summary>
        public static EquipmentCatalog Catalog()
        {
            return Content.CiycContentRegistry.Load()?.EquipmentCatalog;
        }

        /// <summary>
        /// The code-built definitions, made once. Rebuilt after a domain reload, because the
        /// instances behind them do not survive one.
        /// </summary>
        private static EquipmentDefinition[] CachedFallback()
        {
            if (_fallback == null || _fallback.Length == 0 || _fallback[0] == null)
                _fallback = CreateAllDefaultDefinitions();

            return _fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _fallback = null;
        }

        /// <summary>
        /// The definitions as code, one instance per call. This is the authoring source the
        /// editor tools write the assets from, and the runtime fallback when they have not
        /// been written yet - reach it through <see cref="All"/> rather than directly.
        /// </summary>
        public static EquipmentDefinition[] CreateAllDefaultDefinitions()
        {
            return new[]
            {
                Create("flashlight", "Flashlight", EquipmentCategory.Visual, 0, 1,
                    batteryUsage: 0.35f,
                    maxBattery: 100f,
                    canPlace: false,
                    description: "Essential light source. Reveals UV traces when upgraded."),
                Create("emf_detector", "EMF Detector", EquipmentCategory.Detection, 150, 1,
                    batteryUsage: 0.6f,
                    maxBattery: 100f,
                    canPlace: false,
                    description: "Detects electromagnetic surges linked to entity activity."),
                Create("uv_light", "UV Light", EquipmentCategory.Visual, 175, 1,
                    batteryUsage: 0.55f,
                    maxBattery: 90f,
                    canPlace: false,
                    description: "Reveals hidden fingerprints, salt trails, and UV traces."),
                Create("thermometer", "Thermometer", EquipmentCategory.Detection, 125, 1,
                    batteryUsage: 0.25f,
                    maxBattery: 120f,
                    canPlace: false,
                    description: "Measures ambient temperature drops near the entity."),
                Create("evp_recorder", "EVP Recorder", EquipmentCategory.Audio, 200, 1,
                    batteryUsage: 0.45f,
                    maxBattery: 100f,
                    canPlace: true,
                    description: "Ask questions and capture spirit voice responses."),
                Create("parabolic_microphone", "Parabolic Microphone", EquipmentCategory.Audio, 275, 2,
                    batteryUsage: 0.7f,
                    maxBattery: 80f,
                    canPlace: false,
                    description: "Directional audio probe for distant anomalies."),
                Create("photo_camera", "Photo Camera", EquipmentCategory.Visual, 225, 1,
                    batteryUsage: 0.5f,
                    maxBattery: 60f,
                    canPlace: false,
                    description: "Capture photographic evidence and ghost manifestations."),
                Create("spectral_grid", "Spectral Grid Projector", EquipmentCategory.Detection, 300, 2,
                    batteryUsage: 0.85f,
                    maxBattery: 75f,
                    canPlace: true,
                    description: "Projects a grid that reveals spectral silhouettes."),
                Create("video_camera", "Video Camera", EquipmentCategory.Visual, 350, 2,
                    batteryUsage: 0.9f,
                    maxBattery: 90f,
                    canPlace: true,
                    description: "Remote monitoring camera for fixed surveillance points."),
                Create("warding_relic", "Warding Relic", EquipmentCategory.Protection, 350, 1,
                    batteryUsage: 0f,
                    maxBattery: 0f,
                    canPlace: true,
                    description: "Crystal ward that can interrupt an active hunt nearby."),
                Create("salt", "Salt", EquipmentCategory.Utility, 50, 1,
                    batteryUsage: 0f,
                    maxBattery: 0f,
                    canPlace: true,
                    canDrop: true,
                    description: "Place salt piles to reveal footprints under UV light.")
            };
        }

        public static EquipmentDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var catalog = Catalog();
            if (catalog != null)
            {
                var authored = catalog.Resolve(id);
                if (authored != null)
                    return authored;
            }

            foreach (var def in CachedFallback())
            {
                if (def != null && def.Id == id)
                    return def;
            }

            return null;
        }

        /// <summary>
        /// The visual for an item built in code.
        ///
        /// <para>
        /// <b>Without this every item in the game is a placeholder capsule.</b> The authored
        /// <c>VisualProfile_*</c> assets are not under Resources, so nothing at runtime can
        /// load them, and this factory - which IS the live source of definitions - left
        /// <c>VisualProfile</c> null. <c>EquipmentVisualFactory</c> then substituted its
        /// honest-placeholder capsule, which is why the finished flashlight model never
        /// appeared in anybody's hand.
        /// </para>
        ///
        /// <para>
        /// Only the torch has finished art in the project today. The rest get a placeholder
        /// that <b>says</b> it is one, which is the honest state and not something to paper
        /// over: an item that looks finished and is not is worse than an item that looks
        /// unfinished.
        /// </para>
        /// </summary>
        private static EquipmentVisualProfile BuildVisualProfile(string id, string displayName)
        {
            var profile = ScriptableObject.CreateInstance<EquipmentVisualProfile>();
            profile.name = "VisualProfile_Runtime_" + id;
            profile.hideFlags = HideFlags.HideAndDontSave;

            if (string.Equals(id, EquipmentIds.Flashlight, System.StringComparison.Ordinal))
            {
                // Matches the authored VisualProfile_Flashlight asset exactly, including the
                // model's own forward axis - the mesh runs along -X, which is why the torch
                // would otherwise be laid in the hand sideways.
                profile.ApplyModel("Props/CIYC_Flashlight", "Props/MAT_Flashlight",
                                   0.24f, new Vector3(-1f, 0f, 0f));
                return profile;
            }

            profile.ApplyDevPlaceholder(new Vector3(0.06f, 0.18f, 0.06f),
                                        new Color(0.55f, 0.2f, 0.5f));
            return profile;
        }

        private static EquipmentDefinition Create(
            string id,
            string displayName,
            EquipmentCategory category,
            int price,
            int tier,
            float batteryUsage,
            float maxBattery,
            bool canPlace,
            string description,
            bool canDrop = true)
        {
            var def = ScriptableObject.CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.DisplayName = displayName;
            def.Category = category;
            def.Price = price;
            def.Tier = tier;
            def.BatteryUsagePerSecond = batteryUsage;
            def.MaxBattery = maxBattery;
            def.MaxDurability = 100f;
            def.CanPlace = canPlace;
            def.CanDrop = canDrop;
            def.CanUse = true;
            def.InteractionRange = 2.5f;
            def.HandLocalPosition = new Vector3(0.08f, -0.05f, 0.22f);
            def.HandLocalRotation = new Vector3(0f, -90f, 0f);
            def.Description = description;
            def.VisualProfile = BuildVisualProfile(id, displayName);
            return def;
        }
    }
}
