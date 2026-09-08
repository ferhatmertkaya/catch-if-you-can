#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Development
{
    /// <summary>
    /// Two desktop conveniences for looking at the world while building it: X takes and puts
    /// down, and whatever the player is looking at says what it is.
    ///
    /// <para>
    /// <b>It touches no input system.</b> <see cref="Input.MobileInputController"/> owns
    /// movement, look, the joystick and the HUD buttons; this reads one key from the keyboard
    /// directly and calls the same public methods the on-screen button already calls -
    /// <c>IInteractable.Interact</c> and <see cref="PlayerInventory.DropSelected"/>. A keyboard
    /// binding added to that controller would be a change to the file that owns mobile input,
    /// and the point of putting it here is that the file is provably untouched.
    /// </para>
    ///
    /// <para>
    /// <b>It touches no HUD.</b> The label is an <c>OnGUI</c> string in the corner of the screen,
    /// which exists nowhere in a player's build and cannot push a single pixel of the real HUD
    /// around. Redesigning the HUD to carry a debug caption would make the debug caption
    /// permanent.
    /// </para>
    ///
    /// <para>
    /// <b>It installs itself, and only where it belongs.</b> The whole file is fenced behind
    /// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>, so a shipped build contains none of it - no
    /// component, no scene reference, no key to press. Nothing had to be wired into a scene,
    /// which is one fewer hand-written document in a file the user is authoring by hand.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugItemTools : MonoBehaviour
    {
        private const string LogTag = "[CIYC][DebugItems] ";

        /// <summary>Take and put down. Deliberately not E: E is the shipping interact key.</summary>
        private const KeyCode TakeOrDropKey = KeyCode.X;

        private InteractionController _interaction;
        private PlayerInventory _inventory;
        private string _label = string.Empty;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("CIYC_DebugItemTools");
            host.AddComponent<DebugItemTools>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            RefreshPlayer();

            _label = DescribeTarget();

            if (UnityEngine.Input.GetKeyDown(TakeOrDropKey))
                TakeOrDrop();
        }

        /// <summary>
        /// Finds the player's controller when there is one, and forgets it when there is not.
        ///
        /// <para>
        /// Re-found rather than cached forever because this object outlives every scene: the
        /// lobby's player and the mission's player are different objects, and a reference to the
        /// first one is a reference to something that was destroyed at the handover.
        /// </para>
        /// </summary>
        private void RefreshPlayer()
        {
            if (_interaction != null && _inventory != null)
                return;

            _interaction = FindFirstObjectByType<InteractionController>(FindObjectsInactive.Exclude);
            _inventory = _interaction != null
                ? _interaction.GetComponent<PlayerInventory>()
                : null;
        }

        /// <summary>
        /// What is under the crosshair, named by its DEFINITION rather than by its object name.
        ///
        /// <para>
        /// An object name is whatever the factory happened to call the instance; the definition's
        /// display name is the thing the item actually is, and it is the same string the shop and
        /// the inventory show. Naming it any other way would be a second answer to "what is this",
        /// and the debug caption is exactly where a wrong second answer would be believed.
        /// </para>
        /// </summary>
        private string DescribeTarget()
        {
            if (_interaction == null || _interaction.CurrentTarget == null)
                return string.Empty;

            var behaviour = _interaction.CurrentTarget as MonoBehaviour;
            if (behaviour == null)
                return string.Empty;

            var equipment = behaviour.GetComponentInParent<EquipmentBase>();
            if (equipment != null && equipment.Definition != null)
            {
                return equipment.Definition.DisplayName.ToUpperInvariant() + "   /   DEBUG   [" +
                       equipment.Definition.Id + "]";
            }

            string prompt = _interaction.CurrentTarget.Prompt;
            return string.IsNullOrEmpty(prompt)
                ? behaviour.name + "   /   DEBUG"
                : prompt.ToUpperInvariant() + "   /   DEBUG";
        }

        /// <summary>
        /// X: take what is in front of you, or put down what you are holding.
        ///
        /// <para>
        /// Taking wins when both are possible. Standing over a table with a full hand, the
        /// obvious intent is to swap - and <c>CanInteract</c> already refuses a pickup when there
        /// is no free slot, so "take" quietly becomes "drop" exactly when it has to.
        /// </para>
        /// </summary>
        private void TakeOrDrop()
        {
            if (_interaction == null)
                return;

            IInteractable target = _interaction.CurrentTarget;
            if (target != null && target.InteractionType == InteractionType.Pickup &&
                target.CanInteract(_interaction.gameObject))
            {
                target.Interact(_interaction.gameObject);
                CIYCLog.Info(LogTag + "took " + DescribeTarget());
                return;
            }

            if (_inventory != null && _inventory.GetSelectedItem() != null)
            {
                bool dropped = _inventory.DropSelected();
                CIYCLog.Info(LogTag + (dropped ? "dropped the held item." : "the held item refused to drop."));
                return;
            }

            CIYCLog.Info(LogTag + "nothing to take and nothing to put down.");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_label))
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
                _style.normal.textColor = new Color(0.75f, 1f, 0.82f);
            }

            // Below the centre of the screen, where a crosshair caption belongs, and drawn as
            // text only - no box, no background, nothing that could be mistaken for real HUD.
            var rect = new Rect(0f, Screen.height * 0.62f, Screen.width, 30f);
            GUI.Label(rect, _label, _style);
        }
    }
}
#endif
