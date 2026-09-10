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
                 "degrees on both axes. 144 is a 2.5 degree cell - dots about 13 cm apart on a " +
                 "surface 3 m away and under a thousand of them inside a 90x60 degree view. " +
                 "It STARTS here rather than at 240 because a coarse grid that is wrong is " +
                 "legible and a fine one that is wrong is a green wash: at 240 a mapping error " +
                 "and a correct field look the same from across a room. It costs nothing per " +
                 "pixel either way - the shader evaluates a formula, not a list of dots - so " +
                 "the number is a legibility decision, not a performance one.")]
        [SerializeField, Range(16f, 320f)] private float density = 144f;

        [Tooltip("Size of one dot within its cell. Past about 0.3 they merge into the " +
                 "continuous green wash this is not supposed to be.")]
        [SerializeField, Range(0.02f, 0.45f)] private float dotSize = 0.16f;

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

        [Header("Diagnosis")]
        [Tooltip("A ladder for 'it says it is running and nothing is on screen', climbed one " +
                 "rung at a time. RENUMBERED: rung 2 is new and the old 2-5 have each moved up " +
                 "by one.\n\n" +
                 "0 = the game, the finished dots.\n" +
                 "1 = MAGENTA over the whole volume. Does this pass rasterise? CONFIRMED in " +
                 "Unity.\n" +
                 "2 = the screen UV as a red/green gradient. A smooth gradient means the UV is a " +
                 "real screen coordinate; ONE FLAT COLOUR means every fragment reads the same " +
                 "point, which would make the depth rung below look unbound when it is not. It " +
                 "sits above the depth rung because sampling depth USES this UV.\n" +
                 "3 = the RAW depth value with no sky classification at all. Whole view flat " +
                 "BLUE = exactly 0 everywhere, so no depth texture is reaching this pass. Flat " +
                 "GREEN = exactly 1 everywhere. Stripes over the room with blue only through the " +
                 "window = real varying depth, which is correct.\n" +
                 "4 = the reconstructed world position. Bands GLUED to the walls as you turn on " +
                 "the spot are correct; bands that SWIM with the view mean the inverse " +
                 "view-projection is wrong.\n" +
                 "5 = green within range, RED outside. A green ball centred on the device means " +
                 "the lens and range arrived; all red means the origin is elsewhere.\n" +
                 "6 = a coarse 20-degree angular chequerboard. Squares on floor, ceiling and all " +
                 "four walls mean a sphere; squares in one direction only mean a cone.\n\n" +
                 "Every rung returns ABOVE the work the next needs, and none sits below an " +
                 "invisible early-out. Ships at 0, and a guard keeps it there (mistake 23).")]
        [SerializeField, Range(0, 6)] private int debugStage = 0;

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
        private bool _propertiesDirty;

        private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int DotSizeId = Shader.PropertyToID("_DotSize");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        private static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");
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

            // Adopt before building, here as well as at the call site. A head that came across
            // on a clone already carries one of these, and a second would be a second volume
            // drawing the same pattern on top of the first. Defence in depth is worth one
            // GetComponentInChildren on a path that runs once per item: this is the shape that
            // produced mistakes 27 and 30, and both times the cost of finding it was a session.
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
                RequestSceneDepth();
                _propertiesDirty = false;
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
            if (!_running)
                return;

            // The tuning values were pushed ONCE, at switch-on, and never again. Everything
            // below the lens - density, dot size, intensity, the debug stage - therefore sat in
            // the renderer at whatever it was when G was pressed, and
            // an Inspector edit during Play changed a number that reached nothing. That is not
            // a small thing: it is why a six-stage bisect came back with all six stages
            // identical. They were all stage 0. A control that cannot move what it names is
            // worse than no control, because it produces evidence.
            // While a diagnostic stage is selected, the values go across EVERY frame rather than
            // on a dirty flag. The flag depends on OnValidate firing, and OnValidate is an editor
            // callback with its own rules about when it runs; a bisect that quietly measures the
            // wrong stage has already cost one whole session and produced six false findings.
            // A diagnostic has to be the one thing in the frame that cannot be doubted, and the
            // cost of that certainty is a dozen SetFloats on a block this method already owns.
            if (_propertiesDirty || debugStage > 0)
            {
                _propertiesDirty = false;
                PushProperties();
            }

            PushOrigin();
        }

