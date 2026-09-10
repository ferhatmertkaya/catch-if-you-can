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

            if (_input.EquipmentPowerPressed)
                HandlePower();
        }

        /// <summary>
        /// X. Commits a placement when one is being aimed; otherwise leaves the press alone so
        /// the interaction controller can pick things up and take mounted devices back.
        /// </summary>
        private void HandleInteract()
        {
            var aiming = _inventory.GetSelectedItem() as PlaceableEquipmentBase;
            if (aiming == null ||
                aiming.LifecycleState != EquipmentLifecycleState.PlacementPreview)
                return;

            // The press belongs to the placement whether or not it succeeds: a refused commit
            // must not fall through into "pick up whatever the ray hits", which with a preview
            // in front of the player is the wall.
            ConsumedInteractThisFrame = true;

            if (!aiming.HasValidCandidate)
            {
                CIYCLog.Info(LogTag + "[BLOCKED] PLACE reason=" +
                             (aiming.Candidate.Status == EquipmentActionStatus.Blocked
                                  ? "Blocked:" + aiming.Candidate.Detail
                                  : "NoValidWall"));
                return;
            }

            EquipmentActionResult placed = aiming.TryPlace();
            if (placed.Ok)
            {
                CIYCLog.Info(LogTag + "PLACED position=" +
                             aiming.transform.position.ToString("F2") + " rotation=" +
                             aiming.transform.rotation.eulerAngles.ToString("F1"));
            }
            else
            {
                CIYCLog.Info(LogTag + "[BLOCKED] PLACE reason=" + placed.Status +
                             (string.IsNullOrEmpty(placed.Detail) ? "" : ":" + placed.Detail));
            }
        }

        /// <summary>
        /// G. Switches the device the player is responsible for: the one they are holding, or
        /// the one they have on a wall. A device that is neither says so rather than doing
        /// nothing.
        /// </summary>
        private void HandlePower()
        {
            var selected = _inventory.GetSelectedItem();
            if (selected is not SpectralGridProjector projector)
                return;

            if (!projector.IsPlaced)
            {
                CIYCLog.Info(LogTag + "[BLOCKED] POWER reason=NotMounted");
                return;
            }

            projector.SetPowered(!projector.IsProjecting);
            CIYCLog.Info(LogTag + (projector.IsProjecting ? "POWER_ON" : "POWER_OFF"));
        }
    }
}
