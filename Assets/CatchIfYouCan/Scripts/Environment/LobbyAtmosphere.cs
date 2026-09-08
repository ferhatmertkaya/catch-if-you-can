using System.Collections.Generic;
using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// Gives the hand-built lobby warmth, without making it bright.
    ///
    /// <para>
    /// <b>Why the room reads as flat.</b> The lobby floor is 25.8 x 41.9 m under a 4 m ceiling.
    /// The lights authored in the scene stand between x = 17 and x = 27, so roughly a fifth of
    /// that floor has a light near it and the rest has the scene's ambient term - which is
    /// (0.015, 0.03, 0.025) at 0.6. That is not dark, it is black; and where a room is lit by an
    /// ambient term alone, every corner of it is the same value, so there is no falloff, no side
    /// and no shadow. A room like that does not look dark, it looks dead. The same failure the
    /// generated rooms had, in a room somebody built by hand.
    /// </para>
    ///
    /// <para>
    /// <b>What this does instead.</b> It hangs warm practicals over the places the player
    /// actually is - where they arrive, where the doorway is, where the table and the armchair
    /// stand, where the mirror and the board hang - and leaves everything between them dark. The
    /// anchors are found through components and serialized references, never by object name: a
    /// hard-coded name that stops resolving fails silently and forever, which this repository has
    /// now done three times (CLAUDE.md mistakes 3 and 10).
    /// </para>
    ///
    /// <para>
    /// <b>Nothing here floods the room.</b> Every light is a point light with a range that dies
    /// well before the next one, so no renderer is inside more than two of them, and only the
    /// key gets real-time shadows. Amber, around 2200-3000 K by eye, deliberately below white:
    /// a blown-out shade reads as a studio lamp, not as a house nobody has lived in for a while.
    /// </para>
    ///
    /// <para>
    /// <b>It builds, it does not author.</b> Everything is made in <c>Start</c> and lives only in
    /// play mode, like the mirror, the armchair, the table and the board already do. That keeps
    /// the scene file free of thirty more hand-written documents and keeps the numbers here,
    /// where they can be read and changed.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Lobby Atmosphere")]
    public sealed class LobbyAtmosphere : MonoBehaviour
    {
        private const string LogTag = "[CIYC][LobbyAtmosphere] ";

        [Header("Anchors")]
        [Tooltip("Where the player starts. A practical goes over it so the first frame is not " +
                 "black. Left empty, the spawn is taken from the portal's own lobby anchor.")]
        [SerializeField] private Transform playerSpawn;

        [Tooltip("Extra places worth lighting - a chair, a fireplace, the end of a hallway. " +
                 "Each gets its own warm practical.")]
        [SerializeField] private Transform[] extraPractials = new Transform[0];

        [Header("The warm practicals")]
        [Tooltip("Amber, around 2400 K by eye. Deliberately not white.")]
        [SerializeField] private Color practicalColor = new Color(1f, 0.66f, 0.36f);

        [SerializeField, Min(0f)] private float practicalIntensity = 3.2f;

        [Tooltip("Small on purpose. A range that reaches the next lamp is a flood light with " +
                 "extra steps, and it is what turns a dark room into an evenly grey one.")]
        [SerializeField, Min(0.5f)] private float practicalRange = 6.5f;

        [Tooltip("How high over its anchor a practical hangs. Below the 4 m ceiling, so the " +
                 "light pools on the floor instead of washing the whole wall.")]
        [SerializeField, Min(0.5f)] private float practicalHeight = 2.6f;

        [Header("The floor lift")]
        [Tooltip("One very weak warm light high over the walked area. It is not a key light - " +
                 "it exists so a wall three metres past the last lamp is a dark wall rather " +
                 "than a black rectangle.")]
        [SerializeField, Min(0f)] private float liftIntensity = 0.55f;

        [SerializeField, Min(1f)] private float liftRange = 26f;

        [Header("Dust")]
        [SerializeField] private bool buildDust = true;

        [Tooltip("Hard ceiling on the particle count. These are motes in the air, not weather.")]
        [SerializeField, Range(16, 400)] private int dustMaxParticles = 120;

        [Tooltip("Metres across, centred on the lit area. Dust outside the light is invisible " +
                 "and costs the same as dust inside it.")]
        [SerializeField] private Vector3 dustVolume = new Vector3(16f, 3.4f, 16f);

        private readonly List<Light> _built = new List<Light>();
        private ParticleSystem _dust;
        private Material _dustMaterial;
        private Texture2D _dustSprite;

        /// <summary>The lights this component made. Public so a diagnostic can count them.</summary>
        public int BuiltLightCount => _built.Count;

        /// <summary>The dust system, or null when it could not be built.</summary>
        public ParticleSystem Dust => _dust;

        private void Start()
        {
            List<Transform> anchors = CollectAnchors();

            if (anchors.Count == 0)
            {
                CIYCLog.Warn(LogTag + "No anchors to light. The lobby keeps whatever the scene " +
                             "authored, which over a 25 x 42 m floor is the ambient term and " +
                             "nothing else.");
            }

            for (int i = 0; i < anchors.Count; i++)
                BuildPractical(anchors[i]);

            BuildFloorLift(anchors);

            if (buildDust)
                BuildDust(anchors);

            CIYCLog.Info(LogTag + _built.Count + " warm practical(s) over " + anchors.Count +
                         " anchor(s), dust=" + (_dust != null ? "yes" : "no") +
                         ". Everything between them is left dark on purpose.");
        }

        private void OnDestroy()
        {
            // Built here, so destroyed here. A Material and a Texture2D made at runtime are not
            // collected with the GameObject that referenced them.
            if (_dustMaterial != null)
                Destroy(_dustMaterial);
            if (_dustSprite != null)
                Destroy(_dustSprite);
        }

        /// <summary>
        /// The places worth lighting, found through components rather than by name.
        ///
        /// <para>
        /// The portal, the mirror, the board, the props: each is a component this project owns,
        /// so asking for the component is asking the question that actually matters ("is there a
        /// mirror in this room") instead of the one that merely correlates with it ("is there an
        /// object called Lobby_MirrorCorner").
        /// </para>
        /// </summary>
        private List<Transform> CollectAnchors()
        {
            var anchors = new List<Transform>();

            void Add(Transform t)
            {
                if (t == null)
                    return;
                for (int i = 0; i < anchors.Count; i++)
                {
                    // Two anchors a metre apart are one pool of light, not two. Without this the
                    // table and the armchair - 0.9 m apart - would each get a lamp and that
                    // corner would be the brightest thing in a horror game.
                    if ((anchors[i].position - t.position).sqrMagnitude < 2.25f)
                        return;
                }
                anchors.Add(t);
            }

            Add(playerSpawn);

            var portal = FindFirstObjectByType<LobbyPortal>(FindObjectsInactive.Exclude);
            if (portal != null)
                Add(portal.transform);

            var mirror = FindFirstObjectByType<Art.MirrorCorner>(FindObjectsInactive.Exclude);
            if (mirror != null)
                Add(mirror.transform);

            var board = FindFirstObjectByType<Interaction.LobbyInvestigationBoard>(
                FindObjectsInactive.Exclude);
            if (board != null)
                Add(board.transform);

            foreach (Art.RoomProp prop in FindObjectsByType<Art.RoomProp>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (prop != null)
                    Add(prop.transform);
            }

            for (int i = 0; i < extraPractials.Length; i++)
                Add(extraPractials[i]);

            return anchors;
        }

        private void BuildPractical(Transform anchor)
        {
            var go = new GameObject("Lobby_Practical_" + anchor.name);
            go.transform.SetParent(transform, false);
            go.transform.position = anchor.position + Vector3.up * practicalHeight;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = practicalColor;
            light.intensity = practicalIntensity;
            light.range = practicalRange;

            // Only the FIRST practical casts real-time shadows. Every additional shadow-casting
            // point light is another shadow map per frame on a phone, and the difference between
            // one and six of them in a room this size is not something anybody looks at.
            light.shadows = _built.Count == 0 ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.85f;

            // A slow breath, so a still frame of this room is not the same image as the next
            // one. Attached AFTER the intensity is set: CandleFlicker reads it in Awake, and
            // Awake runs inside AddComponent.
            go.AddComponent<Art.CandleFlicker>();

            _built.Add(light);
        }

        /// <summary>
        /// One very weak warm light over the middle of everything, so the space between the
        /// pools is dark rather than absent. It is NOT a key light: at this intensity a wall
        /// three metres past the last lamp reads as a wall, and five metres past it is still
        /// black.
        /// </summary>
        private void BuildFloorLift(List<Transform> anchors)
        {
            if (liftIntensity <= 0f || anchors.Count == 0)
                return;

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < anchors.Count; i++)
                centre += anchors[i].position;
            centre /= anchors.Count;

            var go = new GameObject("Lobby_FloorLift");
            go.transform.SetParent(transform, false);
            go.transform.position = centre + Vector3.up * 3.4f;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.5f);
            light.intensity = liftIntensity;
            light.range = liftRange;
            light.shadows = LightShadows.None;

            _built.Add(light);
        }

        // ---- dust ----------------------------------------------------------------------------

        /// <summary>
        /// Motes in the air, and nothing more than that.
        ///
        /// <para>
        /// Built in world space so the dust stays where it is while the player walks through it -
        /// local space would drag the whole volume along with the object and read as fog stuck to
        /// the camera. No collision, no trails, no sub-emitters, one small additive billboard
        /// each: what makes this visible is not the particle, it is the warm light it drifts
        /// through, which is why the volume is centred on the lit area rather than on the room.
        /// </para>
        /// </summary>
        private void BuildDust(List<Transform> anchors)
        {
            Shader shader = Art.CiycShaders.Find(Art.CiycShaders.ParticlesUnlit);
            if (shader == null)
            {
                // No shader means no dust. Deliberately not a fallback: `Shader.Find("Standard")`
                // resolves everywhere and draws magenta under URP, and a room full of magenta
                // squares is a worse bug than a room with no dust (CLAUDE.md mistake 2).
                CIYCLog.Error(LogTag + "No URP particle shader, so there is no dust. A built-in " +
                              "fallback would be magenta squares drifting through the room.");
                return;
            }

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < anchors.Count; i++)
                centre += anchors[i].position;
            if (anchors.Count > 0)
                centre /= anchors.Count;
            centre.y = dustVolume.y * 0.5f;

            var go = new GameObject("Lobby_DustParticles");
            go.transform.SetParent(transform, false);
            go.transform.position = centre;

            _dust = go.AddComponent<ParticleSystem>();

            // Configured BEFORE it is allowed to play. A ParticleSystem added by AddComponent
            // starts on its own with the module defaults, which is a fountain of white squares.
            var main = _dust.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = dustMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(11f, 20f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.008f, 0.035f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.024f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.90f, 0.76f, 0.30f), new Color(1f, 0.82f, 0.62f, 0.55f));

            // Barely there. Real dust settles; dust that settles at Unity's default gravity is
            // snow.
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.0015f, 0.004f);

            var emission = _dust.emission;
            emission.rateOverTime = Mathf.Max(1f, dustMaxParticles / 16f);

            var shape = _dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = dustVolume;

            // The small random wander that separates a mote from a falling dot. Low frequency
            // and low quality on purpose: this is the one module here that costs anything.
            var noise = _dust.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = new ParticleSystem.MinMaxCurve(0.045f);
            noise.frequency = 0.14f;
            noise.scrollSpeed = 0.05f;

            // Fade in and out rather than pop. A mote that appears at full brightness in mid-air
            // is the one thing about dust anybody ever notices.
            var alpha = _dust.colorOverLifetime;
            alpha.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f)
                });
            alpha.color = new ParticleSystem.MinMaxGradient(gradient);

            var collision = _dust.collision;
            collision.enabled = false;
            var trails = _dust.trails;
            trails.enabled = false;
            var lights = _dust.lights;
            lights.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            _dustMaterial = new Material(shader) { name = "Lobby_Dust_Runtime" };
            ConfigureDustSurface(_dustMaterial);
            renderer.sharedMaterial = _dustMaterial;

            _dust.Play();
        }

        /// <summary>
        /// Transparent and additive, with a soft round sprite.
        ///
        /// <para>
        /// A <c>new Material(shader)</c> on URP's particle shader inherits the shader's defaults,
        /// and those are an OPAQUE surface on the default white texture - which draws as a solid
        /// white SQUARE. That is the exact failure that shipped the portal's sparks as green
        /// rectangles, and dust is the one effect where it would be least obvious what went
        /// wrong: hundreds of tiny white squares read as a rendering artifact, not as a material.
        /// </para>
        /// <para>
        /// Written here rather than borrowed from <c>PortalEffects</c>: that one takes its
        /// texture from the purchased-pack style and is pinned to its own file by a guard. Both
        /// are checked, so they cannot quietly disagree.
        /// </para>
        /// </summary>
        private void ConfigureDustSurface(Material material)
        {
            // URP reads the surface type from these floats AND from the keywords; setting one
            // without the other gives a material that says transparent and renders opaque.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.SetTexture("_BaseMap", DustSprite());

            // Not every URP version names it _BaseMap on the particle shader, and a texture that
            // did not land is exactly the square this method exists to remove.
            material.SetTexture("_MainTex", DustSprite());
        }

        /// <summary>A soft round dot, generated rather than loaded from a path that can be wrong.</summary>
        private Texture2D DustSprite()
        {
            if (_dustSprite != null)
                return _dustSprite;

            const int size = 32;
            _dustSprite = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lobby_DustSprite",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            const float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a *= a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            _dustSprite.SetPixels(pixels);
            _dustSprite.Apply();
            return _dustSprite;
        }
    }
}
