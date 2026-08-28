using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public static class EquipmentDefinitionFactory
    {
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

            foreach (var def in CreateAllDefaultDefinitions())
            {
                if (def != null && def.Id == id)
                    return def;
            }

            return null;
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
            return def;
        }
    }
}
