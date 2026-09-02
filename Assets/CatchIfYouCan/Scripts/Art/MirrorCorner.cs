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
    /// sampled with ordinary 0-1 UVs: the usual planar-reflection setup needs a shader that
    /// samples in screen space, and this project has no custom shaders. The cost is that an
    /// off-axis frustum renders a window rather than a mirror - it does not swap left and right -
    /// so the glass is built with its UVs mirrored, in the mesh rather than in the material.
    /// </para>
    ///
    /// <para>
    /// <b>The near plane is the mirror plane.</b> The reflection camera sits behind the glass
    /// looking out through it, so everything between it and the room - the frame, the wall the
    /// mirror hangs on, and the glass itself - is in front of the lens. The frame alone is a
    /// solid slab wider and taller than the glass sitting exactly on the image plane, which fills
    /// the frustum completely: that is a mirror that renders one flat rectangle of dark wood and
    /// nothing else. Worse, the glass is textured with the very render texture being drawn, so
    /// what little got past the frame was a texture sampling itself. Pushing the near plane out
    /// to the mirror plane removes all three at once, and costs nothing: an off-axis frustum is
    /// built from the corners of a rectangle at a known distance, so moving the near plane
    /// rescales the four edges and leaves the image identical.
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

        [Tooltip("Flip the reflection left to right. On, because an off-axis frustum renders the " +
                 "view through a window rather than the view in a mirror. Built into the glass " +
                 "mesh's UVs, so it is read once when the mirror is built.")]
        [SerializeField] private bool mirrorImage = true;

        [Tooltip("Stop rendering beyond this. The reflection is a second pass over the room, so " +
                 "it should not run while the player is on the other side of the house.")]
        [SerializeField, Min(1f)] private float renderDistance = 7f;

        [Tooltip("Smallest near plane the reflection camera may use. The near plane is normally " +
                 "the mirror plane itself, which is always further than this; this is only a " +
                 "floor for the moment the player's face is against the glass.")]
        [SerializeField, Min(0.01f)] private float nearPlane = 0.05f;

        [SerializeField, Min(1f)] private float farPlane = 40f;

        [Header("Glass")]
        [Tooltip("What the reflection is multiplied by on its way onto the glass. Under one and " +
                 "slightly warm, so it reads as a hundred-year-old mirror rather than as a " +
                 "second window: darker than the room, and a little browner.")]
        [SerializeField] private Color glassTint = new Color(0.72f, 0.69f, 0.63f);

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

        /// <summary>
        /// How far past the mirror plane the near plane is pushed, as a fraction of the distance
        /// to it. Two parts in a thousand - a couple of millimetres at arm's length - which is
        /// enough to clip the glass itself and far too little to clip anything in the room.
        /// </summary>
        private const float NearPlaneInset = 1.002f;

        private Transform _surface;
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

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            BuildFrame(lit);
            BuildGlass(unlit);
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
        private void BuildGlass(Shader unlit)
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
            // Mirrored here rather than in the material. An off-axis frustum renders the view
            // through a window, and a window is a mirror with left and right the wrong way
            // round; swapping U is the whole of the difference.
            float u0 = mirrorImage ? 1f : 0f;
            float u1 = mirrorImage ? 0f : 1f;
            mesh.uv = new[]
            {
                new Vector2(u0, 0f), new Vector2(u1, 0f),
                new Vector2(u0, 1f), new Vector2(u1, 1f)
            };
            // Wound clockwise as seen from local +Z, which is Unity's front-facing order and
            // the side that faces the room. Worked out on paper rather than by rotating a Quad
            // until it looked right: a mirror that is invisible from the front and solid from
            // behind is a five-minute mystery every time.
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

            if (unlit == null)
                return;

            // Unlit, and deliberately so twice over. A mirror is not lit by the room it is in -
            // the reflection carries its own light, and a Lit surface would darken it with the
            // corner's own shadow. And it is the one shader this material is known to survive a
            // player build with: fed to a Lit material as an emission map, with _EMISSION turned
            // on at runtime, the glass came back magenta on device, which is what a URP build
            // shows when a shader variant nobody asked for at build time is asked for at run
            // time. Age is a tint on the base colour instead, which is a plain property and
            // needs no variant of anything.
            _glassMaterial = new Material(unlit) { name = "Mirror_Glass_Runtime" };
            _glassMaterial.mainTexture = _texture;
            _glassMaterial.color = glassTint;
            renderer.sharedMaterial = _glassMaterial;
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

            Camera source = Camera.main;
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

            Vector3 reflected = eye - 2f * inFront * normal;

            // The camera's own axes are made to match the mirror's, which is what reduces the
            // off-axis frustum to four numbers: with right, up and forward already equal to the
            // glass's, the view matrix the transform produces is exactly the one the projection
            // below is built against.
            _mirrorCamera.transform.SetPositionAndRotation(
                reflected, Quaternion.LookRotation(normal, _surface.up));

            // The three corners the frustum is built from, in world space.
            Vector3 right = _surface.right;
            Vector3 up = _surface.up;
            Vector3 bottomLeft = _surface.position
                                 - right * (GlassWorldWidth * 0.5f)
                                 - up * (GlassWorldHeight * 0.5f);
            Vector3 bottomRight = bottomLeft + right * GlassWorldWidth;
            Vector3 topLeft = bottomLeft + up * GlassWorldHeight;

            Vector3 va = bottomLeft - reflected;
            Vector3 vb = bottomRight - reflected;
            Vector3 vc = topLeft - reflected;

            float distance = Vector3.Dot(va, normal);
            if (distance <= 0.001f)
                return;

            // The near plane is the mirror plane, nudged a hair past it. Everything behind the
            // glass - the frame, the wall, and the glass itself with this very texture on it -
            // is between the reflection camera and the room, and clipping it here is what stops
            // the mirror rendering a slab of frame or a texture sampling itself. The off-axis
            // frustum absorbs the move: the edges below are scaled to whatever near plane it is
            // built at, so the image does not change.
            float n = Mathf.Max(nearPlane, distance * NearPlaneInset);
            float scale = n / distance;
            _mirrorCamera.nearClipPlane = n;

            float left = Vector3.Dot(right, va) * scale;
            float rightEdge = Vector3.Dot(right, vb) * scale;
            float bottom = Vector3.Dot(up, va) * scale;
            float top = Vector3.Dot(up, vc) * scale;

            _mirrorCamera.projectionMatrix =
                Matrix4x4.Frustum(left, rightEdge, bottom, top, n, farPlane);
        }

        private float GlassWorldWidth => glassSize.x * transform.lossyScale.x;
        private float GlassWorldHeight => glassSize.y * transform.lossyScale.y;
    }
}
