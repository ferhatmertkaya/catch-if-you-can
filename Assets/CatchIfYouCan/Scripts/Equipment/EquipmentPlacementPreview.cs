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

            int usable = 0;
            for (int i = 0; i < filters.Length; i++)
                if (IsBodyPart(filters[i]))
                    usable++;

            if (usable == 0)
            {
                Core.CIYCLog.Warn(
                    "[CIYC][Placement] no preview for '" + name + "': " + filters.Length +
                    " mesh(es) under its visual and not one of them is part of the object. " +
                    "An effect volume is the screen area an effect is drawn on, not a shape.");
                return null;
            }

            var root = new GameObject(name);

            // new GameObject lands in the ACTIVE scene, which is not necessarily the item's:
            // while the lobby portal prepares a mission the investigation scene is loaded
            // additively with the lobby still active, so a preview built there would be
            // destroyed by the lobby unload while the item it belongs to survives (mistake 17).
            var scene = source.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

            var preview = root.AddComponent<EquipmentPlacementPreview>();
            preview.Compose(source, filters);
            preview.SetVisible(false);
            return preview;
        }

        /// <summary>
        /// Whether this mesh is part of the OBJECT rather than of an effect it draws.
        ///
        /// <para>
        /// The DOTS projector carries both: a 0.25 m casing, and an eleven-metre mesh the dot
        /// field is drawn on. Copying the second one made a preview the size of a room that
        /// followed the aim, which is the sideways-3D-render the player saw rather than a ghost
        /// of the device. <see cref="EffectVolume"/> is the same mark the lobby's measuring code
        /// asks for, so there is one answer to "how big is this thing" (mistake 42).
        /// </para>
        /// </summary>
        private static bool IsBodyPart(MeshFilter filter) =>
            filter != null && filter.sharedMesh != null &&
            !EffectVolume.Encloses(filter.transform);

        private void Compose(Transform source, MeshFilter[] filters)
        {
            var shader = CiycShaders.FindLit();
            if (shader != null)
            {
                _material = new Material(shader) { name = "EquipmentPlacementPreview_Runtime" };
                ConfigureTransparent(_material);
            }
            else
            {
                // Said out loud, and no Shader.Find("Standard") behind it. A renderer with no
                // material draws nothing, so a silent null here is a preview that was built
                // correctly and cannot be seen - the same screenshot as a preview that was never
                // built, and as one whose meshes were all effect volumes (mistake 2).
                Core.CIYCLog.Warn(
                    "[CIYC][Placement] the preview for '" + gameObject.name + "' has no " +
                    "material: CiycShaders.FindLit() resolved nothing. It will be invisible.");
            }

            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (!IsBodyPart(filter))
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
