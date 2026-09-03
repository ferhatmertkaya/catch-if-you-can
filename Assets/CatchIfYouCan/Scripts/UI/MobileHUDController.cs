using System.Collections;
using CatchIfYouCan.Core;
using CatchIfYouCan.Input;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class MobileHUDController : MonoBehaviour
    {
        [SerializeField] private Image caseIcon;
        [SerializeField] private Button journalButton;
        [SerializeField] private RectTransform joystickArea;
        [SerializeField] private Button interactButton;
        [SerializeField] private Button crouchButton;
        [SerializeField] private Button sprintButton;
        [Tooltip("The three inventory slots. It owns them outright - two components writing " +
                 "the same Images is how the icon and the highlight end up disagreeing.")]
        [SerializeField] private InventorySlotSelector inventorySelector;
        [SerializeField] private Button useButton;
        [SerializeField] private RectTransform safeAreaRoot;
        [SerializeField] private float interactPulseSpeed = 3f;
        [SerializeField] private float interactPulseMin = 0.85f;
        [SerializeField] private float interactPulseMax = 1.15f;

        private bool _interactAvailable;
        private Coroutine _pulseRoutine;
        private Image _interactImage;
        private Vector3 _interactBaseScale = Vector3.one;

        public void BindRuntime(
            Image caseIcon,
            Button journalButton,
            RectTransform joystickArea,
            Button interactButton,
            Button crouchButton,
            Button sprintButton,
            InventorySlotSelector inventorySelector,
            Button useButton)
        {
            this.caseIcon = caseIcon;
            this.journalButton = journalButton;
            this.joystickArea = joystickArea;
            this.interactButton = interactButton;
            this.crouchButton = crouchButton;
            this.sprintButton = sprintButton;
            this.inventorySelector = inventorySelector;
            this.useButton = useButton;
            ApplySafeArea();
            WireButtons();
        }

        private void OnEnable()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.HUD, false);
            ApplySafeArea();
            WireButtons();
            inventorySelector?.Refresh();
        }

        private void OnDisable()
        {
            StopPulse();
        }

        private void Start()
        {
            if (_interactImage == null && interactButton != null)
            {
                _interactImage = interactButton.GetComponent<Image>();
                _interactBaseScale = interactButton.transform.localScale;
            }
            ApplySafeArea();
            WireButtons();
        }

        private void WireButtons()
        {
            var input = MobileInputController.Instance;

            if (journalButton != null)
            {
                journalButton.onClick.RemoveAllListeners();
                journalButton.onClick.AddListener(() =>
                {
                    input?.PressJournal();
                    if (UIManager.Instance != null)
                        UIManager.Instance.Toggle(UIScreen.Journal);
                });
            }

            if (interactButton != null)
            {
                interactButton.onClick.RemoveAllListeners();
                interactButton.onClick.AddListener(() => input?.PressInteract());
            }

            if (useButton != null)
            {
                useButton.onClick.RemoveAllListeners();
                useButton.onClick.AddListener(() => input?.PressUse());
            }

            WireHold(crouchButton, held => MobileInputController.Instance?.SetCrouch(held));
            WireHold(sprintButton, held => MobileInputController.Instance?.SetSprint(held));
        }

        /// <summary>
        /// Attaches the hold behaviour to a button once, however many times the HUD is wired.
        ///
        /// <para>
        /// WireButtons runs from BindRuntime, from OnEnable and from Start, and each run used
        /// to <c>AddComponent&lt;HoldButton&gt;</c> unconditionally. So crouch and sprint
        /// arrived with three of them before the first frame, each with its own subscriber,
        /// and every enable of the HUD added two more - one press, three or five or seven
        /// SetCrouch calls, growing for the life of the session.
        /// </para>
        ///
        /// <para>
        /// The handler is also read through the singleton at press time rather than captured
        /// at wire time, because the HUD is built before the input controller exists in every
        /// scene that spawns one - a captured null stays null.
        /// </para>
        /// </summary>
        private static void WireHold(Button button, System.Action<bool> onHeld)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();

            var hold = button.GetComponent<HoldButton>();
            if (hold != null)
                return;

            hold = button.gameObject.AddComponent<HoldButton>();
            hold.OnHeldChanged += onHeld;
        }

        public void SetInteractAvailable(bool available)
        {
            if (_interactAvailable == available) return;
            _interactAvailable = available;
            // Was `available || true`, which is true. The interact button is deliberately
            // always pressable - the pulse is what says whether there is anything to interact
            // with - so this states that rather than computing it and discarding the answer.
            if (interactButton != null)
                interactButton.interactable = true;

            if (available)
                StartPulse();
            else
                StopPulse();
        }

        public void SetCaseIcon(Sprite sprite)
        {
            if (caseIcon == null) return;
            caseIcon.sprite = sprite;
            caseIcon.color = sprite != null ? Color.white : UITheme.Primary;
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            float left = safe.xMin;
            float right = Screen.width - safe.xMax;
            float bottom = safe.yMin;
            float top = Screen.height - safe.yMax;

            if (joystickArea != null)
                joystickArea.offsetMin = new Vector2(left * 0.15f, bottom * 0.1f);

            if (interactButton != null)
            {
                var rect = interactButton.GetComponent<RectTransform>();
                rect.offsetMax = new Vector2(-right * 0.1f, -top * 0.05f);
            }

            if (safeAreaRoot != null)
            {
                safeAreaRoot.offsetMin = new Vector2(left, bottom);
                safeAreaRoot.offsetMax = new Vector2(-right, -top);
            }
        }

        private void StartPulse()
        {
            if (_pulseRoutine != null) return;
            if (interactButton != null)
            {
                _interactImage = interactButton.GetComponent<Image>();
                _interactBaseScale = interactButton.transform.localScale;
            }
            _pulseRoutine = StartCoroutine(PulseInteract());
        }

        private void StopPulse()
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
            if (interactButton != null)
            {
                interactButton.transform.localScale = _interactBaseScale;
                if (_interactImage != null)
                    _interactImage.color = UITheme.Secondary;
            }
        }

        private IEnumerator PulseInteract()
        {
            while (_interactAvailable && interactButton != null)
            {
                float t = (Mathf.Sin(Time.unscaledTime * interactPulseSpeed) + 1f) * 0.5f;
                float scale = Mathf.Lerp(interactPulseMin, interactPulseMax, t);
                interactButton.transform.localScale = _interactBaseScale * scale;
                if (_interactImage != null)
                    _interactImage.color = Color.Lerp(UITheme.Secondary, UITheme.Primary, t);
                yield return null;
            }
        }

        private class HoldButton : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
            UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            public event System.Action<bool> OnHeldChanged;
            private bool _held;

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) => SetHeld(true);
            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) => SetHeld(false);
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => SetHeld(false);

            private void SetHeld(bool value)
            {
                if (_held == value) return;
                _held = value;
                OnHeldChanged?.Invoke(_held);
            }
        }
    }
}
