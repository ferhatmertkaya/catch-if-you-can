using CatchIfYouCan.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Builds the on-screen controls: a movement stick bottom-left, a run button beside it, and
    /// an invisible look area filling the right of the screen.
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

            // The look area is created first so it sits behind the stick and the button in the
            // hierarchy; the raycaster walks front to back, so a thumb on either of those never
            // reaches the look area underneath.
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

        private static void CreateSprintButton(RectTransform parent)
        {
            // Up and to the right of the stick, where the left thumb can reach without letting
            // go of it, and clear of the stick's own touch square.
            var rect = CreateRect("SprintButton", parent, Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(470f, 150f);
            rect.sizeDelta = new Vector2(150f, 150f);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonIdle;
            image.raycastTarget = true;

            var chevron = CreateRect("Accent", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            chevron.sizeDelta = new Vector2(64f, 8f);
            var chevronImage = chevron.gameObject.AddComponent<Image>();
            chevronImage.color = ButtonAccent;
            chevronImage.raycastTarget = false;

            var chevron2 = CreateRect("Accent2", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            chevron2.sizeDelta = new Vector2(44f, 8f);
            chevron2.anchoredPosition = new Vector2(0f, -18f);
            var chevron2Image = chevron2.gameObject.AddComponent<Image>();
            chevron2Image.color = new Color(ButtonAccent.r, ButtonAccent.g, ButtonAccent.b, 0.3f);
            chevron2Image.raycastTarget = false;

            rect.gameObject.AddComponent<SprintButton>();
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
