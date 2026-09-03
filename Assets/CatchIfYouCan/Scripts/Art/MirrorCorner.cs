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
    /// The reflection is a virtual camera at the player's mirrored eye, with an <em>off-axis</em>
    /// frustum whose image plane is exactly the mirror rectangle. That is what lets the result be
    /// sampled with ordinary 0-1 UVs: the usual planar-reflection setup folds a reflection matrix
    /// into the view matrix and then has to sample the result in screen space, which needs a
    /// shader this project does not have.
    /// </para>
    ///
    /// <para>
    /// <b>It is a mirror already, and that is where this went wrong for a long time.</b> The ray
    /// from the mirrored eye through any point of the glass <em>is</em> the reflected ray from
    /// the real eye through that same point - the two differ by the reflection, which fixes every
    /// point of the plane - so the camera renders the reflected world without anything being
    /// reflected. It is not a window: a window would need a camera on the player's side looking
    /// through the hole at what lies beyond. And the one flip a mirror needs is supplied by the
    /// geometry, because the player looks at the glass from the side opposite the one the camera
    /// shot it from. Flipping the UVs on top of that, which this used to do, put the sideways
    /// parallax the wrong way round: strafe left and the reflected room slid left with you, which
    /// reads exactly like a portal swinging about a pivot.
    /// </para>
    ///
    /// <para>
    /// For the same reason there is no <c>GL.invertCulling</c> here and there must not be. The
    /// camera's basis is the glass's own right, up and forward - a perfectly ordinary
    /// right-handed camera - and the room it renders is the room as authored. Nothing is turned
    /// inside out, so nothing needs turning back.
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
    {
        [Header("Mirror")]
        [Tooltip("Glass size in metres. Small on purpose - it is a corner mirror, not a wall of " +
                 "them - but tall enough to still show the player when they crouch.")]
        [SerializeField] private Vector2 glassSize = new Vector2(0.72f, 1.4f);

        [Tooltip("Where the glass sits, relative to this transform. Local +Z is the direction it " +
                 "faces, so this object should be turned to face into the room.")]
        [SerializeField] private Vector3 glassLocalPosition = new Vector3(0f, 1.25f, 0.02f);

        [Tooltip("Resolution of the reflection. 512 is plenty for a 70 cm mirror and a great deal " +
                 "cheaper than matching the screen.")]
        [SerializeField, Min(64)] private int resolution = 512;

        [Tooltip("Flip the reflection left to right, in the glass mesh's own UVs. OFF, and that " +
                 "is the whole of what made the reflection swing about like a portal: it was on, " +
                 "and it was a second flip on top of one the geometry already provides. A " +
                 "reflection camera at the mirrored eye already renders the mirror's content - " +
                 "the ray from it through any point of the glass is the reflected ray from the " +
                 "player through that same point - and the player then looks at that image from " +
                 "the opposite side of the glass to the side the camera shot it from, which is " +
                 "the one flip a mirror needs. Flipping the UVs as well undid it, and inverting " +
                 "the sideways parallax is precisely what reads as the room swinging round a " +
                 "pivot when you strafe. Left as a field only in case a future glass mesh is " +
                 "wound the other way.")]
        [SerializeField] private bool mirrorImage;

        private bool _appliedFlip;
        private bool _hasAppliedFlip;

        [Tooltip("Stop rendering beyond this. The reflection is a second pass over the room, so " +
                 "it should not run while the player is on the other side of the house.")]
        [SerializeField, Min(1f)] private float renderDistance = 7f;

        [Tooltip("Near plane the off-axis frustum is built at. It does not decide what the " +
                 "reflection sees - the oblique clip below moves the real near plane onto the " +
                 "glass - and it does not change the image, because the frustum's four edges are " +
                 "scaled to whatever distance it is built at.")]
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
                 "slightly warm: the glass is a lit surface, so the room's own light is already " +
                 "taking a bite out of the reflection and a dark tint on top of that leaves " +
                 "nothing to see. The age is in the warmth, not in the darkness.")]
        [SerializeField] private Color glassTint = new Color(0.93f, 0.9f, 0.85f);

        [SerializeField, Range(0f, 1f)] private float glassSmoothness = 0.75f;

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
            BuildGlass(lit);
            BuildCamera();

            if (buildLamp)
                BuildLamp(lit);
            if (buildFill)
                BuildFill();
        }

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
        private void BuildGlass(Shader lit)
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
            // Plain. The flip lives on the material instead, so it can be switched while the
            // game is running - see ApplyMirrorFlip.
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

            _texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Default)
            {
                name = "Mirror_Reflection",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _texture.Create();

            if (lit == null)
                return;

            // The reflection goes in as the base map of a plain lit opaque material. No emission,
            // no keyword turned on at run time, no property that this shader might not have: the
            // base map and base colour of URP/Lit are the two things every other material in this
            // room already uses, so they are the two things that cannot be missing from the
            // build. Every write is guarded anyway, because a silently ignored SetTexture is how
            // a mirror ends up showing the tint and nothing else.
            //
            // Being lit does cost something - the reflection is dimmed by whatever falls on the
            // glass - and that is what the standing lamp beside it is for. The tint is therefore
            // near white and slightly warm rather than dark: it ages the reflection without
            // taking it away.
            _glassMaterial = new Material(lit) { name = "Mirror_Glass_Runtime" };
            SetTextureIfPresent(_glassMaterial, "_BaseMap", _texture);
            ApplyMirrorFlip();
            SetColorIfPresent(_glassMaterial, "_BaseColor", glassTint);
            SetFloatIfPresent(_glassMaterial, "_Metallic", 0f);
            SetFloatIfPresent(_glassMaterial, "_Smoothness", glassSmoothness);
            renderer.sharedMaterial = _glassMaterial;

            LogState(renderer);
        }

        /// <summary>
        /// Puts the left-right flip on the material rather than in the mesh, so the tick box can
        /// be turned on and off in Play Mode and the answer seen immediately.
        ///
        /// <para>
        /// Only one of the two settings is physically right, and which one it is has now been
        /// argued both ways from first principles and got wrong once. Five seconds of clicking
        /// beats another page of reasoning: strafe sideways with it off, strafe with it on, and
        /// keep whichever makes the room slide the way a wall mirror does.
        /// </para>
        /// </summary>
        private void ApplyMirrorFlip()
        {
            if (_glassMaterial == null)
                return;
            if (_hasAppliedFlip && _appliedFlip == mirrorImage)
                return;

            _glassMaterial.mainTextureScale = new Vector2(mirrorImage ? -1f : 1f, 1f);
            _glassMaterial.mainTextureOffset = new Vector2(mirrorImage ? 1f : 0f, 0f);
            _appliedFlip = mirrorImage;
            _hasAppliedFlip = true;
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
                      " hasBaseMap=" + (_glassMaterial != null && _glassMaterial.HasProperty("_BaseMap")) +
                      " rtCreated=" + (_texture != null && _texture.IsCreated()) +
                      " rt=" + resolution + "x" + resolution +
                      " rendererEnabled=" + renderer.enabled, this);
        }

        private void BuildCamera()
        {
            var go = new GameObject("Mirror_Camera");
            go.transform.SetParent(transform, false);

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

            var data = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data == null)
                data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // Grading and vignette are the player's view of the room, not the room itself, and
            // running them twice costs a full pass for something nobody can see at this size.
            data.renderPostProcessing = false;
            data.renderShadows = true;

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

            ApplyMirrorFlip();

            // The reflection is only correct from the eye it is drawn for. Camera.main
            // answered "any tagged camera", which in the lobby was the menu camera until
            // the player spawned, and in a scene with two of them is arbitrary.
            Camera source = Core.LocalPlayerService.ResolveViewCamera();
            if (source == null)
            {
                _mirrorCamera.gameObject.SetActive(false);
                return;
            }

            Vector3 eye = source.transform.position;
            Vector3 normal = _surface.forward;
            float inFront = Vector3.Dot(eye - _surface.position, normal);

            // Nothing to reflect from behind the glass, and nothing worth a second render of the
            // room from across the house.
            bool visible = inFront > 0.05f &&
                           Vector3.Distance(eye, _surface.position) <= renderDistance;

            if (_mirrorCamera.gameObject.activeSelf != visible)
                _mirrorCamera.gameObject.SetActive(visible);
            if (!visible)
                return;

            // The reflected eye. This is the one thing that has always been right here and is
            // worth stating plainly: it is the player's camera mirrored across the glass plane,
            // so a step of half a metre towards the mirror moves it half a metre the other way
            // and the parallax comes out symmetric on its own.
            Vector3 reflected = eye - 2f * inFront * normal;

            // The camera is turned to face straight out of the glass, and that is not a
            // simplification - it is the whole point of an off-axis frustum. Where the camera
            // looks does not decide what a mirror shows; the four corners of the glass do. With
            // the camera's own right, up and forward equal to the glass's, the frustum below
            // reduces to four numbers in that same basis, and the image it renders lands on the
            // mirror rectangle exactly, which is what lets it be sampled with plain 0-1 UVs
            // instead of a screen-space shader this project does not have.
            _mirrorCamera.transform.SetPositionAndRotation(
                reflected, Quaternion.LookRotation(normal, _surface.up));

            // Corners of the glass as it is actually drawn, in world space.
            Vector3 bottomLeft = _surface.TransformPoint(_cornerBottomLeft);
            Vector3 bottomRight = _surface.TransformPoint(_cornerBottomRight);
            Vector3 topLeft = _surface.TransformPoint(_cornerTopLeft);

            Vector3 right = _mirrorCamera.transform.right;
            Vector3 up = _mirrorCamera.transform.up;

            Vector3 va = bottomLeft - reflected;
            Vector3 vb = bottomRight - reflected;
            Vector3 vc = topLeft - reflected;

            float distance = Vector3.Dot(va, normal);
            if (distance <= 0.001f)
                return;

            // Kooima's generalised perspective projection: the frustum's edges are where the
            // rays from the eye to the glass corners cross the near plane, so the image plane is
            // the mirror rectangle whatever the near plane is set to. Rebuilt every frame from
            // the moved eye, which is what makes the reflection shift the way a wall mirror does
            // rather than swinging like a camera on a bracket.
            float near = Mathf.Max(0.01f, nearPlane);
            float scale = near / distance;
            _mirrorCamera.nearClipPlane = near;
            _mirrorCamera.farClipPlane = farPlane;
            _mirrorCamera.projectionMatrix = Matrix4x4.Frustum(
                Vector3.Dot(right, va) * scale,
                Vector3.Dot(right, vb) * scale,
                Vector3.Dot(up, va) * scale,
                Vector3.Dot(up, vc) * scale,
                near, farPlane);

            // And then the near plane is moved onto the glass itself. The camera sits behind the
            // mirror looking out through it, so the frame, the wall it hangs on and the glass -
            // which carries the very texture being drawn into - are all in front of the lens.
            // An oblique near plane removes all three exactly, at the surface, rather than
            // approximately, at whatever depth a near-plane number happens to land on.
            _mirrorCamera.projectionMatrix =
                _mirrorCamera.CalculateObliqueMatrix(CameraSpacePlane(_surface.position, normal));
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

            Gizmos.color = new Color(0.4f, 0.85f, 1f);
            Gizmos.DrawLine(_surface.position, _surface.position + _surface.forward * 0.5f);

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
            Vector3 mirrored = eye - 2f * Vector3.Dot(eye - _surface.position, _surface.forward) *
                               _surface.forward;

            Gizmos.color = new Color(0.5f, 1f, 0.5f);
            Gizmos.DrawWireSphere(eye, 0.05f);
            Gizmos.DrawLine(eye, _surface.position);
            Gizmos.color = new Color(1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(mirrored, 0.05f);
            Gizmos.DrawLine(mirrored, _surface.position);
        }
    }
}
