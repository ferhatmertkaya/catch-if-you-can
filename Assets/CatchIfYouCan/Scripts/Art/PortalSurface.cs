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

        [Tooltip("How far past the destination plane the oblique clip sits, in metres.")]
        [SerializeField, Range(0.001f, 0.2f)] private float clipPlaneOffset = 0.02f;

        // Every artistic number lives on the style, which the doorway that owns this surface
        // pushes down. Nothing here holds a second copy of a colour or a noise scale.
        private PortalStyle _style = new PortalStyle();

        private Transform _surface;
        private Camera _portalCamera;
        private RenderTexture _texture;
        private Material _material;
        private Camera _cachedSource;

        private bool _usingRealShader;
        private float _opacity = 1f;
        private float _viewOpacity;
        private float _energyScale = 1f;

        private Vector3 _planePoint;
        private Vector3 _planeNormal;
        private int _textureWidth, _textureHeight;
        private readonly Plane[] _sourceFrustum = new Plane[6];
        private Bounds _openingBounds;
        private bool _built;

        private static readonly int PortalTexId = Shader.PropertyToID("_PortalTex");

        /// <summary>Where a player who walks through ends up. Read by whatever moves them.</summary>
        public Transform Destination => destination;

        /// <summary>Sets the far side at runtime, so one portal can be re-aimed at a generated house.</summary>
        public void SetDestination(Transform target) => destination = target;

        /// <summary>True once the surface, camera and buffer exist.</summary>
        public bool IsBuilt => _built;

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
            if (_built)
            {
                Debug.LogWarning("[CIYC][Portal] SetOpening after the surface was built is " +
                                 "ignored - the mesh and its bounds are already derived from " +
                                 "the old size. Size the opening before the object is enabled.");
                return;
            }

            openingSize = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
            surfaceLocalPosition = localPosition;
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

            // The oval and the noise both need to know the shape of the quad they are drawn on.
            // Derived here rather than authored twice: the surface already knows its own size.
            Vector2 fit = new Vector2(Mathf.Clamp(_style.ovalFit.x, 0.05f, 1f),
                                      Mathf.Clamp(_style.ovalFit.y, 0.05f, 1f));
            _material.SetVector("_Fit", new Vector4(fit.x, fit.y, 0f, 0f));
            SetFloat("_Aspect", openingSize.y > 0.001f ? openingSize.x / openingSize.y : 1f);

            SetFloat("_Opacity", _opacity);
            SetFloat("_ViewOpacity", _viewOpacity);
            SetEnergy(_energyScale);
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
                       ? (_portalCamera.gameObject.activeSelf ? "active" : "idle")
                       : "<none>") +
                   " renderTexture=" + (_texture != null
                       ? _textureWidth + "x" + _textureHeight
                       : "<none>") +
                   " destination=" + (destination != null ? destination.name : "<unbound>") +
                   " playerCamera=" + (_cachedSource != null ? _cachedSource.name : "<unresolved>");
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

            float hx = openingSize.x * 0.5f;
            float hy = openingSize.y * 0.5f;

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
            go.SetActive(false);
        }

        // ---- per frame ------------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_portalCamera == null || _surface == null || destination == null)
                return;

            Camera source = ResolveSource();
            if (source == null)
            {
                _portalCamera.gameObject.SetActive(false);
                return;
            }

            Vector3 eye = source.transform.position;
            float inFront = Vector3.Dot(eye - _planePoint, _planeNormal);
            float distance = Vector3.Distance(eye, _planePoint);

            // Behind the opening, too far, or not on screen - each skips a whole second render
            // of the far room. The last one matters most: a player crossing the lobby faces the
            // portal for a fraction of the time.
            bool visible = inFront > 0.05f && distance <= _style.renderDistance;
            if (visible)
            {
                GeometryUtility.CalculateFrustumPlanes(source, _sourceFrustum);
                visible = GeometryUtility.TestPlanesAABB(_sourceFrustum, _openingBounds);
            }

            if (_portalCamera.gameObject.activeSelf != visible)
                _portalCamera.gameObject.SetActive(visible);
            if (!visible)
                return;

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
            _portalCamera.aspect = source.aspect;
            _portalCamera.nearClipPlane = Mathf.Max(0.01f, nearPlane);
            _portalCamera.farClipPlane = farPlane;
            _portalCamera.ResetProjectionMatrix();

            // The near plane is moved onto the destination plane, so the far room's own doorway
            // wall - which sits right where this camera stands - is removed exactly, at the
            // surface, rather than approximately at whatever depth a near value lands on.
            _portalCamera.projectionMatrix = _portalCamera.CalculateObliqueMatrix(
                CameraSpacePlane(destination.position, destination.forward));
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

        /// <summary>The destination plane in the portal camera's own space, for the oblique clip.</summary>
        private Vector4 CameraSpacePlane(Vector3 point, Vector3 normal)
        {
            Vector3 offsetPoint = point + normal * clipPlaneOffset;
            Matrix4x4 view = _portalCamera.worldToCameraMatrix;

            Vector3 viewPoint = view.MultiplyPoint(offsetPoint);
            Vector3 viewNormal = view.MultiplyVector(normal).normalized;

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
            int resolution = Mathf.Max(128, _style.viewResolution);
            int maxResolution = Mathf.Max(resolution, _style.maxViewResolution);
            int target = Mathf.RoundToInt(Mathf.Lerp(resolution * 0.5f, resolution, t));

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
