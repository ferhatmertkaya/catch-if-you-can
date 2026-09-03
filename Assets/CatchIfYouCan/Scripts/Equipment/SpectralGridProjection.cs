using CatchIfYouCan.Art;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Draws the projector's field of points: one mesh, one material, no dots as objects.
    ///
    /// <para>
    /// The whole effect is a single cone rendered with
    /// <c>CatchIfYouCan/SpectralGrid</c>. The points are computed in the fragment shader at the
    /// world position of whatever surface is behind each pixel, so there is no dot anywhere in
    /// the scene graph: no GameObject per dot, no MonoBehaviour per dot, nothing instantiated
    /// or destroyed while it runs, and nothing to replicate over a network later but the
    /// projector's own transform and whether it is on.
    /// </para>
    ///
    /// <para>
    /// The cone is built once and reused. Range and angle are pushed through a property block
    /// rather than a material instance, so twenty projectors in a house share one material.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Spectral Grid Projection")]
    public sealed class SpectralGridProjection : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Color dotColor = new Color(0.2f, 1f, 0.35f, 1f);

        [Tooltip("How many points across the cone. Angular, so they spread with distance the " +
                 "way a real projector's do.")]
        [SerializeField, Range(4f, 128f)] private float density = 34f;

        [Tooltip("Size of one point within its cell, 0 to 0.5. Past about 0.35 they merge into " +
                 "the continuous green floodlight this is not supposed to be.")]
        [SerializeField, Range(0.02f, 0.45f)] private float dotSize = 0.22f;

        [SerializeField, Range(0f, 8f)] private float intensity = 2.2f;

        [Tooltip("How soft the edge of the cone is, as a fraction of its radius.")]
        [SerializeField, Range(0.01f, 0.9f)] private float edgeSoftness = 0.35f;

        [Tooltip("Metres over which the field fades in at the lens, so standing on the " +
                 "projector is not a wall of light.")]
        [SerializeField, Range(0f, 2f)] private float nearFade = 0.25f;

        [Header("Mesh")]
        [Tooltip("Sides on the cone hull. This is the volume the shader runs inside, not the " +
                 "shape you see, so it needs to be round enough not to clip the field - not " +
                 "smooth.")]
        [SerializeField, Range(8, 48)] private int coneSides = 20;

        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MaterialPropertyBlock _block;
        private Mesh _cone;

        private float _range = 6f;
        private float _fullAngle = 70f;
        private int _builtSides = -1;

        private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int DotSizeId = Shader.PropertyToID("_DotSize");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int NearFadeId = Shader.PropertyToID("_NearFade");

        /// <summary>
        /// Attaches a projection to a device head. The volume is a child, so it inherits the
        /// device's orientation and a wall-mounted projector throws into the room without
        /// anything having to work out which way that is.
        /// </summary>
        public static SpectralGridProjection Attach(Transform head)
        {
            if (head == null)
                return null;

            var go = new GameObject("SpectralGridProjection");
            go.transform.SetParent(head, false);
            return go.AddComponent<SpectralGridProjection>();
        }

        private void Awake()
        {
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // The authored material, so its shader is referenced by an asset and survives a
            // build. Asking Shader.Find for it directly is how a shader gets stripped and the
            // effect quietly becomes nothing on a device.
            var material = Resources.Load<Material>("Materials/MAT_SpectralGrid");
            if (material == null)
            {
                Core.CIYCLog.Warn("No MAT_SpectralGrid under Resources/Materials; the spectral " +
                                  "grid will not draw. Its shader is very likely not in this " +
                                  "build either.");
            }

            _renderer.sharedMaterial = material;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        /// <summary>Sets the shape of the field. Cheap; safe to call whenever it changes.</summary>
        public void Configure(float range, float fullAngleDegrees)
        {
            _range = Mathf.Max(0.1f, range);
            _fullAngle = Mathf.Clamp(fullAngleDegrees, 5f, 170f);

            RebuildCone();
            PushProperties();
        }

        /// <summary>
        /// Turns the field on and off. Everything expensive is behind this: a projector that is
        /// switched off, stowed or in a bag renders nothing at all.
        /// </summary>
        public void SetRunning(bool running)
        {
            if (_renderer != null)
                _renderer.enabled = running && _renderer.sharedMaterial != null;
        }

        /// <summary>
        /// The hull the shader runs inside. Rebuilt only when its shape actually changes, which
        /// in practice is once.
        /// </summary>
        private void RebuildCone()
        {
            float halfAngle = _fullAngle * 0.5f * Mathf.Deg2Rad;
            float radius = Mathf.Tan(halfAngle) * _range;

            if (_cone != null && _builtSides == coneSides)
            {
                // Same topology, different size: scale rather than rebuild the mesh.
                transform.localScale = Vector3.one;
                ResizeCone(radius, _range);
                return;
            }

            _builtSides = coneSides;
            _cone = new Mesh { name = "SpectralGridCone" };
            // Rebuilt only on a shape change, never per frame, so this allocation happens once.
            _cone.MarkDynamic();
            BuildCone(_cone, coneSides, radius, _range);
            _filter.sharedMesh = _cone;
        }

        private void ResizeCone(float radius, float height)
        {
            BuildCone(_cone, _builtSides, radius, height);
            _filter.sharedMesh = _cone;
        }

        /// <summary>
        /// A cone from the origin along +Y - the axis every carried item in this project works
        /// along - closed with a cap so the volume is watertight and the shader's front-face
        /// cull cannot leak.
        /// </summary>
        private static void BuildCone(Mesh mesh, int sides, float radius, float height)
        {
            int rim = Mathf.Max(3, sides);
            var vertices = new Vector3[rim + 2];
            vertices[0] = Vector3.zero;                       // apex, at the lens
            vertices[rim + 1] = new Vector3(0f, height, 0f);  // centre of the far cap

            for (int i = 0; i < rim; i++)
            {
                float a = i / (float)rim * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            }

            var triangles = new int[rim * 6];
            int t = 0;
            for (int i = 0; i < rim; i++)
            {
                int a = i + 1;
                int b = (i + 1) % rim + 1;

                // Side
                triangles[t++] = 0;
                triangles[t++] = b;
                triangles[t++] = a;

                // Far cap
                triangles[t++] = rim + 1;
                triangles[t++] = a;
                triangles[t++] = b;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private void PushProperties()
        {
            if (_renderer == null)
                return;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);

            _block.SetColor(DotColorId, dotColor);
            _block.SetFloat(DensityId, density);
            _block.SetFloat(DotSizeId, dotSize);
            _block.SetFloat(RangeId, _range);
            _block.SetFloat(HalfAngleId, _fullAngle * 0.5f * Mathf.Deg2Rad);
            _block.SetFloat(IntensityId, intensity);
            _block.SetFloat(EdgeSoftnessId, edgeSoftness);
            _block.SetFloat(NearFadeId, nearFade);

            _renderer.SetPropertyBlock(_block);
        }

        private void OnDestroy()
        {
            if (_cone != null)
                Destroy(_cone);
        }
    }
}
