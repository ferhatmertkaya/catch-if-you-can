using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public interface IEquipment
    {
        EquipmentDefinition Definition { get; }
        bool IsEquipped { get; }
        bool IsPlaced { get; }
        float BatteryLevel { get; }
        float BatteryPercent { get; }
        float Durability { get; }
        float MaxDurability { get; }

        void Equip(Transform handAnchor);
        void Unequip();
        void Use();
        bool TryPlace(Vector3 position, Quaternion rotation);
        void Drop(Vector3 position, Quaternion rotation);
    }
}
