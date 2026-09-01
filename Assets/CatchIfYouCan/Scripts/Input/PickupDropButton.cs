using CatchIfYouCan.Interaction;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// One button for taking things and putting them down, above and right of the torch.
    ///
    /// <para>
    /// It is one control rather than two because the two states are almost never both wanted:
    /// with empty hands there is nothing to put down, and with a full hand the thing in reach is
    /// usually not the thing you want. It shows the hand while something is in reach and there is
    /// a slot free, and the hand dropping an item otherwise while carrying. When neither applies
    /// it fades out entirely rather than sitting there greyed - this is a horror game and an
    /// unusable button is just something else covering the room.
    /// </para>
    ///
    /// <para>
    /// It owns no gameplay. Taking goes through <see cref="MobileInputController.TapInteract"/>,
    /// which is the same press a key sends and which <see cref="InteractionController"/> already
    /// turns into <c>IInteractable.Interact</c>; putting down goes through
    /// <see cref="PlayerInventory.DropSelected"/>. Both existed before this button did.
    /// </para>
    ///
    /// <para>
    /// Hidden with a <see cref="CanvasGroup"/> rather than by disabling the object, so a press in
    /// flight is never cancelled underneath the thumb by the state changing.
    /// </para>
    /// </summary>
    public sealed class PickupDropButton : TouchHoldButton
    {
        public enum Mode { Hidden, Pickup, Drop }

        [Header("Visuals")]
        [SerializeField] private Image icon;
        [SerializeField] private Graphic ring;
        [SerializeField] private CanvasGroup group;

        [SerializeField] private Sprite pickupSprite;
        [SerializeField] private Sprite dropSprite;

        [Tooltip("Seconds to fade in and out. The button appears as you walk up to something, so " +
                 "it must not pop.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.14f;

        [Header("References")]
        [Tooltip("Left empty by the HUD, which is built before it can know about the player. " +
                 "Found once, lazily, and then kept.")]
        [SerializeField] private InteractionController interaction;
        [SerializeField] private PlayerInventory inventory;

        private Mode _mode = Mode.Hidden;
        private float _shown;

        /// <summary>What the button is currently offering to do.</summary>
        public Mode Current => _mode;

        private void OnEnable()
        {
            _shown = 0f;
            ApplyFade(0f);
        }

        protected override void OnReleased(bool dragged)
        {
            // A drag was a look. Same rule as the torch: these buttons sit inside the look area
            // and must never act on a gesture that was aimed past them.
            if (dragged || _mode == Mode.Hidden)
                return;

            if (_mode == Mode.Pickup)
                MobileInputController.Instance?.TapInteract();
            else if (inventory != null)
                inventory.DropSelected();
        }

        private void Update()
        {
            Resolve();

            var target = _mode == Mode.Hidden ? 0f : 1f;
            if (!Mathf.Approximately(_shown, target))
            {
                _shown = fadeSeconds > 0f
                    ? Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime / fadeSeconds)
                    : target;
                ApplyFade(_shown);
            }
        }

        /// <summary>
        /// Decides what the button is for this frame.
        ///
        /// <para>
        /// Taking wins over putting down when both are possible, so walking up to a second item
        /// while already carrying one still picks it up rather than making the player empty their
        /// hands first. The free-slot test matters: without it the button would offer a pickup
        /// that <see cref="PlayerInventory.AddItem"/> then silently refuses.
        /// </para>
        /// </summary>
        private void Resolve()
        {
            ResolveReferences();

            bool canTake = interaction != null &&
                           interaction.CurrentTarget != null &&
                           interaction.CurrentTarget.InteractionType == InteractionType.Pickup &&
                           inventory != null && inventory.HasFreeSlot;

            bool carrying = inventory != null && inventory.GetSelectedItem() != null;

            Mode next = canTake ? Mode.Pickup : carrying ? Mode.Drop : Mode.Hidden;
            if (next == _mode)
                return;

            _mode = next;
            if (icon == null)
                return;

            if (_mode == Mode.Pickup && pickupSprite != null)
                icon.sprite = pickupSprite;
            else if (_mode == Mode.Drop && dropSprite != null)
                icon.sprite = dropSprite;
        }

        private void ResolveReferences()
        {
            if (interaction == null)
                interaction = Object.FindAnyObjectByType<InteractionController>();
            if (inventory == null)
                inventory = Object.FindAnyObjectByType<PlayerInventory>();
        }

        private void ApplyFade(float shown)
        {
            if (group == null)
                return;

            group.alpha = shown;
            // Below a sliver it must stop taking touches, or an invisible button sits on top of
            // the look area swallowing drags.
            group.blocksRaycasts = shown > 0.05f;
            group.interactable = group.blocksRaycasts;
        }
    }
}
