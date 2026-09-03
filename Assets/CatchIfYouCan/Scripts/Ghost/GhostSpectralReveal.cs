using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// Shows the ghost's shape while it stands in a spectral grid, and only while.
    ///
    /// <para>
    /// <b>It never touches the gameplay ghost's renderer.</b> It builds a presentation shell -
    /// sibling renderers sharing the ghost's own meshes and, for a skinned ghost, its own bones
    /// and root bone - and gives that shell the reveal material. The real ghost keeps its own
    /// material at all times. When the reveal ends the shell is switched off, and there is
    /// nothing to put back because nothing was changed.
    /// </para>
    ///
    /// <para>
    /// The shell follows the real ghost exactly because it is driven by the same bones. There
    /// is no second GhostController, no second AI, no second NavMeshAgent and no second
    /// transform being kept in sync - a copy that could drift is a copy that will, and a reveal
    /// showing the ghost somewhere it is not is worse than no reveal.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Ghost Spectral Reveal")]
    public sealed class GhostSpectralReveal : MonoBehaviour
    {
        [Tooltip("Seconds the shape stays up once the field stops finding it. Short: this is a " +
                 "glimpse, not a spotlight.")]
        [SerializeField, Min(0.05f)] private float holdSeconds = 0.35f;

        [Tooltip("Seconds to fade in and out.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;

        private readonly List<Renderer> _shell = new List<Renderer>();
        private Material _material;
        private MaterialPropertyBlock _block;

        private float _reveal;
        private float _holdTimer;
        private Matrix4x4 _projectorWorldToLocal = Matrix4x4.identity;
        private float _projectorRange = 6f;
        private float _projectorHalfAngle = 0.61f;
        private bool _built;

        private static readonly int ProjectorId = Shader.PropertyToID("_ProjectorWorldToLocal");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");

        /// <summary>True while the shape is visible at all.</summary>
        public bool IsRevealing => _reveal > 0.001f;

        /// <summary>Ensures a ghost has one of these, without needing the prefab to carry it.</summary>
        public static GhostSpectralReveal Ensure(GhostController ghost)
        {
            if (ghost == null)
                return null;

            var reveal = ghost.GetComponent<GhostSpectralReveal>();
            return reveal != null ? reveal : ghost.gameObject.AddComponent<GhostSpectralReveal>();
        }

        /// <summary>
        /// Called by a projector each tick that the ghost is inside its field. Not a toggle: the
        /// reveal decays on its own the moment the calls stop, so a ghost walking out of the
        /// cone fades rather than needing anyone to notice it left.
        /// </summary>
        public void Illuminate(Transform projectorHead, float range, float halfAngleRadians)
        {
            if (projectorHead == null)
                return;

            EnsureShell();
            if (_shell.Count == 0)
                return;

            _projectorWorldToLocal = projectorHead.worldToLocalMatrix;
            _projectorRange = range;
            _projectorHalfAngle = halfAngleRadians;
            _holdTimer = holdSeconds;
        }

        private void Update()
        {
            if (!_built)
                return;

            bool lit = _holdTimer > 0f;
            if (lit)
                _holdTimer -= Time.deltaTime;

            float target = lit ? 1f : 0f;
            float step = Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            float previous = _reveal;
            _reveal = Mathf.MoveTowards(_reveal, target, step);

            if (Mathf.Approximately(previous, _reveal) && _reveal <= 0f)
                return;

            bool visible = _reveal > 0.001f;
            for (int i = 0; i < _shell.Count; i++)
            {
                var renderer = _shell[i];
                if (renderer == null)
                    continue;

                if (renderer.enabled != visible)
                    renderer.enabled = visible;
            }

            if (visible)
                PushProperties();
        }

        /// <summary>
        /// Builds the shell once, on the first reveal. Deferred rather than done on Awake
        /// because a ghost that is never caught in a grid should not pay for renderers it never
        /// shows.
        /// </summary>
        private void EnsureShell()
        {
            if (_built)
                return;

            _built = true;

            var material = Resources.Load<Material>("Materials/MAT_SpectralReveal");
            if (material == null)
            {
                Core.CIYCLog.Warn("No MAT_SpectralReveal under Resources/Materials, so the " +
                                  "spectral grid cannot show the ghost. Its shader is very " +
                                  "likely not in this build either.");
                return;
            }

            _material = material;

            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
                AddSkinnedShell(skinned[i]);

            var filters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
                AddStaticShell(filters[i]);
        }

        /// <summary>
        /// A skinned copy sharing the original's mesh, bones and root bone - so it is deformed
        /// by the same animation, every frame, for free. Nothing is copied per frame and
        /// nothing can drift.
        /// </summary>
        private void AddSkinnedShell(SkinnedMeshRenderer source)
        {
            if (source == null || source.sharedMesh == null)
                return;

            var go = new GameObject("SpectralShell");
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;

            var shell = go.AddComponent<SkinnedMeshRenderer>();
            shell.sharedMesh = source.sharedMesh;
            shell.bones = source.bones;
            shell.rootBone = source.rootBone;
            shell.sharedMaterial = _material;
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.enabled = false;

            _shell.Add(shell);
        }

        private void AddStaticShell(MeshFilter source)
        {
            if (source == null || source.sharedMesh == null)
                return;

            var go = new GameObject("SpectralShell");
            go.transform.SetParent(source.transform, false);

            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;

            var shell = go.AddComponent<MeshRenderer>();
            shell.sharedMaterial = _material;
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.enabled = false;

            _shell.Add(shell);
        }

        /// <summary>
        /// Pushes the projector's frame at the shell. A property block, so several ghosts and
        /// several projectors never turn into several material instances.
        /// </summary>
        private void PushProperties()
        {
            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < _shell.Count; i++)
            {
                var renderer = _shell[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_block);
                _block.SetMatrix(ProjectorId, _projectorWorldToLocal);
                _block.SetFloat(RangeId, _projectorRange);
                _block.SetFloat(HalfAngleId, _projectorHalfAngle);
                _block.SetFloat(RevealId, _reveal);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
