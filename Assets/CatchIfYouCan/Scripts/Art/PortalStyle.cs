using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Every tunable the portal has, in one serialized object.
    ///
    /// <para>
    /// <b>One authored copy, three consumers.</b> <see cref="PortalSurface"/> needs the shader
    /// numbers and the buffer size, <see cref="PortalEffects"/> needs the particle and light
    /// numbers, and <c>LobbyPortal</c> needs the timings - and before this they were thirty
    /// magic constants spread across those three files, half of them private and none of them
    /// reachable from the Inspector. The doorway owns one of these and pushes it down.
    /// </para>
    ///
    /// <para>
    /// <b>The colour is data.</b> The game ships spectral green because that is this game's
    /// colour, but nothing here or in the shader assumes it: every layer is a field, so the
    /// same portal can be blue, violet or red without an edit to code or shader.
    /// </para>
    /// </summary>
    [System.Serializable]
    public sealed class PortalStyle
    {
        // ---- shape -------------------------------------------------------------------------

        [Header("Portal")]
        // ---- ONE authoritative size ----------------------------------------------------------
        //
        // openingSize is the ONLY thing that says how big the portal is. Everything derives from
        // it: the drawn quad, the shader's breach, the wall's collision hole, the crossing
        // aperture and the threshold volume.
        //
        // There used to be a second knob, breachHalfSize, a FRACTION of the quad that scaled the
        // oval inside it. Two numbers that both had to be right produced exactly the bug it
        // sounds like: the glow grew with openingSize while the hole you could walk through was
        // still the fraction of an opening nobody had updated. It is deleted rather than
        // defaulted to 1, because a knob that must always be 1 is a knob that will not be.

        [Tooltip("The clear hole in metres. Widened past the old 1.06 doorway width because the " +
                 "lobby's north wall is one solid object with no door frame - there are no jambs " +
                 "left for the breach to have to fit between, and a tall narrow oval in a wall " +
                 "reads as a window. This also widens where the player may cross, which is " +
                 "correct: the aperture should be the hole you can see.")]
        public Vector2 openingSize = new Vector2(4.7f, 2.4f);

        [Tooltip("How much BIGGER than the opening the drawn QUAD is, as a fraction. Pure " +
                 "canvas: the hole, the collision and the view all stay exactly Opening Size, " +
                 "and the extra area is only somewhere for the ragged edge and the outer glow " +
                 "to finish. Too little and they end in a straight line at the mesh boundary - " +
                 "the rectangular clipping at the top and the upper corners.\n\n" +
                 "This does NOT stretch the far room: the view is sampled in SCREEN space, so " +
                 "the render texture is shaped by the screen and not by this quad.")]
        [Range(0f, 1.5f)] public float glowMargin = 0.45f;

        /// <summary>
        /// The quad to draw, in metres: the opening plus the margin the glow needs.
        ///
        /// <para>
        /// Derived in one place because three things have to agree about it - the mesh, the
        /// culling bounds, and the shader's <c>_Fit</c> - and a quad sized by one formula while
        /// _Fit is computed by another is a breach that does not sit where the geometry thinks
        /// it does.
        /// </para>
        /// </summary>
        public Vector2 QuadSize()
        {
            float scale = 1f + Mathf.Max(0f, glowMargin);
            return new Vector2(Mathf.Max(0.01f, openingSize.x) * scale,
                               Mathf.Max(0.01f, openingSize.y) * scale);
        }

        /// <summary>
        /// The shader's <c>_Fit</c>: the oval's semi-axes in the QUAD's normalised space.
        ///
        /// <para>
        /// The oval is authored against the opening and drawn on the larger quad, so the trim
        /// has to be divided by the margin. Get this wrong and the hole is the right shape at
        /// the wrong size, which looks like a tuning problem and is an arithmetic one.
        /// </para>
        /// </summary>
        public Vector2 ResolveFit()
        {
            float scale = 1f + Mathf.Max(0f, glowMargin);
            return new Vector2(1f / scale, 1f / scale);
        }

        [Tooltip("How far the oval's edge is chewed away from a clean curve. Zero is a neat " +
                 "porthole; higher is a hole something came through.")]
        [Range(0f, 0.5f)] public float tearAmount = 0.5f;

        [Tooltip("Size of the chunks the edge breaks into. Low is a few big pieces of plaster, " +
                 "high is fine crumbling.")]
        [Range(0.5f, 24f)] public float tearScale = 5.4f;

        [Tooltip("Width of the burning band, as a fraction of the oval's radius.")]
        [Range(0.01f, 0.9f)] public float rimWidth = 0.162f;

        [Range(0.05f, 4f)] public float rimSoftness = 1.95f;

        // ---- colour ------------------------------------------------------------------------

        [Header("Colours")]
        [Tooltip("The hottest, narrowest part of the rim. Near-white: energy that bright has " +
                 "no colour left.")]
        [ColorUsage(true, true)] public Color coreColor = new Color(0.90f, 1.00f, 0.94f);

        [Tooltip("The portal's identity. #57FF68 spectral green by default.")]
        [ColorUsage(true, true)] public Color energyColor = new Color(0.341f, 1.000f, 0.408f);

        [Tooltip("What spills outside the oval. Darker and desaturated so the edge reads as " +
                 "volume rather than as a second ring.")]
        [ColorUsage(true, true)] public Color outerColor = new Color(0.09f, 0.36f, 0.19f);

        [Tooltip("Sparks and streaks. White-green at birth.")]
        [ColorUsage(true, true)] public Color sparkColor = new Color(0.80f, 1.00f, 0.85f);

        [Tooltip("Multiplied onto the far room. Not white by default, because a portal that " +
                 "tints what is behind it reads as glass rather than as a cut-out.")]
        public Color viewTint = new Color(0.92f, 0.98f, 0.94f);

        // ---- shader ------------------------------------------------------------------------

        [Header("Energy shader")]
        [Range(0f, 16f)] public float coreIntensity = 6.53f;
        [Range(0f, 16f)] public float energyIntensity = 2.6f;

        [Tooltip("Layer A: the slow, large turbulence.")]
        [Range(0.5f, 24f)] public float noiseScale = 5.5f;
        [Range(0f, 0.6f)] public float noiseStrength = 0.16f;
        [Range(0f, 4f)] public float noiseSpeed = 0.35f;

        [Tooltip("Layer B: finer and faster, and rotating the other way. Deliberately NOT a " +
                 "multiple of layer A - two layers on the same frequency read as one pattern.")]
        [Range(0.5f, 40f)] public float secondaryNoiseScale = 13.0f;
        [Range(0f, 6f)] public float secondaryNoiseSpeed = 0.85f;

        [Tooltip("How far the far room is dragged sideways near the rim. Falls to zero toward " +
                 "the centre, which stays readable.")]
        // Screen-UV units, so this IS the percentage of the screen the far room can be
        // dragged by. Capped at 1.5% by the range rather than by good intentions: the
        // destination has to stay readable, and the shader already confines the bend to
        // the outer edge, so a large value here does not read as refraction - it reads as
        // the far room sliding around inside the hole.
        [Range(0f, 0.015f)] public float viewDistortionStrength = 0.012f;

        [Range(-3f, 3f)] public float rotationSpeed = 0.22f;
        [Range(0f, 12f)] public float pulseSpeed = 2.1f;
        [Range(0f, 1f)] public float pulseStrength = 0.14f;

        // ---- particles ---------------------------------------------------------------------

        [Header("Sparks")]
        [Tooltip("The sparks' image. Leave empty for the generated soft dot, which is what the " +
                 "portal uses by default; assign a bought pack's spark image to replace it. " +
                 "Whatever is here is TINTED by the particle gradient, so a white or greyscale " +
                 "image behaves best and a strongly coloured one will fight the colours above.")]
        public Texture2D sparkTexture;

        [Tooltip("Particles per second around the whole perimeter, at full quality.")]
        [Min(0f)] public float sparkRate = 55f;
        [Min(0.01f)] public float sparkLifetime = 0.85f;
        [Min(0f)] public float sparkSpeed = 0.75f;
        [Min(0.001f)] public float sparkSize = 0.022f;
        [Range(0f, 8f)] public float sparkIntensity = 2.2f;

        [Header("Energy streaks")]
        [Tooltip("Much rarer than sparks. These are the occasional discharges, not the " +
                 "constant fizz.")]
        [Min(0f)] public float streakRate = 4.5f;
        [Min(0f)] public float streakSpeed = 2.6f;
        [Min(0.01f)] public float streakLifetime = 0.55f;
        [Min(0.01f)] public float trailLifetime = 0.22f;
        [Min(0.001f)] public float trailWidth = 0.035f;
        [Range(0f, 12f)] public float streakIntensity = 4.5f;

        [Header("Wisps")]
        [Tooltip("Slow translucent motes crawling around the rim. Low count on purpose: they " +
                 "reinforce the shader, they do not cover it.")]
        [Min(0f)] public float wispRate = 7f;
        [Min(0.01f)] public float wispLifetime = 2.6f;
        [Min(0.001f)] public float wispSize = 0.09f;

        // ---- light -------------------------------------------------------------------------

        [Header("Light")]
        [Tooltip("Leave black to derive it from the energy colour, which is what keeps the " +
                 "doorway lit in the portal's own colour when that colour changes.")]
        public Color lightColor = Color.black;

        [Min(0f)] public float lightIntensity = 3.2f;
        [Min(0f)] public float lightRange = 4.5f;

        [Tooltip("How much the light breathes once the portal is open. Low: a portal is not a " +
                 "faulty strip light.")]
        [Range(0f, 0.5f)] public float lightVariation = 0.08f;

        // ---- purchased artwork ---------------------------------------------------------------

        [Header("Purchased artwork")]
        [Tooltip("Drive the energy with a bought pack's textures instead of colour alone. The " +
                 "silhouette stays procedural either way - a pack changes what the energy " +
                 "looks like, never where the hole is.")]
        public bool usePurchasedArtwork = false;

        [Tooltip("The pack's energy/flow image. Sampled in the same rotating space as the " +
                 "procedural layers, so it turns with them.")]
        public Texture energyTexture;

        [Tooltip("The pack's mask, red channel. Modulates how hot the rim gets. Optional.")]
        public Texture maskTexture;

        [Range(0.05f, 8f)] public float artworkScale = 1f;
        [Range(-4f, 4f)] public float artworkDrift = 0.35f;

        [Tooltip("How far the artwork is allowed to take over. At zero the portal is exactly " +
                 "the procedural one, pixel for pixel.")]
        [Range(0f, 1f)] public float artworkInfluence = 0.75f;

        /// <summary>
        /// Whether the textured path should actually be switched on.
        ///
        /// <para>
        /// The tick alone is not enough, and that is deliberate. With the keyword on and no
        /// energy texture bound the shader samples the default black, multiplies the energy by
        /// it and the portal goes DARK - a portal that stops glowing because a slot is empty
        /// looks like a broken portal, not like an unconfigured one. Ticking the box with
        /// nothing assigned therefore keeps the procedural portal.
        /// </para>
        /// </summary>
        public bool ArtworkActive => usePurchasedArtwork && energyTexture != null;

        // ---- animation ---------------------------------------------------------------------

        [Header("Animation")]
        [Tooltip("How long the energy takes to reach full. The rim leads this; the view has " +
                 "its own fade below.")]
        [Min(0f)] public float openDuration = 1.1f;

        [Tooltip("How long the far room takes to fade up once the destination camera exists.")]
        [Min(0f)] public float destinationFadeDuration = 0.55f;

        [Tooltip("How long the portal takes to visibly come apart when preparation fails.")]
        [Min(0f)] public float destabiliseDuration = 0.9f;

        [Tooltip("How long the tear takes to shut when a hunt seals it, and to reopen after. " +
                 "Faster than the first opening: this is a wall slamming, not one tearing.")]
        [Min(0f)] public float huntSealDuration = 0.35f;

        // ---- performance --------------------------------------------------------------------

        [Header("Performance")]
        // The buffer height ladder. Width always follows the screen aspect, because the view is
        // sampled in screen space - only the height is a quality decision.
        //
        // These are the two ENDS, and the quality level slides between them. There is
        // deliberately no tier enum here: the project has exactly one notion of how much
        // machine this is, QualitySettings.GetQualityLevel(), and a second one that could
        // disagree with it is worse than a coarse one that cannot. A four-level project
        // therefore lands on roughly 640 / 787 / 933 / 1080, which is the mobile-low to
        // desktop-high span the platform tiers ask for; maxViewResolution is what an Ultra
        // level is allowed to reach past that.
        [Tooltip("Buffer height in pixels at the LOWEST quality level. Mobile low sits here.")]
        [Min(128)] public int minViewResolution = 640;

        [Tooltip("Buffer height in pixels at the TOP quality level.")]
        [Min(128)] public int viewResolution = 1080;

        [Tooltip("Hard ceiling. Only reached if a quality level and an aspect ratio ask for it.")]
        [Min(128)] public int maxViewResolution = 2160;

        [Tooltip("Stop rendering the far room beyond this. A second pass over a whole house is " +
                 "not something to run while the player is across the lobby.")]
        [Min(1f)] public float renderDistance = 9f;

        [Tooltip("Multiplies every emission rate. Scaled again by the quality level on top of " +
                 "this, so mobile gets far fewer without a second particle system existing.")]
        [Range(0f, 2f)] public float particleQuality = 1f;

        [Tooltip("How often the far room is re-rendered at the TOP quality level, in hertz. " +
                 "Zero means every frame, which is what a portal you can walk up to wants.")]
        [Range(0f, 120f)] public float refreshHz = 0f;

        [Tooltip("The same at the LOWEST quality level. The far room is a second pass over a " +
                 "whole house, so halving its rate on a phone buys back most of that cost - but " +
                 "the parallax stops being smooth as you strafe, so it is a trade, not a free " +
                 "win. Zero means every frame here too.")]
        [Range(0f, 120f)] public float mobileRefreshHz = 30f;

        /// <summary>
        /// Seconds between refreshes on this device, or zero for every frame.
        ///
        /// <para>
        /// Interpolated across the quality levels by the same fraction the buffer size and the
        /// particle rates use, so there is one notion of "how much machine is this" and not
        /// three.
        /// </para>
        /// </summary>
        public float RefreshInterval()
        {
            float hz = Mathf.Lerp(mobileRefreshHz, refreshHz, QualityFraction01());
            return hz <= 0.01f ? 0f : 1f / hz;
        }

        /// <summary>
        /// Where the quality level puts this device, 0 at the lowest and 1 at the highest.
        ///
        /// <para>
        /// The project's one convention for this, shared with <c>MirrorCorner</c> and the
        /// portal's own buffer sizing: the position of the active quality level within
        /// <c>QualitySettings.names</c>. There is deliberately no second tier enum - a parallel
        /// quality system is a way for two parts of one frame to disagree about what device
        /// they are on.
        /// </para>
        /// </summary>
        public static float QualityFraction01()
        {
            string[] names = QualitySettings.names;
            int levels = names != null && names.Length > 0 ? names.Length : 1;
            if (levels <= 1)
                return 1f;

            int level = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, levels - 1);
            return (float)level / (levels - 1);
        }

        /// <summary>
        /// The particle multiplier for this device: the authored quality, scaled by where the
        /// quality level sits. Low end keeps a quarter of the sparks, not none - the identity
        /// has to survive, only the density changes.
        /// </summary>
        public float ResolveParticleScale()
        {
            return Mathf.Max(0f, particleQuality) * Mathf.Lerp(0.25f, 1f, QualityFraction01());
        }

        /// <summary>The light's colour: authored, or the energy colour when none was set.</summary>
        public Color ResolveLightColor()
        {
            if (lightColor.r > 0.001f || lightColor.g > 0.001f || lightColor.b > 0.001f)
                return lightColor;

            Color c = energyColor;
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 1f)
                return new Color(c.r, c.g, c.b, 1f);

            return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
        }
    }
}
