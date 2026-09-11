using System;
using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;

namespace CatchIfYouCan.Player
{
    public class PlayerInventory : MonoBehaviour
    {
        /// <summary>
        /// Investigation equipment slots. Three, and it stays three: the HUD selector, the
        /// pickup rules and the replication contract all count on it.
        /// </summary>
        public const int SlotCount = 3;

        /// <summary>
        /// The torch's own place, addressable for selection but not one of the three.
        ///
        /// <para>
        /// <b>Why the torch is not a normal slot.</b> The vertical slice needs four tools -
        /// torch, EMF, UV, thermometer - and the torch used to take slot 0, so the third
        /// investigation device had nowhere to go and was dropped on the floor of a log line.
        /// The torch is not really one of the three anyway: the player never chooses to bring
        /// it, never trades it for something better, and it has its own HUD control rather
        /// than a slot in the selector.
        /// </para>
        ///
        /// <para>
        /// <b>It is a slot for selection and nothing more.</b> Exactly one item is in the hand
        /// at a time, here as before - <see cref="EquipSelected"/> holds the selected one and
        /// stows the rest, the hand anchor is unchanged, and no grip or presentation maths is
        /// involved. A torch stowed while the EMF is out still goes dark, which is the
        /// behaviour <c>HeldFlashlight</c> documents and intends.
        /// </para>
        /// </summary>
        public const int TorchSlotIndex = SlotCount;

        /// <summary>Selectable positions: the three, plus the torch.</summary>
        public const int SelectableSlotCount = SlotCount + 1;

        [SerializeField] private Transform handAnchor;
        [SerializeField] private Transform dropOrigin;

        private readonly EquipmentBase[] _slots = new EquipmentBase[SlotCount];
        private EquipmentBase _torch;
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
                    if (IsSlotAvailable(i))
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Whether a pickup could land in this slot.
        ///
        /// <para>
        /// Empty is the ordinary case. The other one is a slot whose occupant is INSTALLED in
        /// the room: that item is world state, anybody in reach may take it off the wall, and it
        /// is not in this bag however much the array still points at it. Counting it as occupied
        /// would cost the player a slot per device they set down, and treating it as bag content
        /// is the resurrection bug itself - see <see cref="Equipment.EquipmentBase.Carrier"/>.
        /// </para>
        /// </summary>
        private bool IsSlotAvailable(int index)
        {
            if (index < 0 || index >= SlotCount)
                return false;

            EquipmentBase occupant = _slots[index];
            return occupant == null || occupant.IsPlaced;
        }

        /// <summary>
        /// Which slot holds this exact item, or -1. <see cref="TorchSlotIndex"/> when it is the
        /// torch.
        ///
        /// <para>
        /// Compared by reference rather than with <c>==</c>: Unity's equality operator treats a
        /// destroyed object as equal to null, and "which slot is this destroyed thing in" is a
        /// question that still has to be answerable while the slot is being cleaned up.
        /// </para>
        /// </summary>
        public int IndexOf(EquipmentBase item)
        {
            if (item == null)
                return -1;

            for (int i = 0; i < SlotCount; i++)
                if (ReferenceEquals(_slots[i], item))
                    return i;

            return ReferenceEquals(_torch, item) ? TorchSlotIndex : -1;
        }

