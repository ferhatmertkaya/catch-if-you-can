using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Decides what the local player sees of their own character.
    ///
    /// <para>
    /// A first-person camera sits inside the character's head, so without help the face, teeth
    /// and the inside of the skull fill the screen. The obvious fixes are both wrong here:
    /// deleting the head damages a model that other players must later see whole, and hiding the
    /// whole renderer leaves the player a floating camera with no legs to look down at.
    /// </para>
    ///
    /// <para>
    /// Nathan is one skinned mesh with one material — checked, not assumed — so there is no head
    /// renderer to switch off and no head submesh to skip. What there is, is a head bone. Scaling
    /// that bone to nothing collapses the skull, jaw, eyes and hair into a point at the top of the
    /// neck, and leaves every other bone untouched. The body below the collar renders exactly as
    /// authored, which is what puts a chest, hips, legs and shoes under the camera when the player
    /// looks down.
    /// </para>
    ///
    /// <para>
    /// This is a local visibility behaviour and nothing else. It scales a bone on one instance at
    /// runtime; the shared prefab, the mesh and the skeleton are untouched, so a remote copy of
    /// the same character simply never has this component enabled and draws in full.
    /// </para>
    ///
    /// <para>
    /// The one thing it costs is the head in the shadow. With the bone collapsed the silhouette on
    /// the floor is headless. That is the accepted trade in every true-first-person game that does
    /// this; <see cref="BodyMode.ShadowsOnlyBody"/> is kept for anyone who would rather have a
    /// complete shadow and no visible body.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Local Player Body Visibility")]
    public sealed class LocalPlayerBodyVisibility : MonoBehaviour
    {
        public enum BodyMode
        {
            /// <summary>Body visible, head bone collapsed. The player can look down at themselves.</summary>
            FirstPersonBody,

            /// <summary>Nothing drawn but the shadow. No visible body, complete silhouette.</summary>
            ShadowsOnlyBody,

            /// <summary>Everything drawn, head included. For a remote player's copy.</summary>
            FullBody
        }

        [Tooltip("Root of the character visual. Falls back to this object.")]
        [SerializeField] private Transform visualRoot;

        [SerializeField] private BodyMode mode = BodyMode.FirstPersonBody;

        [Header("First person body")]
        [Tooltip("Bone whose subtree is collapsed out of view. Matched by name suffix, so it " +
                 "survives the model's long prefixed bone names.")]
        [SerializeField] private string headBoneSuffix = "_head";

        [Tooltip("Not quite zero. An exactly zero scale produces a degenerate matrix and can " +
                 "push NaNs through the skinning.")]
        [SerializeField] private float collapsedScale = 0.0001f;

        [Header("Shadows only")]
        [Tooltip("Keep casting a shadow while hidden. A player with no shadow reads as disembodied.")]
        [SerializeField] private bool keepShadow = true;

        private Renderer[] _renderers;
        private UnityEngine.Rendering.ShadowCastingMode[] _originalModes;
        private Transform _headBone;
        private Vector3 _headBoneScale = Vector3.one;
        private bool _captured;

        /// <summary>Which body the local camera is looking at. Safe to change at runtime.</summary>
        public BodyMode Mode
        {
            get => mode;
            set { mode = value; Apply(); }
        }

        /// <summary>The collapsed head bone, or null when nothing matched.</summary>
        public Transform HeadBone => _headBone;

        private void Awake()
        {
            Capture();
            Apply();
        }

        private void OnDisable()
        {
            // Put the character back the way it was found, so an instance that stops being the
            // local player is immediately drawable in full.
            Restore();
        }

        private void Capture()
        {
            if (_captured)
                return;

            var root = visualRoot != null ? visualRoot : transform;
            _renderers = root.GetComponentsInChildren<Renderer>(true);

            _originalModes = new UnityEngine.Rendering.ShadowCastingMode[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalModes[i] = _renderers[i].shadowCastingMode;

            _headBone = FindHeadBone(root);
            if (_headBone != null)
                _headBoneScale = _headBone.localScale;

            _captured = true;
        }

        /// <summary>
        /// Finds the head by name suffix, preferring the shallowest match so a bone called
        /// something like "head_end" further down cannot win over the head itself.
        /// </summary>
        private Transform FindHeadBone(Transform root)
        {
            if (string.IsNullOrEmpty(headBoneSuffix))
                return null;

            Transform best = null;
            int bestDepth = int.MaxValue;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].name.EndsWith(headBoneSuffix, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                int depth = 0;
                for (var t = all[i]; t != null && t != root; t = t.parent)
                    depth++;

                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    best = all[i];
                }
            }

            return best;
        }

        private void Apply()
        {
            if (!_captured || _renderers == null)
                return;

            bool visible = mode != BodyMode.ShadowsOnlyBody;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null)
                    continue;

                if (visible)
                {
                    r.enabled = true;
                    r.shadowCastingMode = _originalModes[i];
                }
                else
                {
                    r.shadowCastingMode = keepShadow
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.enabled = keepShadow;
                }
            }

            if (_headBone != null)
            {
                _headBone.localScale = mode == BodyMode.FirstPersonBody
                    ? Vector3.one * collapsedScale
                    : _headBoneScale;
            }
        }

        private void Restore()
        {
            if (!_captured)
                return;

            if (_headBone != null)
                _headBone.localScale = _headBoneScale;

            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;

                _renderers[i].enabled = true;
                _renderers[i].shadowCastingMode = _originalModes[i];
            }
        }

        /// <summary>Kept for existing callers: true hides the body, false draws it in full.</summary>
        public void SetHiddenFromLocalCamera(bool hidden)
        {
            Mode = hidden ? BodyMode.FirstPersonBody : BodyMode.FullBody;
        }
    }
}
