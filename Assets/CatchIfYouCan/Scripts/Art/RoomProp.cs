using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Puts a furniture model in the room at a real-world size, standing on the floor, with a
    /// collider you cannot walk through.
    ///
    /// <para>
    /// The model is <b>measured</b> rather than trusted. These come out of a generator that
    /// normalises everything into a two-unit box, so taken at face value a chair is two metres
    /// tall and a table is two metres wide. What is authored here is the one dimension a person
    /// would actually know - how high the back of a chair is, how high a table top is - and the
    /// rest follows from the mesh's own proportions. That also means swapping the model for a
    /// better one later needs no new numbers.
    /// </para>
    ///
    /// <para>
    /// The collider is built from the scaled bounds after the model has been turned, so it is the
    /// box the furniture actually occupies rather than the box it occupied before it was rotated.
    /// The prop's own transform stays unrotated and unscaled for exactly that reason: a rotated
    /// root would make the axis-aligned box wrong in a way that is invisible until someone walks
    /// through the arm of a chair.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Room Prop")]
    public sealed class RoomProp : MonoBehaviour
    {
        public enum FitAxis
        {
            /// <summary>Scale so the model is <see cref="targetSize"/> tall. Right for furniture.</summary>
            Height,
            /// <summary>Scale so its widest horizontal dimension is <see cref="targetSize"/>.</summary>
            Width,
            /// <summary>Scale so its longest dimension of any kind is <see cref="targetSize"/>.</summary>
            Longest
        }

        [Header("Model")]
        [SerializeField] private string modelResourcePath = "";

        [Tooltip("The dimension that is authored. Height, because that is the measurement a " +
                 "person can picture: a table top is about 0.75 m and the back of a chair about " +
                 "0.85 m, whatever else the model gets right or wrong.")]
        [SerializeField] private FitAxis fitAxis = FitAxis.Height;

        [SerializeField, Min(0.01f)] private float targetSize = 0.85f;

        [Tooltip("Turn about the vertical, in degrees. Applied to the model rather than to this " +
                 "object, so the collider below stays honest.")]
        [SerializeField] private float yawDegrees;

        [Tooltip("Sit the model on this object's Y rather than wherever its own origin happens " +
                 "to be. Generated models rarely have their origin on the floor.")]
        [SerializeField] private bool standOnFloor = true;

        [Tooltip("Centre the model horizontally on this object, for the same reason.")]
        [SerializeField] private bool centreHorizontally = true;

        [Tooltip("Nudge up or down after everything else, in metres. For models whose bounds " +
                 "include something below the part that is supposed to touch the floor - a " +
                 "threshold, a backing plane - where sitting the bounds on the floor leaves the " +
                 "visible object hovering above it.")]
        [SerializeField] private float verticalOffset;

        [Header("Collision")]
        [SerializeField] private bool addCollider = true;

        [Tooltip("Shrink the collider by this much on each horizontal side, in metres. A little " +
                 "inset stops a player brushing a cushion at arm's length and being stopped by " +
                 "air.")]
        [SerializeField, Min(0f)] private float colliderInset = 0.04f;

        [Tooltip("How much of the model's height the collider covers, from the floor up. 1 is " +
                 "the whole thing.")]
        [SerializeField, Range(0.05f, 1f)] private float colliderHeightFraction = 1f;

        [Header("Rendering")]
        [SerializeField] private bool castShadows = true;

        private Transform _model;
        private bool _built;

        /// <summary>The instantiated model, once it exists.</summary>
        public Transform Model => _model;

        /// <summary>Size the model ended up, in metres. Zero until it has been built.</summary>
        public Vector3 FittedSize { get; private set; }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            if (string.IsNullOrEmpty(modelResourcePath))
                return;

            var prefab = Resources.Load<GameObject>(modelResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[CIYC] No prop model at Resources/" + modelResourcePath +
                                 ", so " + name + " is empty.", this);
                return;
            }

            var go = Instantiate(prefab, transform);
            go.name = "Model";
            _model = go.transform;
            _model.localPosition = Vector3.zero;
            _model.localRotation = Quaternion.identity;
            _model.localScale = Vector3.one;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float source = fitAxis switch
            {
                FitAxis.Height => bounds.size.y,
                FitAxis.Width => Mathf.Max(bounds.size.x, bounds.size.z),
                _ => Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
            };

            if (source > 0.0001f)
                _model.localScale = Vector3.one * (targetSize / source);

            _model.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = true;
            }

            // Re-measured after scaling and turning: this is the box the furniture now occupies.
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            FittedSize = bounds.size;

            Vector3 shift = Vector3.zero;
            if (centreHorizontally)
            {
                shift.x = transform.position.x - bounds.center.x;
                shift.z = transform.position.z - bounds.center.z;
            }
            if (standOnFloor)
                shift.y = transform.position.y - bounds.min.y;
            shift.y += verticalOffset;

            _model.position += shift;
            bounds.center += shift;

            if (addCollider)
                AddBox(bounds);
        }

        private void AddBox(Bounds worldBounds)
        {
            var box = gameObject.GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();

            Vector3 size = worldBounds.size;
            size.x = Mathf.Max(0.02f, size.x - colliderInset * 2f);
            size.z = Mathf.Max(0.02f, size.z - colliderInset * 2f);
            size.y = Mathf.Max(0.02f, size.y * colliderHeightFraction);

            // From the floor up, so a partial-height collider hugs the ground rather than
            // floating at the model's middle.
            Vector3 centre = worldBounds.center;
            centre.y = worldBounds.min.y + size.y * 0.5f;

            box.center = transform.InverseTransformPoint(centre);
            box.size = size;
        }
    }
}
