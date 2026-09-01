using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// Marks a HUD element that the mouse may rest on without the camera stopping.
    ///
    /// <para>
    /// <see cref="MobileInputController"/> suppresses free mouse-look whenever the cursor is over
    /// UI, which is right for a menu and wrong for a transparent overlay: on desktop the cursor
    /// sits in the middle-right of the screen, which is exactly where the touch controls are, so
    /// the view would freeze whenever it drifted over one of them. Hiding the HUD on desktop
    /// would fix it and cost the ability to see or click the controls while testing in the
    /// editor, so instead the controls say what they are.
    /// </para>
    ///
    /// <para>
    /// A count rather than a flag, because the pointer moving from one control to an overlapping
    /// one raises the enter before the exit, and a flag would flicker off for a frame in between.
    /// Instances decrement themselves when disabled, so a HUD switched off under the cursor
    /// cannot leave the count stuck above zero.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LookTransparentUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static int _inside;
        private bool _counted;

        /// <summary>True while the pointer is over a HUD control that should not block looking.</summary>
        public static bool PointerIsOver => _inside > 0;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_counted)
                return;

            _counted = true;
            _inside++;
        }

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (!_counted)
                return;

            _counted = false;
            _inside = Mathf.Max(0, _inside - 1);
        }
    }
}
