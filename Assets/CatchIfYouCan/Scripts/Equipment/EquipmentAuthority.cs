using CatchIfYouCan.Core;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The equipment-facing name for the one authority. It forwards; it does not decide.
    ///
    /// <para>
    /// V3 introduced this class and it was the right shape under the wrong name.
    /// <c>CanConfirmEvidence</c> was never an equipment question, and by V4 the ghost, the
    /// interactables and the mission need the same answer. Adding a second authority for them
    /// would be exactly the mistake this repository has already made twice - two flashlights,
    /// two inventories, both merging cleanly and only one of each being real.
    /// </para>
    ///
    /// <para>
    /// So the provider moved to <see cref="SessionAuthority"/> and this became three
    /// forwarding lines. Equipment code keeps calling the name it knows, there is exactly one
    /// provider, and setting it once changes every gate in the game.
    /// </para>
    /// </summary>
    public static class EquipmentAuthority
    {
        /// <summary>The one provider. Same object <see cref="SessionAuthority"/> holds.</summary>
        public static SessionAuthority.IAuthorityProvider Provider
        {
            get => SessionAuthority.Provider;
            set => SessionAuthority.Provider = value;
        }

        public static bool IsHost => SessionAuthority.IsHost;

        /// <summary>
        /// Whether this process may install or remove this item in the room. Asked by
        /// <see cref="PlaceableEquipmentBase.TryPlace"/> and
        /// <see cref="HeldEquipmentBase.TryPickupPlaced"/>.
        /// </summary>
        public static bool CanChangeWorldState(EquipmentBase equipment) =>
            SessionAuthority.CanChangeWorldState(equipment);

        /// <summary>
        /// Whether this process may turn an observation into confirmed evidence. Asked by
        /// <see cref="Evidence.EvidenceValidator"/>.
        /// </summary>
        public static bool CanConfirmEvidence => SessionAuthority.CanConfirmEvidence;
    }
}
