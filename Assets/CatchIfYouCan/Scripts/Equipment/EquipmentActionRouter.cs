using CatchIfYouCan.Core;
using CatchIfYouCan.Input;
using CatchIfYouCan.Player;
using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Routes the two keys a deployable device needs to the calls that already exist.
    ///
    /// <para>
    /// <b>X confirms a placement, and G switches a deployed device.</b> Both actions were
    /// already implemented and reachable only from a HUD button:
    /// <see cref="PlaceableEquipmentBase.TryPlace"/> and the device's own power. This puts the
    /// keys on them without a second placement framework and without a second input path -
    /// <see cref="MobileInputController"/> stays the only thing that reads a key, and this only
    /// asks it what happened.
    /// </para>
    ///
    /// <para>
    /// X is deliberately handled HERE rather than in the interaction controller: while a
    /// preview is up the player is aiming a device, not looking for something to touch, and a
    /// world raycast would find the wall behind the preview instead of committing it. Taking
    /// the press before the raycast is what makes "X places it" and "X picks things up" one
    /// key rather than two that fight.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentActionRouter : MonoBehaviour
    {
        private const string LogTag = "[CIYC][DOTS] ";

        private MobileInputController _input;
        private PlayerInventory _inventory;

        /// <summary>
        /// True while the player is aiming a device, so the interaction controller knows the
        /// press was spent. Read rather than guessed: two consumers of one press is how an
        /// item gets placed and immediately picked up again in the same frame.
        /// </summary>
        public bool ConsumedInteractThisFrame { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            // The torch offers G here first. Claiming it means the torch does NOT also toggle.
            MobileInputController.EquipmentPowerClaim = TryClaimPower;
        }

        private void OnDisable()
        {
            if (MobileInputController.EquipmentPowerClaim == TryClaimPower)
                MobileInputController.EquipmentPowerClaim = null;
        }

        private void Update()
        {
            ConsumedInteractThisFrame = false;

            if (_input == null)
                _input = MobileInputController.Instance;
            if (_input == null || _inventory == null)
                return;

            if (MenuInputGate.IsMenuOpen)
                return;

            if (_input.InteractPressed)
                HandleInteract();
        }

        /// <summary>
        /// X. Commits a placement ONLY when a valid one is being aimed.
        ///
        /// <para>
        /// The press is consumed only in that case. Without a valid wall the press belongs to
        /// the interaction controller as usual, so looking at something else and pressing X
        /// still picks it up - swallowing every press while a projector is in hand would make
        /// the rest of the world unusable while carrying one.
        /// </para>
        /// </summary>
        private void HandleInteract()
        {
            var aiming = _inventory.GetSelectedItem() as PlaceableEquipmentBase;
            if (aiming == null ||
                aiming.LifecycleState != EquipmentLifecycleState.PlacementPreview)
                return;

            if (!aiming.HasValidCandidate)
            {
                CIYCLog.Info(LogTag + "PREVIEW_HIDDEN reason=NoValidWall (X left to the world)");
                return;
            }

            // The press belongs to the placement from here on. Letting it through as well is
            // how the same X both places the device and immediately acts on the wall behind it.
            ConsumedInteractThisFrame = true;
            CIYCLog.Info(LogTag + "X_ROUTE=PLACE");

            EquipmentActionResult placed = aiming.TryPlace();
            if (placed.Ok)
            {
                CIYCLog.Info(LogTag + "PLACED position=" +
                             aiming.transform.position.ToString("F2") + " rotation=" +
                             aiming.transform.rotation.eulerAngles.ToString("F1"));
            }
            else
            {
                CIYCLog.Error(LogTag + "[BLOCKED] PLACE reason=" + placed.Status +
                              (string.IsNullOrEmpty(placed.Detail) ? "" : ":" + placed.Detail) +
                              " - the preview was valid, so this is a routing bug.");
            }
        }

        /// <summary>
        /// G. Takes the press only for a device this player has DEPLOYED; otherwise leaves it
        /// to the torch, which is what G has always done.
        /// </summary>
        /// <returns>True when a deployed device handled it and the torch must not.</returns>
        private bool TryClaimPower()
        {
            if (_inventory == null || MenuInputGate.IsMenuOpen)
                return false;

            if (_inventory.GetSelectedItem() is not SpectralGridProjector projector)
                return false;

            if (!projector.IsPlaced)
            {
                // Held rather than mounted: say so, and let the torch have the press. Silently
                // eating it would make G look broken while a projector is in hand.
                CIYCLog.Info(LogTag + "[BLOCKED] POWER reason=NotMounted");
                return false;
            }

            CIYCLog.Info(LogTag + "G_ROUTE=DOTS");
            projector.SetPowered(!projector.IsProjecting);
            CIYCLog.Info(LogTag + (projector.IsProjecting ? "POWER_ON" : "POWER_OFF"));
            return true;
        }
    }
}
