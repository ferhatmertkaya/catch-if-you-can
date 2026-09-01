using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Puts the character's material back on if the model arrived without one.
    ///
    /// <para>
    /// The character is a prefab variant of a prefab variant of the FBX, and none of those three
    /// carries a material: the model imports with material creation switched off, and the one
    /// material it should use is attached by a remap in the model's import settings. That remap
    /// is real, it works, and it lives in a <c>.meta</c> file — which is exactly the problem. It
    /// is a per-machine import setting, so any of a reimport, a settings reset or a fresh clone
    /// that predates it leaves every renderer with an empty slot and the character drawn in
    /// default grey. It has already happened twice.
    /// </para>
    ///
    /// <para>
    /// So the material is also held here, as an ordinary serialized reference on a prefab that is
    /// checked in. A reference cannot be lost to an import setting; if the mesh has no material
    /// this puts one on, and if the import is working this finds nothing to do and costs one loop
    /// over a handful of renderers, once, at spawn.
    /// </para>
    ///
    /// <para>
    /// It fills empty slots only. A renderer that already has a material — a different one, a
    /// better one, one an artist assigned deliberately — is left exactly as it is, so this can
    /// never quietly overwrite authored work.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Character Material Guard")]
    public sealed class CharacterMaterialGuard : MonoBehaviour
    {
        [Tooltip("Material to fall back to. Normally the character's own body material.")]
        [SerializeField] private Material bodyMaterial;

        [Tooltip("Warn when this actually has to do something. It doing something means the " +
                 "model's material remap is not applying, which is worth knowing about rather " +
                 "than silently papering over.")]
        [SerializeField] private bool warnWhenApplied = true;

        /// <summary>How many renderers had to be repaired. Zero is the healthy answer.</summary>
        public int RepairedRenderers { get; private set; }

        private void Awake()
        {
            Apply();
        }

        /// <summary>Fills any empty material slot under this object. Safe to call again.</summary>
        public void Apply()
        {
            RepairedRenderers = 0;

            if (bodyMaterial == null)
                return;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var slots = renderers[i].sharedMaterials;

                // A renderer with no slots at all still needs one; a skinned mesh always draws
                // at least one submesh.
                if (slots == null || slots.Length == 0)
                {
                    renderers[i].sharedMaterials = new[] { bodyMaterial };
                    RepairedRenderers++;
                    continue;
                }

                bool changed = false;
                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] != null)
                        continue;

                    slots[s] = bodyMaterial;
                    changed = true;
                }

                if (!changed)
                    continue;

                renderers[i].sharedMaterials = slots;
                RepairedRenderers++;
            }

            if (RepairedRenderers > 0 && warnWhenApplied)
            {
                Debug.LogWarning("[CIYC] " + RepairedRenderers + " renderer(s) on " + name +
                                 " arrived with no material and were given " + bodyMaterial.name +
                                 ". The character is drawn correctly, but the model's material " +
                                 "remap is not applying - re-run Catch If You Can > Characters > " +
                                 "Build Nathan Player Visual and commit the .fbx.meta it writes.",
                                 this);
            }
        }
    }
}
