using CatchIfYouCan.Input;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Builds the on-screen controls: a glass movement stick bottom-left, an invisible look area
    /// filling the right of the screen, a curved run / torch / crouch cluster bottom-right, and a
    /// small reticle at the centre.
    ///
    /// <para>
    /// The action buttons sit on the right, inside the look area. That looks like a conflict and
    /// is not: they are <see cref="TouchHoldButton"/>s, which forward their own drag to the look,
    /// so the thumb that holds one still turns the camera by sliding. The left thumb never leaves
    /// the movement stick, so the right is the only one free to press anything, and putting the
    /// buttons anywhere else would mean letting go of moving in order to run.
    /// </para>
    ///
    /// <para>
    /// The three are arranged on an arc rather than a column so that all of them are the same
    /// distance from where the thumb rests. A column puts the top button a thumb-length further
    /// away than the bottom one, which is why the torch - the one pressed most - is the large
    /// button at the near end of the arc and the other two curve away from it.
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
    /// Layout is anchored to the corner each control belongs to and expressed in
    /// reference-resolution units, then inset by <see cref="Screen.safeArea"/>, so a notch, a
    /// Dynamic Island or a rounded corner moves the controls rather than covering them. The
    /// canvas scaler matches by height, which keeps every control the same physical size whether
    /// the screen is 16:9 or 20:9 - only the empty middle grows. The reticle is deliberately
    /// outside the safe area, because it marks where the camera is pointing and the camera is
    /// centred on the screen, not on the safe rectangle.
    /// </para>
    /// </summary>
    public static class TouchHudFactory
    {
        // Landscape reference. Matching on height means wider phones get more room between the
        // thumbs rather than larger controls.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ---- palette -----------------------------------------------------------------------
        // Smoked glass: a pale tint at very low alpha rather than a dark one. Over a dark horror
        // scene a dark fill is simply invisible, so what reads as "smoked glass" here is a faint
        // lift, not a shade.
        //
        // Every alpha below is one of the brief's numbers, and they are the whole of the style:
        // button body 0.08-0.13, border 0.20-0.28, icon 0.65-0.80, stick body 0.06-0.10, knob
        // 0.12-0.18. The icon is the only thing here meant to be read at a glance, so it sits at
        // the top of its band while everything that is only there to say where to press sits at
        // the bottom of its own. Nothing is a disc of colour: at these alphas the controls are
        // an outline and a symbol, and the room shows through all of them.

        private static readonly Color Glass = new Color(0.60f, 0.66f, 0.63f, 0.07f);
        private static readonly Color GlassBorder = new Color(0.86f, 0.92f, 0.89f, 0.16f);
        private static readonly Color IconTint = new Color(0.91f, 0.94f, 0.92f, 0.58f);

        private static readonly Color StickGlass = new Color(0.55f, 0.62f, 0.58f, 0.05f);
        private static readonly Color StickBorder = new Color(0.84f, 0.90f, 0.87f, 0.13f);
        private static readonly Color KnobGlass = new Color(0.80f, 0.87f, 0.83f, 0.10f);
        private static readonly Color KnobBorder = new Color(0.90f, 0.95f, 0.92f, 0.16f);

        // Down from a third to a tenth. At the old alpha the four arrows were the loudest thing
        // on the screen and the stick read as an arcade pad; this leaves them as a hint of which
        // way the thumb may go, visible when looked at and invisible when not.
        private static readonly Color StickChevron = new Color(0.90f, 0.94f, 0.92f, 0.07f);

        private static readonly Color ReticleTint = new Color(0.88f, 0.92f, 0.90f, 0.15f);

        // Barely there, and only visible where the scene behind is bright. Against the dark it
        // does nothing, which is the point: a drop shadow that reads in a lit corridor and
        // disappears in a black room.
        private static readonly Color Shade = new Color(0f, 0f, 0f, 0.06f);

        // On is the icon, and only the icon: solid white at full opacity while everything around
        // it stays the same sheet of glass it was. No halo, no fill, no lit ring. The eye reads
        // one symbol going from faint to solid far faster than it reads a circle changing
        // colour, and it costs the HUD nothing in weight.
        private static readonly Color IconActive = new Color(1f, 1f, 1f, 1f);
        private static readonly Color GlowActive = new Color(0f, 0f, 0f, 0f);

        // The crouch latch has no icon state of its own to change - the same figure means both
        // "crouch" and "crouching" - so that one keeps a border that brightens, kept as faint as
        // it can be and still be seen.
        private static readonly Color RingActive = new Color(0.92f, 0.96f, 0.94f, 0.30f);

        // ---- layout ------------------------------------------------------------------------

        private const float StickTouchSize = 380f;   // the pad the thumb may land anywhere in
        private const float StickRingSize = 300f;    // the circle actually drawn
        private const float StickKnobSize = 118f;
        private const float StickHandleRange = 108f;
        private static readonly Vector2 StickCentre = new Vector2(300f, 300f);

        private const float FlashlightSize = 196f;
        private const float SmallButtonSize = 157f;  // 20% smaller than the torch
        private const float ClusterArcRadius = 192f;
        private const float ClusterArcDegrees = 142f;
        private static readonly Vector2 FlashlightCentre = new Vector2(-160f, 250f);

        private const float ChevronSize = 38f;
        private const float ChevronRadius = 112f;   // between the knob and the ring

        private const float IconFraction = 0.5f;
        private const float ReticleSize = 34f;

        // Above and to the right of the torch, clear of it and of the run button.
        private const float CarryButtonSize = 132f;
        private static readonly Vector2 CarryCentre = new Vector2(-118f, 452f);

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

            // Outside the safe area on purpose: it marks the middle of the camera, and the camera
            // is centred on the screen whatever the notch does to the usable rectangle.
            CreateReticle(root.GetComponent<RectTransform>());

            var safe = CreateRect("SafeArea", root.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            CreateLookArea(safe);
            var joystick = CreateJoystick(safe);
            CreateActionCluster(safe);

            // The look area is created first so it sits behind the stick and the buttons in the
            // hierarchy; the raycaster walks front to back, so a thumb on any of those never
            // reaches the look area underneath. That is exactly why the buttons have to forward
            // their own drag - the area beneath them is unreachable while they are pressed.
            MobileInputController.Instance?.BindJoystick(joystick);

            return root;
        }

        /// <summary>
        /// Makes sure something is dispatching pointer events, through the project's own helper.
        ///
        /// <para>
        /// This used to build its own EventSystem with a StandaloneInputModule, and that was
        /// wrong for this project: the player settings select the Input System, so an
        /// InputSystemUIInputModule is what should be dispatching. It happened to work because
        /// the legacy manager is also enabled, but whichever of the HUD and
        /// <see cref="EventSystemUtil"/> ran first decided which module the whole game got.
        /// </para>
        /// </summary>
        private static void EnsureEventSystem() => EventSystemUtil.EnsureEventSystem();

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
            rect.gameObject.AddComponent<LookTransparentUI>();
        }

        private static VirtualJoystick CreateJoystick(RectTransform parent)
        {
            var pad = CreateAnchored("MoveJoystick", parent, new Vector2(0f, 0f),
                                     StickCentre, new Vector2(StickTouchSize, StickTouchSize));

            // Touch target is the whole 380 px square; the ring drawn inside it is smaller, so
            // the control is easier to hit than it looks.
            var padImage = pad.gameObject.AddComponent<Image>();
            padImage.color = new Color(0f, 0f, 0f, 0f);
            padImage.raycastTarget = true;
            pad.gameObject.AddComponent<LookTransparentUI>();

            AddSprite(pad, "Shade", HudSprites.Glow, Shade, StickRingSize * 1.25f);

            var background = CreateRect("Background", pad, Half, Half);
            background.sizeDelta = new Vector2(StickRingSize, StickRingSize);
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.sprite = HudSprites.Disc;
            bgImage.color = StickGlass;
            bgImage.raycastTarget = false;

            AddSprite(background, "Border", HudSprites.Ring, StickBorder, StickRingSize);
            AddChevrons(background);

            var handle = CreateRect("Handle", background, Half, Half);
            handle.sizeDelta = new Vector2(StickKnobSize, StickKnobSize);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HudSprites.Disc;
            handleImage.color = KnobGlass;
            handleImage.raycastTarget = false;

            AddSprite(handle, "Border", HudSprites.Ring, KnobBorder, StickKnobSize);

            var joystick = pad.gameObject.AddComponent<VirtualJoystick>();
            SetPrivateField(joystick, "background", background);
            SetPrivateField(joystick, "handle", handle);
            SetPrivateField(joystick, "handleRange", StickHandleRange);
            SetPrivateField(joystick, "deadZone", 0.14f);
            return joystick;
        }

        /// <summary>
        /// Four marks around the knob, so the ring is not simply empty. Faint and small on
        /// purpose - they are a hint, not a direction pad. One sprite, rotated: a separate asset
        /// per direction would be four things to keep looking identical.
        /// </summary>
        private static void AddChevrons(RectTransform background)
        {
            for (int i = 0; i < 4; i++)
            {
                float degrees = i * 90f;                       // up, left, down, right
                float radians = (90f + degrees) * Mathf.Deg2Rad;
                var chevron = AddSprite(background, "Chevron", HudSprites.Chevron,
                                        StickChevron, ChevronSize);
                chevron.rectTransform.anchoredPosition =
                    new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ChevronRadius;
                chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }
        }

        /// <summary>
        /// The three action buttons, on an arc opening away from the corner. The torch sits at
        /// the near end and the other two are placed by angle rather than by hand, so the arc
        /// stays an arc if any of the sizes are retuned.
        /// </summary>
        private static void CreateActionCluster(RectTransform parent)
        {
            float a = ClusterArcDegrees * Mathf.Deg2Rad;
            var offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ClusterArcRadius;

            // Torch first, so its glow - which is wider than the button and reaches the other
            // two - is drawn underneath them rather than washing green over their glass.
            CreateFlashlightButton(parent, FlashlightCentre);
            CreateSprintButton(parent, FlashlightCentre + offset);
            CreateCrouchButton(parent, FlashlightCentre + new Vector2(offset.x, -offset.y));
            CreateCarryButton(parent);
        }

        private static void CreateFlashlightButton(RectTransform parent, Vector2 centre)
        {
            var button = CreateRoundButton("FlashlightButton", parent, centre, FlashlightSize,
                                           HudSprites.Flashlight,
                                           out var ring, out var icon, out var glow);

            var flashlight = button.gameObject.AddComponent<FlashlightButton>();
            SetPrivateField(flashlight, "icon", icon);
            SetPrivateField(flashlight, "ring", ring);
            SetPrivateField(flashlight, "glow", glow);
            SetPrivateField(flashlight, "iconIdle", IconTint);
            SetPrivateField(flashlight, "iconActive", IconActive);
            // Idle and active are the same colour on purpose: the ring is glass, and glass does
            // not light up because a torch is on.
            SetPrivateField(flashlight, "ringIdle", GlassBorder);
            SetPrivateField(flashlight, "ringActive", GlassBorder);
            SetPrivateField(flashlight, "glowActive", GlowActive);
        }

        private static void CreateSprintButton(RectTransform parent, Vector2 centre)
        {
            var button = CreateRoundButton("SprintButton", parent, centre, SmallButtonSize,
                                           HudSprites.Sprint, out _, out _, out _);
            button.gameObject.AddComponent<SprintButton>();
        }

        private static void CreateCrouchButton(RectTransform parent, Vector2 centre)
        {
            var button = CreateRoundButton("CrouchButton", parent, centre, SmallButtonSize,
                                           HudSprites.Crouch, out var ring, out _, out _);

            var crouch = button.gameObject.AddComponent<CrouchButton>();
            // The border carries the latch, exactly as the torch's does, so the two read as one
            // language rather than as two unrelated indicators.
            SetPrivateField(crouch, "activeIndicator", ring);
            SetPrivateField(crouch, "idleColor", GlassBorder);
            SetPrivateField(crouch, "activeColor", RingActive);
        }

        /// <summary>
        /// The take / put-down button. Built like the others and then given a CanvasGroup, because
        /// unlike them it is not always there: it fades in when there is something to do with it.
        /// </summary>
        private static void CreateCarryButton(RectTransform parent)
        {
            var button = CreateRoundButton("CarryButton", parent, CarryCentre, CarryButtonSize,
                                           HudSprites.Pickup, out var ring, out var icon, out _);

            var group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var carry = button.gameObject.AddComponent<PickupDropButton>();
            SetPrivateField(carry, "icon", icon);
            SetPrivateField(carry, "ring", ring);
            SetPrivateField(carry, "group", group);
            SetPrivateField(carry, "interactSprite", HudSprites.Interact);
            SetPrivateField(carry, "pickupSprite", HudSprites.Pickup);
            SetPrivateField(carry, "dropSprite", HudSprites.Drop);
        }

        /// <summary>
        /// One glass button: a transparent container that takes the touch, with the shadow, body,
        /// border and icon drawn inside it. The container carries the raycast rather than the
        /// glass, so the whole square is pressable and the thumb does not have to find the circle.
        /// </summary>
        private static RectTransform CreateRoundButton(string name, RectTransform parent,
                                                       Vector2 centre, float size,
                                                       Sprite iconSprite,
                                                       out Image ring, out Image icon,
                                                       out Image glow)
        {
            var button = CreateAnchored(name, parent, new Vector2(1f, 0f), centre,
                                        new Vector2(size, size));

            var hit = button.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            button.gameObject.AddComponent<LookTransparentUI>();

            // Behind everything, and transparent until something fades it up.
            glow = AddSprite(button, "Glow", HudSprites.Glow, new Color(0f, 0f, 0f, 0f), size * 1.3f);
            AddSprite(button, "Shade", HudSprites.Glow, Shade, size * 1.25f);
            AddSprite(button, "Glass", HudSprites.Disc, Glass, size);
            ring = AddSprite(button, "Border", HudSprites.Ring, GlassBorder, size);
            icon = AddSprite(button, "Icon", iconSprite, IconTint, size * IconFraction);
            return button;
        }

        private static void CreateReticle(RectTransform parent)
        {
            var rect = CreateRect("Reticle", parent, Half, Half);
            rect.sizeDelta = new Vector2(ReticleSize, ReticleSize);
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudSprites.Reticle;
            image.color = ReticleTint;
            image.raycastTarget = false;
        }

        // ---- plumbing ----------------------------------------------------------------------

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

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

        /// <summary>
        /// A fixed-size rect pinned to one corner, positioned by its own centre. Anchor and pivot
        /// are the same point, so the offset is a plain distance from that corner and the control
        /// keeps its distance from it on every aspect ratio.
        /// </summary>
        private static RectTransform CreateAnchored(string name, RectTransform parent,
                                                    Vector2 corner, Vector2 centre, Vector2 size)
        {
            var rect = CreateRect(name, parent, corner, corner);
            rect.pivot = Half;
            rect.sizeDelta = size;
            rect.anchoredPosition = centre;
            return rect;
        }

        private static Image AddSprite(RectTransform parent, string name, Sprite sprite,
                                       Color color, float size)
        {
            var rect = CreateRect(name, parent, Half, Half);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            // Nothing here is nine-sliced, and a circle cannot be; simple keeps the quad count
            // at one per element.
            image.type = Image.Type.Simple;
            return image;
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
