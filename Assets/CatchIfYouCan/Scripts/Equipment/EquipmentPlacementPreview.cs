using System.Collections.Generic;
using CatchIfYouCan.Art;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The translucent copy of an item shown where it would land.
    ///
    /// <para>
    /// Built once from the item's own carried visual, so the preview is the shape of the thing
    /// being placed rather than a generic marker - and so an item whose art is later swapped
    /// gets a preview of the new art for free. It shares the item's meshes and owns one
    /// material; there is no per-frame allocation and nothing is created or destroyed while it
    /// follows the aim.
    /// </para>
    ///
    /// <para>
    /// It is presentation and nothing else: no colliders, no gameplay components, no
    /// projection, no audio. What it shows is where the real item would go, and it is white
    /// rather than coloured because it is a shadow of the object, not a signal in its own right.
    /// </para>
    /// </summary>
    public sealed class EquipmentPlacementPreview : MonoBehaviour
    {
        [Tooltip("Tint while the spot is legal. Kept close to white and translucent - this is " +
                 "a shadow of the object, not a neon marker.")]
        [SerializeField] private Color validTint = new Color(1f, 1f, 1f, 0.34f);

        [Tooltip("Tint while the spot is refused. Distinguishable without being an arcade red.")]
        [SerializeField] private Color invalidTint = new Color(0.85f, 0.42f, 0.42f, 0.28f);

        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private Material _material;
        private MaterialPropertyBlock _block;
        private bool _lastValid = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Builds a preview shell from an item's visual. Returns null when there is nothing to
        /// copy, which is honest: a preview of nothing would be a preview that lies.
        /// </summary>
        public static EquipmentPlacementPreview Build(Transform source, string name)
        {
            if (source == null)
                return null;

            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
                return null;

            var root = new GameObject(name);
            var preview = root.AddComponent<EquipmentPlacementPreview>();
            preview.Compose(source, filters);
            preview.SetVisible(false);
            return preview;
        }

        private void Compose(Transform source, MeshFilter[] filters)
        {
            var shader = CiycShaders.FindLit();
            if (shader != null)
            {
                _material = new Material(shader) { name = "EquipmentPlacementPreview_Runtime" };
                ConfigureTransparent(_material);
            }

            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var piece = new GameObject("PreviewPiece");
                piece.transform.SetParent(transform, false);

                // The copy sits where the original sits relative to the item, so the preview is
                // the item's silhouette rather than a box around it.
                piece.transform.localPosition = source.InverseTransformPoint(filter.transform.position);
                piece.transform.localRotation =
                    Quaternion.Inverse(source.rotation) * filter.transform.rotation;
                piece.transform.localScale = filter.transform.lossyScale;

                piece.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _renderers.Add(renderer);
            }

            ApplyTint(true);
        }

        /// <summary>Moves the preview to a candidate and says whether that candidate is legal.</summary>
        public void Show(Vector3 position, Quaternion rotation, bool valid)
        {
            transform.SetPositionAndRotation(position, rotation);
            SetVisible(true);

            if (valid != _lastValid)
            {
                _lastValid = valid;
                ApplyTint(valid);
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        /// <summary>
        /// Through a property block. Writing Renderer.material here would instantiate a material
        /// every time the aim crossed a doorway.
        /// </summary>
        private void ApplyTint(bool valid)
        {
            _block ??= new MaterialPropertyBlock();
            Color tint = valid ? validTint : invalidTint;

            for (int i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                _block.SetColor(ColorId, tint);
                renderer.SetPropertyBlock(_block);
            }
        }

        private static void ConfigureTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
