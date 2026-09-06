using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// A lit corner with a mirror in it, so the player can look at themselves.
    ///
    /// <para>
    /// Built in code rather than authored, because the mirror is not really geometry: it is a
    /// second camera, a render texture and a projection matrix that has to be rebuilt every frame
    /// from where the player is standing. Only the lamp and the frame are objects, and building
    /// those here too keeps the whole feature in one file that can be moved by moving one
    /// transform.
    /// </para>
    ///
    /// <para>
    /// <b>THE MIRROR NEVER MOVES.</b> Not its position, not its rotation, not its normal. Nothing
    /// here reads where the player is and turns the glass towards them; there is no
    /// <c>LookAt</c>, no <c>LookRotation</c> towards a player, and the glass is not parented to
    /// anything that follows one. The plane is captured once when the glass is built - see
    /// <c>_planePoint</c> and <c>_planeNormal</c> - so it cannot start tracking anybody even if
    /// something later writes to the transform. Only the hidden reflection camera moves.
    /// </para>
    ///
    /// <para>
    /// The reflection is the player's own camera reflected across that fixed plane: position,
    /// forward and up each mirrored, and the pose built from the reflected forward and up. It
    /// then renders with the player's field of view and aspect, and the result is sampled in
    /// <em>screen space</em>, which is what makes it a mirror rather than a second view of the
    /// room: the ray through any screen pixel of the glass is the reflection of the player's own
    /// ray through that same pixel.
    /// </para>
    ///
    /// <para>
    /// <b>This replaced an off-axis frustum, and that is what was wrong.</b> The camera used to be
    /// locked to look straight out along the mirror normal with a frustum pinned to the four glass
    /// corners. The maths is sound and the mirror plane never rotated even then - but the frustum
    /// shears further and further as the player moves sideways, and a hard-sheared projection
    /// keystones the room. That is what read as the reflection swinging round a pivot. Reflecting
    /// the camera's whole pose instead means the projection stays an ordinary symmetric
    /// perspective at every player position, and there is nothing left to shear.
    /// </para>
    ///
    /// <para>
    /// There is no <c>GL.invertCulling</c> here and there must not be. Reflecting forward, right
    /// and up across a plane gives a <em>left-handed</em> basis, which no Transform can hold;
    /// building the pose from reflected forward and up gives a proper rotation whose right vector
    /// is the negative of the reflected one. The shader flips screen u to undo exactly that, which
    /// is exact for a symmetric frustum. So nothing renders with inverted winding, and nothing can
    /// be left inverted after an early return.
    /// </para>
    ///
    /// <para>
    /// <b>The near plane is moved onto the glass.</b> The reflection camera sits behind the glass
    /// looking out through it, so everything between it and the room - the frame, the wall the
    /// mirror hangs on, and the glass itself - is in front of the lens. The frame alone is a
    /// solid slab wider and taller than the glass sitting exactly on the image plane, which fills
    /// the frustum completely: that is a mirror that renders one flat rectangle of dark wood and
    /// nothing else. Worse, the glass is textured with the very render texture being drawn, so
    /// what little got past the frame was a texture sampling itself. An oblique projection puts
    /// the near plane on the mirror surface exactly, which removes all three at once and is what
    /// stops the reflection ever feeding back into itself.
    /// </para>
    ///
    /// <para>
    /// A second camera rendering the room every frame is not free, so it only runs when the
    /// player is close enough to see anything in it and is on the reflective side of the glass.
    /// Beyond that the camera is switched off entirely rather than rendering to a texture nobody
    /// is looking at.
    /// </para>
    ///
    /// <para>
    /// The lamp is an ordinary point light. Additional lights cast no shadows in this project's
    /// URP asset, so it lights the corner flatly and brightly rather than dramatically - which is
    /// what this corner is for. It is the one place in the room meant to be easy to see in.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Mirror Corner")]
    public sealed class MirrorCorner : MonoBehaviour
#if UNITY_EDITOR
        , IEditorPreviewBuildable
