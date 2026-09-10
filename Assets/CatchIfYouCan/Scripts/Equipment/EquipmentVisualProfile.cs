using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// What a piece of equipment looks like, as content rather than as code.
    ///
    /// <para>
    /// The flashlight owned its own appearance: a Resources path, a material path, a target
    /// length, a beam axis, bounds measurement, a capsule fallback and a colour, all as fields
    /// on the gameplay class. That worked for exactly one item. The other ten would each have
    /// needed the same hundred lines, and replacing placeholder art with a real FBX would have
    /// been a code change every time.
    /// </para>
    ///
    /// <para>
    /// <b>Swapping art must never touch gameplay.</b> Point <see cref="VisualPrefab"/> at the
    /// finished model, clear <see cref="IsDevPlaceholder"/>, and nothing else changes.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "VisualProfile", menuName = "Catch If You Can/Equipment Visual Profile")]
    public sealed class EquipmentVisualProfile : ScriptableObject
    {
        [Header("Final art")]
        [Tooltip("The finished model, as a prefab reference. This is the one that should be " +
                 "set once real art exists; everything below it is fallback.")]
        [SerializeField] private GameObject visualPrefab;

        [Header("Resources fallback")]
        [Tooltip("Resources path of a model to load when there is no prefab. The flashlight " +
                 "reaches its FBX this way because that FBX already lives under Resources.")]
        [SerializeField] private string modelResourcePath;

        [Tooltip("Resources path of a material to pin onto the loaded model. An import whose " +
                 "material remap quietly did not take is an object that is there and cannot be " +
                 "seen; loading the real one by path removes that whole class of failure.")]
        [SerializeField] private string modelMaterialPath;

        [Header("Shape")]
        [Tooltip("How long the item is in the hand, in metres. Whatever units the model was " +
                 "exported in, it is scaled to this.")]
        [SerializeField, Min(0.01f)] private float length = 0.24f;

        [Tooltip("Which way along the model its length runs, in the model's own axes as Unity " +
                 "imports them. The measured mesh is turned so this becomes the carried " +
                 "transform's +Y, which is the convention everything downstream shares.")]
        [SerializeField] private Vector3 modelForwardAxis = Vector3.up;

        [Tooltip("Size of the stand-in built when there is no model at all, in metres.")]
        [SerializeField] private Vector3 fallbackSize = new Vector3(0.052f, 0.24f, 0.052f);

        [Header("Placeholder")]
        [Tooltip("TRUE while this item has no final art. A placeholder must say so: an " +
                 "unimplemented item that looks finished is one nobody ever finishes. The " +
                 "validator refuses to call anything with this set production-ready.")]
        [SerializeField] private bool isDevPlaceholder = true;

        [Tooltip("Colour of the stand-in. Deliberately not realistic.")]
        [SerializeField] private Color placeholderColor = new Color(0.85f, 0.2f, 0.75f);

        [Tooltip("Log what the visual actually ended up being: whether the model loaded, how " +
                 "many renderers it has, what shader they are on and how big it came out.")]
        [SerializeField] private bool logState;

        public GameObject VisualPrefab => visualPrefab;
        public string ModelResourcePath => modelResourcePath;
        public string ModelMaterialPath => modelMaterialPath;
        public float Length => length;
        public Vector3 ModelForwardAxis => modelForwardAxis;
        public Vector3 FallbackSize => fallbackSize;
        public bool IsDevPlaceholder => isDevPlaceholder;
        public Color PlaceholderColor => placeholderColor;
        public bool LogState => logState;

        /// <summary>Whether this profile points at anything at all.</summary>
        public bool HasArt => visualPrefab != null || !string.IsNullOrEmpty(modelResourcePath);

        /// <summary>
        /// Fills in a profile built at runtime for something that has no authored art yet.
        ///
        /// <para>
        /// A method rather than public fields: the fields stay private and inspector-authored,
        /// which is what keeps an authored profile the source of truth. Callers that need a
        /// placeholder say what shape and colour it is and nothing else, and swapping in real
        /// art is still assigning <see cref="VisualPrefab"/> to an authored asset.
        /// </para>
        /// </summary>
        public void ApplyDevPlaceholder(Vector3 size, Color tint)
        {
            fallbackSize = size;
            length = Mathf.Max(0.01f, size.y);
            placeholderColor = tint;
            isDevPlaceholder = true;
        }

        /// <summary>
        /// Fills in a profile for an item whose art IS in the project, from code.
        ///
        /// <para>
        /// <b>This is the call that was missing.</b> The authored profiles live under
        /// <c>Definitions/Equipment/Visual</c>, which is not a Resources folder, so nothing at
        /// runtime can load them - and <see cref="EquipmentDefinitionFactory"/>, which is the
        /// live source of every definition in the game, never set <c>VisualProfile</c> at all.
        /// So every item reached <see cref="EquipmentVisualFactory"/> with a null profile and
        /// got the placeholder capsule, including the flashlight, whose finished model has been
        /// sitting in <c>Resources/Props</c> the whole time.
        /// </para>
        ///
        /// <para>
        /// A method rather than public fields, for the same reason as
        /// <see cref="ApplyDevPlaceholder"/>: the fields stay inspector-authored, and an
        /// authored asset remains the source of truth wherever one can actually be reached.
        /// </para>
        /// </summary>
        public void ApplyModel(string resourcePath, string materialPath, float lengthMetres,
                               Vector3 forwardAxis)
        {
            modelResourcePath = resourcePath;
            modelMaterialPath = materialPath;
            length = Mathf.Max(0.01f, lengthMetres);
            modelForwardAxis = forwardAxis.sqrMagnitude < 0.0001f ? Vector3.up : forwardAxis;
            isDevPlaceholder = false;
            logState = true;
        }

        private static EquipmentVisualProfile _fallback;

        /// <summary>
        /// What an item with no profile gets: a placeholder box, and an honest one. Used when a
        /// definition has not been given a visual profile yet, so the item is visibly
        /// unfinished rather than invisible.
        /// </summary>
        public static EquipmentVisualProfile Fallback
        {
            get
            {
                if (_fallback != null)
                    return _fallback;

                _fallback = CreateInstance<EquipmentVisualProfile>();
                _fallback.name = "VisualProfile_Fallback";
                _fallback.hideFlags = HideFlags.HideAndDontSave;
                _fallback.fallbackSize = new Vector3(0.12f, 0.12f, 0.12f);
                _fallback.length = 0.12f;
                return _fallback;
            }
        }
    }
}
