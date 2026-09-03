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
        [Tooltip("DEPRECATED. Superseded by EquipmentGripProfile, which describes how an item " +
                 "sits in a hand in the hand's own measured axes rather than as a local " +
                 "transform on an anchor. Kept only so existing authored values can be " +
                 "migrated; nothing reads them once a grip profile is assigned.")]
        public Vector3 HandLocalPosition;

        [Tooltip("DEPRECATED. See HandLocalPosition.")]
        public Vector3 HandLocalRotation;

        [Header("Grip")]
        [Tooltip("How this item sits in a hand. Null falls back to the shared default, which " +
                 "is the flashlight's measured grip - the one grip in this project that has " +
                 "ever been tuned against a real character.")]
        public EquipmentGripProfile GripProfile;

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
