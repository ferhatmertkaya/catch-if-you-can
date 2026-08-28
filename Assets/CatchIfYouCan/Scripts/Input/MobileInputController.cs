using System;
using UnityEngine;
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

                float x = UnityEngine.Input.GetAxisRaw("Horizontal");
                float y = UnityEngine.Input.GetAxisRaw("Vertical");
                var keyboard = new Vector2(x, y);
                return keyboard.sqrMagnitude > 1f ? keyboard.normalized : keyboard;
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
        private bool _blockLookThisFrame;

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
            _blockLookThisFrame = IsPointerOverUI();

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
            if (_blockLookThisFrame || Input.touchCount > 0 || _lookFingerId >= 0)
                return;

            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
                LookDelta = new Vector2(mouseX, mouseY) * lookSensitivity * 10f;
        }

        private void ProcessKeyboardActions()
        {
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
        }

        private void ProcessLookTouch()
        {
            if (_blockLookThisFrame)
            {
                _lookFingerId = -1;
                return;
            }

            if (Input.touchCount == 0)
            {
                _lookFingerId = -1;
                return;
            }

            if (_lookFingerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
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

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                if (touch.position.x < Screen.width * lookScreenSplit)
                    continue;

                if (IsTouchOverUI(touch.fingerId))
                    continue;

                _lookFingerId = touch.fingerId;
                return;
            }
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

        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
                return false;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                        return true;
                }
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsTouchOverUI(int fingerId)
        {
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
