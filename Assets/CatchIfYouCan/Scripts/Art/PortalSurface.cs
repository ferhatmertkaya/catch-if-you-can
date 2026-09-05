using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// A doorway that is a hole into somewhere else, rendered live.
    ///
    /// <para>
    /// <b>The portal never moves.</b> Not its position, not its rotation, not its normal. There
    /// is no <c>LookAt</c>, no rotation toward the player, and the surface is not parented to
    /// anything that follows one. The plane is captured once when the surface is built, exactly
    /// as <see cref="MirrorCorner"/> captures its own, so it cannot start tracking anybody even
    /// if something later writes to the transform. Only the hidden camera on the far side moves.
    /// </para>
    ///
    /// <para>
    /// The camera pose is the player's pose carried through the portal pair:
    /// <c>destination * inverse(source) * playerPose</c>. That is a rigid motion, so unlike the
    /// mirror it produces a proper right-handed basis and needs no flip anywhere - the shader
    /// samples screen space without inverting u, and there is no <c>GL.invertCulling</c>.
    /// </para>
    ///
    /// <para>
    /// Screen-space sampling is what makes it an opening rather than a television. The ray
    /// through any pixel of the surface is the continuation of the player's own ray through
    /// that pixel, so the far room shifts with parallax when the player steps left, right, up
    /// or closer, and the frame stays put.
    /// </para>
    ///
    /// <para>
    /// An oblique near plane on the destination plane removes everything behind it, which is
    /// what stops the far room's own entrance wall standing in front of the view.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Portal Surface")]
    public sealed class PortalSurface : MonoBehaviour
    {
        [Header("Pairing")]
        [Tooltip("Where this portal leads. Its forward is the direction the player emerges " +
                 "facing, so it should point INTO the destination room.")]
        [SerializeField] private Transform destination;

        [Header("Opening")]
        [Tooltip("Size of the hole in metres. The lobby doorway is 1.2 x 2.4.")]
        [SerializeField] private Vector2 openingSize = new Vector2(1.2f, 2.4f);

        [Tooltip("Where the surface sits relative to this transform. Local +Z is the side the " +
                 "player looks in from.")]
        [SerializeField] private Vector3 surfaceLocalPosition = new Vector3(0f, 1.2f, 0f);

        [Header("Camera")]
        [Tooltip("What the portal camera may see. The player's own body and the HUD are off: " +
                 "seeing yourself standing in the far room is the one thing that breaks it.")]
        [SerializeField] private LayerMask viewLayers = ~((1 << 5) | (1 << 8) | (1 << 15) | (1 << 16));

        [SerializeField, Min(0.01f)] private float nearPlane = 0.05f;
        [SerializeField, Min(1f)] private float farPlane = 60f;

        [Tooltip("How far past the destination plane the oblique clip sits, in metres. Small on " +
                 "purpose: it exists to stop the destination wall z-fighting the clip plane, not " +
                 "to hide metres of the far room.")]
        [SerializeField, Range(0.001f, 0.2f)] private float clipPlaneOffset = 0.02f;

        [Tooltip("Stop rendering when the destination is authored facing the wrong way, rather " +
                 "than showing a view whose traversal would send the player out backwards. " +
                 "Clearing this renders anyway - the view is correct either way, but anything " +
                 "reading the destination's forward as \"into the room\" is not.")]
        [SerializeField] private bool refuseOnOrientationMismatch = true;

        [Header("Diagnostics")]
        [Tooltip("Off in production. On, the doorway's readout gains the full portal state: " +
                 "both poses, the camera-space clip plane, the buffer size and whether this " +
                 "frame actually rendered.")]
        [SerializeField] private bool debugReadout;

        // Every artistic number lives on the style, which the doorway that owns this surface
        // pushes down. Nothing here holds a second copy of a colour or a noise scale.
        private PortalStyle _style = new PortalStyle();

        private Transform _surface;
        private Camera _portalCamera;
        private RenderTexture _texture;
        private Material _material;
        private Camera _cachedSource;

        private bool _usingRealShader;
        private Renderer _surfaceRenderer;
        private float _opacity = 1f;
        private float _viewOpacity;
        private float _energyScale = 1f;
        // ZU als Ausgangszustand. Eine Wand ist zu, bis jemand sie aufreisst - und dieser
        // Standardwert war 1, also stand die Portalflaeche vom ersten Frame an sichtbar in
        // der Wand, bevor irgendjemand START INVESTIGATION gedrueckt hatte.
        private float _open;

        private Vector3 _planePoint;
        private Vector3 _planeNormal;
        private int _textureWidth, _textureHeight;
        private readonly Plane[] _sourceFrustum = new Plane[6];
        private Bounds _openingBounds;
        private bool _built;

        // Per-frame state, kept only so the debug readout can describe the frame that happened
        // rather than recompute a second, different one.
        private bool _visible;
        private bool _orientationValid = true;
        private bool _orientationReported;
        private float _lastRenderTime = -999f;
        private Vector4 _clipPlane;
        private int _viewSlot = -1;

        private static readonly int PortalTexId = Shader.PropertyToID("_PortalTex");

        /// <summary>The shader_feature that compiles the purchased-artwork layer in or out.</summary>
        private const string TexturedKeyword = "_PORTAL_TEXTURED";

        /// <summary>Where a player who walks through ends up. Read by whatever moves them.</summary>
        public Transform Destination => destination;

        /// <summary>Sets the far side at runtime, so one portal can be re-aimed at a generated house.</summary>
        public void SetDestination(Transform target) => destination = target;

        /// <summary>True once the surface, camera and buffer exist.</summary>
        public bool IsBuilt => _built;

        /// <summary>
        /// The plane the portal pair is built around: the surface's own transform, which is what
        /// <c>PortalToWorldInverse</c> maps through.
        ///
        /// <para>
        /// Exposed so the player can be carried through by the SAME transform the camera is
        /// posed with. Mapping the player against the doorway's root while the camera maps
        /// against the surface centre would put them out half an opening's height from where
        /// the view they walked into said they would be.
        /// </para>
        /// </summary>
        public Transform SurfacePlane => _surface;

        /// <summary>
        /// Sizes the hole to the doorway that owns it, before it is built.
        ///
        /// <para>
        /// The mesh, the captured plane and the culling bounds are all derived from these two
        /// values in <see cref="Build"/>, so changing them afterwards would leave a surface whose
        /// geometry and whose bounds disagree. Rather than half-apply, this refuses and says so:
        /// an opening that quietly kept the wrong size is a portal that does not fit its frame.
        /// </para>
        /// </summary>
        public void SetOpening(Vector2 size, Vector3 localPosition)
        {
            openingSize = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
            surfaceLocalPosition = localPosition;

            // Already built means the mesh, the captured plane and the culling bounds were all
            // derived from the OLD size, so they have to be derived again. This used to refuse
            // and log instead, which made the opening size un-tunable: the only way to see a
            // different width was to edit the default and restart, and an artistic control you
            // cannot turn while looking at the thing is not a control.
            if (_built)
                Rebuild();
        }

        /// <summary>
        /// Re-derives the mesh, the plane and the bounds from the current size and style.
        ///
        /// <para>
        /// Everything the portal does geometrically comes from these three, and they must be
        /// recomputed TOGETHER - a mesh resized without its bounds is a portal that culls itself
        /// at the old size, and a plane left behind is an opening whose crossing test is
        /// somewhere the player cannot see.
        /// </para>
        ///
        /// <para>
        /// An authoring action, not a per-frame one. Nothing in LateUpdate reaches this: the
        /// captured plane still cannot follow the player, it can only be re-authored.
        /// </para>
        /// </summary>
        public void Rebuild()
        {
            if (!_built || _surface == null)
                return;

            _surface.localPosition = surfaceLocalPosition;

            Vector2 quad = _style.QuadSize();
            float hx = quad.x * 0.5f;
            float hy = quad.y * 0.5f;

            var filter = _surface.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh mesh = filter.sharedMesh;
                mesh.vertices = new[]
                {
                    new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f),
                    new Vector3(-hx,  hy, 0f), new Vector3(hx,  hy, 0f)
                };
                mesh.RecalculateBounds();
            }

            _planePoint = _surface.position;
            _planeNormal = _surface.forward;

            if (_surfaceRenderer != null)
            {
                _openingBounds = _surfaceRenderer.bounds;
                _openingBounds.Expand(0.05f);
            }

            PushStyle();
        }

        /// <summary>
        /// Hands this surface the numbers it should look like.
        ///
        /// <para>
        /// Safe before the surface is built, which is the normal case: a portal is described
        /// while its GameObject is still inactive, and that is exactly when the material does
        /// not exist yet. The style is kept and re-applied by <see cref="Build"/>.
        /// </para>
        /// </summary>
        public void ApplyStyle(PortalStyle value)
        {
            if (value == null)
                return;

            _style = value;
            PushStyle();
        }

        /// <summary>The style this surface is drawing with. Never null.</summary>
        public PortalStyle Style => _style;

        /// <summary>
        /// How strongly the energy burns, 0 to 1. Written by whatever animates the opening.
        ///
        /// <para>
        /// A SCALE on the authored intensities rather than an absolute value, so the ramp
        /// cannot quietly overwrite what the artist set - the old <c>SetRimIntensity</c> took
        /// an absolute number, which meant the opening animation and the Inspector were two
        /// sources for one thing.
        /// </para>
        /// </summary>
        public void SetEnergy(float value01)
        {
            _energyScale = Mathf.Clamp01(value01);
            if (_material == null || !_usingRealShader)
                return;

            SetFloat("_CoreIntensity", _style.coreIntensity * _energyScale);
            SetFloat("_EnergyIntensity", _style.energyIntensity * _energyScale);
            SetFloat("_DistortionStrength", _style.viewDistortionStrength * _energyScale);
        }

        /// <summary>
        /// How far open the surface is, 0 to 1. Zero is an empty doorway; one is a hole.
        ///
        /// <para>
        /// Stored as well as pushed, because the opening is normally described while the
        /// object is still inactive - which is exactly when the material does not exist yet.
        /// </para>
        /// </summary>
        public void SetOpacity(float value)
        {
            _opacity = Mathf.Clamp01(value);
            if (_material != null && _usingRealShader)
                SetFloat("_Opacity", _opacity);
        }

        /// <summary>
        /// How far the FAR ROOM has faded in, 0 to 1, independently of the opening itself.
        ///
        /// <para>
        /// Two fades, not one, because the destination is not ready when the doorway starts
        /// reacting. At zero the centre is black behind a burning rim, which is what an opening
        /// that has not finished forming should look like; the view then comes up on its own
        /// when the far camera exists.
        /// </para>
        /// </summary>
        public void SetViewOpacity(float value)
        {
            _viewOpacity = Mathf.Clamp01(value);
            if (_material != null && _usingRealShader)
                SetFloat("_ViewOpacity", _viewOpacity);
        }

        /// <summary>
        /// How far the wall is torn open, 0 to 1.
        ///
        /// <para>
        /// At ZERO the breach has no size and the shader draws nothing anywhere: the wall is
        /// whole, with no hole in it. This is separate from opacity on purpose - opacity fades
        /// what is drawn, this decides whether there is an opening to draw at all.
        /// </para>
        /// </summary>
        public void SetOpen(float value01)
        {
            _open = Mathf.Clamp01(value01);
            if (_material != null && _usingRealShader)
                SetFloat("_Open", _open);

            // Ein geschlossenes Portal zeichnet NICHTS, und zwar indem der Renderer aus ist -
            // nicht indem der Shader alles auf durchsichtig rechnet.
            //
            // Der Unterschied ist genau der Fehler, der hier stand: die Zeile darueber setzt
            // die Shader-Eigenschaft nur, wenn der echte Portal-Shader gefunden wurde. Wurde
            // er es nicht, laeuft das Ersatzmaterial - und das zeichnet ein ganz normales,
            // undurchsichtiges Viereck von 1,2 x 2,4 m mitten in die Wand. Das sieht aus wie
            // eine Tuer, und es war eine, obwohl die echte Tuer laengst geloescht war.
            //
            // Ausgeschaltet ist ausgeschaltet, unabhaengig davon, welches Material haengt.
            if (_surfaceRenderer != null)
                _surfaceRenderer.enabled = _open > 0.001f;
        }

        /// <summary>Everything the style says, pushed at once. Silent when nothing is built.</summary>
        private void PushStyle()
        {
            if (_material == null || !_usingRealShader)
                return;

            SetColour("_CoreColor", _style.coreColor);
            SetColour("_EnergyColor", _style.energyColor);
            SetColour("_OuterColor", _style.outerColor);
            SetColour("_Tint", _style.viewTint);

            SetFloat("_RimWidth", _style.rimWidth);
            SetFloat("_RimSoftness", _style.rimSoftness);
            SetFloat("_NoiseScale", _style.noiseScale);
            SetFloat("_NoiseStrength", _style.noiseStrength);
            SetFloat("_NoiseSpeed", _style.noiseSpeed);
            SetFloat("_SecondaryNoiseScale", _style.secondaryNoiseScale);
            SetFloat("_SecondaryNoiseSpeed", _style.secondaryNoiseSpeed);
            SetFloat("_RotationSpeed", _style.rotationSpeed);
            SetFloat("_PulseSpeed", _style.pulseSpeed);
            SetFloat("_PulseStrength", _style.pulseStrength);

            SetFloat("_TearAmount", _style.tearAmount);
            SetFloat("_TearScale", _style.tearScale);

            PushArtwork();

            // The breach and the noise both need to know the shape of the quad they are drawn
            // on. Derived here rather than authored twice: the surface already knows its size.
            Vector2 fit = _style.ResolveFit();
            _material.SetVector("_Fit", new Vector4(fit.x, fit.y, 0f, 0f));
            SetFloat("_Aspect", openingSize.y > 0.001f ? openingSize.x / openingSize.y : 1f);

            SetFloat("_Opacity", _opacity);
            SetFloat("_ViewOpacity", _viewOpacity);
            SetFloat("_Open", _open);
            SetEnergy(_energyScale);
        }

        /// <summary>
        /// The purchased pack's artwork, or the procedural portal when there is none.
        ///
        /// <para>
        /// <b>The keyword is the switch, not the influence value.</b> Leaving
        /// <c>_PORTAL_TEXTURED</c> on with influence at zero still costs two texture samples on
        /// every portal pixel of every frame, on a phone, to produce a result identical to not
        /// sampling at all. The shader compiles the whole layer out instead.
        /// </para>
        ///
        /// <para>
        /// Called from <see cref="PushStyle"/> only. Keyword changes are a material variant
        /// switch, which is not something to do per frame.
        /// </para>
        /// </summary>
        private void PushArtwork()
        {
            bool active = _style.ArtworkActive;

            if (active)
                _material.EnableKeyword(TexturedKeyword);
            else
                _material.DisableKeyword(TexturedKeyword);

            // Written whether or not the keyword is on, so the material inspector shows what
            // WOULD be used and a stale texture from a previous adoption cannot linger bound.
            SetFloat("_Textured", active ? 1f : 0f);
            SetTexture("_EnergyTex", active ? _style.energyTexture : null);
            SetTexture("_MaskTex", active ? _style.maskTexture : null);
            SetFloat("_TexScale", _style.artworkScale);
            SetFloat("_TexSpeed", _style.artworkDrift);
            SetFloat("_TexInfluence", _style.artworkInfluence);
        }

        /// <summary>True when the finished portal shader is in use rather than the fallback.</summary>
        public bool UsingRealShader => _usingRealShader;

        /// <summary>
        /// What this surface actually resolved to, for the one diagnostic line the portal
        /// prints when it opens. Reports what IS, including the nulls - a diagnostic that only
        /// describes the healthy case cannot tell you which piece is missing.
        /// </summary>
        public string Describe()
        {
            string shaderName = _material != null && _material.shader != null
                ? _material.shader.name
                : "<none>";

            return "surface=" + (_surface != null ? "OK" : "MISSING") +
                   " shader=" + shaderName + (_usingRealShader ? "" : " (FALLBACK)") +
                   " material=" + (_material != null ? _material.name : "<none>") +
                   " portalCamera=" + (_portalCamera != null
                       ? (_portalCamera.enabled ? "rendering" : "idle")
                       : "<none>") +
                   " renderTexture=" + (_texture != null
                       ? _textureWidth + "x" + _textureHeight
                       : "<none>") +
                   " destination=" + (destination != null ? destination.name : "<unbound>") +
                   " playerCamera=" + (_cachedSource != null ? _cachedSource.name : "<unresolved>") +
                   DescribeDebug();
        }

        /// <summary>
        /// The whole portal state, and only when asked for.
        ///
        /// <para>
        /// Everything here answers a question that is otherwise guesswork from a screenshot:
        /// whether the frame rendered at all, where the virtual camera actually is, and what the
        /// oblique plane came out as. It is off by default because a portal that logs its matrix
        /// every frame is a portal nobody can read the console around.
        /// </para>
        /// </summary>
        private string DescribeDebug()
        {
            if (!debugReadout)
                return string.Empty;

            var cam = _portalCamera != null ? _portalCamera.transform : null;
            float interval = _style.RefreshInterval();

            return "\n    visible=" + _visible +
                   " rendered=" + (_portalCamera != null && _portalCamera.enabled) +
                   " lastRender=" + (Time.unscaledTime - _lastRenderTime).ToString("F3") + "s ago" +
                   " refresh=" + (interval <= 0f ? "every frame"
                                                 : (1f / interval).ToString("F0") + " Hz") +
                   "\n    orientation=" + (_orientationValid ? "OK" : "MISMATCHED") +
                   " refuseOnMismatch=" + refuseOnOrientationMismatch +
                   "\n    portalCameraPos=" + (cam != null ? cam.position.ToString("F2") : "<none>") +
                   " portalCameraRot=" + (cam != null ? cam.rotation.eulerAngles.ToString("F1") : "<none>") +
                   "\n    clipPlane(cameraSpace)=" + _clipPlane.ToString("F3") +
                   " offset=" + clipPlaneOffset.ToString("F3") + "m" +
                   "\n    fov=" + (_portalCamera != null ? _portalCamera.fieldOfView.ToString("F1") : "?") +
                   " aspect=" + (_portalCamera != null ? _portalCamera.aspect.ToString("F3") : "?") +
                   " planePoint=" + _planePoint.ToString("F2") +
                   " planeNormal=" + _planeNormal.ToString("F2");
        }

        private void Start()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                if (_portalCamera != null)
                    _portalCamera.targetTexture = null;
                _texture.Release();
                Destroy(_texture);
            }

            if (_material != null)
                Destroy(_material);
        }

        // ---- construction -------------------------------------------------------------------

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            BuildSurface();
            BuildCamera();
        }

        private void BuildSurface()
        {
            var go = new GameObject("Portal_Surface");
            _surface = go.transform;
            _surface.SetParent(transform, false);
            _surface.localPosition = surfaceLocalPosition;
            _surface.localRotation = Quaternion.identity;

            // The QUAD, not the opening: the drawn surface is deliberately larger so the
            // ragged edge and the outer glow have somewhere to go. Cut this to the opening and
            // the glow ends in a straight line at the mesh boundary, which is a portal with a
            // flat top.
            Vector2 quad = _style.QuadSize();
            float hx = quad.x * 0.5f;
            float hy = quad.y * 0.5f;

            // Four vertices written out rather than a Quad primitive: a Quad faces its own way
            // and carries its own UVs, and both matter here.
            var mesh = new Mesh { name = "Portal_Surface" };
            mesh.vertices = new[]
            {
                new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f),
                new Vector3(-hx,  hy, 0f), new Vector3(hx,  hy, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            _surfaceRenderer = renderer;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Captured once. Everything the portal does is built from these and neither is
            // written again, which is the guarantee that the opening cannot follow the player.
            _planePoint = _surface.position;
            _planeNormal = _surface.forward;

            _openingBounds = renderer.bounds;
            _openingBounds.Expand(0.05f);

            EnsureTexture(Core.LocalPlayerService.ResolveViewCamera());

            // A renderer with no material draws NOTHING. This used to return here when the
            // portal shader was missing, leaving a correctly built, correctly placed, correctly
            // sized quad that was simply invisible - and an invisible portal is
            // indistinguishable from a portal that was never created, which is the hardest kind
            // of bug to report. If the real shader is unavailable the opening still shows the
            // far room, through unlit, and says loudly that it is doing so.
            Shader shader = CiycShaders.Find(CiycShaders.Portal);
            bool real = shader != null;

            if (!real)
            {
                shader = CiycShaders.Find(CiycShaders.Unlit);
                Debug.LogError("[CIYC][Portal] The portal shader is not in this build, so the " +
                               "opening falls back to unlit: the far room is visible but there " +
                               "is no rim, no distortion and no fade. Put " +
                               CiycShaders.Portal + " on a material under Resources or in " +
                               "Always Included Shaders.");
            }

            if (shader == null)
            {
                Debug.LogError("[CIYC][Portal] No usable shader at all. The doorway will be " +
                               "invisible.");
                return;
            }

            _material = new Material(shader) { name = real ? "Portal_Runtime" : "Portal_Unlit_Fallback" };
            _usingRealShader = real;
            if (_surfaceRenderer != null)
                _surfaceRenderer.enabled = _open > 0.001f;

            SetTexture(real ? "_PortalTex" : "_BaseMap", _texture);
            PushStyle();

            renderer.sharedMaterial = _material;

            Debug.Log("[CIYC][Portal] surface built: " + openingSize.x.ToString("F2") + " x " +
                      openingSize.y.ToString("F2") + " m, shader=" + shader.name +
                      ", render texture " + _textureWidth + "x" + _textureHeight);
        }

        private void BuildCamera()
        {
            var go = new GameObject("Portal_Camera");
            go.transform.SetParent(transform, false);

            // Explicitly untagged: a second camera that answers to Camera.main is a camera that
            // renders the game from inside a wall.
            go.tag = "Untagged";

            _portalCamera = go.AddComponent<Camera>();
            _portalCamera.targetTexture = _texture;
            // Before the player's camera, so the surface carries this frame's view.
            _portalCamera.depth = -25f;
            _portalCamera.clearFlags = CameraClearFlags.Skybox;
            _portalCamera.cullingMask = viewLayers;
            _portalCamera.allowHDR = false;
            _portalCamera.allowMSAA = false;
            _portalCamera.useOcclusionCulling = false;

            var data = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data == null)
                data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            // Nothing else goes on this object, ever: no AudioListener, no PlayerLook, no
            // gameplay component. Built from a bare GameObject precisely so none can arrive.

            // The GameObject stays ACTIVE and the camera component is what gets switched.
            //
            // Unity renders an enabled camera automatically, in depth order, after LateUpdate -
            // which is where the pose and the clip plane are written - so "enable it on the
            // frames it should draw" already gives exact ordering, one render per frame at most,
            // and nothing to switch off afterwards. Deactivating the GameObject instead churns
            // the whole hierarchy through OnDisable/OnEnable every time the player looks away.
            //
            // The alternative - leaving it disabled and calling Render() by hand - is not
            // available: Camera.Render() is unsupported under a scriptable render pipeline and
            // logs on every call. Unity 6 replaces it with RenderPipeline.SubmitRenderRequest,
            // and that signature cannot be verified from here (every Unity documentation and
            // package host answers 403 in this environment), so it is not being guessed at.
            // SubmitRenderRequest belongs exactly here if someone with an Editor wants it.
            _portalCamera.enabled = false;
        }

        // ---- per frame ------------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_portalCamera == null || _surface == null || destination == null)
                return;

            Camera source = ResolveSource();
            if (source == null)
            {
                _portalCamera.enabled = false;
                return;
            }

            Vector3 eye = source.transform.position;

            // Which side of the portal plane the eye is on, and how far. A signed distance, not
            // a screen-space test: Camera.WorldToScreenPoint divides by a z that is negative
            // behind the camera, so a portal directly behind the player comes back with
            // plausible-looking coordinates on screen. A dot product cannot lie about the side.
            float inFront = Vector3.Dot(eye - _planePoint, _planeNormal);
            float distance = Vector3.Distance(eye, _planePoint);

            // Behind the opening, too far, or not on screen - each skips a whole second render
            // of the far room. The last one matters most: a player crossing the lobby faces the
            // portal for a fraction of the time. The frustum test is against the opening's own
            // bounds, so facing away is covered by the same test as looking past it.
            _visible = inFront > 0.05f && distance <= _style.renderDistance;
            if (_visible)
            {
                GeometryUtility.CalculateFrustumPlanes(source, _sourceFrustum);
                _visible = GeometryUtility.TestPlanesAABB(_sourceFrustum, _openingBounds);
            }

            if (!_visible)
            {
                _portalCamera.enabled = false;
                return;
            }

            // Cadence. Zero interval is every frame, which is what the top quality level asks
            // for; a phone can be told to refresh at 30 Hz while the game runs at 60. The buffer
            // keeps its last contents on a skipped frame, so the portal does not flicker - it
            // updates its parallax half as often, which is the trade being made.
            float interval = _style.RefreshInterval();
            if (interval > 0f && Time.unscaledTime - _lastRenderTime < interval)
            {
                _portalCamera.enabled = false;
                return;
            }

            // The lobby's other secondary view is the mirror, and a player standing where both
            // are on screen otherwise pays for three full renders of the room. Asked last, so a
            // portal that is off screen or on a skipped cadence frame never spends a share the
            // mirror needs; with one claimant this always grants.
            if (_viewSlot < 0)
                _viewSlot = SecondaryViewBudget.Reserve();

            if (!SecondaryViewBudget.MayRender(_viewSlot))
            {
                _portalCamera.enabled = false;
                return;
            }

            EnsureTexture(source);

            // ---- carry the player's pose through the pair -------------------------------------
            // The player's transform expressed in the portal's space, then re-read in the
            // destination's space. A rigid motion, so the basis stays right-handed and nothing
            // needs flipping - the one real difference from the mirror.
            Matrix4x4 throughPortal = destination.localToWorldMatrix *
                                      PortalToWorldInverse() *
                                      source.transform.localToWorldMatrix;

            // Column 3 is the translation, 2 the forward axis, 1 the up axis. Cast explicitly
            // rather than leaning on Vector4's implicit conversion to Vector3: the implicit one
            // silently drops w, and a matrix column read as a point when it is a direction is
            // the kind of mistake that shows up as a portal looking at the floor.
            Vector3 throughPosition = throughPortal.GetColumn(3);
            Vector3 throughForward = throughPortal.GetColumn(2);
            Vector3 throughUp = throughPortal.GetColumn(1);

            if (throughForward.sqrMagnitude < 1e-8f || throughUp.sqrMagnitude < 1e-8f)
                return;

            _portalCamera.transform.SetPositionAndRotation(
                throughPosition, Quaternion.LookRotation(throughForward, throughUp));

            _portalCamera.orthographic = false;
            _portalCamera.fieldOfView = source.fieldOfView;

            // Taken from the buffer, not from the source. The view is sampled in screen space -
            // the fragment shader divides ComputeScreenPos by w - so the image has to be shaped
            // like the screen. Width follows the source aspect in ResolveTextureSize, but it is
            // clamped there, and on a display wide enough for that clamp to bite the two numbers
            // stop agreeing. Reading the aspect back off the texture makes the render and the
            // lookup the same shape by construction rather than by arithmetic that holds most
            // of the time.
            _portalCamera.aspect = _textureHeight > 0
                ? (float)_textureWidth / _textureHeight
                : source.aspect;

            // ---- the orientation convention, checked rather than compensated for -------------
            //
            // One rule, for both transforms: local +Z points OUT of the visible surface. The
            // half turn folded into PortalToWorldInverse then lands the portal camera BEHIND the
            // destination plane looking through it, which is what makes walking in one side come
            // out forwards rather than backwards.
            //
            // So the camera must be on the destination's -Z side. If it is not, the destination
            // was authored facing the other way, and the consequences are not confined to this
            // component: destination.forward is read elsewhere as "the direction the player
            // emerges facing". Rendering anyway would produce a view that looks plausible and a
            // traversal that sends the player out backwards, which is worse than a hole that
            // says what is wrong with it.
            float destinationSide =
                Vector3.Dot(destination.forward,
                            _portalCamera.transform.position - destination.position);

            _orientationValid = destinationSide <= 0f;
            if (!_orientationValid)
            {
                ReportOrientation();
                if (refuseOnOrientationMismatch)
                {
                    _portalCamera.enabled = false;
                    return;
                }
            }

            _portalCamera.nearClipPlane = Mathf.Max(0.01f, nearPlane);

            // Set, but not what ends up bounding the view: the oblique matrix below moves the
            // near plane onto an arbitrary plane, and doing that destroys the conventional far
            // plane - the frustum becomes a shape whose far bound is wherever the skewed near
            // plane leaves it. That is inherent to the technique, not a bug to fix here, and
            // farPlane survives only as the value ResetProjectionMatrix starts from.
            _portalCamera.farClipPlane = farPlane;
            _portalCamera.ResetProjectionMatrix();

            // The near plane is moved onto the destination plane, so the far room's own doorway
            // wall - which sits right where this camera stands - is removed exactly, at the
            // surface, rather than approximately at whatever depth a near value lands on.
            _clipPlane = CameraSpacePlane(destination.position, destination.forward);
            _portalCamera.projectionMatrix = _portalCamera.CalculateObliqueMatrix(_clipPlane);

            // Last: everything the render depends on is written, so enabling it here is the one
            // point at which this frame's far room can be drawn. Unity picks it up after
            // LateUpdate and draws it before the player's camera, because its depth is lower.
            _portalCamera.enabled = true;
            _lastRenderTime = Time.unscaledTime;
        }

        /// <summary>
        /// Names both objects and the fix, once. A convention violation is a content bug and the
        /// person who can fix it is looking at a Hierarchy, not at this file.
        /// </summary>
        private void ReportOrientation()
        {
            if (_orientationReported)
                return;

            _orientationReported = true;
            Core.CIYCLog.Error(
                "[CIYC][Portal] Orientation mismatch between source '" + name +
                "' and destination '" + destination.name + "'. The convention is that local +Z " +
                "points OUT of the visible surface for both, which puts the portal camera " +
                "behind the destination plane; here it came out in front, so '" +
                destination.name + "' is facing the wrong way. Rotate it 180 degrees about Y. " +
                (refuseOnOrientationMismatch
                    ? "Refusing to render until then rather than showing a view whose traversal " +
                      "would send the player out backwards. Clear refuseOnOrientationMismatch on '" +
                      name + "' to render anyway."
                    : "Rendering anyway because refuseOnOrientationMismatch is off; the view is " +
                      "correct but anything reading " + destination.name +
                      ".forward as \"into the room\" is not."));
        }

        /// <summary>
        /// The portal's own world matrix, inverted, with a half turn folded in.
        ///
        /// <para>
        /// The half turn is what makes a pair of portals face each other rather than back to
        /// back. Without it the player walks in through one side and the far camera looks out
        /// of the destination the wrong way, which reads as the far room being mirrored.
        /// </para>
        /// </summary>
        private Matrix4x4 PortalToWorldInverse()
        {
            Matrix4x4 flip = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            return flip * _surface.worldToLocalMatrix;
        }

        /// <summary>
        /// The destination plane in the portal camera's own space, for the oblique clip.
        ///
        /// <para>
        /// <b>The side is derived, never assumed.</b> CalculateObliqueMatrix keeps the half-space
        /// the plane's normal points into and clips the other one away. The portal camera stands
        /// behind the destination plane looking through it, so the half to keep is the far room -
        /// the side the camera is NOT on. This used to pass <c>destination.forward</c> straight
        /// through, which is only correct while that transform happens to face into the room.
        /// <c>destination</c> is an authored or loaded Transform and nothing constrains its
        /// orientation, so that was a coin flip; and when it landed wrong the matrix clipped the
        /// entire room away and left the sky, which reads on screen as <i>a black hole behind a
        /// lit rim</i> - the portal interior being dark with the frame still burning.
        /// </para>
        ///
        /// <para>
        /// So the normal is flipped to point away from the camera, and the offset that lifts the
        /// plane clear of the destination wall follows the flipped normal - otherwise on the
        /// wrong-facing case it would push the plane the wrong way and shave 2 cm off the room
        /// instead of off the wall.
        /// </para>
        /// </summary>
        private Vector4 CameraSpacePlane(Vector3 point, Vector3 normal)
        {
            Vector3 cameraPosition = _portalCamera.transform.position;

            // Positive when the camera is on the side the normal points at. Mathf.Sign never
            // returns zero, so a camera exactly on the plane picks a side rather than
            // collapsing the plane to nothing.
            float onNormalSide = Mathf.Sign(Vector3.Dot(normal, cameraPosition - point));

            // Away from the camera: that is the half being kept.
            Vector3 kept = normal * -onNormalSide;

            Vector3 offsetPoint = point + kept * clipPlaneOffset;
            Matrix4x4 view = _portalCamera.worldToCameraMatrix;

            Vector3 viewPoint = view.MultiplyPoint(offsetPoint);
            Vector3 viewNormal = view.MultiplyVector(kept).normalized;

            return new Vector4(viewNormal.x, viewNormal.y, viewNormal.z,
                               -Vector3.Dot(viewPoint, viewNormal));
        }

        private Camera ResolveSource()
        {
            if (_cachedSource != null)
                return _cachedSource;

            _cachedSource = Core.LocalPlayerService.ResolveViewCamera();
            return _cachedSource;
        }

        /// <summary>
        /// The view buffer, allocated once and reallocated only when the size it should be
        /// actually changes - a quality switch, or a resized window.
        /// </summary>
        private void EnsureTexture(Camera source)
        {
            ResolveTextureSize(source, out int width, out int height);

            if (_texture != null && _textureWidth == width && _textureHeight == height &&
                _texture.IsCreated())
                return;

            if (_texture != null)
            {
                if (_portalCamera != null)
                    _portalCamera.targetTexture = null;
                _texture.Release();
                Destroy(_texture);
            }

            _textureWidth = width;
            _textureHeight = height;

            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "Portal_View",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _texture.Create();

            if (_portalCamera != null)
                _portalCamera.targetTexture = _texture;

            if (_material != null && _material.HasProperty(PortalTexId))
                _material.SetTexture(PortalTexId, _texture);
        }

        private void ResolveTextureSize(Camera source, out int width, out int height)
        {
            // The project's one quality convention, shared with MirrorCorner: where the active
            // level sits inside QualitySettings.names. Mobile lands low and gets half the
            // buffer; nothing here decides what "mobile" is on its own.
            float t = PortalStyle.QualityFraction01();
            int top = Mathf.Max(128, _style.viewResolution);
            int bottom = Mathf.Clamp(_style.minViewResolution, 128, top);
            int maxResolution = Mathf.Max(top, _style.maxViewResolution);

            // Named ends rather than "the top one, halved". Halving made the lowest level a
            // function of the highest, so raising the desktop buffer silently raised the phone's
            // too - which is the opposite of what a quality ladder is for.
            int target = Mathf.RoundToInt(Mathf.Lerp(bottom, top, t));

            height = Mathf.Clamp(target, 128, maxResolution);

            float aspect = source != null && source.aspect > 0.01f
                ? source.aspect
                : (Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7778f);

            width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 128,
                                Mathf.Max(128, maxResolution * 2));
        }

        private void SetTexture(string property, Texture value)
        {
            if (_material.HasProperty(property))
                _material.SetTexture(property, value);
            else
                Debug.LogWarning("[CIYC] Portal shader has no '" + property + "'.");
        }

        private void SetColour(string property, Color value)
        {
            if (_material.HasProperty(property))
                _material.SetColor(property, value);
        }

        private void SetFloat(string property, float value)
        {
            if (_material.HasProperty(property))
                _material.SetFloat(property, value);
        }
    }
}
