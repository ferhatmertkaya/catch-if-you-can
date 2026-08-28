using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public enum EquipmentCategory
    {
        Detection,
        Visual,
        Audio,
        Protection,
        Utility
    }

    [CreateAssetMenu(fileName = "EquipmentDefinition", menuName = "Catch If You Can/Equipment Definition")]
    public class EquipmentDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public Sprite Icon;
        public GameObject Prefab;

        [Header("Hand Pose")]
        public Vector3 HandLocalPosition;
        public Vector3 HandLocalRotation;

        [Header("Power")]
        public float BatteryUsagePerSecond = 0.5f;
        public float MaxBattery = 100f;
        public float MaxDurability = 100f;

        [Header("Capabilities")]
        public bool CanPlace;
        public bool CanDrop = true;
        public bool CanUse = true;
        public float InteractionRange = 2.5f;

        [Header("Audio")]
        public AudioClip UseAudio;
        public AudioClip PlaceAudio;

        [Header("Shop")]
        public int Tier = 1;
        public int Price = 100;
        public EquipmentCategory Category = EquipmentCategory.Detection;

        [TextArea(2, 6)]
        public string Description;
    }
}
