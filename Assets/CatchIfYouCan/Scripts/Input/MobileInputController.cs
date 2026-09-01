using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.EventSystems;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Input
{
    public class MobileInputController : SingletonBehaviour<MobileInputController>
    {
        [Header("References")]
        [SerializeField] private VirtualJoystick moveJoystick;
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private float lookScreenSplit = 0.5f;

        public Vector2 MoveInput
        {
            get
            {
                if (moveJoystick != null && moveJoystick.Direction.sqrMagnitude > 0.01f)
                    return moveJoystick.Direction;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current == null)
                    return Vector2.zero;

                float x = 0f;
                float y = 0f;

                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;

                var keyboard = new Vector2(x, y);
                return keyboard.sqrMagnitude > 1f ? keyboard.normalized : keyboard;
#else
                float x = UnityEngine.Input.GetAxisRaw("Horizontal");
                float y = UnityEngine.Input.GetAxisRaw("Vertical");
                var keyboard = new Vector2(x, y);
                return keyboard.sqrMagnitude > 1f ? keyboard.normalized : keyboard;
#endif
            }
        }
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool UsePressed { get; private set; }
        public bool JournalPressed { get; private set; }
        public bool FlashlightPressed { get; private set; }
        public bool InteractHeld { get; private set; }

        private int _lookFingerId = -1;
        private bool _blockMouseLookThisFrame;

        // Set while a TouchLookArea is alive. When one is, the EventSystem is delivering look
        // drags to it per pointer and the raw touch scan below must stay out of the way.
        private static TouchLookArea _lookArea;

        internal static void RegisterLookArea(TouchLookArea area) => _lookArea = area;

        internal static void UnregisterLookArea(TouchLookArea area)
        {
            if (_lookArea == area)
                _lookArea = null;
        }

        /// <summary>
        /// Adds look movement measured by the UI, in pixels. Accumulated rather than assigned so
        /// two sources in the same frame cannot silently discard each other.
        /// </summary>
        public void AddLookDelta(Vector2 pixelDelta)
        {
            LookDelta += pixelDelta * lookSensitivity;
        }

        public event Action OnInteractTap;
        public event Action OnUseTap;
        public event Action OnJournalTap;
        public event Action OnFlashlightTap;

        private void Update()
        {
            LookDelta = Vector2.zero;
            InteractPressed = false;
            UsePressed = false;
            JournalPressed = false;
            FlashlightPressed = false;
            // Only the mouse is blocked by the cursor being over UI. Touch is not: asking "is any
            // pointer over UI" and blocking the whole frame is what made holding the movement
            // joystick — itself a UI element — switch looking off, which is the opposite of the
            // simultaneous move-and-look this game needs.
            _blockMouseLookThisFrame = IsMousePointerOverUI();

            ProcessLookTouch();
            ProcessKeyboardLook();
            ProcessKeyboardActions();
        }

        public void BindJoystick(VirtualJoystick joystick)
        {
            if (joystick != null)
                moveJoystick = joystick;
        }

        private void ProcessKeyboardLook()
        {
#if ENABLE_INPUT_SYSTEM
            bool hasActiveTouch = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
            if (_blockMouseLookThisFrame || hasActiveTouch || _lookFingerId >= 0)
                return;

            if (Mouse.current == null)
                return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            if (mouseDelta.sqrMagnitude > 0.001f)
                LookDelta += mouseDelta * lookSensitivity * 0.05f;
#else
            if (_blockMouseLookThisFrame || UnityEngine.Input.touchCount > 0 || _lookFingerId >= 0)
                return;

            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
                LookDelta += new Vector2(mouseX, mouseY) * lookSensitivity * 10f;
#endif
        }

        private void ProcessKeyboardActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
                PressInteract();
            if (Keyboard.current.fKey.wasPressedThisFrame)
                PressUse();
            if (Keyboard.current.tabKey.wasPressedThisFrame)
                PressJournal();
            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
                SetSprint(true);
            if (Keyboard.current.leftShiftKey.wasReleasedThisFrame)
                SetSprint(false);
            if (Keyboard.current.cKey.wasPressedThisFrame)
                SetCrouch(true);
            if (Keyboard.current.cKey.wasReleasedThisFrame)
                SetCrouch(false);
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                PressInteract();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                PressUse();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                PressJournal();
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftShift))
                SetSprint(true);
            if (UnityEngine.Input.GetKeyUp(KeyCode.LeftShift))
                SetSprint(false);
            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
                SetCrouch(true);
            if (UnityEngine.Input.GetKeyUp(KeyCode.C))
                SetCrouch(false);
