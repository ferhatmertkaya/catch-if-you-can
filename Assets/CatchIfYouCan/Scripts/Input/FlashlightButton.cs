using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// The primary action button: tap to toggle the torch, hold and drag to look around.
    ///
    /// <para>
    /// It is the largest control and it sits in the middle of the look area, which would normally
    /// make it a hole in the thing the player turns the camera with. <see cref="TouchHoldButton"/>
    /// is what stops that: the drag is forwarded to the look, so the same thumb that taps the
    /// torch also turns the view by sliding, and the button is an extension of the look surface
    /// rather than a hole in it.
    /// </para>
    ///
    /// <para>
    /// The two gestures are told apart by distance, not time. The toggle fires once, on release,
    /// and only when the thumb travelled less than the slop - so a look that begins on the button
    /// never flicks the torch, and a hold, however long, can never toggle it more than once,
    /// because there is exactly one release per press.
    /// </para>
    ///
    /// <para>
    /// It reports through <see cref="MobileInputController.PressFlashlight"/>, the same
    /// input-facing method a keyboard would call, and reads the resulting state back from
    /// <see cref="MobileInputController.FlashlightOn"/> rather than talking to the torch. The HUD
    /// stays a view onto the input layer and owns no gameplay state of its own.
    /// </para>
    /// </summary>
    public sealed class FlashlightButton : TouchHoldButton
    {
        [Header("Active state")]
        [Tooltip("The icon. Brightened and tinted towards the accent while the torch is lit.")]
        [SerializeField] private Graphic icon;

        [Tooltip("The button's border. Picks up the accent while lit.")]
        [SerializeField] private Graphic ring;

        [Tooltip("A soft disc behind the button, faded up while lit. Optional.")]
        [SerializeField] private Graphic glow;

        [SerializeField] private Color iconIdle = new Color(0.91f, 0.94f, 0.92f, 0.8f);
        [SerializeField] private Color iconActive = new Color(0.78f, 1f, 0.82f, 0.96f);
        [SerializeField] private Color ringIdle = new Color(0.86f, 0.92f, 0.89f, 0.26f);
        [SerializeField] private Color ringActive = new Color(0.34f, 1f, 0.41f, 0.55f);
        [SerializeField] private Color glowActive = new Color(0.34f, 1f, 0.41f, 0.1f);

        [Tooltip("Seconds for the active state to fade in and out. A hard switch reads as a " +
                 "UI toggle; a fade reads as a lamp.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.12f;

        private float _lit;
        private bool _wantLit;

        private void OnEnable() => Apply(_wantLit ? 1f : 0f);

        protected override void OnReleased(bool dragged)
        {
            // The thumb was looking around, not pressing. Leaving the torch alone here is the
            // whole reason the button can sit inside the look area.
            if (dragged)
                return;

            MobileInputController.Instance?.PressFlashlight();
        }

        private void Update()
        {
            var input = MobileInputController.Instance;
            _wantLit = input != null && input.FlashlightOn;

            float target = _wantLit ? 1f : 0f;
            if (Mathf.Approximately(_lit, target))
                return;

            _lit = fadeSeconds > 0f
                ? Mathf.MoveTowards(_lit, target, Time.unscaledDeltaTime / fadeSeconds)
                : target;
            Apply(_lit);
        }

        private void Apply(float lit)
        {
            _lit = lit;

            if (icon != null)
                icon.color = Color.Lerp(iconIdle, iconActive, lit);
            if (ring != null)
                ring.color = Color.Lerp(ringIdle, ringActive, lit);
            if (glow != null)
            {
                var c = glowActive;
                c.a *= lit;
                glow.color = c;
            }
        }
    }
}