        /// <summary>
        /// Lets go of an item that has just been installed in the room.
        ///
        /// <para>
        /// <b>This is the other half of a placement.</b> Committing one moves the object out of
        /// the hand and into the room; without this the slot went on pointing at it, so the same
        /// device was both a thing standing on a wall and the occupant of slot 1. Selecting
        /// another slot then swept that occupant through <see cref="Holster"/>, which un-placed
        /// it, reparented it to the hand anchor and hid it - and selecting the first slot again
        /// handed it straight back. The player saw the projector they had just installed
        /// reappear in their hand, with nothing left where they put it.
        /// </para>
        ///
        /// <para>
        /// Ownership is deliberately NOT released. <c>EquipmentHold.Placed</c> says an installed
        /// item remembers who placed it while staying takeable by anybody in reach, and that is
        /// the state this leaves it in: out of the bag, still attributable.
        /// </para>
        ///
        /// <para>
        /// Answers false when this bag does not hold the item, which is the honest answer for a
        /// device placed by somebody else or already released.
        /// </para>
        /// </summary>
        public bool ReleaseToWorld(EquipmentBase item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;

            ClearSlot(index);
            OnSlotChanged?.Invoke(index, null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CIYCLog.Info("[CIYC][Inventory] slot " + index + " released to the world: '" +
                         (item.Definition != null ? item.Definition.Id : item.name) +
                         "' is installed in the room and is no longer carried.");
#endif

            // The hand is empty now if that was the selected slot, so whatever else is carried
            // gets its turn. Runs AFTER the slot is cleared, so the sweep cannot find the item
            // it has just let go of.
            if (index == _selectedIndex)
                EquipSelected();

            GameEvents.EquipmentChanged();
            return true;
        }

        /// <summary>
        /// Lets go of a slot whose occupant turned out to be installed in the room. Defence in
        /// depth behind <see cref="ReleaseToWorld"/>: any path that ever leaves a placed item in
        /// a slot is repaired the next time the bag is read, rather than producing a device in
        /// two ownership states at once.
        /// </summary>
        private bool ForgetPlacedOccupant(int index)
        {
            EquipmentBase occupant = GetSlot(index);
            if (occupant == null || !occupant.IsPlaced)
                return false;

            ClearSlot(index);
            OnSlotChanged?.Invoke(index, null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CIYCLog.Info("[CIYC][Inventory] slot " + index + " still pointed at '" +
                         (occupant.Definition != null ? occupant.Definition.Id : occupant.name) +
                         "', which is installed in the room. Dropped the reference.");
#endif
            return true;
        }

        /// <summary>True once the player is carrying their torch.</summary>
        public bool HasTorch => _torch != null;

        /// <summary>The torch, wherever it is in the carry cycle, or null.</summary>
        public EquipmentBase Torch => _torch;

        public EquipmentBase GetSlot(int index)
        {
            if (index == TorchSlotIndex)
                return _torch;
            if (index < 0 || index >= SlotCount)
                return null;
            return _slots[index];
        }

        public IEquipment GetSelectedEquipment() => GetSlot(_selectedIndex);

        public EquipmentBase GetSelectedItem() => GetSlot(_selectedIndex);

        public bool SelectSlot(int index)
        {
            if (index < 0 || index >= SelectableSlotCount)
                return false;

            _selectedIndex = index;
            EquipSelected();
            GameEvents.EquipmentChanged();
            // GetSlot, not _slots[...]. The array holds the three INVESTIGATION slots;
            // _selectedIndex is validated against SelectableSlotCount, which is four, because
            // the torch has a place of its own at index 3. Indexing the array with it threw
            // IndexOutOfRangeException every single time the torch slot was selected - from the
            // torch HUD button, from SelectTorch, and from the automatic fall-back to the torch
            // when the bag is empty, which is exactly what the lobby does when it takes the
            // torch out of the player's hands at startup.
            //
            // It threw AFTER _selectedIndex was written and AFTER EquipSelected() ran, so the
            // damage was not "nothing happened": the equip had already happened and the
            // exception then escaped through AddItem and InteractivePickup.Interact, abandoning
            // the rest of the pickup. Every other read in this file goes through GetSlot, which
            // knows about the torch; this one line did not.
            OnSlotChanged?.Invoke(_selectedIndex, GetSlot(_selectedIndex));
            return true;
        }

        /// <summary>
        /// Packs an item WITHOUT changing what is in the hand. What the mission loadout installer
        /// wants: it adds three items in a row, and selecting each one as it arrives would leave
        /// the player holding whichever happened to be last in the list.
        /// </summary>
        public bool TryAddItem(EquipmentBase item) => AddItem(item, selectFilledSlot: false);

        /// <summary>
        /// Picks an item up, and puts it in the hand.
        ///
        /// <para>
        /// Selecting the slot is what a pickup means. Without it the item went into the first
        /// free slot while the selection stayed where it was - in the lobby that is the torch -
        /// so the projector was picked up, holstered on the same frame, and the player was left
        /// looking at an empty hand and a floor with nothing on it. An item that is carried and
        /// invisible is indistinguishable from one that was never picked up (mistakes 20, 27, 28,
        /// 42 and 47, all the same report).
        /// </para>
        /// </summary>
        public bool AddItem(EquipmentBase item) => AddItem(item, selectFilledSlot: true);

        /// <summary>
        /// The one implementation. <paramref name="selectFilledSlot"/> is the only difference
        /// between a pickup and a pack, so there is one set of rules rather than two.
        /// </summary>
        public bool AddItem(EquipmentBase item, bool selectFilledSlot)
        {
            if (item == null)
                return false;

            // Already in this bag. Not an error and not a second copy: a pickup offered twice in
            // one frame, or a placed item handed back by a path that had not let go of it yet.
            // Refusing would report NoInventorySpace, which is a lie about a bag that holds it.
            int existing = IndexOf(item);
            if (existing >= 0)
            {
                if (selectFilledSlot)
                    SelectSlot(existing);
                return true;
            }

            // The torch always goes to its own place, whether it is being handed over at spawn
            // or picked back up off the floor. Without this, a torch put down and retrieved
            // would take an investigation slot and the contradiction would come straight back.
            if (IsTorch(item) && _torch == null)
                return AdoptTorch(item);

            for (int i = 0; i < SlotCount; i++)
            {
                if (!IsSlotAvailable(i))
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

                // The slot was counted as free because its occupant is installed in the room.
                // Letting go of it here rather than overwriting the reference means that device
                // is left with no bag claiming it, which is what being on a wall means.
                ForgetPlacedOccupant(i);

                _slots[i] = item;

                // The item can now answer "whose slot am I in", which is what lets a placement
                // vacate its own slot instead of leaving the bag pointing at a wall.
                item.BindCarrier(this);

                OnSlotChanged?.Invoke(i, item);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // What actually happened, in one line, at the moment it happened. "It vanished
                // and I do not know which state owns it" is the failure this prevents.
                CIYCLog.Info("[CIYC][Pickup] '" +
                             (item.Definition != null ? item.Definition.Id : "<no definition>") +
                             "' (" + (item.Definition != null ? item.Definition.DisplayName : item.name) +
                             ") accepted into slot " + i + " of " + SlotCount +
                             "; selected slot is " + _selectedIndex +
                             (_selectedIndex == i ? " (this one - equipping)" : " (elsewhere - holstering)") +
                             "; torch slot is " + TorchSlotIndex + ".");
#endif

                // A pickup goes into the hand. Anything else is a pack, and a packed item is
                // stowed rather than unequipped: Unequip unparents to world space, which for an
                // item entering a bag means leaving it behind in the room it was picked up from.
                if (selectFilledSlot)
                    _selectedIndex = i;

                if (_selectedIndex == i)
                    EquipSelected();
                else
                    Holster(item);

                GameEvents.EquipmentChanged();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether this is the player's torch rather than an investigation device. Asked of the
        /// definition id, which is a declared constant, rather than of the component type -
        /// a mission that ever hands out a second torch-like light should not silently claim
        /// the dedicated place.
        /// </summary>
        private static bool IsTorch(EquipmentBase item) =>
            item != null && item.Definition != null &&
            string.Equals(item.Definition.Id, EquipmentIds.Flashlight, StringComparison.Ordinal);

        /// <summary>
        /// Takes the torch into its dedicated place. Ownership is claimed here through exactly
        /// the same call <see cref="AddItem"/> uses, because this file is still the only place
        /// equipment ownership is claimed or released.
        /// </summary>
        public bool AdoptTorch(EquipmentBase item)
        {
            if (item == null || _torch != null)
                return false;

            var claim = item.TryClaim(OwnerClientId);
            if (!Procedural.Deterministic.EquipmentOwnership.Holds(claim))
            {
                CIYCLog.Info("Torch refused: " +
                             Procedural.Deterministic.EquipmentOwnership.Describe(claim) + ".");
                return false;
            }

            _torch = item;
            item.BindCarrier(this);
            OnSlotChanged?.Invoke(TorchSlotIndex, item);

            // A player who has only their torch has it in their hand. This is what the lobby
            // has always looked like - the torch used to be slot 0 and slot 0 is selected by
            // default - and moving it out of the three slots must not quietly empty their hands.
            if (_selectedIndex != TorchSlotIndex && !HasAnyEquipment())
                SelectSlot(TorchSlotIndex);
            else if (_selectedIndex == TorchSlotIndex)
                EquipSelected();
            else
                Holster(item);

            GameEvents.EquipmentChanged();
            return true;
        }

        /// <summary>
        /// Brings the torch into the hand. The torch button calls this before switching on,
        /// because a stowed torch refuses to light - which is
        /// <see cref="Equipment.HeldFlashlight"/>'s intended behaviour and not something to
        /// work around.
        /// </summary>
        public bool SelectTorch() => _torch != null && SelectSlot(TorchSlotIndex);

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
            ClearSlot(index);

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

            EquipmentBase held = GetSlot(index);
            held.Unequip();
            held.ReleaseOwnership();
            ClearSlot(index);
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
        /// <summary>Whether any of the three investigation slots is occupied.</summary>
        private bool HasAnyEquipment()
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] != null)
                    return true;
            return false;
        }

        /// <summary>
        /// Empties one slot, and tells the item it is no longer in this bag.
        ///
        /// <para>
        /// The two halves belong together. A slot cleared without unbinding leaves an item whose
        /// <see cref="Equipment.EquipmentBase.Carrier"/> names a bag that does not hold it, and
        /// the next thing that asked would get the wrong answer with no way to tell.
        /// </para>
        /// </summary>
        private void ClearSlot(int index)
        {
            EquipmentBase leaving = GetSlot(index);

            if (index == TorchSlotIndex)
                _torch = null;
            else if (index >= 0 && index < SlotCount)
                _slots[index] = null;
            else
                return;

            if (leaving != null && leaving.Carrier == this)
                leaving.BindCarrier(null);
        }

        private void EquipSelected()
        {
            Transform anchor = ResolveHandAnchor();

            for (int i = 0; i < SelectableSlotCount; i++)
            {
                EquipmentBase item = GetSlot(i);
                if (item == null)
                    continue;

                // An installed device is world state and not bag content. Sweeping it with the
                // rest is what took a mounted projector off the wall on every slot change: the
                // holster un-placed it, parented it to the hand and hid it, and the next press
                // of its own number handed it back. The slot is let go of instead.
                if (item.IsPlaced)
                {
                    ForgetPlacedOccupant(i);
                    continue;
                }

                if (i == _selectedIndex)
                {
                    if (item is IHeldEquipment held)
                    {
                        var equipped = held.TryEquip(anchor);
                        if (!equipped.Ok)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            // A refused equip used to be thrown away here, so "the item is in my
                            // bag and not in my hand" had no reason attached to it anywhere.
                            CIYCLog.Info("[CIYC][Inventory] slot " + i + " refused the hand: " +
                                         equipped.Status + " (" + equipped.Detail + ").");
#endif
                        }
                    }
                    else
                    {
                        item.Equip(anchor);
                    }
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
            // Never an installed device. The room owns it until somebody takes it off the wall,
            // and stowing it here would be this bag quietly reclaiming world state.
            if (item == null || item.IsPlaced)
                return;

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