#if UNITY_EDITOR
        /// <summary>
        /// An Inspector edit marks the values stale rather than pushing them here: OnValidate
        /// can run before <see cref="EnsureVolume"/> has made a renderer, and it can run outside
        /// Play. The next frame that is actually running does the work.
        /// </summary>
        private void OnValidate()
        {
            _propertiesDirty = true;
        }
#endif

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
            _block.SetFloat(DebugModeId, debugStage);

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

        /// <summary>
        /// Declares that this effect cannot work without a scene depth texture, on the cameras
        /// that actually render to a display.
        ///
        /// <para>
        /// The shader reconstructs every dot from the depth buffer, so without one it draws
        /// nothing at all - not something wrong, NOTHING, which is indistinguishable from a dead
        /// pass. <c>CIYC_URP.asset</c> already carries <c>m_RequireDepthTexture: 1</c> and all
        /// three quality levels use it (their <c>customRenderPipeline</c> is 0), so the pipeline
        /// asks for depth globally. What the pipeline setting cannot do is override a CAMERA that
        /// says no - and the player's camera is created at RUNTIME by <c>PlayerRigBuilder</c>,
        /// which adds a <c>UniversalAdditionalCameraData</c> and sets only
        /// <c>renderPostProcessing</c> on it. Whatever that component's depth option defaults to
        /// on a freshly added instance is the one link in this chain that cannot be read from any
        /// file in this repository.
        /// </para>
        ///
        /// <para>
        /// So the requirement is declared HERE, by the thing that has it, through
        /// <c>Camera.depthTextureMode</c> - a core engine property rather than a package one.
        /// That choice is deliberate: URP 17.5.0's sources are not on this machine and
        /// <c>docs.unity3d.com</c> answers 403, so the URP-specific property that backs the
        /// camera's own Depth Texture dropdown cannot be verified to exist under that name in
        /// this version. Writing an unverifiable symbol into a build would not fail this
        /// effect - it would fail EVERY script in the project (mistake 9), and a dark projector
        /// is a far smaller bug than a project that will not compile.
        /// </para>
        ///
        /// <para>
        /// Cameras with a <c>targetTexture</c> are skipped on purpose: the portal and the mirror
        /// both render into buffers of their own and neither is the view this effect is seen in.
        /// </para>
        /// </summary>
        private void RequestSceneDepth()
        {
            Camera[] cameras = Camera.allCameras;
            if (cameras == null)
                return;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || cam.targetTexture != null)
                    continue;

                cam.depthTextureMode |= DepthTextureMode.Depth;
            }
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
                " material=" + (_renderer != null && _renderer.sharedMaterial != null
                    ? _renderer.sharedMaterial.name : "NULL") +
                " renderer=" + (_renderer != null && _renderer.enabled) +
                (debugStage > 0 ? "  DEBUG STAGE " + debugStage : string.Empty);

            // Everything a person would otherwise have to pause the game and click through, in
            // one line at the moment it is switched on. "It says PROJECTING and there is nothing
            // on screen" has half a dozen causes that all look identical from the player's seat -
            // a disabled renderer, a culled volume, a material that did not resolve, a shader the
            // platform refuses, a volume the size of a coin - and each of them is a different
            // afternoon. None of them can hide from this.
            string volume = "[CIYC][DOTS][VOLUME]";
            if (_renderer == null)
            {
                volume += " renderer=NULL";
            }
            else
            {
                Material mat = _renderer.sharedMaterial;
                Bounds b = _renderer.bounds;
                volume +=
                    " go=" + _renderer.gameObject.name +
                    " activeInHierarchy=" + _renderer.gameObject.activeInHierarchy +
                    " rendererEnabled=" + _renderer.enabled +
                    " layer=" + _renderer.gameObject.layer +
                    " worldScale=" + _renderer.transform.lossyScale.ToString("F3") +
                    " boundsCenter=" + b.center.ToString("F2") +
                    " boundsSize=" + b.size.ToString("F2") +
                    " mesh=" + (_filter != null && _filter.sharedMesh != null
                        ? _filter.sharedMesh.name + "/" + _filter.sharedMesh.vertexCount + "v"
                        : "NULL") +
                    // The NAME answers "shared asset or per-instance copy": Unity suffixes an
                    // instantiated material with " (Instance)". Reading an id would say the same
                    // thing through an API this project cannot typecheck offline.
                    " material=" + (mat != null ? mat.name : "NULL") +
                    " shader=" + (mat != null && mat.shader != null ? mat.shader.name : "NONE") +
                    " shaderSupported=" + (mat != null && mat.shader != null && mat.shader.isSupported) +
                    " debugStage=" + debugStage;
            }

            Core.CIYCLog.Info(volume);

            // Whether the camera stands INSIDE the box is the one fact nobody can read off a
            // screenshot of an invisible effect, and it used to decide which faces were drawn.
            // The pass culls NOTHING now, so it no longer decides anything - which is the point:
            // one fewer thing that has to be right before a single pixel appears. The line stays
            // because it still separates "the volume is nowhere near the viewer" from "the
            // volume is all around the viewer and still draws nothing", and those are different
            // afternoons.
            Camera cam = Camera.main;
            if (cam == null || _renderer == null)
            {
                Core.CIYCLog.Info("[CIYC][DOTS][CAMERA] no main camera or no renderer to compare");
            }
            else
            {
                Vector3 eye = cam.transform.position;
                bool inside = _renderer.bounds.Contains(eye);
                bool masked = (cam.cullingMask & (1 << _renderer.gameObject.layer)) != 0;
                Core.CIYCLog.Info(
                    "[CIYC][DOTS][CAMERA] camera=" + cam.name +
                    " position=" + eye.ToString("F2") +
                    " volumeContainsCamera=" + inside +
                    " cameraMaskIncludesVolume=" + masked +
                    " cullMode=Off (the camera is " + (inside ? "INSIDE" : "outside") + " the volume, " +
                    "and with culling off that no longer changes what is drawn)");

                if (!masked)
                {
                    Core.CIYCLog.Error("[CIYC][DOTS][CAMERA] the camera's culling mask EXCLUDES " +
                                       "the volume's layer, so this pass never runs for it. " +
                                       "Nothing downstream of here can be the reason.");
                }
            }

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

            ReportDuplicates(volumes);
            ReportDepth();
        }

        /// <summary>
        /// Whether a scene depth texture exists at all, and which camera would be producing it.
        ///
        /// <para>
        /// This is the one question the shader's own rungs cannot fully answer. Rung 3 shows a
        /// flat blue view when the sampled value is zero everywhere, and that has two causes with
        /// one picture: the texture was never produced, or it was produced and this pass is not
        /// being given it. <c>Shader.GetGlobalTexture</c> answers the first half from outside the
        /// shader entirely - a null there means nothing produced it, and no amount of reading the
        /// shader will ever say so.
        /// </para>
        ///
        /// <para>
        /// And it does NOT trust <c>Camera.main</c>. That is the camera tagged MainCamera, which
        /// is not necessarily the camera the Game view is drawn by: this scene also runs a portal
        /// camera and a mirror camera, and an Overlay camera in a stack renders through its Base.
        /// Every camera without a <c>targetTexture</c> is listed with its depth ordering, so the
        /// reader can see which one is on top rather than being told.
        /// </para>
        /// </summary>
        private void ReportDepth()
        {
            // Read after a frame has rendered, so this is the texture the last frame actually
            // had. Null is the finding; a size is the finding too.
            Texture depth = Shader.GetGlobalTexture("_CameraDepthTexture");

            var line = new System.Text.StringBuilder("[CIYC][DOTS][DEPTH]");
            line.Append(" depthTextureAvailable=").Append(depth != null);
            if (depth != null)
                line.Append(" depthSize=").Append(depth.width).Append('x').Append(depth.height);
            line.Append(" screenSize=").Append(Screen.width).Append('x').Append(Screen.height);

            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            line.Append(" pipeline=").Append(pipeline != null ? pipeline.name : "NONE");
            line.Append(" mainCamera=").Append(Camera.main != null ? Camera.main.name : "NULL");

            Camera[] cameras = Camera.allCameras;
            int displayCameras = 0;
            if (cameras != null)
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera cam = cameras[i];
                    if (cam == null || cam.targetTexture != null)
                        continue;

                    displayCameras++;
                    line.Append("\n  camera=").Append(cam.name)
                        .Append(" depth=").Append(cam.depth.ToString("F1"))
                        .Append(" targetDisplay=").Append(cam.targetDisplay)
                        .Append(" isMainTagged=").Append(cam == Camera.main)
                        .Append(" requestsDepth=")
                        .Append((cam.depthTextureMode & DepthTextureMode.Depth) != 0);

                    // GetComponent rather than the GetUniversalAdditionalCameraData()
                    // extension: the extension needs a using this file does not carry, and a
                    // plain GetComponent on a type three other files in this project already
                    // name asks nothing new of the package.
                    var data = cam.GetComponent<
                        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    line.Append(" renderType=")
                        .Append(data != null ? data.renderType.ToString() : "NO_URP_DATA");
                }
            }

            line.Append("\n  displayCameras=").Append(displayCameras);

            if (depth == null)
            {
                Core.CIYCLog.Error(line + "\n  <- NO SCENE DEPTH TEXTURE. Every dot is " +
                                   "reconstructed from it, so the projection can only draw " +
                                   "nothing. This is upstream of the shader entirely.");
            }
            else
            {
                Core.CIYCLog.Info(line.ToString());
            }
        }

        /// <summary>
        /// How many of each of these there are under the DEVICE, which is a different question
        /// from how many are under this component.
        ///
        /// <para>
        /// Every item reaches the world as an <c>Instantiate</c> of a live template, so the
        /// clone arrives carrying whatever the template built - a ProjectorHead, a projection, a
        /// volume - while every private field that pointed at them is null. A build step that
        /// reads one of those fields as "nothing here yet" then builds a SECOND set beside the
        /// first (mistakes 27 and 30). Two coincident copies of one effect do not look like two
        /// of anything; on an additive pass they look like one effect at double brightness, and
        /// on a dead one they look like nothing at all. So they are COUNTED, from the device
        /// rather than from here - counting from here can only ever find this component's own
        /// children and would report a clean 1 while a second projection sat next door.
        /// </para>
        /// </summary>
        private void ReportDuplicates(MeshRenderer[] volumesUnderThis)
        {
            // The device, not the scene root: the item spends half its life parented into the
            // player's hand, and counting from there would sweep the whole rig.
            Component device = GetComponentInParent<SpectralGridProjector>();
            Transform scope = device != null ? device.transform : transform;

            var projections = scope.GetComponentsInChildren<SpectralGridProjection>(true);
            var volumes = scope.GetComponentsInChildren<EffectVolume>(true);

            Core.CIYCLog.Info(
                "[CIYC][DOTS][COUNT] scope=" + scope.name +
                " projectionCountUnderProjector=" + projections.Length +
                " volumeCountUnderProjector=" + volumes.Length +
                " renderersUnderThisProjection=" + volumesUnderThis.Length +
                " expected=1/1/1");

            if (projections.Length == 1 && volumes.Length == 1 && volumesUnderThis.Length == 1)
                return;

            // Named, not counted. "2" sends the reader looking for a second projector; the PATHS
            // say whether it is a duplicated volume, a stray marked renderer or the device's own
            // model caught by the marker.
            Core.CIYCLog.Error("[CIYC][DOTS][ERROR] duplicate projection machinery on this " +
                               "device: projections=" + Describe(projections) +
                               " volumes=" + Describe(volumes) +
                               " renderersUnderThisProjection=" + Describe(volumesUnderThis));
        }

        private static string Describe(Component[] parts)
        {
            if (parts == null || parts.Length == 0)
                return "(none)";

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                if (names.Length > 0)
                    names.Append(" | ");
                Transform t = parts[i].transform;
                names.Append(t.name);
                if (t.parent != null)
                    names.Append(" (under ").Append(t.parent.name).Append(')');
            }

            return names.Length > 0 ? names.ToString() : "(none)";
        }
#endif

        private void OnDestroy()
        {
            if (_volume != null)
                Destroy(_volume);
        }
    }
}
