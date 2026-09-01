using System;
using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;

namespace CatchIfYouCan.Player
{
    public class PlayerInventory : MonoBehaviour
    {
        public const int SlotCount = 3;

        [SerializeField] private Transform handAnchor;
        [SerializeField] private Transform dropOrigin;

        private readonly EquipmentBase[] _slots = new EquipmentBase[SlotCount];
        private int _selectedIndex;

        public int SelectedIndex => _selectedIndex;
        public event Action<int, EquipmentBase> OnSlotChanged;

        public EquipmentBase GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount)
                return null;
            return _slots[index];
        }

        public IEquipment GetSelectedEquipment() => GetSlot(_selectedIndex);

        public EquipmentBase GetSelectedItem() => GetSlot(_selectedIndex);

        public bool SelectSlot(int index)
        {
            if (index < 0 || index >= SlotCount)
                return false;

            _selectedIndex = index;
            EquipSelected();
            GameEvents.EquipmentChanged();
            OnSlotChanged?.Invoke(_selectedIndex, _slots[_selectedIndex]);
            return true;
        }

        public bool TryAddItem(EquipmentBase item) => AddItem(item);

        public bool AddItem(EquipmentBase item)
        {
            if (item == null)
                return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null)
                    continue;

                _slots[i] = item;
                OnSlotChanged?.Invoke(i, item);

                if (_selectedIndex == i)
                    EquipSelected();
                else
                    item.Unequip();

                GameEvents.EquipmentChanged();
                return true;
            }

            return false;
        }

        public bool SwapSlots(int from, int to)
        {
            if (from < 0 || from >= SlotCount || to < 0 || to >= SlotCount || from == to)
                return false;

            EquipmentBase temp = _slots[from];
            _slots[from] = _slots[to];
            _slots[to] = temp;

            OnSlotChanged?.Invoke(from, _slots[from]);
            OnSlotChanged?.Invoke(to, _slots[to]);
            EquipSelected();
            GameEvents.EquipmentChanged();
            return true;
        }

        public bool DropSelected() => DropFromSlot(_selectedIndex);

        public bool DropFromSlot(int index)
        {
            EquipmentBase item = GetSlot(index);
            if (item == null)
                return false;

            item.Unequip();
            item.Drop(GetDropPosition(), Quaternion.LookRotation(GetDropDirection()));
            _slots[index] = null;

            OnSlotChanged?.Invoke(index, null);
            if (index == _selectedIndex)
                EquipSelected();

            GameEvents.EquipmentChanged();
            return true;
        }

        public bool RemoveItemFromSlot(int index, bool dropWorldItem)
        {
            if (GetSlot(index) == null)
                return false;

            if (dropWorldItem)
                return DropFromSlot(index);

            _slots[index].Unequip();
            _slots[index] = null;
            OnSlotChanged?.Invoke(index, null);

            if (index == _selectedIndex)
                EquipSelected();

            GameEvents.EquipmentChanged();
            return true;
        }

        public void SetHandAnchor(Transform anchor)
        {
            handAnchor = anchor;
            EquipSelected();
        }

        private void EquipSelected()
        {
            Transform anchor = ResolveHandAnchor();

            for (int i = 0; i < SlotCount; i++)
            {
                EquipmentBase item = _slots[i];
                if (item == null)
                    continue;

                if (i == _selectedIndex)
                    item.Equip(anchor);
                else
                    item.Unequip();
            }
        }

        private Transform ResolveHandAnchor()
        {
            if (handAnchor != null)
                return handAnchor;

            var manager = EquipmentManager.Instance;
            if (manager != null && manager.HandAnchor != null)
                return manager.HandAnchor;

            return transform;
        }

        private Vector3 GetDropPosition()
        {
            return dropOrigin != null ? dropOrigin.position : transform.position + transform.forward * 0.75f;
        }

        private Vector3 GetDropDirection()
        {
            return dropOrigin != null ? dropOrigin.forward : transform.forward;
        }
    }
}
