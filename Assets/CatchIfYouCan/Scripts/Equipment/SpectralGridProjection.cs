using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The projector's field of laser points: one volume, one material, no dots as objects.
    ///
    /// <para>
    /// The whole effect is a single box rendered with <c>CatchIfYouCan/SpectralGrid</c>. For
    /// each of its pixels the shader reconstructs the world position of the scene surface
    /// behind that pixel and asks whether the direction from the LENS to that point lands on a
    /// dot of an angular grid. So the dots belong to the floor, the walls, the ceiling and the
    /// props they fall on, and there is no dot anywhere in the scene graph: no GameObject per
    /// dot, no particle, no light per dot, nothing instantiated or destroyed while it runs, and
    /// nothing to replicate over a network later but the projector's transform and whether it
    /// is on.
    /// </para>
    ///
    /// <para>
    /// <b>A sphere, not a cone.</b> The pattern lives in the projector's own spherical
    /// coordinates, so it surrounds the device - as much of it reaches the ceiling as the floor,
    /// and it converges towards the device's axis the way a real multi-directional laser
    /// projector does. Two previous attempts were shaped like the emitter instead of like the
    /// room: one 70-degree spot, which is a torch, and then five wider spots, which is a torch
    /// with company. Neither could cover a sphere, because a cone cannot.
    /// </para>
    ///
    /// <para>
    /// <b>Everything the shader needs is pushed in WORLD space.</b> The lens position and the
    /// device's three axes go in as uniforms rather than being read from the object matrix. The
    /// version before last worked in object space and drew nothing, for a reason that was never
    /// established (CLAUDE.md mistake 33) - so rather than guess at it a fifth time, the
    /// dependency is gone: nothing here can be broken by how this object is parented, nested or
    /// scaled.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Spectral Grid Projection")]
    public sealed class SpectralGridProjection : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Laser green. Bright and saturated; the room around the dots stays dark " +
                 "because the material adds light and never floods it.")]
        [SerializeField] private Color dotColor = new Color(0.224f, 1f, 0.290f, 1f);

        [Tooltip("How many dot cells go the whole way round the device. Elevation gets half " +
                 "as many, spanning half the angle, which makes a cell square: 360/density " +
                 "degrees on both axes. 240 is a 1.5 degree cell - dots about 8 cm apart on a " +
                 "surface 3 m away, roughly 2400 of them inside a 90x60 degree view, and 28800 " +
                 "over the whole sphere. It costs nothing per pixel: the shader evaluates a " +
                 "formula, not a list of dots, so density is free and only aliasing limits it.")]
        [SerializeField, Range(16f, 320f)] private float density = 240f;

        [Tooltip("Size of one dot within its cell. Past about 0.3 they merge into the " +
                 "continuous green wash this is not supposed to be.")]
        [SerializeField, Range(0.02f, 0.45f)] private float dotSize = 0.16f;

        [Tooltip("How far the soft halo around a dot reaches, as a multiple of the dot itself. " +
                 "Kept below 3 so that dotSize x this stays under half a cell and neighbouring " +
                 "dots cannot run into one another however bright they are.")]
        [SerializeField, Range(1f, 3f)] private float glowRadius = 2.2f;

        [Tooltip("How bright that halo is against the dot's core. A little reads as laser " +
                 "light; a lot reads as fog.")]
        [SerializeField, Range(0f, 1f)] private float glowStrength = 0.35f;

        [Tooltip("Brightness of a dot. This is the only brightness there is - there is no " +
                 "light in this effect, so it cannot flood a room however high it goes.")]
        [SerializeField, Range(0f, 12f)] private float intensity = 3f;

        [Header("Reach")]
        [Tooltip("How far the dots reach, in metres. A room, not a building. Geometry beyond " +
                 "this gets nothing at all.")]
        [SerializeField, Range(2f, 10f)] private float projectionRange = 5.5f;

        [Tooltip("Where the fade begins, as a fraction of the range. 0.55 keeps dots at full " +
                 "strength across most of a room and fades them over the last stretch.")]
        [SerializeField, Range(0.05f, 1f)] private float fadeStart = 0.55f;

        [Tooltip("Extra punch within a metre of the lens, where the reference pattern is at " +
                 "its densest and brightest.")]
        [SerializeField, Range(0f, 4f)] private float nearBoost = 1.2f;

        [Tooltip("A short fade right at the lens, so standing on the device is not a wall of " +
                 "light.")]
        [SerializeField, Range(0f, 1f)] private float nearFade = 0.12f;

        [Tooltip("How hard to drop the pattern on surfaces that are edge-on to the lens, which " +
                 "is what makes it wrap convincingly across a corner. 0 disables it.")]
        [SerializeField, Range(0f, 1f)] private float facingStrength = 0.7f;

        [Header("Occlusion")]
        [Tooltip("How completely a wall between the lens and a surface takes that surface's " +
                 "dots away. The test is a screen-space march and is built to fail OPEN: every " +
                 "way it can be unsure leaves the dot lit, never the other way round. 0 removes " +
                 "it entirely and costs nothing - use that if it ever misbehaves, because a " +
                 "projector with too few shadows is a far smaller bug than an invisible one.")]
        [SerializeField, Range(0f, 1f)] private float occlusionStrength = 1f;

        [Tooltip("How far in front of a marched point a surface has to be before it counts as " +
                 "blocking, in metres. Too small and a surface shadows itself; too large and " +
                 "thin occluders are missed. Missing one is the safe direction.")]
        [SerializeField, Range(0.005f, 0.5f)] private float occlusionBias = 0.05f;

        [Header("Emitter")]
        [Tooltip("Where the lens sits relative to the device's pivot, in its own space. +Y is " +
                 "the device's working axis by the shared carried-transform convention.")]
        [SerializeField] private Vector3 projectionOriginOffset = new Vector3(0f, 0.06f, 0f);

        /// <summary>The child that marks the lens. Found by name so it survives a clone.</summary>
        private const string OriginChildName = "ProjectionOrigin";

        /// <summary>The child that carries the volume. Also found by name, for the same reason.</summary>
        private const string VolumeChildName = "SpectralGrid_Volume";

        private Transform _origin;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MaterialPropertyBlock _block;
        private Mesh _volume;
        private float _builtRange = -1f;
        private bool _running;

        private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int DotSizeId = Shader.PropertyToID("_DotSize");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        private static readonly int NearBoostId = Shader.PropertyToID("_NearBoost");
        private static readonly int NearFadeId = Shader.PropertyToID("_NearFade");
        private static readonly int FacingId = Shader.PropertyToID("_FacingStrength");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int OcclusionId = Shader.PropertyToID("_OcclusionStrength");
        private static readonly int OcclusionBiasId = Shader.PropertyToID("_OcclusionBias");
        private static readonly int OriginId = Shader.PropertyToID("_OriginWS");
        private static readonly int AxisXId = Shader.PropertyToID("_AxisXWS");
        private static readonly int AxisYId = Shader.PropertyToID("_AxisYWS");
        private static readonly int AxisZId = Shader.PropertyToID("_AxisZWS");

        /// <summary>Where the dots come from. The lens, in world space.</summary>
        public Vector3 ProjectionOrigin => _origin != null
            ? _origin.position
            : transform.TransformPoint(projectionOriginOffset);

        /// <summary>How far the dots reach, in metres.</summary>
        public float Range => projectionRange;

        /// <summary>
        /// Attaches a projection to a device head. The volume is a child, so it follows the
        /// device without anything having to track it.
        /// </summary>
        public static SpectralGridProjection Attach(Transform head)
        {
            if (head == null)
                return null;

            var go = new GameObject("SpectralGridProjection");
            go.transform.SetParent(head, false);
            return go.AddComponent<SpectralGridProjection>();
        }

        /// <summary>
        /// Sets how far the field reaches. Cheap; safe to call whenever it changes.
        ///
        /// <para>
        /// The angle argument describes the device's EVIDENCE cone, which
        /// <see cref="SpectralGridProjector.FieldStrengthAt"/> uses to decide whether a ghost is
        /// standing in the field. It does not size the projection: the lit field is a sphere and
        /// that cone is 70 degrees forward, so a ghost lit on a side wall is not currently
        /// counted. Widening the evidence cone changes what the projector can prove and is an
        /// evidence-contract decision, not a rendering one.
        /// </para>
        /// </summary>
        public void Configure(float evidenceRange, float evidenceConeDegrees)
        {
            EnsureVolume();
            PushProperties();
        }

        /// <summary>
        /// Turns the field on and off. Everything expensive is behind this: a projector that is
        /// switched off, stowed or in a bag renders nothing at all.
        /// </summary>
        public void SetRunning(bool running)
        {
            _running = running;
            EnsureVolume();

            if (_renderer != null)
                _renderer.enabled = running && _renderer.sharedMaterial != null;

            if (running)
            {
                PushProperties();
                PushOrigin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ReportProjection();
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Core.CIYCLog.Info("[CIYC][DOTS] PROJECTION_OFF");
            }
#endif
        }

        /// <summary>
        /// The lens moves whenever the device does - it is in the player's hand half the time -
        /// so the world-space origin is refreshed after everything else has moved. Four vector
        /// writes into a property block that is allocated once: no garbage, no material
        /// instance, and nothing at all while the projector is off.
        /// </summary>
        private void LateUpdate()
        {
            if (_running)
                PushOrigin();
        }

        /// <summary>
        /// Builds the volume once, and adopts the one a CLONE already carries.
        ///
        /// <para>
        /// Every piece of equipment in this project reaches the world as an <c>Instantiate</c>
        /// of a live template, and <c>Instantiate</c> copies GameObjects and components while
        /// dropping the private field that pointed at them. So on the clone the children are
        /// already there and the fields are null - and a <c>new GameObject</c> here would hang a
        /// SECOND volume on the same device, drawing the whole pattern twice at double
        /// brightness. That is mistakes 27 and 30 wearing another face, so the children are
        /// looked for by NAME and their parts by component; both survive the copy, the fields
        /// do not.
        /// </para>
        /// </summary>
        private void EnsureVolume()
        {
            if (_origin == null)
            {
                _origin = transform.Find(OriginChildName);
                if (_origin == null)
                {
                    var originGo = new GameObject(OriginChildName);
                    originGo.transform.SetParent(transform, false);
                    _origin = originGo.transform;
                }
            }

            _origin.localPosition = projectionOriginOffset;
            _origin.localRotation = Quaternion.identity;

            Transform host = transform.Find(VolumeChildName);
            if (host == null)
            {
                var hostGo = new GameObject(VolumeChildName);
                hostGo.transform.SetParent(transform, false);
                host = hostGo.transform;
            }

            host.localPosition = projectionOriginOffset;
            host.localRotation = Quaternion.identity;

            // This box is the effect's screen footprint, not the device. Marked so that
            // everything which measures "how big is this item" leaves it out - unmarked, the
            // lobby measured eleven metres for a 25 cm projector and lifted it above the
            // ceiling, where an item that is there and an item that never spawned look alike.
            EffectVolume.Mark(host.gameObject);

            // The volume is sized in METRES, so any scale inherited from the device's visual
            // chain is cancelled here. Belt and braces - the chain is unit-scaled today - but it
            // costs one line and removes a way for the box to end up a tenth of the size the
            // range says it is.
            Vector3 lossy = transform.lossyScale;
            host.localScale = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
                Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
                Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z);

            _filter = host.GetComponent<MeshFilter>();
            if (_filter == null)
                _filter = host.gameObject.AddComponent<MeshFilter>();

            _renderer = host.GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = host.gameObject.AddComponent<MeshRenderer>();

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (_renderer.sharedMaterial == null)
            {
                // The authored material, so its shader is referenced by an asset and survives a
                // build. Asking Shader.Find for it directly is how a shader gets stripped and
                // the effect quietly becomes nothing on a device.
                var material = Resources.Load<Material>("Materials/MAT_SpectralGrid");
                if (material == null)
                {
                    Core.CIYCLog.Error("[CIYC][DOTS] No MAT_SpectralGrid under " +
                                       "Resources/Materials, so the projector will draw " +
                                       "nothing. Its shader is very likely not in this build " +
                                       "either.");
                }

                _renderer.sharedMaterial = material;
            }

            RebuildVolume();

            _renderer.enabled = _running && _renderer.sharedMaterial != null;
            _block ??= new MaterialPropertyBlock();
        }

        /// <summary>
        /// The box the shader runs inside: twelve triangles around the lens, half-extent equal
        /// to the range.
        ///
        /// <para>
        /// It exists only to give the fragment shader pixels to run on, and it is a box rather
        /// than a full-screen pass so a projector across the house costs its own screen footprint
        /// instead of the whole frame. Front faces are culled, so the volume still covers the
        /// screen when the camera is inside it.
        /// </para>
        /// </summary>
        private void RebuildVolume()
        {
            if (Mathf.Approximately(_builtRange, projectionRange) && _volume != null)
            {
                _filter.sharedMesh = _volume;
                return;
            }

            _builtRange = projectionRange;

            if (_volume == null)
                _volume = new Mesh { name = "SpectralGridVolume" };

            float r = projectionRange;
            var vertices = new Vector3[8]
            {
                new Vector3(-r, -r, -r), new Vector3(r, -r, -r),
                new Vector3(r, -r, r),   new Vector3(-r, -r, r),
                new Vector3(-r, r, -r),  new Vector3(r, r, -r),
                new Vector3(r, r, r),    new Vector3(-r, r, r),
            };

            var triangles = new int[]
            {
                0, 2, 1, 0, 3, 2,   // bottom
                4, 5, 6, 4, 6, 7,   // top
                0, 1, 5, 0, 5, 4,   // -z
                2, 3, 7, 2, 7, 6,   // +z
                3, 0, 4, 3, 4, 7,   // -x
                1, 2, 6, 1, 6, 5,   // +x
            };

            _volume.Clear();
            _volume.vertices = vertices;
            _volume.triangles = triangles;
            _volume.RecalculateBounds();
            _filter.sharedMesh = _volume;
        }

        private void PushProperties()
        {
            if (_renderer == null)
                return;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);

            _block.SetColor(DotColorId, dotColor);
            // Rounded: the azimuth grid wraps from +PI to -PI, and only a whole number of cells
            // meets itself there. A fractional density puts a visible seam down one meridian.
            _block.SetFloat(DensityId, Mathf.Round(density));
            _block.SetFloat(DotSizeId, dotSize);
            _block.SetFloat(RangeId, projectionRange);
            _block.SetFloat(IntensityId, intensity);
            _block.SetFloat(FadeStartId, fadeStart);
            _block.SetFloat(NearBoostId, nearBoost);
            _block.SetFloat(NearFadeId, nearFade);
            _block.SetFloat(FacingId, facingStrength);
            _block.SetFloat(GlowRadiusId, glowRadius);
            _block.SetFloat(GlowStrengthId, glowStrength);
            _block.SetFloat(OcclusionId, occlusionStrength);
            _block.SetFloat(OcclusionBiasId, occlusionBias);

            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// The lens and the device's three axes, in world space. Everything angular in the
        /// shader is measured from these, which is what keeps the pattern centred on the
        /// physical projector and turning with it.
        /// </summary>
        private void PushOrigin()
        {
            if (_renderer == null || _origin == null)
                return;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);

            Vector3 o = _origin.position;
            _block.SetVector(OriginId, new Vector4(o.x, o.y, o.z, 0f));
            _block.SetVector(AxisXId, _origin.right);
            _block.SetVector(AxisYId, _origin.up);
            _block.SetVector(AxisZId, _origin.forward);

            _renderer.SetPropertyBlock(_block);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// One line, at switch-on. Never per frame: this runs from <see cref="SetRunning"/>,
        /// which runs on a keypress.
        /// </summary>
        private void ReportProjection()
        {
            int cells = Mathf.RoundToInt(Mathf.Round(density) * Mathf.Round(density) * 0.5f);
            var volumes = GetComponentsInChildren<MeshRenderer>(true);

            string block =
                "[CIYC][DOTS] PROJECTION_ON" +
                " mode=SphericalAngular" +
                " origin=" + ProjectionOrigin.ToString("F2") +
                " axis=" + (_origin != null ? _origin.up.ToString("F2") : "-") +
                " range=" + projectionRange.ToString("F1") +
                " density=" + Mathf.Round(density).ToString("F0") +
                " cellsOnSphere=" + cells +
                " dotSize=" + dotSize.ToString("F2") +
                " cellDegrees=" + (360f / Mathf.Max(1f, Mathf.Round(density))).ToString("F2") +
                " intensity=" + intensity.ToString("F1") +
                " occlusion=" + occlusionStrength.ToString("F2") +
                " material=" + (_renderer != null && _renderer.sharedMaterial != null
                    ? _renderer.sharedMaterial.name : "NULL") +
                " renderer=" + (_renderer != null && _renderer.enabled);

            bool broken = _renderer == null || !_renderer.enabled ||
                          _renderer.sharedMaterial == null;
            if (broken)
            {
                Core.CIYCLog.Error(block + "  <- FAILED PROJECTION STATE");
            }
            else
            {
                // What this cannot see: whether the URP asset still has its depth texture. The
                // shader reconstructs every dot from it, and without it every pixel resolves to
                // the far plane and the effect is invisible rather than wrong.
                Core.CIYCLog.Info(block);
            }

            if (volumes.Length > 1)
            {
                Core.CIYCLog.Error("[CIYC][DOTS][ERROR] unexpectedVolumeCount=" + volumes.Length +
                                   " expected=1 - a second volume was built on a clone that " +
                                   "already had one, so the pattern is drawn twice.");
            }
        }
#endif

        private void OnDestroy()
        {
            if (_volume != null)
                Destroy(_volume);
        }
    }
}
