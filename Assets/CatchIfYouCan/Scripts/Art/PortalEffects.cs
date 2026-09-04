using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Everything burning around the opening that is not the surface itself: sparks, occasional
    /// discharges, drifting wisps, and the one light that puts the portal into the room.
    ///
    /// <para>
    /// <b>All four emit on the OVAL, not in a box and not on a circle.</b> The previous version
    /// emitted wisps from a Box the size of the whole doorway, so they came out of the middle
    /// of the opening - through the view - instead of off its edge. Unity's Circle shape is
    /// round, and a round emitter around a 1.06 x 2.4 doorway is visibly wrong at the top and
    /// bottom, so the shape is scaled to the portal's own proportions and its thickness set to
    /// zero, which puts every particle on the contour.
    /// </para>
    ///
    /// <para>
    /// <b>One light, never more.</b> The lobby already renders a mirror and a portal camera;
    /// a handful of extra real-time lights around a doorway is how a scene stops fitting in the
    /// per-object light budget. Its colour comes from the energy colour, so re-tinting the
    /// portal re-tints what it throws on the door frame without a second edit.
    /// </para>
    ///
    /// <para>
    /// Built in code because the opening's size is decided by the doorway that owns it, and a
    /// prefab would have to be resized at spawn anyway. It uses the project's existing unlit
    /// particle shader through <see cref="CiycShaders"/>, which returns null rather than
    /// resolving to a magenta built-in one.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalEffects : MonoBehaviour
    {
        private PortalStyle _style;
        private float _particleScale = 1f;

        private ParticleSystem _sparks;
        private ParticleSystem _streaks;
        private ParticleSystem _wisps;

        private Light _light;
        private float _lightBase;
        private float _intensity;
        private bool _destabilising;

        /// <summary>
        /// Builds the effects as a child of the surface, sized to the opening it belongs to.
        /// </summary>
        public static PortalEffects Build(Transform parent, PortalStyle style)
        {
            if (parent == null)
                return null;

            style = style ?? new PortalStyle();

            var go = new GameObject("PortalFX");
            go.transform.SetParent(parent, false);

            var fx = go.AddComponent<PortalEffects>();
            fx._style = style;
            fx._particleScale = style.ResolveParticleScale();

            fx._sparks = fx.BuildSparks();
            fx._streaks = fx.BuildStreaks();
            fx._wisps = fx.BuildWisps();
            fx.BuildLight();

            fx.SetIntensity(0f);
            return fx;
        }

        /// <summary>
        /// How far open the portal is, 0 to 1. Drives emission and the light together, so the
        /// effects arrive with the opening rather than being switched on beside it.
        /// </summary>
        public void SetIntensity(float t)
        {
            _intensity = Mathf.Clamp01(t);

            Drive(_sparks, _style.sparkRate * _particleScale * _intensity);
            // Streaks hold off until the rim is genuinely burning. A discharge from a portal
            // that has barely started forming reads as a glitch rather than as power.
            Drive(_streaks, _style.streakRate * _particleScale *
                            Mathf.Clamp01((_intensity - 0.35f) / 0.65f));
            Drive(_wisps, _style.wispRate * _particleScale * _intensity);

            if (_light != null)
            {
                _light.enabled = _intensity > 0.01f;
                _light.intensity = _lightBase * _intensity;
            }
        }

        /// <summary>
        /// The light breathes very slightly once the portal is open, and not at all while it is
        /// still forming. Low amplitude on purpose: a portal is not a faulty strip light.
        /// </summary>
        private void Update()
        {
            if (_light == null || !_light.enabled || _destabilising || _intensity < 0.99f)
                return;

            float wave = Mathf.Sin(Time.time * 1.7f) * 0.6f + Mathf.Sin(Time.time * 3.1f) * 0.4f;
            _light.intensity = _lightBase * (1f + wave * _style.lightVariation);
        }

        /// <summary>
        /// Comes apart, visibly. Used when the mission world could not be prepared: the portal
        /// has to be seen to fail, because a doorway that silently stops existing is
        /// indistinguishable from a button that did nothing.
        /// </summary>
        public void SetDestabilising(bool value)
        {
            _destabilising = value;
            if (!value)
                return;

            // Emission stops but the particles already in the air are left to finish, so the
            // last sparks drift off an edge that is no longer there.
            StopEmitting(_sparks);
            StopEmitting(_streaks);
            StopEmitting(_wisps);
        }

        private void OnDestroy()
        {
            DestroyMaterials(_sparks);
            DestroyMaterials(_streaks);
            DestroyMaterials(_wisps);
        }

        // ---- systems ---------------------------------------------------------------------

        /// <summary>
        /// Sparks: the constant fizz. Small, short-lived, mostly outward off the rim with
        /// enough tangential drift that the pattern never reads as a fountain.
        /// </summary>
        private ParticleSystem BuildSparks()
        {
            ParticleSystem ps = CreateSystem("Sparks", out ParticleSystemRenderer renderer);
            if (ps == null)
                return null;

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(_style.sparkLifetime * 0.6f,
                                                               _style.sparkLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_style.sparkSpeed * 0.35f,
                                                             _style.sparkSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(_style.sparkSize * 0.5f,
                                                            _style.sparkSize);
            main.startColor = Scaled(_style.sparkColor, _style.sparkIntensity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.gravityModifier = -0.02f;

            ShapeToOval(ps);

            // Tangential drift, so they curl off the edge instead of firing straight out.
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);

            FadeOverLifetime(ps, _style.sparkColor, _style.energyColor);
            SparkSizeOverLifetime(ps);

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>
        /// Streaks: the rare discharge. Fast, trailed, curving, and far less frequent than the
        /// sparks - the point of them is that they are an event.
        /// </summary>
        private ParticleSystem BuildStreaks()
        {
            ParticleSystem ps = CreateSystem("EnergyStreaks", out ParticleSystemRenderer renderer);
            if (ps == null)
                return null;

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(_style.streakLifetime * 0.6f,
                                                               _style.streakLifetime * 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_style.streakSpeed * 0.55f,
                                                             _style.streakSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(_style.trailWidth * 0.7f,
                                                            _style.trailWidth * 1.3f);
            main.startColor = Scaled(_style.sparkColor, _style.streakIntensity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            ShapeToOval(ps);

            // The curve. Straight identical lines are lasers; a discharge bends.
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.2f, 1.4f);

            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;

            FadeOverLifetime(ps, _style.sparkColor, _style.energyColor);

            ParticleSystem.TrailModule trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 1f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(_style.trailLifetime);
            trails.minVertexDistance = 0.015f;
            trails.dieWithParticles = false;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            // Tapers to nothing behind the head, which is what makes it a streak rather than a
            // ribbon of constant width.
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.trailMaterial = renderer.sharedMaterial;
            return ps;
        }

        /// <summary>
        /// Wisps: slow translucent motes crawling round the rim. The rate is low on purpose -
        /// this is the air being disturbed, not a smoke machine.
        /// </summary>
        private ParticleSystem BuildWisps()
        {
            ParticleSystem ps = CreateSystem("AmbientWisps", out ParticleSystemRenderer renderer);
            if (ps == null)
                return null;

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(_style.wispLifetime * 0.7f,
                                                               _style.wispLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
            main.startSize = new ParticleSystem.MinMaxCurve(_style.wispSize * 0.6f,
                                                            _style.wispSize * 1.4f);
            main.startColor = Scaled(_style.energyColor, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ShapeToOval(ps);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            // Expanding while losing opacity, which is what makes them read as dissipating gas
            // rather than as shrinking dots.
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));

            FadeOverLifetime(ps, _style.energyColor, _style.outerColor);

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>
        /// The one light. Point rather than spot so the jambs on both sides catch it, no
        /// shadows because a doorway-sized light casting shadows in a lobby that already
        /// renders a mirror and a portal camera is the most expensive thing in the frame.
        /// </summary>
        private void BuildLight()
        {
            var go = new GameObject("PortalLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.35f);

            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _style.ResolveLightColor();
            _light.range = Mathf.Max(0.1f, _style.lightRange);
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForceVertex;

            _lightBase = Mathf.Max(0f, _style.lightIntensity);
            _light.intensity = 0f;
            _light.enabled = false;
        }

        // ---- shared ----------------------------------------------------------------------

        /// <summary>
        /// The oval emitter. A Circle with zero thickness puts every particle on the contour,
        /// and the shape's own scale - not the transform's, which would resize the particles
        /// too - stretches that circle into the portal's proportions.
        /// </summary>
        private void ShapeToOval(ParticleSystem ps)
        {
            Vector2 opening = _style.openingSize;
            Vector2 fit = _style.ovalFit;

            float halfWidth = Mathf.Max(0.01f, opening.x * 0.5f * Mathf.Clamp(fit.x, 0.05f, 1f));
            float halfHeight = Mathf.Max(0.01f, opening.y * 0.5f * Mathf.Clamp(fit.y, 0.05f, 1f));

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.scale = new Vector3(halfWidth, halfHeight, 1f);
            shape.randomDirectionAmount = 0.18f;
        }

        private ParticleSystem CreateSystem(string label, out ParticleSystemRenderer renderer)
        {
            renderer = null;

            Shader shader = CiycShaders.Find(CiycShaders.ParticlesUnlit);
            if (shader == null)
            {
                Debug.LogError("[CIYC][Portal] No unlit particle shader, so '" + label +
                               "' cannot be built. The portal will have its rim but no sparks.");
                return null;
            }

            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(shader) { name = "Portal_" + label };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortMode = ParticleSystemSortMode.None;
            }

            return ps;
        }

        /// <summary>
        /// Birth white-green, middle the portal's own colour, death the same hue at zero alpha.
        /// The alpha is what fades - scaling a fully opaque particle to nothing is a pop.
        /// </summary>
        private static void FadeOverLifetime(ParticleSystem ps, Color birth, Color death)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Normalised(birth), 0f),
                    new GradientColorKey(Normalised(death), 0.55f),
                    new GradientColorKey(Normalised(death), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.65f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });

            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Small, a brief flare, then away. Never a hard pop at zero.</summary>
        private static void SparkSizeOverLifetime(ParticleSystem ps)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0.1f));

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>
        /// Rate in, play/stop out. Emission is written rather than the system being destroyed
        /// and rebuilt, so nothing allocates a GameObject per frame of the opening ramp.
        /// </summary>
        private static void Drive(ParticleSystem ps, float rate)
        {
            if (ps == null)
                return;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);

            bool wanted = rate > 0.01f;
            if (wanted && !ps.isPlaying)
                ps.Play();
            else if (!wanted && ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void StopEmitting(ParticleSystem ps)
        {
            if (ps == null)
                return;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            if (ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void DestroyMaterials(ParticleSystem ps)
        {
            if (ps == null)
                return;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                Destroy(renderer.sharedMaterial);
        }

        /// <summary>
        /// An HDR colour turned into a particle start colour at the given intensity. Particle
        /// vertex colours carry HDR fine, but alpha has to survive the multiply or the whole
        /// system is invisible.
        /// </summary>
        private static Color Scaled(Color c, float intensity)
        {
            float k = Mathf.Max(0f, intensity);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>The same hue at unit brightness, for gradient keys, whose alpha is separate.</summary>
        private static Color Normalised(Color c)
        {
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 1f)
                return new Color(c.r, c.g, c.b, 1f);

            return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
        }
    }
}