#endif
    {
        [Header("Mirror")]
        [Tooltip("Glass size in metres. Small on purpose - it is a corner mirror, not a wall of " +
                 "them - but tall enough to still show the player when they crouch.")]
        [SerializeField] private Vector2 glassSize = new Vector2(0.72f, 1.4f);

        [Tooltip("Where the glass sits, relative to this transform. Local +Z is the direction it " +
                 "faces, so this object should be turned to face into the room.")]
        [SerializeField] private Vector3 glassLocalPosition = new Vector3(0f, 1.25f, 0.02f);

        [Tooltip("Height of the reflection buffer in pixels, at the top quality level. The width " +
                 "follows the screen's aspect, because the reflection is sampled in screen " +
                 "space: a square buffer for a 0.72 x 1.4 glass spent half its pixels on nothing " +
                 "and stretched what was left. Lower quality levels step this down - see " +
                 "ResolveTextureSize.")]
        [SerializeField, Min(128)] private int resolution = 1024;

        [Tooltip("Cap on the reflection buffer height. 2048 is the Ultra tier; it is reached " +
                 "only when the quality level asks for it, never forced on every platform.")]
        [SerializeField, Min(128)] private int maxResolution = 2048;

        [Tooltip("Stop rendering beyond this. The reflection is a second pass over the room, so " +
                 "it should not run while the player is on the other side of the house.")]
        [SerializeField, Min(1f)] private float renderDistance = 7f;

        [Tooltip("What the reflection is allowed to see. UI, post-processing volumes and " +
                 "occlusion helpers are off by default: none of them belongs in a mirror, and " +
                 "the first one would put the HUD in it. Player and Ghost stay ON - being able " +
                 "to look at yourself is what this corner is for, and a ghost in the glass is " +
                 "the whole point of a mirror in a horror game.")]
        [SerializeField] private LayerMask reflectionLayers = ~((1 << 5) | (1 << 15) | (1 << 16));

        [Tooltip("Beyond this, the reflection is drawn without shadows. A shadow map is a whole " +
                 "extra pass, and at three metres in a 70 cm mirror nobody can tell. Zero keeps " +
                 "shadows at every distance.")]
        [SerializeField, Min(0f)] private float shadowDistance = 3f;

        [Tooltip("Near plane the reflection camera is built at before the oblique clip moves the " +
                 "real one onto the glass. Small, because the reflected eye can end up close to " +
                 "the plane when the player stands against the mirror.")]
        [SerializeField, Min(0.01f)] private float nearPlane = 0.05f;

        [SerializeField, Min(1f)] private float farPlane = 40f;

        [Tooltip("How far past the glass the oblique clip plane sits, in metres. Just enough " +
                 "that the glass itself, which carries the very texture being drawn, falls on " +
                 "the far side of it.")]
        [SerializeField, Range(0.001f, 0.05f)] private float clipPlaneOffset = 0.01f;

        [Tooltip("Draw the mirror plane, both eye positions and the four glass corners in the " +
                 "scene view. Off for shipping; on when the reflection is misbehaving.")]
        [SerializeField] private bool drawDebug;

        [Header("Glass")]
        [Tooltip("What the reflection is multiplied by on its way onto the glass. Near white and " +
                 "slightly warm. The glass is unlit now, so this is the only thing between the " +
                 "reflection and the screen - a dark tint here takes the reflection away and " +
                 "nothing gives it back.")]
        [SerializeField] private Color glassTint = new Color(0.94f, 0.92f, 0.88f);

        [Tooltip("Overall reflection brightness. Slightly under one: old silvering returns a " +
                 "little less than it receives.")]
        [SerializeField, Range(0.4f, 1.4f)] private float glassExposure = 0.94f;

        [Tooltip("Uneven silvering, as a fraction. Restrained on purpose - the reflection has to " +
                 "stay the thing you look at, not the dirt in front of it.")]
        [SerializeField, Range(0f, 0.5f)] private float glassGrime = 0.13f;

        [Tooltip("Dirt gathered where the glass meets the frame.")]
        [SerializeField, Range(0f, 0.7f)] private float glassEdgeDirt = 0.34f;

        [Tooltip("Sparse hairline scratches. Very low; anything visible as a pattern is worse " +
                 "than none.")]
        [SerializeField, Range(0f, 0.25f)] private float glassScratches = 0.05f;

        [Tooltip("How much colour age has taken out of the reflection.")]
        [SerializeField, Range(0f, 0.6f)] private float glassDesaturation = 0.12f;

        [Tooltip("Log what the glass material ended up as, once, on build. Left on: it is one " +
                 "line and it is the difference between seeing a magenta mirror and knowing why.")]
        [SerializeField] private bool logState = true;

        [Header("Frame")]
        [SerializeField] private float frameBorder = 0.06f;
        [SerializeField] private float frameDepth = 0.05f;
        [SerializeField] private Color frameColor = new Color(0.16f, 0.12f, 0.09f);

        [Header("Standing lamp")]
        [SerializeField] private bool buildLamp = true;

        [Tooltip("Where the lamp stands, relative to this transform.")]
        [SerializeField] private Vector3 lampLocalPosition = new Vector3(0.85f, 0f, 0.35f);

        [SerializeField] private float lampHeight = 1.55f;
        [SerializeField] private Color lampColor = new Color(1f, 0.87f, 0.7f);

        [Tooltip("Bright on purpose. This corner exists so the player can actually see " +
                 "themselves, which a room lit for a horror game otherwise does not allow.")]
        [SerializeField] private float lampIntensity = 6.5f;

        [SerializeField] private float lampRange = 7f;
        [SerializeField] private Color lampShadeColor = new Color(0.86f, 0.78f, 0.62f);

        [Header("Fill")]
        [Tooltip("A softer light above the mirror aimed back into the room, so the player is lit " +
                 "from the front rather than silhouetted against the lamp beside them. This is " +
                 "what actually makes a face readable in the glass, and it is on: the URP asset " +
                 "now allows eight additional lights per object, where the four it allowed " +
                 "before were already spent on the room's lamp and fill, the standing lamp here " +
                 "and the torch.")]
        [SerializeField] private bool buildFill = true;

        [SerializeField] private Vector3 fillLocalPosition = new Vector3(0f, 2.15f, 0.12f);
        [SerializeField] private float fillIntensity = 3f;

        [Tooltip("Kept small deliberately. This light's job is the metre or so in front of the " +
                 "glass; a range that reaches across the room is a light that has to be " +
                 "considered for every object in it, for no visible gain.")]
        [SerializeField] private float fillRange = 4f;

        /// <summary>How far behind the glass the frame sits, in metres. Enough to not z-fight.</summary>
        private const float FrameSetback = 0.006f;

        private Transform _surface;
        private Vector3 _cornerBottomLeft, _cornerBottomRight, _cornerTopLeft;
        private Camera _mirrorCamera;
        private RenderTexture _texture;
        private Material _glassMaterial;
        private bool _built;

        /// <summary>
        /// The mirror plane, taken once when the glass is built and never taken again.
        ///
        /// <para>
        /// <b>This is the immutable thing.</b> Reading it from <c>_surface</c> every frame would
        /// work today and would quietly start tracking the player the day somebody wrote to that
        /// transform. Captured once, it cannot: the plane the reflection is built from is the
        /// plane the glass was built on, whatever anything else does afterwards.
        /// </para>
        /// </summary>
        private Vector3 _planePoint;
        private Vector3 _planeNormal;

        private Camera _cachedSource;
        private int _textureWidth, _textureHeight;

        /// <summary>
        /// The glass in world space, taken once. The mirror never moves, so neither do these.
        /// </summary>
        private Bounds _glassBounds;

        // The lobby's other secondary view is the portal, and neither knew the other
        // existed. This is the slot the shared arbiter grants by; the mirror's own plane,
        // capture and culling are untouched by it.
        private int _viewSlot = -1;

        /// <summary>
        /// Reused every frame. The <c>Plane[]</c>-returning overload of
        /// <see cref="GeometryUtility.CalculateFrustumPlanes(Camera)"/> allocates an array per
        /// call, which in LateUpdate is six planes of garbage per frame forever.
        /// </summary>
        private readonly Plane[] _sourceFrustum = new Plane[6];

        private UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _cameraData;
        private bool _shadowsOn = true;

        private static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");

        private void Start()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                if (_mirrorCamera != null)
                    _mirrorCamera.targetTexture = null;
                _texture.Release();
                Destroy(_texture);
            }

            if (_glassMaterial != null)
                Destroy(_glassMaterial);
        }

        // ---- construction --------------------------------------------------------------------

        private void Build()
        {
            Build(withReflection: true);
        }

        /// <summary>
        /// <paramref name="withReflection"/> is false only for an authoring preview. Everything
        /// else is identical, in the same order, from the same measurements - the mirror the
        /// editor shows is the mirror the game builds, minus the camera and the RenderTexture
        /// that would otherwise be rendering on every repaint.
        /// </summary>
        private void Build(bool withReflection)
        {
            if (_built)
                return;
            _built = true;

            // One shader, and it is deliberately the one the rest of the room is built from.
            //
            // <para>
            // This is the whole of the magenta mirror. Shader.Find only returns what is actually
            // in the build, and a player build only contains the shaders its materials ask for:
            // twenty-eight of this project's materials are Universal Render Pipeline/Lit and not
            // one is Universal Render Pipeline/Unlit, so Unlit exists in the editor and does not
            // exist on the device. The old fallback then reached for Unlit/Texture - a Built-in
            // Render Pipeline shader, which is in Always Included Shaders and so does resolve -
            // and a built-in shader under URP draws solid magenta. It worked every time in the
            // editor and was magenta every time on the phone.
            // </para>
            var lit = CiycShaders.FindLit();

            BuildFrame(lit);
            BuildGlass();

            if (withReflection)
                BuildCamera();

            if (buildLamp)
                BuildLamp(lit);
            if (buildFill)
                BuildFill();
        }

