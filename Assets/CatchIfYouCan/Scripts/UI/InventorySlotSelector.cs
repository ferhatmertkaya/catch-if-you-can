using CatchIfYouCan.Core;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The three inventory slots in the touch HUD, and the taps that switch between them.
    ///
    /// <para>
    /// The HUD already drew three slots, but they were pictures: nothing could be tapped, and
    /// nothing showed which of the three was in the player's hand. Selecting a slot was
    /// possible only with a keyboard, which on the platform this game is actually built for
    /// means it was not possible at all - the second item a player picked up was unreachable.
    /// </para>
    ///
    /// <para>
    /// It owns the slot visuals outright. Two components writing the same three Images is how
    /// the icon and the highlight end up disagreeing about which slot is selected.
    /// </para>
    /// </summary>
    public sealed class InventorySlotSelector : MonoBehaviour
    {
        [SerializeField] private Button[] slotButtons = new Button[PlayerInventory.SlotCount];
        [SerializeField] private Image[] slotIcons = new Image[PlayerInventory.SlotCount];

        [Tooltip("The frame drawn behind each slot. Its colour is the only thing that says " +
                 "which slot is selected, so it is a separate graphic from the icon.")]
        [SerializeField] private Image[] slotFrames = new Image[PlayerInventory.SlotCount];

        [Tooltip("UITheme.Secondary, subdued. The only new colour on the HUD: an unselected " +
                 "slot keeps the panel colour it already had.")]
        [SerializeField] private Color selectedFrame = new Color(0.098f, 0.843f, 0.482f, 0.45f);

        [Tooltip("UITheme.BackgroundPanel at the alpha UITheme.ApplyPanel uses, so a slot that " +
                 "is not selected looks exactly as it did before it became a button.")]
        [SerializeField] private Color idleFrame = new Color(0.063f, 0.082f, 0.075f, 0.92f);

        [Tooltip("Tint of a slot's icon when the slot is empty. Not fully transparent, so an " +
                 "empty slot still reads as a slot rather than as a gap in the row.")]
        [SerializeField] private Color emptyIcon = new Color(1f, 1f, 1f, 0.15f);

        private bool _wired;

        /// <summary>Called by the runtime UI factory, which builds the row.</summary>
        public void BindRuntime(Button[] buttons, Image[] icons, Image[] frames)
        {
            slotButtons = buttons;
            slotIcons = icons;
            slotFrames = frames;
            _wired = false;
            WireButtons();
            Refresh();
        }

        private void OnEnable()
        {
            WireButtons();

            GameEvents.OnEquipmentChanged += Refresh;
            // The HUD is built before the player exists in every scene that spawns one, so
            // without this the row comes up empty and stays empty until something else
            // happens to change the inventory.
            LocalPlayerService.PlayerRegistered += Refresh;
            LocalPlayerService.PlayerUnregistered += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnEquipmentChanged -= Refresh;
            LocalPlayerService.PlayerRegistered -= Refresh;
            LocalPlayerService.PlayerUnregistered -= Refresh;
        }

        private void WireButtons()
        {
            if (_wired || slotButtons == null)
                return;

            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null)
                    continue;

                int index = i;                       // captured per slot, not per loop

                // RemoveAllListeners, not RemoveListener: a lambda is a fresh delegate every
                // time it is written, so removing "the same" lambda removes nothing and the
                // slot ends up selecting itself twice per tap.
                slotButtons[i].onClick.RemoveAllListeners();
                slotButtons[i].onClick.AddListener(() => Select(index));
            }

            _wired = true;
        }

        private void Select(int index)
        {
            var inventory = LocalPlayerService.GetPlayerComponent<PlayerInventory>();
            if (inventory == null)
                return;

            // SelectSlot raises EquipmentChanged, which brings us back through Refresh, so the
            // highlight follows the inventory rather than the tap. A tap the inventory refuses
            // therefore changes nothing on screen, which is the honest answer.
            inventory.SelectSlot(index);
        }

        /// <summary>
        /// Redraws the row from the inventory. Public because the HUD refreshes it when it is
        /// shown, and cheap enough to call on any change worth redrawing for.
        /// </summary>
        public void Refresh()
        {
            var inventory = LocalPlayerService.GetPlayerComponent<PlayerInventory>();
            var loadout = Equipment.EquipmentManager.Instance;

            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                var item = inventory != null ? inventory.GetSlot(i) : null;

                // What is in the player's hands first, what they packed second. The loadout is
                // only a stand-in for a slot the player has not filled yet.
                Sprite icon = item != null && item.Definition != null ? item.Definition.Icon : null;
                if (icon == null && loadout != null && i < loadout.Loadout.Count &&
                    loadout.Loadout[i] != null)
                    icon = loadout.Loadout[i].Icon;

                if (slotIcons != null && i < slotIcons.Length && slotIcons[i] != null)
                {
                    slotIcons[i].sprite = icon;
                    slotIcons[i].color = icon != null ? Color.white : emptyIcon;
                }

                bool selected = inventory != null && inventory.SelectedIndex == i;
                if (slotFrames != null && i < slotFrames.Length && slotFrames[i] != null)
                    slotFrames[i].color = selected ? selectedFrame : idleFrame;
            }
        }
    }
}
