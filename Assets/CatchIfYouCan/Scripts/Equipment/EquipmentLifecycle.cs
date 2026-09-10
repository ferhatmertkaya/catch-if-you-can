using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Where a piece of equipment is in the world, as one value.
    ///
    /// <para>
    /// This used to be three independent booleans - <c>IsEquipped</c>, <c>IsPlaced</c> and the
    /// device-active flag - which between them can express states that cannot exist, such as
    /// equipped and placed at once, and cannot express states that do, such as "in a slot the
    /// player is not currently holding". A torch in the bag and a torch on the floor were the
    /// same pair of falses, and only the torch knew the difference.
    /// </para>
    ///
    /// <para>
    /// Not every item reaches every state. What an item is allowed to do is
    /// <see cref="EquipmentDefinition"/>'s to say - <c>CanUse</c>, <c>CanPlace</c>,
    /// <c>CanDrop</c> - and the lifecycle refuses the rest with a reason rather than silently.
    /// </para>
    /// </summary>
    public enum EquipmentLifecycleState
    {
        /// <summary>Lying in the room, not owned by anyone. Pickable.</summary>
        World = 0,

        /// <summary>In an inventory slot that is not the selected one. Nothing is rendered in the hand.</summary>
        Holstered = 1,

        /// <summary>In the selected slot, in the hand, presented.</summary>
        Equipped = 2,

        /// <summary>Equipped and mid-use. A momentary state; most items return to Equipped.</summary>
        Using = 3,

        /// <summary>Equipped, and showing where it would go if placed.</summary>
        PlacementPreview = 4,

        /// <summary>Installed in the room and still owned as a logical item. Not the same as World.</summary>
        Placed = 5,
    }

    /// <summary>
    /// Why an equipment action did or did not happen.
    ///
    /// <para>
    /// Every one of these was previously a <c>return false</c> or a silent no-op, which is the
    /// same answer for "this item cannot be placed", "the battery is flat", "you are pointing
    /// at a wall you cannot reach" and "your hands are full". The lab could not tell you which,
    /// and neither could a player.
    /// </para>
    /// </summary>
    public enum EquipmentActionStatus
    {
        Success = 0,

        /// <summary>The definition forbids it: CanUse, CanPlace or CanDrop is false.</summary>
        NotAllowedByDefinition,

        /// <summary>Wrong lifecycle state for this action - placing something already placed.</summary>
        WrongState,

        /// <summary>Flat battery.</summary>
        NoBattery,

        /// <summary>Durability is gone.</summary>
        Broken,

        /// <summary>Every inventory slot is full.</summary>
        NoInventorySpace,

        /// <summary>Nothing to place against, or the surface is the wrong kind.</summary>
        NoValidSurface,

        /// <summary>The candidate position intersects geometry.</summary>
        Blocked,

        /// <summary>Out of reach.</summary>
        OutOfRange,

        /// <summary>A prefab, material, profile or clip the action needs is not there.</summary>
        MissingContent,

        /// <summary>The item does not implement this action at all.</summary>
        NotSupported,

        /// <summary>
        /// The caller is not allowed to decide this. Unused in single player, and the seam a
        /// future host-authoritative layer answers through without any item changing.
        /// </summary>
        NoAuthority,
    }

    /// <summary>
    /// The answer to an equipment request: whether it happened, and if not, why.
    ///
    /// <para>
    /// A struct rather than a bool so the reason survives the return, and readonly so a caller
    /// cannot edit the answer it was given. The <see cref="Detail"/> is for a human reading a
    /// lab readout or a log - never parse it.
    /// </para>
    /// </summary>
    public readonly struct EquipmentActionResult
    {
        public readonly EquipmentActionStatus Status;
        public readonly string Detail;

        public bool Ok => Status == EquipmentActionStatus.Success;

        public EquipmentActionResult(EquipmentActionStatus status, string detail = null)
        {
            Status = status;
            Detail = detail;
        }

        public static EquipmentActionResult Success => new EquipmentActionResult(EquipmentActionStatus.Success);

        public static EquipmentActionResult Fail(EquipmentActionStatus status, string detail = null) =>
            new EquipmentActionResult(status, detail);

        public override string ToString() =>
            string.IsNullOrEmpty(Detail) ? Status.ToString() : Status + ": " + Detail;
    }
}