#if UNITY_EDITOR
        void IEditorPreviewBuildable.BuildEditorPreview() => Build(withReflection: false);

        void IEditorPreviewBuildable.ForgetEditorPreview()
        {
            _built = false;
            _surface = null;
            _glassMaterial = null;
        }
#endif

        private void BuildFrame(Shader lit)
        {
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Mirror_Frame";
            frame.transform.SetParent(transform, false);
            // Set back from the glass rather than flush with it. Flush is two opaque surfaces on
            // one plane, which is a z-fight that reads as the mirror flickering.
            frame.transform.localPosition = glassLocalPosition -
                                            new Vector3(0f, 0f, frameDepth * 0.5f + FrameSetback);
            frame.transform.localScale = new Vector3(glassSize.x + frameBorder * 2f,
                                                     glassSize.y + frameBorder * 2f,
                                                     frameDepth);

            if (lit == null)
                return;

            var material = new Material(lit) { name = "Mirror_Frame_Runtime" };
            material.color = frameColor;
            frame.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>
        /// The glass, as a mesh built here rather than a Quad primitive. Unity's Quad faces its
        /// own way and carries its own UVs, and both of those matter to a mirror; four vertices
        /// written out are four things that cannot be wrong.
        /// </summary>
        private void BuildGlass()
        {
            var go = new GameObject("Mirror_Glass");
            _surface = go.transform;
            _surface.SetParent(transform, false);
            _surface.localPosition = glassLocalPosition;
            _surface.localRotation = Quaternion.identity;

            float hx = glassSize.x * 0.5f;
            float hy = glassSize.y * 0.5f;

            var mesh = new Mesh { name = "Mirror_Glass" };
            mesh.vertices = new[]
            {
                new Vector3(-hx, -hy, 0f),
                new Vector3( hx, -hy, 0f),
                new Vector3(-hx,  hy, 0f),
                new Vector3( hx,  hy, 0f)
            };
            // Plain 0-1. They are the ageing's coordinates, not the reflection's - the
            // reflection is sampled in screen space, which is what makes it a mirror.
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            // Wound clockwise as seen from local +Z, which is Unity's front-facing order and
            // the side that faces the room. Worked out on paper rather than by rotating a Quad
            // until it looked right: a mirror that is invisible from the front and solid from
            // behind is a five-minute mystery every time.
            // Kept, in the glass's own space, as the frustum's four corners. Taken from the
            // mesh that is actually drawn rather than from the size field and a lossy scale:
            // those two agree only while nothing above this transform is rotated or scaled
            // unevenly, and a frustum built from corners the player cannot see is a reflection
            // that slides against its own frame.
            _cornerBottomLeft = new Vector3(-hx, -hy, 0f);
            _cornerBottomRight = new Vector3(hx, -hy, 0f);
            _cornerTopLeft = new Vector3(-hx, hy, 0f);

            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // The plane, taken once, from the glass as it has just been placed. Everything the
            // reflection does is built from these three vectors and none of them is ever written
            // again - which is the guarantee that the mirror cannot start following the player.
            _planePoint = _surface.position;
            _planeNormal = _surface.forward;

            // The glass in world space, for the "is the mirror even on screen" test. Taken once
            // for the same reason the plane is: the mirror does not move. Expanded slightly
            // because a flat quad's bounds are zero-thick on one axis, and a zero-extent box is
            // an awkward thing to hand a frustum test.
            _glassBounds = renderer.bounds;
            _glassBounds.Expand(0.05f);

            EnsureTexture(Core.LocalPlayerService.ResolveViewCamera());

            // The mirror shader, not URP/Lit. A lit mirror is a surface the room's light falls on,
            // which takes a bite out of the reflection before anybody sees it - the old build
            // compensated with a very bright lamp and still lost contrast. This one is unlit, so
            // the tint below is the only thing between the reflection and the screen.
            var mirrorShader = CiycShaders.Find(CiycShaders.PlanarMirror);
            if (mirrorShader == null)
                return;

            // Every write is guarded, because a silently ignored SetTexture is how a mirror ends
            // up showing the tint and nothing else. The ageing lives in the shader rather than in
            // a texture: a grime map would have to be authored, imported and kept, and this is a
            // 70 cm mirror.
            _glassMaterial = new Material(mirrorShader) { name = "Mirror_Glass_Runtime" };
            SetTextureIfPresent(_glassMaterial, "_ReflectionTex", _texture);
            SetColorIfPresent(_glassMaterial, "_Tint", glassTint);
            SetFloatIfPresent(_glassMaterial, "_Exposure", glassExposure);
            SetFloatIfPresent(_glassMaterial, "_GrimeStrength", glassGrime);
            SetFloatIfPresent(_glassMaterial, "_EdgeDirt", glassEdgeDirt);
            SetFloatIfPresent(_glassMaterial, "_ScratchStrength", glassScratches);
            SetFloatIfPresent(_glassMaterial, "_Desaturate", glassDesaturation);
            renderer.sharedMaterial = _glassMaterial;

            LogState(renderer);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture value)
        {
            if (material.HasProperty(property))
                material.SetTexture(property, value);
            else
                Debug.LogWarning("[CIYC] Mirror glass shader has no '" + property + "'.");
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        /// <summary>
        /// One line, once, saying what the mirror actually ended up being. Cheap to leave in and
        /// the difference between "it is magenta" and knowing why in one look at a device log.
        /// </summary>
        private void LogState(Renderer renderer)
        {
            if (!logState)
                return;

            Shader shader = _glassMaterial != null ? _glassMaterial.shader : null;
            Debug.Log("[CIYC] Mirror glass: shader=" + (shader != null ? shader.name : "<none>") +
                      " supported=" + (shader != null && shader.isSupported) +
                      " hasReflectionTex=" +
                      (_glassMaterial != null && _glassMaterial.HasProperty("_ReflectionTex")) +
                      " rtCreated=" + (_texture != null && _texture.IsCreated()) +
                      " rt=" + _textureWidth + "x" + _textureHeight +
                      " rendererEnabled=" + renderer.enabled, this);
        }

        private void BuildCamera()
        {
            var go = new GameObject("Mirror_Camera");
            go.transform.SetParent(transform, false);

            // Explicitly untagged. A second camera that picks up MainCamera is a camera that
            // Camera.main might answer with, and this one renders the room from inside a wall.
            go.tag = "Untagged";

            _mirrorCamera = go.AddComponent<Camera>();
            _mirrorCamera.targetTexture = _texture;
            // Before the player's camera, so the texture on the glass is this frame's rather than
            // last frame's.
            _mirrorCamera.depth = -20f;
            _mirrorCamera.clearFlags = CameraClearFlags.Skybox;
            _mirrorCamera.nearClipPlane = nearPlane;
            _mirrorCamera.farClipPlane = farPlane;
            _mirrorCamera.allowHDR = false;
            _mirrorCamera.allowMSAA = false;
            _mirrorCamera.useOcclusionCulling = false;

            // Render fewer things, which is the cheapest optimisation there is. The HUD in
            // particular must never appear in the glass.
            _mirrorCamera.cullingMask = reflectionLayers;

            _cameraData = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (_cameraData == null)
                _cameraData = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // Grading and vignette are the player's view of the room, not the room itself, and
            // running them twice costs a full pass for something nobody can see at this size.
            _cameraData.renderPostProcessing = false;
            _cameraData.renderShadows = true;
            _shadowsOn = true;

            // Nothing else goes on this object, ever: no AudioListener (two of them is a Unity
            // warning and a wrong mix), no PlayerLook, no gameplay component. It is built from a
            // bare GameObject rather than a copy of the player's camera precisely so that none of
            // those can arrive by inheritance.
            go.SetActive(false);
        }

        private void BuildLamp(Shader lit)
        {
            var lamp = new GameObject("Standing_Lamp");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = lampLocalPosition;

            Material metal = null;
            Material shade = null;
            if (lit != null)
            {
                metal = new Material(lit) { name = "Lamp_Metal_Runtime" };
                metal.color = new Color(0.12f, 0.11f, 0.1f);

                shade = new Material(lit) { name = "Lamp_Shade_Runtime" };
                shade.color = lampShadeColor;
                shade.EnableKeyword("_EMISSION");
                shade.SetColor("_EmissionColor", lampColor * 1.6f);
            }

            AddPart(lamp.transform, PrimitiveType.Cylinder, "Base",
                    new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.02f, 0.3f), metal);
            AddPart(lamp.transform, PrimitiveType.Cylinder, "Pole",
                    new Vector3(0f, lampHeight * 0.5f, 0f),
                    new Vector3(0.04f, lampHeight * 0.5f, 0.04f), metal);
            AddPart(lamp.transform, PrimitiveType.Cylinder, "Shade",
                    new Vector3(0f, lampHeight + 0.14f, 0f), new Vector3(0.36f, 0.16f, 0.36f), shade);

            var bulb = new GameObject("Bulb");
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localPosition = new Vector3(0f, lampHeight + 0.06f, 0f);

            var light = bulb.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lampColor;
            light.intensity = lampIntensity;
            light.range = lampRange;
            light.shadows = LightShadows.None;   // additional-light shadows are off in the URP asset
        }

        private void BuildFill()
        {
            var go = new GameObject("Mirror_Fill");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = fillLocalPosition;
            // Out into the room and angled down, at whoever is standing in front of the glass.
            // Aiming it at the mirror would light an unlit surface and the wall behind it.
            go.transform.localRotation = Quaternion.LookRotation(
                new Vector3(0f, -0.35f, 1f).normalized, Vector3.up);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.98f, 0.93f, 0.86f);
            light.intensity = fillIntensity;
            light.range = fillRange;
            light.spotAngle = 78f;
            light.innerSpotAngle = 40f;
            light.shadows = LightShadows.None;
        }

        private static void AddPart(Transform parent, PrimitiveType type, string name,
                                    Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            if (material != null)
                part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---- per frame -----------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_mirrorCamera == null || _surface == null)
                return;

            // The reflection is only correct from the eye it is drawn for. Camera.main answered
            // "any tagged camera", which in the lobby was the menu camera until the player
            // spawned. Cached, and only re-resolved when the one we have has gone.
            Camera source = ResolveSource();
            if (source == null)
            {
                _mirrorCamera.gameObject.SetActive(false);
                return;
            }

            Vector3 eye = source.transform.position;
            float inFront = Vector3.Dot(eye - _planePoint, _planeNormal);
            float distance = Vector3.Distance(eye, _planePoint);

            // Three tests, cheapest first, and every one of them skips a whole second render of
            // the room.
            //
            //   1. Behind the glass. The quad is single-sided, so from back there the mirror is
            //      not merely unlit - it is not drawn at all.
            //   2. Too far to be worth it.
            //   3. Not on screen. This is the one that matters most: a player walking round the
            //      lobby faces away from the mirror most of the time, and until now that still
            //      cost a full render of the room every single frame. The glass is a fixed box,
            //      so this is six plane tests against an AABB that never moves.
            bool visible = inFront > 0.05f && distance <= renderDistance;

            if (visible)
            {
                GeometryUtility.CalculateFrustumPlanes(source, _sourceFrustum);
                visible = GeometryUtility.TestPlanesAABB(_sourceFrustum, _glassBounds);
            }

            // Asked only once the mirror already wants to render, so a mirror the player is
            // not looking at never spends a share the portal needs. With one claimant this
            // always grants; with two on a low quality level they alternate frames.
            if (visible)
            {
                if (_viewSlot < 0)
                    _viewSlot = SecondaryViewBudget.Reserve();
                visible = SecondaryViewBudget.MayRender(_viewSlot);
            }

            if (_mirrorCamera.gameObject.activeSelf != visible)
                _mirrorCamera.gameObject.SetActive(visible);
            if (!visible)
                return;

            // A shadow map is a whole extra pass. In a 70 cm mirror across the room, it buys
            // nothing. Set only when it changes, so this is not a write per frame.
            bool wantShadows = shadowDistance <= 0f || distance <= shadowDistance;
            if (_cameraData != null && _shadowsOn != wantShadows)
            {
                _cameraData.renderShadows = wantShadows;
                _shadowsOn = wantShadows;
            }

            EnsureTexture(source);

            // ---- the reflected pose ----------------------------------------------------------
            // Position, forward and up, each mirrored across the fixed plane. The plane comes
            // from _planePoint and _planeNormal, which were taken once when the glass was built:
            // nothing in this method reads the mirror's transform, so nothing in this method can
            // move it.
            Vector3 reflectedPosition = ReflectPoint(eye);
            Vector3 reflectedForward = ReflectDirection(source.transform.forward);
            Vector3 reflectedUp = ReflectDirection(source.transform.up);

            // Degenerate only if the player is looking exactly along the plane's normal with
            // their up vector parallel to their forward, which cannot happen - but LookRotation
            // with a zero or parallel pair silently returns identity, and an identity mirror
            // camera is a reflection pointing at a wall.
            if (reflectedForward.sqrMagnitude < 1e-8f || reflectedUp.sqrMagnitude < 1e-8f)
                return;

            _mirrorCamera.transform.SetPositionAndRotation(
                reflectedPosition, Quaternion.LookRotation(reflectedForward, reflectedUp));

            // ---- the projection --------------------------------------------------------------
            // The player's own lens. An ordinary symmetric perspective at every player position,
            // which is the whole difference from the off-axis frustum this replaced: there is no
            // shear to grow as the player moves sideways, so there is nothing to keystone.
            _mirrorCamera.orthographic = false;
            _mirrorCamera.fieldOfView = source.fieldOfView;
            _mirrorCamera.aspect = source.aspect;
            _mirrorCamera.nearClipPlane = Mathf.Max(0.01f, nearPlane);
            _mirrorCamera.farClipPlane = farPlane;
            _mirrorCamera.ResetProjectionMatrix();

            // And then the near plane is moved onto the glass itself. The camera sits behind the
            // mirror looking out through it, so the frame, the wall it hangs on and the glass -
            // which carries the very texture being drawn into - are all in front of the lens. An
            // oblique near plane removes all three exactly, at the surface, rather than
            // approximately, at whatever depth a near-plane number happens to land on. It is also
            // the whole of the self-reflection prevention: the glass cannot sample itself because
            // the glass is on the clipped side.
            _mirrorCamera.projectionMatrix =
                _mirrorCamera.CalculateObliqueMatrix(CameraSpacePlane(_planePoint, _planeNormal));
        }

        /// <summary>
        /// A point mirrored across the fixed plane. Reads <c>_planePoint</c> and
        /// <c>_planeNormal</c>, never the mirror's transform.
        /// </summary>
        private Vector3 ReflectPoint(Vector3 point) =>
            point - 2f * Vector3.Dot(point - _planePoint, _planeNormal) * _planeNormal;

        /// <summary>A direction mirrored across the fixed plane. The plane's point is irrelevant.</summary>
        private Vector3 ReflectDirection(Vector3 direction) =>
            direction - 2f * Vector3.Dot(direction, _planeNormal) * _planeNormal;

        /// <summary>
        /// The player's camera, cached.
        ///
        /// <para>
        /// <c>ResolveViewCamera</c> is cheap, but it falls back to <c>Camera.main</c> when nothing
        /// is registered and that is a scene search. Holding the answer means the fallback runs
        /// once rather than every frame the player has not spawned yet.
        /// </para>
        /// </summary>
        private Camera ResolveSource()
        {
            if (_cachedSource != null)
                return _cachedSource;

            _cachedSource = Core.LocalPlayerService.ResolveViewCamera();
            return _cachedSource;
        }

        /// <summary>
        /// The reflection buffer, allocated once and reallocated only when the size it should be
        /// actually changes - a quality level switch, or a window resized on desktop.
        ///
        /// <para>
        /// Sized to the screen's aspect rather than square, because the reflection is sampled in
        /// screen space. The old square buffer for a 0.72 x 1.4 glass spent half its pixels on
        /// nothing and stretched what was left, which is what made the edges look pulled.
        /// </para>
        /// </summary>
        private void EnsureTexture(Camera source)
        {
            ResolveTextureSize(source, out int width, out int height);

            if (_texture != null && _textureWidth == width && _textureHeight == height &&
                _texture.IsCreated())
                return;

            if (_texture != null)
            {
                if (_mirrorCamera != null)
                    _mirrorCamera.targetTexture = null;
                _texture.Release();
                Destroy(_texture);
            }

            _textureWidth = width;
            _textureHeight = height;

            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "Mirror_Reflection",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _texture.Create();

            if (_mirrorCamera != null)
                _mirrorCamera.targetTexture = _texture;

            if (_glassMaterial != null && _glassMaterial.HasProperty(ReflectionTexId))
                _glassMaterial.SetTexture(ReflectionTexId, _texture);
        }

        /// <summary>
        /// How big the reflection should be at this quality level.
        ///
        /// <para>
        /// Stepped by <see cref="QualitySettings"/> rather than by platform, so the Ultra tier is
        /// something a machine asks for rather than something forced on every device. The height
        /// is the number that matters and the width follows the screen, so the reflection has the
        /// same pixel density in both axes as the view it is a reflection of.
        /// </para>
        /// </summary>
        private void ResolveTextureSize(Camera source, out int width, out int height)
        {
            int levels = Mathf.Max(1, QualitySettings.names != null ? QualitySettings.names.Length : 1);
            int level = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, levels - 1);

            // Bottom level quarter, top level full, everything between interpolated. A one-level
            // project gets the full height rather than a quarter of it.
            float t = levels <= 1 ? 1f : (float)level / (levels - 1);
            int target = Mathf.RoundToInt(Mathf.Lerp(resolution * 0.5f, resolution, t));

            height = Mathf.Clamp(target, 128, Mathf.Max(128, maxResolution));

            float aspect = source != null && source.aspect > 0.01f
                ? source.aspect
                : (Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7778f);

            width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 128, Mathf.Max(128, maxResolution * 2));
        }

        /// <summary>
        /// The mirror plane written in the reflection camera's own space, which is the form
        /// <see cref="Camera.CalculateObliqueMatrix"/> takes. The offset pushes it a centimetre
        /// out into the room so the glass falls on the clipped side of it rather than exactly on
        /// the boundary.
        /// </summary>
        private Vector4 CameraSpacePlane(Vector3 point, Vector3 normal)
        {
            Vector3 offsetPoint = point + normal * clipPlaneOffset;
            Matrix4x4 view = _mirrorCamera.worldToCameraMatrix;

            Vector3 viewPoint = view.MultiplyPoint(offsetPoint);
            Vector3 viewNormal = view.MultiplyVector(normal).normalized;

            return new Vector4(viewNormal.x, viewNormal.y, viewNormal.z,
                               -Vector3.Dot(viewPoint, viewNormal));
        }

        /// <summary>
        /// The plane, both eye positions and the glass rectangle, drawn while the mirror is
        /// selected. Off by default; the one thing worth seeing when a reflection misbehaves is
        /// whether the mirrored eye is really the mirror image of the real one.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawDebug || _surface == null)
                return;

            // The CAPTURED plane, not the transform. If these two ever disagree, something has
            // written to the mirror's transform and that is the bug worth seeing.
            Gizmos.color = new Color(0.4f, 0.85f, 1f);
            Gizmos.DrawLine(_planePoint, _planePoint + _planeNormal * 0.5f);
            Gizmos.color = new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawLine(_surface.position, _surface.position + _surface.forward * 0.35f);

            Vector3 bl = _surface.TransformPoint(_cornerBottomLeft);
            Vector3 br = _surface.TransformPoint(_cornerBottomRight);
            Vector3 tl = _surface.TransformPoint(_cornerTopLeft);
            Vector3 tr = tl + (br - bl);
            Gizmos.color = new Color(1f, 0.85f, 0.3f);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);

            Camera source = Core.LocalPlayerService.ResolveViewCamera();
            if (source == null)
                return;

            Vector3 eye = source.transform.position;
            Vector3 mirrored = ReflectPoint(eye);

            Gizmos.color = new Color(0.5f, 1f, 0.5f);
            Gizmos.DrawWireSphere(eye, 0.05f);
            Gizmos.DrawRay(eye, source.transform.forward * 0.5f);
            Gizmos.color = new Color(1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(mirrored, 0.05f);
            Gizmos.DrawRay(mirrored, ReflectDirection(source.transform.forward) * 0.5f);
        }
    }
}
