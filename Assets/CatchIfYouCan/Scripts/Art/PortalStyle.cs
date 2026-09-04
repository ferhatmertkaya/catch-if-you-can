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
        [Tooltip("The clear hole in metres. The lobby doorway measures 1.07 between the jamb " +
                 "faces and 2.4 to the lintel.")]
        public Vector2 openingSize = new Vector2(1.06f, 2.4f);

        [Tooltip("Half size of the torn breach, as a fraction of the quad. Below 1 on purpose: " +
                 "the space left over is where the ragged edge and the outer energy spill.")]
        public Vector2 breachHalfSize = new Vector2(0.78f, 0.90f);

        [Tooltip("How far the wall's edge is chewed away from a clean rectangle. Zero is a neat " +
                 "cut; higher is a hole something came through.")]
        [Range(0f, 0.5f)] public float tearAmount = 0.16f;

        [Tooltip("Size of the chunks the edge breaks into. Low is a few big pieces of plaster, " +
                 "high is fine crumbling.")]
        [Range(0.5f, 24f)] public float tearScale = 4.5f;

        [Tooltip("Width of the burning band, as a fraction of the oval's radius.")]
        [Range(0.01f, 0.9f)] public float rimWidth = 0.26f;

        [Range(0.05f, 4f)] public float rimSoftness = 1.15f;

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
        [Range(0f, 16f)] public float coreIntensity = 6.0f;
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
        [Tooltip("Height of the view buffer in pixels at the top quality level. Width follows " +
                 "the screen aspect, because the view is sampled in screen space.")]
        [Min(128)] public int viewResolution = 1024;

        [Min(128)] public int maxViewResolution = 2048;

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
