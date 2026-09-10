using UnityEngine;

namespace CatchIfYouCan.Evidence
{
    /// <summary>
    /// A trace that is invisible until something shines on it: a handprint, a salt footprint.
    ///
    /// <para>
    /// It used to snap: one tick of a UV lamp swapped the material and the trace was simply
    /// there, for thirty seconds, from a single frame's worth of light. Exposure now
    /// accumulates while it is lit and decays when it is not, so sweeping a beam past
    /// something shows a hint of it and holding the beam on it brings it out. That is also
    /// what stops one frame of light being proof of anything.
    /// </para>
    ///
    /// <para>
    /// The fade is driven through the shader's own <c>_UVReveal</c> property, which
    /// <c>MAT_UVEvidence</c> has always had and nothing has ever written. A property block, so
    /// a hundred handprints share one material.
    /// </para>
    /// </summary>
    public class EvidenceReveal : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material hiddenMaterial;
        [SerializeField] private Material revealedMaterial;

        [Tooltip("How long a fully revealed trace stays up once the light leaves it, in " +
                 "seconds. Zero keeps it up for good.")]
        [SerializeField] private float revealLifetime = 30f;

        [Tooltip("Seconds of continuous light needed to bring a trace fully out.")]
        [SerializeField, Min(0.05f)] private float exposureSeconds = 1.2f;

        [Tooltip("How fast exposure bleeds away once the light is off it, as a fraction per " +
                 "second. Slower than it builds, so a sweep back and forth still works.")]
        [SerializeField, Min(0f)] private float decayPerSecond = 0.6f;

        [SerializeField] private bool hideUntilRevealed = true;

        private float _exposure;
        private float _revealTimer;
        private bool _revealed;
        private bool _litThisFrame;
        private MaterialPropertyBlock _block;

        private static readonly int UVRevealId = Shader.PropertyToID("_UVReveal");

        /// <summary>Fully brought out, and therefore worth reporting.</summary>
        public bool IsRevealed => _revealed;

        /// <summary>How far out it is, 0 to 1. What the shader is being driven with.</summary>
        public float Exposure => _exposure;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (hideUntilRevealed && targetRenderer != null && hiddenMaterial != null)
                targetRenderer.sharedMaterial = hiddenMaterial;

            ApplyExposure();
        }

        /// <summary>
        /// Called by whatever is shining on it, once per its own tick.
        /// <paramref name="deltaTime"/> is that tick, not the frame.
        /// </summary>
        public void Expose(float deltaTime)
        {
            _litThisFrame = true;

            if (_revealed)
            {
                _revealTimer = revealLifetime;
                return;
            }

            _exposure = Mathf.Clamp01(_exposure + deltaTime / Mathf.Max(0.05f, exposureSeconds));
            ApplyExposure();

            if (_exposure >= 1f)
                Reveal();
        }

        private void Update()
        {
            if (!_litThisFrame && !_revealed && _exposure > 0f)
            {
                _exposure = Mathf.Max(0f, _exposure - decayPerSecond * Time.deltaTime);
                ApplyExposure();
            }

            _litThisFrame = false;

            if (!_revealed || revealLifetime <= 0f)
                return;

            _revealTimer -= Time.deltaTime;
            if (_revealTimer <= 0f)
                Hide();
        }

        /// <summary>Brings it fully out at once. Kept for callers that place an already-visible trace.</summary>
        public void Reveal()
        {
            if (_revealed)
                return;

            _revealed = true;
            _exposure = 1f;
            _revealTimer = revealLifetime;

            if (targetRenderer != null && revealedMaterial != null)
                targetRenderer.sharedMaterial = revealedMaterial;

            ApplyExposure();
        }

        public void Hide()
        {
            _revealed = false;
            _exposure = 0f;

            if (targetRenderer != null && hiddenMaterial != null)
                targetRenderer.sharedMaterial = hiddenMaterial;

            ApplyExposure();
        }

        /// <summary>
        /// Pushes the current exposure at the shader. A property block rather than a material
        /// instance, so every trace in the house shares one material.
        /// </summary>
        private void ApplyExposure()
        {
            if (targetRenderer == null)
                return;

            _block ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_block);
            _block.SetFloat(UVRevealId, _exposure);
            targetRenderer.SetPropertyBlock(_block);
        }
    }
}
