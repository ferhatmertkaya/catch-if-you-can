using UnityEngine;

namespace CatchIfYouCan.Character
{
    /// <summary>
    /// One playable character: who they are, what they look like, and the numbers the
    /// player rig needs to be built around them.
    ///
    /// <para>
    /// Identity is the string id, not this asset. An id survives the asset being moved,
    /// renamed or replaced, fits in a save file, and is small enough to send over a wire
    /// when there is one - which is why <c>MatchConfig</c> already identifies its map the
    /// same way.
    /// </para>
    ///
    /// <para>
    /// The four metrics were <c>public const float</c> on PlayerFactory. They are here
    /// because they describe a body: a taller character has a different eye height, and a
    /// const cannot have two values. Nathan's asset must carry exactly the old numbers.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Character_", menuName = "Catch If You Can/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, lowercase, never reused. This is what a save file and a future " +
                 "join handshake carry; the asset is only how it is authored.")]
        [SerializeField] private string id = "nathan";
        [SerializeField] private string displayName = "Nathan";
        [SerializeField] private Sprite portrait;

        [Header("Content")]
        [Tooltip("The character visual. A direct reference, so it ships because something " +
                 "points at it rather than because it sits under Resources.")]
        [SerializeField] private GameObject visualPrefab;

        [Tooltip("Left empty the built-in default is used, which is Nathan's naming.")]
        [SerializeField] private CharacterRigProfile rigProfile;

        [Tooltip("Re-asserted on every renderer, because the model imports with no material " +
                 "of its own and an empty slot draws as untextured grey.")]
        [SerializeField] private Material bodyMaterial;

        [Header("Body metrics")]
        [Tooltip("Eye height above the feet. Nathan: 1.68.")]
        [SerializeField] private float eyeHeight = 1.68f;

        [Tooltip("How far forward of the spine the eyes sit. Nathan: 0.21. Without it the " +
                 "camera ends up inside the neck.")]
        [SerializeField] private float eyeForward = 0.21f;

        [Tooltip("Collision capsule height. Nathan: 1.86.")]
        [SerializeField] private float capsuleHeight = 1.86f;

        [Tooltip("Uniform scale of the visual root. Nathan: 1.04. Applied about the feet, " +
                 "which sit at the visual root's own origin.")]
        [SerializeField] private float visualScale = 1.04f;

        [Header("Availability")]
        [SerializeField] private bool unlockedByDefault = true;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public Sprite Portrait => portrait;
        public GameObject VisualPrefab => visualPrefab;
        public CharacterRigProfile RigProfile => rigProfile != null ? rigProfile : CharacterRigProfile.Default;
        public Material BodyMaterial => bodyMaterial;
        public float EyeHeight => eyeHeight;
        public float EyeForward => eyeForward;
        public float CapsuleHeight => capsuleHeight;
        public float VisualScale => visualScale;
        public bool UnlockedByDefault => unlockedByDefault;

        public bool IsUsable => !string.IsNullOrEmpty(id) && visualPrefab != null;
    }
}
