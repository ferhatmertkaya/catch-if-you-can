using UnityEngine;

namespace CatchIfYouCan.Core
{
    /// <summary>What kinds of input this machine has. Not what platform it is.</summary>
    [System.Flags]
    public enum InputCapability
    {
        None = 0,
        Touch = 1 << 0,
        KeyboardMouse = 1 << 1,
        Gamepad = 1 << 2,
    }

    /// <summary>
    /// What this machine can do, asked as a capability rather than as a platform name.
    ///
    /// <para>
    /// <b>Gameplay must never branch on platform, and network protocol must never branch on
    /// anything.</b> A phone and a desktop in the same session play the same game by the same
    /// rules over the same protocol; what differs is how a person tells their character to walk
    /// forward, and that difference has to end at the input layer or it is not crossplay, it is
    /// two games that can connect to each other.
    /// </para>
    ///
    /// <para>
    /// So this answers "is there a gamepad" and not "is this a console". A capability can
    /// appear and disappear while the game runs - somebody plugs in a controller, an iPad gets
    /// a keyboard - and a platform define cannot describe that. It is also why the only
    /// existing platform branch in this project, in HapticManager, is correct: whether a device
    /// can buzz is a device fact, not a rule.
    /// </para>
    ///
    /// <para>
    /// <b>Console adapters plug in here.</b> Xbox, PlayStation and Switch each need an
    /// identity, a store, a session service and their own certification requirements, and none
    /// of those is implemented or callable without that platform's SDK. When one arrives, it
    /// contributes a capability and an identity provider through this class and through
    /// <c>Session.IMultiplayerSession</c>. It does not add a case to the ghost, the equipment
    /// or the evidence system, and if it ever needs to, the seam was drawn in the wrong place.
    /// See <c>Docs/CROSSPLAY_PLATFORM_MATRIX.md</c>.
    /// </para>
    /// </summary>
    public static class PlatformCapabilities
    {
        private static InputCapability _cached;
        private static float _cachedAt = -1f;

        /// <summary>
        /// How long a capability answer is reused, in seconds. Devices appear and disappear
        /// while the game runs, so this cannot be resolved once - and asking the input system
        /// for every device every frame is a cost for a question whose answer changes when
        /// somebody reaches behind a desk.
        /// </summary>
        private const float CacheSeconds = 1f;

        /// <summary>What this machine currently has, re-checked at most once a second.</summary>
        public static InputCapability Available
        {
            get
            {
                if (_cachedAt >= 0f && Time.unscaledTime - _cachedAt < CacheSeconds)
                    return _cached;

                _cached = Detect();
                _cachedAt = Time.unscaledTime;
                return _cached;
            }
        }

        public static bool Has(InputCapability capability) => (Available & capability) != 0;

        /// <summary>
        /// Whether the on-screen controls should be shown.
        ///
        /// <para>
        /// Touch present and no keyboard: the honest test for "this person has nothing else to
        /// press". A desktop with a touchscreen keeps its keyboard and does not get a joystick
        /// drawn over the game, and a phone with a Bluetooth keyboard attached is a case
        /// somebody will hit eventually.
        /// </para>
        /// </summary>
        public static bool WantsTouchControls =>
            Has(InputCapability.Touch) && !Has(InputCapability.KeyboardMouse);

        private static InputCapability Detect()
        {
            var found = InputCapability.None;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null)
                found |= InputCapability.Touch;

            if (UnityEngine.InputSystem.Keyboard.current != null ||
                UnityEngine.InputSystem.Mouse.current != null)
                found |= InputCapability.KeyboardMouse;

            if (UnityEngine.InputSystem.Gamepad.current != null)
                found |= InputCapability.Gamepad;
#else
            if (UnityEngine.Input.touchSupported)
                found |= InputCapability.Touch;

            if (UnityEngine.Input.mousePresent)
                found |= InputCapability.KeyboardMouse;

            // The legacy manager has no device list, so a named joystick is the only signal.
            var joysticks = UnityEngine.Input.GetJoystickNames();
            for (int i = 0; i < joysticks.Length; i++)
            {
                if (!string.IsNullOrEmpty(joysticks[i]))
                {
                    found |= InputCapability.Gamepad;
                    break;
                }
            }
#endif

            return found;
        }

        /// <summary>Forgets the cached answer. For the lab, and after a device change.</summary>
        public static void Invalidate() => _cachedAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Invalidate();
    }
}
