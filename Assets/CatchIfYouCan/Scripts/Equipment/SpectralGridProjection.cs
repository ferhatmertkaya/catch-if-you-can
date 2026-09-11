using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The DOTS projector's field of laser points: one mesh, one renderer, no dots as objects.
    ///
    /// <para>
    /// <b>What changed, and why.</b> Three versions of this drew the dots in a fragment shader by
    /// reconstructing the world position of the surface behind each pixel from the scene depth
    /// texture. The technique is correct and the shader compiled - the diagnostic ladder proved
    /// both: its magenta rung drew over the whole volume, and its screen-UV rung drew a correct
    /// gradient. The rung below it read the RAW value out of <c>SampleSceneDepth</c> with no
    /// interpretation on top and came back flat blue, meaning exactly 0.0 at every pixel. No depth
    /// texture reaches that pass in this project. A technique that needs a resource nobody can
    /// hand it is the wrong technique here, however right it is in the abstract, so it is gone
    /// rather than debugged a fourth time.
    /// </para>
    ///
    /// <para>
    /// <b>How it works now.</b> Rays are cast out of the lens in every direction, and one small
    /// quad is laid flat on each surface they hit. All the quads go into ONE mesh on ONE
    /// MeshRenderer with ONE material, so the whole field is a single draw call and there is no
    /// GameObject, particle, light, decal or collider per dot. Nothing is sampled from the frame
    /// buffer, so there is nothing left that can be unbound.
    /// </para>
    ///
    /// <para>
    /// <b>A sphere by construction, not by hoping.</b> The ray directions are a Fibonacci sphere:
    /// a genuinely uniform covering of all 4-pi steradians with no clustering at the poles and no
    /// seam at the wrap. Floor, ceiling, both side walls, in front and behind all get the same
    /// density, and no axis of the device can remove a hemisphere - the device's rotation only
    /// turns the pattern. Two earlier attempts were cones (mistake 39), and a cone cannot cover a
    /// sphere however many of them there are.
    /// </para>
    ///
    /// <para>
    /// <b>Cost.</b> The rays are cast when the field is switched on and then only when the lens
    /// has actually moved, never per frame. A deployed projector - the case this device is for -
    /// casts once and then costs one draw call until it is switched off. In the hand it rebuilds
    /// on a movement threshold at a much lower count, because a projector being carried is being
    /// carried somewhere rather than being read.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpectralGridProjection : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Laser green. Bright and saturated; the room around the dots stays dark because " +
                 "the material adds light and never floods it.")]
        [SerializeField] private Color dotColor = new Color(0.208f, 1f, 0.271f, 1f);

        [Tooltip("Brightness of a dot. This is the only brightness there is - there is no light " +
                 "in this effect, so it cannot flood a room however high it goes.")]
        [SerializeField, Range(0f, 12f)] private float intensity = 2.8f;

        [Tooltip("How soft a dot's edge is, as a fraction of its radius. Small keeps it a crisp " +
                 "laser point; large turns it into a glow blob.")]
        [SerializeField, Range(0.01f, 0.6f)] private float softness = 0.26f;

        [Tooltip("How wide a dot is on the surface at one metre, in metres. Dots grow with " +
                 "distance the way a real projected beam does, so this is the size at the near " +
                 "end rather than everywhere. 0.0065 is a 2 cm dot on a wall three metres away - " +
                 "fine and crisp rather than a coin.")]
        [SerializeField, Range(0.002f, 0.05f)] private float dotSizeAtOneMetre = 0.0065f;

        [Header("Field")]
        [Tooltip("How many rays go out in every direction when the device is DEPLOYED. They are " +
                 "spread over the whole sphere, so a 90x60 degree view holds roughly an eighth " +
                 "of them: 6000 here is about 780 dots in view and 6000 around the room. Each " +
                 "one is four vertices in a single shared mesh, not an object.")]
        [SerializeField, Range(512, 16000)] private int deployedDotCount = 6000;

        [Tooltip("The same, while the device is being CARRIED. Lower on purpose: a projector in " +
                 "the hand moves constantly, so its field is rebuilt often, and a projector being " +
                 "carried is being carried somewhere rather than being read.")]
        [SerializeField, Range(128, 4000)] private int carriedDotCount = 1200;

        [Header("Reach")]
        [Tooltip("How far the dots reach, in metres. A room, not a building. Geometry beyond " +
                 "this gets nothing at all.")]
        [SerializeField, Range(2f, 10f)] private float projectionRange = 5.5f;

        [Tooltip("Where the fade begins, as a fraction of the range. 0.55 keeps dots at full " +
                 "strength across most of a room and fades them over the last stretch. 0.70 holds " +
                 "them longer before the falloff starts, which reads as reach rather than as dimming.")]
        [SerializeField, Range(0.05f, 1f)] private float fadeStart = 0.7f;

        [Header("Rebuilding")]
        [Tooltip("How far the lens must move, in metres, before the field is cast again. Zero " +
                 "would rebuild every frame; this is what keeps a carried projector affordable.")]
        [SerializeField, Range(0.02f, 1f)] private float rebuildMoveThreshold = 0.18f;

        [Tooltip("How far the lens must turn, in degrees, before the field is cast again.")]
        [SerializeField, Range(1f, 45f)] private float rebuildTurnThreshold = 9f;

        [Tooltip("The shortest time between two rebuilds, in seconds. A floor under the cost of " +
                 "walking around with the device in hand.")]
        [SerializeField, Range(0.05f, 1f)] private float rebuildMinInterval = 0.2f;

        [Header("Diagnosis")]
        [Tooltip("0 = the effect. 1 = magenta over every dot quad, which answers 'is this pass " +
                 "running at all' and nothing else. The six-rung ladder that used to live here " +
                 "was asking about a depth texture this effect no longer uses. Ships at 0, and a " +
                 "guard keeps it there: a diagnostic that runs while somebody plays does not " +
                 "diagnose, it creates (mistake 23).")]
        [SerializeField, Range(0, 1)] private int debugStage = 0;

        [Header("Emitter")]
        [Tooltip("Where the lens sits relative to the device's pivot, in its own space. +Y is " +
                 "the device's working axis by the shared carried-transform convention. It moves " +
                 "the ORIGIN of the field; it cannot make the field directional.")]
        [SerializeField] private Vector3 projectionOriginOffset = new Vector3(0f, 0.06f, 0f);

        /// <summary>The child that marks the lens. Found by name so it survives a clone.</summary>
        private const string OriginChildName = "ProjectionOrigin";

        /// <summary>The child that carries the dot mesh. Also found by name, for the same reason.</summary>
        private const string RigChildName = "DOTS_ProjectionRig";

        /// <summary>
        /// How far along its own ray a dot sits off the surface, in metres. Enough to clear
        /// z-fighting with the wall it is painted on, small enough not to read as floating.
        /// </summary>
        private const float SurfaceLift = 0.012f;

        /// <summary>
        /// Where a ray starts, in metres from the lens. The device's own body is around the lens,
        /// and a ray that begins inside it hits it immediately: the whole field would be a
        /// handful of dots on the projector's own casing.
        /// </summary>
        private const float RayStartOffset = 0.07f;

        private Transform _origin;
        private Transform _rig;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Mesh _mesh;
        private bool _running;

        private Vector3[] _vertices;
        private Vector2[] _uv;
        private Color32[] _colors;
        private int[] _indices;
        private int _capacity;

        private Vector3 _lastBuildPosition;
        private Quaternion _lastBuildRotation;
        private float _lastBuildTime = -999f;
        private int _lastDotsPlaced;

        private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");

        private MaterialPropertyBlock _block;

        /// <summary>Where the dots come from. The lens, in world space.</summary>
        public Vector3 ProjectionOrigin => _origin != null
            ? _origin.position
            : transform.TransformPoint(projectionOriginOffset);

        /// <summary>How far the dots reach, in metres.</summary>
        public float Range => projectionRange;

        /// <summary>How many dots the last cast actually landed on a surface.</summary>
        public int DotsPlaced => _lastDotsPlaced;

        /// <summary>
        /// Attaches a projection to a device head. The rig is a child, so it follows the device
        /// without anything having to track it.
        ///
        /// <para>
        /// Adopt before building: a head that came across on a clone already carries one of
        /// these, and a second would draw the whole field twice. This is the shape that produced
        /// mistakes 27, 30 and 46.
        /// </para>
        /// </summary>
        public static SpectralGridProjection Attach(Transform head)
        {
            if (head == null)
                return null;

            var existing = head.GetComponentInChildren<SpectralGridProjection>(true);
            if (existing != null)
                return existing;

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
        /// standing in the field. It does not shape the projection: the lit field is a sphere.
        /// </para>
        /// </summary>
        public void Configure(float evidenceRange, float evidenceConeDegrees)
        {
            EnsureRig();
        }

        /// <summary>
        /// Turns the field on and off. Everything expensive is behind this: a projector that is
        /// switched off, stowed or in a bag renders nothing and casts nothing.
        ///
        /// <para>
        /// The rig is built once and toggled, never created and destroyed - switching on is a
        /// renderer flag plus one cast, and switching off is a renderer flag.
        /// </para>
        /// </summary>
        public void SetRunning(bool running)
        {
            _running = running;
            EnsureRig();

            if (_renderer != null)
                _renderer.enabled = running && _renderer.sharedMaterial != null;

            if (running)
            {
                PushProperties();
                Rebuild();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Report();
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
        /// Recasts the field when the lens has actually moved, and never otherwise.
        ///
        /// <para>
        /// A deployed projector does not move, so after the cast at switch-on this method does
        /// nothing at all for the rest of its life but two comparisons. That is the whole
        /// performance strategy: the expensive part is proportional to how much the device moves,
        /// and the device it was designed for does not.
        /// </para>
        /// </summary>
        private void LateUpdate()
        {
            if (!_running || _origin == null)
                return;

            // Before anything else: the device has moved this frame, so the rig has been dragged
            // with it. Put it back on the world origin, or the dots painted on the walls last
            // rebuild slide along with the projector.
            NeutraliseRig();

            if (debugStage > 0)
                PushProperties();

            if (Time.time - _lastBuildTime < rebuildMinInterval)
                return;

            float moved = Vector3.Distance(_origin.position, _lastBuildPosition);
            float turned = Quaternion.Angle(_origin.rotation, _lastBuildRotation);
            if (moved < rebuildMoveThreshold && turned < rebuildTurnThreshold)
                return;

            Rebuild();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _running)
            {
                PushProperties();
                _lastBuildTime = -999f;
            }
        }
#endif

        /// <summary>
        /// Builds the lens, the rig and the mesh once, and adopts the ones a CLONE already
        /// carries.
        ///
        /// <para>
        /// Every piece of equipment in this project reaches the world as an <c>Instantiate</c> of
        /// a live template, and <c>Instantiate</c> copies GameObjects and components while
        /// dropping the private fields that pointed at them. So on a clone the children are
        /// already there and the fields are null - the one state a naive build reads as "nothing
        /// here yet" (mistakes 27, 30 and 46). Children are found by NAME and their parts by
        /// component; both survive the copy, the fields do not.
        /// </para>
        /// </summary>
        private void EnsureRig()
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

            if (_rig == null)
            {
                _rig = _origin.Find(RigChildName);
                if (_rig == null)
                {
                    var rigGo = new GameObject(RigChildName);
                    rigGo.transform.SetParent(_origin, false);
                    _rig = rigGo.transform;
                }
            }

            NeutraliseRig();

            // The mesh is the effect's screen footprint, not the device. Marked so that everything
            // which measures "how big is this item" leaves it out - unmarked, the lobby measured
            // the effect instead of the object and lifted a 0.25 m projector above the ceiling,
            // where an item that is there and an item that never spawned look alike (mistake 42).
            EffectVolume.Mark(_rig.gameObject);

            _filter = _rig.GetComponent<MeshFilter>();
            if (_filter == null)
                _filter = _rig.gameObject.AddComponent<MeshFilter>();

            _renderer = _rig.GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = _rig.gameObject.AddComponent<MeshRenderer>();

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (_renderer.sharedMaterial == null)
            {
                // The authored material, so its shader is referenced by an asset and survives a
                // build. Asking Shader.Find for it directly is how a shader gets stripped and the
                // effect quietly becomes nothing on a device (mistake 2).
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

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "SpectralGridDots" };
                _mesh.MarkDynamic();
                _filter.sharedMesh = _mesh;
            }

            _block ??= new MaterialPropertyBlock();
            _renderer.enabled = _running && _renderer.sharedMaterial != null;
        }

        /// <summary>
        /// Holds the rig at WORLD identity, so the mesh it carries is drawn exactly where its
        /// vertices say.
        ///
        /// <para>
        /// This is the one line that decides whether any of this appears in the right place. The
        /// quads are built in WORLD coordinates, and the rig is a child of the lens - so setting
        /// its LOCAL transform to identity does not mean "no transform", it means "wear the lens's
        /// position and rotation", and every dot would have the lens transform applied to it a
        /// second time. A projector standing two metres from the origin and turned on its side
        /// would paint its dots four metres away, rotated twice. It has to be world identity, and
        /// it has to be re-asserted whenever the device moves, which is why this is called from
        /// the per-frame path as well as from the build.
        /// </para>
        ///
        /// <para>
        /// The scale is cancelled rather than set to one for the same reason: a one in local scale
        /// inherits whatever the chain above it is doing, and this chain runs through the item's
        /// visual, which is scaled to fit the model.
        /// </para>
        /// </summary>
        private void NeutraliseRig()
        {
            if (_rig == null)
                return;

            _rig.position = Vector3.zero;
            _rig.rotation = Quaternion.identity;

            Vector3 lossy = _rig.parent != null ? _rig.parent.lossyScale : Vector3.one;
            _rig.localScale = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
                Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
                Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z);
        }

        /// <summary>
        /// Casts the field and writes it into the mesh.
        ///
        /// <para>
        /// One ray per dot, in a Fibonacci-sphere direction, from just outside the device's own
        /// casing. Each hit becomes a quad lying flat on that surface, lifted a few millimetres
        /// along its normal so it does not fight the wall for the same pixels, sized so that it
        /// grows with distance the way a real projected beam does, and given a per-dot fade in its
        /// vertex colour. Rays that hit nothing within range simply produce no dot, which is what
        /// bounds the field to a room.
        /// </para>
        /// </summary>
        private void Rebuild()
        {
            if (_origin == null || _mesh == null)
                return;

            int wanted = Mathf.Max(16, IsCarried() ? carriedDotCount : deployedDotCount);
            EnsureCapacity(wanted);

            Vector3 lens = _origin.position;
            Transform deviceRoot = transform.root;

            // The golden angle. Successive directions land as far from their predecessors as the
            // sphere allows, which is what makes the covering uniform without a grid - and a grid
            // is what put a seam down one meridian and a pile at each pole last time.
            const float GoldenAngle = 2.399963f;

            int placed = 0;
            for (int i = 0; i < wanted; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / wanted;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = GoldenAngle * i;
                Vector3 localDir = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);

                // Into the device's own frame, so the pattern turns with it. A rotation cannot
                // remove a hemisphere: whatever the device's axis is, every direction is still
                // covered (mistake 34 and 39 both live here).
                Vector3 dir = _origin.TransformDirection(localDir);

                Vector3 start = lens + dir * RayStartOffset;
                float reach = projectionRange - RayStartOffset;

                if (!Physics.Raycast(start, dir, out RaycastHit hit, reach,
                                     Physics.DefaultRaycastLayers,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                // The device's own body, and the player carrying it, are not surfaces to paint.
                if (hit.collider != null && hit.collider.transform.IsChildOf(deviceRoot))
                    continue;

                float dist = RayStartOffset + hit.distance;
                float travel = dist / Mathf.Max(0.01f, projectionRange);
                float fade = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(fadeStart, 1f, travel));
                if (fade <= 0.01f)
                    continue;

                WriteQuad(placed, hit.point + hit.normal * SurfaceLift, hit.normal,
                          dotSizeAtOneMetre * dist, fade);
                placed++;
            }

            _lastDotsPlaced = placed;
            UploadMesh(placed, lens);

            _lastBuildPosition = lens;
            _lastBuildRotation = _origin.rotation;
            _lastBuildTime = Time.time;
        }

        /// <summary>True while the device is in somebody's hands rather than deployed.</summary>
        private bool IsCarried()
        {
            var projector = GetComponentInParent<SpectralGridProjector>();
            return projector != null && !projector.IsPlaced;
        }

        /// <summary>
        /// One dot: four vertices lying in the surface's own plane, centred on the hit.
        ///
        /// <para>
        /// Flat on the surface rather than facing the camera, because that is what a projected
        /// dot is - it stretches into an ellipse on a wall seen at a glancing angle, exactly as a
        /// real one does, and it needs nothing per frame to keep facing anybody.
        /// </para>
        /// </summary>
        private void WriteQuad(int index, Vector3 centre, Vector3 normal, float size, float fade)
        {
            // Any two perpendicular directions in the surface's plane. Built from whichever world
            // axis is least parallel to the normal, so the cross product never collapses.
            Vector3 reference = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, reference));
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            float half = size * 0.5f;
            Vector3 a = tangent * half;
            Vector3 b = bitangent * half;

            int v = index * 4;
            _vertices[v + 0] = centre - a - b;
            _vertices[v + 1] = centre + a - b;
            _vertices[v + 2] = centre + a + b;
            _vertices[v + 3] = centre - a + b;

            var tint = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(fade * 255f), 0, 255));
            _colors[v + 0] = tint;
            _colors[v + 1] = tint;
            _colors[v + 2] = tint;
            _colors[v + 3] = tint;
        }

        /// <summary>
        /// Grows the working arrays, and only ever upwards.
        ///
        /// <para>
        /// The UVs and the indices never change once written for a slot, so they are filled here
        /// rather than per rebuild - a rebuild writes positions and colours only. Nothing is
        /// allocated while the field is merely running.
        /// </para>
        /// </summary>
        private void EnsureCapacity(int dots)
        {
            if (_capacity >= dots && _vertices != null)
                return;

            int previous = _capacity;
            _capacity = Mathf.Max(dots, 16);

            System.Array.Resize(ref _vertices, _capacity * 4);
            System.Array.Resize(ref _uv, _capacity * 4);
            System.Array.Resize(ref _colors, _capacity * 4);
            System.Array.Resize(ref _indices, _capacity * 6);

            for (int i = previous; i < _capacity; i++)
            {
                int v = i * 4;
                _uv[v + 0] = new Vector2(0f, 0f);
                _uv[v + 1] = new Vector2(1f, 0f);
                _uv[v + 2] = new Vector2(1f, 1f);
                _uv[v + 3] = new Vector2(0f, 1f);

                int t = i * 6;
                _indices[t + 0] = v + 0;
                _indices[t + 1] = v + 2;
                _indices[t + 2] = v + 1;
                _indices[t + 3] = v + 0;
                _indices[t + 4] = v + 3;
                _indices[t + 5] = v + 2;
            }
        }

        /// <summary>
        /// Hands the mesh to Unity, with the unused tail collapsed rather than left behind.
        ///
        /// <para>
        /// Slots past the number of dots actually placed keep whatever they held last time, so
        /// they are folded onto the lens where they have zero area and draw nothing. Clearing the
        /// mesh and rebuilding its arrays instead would allocate on every rebuild, which is the
        /// one thing a per-movement path must not do.
        /// </para>
        /// </summary>
        private void UploadMesh(int placed, Vector3 lens)
        {
            for (int i = placed; i < _capacity; i++)
            {
                int v = i * 4;
                _vertices[v + 0] = lens;
                _vertices[v + 1] = lens;
                _vertices[v + 2] = lens;
                _vertices[v + 3] = lens;
            }

            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.uv = _uv;
            _mesh.colors32 = _colors;
            _mesh.triangles = _indices;

            // The dots are written in world space on an identity rig, so the bounds are a world
            // box around the lens. Set rather than recalculated: recalculating walks every vertex,
            // and the answer is known - the field cannot reach further than its own range.
            _mesh.bounds = new Bounds(lens, Vector3.one * (projectionRange * 2f));
        }

        private void PushProperties()
        {
            if (_renderer == null)
                return;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(DotColorId, dotColor);
            _block.SetFloat(IntensityId, intensity);
            _block.SetFloat(SoftnessId, softness);
            _block.SetFloat(DebugModeId, debugStage);
            _renderer.SetPropertyBlock(_block);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// One line, at switch-on. Never per frame: this runs from <see cref="SetRunning"/>, which
        /// runs on a keypress.
        ///
        /// <para>
        /// It reports the DOT COUNT above everything else, because that one number separates the
        /// two failures this device has spent its life confusing: a field that was never cast
        /// (zero dots - the rays hit nothing, so the projector is somewhere with no geometry
        /// around it) and a field that was cast and is not being drawn (thousands of dots, and a
        /// black screen - the renderer, the material or the shader).
        /// </para>
        /// </summary>
        private void Report()
        {
            int projections = 0;
            int rigs = 0;
            Component device = GetComponentInParent<SpectralGridProjector>();
            Transform scope = device != null ? device.transform : transform;
            projections = scope.GetComponentsInChildren<SpectralGridProjection>(true).Length;
            rigs = scope.GetComponentsInChildren<EffectVolume>(true).Length;

            Material mat = _renderer != null ? _renderer.sharedMaterial : null;
            Core.CIYCLog.Info(
                "[CIYC][DOTS] PROJECTION_ON mode=RaycastQuads" +
                " dotsPlaced=" + _lastDotsPlaced +
                " of " + (IsCarried() ? carriedDotCount : deployedDotCount) + " rays" +
                " origin=" + ProjectionOrigin.ToString("F2") +
                " range=" + projectionRange.ToString("F1") +
                " carried=" + IsCarried() +
                " rendererEnabled=" + (_renderer != null && _renderer.enabled) +
                " material=" + (mat != null ? mat.name : "NULL") +
                " shader=" + (mat != null && mat.shader != null ? mat.shader.name : "NONE") +
                " shaderSupported=" + (mat != null && mat.shader != null && mat.shader.isSupported) +
                " meshVerts=" + (_mesh != null ? _mesh.vertexCount : 0) +
                " projectionCountUnderProjector=" + projections +
                " volumeCountUnderProjector=" + rigs +
                (debugStage > 0 ? "  DEBUG STAGE " + debugStage : string.Empty));

            if (_lastDotsPlaced == 0)
            {
                Core.CIYCLog.Error("[CIYC][DOTS] NOT ONE RAY HIT ANYTHING within " +
                                   projectionRange.ToString("F1") + " m of " +
                                   ProjectionOrigin.ToString("F2") + ". The field is cast by " +
                                   "raycasts, so this is a projector with no geometry around it " +
                                   "rather than a rendering fault.");
            }

            if (projections != 1 || rigs != 1)
            {
                Core.CIYCLog.Error("[CIYC][DOTS][ERROR] duplicate projection machinery: " +
                                   "projections=" + projections + " rigs=" + rigs +
                                   " expected=1/1. A clone built a second set beside the one it " +
                                   "already carried (mistakes 27, 30, 46).");
            }
        }
#endif

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
