using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Keeps the local player's own body out of their first-person camera without damaging the
    /// character.
    ///
    /// <para>
    /// A first-person camera sits inside the character's head, so the head, face and hair render
    /// across the whole screen. The obvious fix — deleting the head, or the whole body — is the
    /// wrong one here: the same character has to be visible in full to other players later, and
    /// a model with its head removed cannot be that.
    /// </para>
    ///
    /// <para>
    /// Nothing is destroyed. The renderers are switched to shadows-only for the local instance,
    /// which stops them drawing for every camera on this machine while the character still casts
    /// a real shadow onto the floor — worth keeping, because a first-person player with no
    /// shadow reads as disembodied. A remote copy of the same prefab simply never has this
    /// component enabled and renders normally.
    /// </para>
    ///
    /// <para>
    /// The alternative, a camera culling mask on a dedicated layer, is left available through
    /// <see cref="hideByLayer"/> for when remote players exist and the body needs to be visible
    /// to their cameras in the same process — a split-screen or spectator view. Shadows-only is
    /// the default because it needs no layer bookkeeping and cannot be broken by another system
    /// changing a culling mask.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Local Player Body Visibility")]
    public sealed class LocalPlayerBodyVisibility : MonoBehaviour
    {
        [Tooltip("Root of the character visual. Every renderer beneath it is affected. Falls " +
                 "back to this object.")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("Hide the body from the local camera. Turn this off on a remote player's copy " +
                 "so their character is drawn in full.")]
        [SerializeField] private bool hideFromLocalCamera = true;

        [Tooltip("Keep casting shadows while hidden. A first-person player with no shadow on " +
                 "the floor looks wrong.")]
        [SerializeField] private bool keepShadow = true;

        [Tooltip("Move the renderers to a layer instead of using shadows-only. Only needed when " +
                 "another camera in the same process must still see this body.")]
        [SerializeField] private bool hideByLayer;

        [Tooltip("Layer used when hideByLayer is on. The local camera must cull it.")]
        [SerializeField] private string hiddenLayerName = "Player";

        private Renderer[] _renderers;
        private UnityEngine.Rendering.ShadowCastingMode[] _originalModes;
        private int[] _originalLayers;
        private bool _captured;

        private void Awake()
        {
            var root = visualRoot != null ? visualRoot : transform;
            _renderers = root.GetComponentsInChildren<Renderer>(true);

            _originalModes = new UnityEngine.Rendering.ShadowCastingMode[_renderers.Length];
            _originalLayers = new int[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalModes[i] = _renderers[i].shadowCastingMode;
                _originalLayers[i] = _renderers[i].gameObject.layer;
            }
            _captured = true;

            Apply();
        }

        /// <summary>Applies or lifts the local hide. Safe to call at any time.</summary>
        public void SetHiddenFromLocalCamera(bool hidden)
        {
            hideFromLocalCamera = hidden;
            Apply();
        }

        private void Apply()
        {
            if (!_captured || _renderers == null)
                return;

            int hiddenLayer = LayerMask.NameToLayer(hiddenLayerName);

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null)
                    continue;

                if (!hideFromLocalCamera)
                {
                    // Restored from what was captured, never from the current value, so
                    // toggling this repeatedly cannot walk the settings away from the prefab's.
                    r.shadowCastingMode = _originalModes[i];
                    r.gameObject.layer = _originalLayers[i];
                    r.enabled = true;
                    continue;
                }

                if (hideByLayer && hiddenLayer >= 0)
                {
                    r.gameObject.layer = hiddenLayer;
                    r.shadowCastingMode = _originalModes[i];
                }
                else
                {
                    r.shadowCastingMode = keepShadow
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : UnityEngine.Rendering.ShadowCastingMode.Off;

                    if (!keepShadow)
                        r.enabled = false;
                }
            }
        }
    }
}
