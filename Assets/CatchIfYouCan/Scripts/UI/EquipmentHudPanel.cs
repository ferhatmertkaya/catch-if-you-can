using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// What the item in your hand is called, what it is reading, and the buttons it brings
    /// with it.
    ///
    /// <para>
    /// There was none of this. The HUD had three slot pictures and a Use button, so the player
    /// could see which item was selected and press it, and could not see its battery, its
    /// charges, how much salt was left, what the thermometer said or which question the
    /// recorder would ask next. Eight of the eleven items also have controls beyond Use - zoom,
    /// night vision, the next question, installing the thing in a room - and on a phone none of
    /// them could be reached at all.
    /// </para>
    ///
    /// <para>
    /// The panel knows nothing about any particular item. It asks whatever is selected for a
    /// readout and a list of actions, and draws them. Adding a control to an item is a change
    /// to that item; it is not a change here.
    /// </para>
    /// </summary>
    public sealed class EquipmentHudPanel : MonoBehaviour
    {
        /// <summary>
        /// How many action buttons the row can show. Three: a thumb reaches three comfortably
        /// at the bottom of a phone, and no item currently offers more.
        /// </summary>
        public const int ActionCount = 3;

        [SerializeField] private Component titleText;
        [SerializeField] private Component readoutText;
        [SerializeField] private Button[] actionButtons = new Button[ActionCount];
        [SerializeField] private Component[] actionLabels = new Component[ActionCount];

        [Tooltip("Seconds between refreshes. A readout is read by a human, and rebuilding the " +
                 "row sixty times a second to move a temperature by a tenth of a degree is " +
                 "work nobody sees.")]
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        // One list for the life of the panel. Rebuilt in place every refresh, so showing the
        // HUD allocates nothing.
        private readonly List<EquipmentAction> _actions = new List<EquipmentAction>(ActionCount);

        private EquipmentBase _bound;
        private float _timer;
        private string _title;
        private string _readout;

        /// <summary>Called by the runtime UI factory, which builds the panel.</summary>
        public void BindRuntime(Component title, Component readout,
                                Button[] buttons, Component[] labels)
        {
            titleText = title;
            readoutText = readout;
            actionButtons = buttons;
            actionLabels = labels;
            Refresh();
        }

        private void OnEnable()
        {
            GameEvents.OnEquipmentChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnEquipmentChanged -= Refresh;
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;

            _timer = refreshInterval;
            Refresh();
        }

        /// <summary>Redraws from whatever is in the player's hand. Public so the HUD can force it.</summary>
        public void Refresh()
        {
            var inventory = LocalPlayerService.GetPlayerComponent<PlayerInventory>();
            var item = inventory != null ? inventory.GetSelectedItem() : null;

            if (item != _bound)
            {
                _bound = item;
                // A new item means new controls, so the row is rebuilt rather than retitled.
                // Rebinding the listeners only on a change keeps a per-frame refresh from
                // churning delegates.
                RebuildActions(item);
            }
            else
            {
                RefreshActionState(item);
            }

            if (item == null)
            {
                SetTitle("EMPTY");
                SetReadout(string.Empty);
                return;
            }

            string title = item.Definition != null ? item.Definition.DisplayName : item.name;
            SetTitle(title.ToUpperInvariant());
            SetReadout(item.HudReadout);
        }

        /// <summary>
        /// Writes the title only when it changed. It changes when the selected item does,
        /// which is a few times a minute - and ToUpperInvariant allocates a string every time
        /// it is called, ten times a second, for the length of a mission.
        /// </summary>
        private void SetTitle(string value)
        {
            if (_title == value)
                return;

            _title = value;
            UITheme.SetText(titleText, value);
        }

        /// <summary>
        /// The readout genuinely changes - it is a temperature, a battery, a countdown - so
        /// building it is unavoidable at the refresh rate. Comparing before writing still
        /// saves the layout rebuild a text change costs, which is the expensive half.
        /// </summary>
        private void SetReadout(string value)
        {
            if (_readout == value)
                return;

            _readout = value;
            UITheme.SetText(readoutText, value);
        }

        private void RebuildActions(EquipmentBase item)
        {
            _actions.Clear();
            item?.CollectActions(_actions);

            for (int i = 0; i < ActionCount; i++)
            {
                var button = Button(i);
                if (button == null)
                    continue;

                bool used = i < _actions.Count && _actions[i].IsValid;

                button.gameObject.SetActive(used);
                button.onClick.RemoveAllListeners();

                if (!used)
                    continue;

                var action = _actions[i];
                // Captured from the local, not from the list: the list is cleared and refilled
                // on the next rebuild, and a listener reading _actions[i] then would invoke
                // whatever item happens to be selected later.
                button.onClick.AddListener(() => action.Invoke?.Invoke());
                button.interactable = action.Enabled;

                _labelCache[i] = action.Label;
                UITheme.SetText(Label(i), action.Label);
            }
        }

        /// <summary>
        /// Re-asks the same item what its buttons should say and whether they can be pressed,
        /// without rewiring them. PLACE turning from grey to green as the aim finds a wall is
        /// this, and it has to happen at the refresh rate to be worth anything.
        /// </summary>
        private void RefreshActionState(EquipmentBase item)
        {
            if (item == null)
                return;

            _actions.Clear();
            item.CollectActions(_actions);

            // The set of actions can change without the item changing - a projector being
            // aimed offers PLACE and CANCEL where a moment ago it offered AIM - so a differing
            // count is a rebuild, not a state refresh.
            if (_actions.Count != CountActive())
            {
                RebuildActions(item);
                return;
            }

            for (int i = 0; i < _actions.Count && i < ActionCount; i++)
            {
                var button = Button(i);
                if (button == null)
                    continue;

                button.interactable = _actions[i].Enabled;
                SetLabel(i, _actions[i].Label);
            }
        }

        /// <summary>
        /// Writes an action label only when it changed. This runs at the refresh rate for
        /// every visible button, and almost every refresh writes the same three words back.
        /// </summary>
        private void SetLabel(int index, string value)
        {
            if (_labelCache[index] == value)
                return;

            _labelCache[index] = value;
            UITheme.SetText(Label(index), value);
        }

        private readonly string[] _labelCache = new string[ActionCount];

        private int CountActive()
        {
            int count = 0;
            for (int i = 0; i < ActionCount; i++)
            {
                var button = Button(i);
                if (button != null && button.gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        private Button Button(int index) =>
            actionButtons != null && index < actionButtons.Length ? actionButtons[index] : null;

        private Component Label(int index) =>
            actionLabels != null && index < actionLabels.Length ? actionLabels[index] : null;
    }
}
