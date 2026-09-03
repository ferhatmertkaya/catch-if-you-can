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

        [Tooltip("Device scale on look input. Left at 1 so the degrees-per-pixel figure lives in " +
                 "one place, on PlayerLook, rather than being the product of two fields.")]
        [SerializeField] private float lookSensitivity = 1f;

        [Tooltip("Gamepad look speed, in reference pixels per second at full stick. A stick is " +
                 "a rate where a mouse is a displacement, so this converts one into the other.")]
        [SerializeField, Min(1f)] private float gamepadLookSpeed = 900f;

        [Tooltip("Look input is normalised to this screen height in pixels, so the same thumb " +
                 "swipe turns the same amount on a 720p phone and a 1440p one.")]
        [SerializeField] private float lookReferenceHeight = 1080f;

        [SerializeField] private float lookScreenSplit = 0.5f;

        public Vector2 MoveInput
        {
            get
            {
                if (moveJoystick != null && moveJoystick.Direction.sqrMagnitude > 0.01f)
                    return moveJoystick.Direction;

#if ENABLE_INPUT_SYSTEM
                // A stick, if there is one. It arrives here rather than anywhere else on
                // purpose: a gamepad is another way of saying "walk forward", and every way of
                // saying that has to end at this one property or the game has two movement
                // systems that have to agree.
                if (Gamepad.current != null)
                {
                    Vector2 stick = Gamepad.current.leftStick.ReadValue();
                    if (stick.sqrMagnitude > 0.01f)
                        return stick.sqrMagnitude > 1f ? stick.normalized : stick;
                }

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
        private Vector2 _lookAccumulator;
        private int _lookAccumulatorFrame = -1;

        /// <summary>
        /// Look movement gathered this frame, in reference pixels.
        ///
        /// <para>
        /// Stamped with the frame it was gathered in rather than cleared at the top of Update.
        /// That distinction is the whole fix: the only writer for touch is a UI drag callback,
        /// which Unity dispatches from the EventSystem's own Update, and the order of two
        /// MonoBehaviour Updates is not defined. Clearing here meant the accumulated delta could
        /// be wiped after the drag had already added it and before PlayerLook read it in
        /// LateUpdate, which killed looking outright while leaving movement working — the
        /// joystick reports persistent state, so nothing could erase it.
        /// </para>
        ///
        /// <para>
        /// Reading on any later frame yields zero, which is what stops the camera coasting after
        /// the finger lifts. LateUpdate always follows every Update, so a consumer there always
        /// sees the frame's full delta no matter which order the Updates ran in.
        /// </para>
        /// </summary>
        public Vector2 LookDelta =>
            _lookAccumulatorFrame == Time.frameCount ? _lookAccumulator : Vector2.zero;
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }

        // One-shot presses, stamped with the frame they happened in rather than cleared at the
        // top of Update. Same reasoning as LookDelta above, and the same bug if it is not done:
        // a HUD button reports from a UI callback, which Unity dispatches from the EventSystem's
        // own Update, and the order of two MonoBehaviour Updates is not defined. Clearing a bool
        // here could wipe a press the button had already reported and that its consumer had not
        // read yet, so a tap would do nothing - intermittently, depending on script order.
        private int _interactFrame = -1;
        private int _useFrame = -1;
        private int _journalFrame = -1;
        private int _flashlightFrame = -1;

        public bool InteractPressed => _interactFrame == Time.frameCount;
        public bool UsePressed => _useFrame == Time.frameCount;
        public bool JournalPressed => _journalFrame == Time.frameCount;
        public bool FlashlightPressed => _flashlightFrame == Time.frameCount;
        public bool InteractHeld { get; private set; }

        /// <summary>
        /// Whether the torch is currently lit, as reported by whatever owns it.
        ///
        /// <para>
        /// Input does not decide this and does not toggle it: <see cref="PressFlashlight"/> asks,
        /// the torch answers. Keeping the answer here rather than having the HUD find the torch
        /// is what lets the button show the real state without the UI holding a reference to a
        /// gameplay object, or - worse - keeping its own copy of the state that drifts the first
        /// time anything else switches the light.
        /// </para>
        /// </summary>
        public bool FlashlightOn { get; private set; }

        /// <summary>Called by the torch when it lights or goes out.</summary>
        public void ReportFlashlightState(bool on) => FlashlightOn = on;

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
            if (_lookAccumulatorFrame != Time.frameCount)
            {
                _lookAccumulator = Vector2.zero;
                _lookAccumulatorFrame = Time.frameCount;
            }

            float scale = lookReferenceHeight / Mathf.Max(1, Screen.height);
            _lookAccumulator += pixelDelta * scale * lookSensitivity;
        }

        public event Action OnInteractTap;
        public event Action OnUseTap;
        public event Action OnJournalTap;
        public event Action OnFlashlightTap;

        private void Update()
        {
            // Only the mouse is blocked by the cursor being over UI. Touch is not: asking "is any
            // pointer over UI" and blocking the whole frame is what made holding the movement
            // joystick — itself a UI element — switch looking off, which is the opposite of the
            // simultaneous move-and-look this game needs.
            _blockMouseLookThisFrame = IsMousePointerOverUI();

            ProcessLookTouch();
            ProcessKeyboardLook();
            ProcessGamepadLook();
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
                AddLookDelta(mouseDelta);
#else
            if (_blockMouseLookThisFrame || UnityEngine.Input.touchCount > 0 || _lookFingerId >= 0)
                return;

            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
                AddLookDelta(new Vector2(mouseX, mouseY) * 10f);
#endif
        }

        /// <summary>
        /// The right stick, into the same look accumulator the mouse and the touch drag feed.
        ///
        /// <para>
        /// A stick is a rate and a mouse is a displacement, so it is scaled by delta time to
        /// become one - without that, look speed on a pad would depend on frame rate, which is
        /// the classic gamepad-look bug.
        /// </para>
        ///
        /// <para>
        /// It goes to the same accumulator rather than to a second look path because a second
        /// look path is a second sensitivity, a second inversion setting and a second place for
        /// the pitch clamp to be forgotten.
        /// </para>
        /// </summary>
        private void ProcessGamepadLook()
        {
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current == null)
                return;

            Vector2 stick = Gamepad.current.rightStick.ReadValue();
            if (stick.sqrMagnitude <= 0.01f)
                return;

            AddLookDelta(stick * (gamepadLookSpeed * Time.unscaledDeltaTime));
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
            // The torch had no key at all, so the HUD button was its only control and desktop
            // could not switch it on. G rather than the more usual F because F is already bound
            // to Use above and rebinding it is not this change's business.
            if (Keyboard.current.gKey.wasPressedThisFrame)
                PressFlashlight();
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.G))
                PressFlashlight();
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

                    Vector2 delta = touchControl.delta.ReadValue();
                    if (delta.sqrMagnitude > 0.001f)
                        AddLookDelta(delta);

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
                        AddLookDelta(touch.deltaPosition);
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
            _interactFrame = Time.frameCount;
            InteractHeld = true;
            OnInteractTap?.Invoke();
        }

        /// <summary>
        /// A press and its release in one call, for a control that taps rather than holds.
        /// Interactions with no hold duration - picking something up, flicking a switch - only
        /// ever read the press, and leaving InteractHeld latched afterwards would quietly
        /// complete every hold-to-interact the player then looked at.
        /// </summary>
        public void TapInteract()
        {
            PressInteract();
            InteractHeld = false;
        }

        public void ReleaseInteract() => InteractHeld = false;

        /// <summary>
        /// Holds or releases the interact button.
        ///
        /// <para>
        /// Separate from <see cref="PressInteract"/> because a hold and a press are different
        /// things to the interaction system: an instant interactable reads the press, while one
        /// with a hold duration - a hiding place, a breaker box - only progresses while this is
        /// true, and abandons the hold the moment it is not.
        /// </para>
        /// </summary>
        public void SetInteractHeld(bool held) => InteractHeld = held;

        public void PressUse()
        {
            _useFrame = Time.frameCount;
            OnUseTap?.Invoke();
        }

        public void PressJournal()
        {
            _journalFrame = Time.frameCount;
            OnJournalTap?.Invoke();
        }

        public void PressFlashlight()
        {
            _flashlightFrame = Time.frameCount;
            OnFlashlightTap?.Invoke();
        }

        /// <summary>
        /// True when the mouse cursor is over a UI element that should stop the camera.
        ///
        /// <para>
        /// Deliberately ignores touches: a finger on the joystick says nothing about whether the
        /// other thumb may look around.
        /// </para>
        ///
        /// <para>
        /// It also ignores the touch HUD itself. The controls are a transparent overlay across
        /// the right of the screen - exactly where a desktop cursor lives - so counting them as
        /// blocking meant free mouse-look froze whenever the pointer drifted over a button, on a
        /// build that has no touchscreen and never uses them. Menus and panels still block, which
        /// is the case this test exists for.
        /// </para>
        /// </summary>
        private static bool IsMousePointerOverUI()
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                return false;

            return !LookTransparentUI.PointerIsOver;
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
