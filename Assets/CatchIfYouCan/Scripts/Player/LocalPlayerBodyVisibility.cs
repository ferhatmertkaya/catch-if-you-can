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
    /// renderer to switch off and no head submesh to skip. What there is, is a head bone. Shrinking
    /// that bone takes the skull, jaw, eyes and hair down with it and leaves every other bone
    /// untouched. That is structural rather than a matter of tuning: the head bone's descendants
    /// are <c>head_end</c>, <c>jaw</c>, <c>eye</c>, <c>eyelid</c>, <c>eyebrow</c> and
    /// <c>mouth</c> and nothing else — <c>neck</c>, <c>shoulder_l/r</c>, <c>spine_01..03</c> and
    /// the arms hang off the spine, not off the head, so no amount of scaling here can reach the
    /// torso, shoulders, arms, waist or legs.
    /// </para>
    ///
    /// <para>
    /// <b>It is shrunk, not erased, and that distinction is the whole fix.</b> This used to scale
    /// the head to 0.0001, which is effectively a point. The head is what caps the neck: the
    /// vertices forming the neck's top rim are weighted to the head bone, so collapsing it to a
    /// point drew that rim down to a needle and left the neck and shirt collar standing open. The
    /// reported symptom — a large open neck and collar when looking down — was that, not a missing
    /// renderer. At <see cref="collapsedScale"/> 0.2 the rim barely moves, so the neck keeps its
    /// real diameter and stays capped, while the visible head is about 5 cm tall: far too small to
    /// read as a face, and small enough that a camera sitting at the head bone would clip the
    /// whole of it inside the 5 cm near plane.
    /// </para>
    ///
    /// <para>
    /// This is a local visibility behaviour and nothing else. It scales a bone on one instance at
    /// runtime; the shared prefab, the mesh and the skeleton are untouched, so a remote copy of
    /// the same character simply never has this component enabled and draws in full.
    /// </para>
    ///
    /// <para>
    /// It also keeps the shadow honest. Skinned meshes are only re-deformed when their renderer
    /// passes visibility culling, so a first-person body — culled about as often as not, since
    /// the camera is inside its bounds looking away from it — would otherwise cast a shadow
    /// frozen in whatever pose it was last skinned in. <c>updateWhenOffscreen</c> is set here
    /// rather than in the character build so it applies to every instance without the generated
    /// prefab having to be rebuilt for it.
    /// </para>
    ///
    /// <para>
    /// The one thing it costs is the head in the shadow. Bone scale is skeleton state and the
    /// skeleton is shared, so shrinking the head bone shrinks it for the shadow too: the
    /// silhouette on the floor has a small head. That is the accepted trade in every true-first-person game that does
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

        [Tooltip("How much of the head is left. Not zero: the head is what caps the neck, so " +
                 "collapsing it to a point opens the neck instead of closing it. 0.2 leaves a " +
                 "plug about 5 cm tall that holds the neck's top rim at very nearly its real " +
                 "diameter, which is what makes the collar read as a collar. Raise it if the " +
                 "neck still gapes; lower it if a shrunken head ever comes into view.")]
        [SerializeField, Range(0.02f, 0.6f)] private float collapsedScale = 0.2f;

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

        /// <summary>
        /// Holds the head collapsed against the Animator.
        ///
        /// <para>
        /// Applying it once in Awake was enough only while the character was standing still. The
        /// Animator writes bone transforms every frame, in its own pass after Update, and a clip
        /// exported from Maya routinely carries a scale curve for every joint it touches. If the
        /// walk clip carries one for the head, the first frame of animation puts the face back
        /// directly in front of the camera. Re-asserting it in LateUpdate is a comparison per
        /// frame and a write only when something else has moved it.
        /// </para>
        /// </summary>
        private void LateUpdate()
        {
            if (mode != BodyMode.FirstPersonBody || _headBone == null)
                return;

            if (_headBone.localScale.x != collapsedScale)
                _headBone.localScale = Vector3.one * collapsedScale;
        }

        /// <summary>
        /// Below this the head is a point rather than a plug, and the neck stands open.
        /// </summary>
        private const float MinimumUsefulScale = 0.02f;

        private const float DefaultCollapsedScale = 0.2f;

        private void Capture()
        {
            if (_captured)
                return;

            // Player_CharacterVisual.prefab is generated by an editor step, and one generated
            // before this was a plug rather than a point carries a serialized 0.0001 that a
            // changed C# default cannot reach. Anything that small is the old sentinel rather
            // than a deliberate setting, and it is exactly what left the neck open.
            if (collapsedScale < MinimumUsefulScale)
            {
                Debug.Log("[CIYC] LocalPlayerBodyVisibility: collapsedScale was " + collapsedScale +
                          ", which collapses the head to a point and leaves the neck and collar " +
                          "open. Using " + DefaultCollapsedScale + " instead. Set it on " +
                          "Resources/Characters/Player_CharacterVisual to make this explicit.",
                          this);
                collapsedScale = DefaultCollapsedScale;
            }

            var root = visualRoot != null ? visualRoot : transform;
            _renderers = root.GetComponentsInChildren<Renderer>(true);

            _originalModes = new UnityEngine.Rendering.ShadowCastingMode[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalModes[i] = _renderers[i].shadowCastingMode;

                // The other half of a problem that was only half fixed. The character build sets
                // Animator.cullingMode to AlwaysAnimate so the bones keep moving while the body
                // is culled — but moving the bones and deforming the mesh to match are two
                // different stages, and the second one is gated on the renderer being visible.
                // When it is skipped the skinned vertex buffer keeps last frame's pose, and the
                // shadow pass draws exactly that: a walking skeleton casting a standing shadow.
                //
                // A first-person body is the worst case for it. The bounds used for that
                // visibility test are the mesh's bind-pose bounds, and this bind pose is a
                // T-pose, so they are a 1.9 m box the camera is sitting inside and mostly
                // looking away from. Whether the body counts as visible from in there flips
                // about, which is why the shadow freezes rather than never animating at all.
                //
                // Cost: skinning and a bounds recompute every frame instead of only when
                // visible. This mesh is 10,828 vertices with at most four influences each, one
                // character, once per frame — small, and the only way to keep a shadow correct
                // while the thing casting it is culled.
                if (_renderers[i] is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }

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
