using CatchIfYouCan.Interaction;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// One button for using, taking and putting down, above and right of the torch.
    ///
    /// <para>
    /// It is one control rather than three because at most one of them is ever the obvious thing
    /// to do: a reaching hand for something that can be taken, a finger for a door or a switch
    /// that is used rather than pocketed, and a hand dropping an item when carrying with nothing
    /// in front of you. When none applies it fades out entirely rather than sitting there greyed
    /// - this is a horror game and an unusable button is just something else covering the room.
    /// </para>
    ///
    /// <para>
    /// Press and hold works, which matters because hiding and the breaker box are holds rather
    /// than taps. While the thumb is down and has not wandered, the interact input is held, so
    /// those progress; the moment the press turns into a look the hold is dropped, which cancels
    /// them exactly as taking the finger off would.
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
        public enum Mode { Hidden, Interact, Pickup, Drop }

        [Header("Visuals")]
        [SerializeField] private Image icon;
        [SerializeField] private Graphic ring;
        [SerializeField] private CanvasGroup group;

        [SerializeField] private Sprite interactSprite;
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
        private bool _holdingInteract;

        /// <summary>What the button is currently offering to do.</summary>
        public Mode Current => _mode;

        private void OnEnable()
        {
            _shown = 0f;
            ApplyFade(0f);
        }

        protected override void OnPressed()
        {
            // Nothing fires here. The press only becomes an action on release, and only if the
            // thumb stayed put, because this button sits inside the look area and a drag that
            // starts on it is a look.
            SetHold(_mode == Mode.Interact || _mode == Mode.Pickup);
        }

        protected override void OnReleased(bool dragged)
        {
            SetHold(false);

            if (dragged || _mode == Mode.Hidden)
                return;

            if (_mode == Mode.Drop)
            {
                if (inventory != null)
                    inventory.DropSelected();
                return;
            }

            // Instant interactables read the press; ones with a hold duration were already
            // progressing from the held flag above and ignore this.
            MobileInputController.Instance?.TapInteract();
        }

        protected override void OnCancelled() => SetHold(false);

        private void SetHold(bool held)
        {
            if (_holdingInteract == held)
                return;

            _holdingInteract = held;
            MobileInputController.Instance?.SetInteractHeld(held);
        }

        private void Update()
        {
            Resolve();

            // Letting the thumb wander turns the press into a look, so the hold has to go with
            // it - otherwise sliding off the button to look around would quietly keep opening
            // whatever it was pointed at.
            if (_holdingInteract && (WasDragged || !IsHeld || _mode == Mode.Hidden || _mode == Mode.Drop))
                SetHold(false);

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
        /// What is in front of the player wins over what is in their hands, so walking up to a
        /// second item while already carrying one still picks it up rather than making them empty
        /// their hands first, and a door in reach is still a door. The free-slot test matters:
        /// without it the button would offer a pickup that <see cref="PlayerInventory.AddItem"/>
        /// then silently refuses - and a full inventory in front of an item falls through to the
        /// interact icon, which is at least honest about there being something there.
        /// </para>
        /// </summary>
        private void Resolve()
        {
            ResolveReferences();

            var target = interaction != null ? interaction.CurrentTarget : null;
            bool isPickup = target != null && target.InteractionType == InteractionType.Pickup;
            bool canTake = isPickup && inventory != null && inventory.HasFreeSlot;
            bool carrying = inventory != null && inventory.GetSelectedItem() != null;

            Mode next;
            if (canTake)
                next = Mode.Pickup;
            else if (target != null && !isPickup)
                next = Mode.Interact;
            else if (carrying)
                next = Mode.Drop;
            else
                next = Mode.Hidden;

            if (next == _mode)
                return;

            _mode = next;
            if (icon == null)
                return;

            Sprite sprite = _mode == Mode.Pickup ? pickupSprite
                          : _mode == Mode.Interact ? interactSprite
                          : _mode == Mode.Drop ? dropSprite
                          : null;
            if (sprite != null)
                icon.sprite = sprite;
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
