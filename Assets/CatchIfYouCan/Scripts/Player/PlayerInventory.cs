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
        private PlayerPresence _presence;

        public int SelectedIndex => _selectedIndex;

        /// <summary>
        /// Whose bag this is.
        ///
        /// <para>
        /// Asked of the presence on this player rather than of
        /// <see cref="Core.LocalPlayerService"/>, which holds exactly one player: the one on
        /// this machine. Correct for the local inventory and silently wrong for every other
        /// one - every remote player's pickups would be recorded as the local player's.
        /// </para>
        /// </summary>
        public int OwnerClientId
        {
            get
            {
                if (_presence == null)
                    _presence = GetComponent<PlayerPresence>();

                return _presence != null
                    ? _presence.ClientId
                    : Procedural.Deterministic.MultiplayerProtocol.LocalOnlyClientId;
            }
        }
        public event Action<int, EquipmentBase> OnSlotChanged;

        /// <summary>True when at least one slot is empty, so a pickup would actually land.</summary>
        public bool HasFreeSlot
        {
            get
            {
                for (int i = 0; i < SlotCount; i++)
                    if (_slots[i] == null)
                        return true;
                return false;
            }
        }

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

                // Claimed before the slot is filled, and only once a slot is known to be free.
                // Claiming an item this bag has no room for would take it off whoever else
                // could have had it and then leave it on the floor belonging to nobody who is
                // holding it. Offline every claim is granted, so nothing changes.
                var claim = item.TryClaim(OwnerClientId);
                if (!Procedural.Deterministic.EquipmentOwnership.Holds(claim))
                {
                    CIYCLog.Info("Pickup refused: " +
                                 Procedural.Deterministic.EquipmentOwnership.Describe(claim) + ".");
                    return false;
                }

                _slots[i] = item;
                OnSlotChanged?.Invoke(i, item);

                // Straight into a slot the player is not holding: stow it, do not unequip it.
                // Unequip unparents to world space, which for an item entering a bag means
                // leaving it behind in the room the moment it is picked up.
                if (_selectedIndex == i)
                    EquipSelected();
                else
                    Holster(item);

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

        public bool DropFromSlot(int index) => TryDropFromSlot(index).Ok;

        /// <summary>
        /// Throws an item out of a slot, and says why not when it will not go. The bool
        /// overloads above are kept because the HUD and the labs call them, but everything new
        /// should ask for the reason: "cannot be dropped", "the slot is empty" and "the item
        /// refused" were previously the same false.
        /// </summary>
        public EquipmentActionResult TryDropFromSlot(int index)
        {
            EquipmentBase item = GetSlot(index);
            if (item == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "slot " + index + " is empty");

            if (item is IHeldEquipment held)
            {
                var allowed = held.TryDrop();
                if (!allowed.Ok)
                    return allowed;
            }
            else if (item.Definition != null && !item.Definition.CanDrop)
            {
                return EquipmentActionResult.Fail(EquipmentActionStatus.NotAllowedByDefinition);
            }

            item.Unequip();
            item.Drop(GetDropPosition(), Quaternion.LookRotation(GetDropDirection()));

            // Released here rather than in Unequip, because unequipping is also how an item is
            // stowed and a stowed item is still its owner's. Clearing ownership there would
            // hand everybody's spare equipment to the first person who walked past.
            item.ReleaseOwnership();
            _slots[index] = null;

            OnSlotChanged?.Invoke(index, null);
            if (index == _selectedIndex)
                EquipSelected();

            GameEvents.EquipmentChanged();
            return EquipmentActionResult.Success;
        }

        /// <summary>
        /// The Use button, applied to whatever is in the selected slot. One entry point, so the
        /// HUD does not have to know what kind of item it is pressing.
        /// </summary>
        public EquipmentActionResult TryUseSelected()
        {
            EquipmentBase item = GetSelectedItem();
            if (item == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "nothing in the selected slot");

            if (item is IHeldEquipment held)
                return held.TryUse();

            item.Use();
            return EquipmentActionResult.Success;
        }

        public bool RemoveItemFromSlot(int index, bool dropWorldItem)
        {
            if (GetSlot(index) == null)
                return false;

            if (dropWorldItem)
                return DropFromSlot(index);

            _slots[index].Unequip();
            _slots[index].ReleaseOwnership();
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

        /// <summary>
        /// Brings the selected slot into the hand and stows the rest.
        ///
        /// <para>
        /// The unselected items used to be sent through <c>Unequip</c>, which unparents to
        /// world space - so putting something in slot 2 left it hovering wherever the player
        /// happened to be standing. With one item in the inventory that never showed. With
        /// three it is every item but the one being held. Items on the held-equipment contract
        /// are stowed instead: still owned, still travelling with the player, not rendered and
        /// not pickable.
        /// </para>
        /// </summary>
        private void EquipSelected()
        {
            Transform anchor = ResolveHandAnchor();

            for (int i = 0; i < SlotCount; i++)
            {
                EquipmentBase item = _slots[i];
                if (item == null)
                    continue;

                if (i == _selectedIndex)
                {
                    if (item is IHeldEquipment held)
                        held.TryEquip(anchor);
                    else
                        item.Equip(anchor);
                }
                else
                {
                    Holster(item);
                }
            }
        }

        /// <summary>
        /// Stows one item. Items that have been converted to the held-equipment contract know
        /// how; the rest still fall back to Unequip until their phase converts them, which is
        /// the pre-existing behaviour and no worse than it was.
        /// </summary>
        private void Holster(EquipmentBase item)
        {
            if (item is HeldEquipmentBase held)
            {
                held.TryHolster(ResolveHandAnchor());
                return;
            }

            item.Unequip();
        }

        /// <summary>
        /// Where held items are parented. The inventory owns this outright now: the loadout
        /// service used to keep a second hand anchor of its own, and whichever one a caller
        /// happened to ask decided where the item ended up.
        /// </summary>
        private Transform ResolveHandAnchor()
        {
            return handAnchor != null ? handAnchor : transform;
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
