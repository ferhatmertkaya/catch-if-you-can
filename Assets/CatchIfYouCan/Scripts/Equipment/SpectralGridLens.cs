using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The green eye in the middle of the DOTS projector, lit while the device is running.
    ///
    /// <para>
    /// <b>The lens was already masked and never lit.</b> The model's material carries an
    /// <c>_EmissionMap</c> - <c>T_DOTSProjectorLevel1_Emission</c>, black everywhere except the
    /// lens - so the shape of the glow was authored long ago. What it did not carry was the
    /// <c>_EMISSION</c> shader keyword, and URP/Lit does not evaluate emission at all without it.
    /// An assigned map and a missing keyword look exactly like a model nobody painted a lens on,
    /// which is why this read as "the centre is just dark plastic" rather than as a bug.
    /// </para>
    ///
    /// <para>
    /// <b>Only the lens can glow, and that is the map's doing rather than a promise.</b> This
    /// pushes ONE colour at the whole renderer; everywhere the emission map is black, a colour
    /// multiplied by zero is still zero. So there is no way for this to light the casing even if
    /// the numbers are turned up absurdly - the mask decides where, the colour decides how much.
    /// </para>
    ///
    /// <para>
    /// <b>A property block, not a material instance.</b> Reading <c>renderer.material</c> would
    /// clone the material on first touch and leak one per projector; a block is allocated once
    /// here and written only when the power state actually changes, which for a device somebody
    /// switches on and off by hand is a handful of times a session rather than per frame.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpectralGridLens : MonoBehaviour
    {
        [Header("Off")]
        [Tooltip("What the lens emits while the device is off. Deliberately not zero: the centre " +
                 "should still read as green glass rather than as a hole. Kept well under the " +
                 "Bloom threshold the menu volume sets (0.8), so a device at rest does not glow.")]
        [SerializeField] private Color offEmission = new Color(0.015f, 0.075f, 0.025f, 1f);

        [Header("On")]
        [Tooltip("The lit colour, before the multiplier. Laser green, matching the dots the " +
                 "device throws - the same family of green, not the same number: this is a lamp " +
                 "and those are points on a wall.")]
        [SerializeField] private Color onEmission = new Color(0.10f, 1f, 0.15f, 1f);

        [Tooltip("How far above 1 the lit colour goes. Bloom in this project triggers over 0.8, " +
                 "so anything past that blooms; 4.5 puts the green channel well clear of it " +
                 "while leaving the red and blue channels below, which is what keeps the glow " +
                 "green rather than blowing out to white.")]
        [SerializeField, Range(1f, 12f)] private float onMultiplier = 4.5f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private bool _powered;
        private bool _applied;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Puts a lens on an item's visual, or hands back the one a clone already carries.
        ///
        /// <para>
        /// Every piece of equipment reaches the world as an <c>Instantiate</c> of a live
        /// template, so the component comes across on the copy while every private field that
        /// pointed at anything does not. GetComponent before AddComponent, and the renderer list
        /// is re-found lazily rather than trusted (mistakes 27, 30, 46).
        /// </para>
        /// </summary>
        public static SpectralGridLens Attach(Transform visualRoot)
        {
            if (visualRoot == null)
                return null;

            var existing = visualRoot.GetComponent<SpectralGridLens>();
            if (existing != null)
                return existing;

            return visualRoot.gameObject.AddComponent<SpectralGridLens>();
        }

        /// <summary>
        /// Lights the lens, or puts it out. Cheap enough to call on every power transition and
        /// does nothing at all when the state has not moved.
        /// </summary>
        public void SetPowered(bool powered)
        {
            if (_applied && _powered == powered)
                return;

            _powered = powered;
            _applied = true;
            Apply();
        }

        private void Apply()
        {
            EnsureRenderers();
            if (_renderers == null)
                return;

            Color emission = _powered
                ? new Color(onEmission.r * onMultiplier,
                            onEmission.g * onMultiplier,
                            onEmission.b * onMultiplier,
                            1f)
                : offEmission;

            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// The renderers that make up the device's BODY.
        ///
        /// <para>
        /// The dot field is a renderer under this same visual, and it is not part of the object -
        /// it is the effect the object throws. It is marked with <see cref="EffectVolume"/>
        /// exactly so that everything walking this hierarchy can leave it out, and writing an
        /// emission colour onto it would be harmless but would also opt its mesh out of SRP
        /// batching for a property its shader does not declare.
        /// </para>
        /// </summary>
        private void EnsureRenderers()
        {
            if (_renderers != null && _renderers.Length > 0 && _renderers[0] != null)
                return;

            var found = GetComponentsInChildren<Renderer>(true);
            int kept = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !EffectVolume.Encloses(found[i].transform))
                    found[kept++] = found[i];
            }

            System.Array.Resize(ref found, kept);
            _renderers = found;
        }
    }
}
