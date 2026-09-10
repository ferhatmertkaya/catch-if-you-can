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
#if UNITY_EDITOR
        , IEditorPreviewBuildable
#endif
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

        [Tooltip("Force this material onto every renderer in the model. For models whose own " +
                 "materials cannot be relied on - a generated FBX whose material description " +
                 "points at textures that were never delivered, or a remap that quietly did not " +
                 "take - this is the difference between a prop and an invisible one. Left empty " +
                 "the model keeps whatever it imported with.")]
        [SerializeField] private Material materialOverride;

        [Tooltip("Skip every measurement and put the model at exactly this uniform scale. Zero " +
                 "means measure, which is the normal path. This is the escape hatch for a model " +
                 "whose import cannot be trusted: one number, changeable in the Inspector while " +
                 "the game runs, with nothing between it and the transform.")]
        [SerializeField, Min(0f)] private float absoluteScale;

        [Tooltip("Hide the one part of the model that wraps all the others - a door's own frame " +
                 "around its own leaf, a cabinet's shell around its drawers - and fit what is " +
                 "left. For a prop going into an opening the room already frames, this is the " +
                 "difference between one frame and two nested ones.")]
        [SerializeField] private bool hideOuterShell;

        [Tooltip("Log once what this prop actually ended up as: whether the model loaded, how " +
                 "many renderers it has, what shader they are on, and the box it occupies. On " +
                 "for anything that has been reported missing.")]
        [SerializeField] private bool logState;

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

#if UNITY_EDITOR
        /// <summary>
        /// The same Build the game runs. This prop loads a prefab and fits it - there is no
        /// runtime state in that, so the preview is not a reduced version of anything.
        /// </summary>
        void IEditorPreviewBuildable.BuildEditorPreview() => Build();

        void IEditorPreviewBuildable.ForgetEditorPreview()
        {
            _built = false;
            _model = null;
            FittedSize = Vector3.zero;
        }