#endif
        }

        private void ProcessLookTouch()
        {
            // A TouchLookArea owns looking when one exists; the EventSystem routes each finger to
            // exactly one widget, which is stricter than anything this scan can manage. The scan
            // stays only as a fallback for scenes that have no HUD.
            if (_lookArea != null)
            {
                _lookFingerId = -1;
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                _lookFingerId = -1;
                return;
            }

            if (_lookFingerId >= 0)
            {
                foreach (var touchControl in touchscreen.touches)
                {
                    if (!touchControl.press.isPressed)
                        continue;

                    int touchId = touchControl.touchId.ReadValue();
                    if (touchId != _lookFingerId)
                        continue;

                    Vector2 delta = touchControl.delta.ReadValue() * lookSensitivity;
                    if (delta.sqrMagnitude > 0.001f)
                        LookDelta = delta;

                    return;
                }

                _lookFingerId = -1;
            }

            foreach (var touchControl in touchscreen.touches)
            {
                if (!touchControl.press.wasPressedThisFrame)
                    continue;

                Vector2 position = touchControl.position.ReadValue();
                if (position.x < Screen.width * lookScreenSplit)
                    continue;

                int touchId = touchControl.touchId.ReadValue();
                if (IsTouchOverUI(touchId))
                    continue;

                _lookFingerId = touchId;
                return;
            }
#else
            if (UnityEngine.Input.touchCount == 0)
            {
                _lookFingerId = -1;
                return;
            }

            if (_lookFingerId >= 0)
            {
                for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    Touch touch = UnityEngine.Input.GetTouch(i);
                    if (touch.fingerId != _lookFingerId)
                        continue;

                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _lookFingerId = -1;
                        return;
                    }

                    if (touch.phase == TouchPhase.Moved)
                    {
                        Vector2 delta = touch.deltaPosition * lookSensitivity;
                        LookDelta = new Vector2(delta.x, delta.y);
                    }
                    return;
                }

                _lookFingerId = -1;
            }

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                Touch touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                if (touch.position.x < Screen.width * lookScreenSplit)
                    continue;

                if (IsTouchOverUI(touch.fingerId))
                    continue;

                _lookFingerId = touch.fingerId;
                return;
            }
#endif
        }

        public void SetSprint(bool held) => SprintHeld = held;
        public void SetCrouch(bool held) => CrouchHeld = held;

        public void PressInteract()
        {
            InteractPressed = true;
            InteractHeld = true;
            OnInteractTap?.Invoke();
        }

        public void ReleaseInteract() => InteractHeld = false;

        public void PressUse()
        {
            UsePressed = true;
            OnUseTap?.Invoke();
        }

        public void PressJournal()
        {
            JournalPressed = true;
            OnJournalTap?.Invoke();
        }

        public void PressFlashlight()
        {
            FlashlightPressed = true;
            OnFlashlightTap?.Invoke();
        }

        /// <summary>
        /// True when the mouse cursor is over a UI element. Deliberately ignores touches: a
        /// finger on the joystick says nothing about whether the other thumb may look around.
        /// </summary>
        private static bool IsMousePointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
                return false;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                foreach (var touchControl in Touchscreen.current.touches)
                {
                    if (!touchControl.press.isPressed)
                        continue;

                    int touchId = touchControl.touchId.ReadValue();
                    if (EventSystem.current.IsPointerOverGameObject(touchId))
                        return true;
                }
            }

            return EventSystem.current.IsPointerOverGameObject();
#else
            if (UnityEngine.Input.touchCount > 0)
            {
                for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    if (EventSystem.current.IsPointerOverGameObject(UnityEngine.Input.GetTouch(i).fingerId))
                        return true;
                }
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
#endif
        }

        private static bool IsTouchOverUI(int fingerId)
        {
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
