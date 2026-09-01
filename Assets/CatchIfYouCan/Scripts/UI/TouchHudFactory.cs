using CatchIfYouCan.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Builds the on-screen controls: a movement stick bottom-left, an invisible look area
    /// filling the right of the screen, and the run and crouch buttons stacked at the right edge
    /// on top of it.
    ///
    /// <para>
    /// The action buttons are on the right, with the look. That looks like a conflict and is not:
    /// they are <see cref="TouchHoldButton"/>s, which forward their own drag to the look, so the
    /// thumb that holds run also turns the camera by sliding. The left thumb never leaves the
    /// movement stick, so the right is the only one free to press anything, and putting the
    /// buttons anywhere else would mean letting go of moving in order to run.
    /// </para>
    ///
    /// <para>
    /// Built in code rather than authored as a prefab, for the same reason the player is: it has
    /// to exist only after the menu hands over, and constructing it on demand means there is no
    /// dormant canvas sitting in the scene during the cinematic, no prefab whose references can
    /// drift, and nothing to remember to switch off.
    /// </para>
    ///
    /// <para>
    /// Layout is anchored and expressed in reference-resolution units, then inset by
    /// <see cref="Screen.safeArea"/>, so a notch, a Dynamic Island or a rounded corner moves the
    /// controls rather than covering them. The canvas scaler matches by height, which keeps the
    /// stick the same physical size whether the screen is 16:9 or 20:9 — only the empty middle
    /// grows.
    /// </para>
    /// </summary>
    public static class TouchHudFactory
    {
        // Landscape reference. Matching on height means wider phones get more room between the
        // thumbs rather than larger controls.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly Color StickBackground = new Color(0.06f, 0.09f, 0.08f, 0.28f);
        private static readonly Color StickHandle = new Color(0.55f, 0.78f, 0.66f, 0.42f);
        private static readonly Color ButtonIdle = new Color(0.08f, 0.12f, 0.10f, 0.34f);
        private static readonly Color ButtonAccent = new Color(0.55f, 0.78f, 0.66f, 0.55f);
        private static readonly Color CrouchIdle = new Color(0.55f, 0.78f, 0.66f, 0.22f);
        private static readonly Color CrouchActive = new Color(0.66f, 0.88f, 0.74f, 0.85f);

        /// <summary>
        /// Creates the HUD and hands back its root. The caller owns when it is shown; nothing
        /// here switches itself on.
        /// </summary>
        public static GameObject Create()
        {
            EnsureEventSystem();

            var root = new GameObject("TouchHUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the transition fade (500) so the controls never appear over a black screen.
            canvas.sortingOrder = 200;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            root.AddComponent<GraphicRaycaster>();

            var safe = CreateRect("SafeArea", root.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            CreateLookArea(safe);
            var joystick = CreateJoystick(safe);
            CreateSprintButton(safe);
            CreateCrouchButton(safe);

            // The look area is created first so it sits behind the stick and the buttons in the
            // hierarchy; the raycaster walks front to back, so a thumb on any of those never
            // reaches the look area underneath. That is exactly why the buttons have to forward
            // their own drag - the area beneath them is unreachable while they are pressed.
            MobileInputController.Instance?.BindJoystick(joystick);

            return root;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ---- pieces ------------------------------------------------------------------------

        private static void CreateLookArea(RectTransform parent)
        {
            // The right 55% of the screen. Wide on purpose: the thumb should be able to land
            // anywhere comfortable rather than hunting for a pad.
            var rect = CreateRect("LookArea", parent, new Vector2(0.45f, 0f), Vector2.one);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);   // invisible, but still raycastable
            image.raycastTarget = true;

            rect.gameObject.AddComponent<TouchLookArea>();
        }

        private static VirtualJoystick CreateJoystick(RectTransform parent)
        {
            var rect = CreateRect("MoveJoystick", parent, Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(150f, 140f);
            rect.sizeDelta = new Vector2(300f, 300f);

            // Touch target is the whole 300 px square; the ring drawn inside it is smaller, so
            // the control is easier to hit than it looks.
            var pad = rect.gameObject.AddComponent<Image>();
            pad.color = new Color(0f, 0f, 0f, 0f);
            pad.raycastTarget = true;

            var background = CreateRect("Background", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            background.sizeDelta = new Vector2(220f, 220f);
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.color = StickBackground;
            bgImage.raycastTarget = false;

            var handle = CreateRect("Handle", background, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            handle.sizeDelta = new Vector2(92f, 92f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = StickHandle;
            handleImage.raycastTarget = false;

            var joystick = rect.gameObject.AddComponent<VirtualJoystick>();
            SetPrivateField(joystick, "background", background);
            SetPrivateField(joystick, "handle", handle);
            SetPrivateField(joystick, "handleRange", 96f);
            SetPrivateField(joystick, "deadZone", 0.14f);
            return joystick;
        }

        // Right edge, stacked. Sized and spaced for a thumb that also has to reach the whole
        // look area around them: 160 px at the 1080-high reference is about 11 mm on a phone.
        private const float ButtonSize = 160f;
        private const float ButtonEdgeInset = -130f;

        private static void CreateSprintButton(RectTransform parent)
        {
            var rect = CreateRightEdgeButton("SprintButton", parent, 320f);

            // Two chevrons, the upper one solid: reads as "faster" without a word of text.
            AddBar(rect, new Vector2(66f, 9f), new Vector2(0f, 10f), ButtonAccent);
            AddBar(rect, new Vector2(44f, 9f), new Vector2(0f, -10f),
                   new Color(ButtonAccent.r, ButtonAccent.g, ButtonAccent.b, 0.3f));

            rect.gameObject.AddComponent<SprintButton>();
        }

        private static void CreateCrouchButton(RectTransform parent)
        {
            var rect = CreateRightEdgeButton("CrouchButton", parent, 130f);

            // A lid over a floor. The lid brightens while crouched, so the latch is readable at
            // a glance - a toggle with no visible state is a toggle nobody trusts.
            var lid = AddBar(rect, new Vector2(62f, 9f), new Vector2(0f, 12f), CrouchIdle);
            AddBar(rect, new Vector2(62f, 5f), new Vector2(0f, -20f),
                   new Color(ButtonAccent.r, ButtonAccent.g, ButtonAccent.b, 0.28f));

            var crouch = rect.gameObject.AddComponent<CrouchButton>();
            SetPrivateField(crouch, "activeIndicator", lid);
            SetPrivateField(crouch, "idleColor", CrouchIdle);
            SetPrivateField(crouch, "activeColor", CrouchActive);
        }

        private static RectTransform CreateRightEdgeButton(string name, RectTransform parent, float y)
        {
            // Anchored to the bottom-right corner rather than positioned from the left, so the
            // buttons stay under the thumb on any aspect ratio instead of drifting inward as the
            // screen gets wider.
            var rect = CreateRect(name, parent, new Vector2(1f, 0f), new Vector2(1f, 0f));
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(ButtonEdgeInset, y);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonIdle;
            image.raycastTarget = true;
            return rect;
        }

        private static Image AddBar(RectTransform parent, Vector2 size, Vector2 offset, Color color)
        {
            var bar = CreateRect("Accent", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            bar.sizeDelta = size;
            bar.anchoredPosition = offset;

            var image = bar.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