#endif

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
            {
                Debug.LogError("[CIYC] " + name + " loaded Resources/" + modelResourcePath +
                               " but it has no renderers, so there is nothing to show.", this);
                return;
            }

            // Anything the importer decided was hidden is made visible again, and the material is
            // pinned where one has been given. An imported object that arrives inactive, or a
            // renderer that arrives disabled, or a material whose textures were never in the
            // delivery, all look identical from the player's side: a hole where a prop should be.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].gameObject.activeSelf)
                    renderers[i].gameObject.SetActive(true);
                renderers[i].enabled = true;

                if (materialOverride != null)
                {
                    var slots = new Material[renderers[i].sharedMaterials.Length == 0
                        ? 1
                        : renderers[i].sharedMaterials.Length];
                    for (int m = 0; m < slots.Length; m++)
                        slots[m] = materialOverride;
                    renderers[i].sharedMaterials = slots;
                }
            }

            if (hideOuterShell)
                HideOuterShell(renderers);

            // Everything from here on is about what can actually be seen. A hidden shell must not
            // decide how big the rest is scaled to, where it stands, or how big its collider is.
            Renderer[] visible = Visible(renderers);

            Bounds bounds = Measure(visible);

            if (absoluteScale > 0f)
            {
                _model.localScale = Vector3.one * absoluteScale;
                _model.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
                ApplyShadowFlags(renderers);
                Place(visible, renderers);
                return;
            }

            float source = FitDimension(bounds.size);

            // A measurement that has collapsed is the difference between a door and a door the
            // size of the room: the scale is targetSize over whatever was measured, so a
            // measurement a hundredth of what it should be is a prop a hundred times too big.
            // Anything under a twentieth of the target is not a small prop, it is a failed
            // measurement, and the whole model is measured again rather than trusted.
            if (source < targetSize * 0.05f)
            {
                Debug.LogError("[CIYC] " + name + " measured only " + source.ToString("F4") +
                               " m on its fit axis against a target of " + targetSize +
                               ". Falling back to measuring every renderer.", this);
                bounds = Measure(renderers);
                visible = renderers;
                source = FitDimension(bounds.size);
            }

            if (source > 0.0001f)
                _model.localScale = Vector3.one * (targetSize / source);

            _model.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ApplyShadowFlags(renderers);

            // Re-measured after scaling and turning: this is the box the furniture now occupies.
            bounds = Measure(visible);

            // And then corrected against itself until it is the size it was asked to be.
            //
            // <para>
            // Everything above this is an <em>estimate</em> of the scale that will produce the
            // target: measure the model, divide, apply. That estimate rests on the importer, the
            // file's units, the node the importer decided to collapse into the prefab root, and
            // this component overwriting whatever scale that root arrived with - four things
            // that have to agree, and a prop the size of the room if any one of them does not.
            // This asks none of them. It measures the object that is now standing in the scene
            // and, if that is not the size requested, multiplies by the ratio and measures again.
            // Two passes are one more than a linear scale needs; the second only ever confirms.
            // </para>
            for (int pass = 0; pass < 2; pass++)
            {
                float actual = FitDimension(bounds.size);
                if (actual < 0.0001f || Mathf.Abs(actual - targetSize) <= targetSize * 0.01f)
                    break;

                _model.localScale *= targetSize / actual;
                bounds = Measure(visible);
            }

            float fitted = FitDimension(bounds.size);
            if (Mathf.Abs(fitted - targetSize) > targetSize * 0.05f)
            {
                Debug.LogError("[CIYC] " + name + " asked for " + targetSize + " m and came out " +
                               fitted.ToString("F3") + " m (" + bounds.size.ToString("F3") +
                               ") even after correction.", this);
            }

            Place(visible, renderers);
        }

        /// <summary>
        /// Stands the prop where it belongs, boxes it, and says once what it became. Shared by
        /// the measured path and the absolute-scale one, so the two cannot drift apart.
        /// </summary>
        private void Place(Renderer[] visible, Renderer[] renderers)
        {
            Bounds bounds = Measure(visible);
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

            if (!logState)
                return;

            var shader = visible[0].sharedMaterial != null ? visible[0].sharedMaterial.shader : null;
            Debug.Log("[CIYC] " + name + ": renderers=" + renderers.Length +
                      " visible=" + visible.Length +
                      " active=" + _model.gameObject.activeInHierarchy +
                      " shader=" + (shader != null ? shader.name : "<none>") +
                      " supported=" + (shader != null && shader.isSupported) +
                      " scale=" + _model.localScale.x.ToString("F4") +
                      " size=" + FittedSize.ToString("F3") +
                      " min=" + bounds.min.ToString("F3") +
                      " max=" + bounds.max.ToString("F3"), this);
        }

        private void ApplyShadowFlags(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = true;
            }
        }

        /// <summary>
        /// Switches off the renderer whose box holds the most of the others.
        ///
        /// <para>
        /// Found by shape rather than by name, on purpose. Which child of a generated FBX is the
        /// casing and which is the leaf is a fact about the geometry, and the names - Plane.002
        /// wrapping Plane.001 wrapping Circle.002 - carry no meaning and change with every
        /// re-export. What does not change is that the piece that contains all the others is the
        /// shell around them.
        /// </para>
        /// </summary>
        private static void HideOuterShell(Renderer[] renderers)
        {
            if (renderers.Length < 2)
                return;

            int shell = -1;
            int held = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds outer = RendererBounds(renderers[i]);
                if (outer.size.sqrMagnitude < 1e-10f)
                    continue;

                // Counted by centres rather than by whole boxes. A door leaf sits inside its own
                // casing but its handle pokes out of both, so asking whether every corner is
                // contained answers "no" for the very piece the test exists to find.
                int count = 0;
                for (int j = 0; j < renderers.Length; j++)
                {
                    if (j == i)
                        continue;

                    Bounds inner = RendererBounds(renderers[j]);
                    if (inner.size.sqrMagnitude < 1e-10f)
                        continue;
                    if (outer.Contains(inner.center))
                        count++;
                }

                // Ties broken by volume, so which of two equally-containing pieces is the shell
                // is never decided by the order the renderers happened to come back in.
                if (count > held ||
                    (count == held && count > 0 && shell >= 0 &&
                     Volume(outer) > Volume(RendererBounds(renderers[shell]))))
                {
                    held = count;
                    shell = i;
                }
            }

            if (shell >= 0 && held > 0)
                renderers[shell].enabled = false;
        }

        /// <summary>The one dimension of a box that <see cref="fitAxis"/> is about.</summary>
        private float FitDimension(Vector3 size) => fitAxis switch
        {
            FitAxis.Height => size.y,
            FitAxis.Width => Mathf.Max(size.x, size.z),
            _ => Mathf.Max(size.x, Mathf.Max(size.y, size.z))
        };

        private static float Volume(Bounds bounds) =>
            bounds.size.x * bounds.size.y * bounds.size.z;

        /// <summary>The renderers still switched on, which is the prop as anyone will see it.</summary>
        private static Renderer[] Visible(Renderer[] renderers)
        {
            int count = 0;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].enabled)
                    count++;

            if (count == 0 || count == renderers.Length)
                return renderers;

            var visible = new Renderer[count];
            int at = 0;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].enabled)
                    visible[at++] = renderers[i];
            return visible;
        }

        /// <summary>
        /// The world box of every renderer together, taken from the meshes rather than from
        /// <see cref="Renderer.bounds"/> where it can be. A renderer that has not been through a
        /// frame yet - which is exactly where this runs - can report an empty box, and an empty
        /// box measured here becomes a scale of nothing and a prop left at its imported size.
        /// </summary>
        private static Bounds Measure(Renderer[] renderers)
        {
            bool any = false;
            var total = new Bounds();

            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds b = RendererBounds(renderers[i]);

                if (b.size.sqrMagnitude < 1e-10f)
                    continue;

                if (!any)
                {
                    total = b;
                    any = true;
                }
                else
                {
                    total.Encapsulate(b);
                }
            }

            if (any)
                return total;

            // Nothing measurable: fall back to whatever the renderers claim, empty or not, so the
            // caller's guards see a zero rather than a lie.
            var fallback = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                fallback.Encapsulate(renderers[i].bounds);
            return fallback;
        }

        /// <summary>
        /// One renderer's world box, from its mesh rather than from <see cref="Renderer.bounds"/>
        /// where it can be, for the reason given above.
        /// </summary>
        private static Bounds RendererBounds(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return TransformBounds(renderer.transform, filter.sharedMesh.bounds);
            return renderer.bounds;
        }

        /// <summary>The world box of a local box, from its eight corners.</summary>
        private static Bounds TransformBounds(Transform transform, Bounds local)
        {
            Vector3 c = local.center;
            Vector3 e = local.extents;
            var result = new Bounds(transform.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);

            result.Encapsulate(transform.TransformPoint(c + new Vector3(e.x, -e.y, -e.z)));
            result.Encapsulate(transform.TransformPoint(c + new Vector3(-e.x, e.y, -e.z)));
            result.Encapsulate(transform.TransformPoint(c + new Vector3(e.x, e.y, -e.z)));
            result.Encapsulate(transform.TransformPoint(c + new Vector3(-e.x, -e.y, e.z)));
            result.Encapsulate(transform.TransformPoint(c + new Vector3(e.x, -e.y, e.z)));
            result.Encapsulate(transform.TransformPoint(c + new Vector3(-e.x, e.y, e.z)));
            result.Encapsulate(transform.TransformPoint(c + new Vector3(e.x, e.y, e.z)));
            return result;
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
