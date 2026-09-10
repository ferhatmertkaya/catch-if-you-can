using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The one contract every piece of carried equipment answers to.
    ///
    /// <para>
    /// Eleven items, one lifecycle. Before this there was one: the torch had a hand, a grip, a
    /// lagged aim and real drop physics, and the other ten had a transform parented to an
    /// anchor with two offsets off their definition. "All eleven have a runtime path" is this
    /// interface being implemented eleven times, not eleven copies of the torch.
    /// </para>
    ///
    /// <para>
    /// <b>Every verb is a request that returns a reason.</b> That is not politeness, it is the
    /// networking seam: in single player the item answers for itself, and a later
    /// host-authoritative layer answers the same calls with
    /// <see cref="EquipmentActionStatus.NoAuthority"/> or with the host's decision, without any
    /// of the eleven implementations changing. There is no NGO here and there must not be -
    /// only the shape that makes adding it a wrapper rather than a rewrite.
    /// </para>
    ///
    /// <para>
    /// State lives on the item, not on the holder: battery, durability and lifecycle survive
    /// equip, holster, drop, pickup and placement, because they belong to the object rather
    /// than to whoever is carrying it this second.
    /// </para>
    /// </summary>
    public interface IHeldEquipment
    {
        /// <summary>The data identity. Stable id, prices, capabilities, content references.</summary>
        EquipmentDefinition Definition { get; }

        /// <summary>Where this item is right now. One value, not three booleans.</summary>
        EquipmentLifecycleState LifecycleState { get; }

        /// <summary>Whether the device itself is switched on. Orthogonal to being held.</summary>
        bool IsDeviceActive { get; }

        float BatteryPercent { get; }
        float Durability { get; }

        /// <summary>Its pose in the world, for a future layer that has to replicate it.</summary>
        Transform WorldPose { get; }

        /// <summary>
        /// Taken from the world into an inventory. The inventory decides whether there is room;
        /// the item decides whether it is in a state that can be taken.
        /// </summary>
        EquipmentActionResult TryPickup(Player.PlayerInventory into);

        /// <summary>Brought into the hand and presented. Called when its slot is selected.</summary>
        EquipmentActionResult TryEquip(Transform handAnchor);

        /// <summary>Put away. Still owned, no longer rendered in the hand, expensive work off.</summary>
        EquipmentActionResult TryHolster();

        /// <summary>The Use button, for whatever this item means by it.</summary>
        EquipmentActionResult TryUse();

        /// <summary>Start showing where it would go. Placeable items only.</summary>
        EquipmentActionResult TryBeginPlacement();

        /// <summary>Stop showing the preview without placing.</summary>
        EquipmentActionResult TryCancelPlacement();

        /// <summary>Commit the current preview.</summary>
        EquipmentActionResult TryPlace();

        /// <summary>Take a placed item back, keeping its battery, durability and settings.</summary>
        EquipmentActionResult TryPickupPlaced(Player.PlayerInventory into);

        /// <summary>Throw it out of the hand and let physics decide where it lands.</summary>
        EquipmentActionResult TryDrop();
    }
}
