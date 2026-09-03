using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The green energy at the edge of an opening: a soft glow on the frame and a few wisps
    /// drifting out of it.
    ///
    /// <para>
    /// <b>Deliberately small.</b> The portal is a hole, and what sells a hole is the view
    /// through it - a hole surrounded by a firework is a firework. This is a low-rate emitter
    /// confined to the plane of the opening and a single additive quad behind the frame, both
    /// scaled by how far open the portal is, so a closed doorway emits nothing at all.
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
        private static readonly Color Energy = new Color(0.16f, 0.95f, 0.45f);

        private ParticleSystem _wisps;
        private float _baseRate;
        private Renderer _glow;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TintId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Builds the effects as a child of the surface, sized to the opening.
        /// </summary>
        public static PortalEffects Build(Transform parent, Vector2 openingSize)
        {
            if (parent == null)
                return null;

            var go = new GameObject("PortalFX");
            go.transform.SetParent(parent, false);

            var fx = go.AddComponent<PortalEffects>();
            fx._block = new MaterialPropertyBlock();
            fx.BuildGlow(openingSize);
            fx.BuildWisps(openingSize);
            fx.SetIntensity(0f);
            return fx;
        }

        /// <summary>
        /// How far open the portal is, 0 to 1. Drives emission and glow together, so the
        /// effects arrive with the opening rather than being switched on beside it.
        /// </summary>
        public void SetIntensity(float t)
        {
            float k = Mathf.Clamp01(t);

            if (_wisps != null)
            {
                ParticleSystem.EmissionModule emission = _wisps.emission;
                emission.rateOverTime = _baseRate * k;

                if (k > 0.01f && !_wisps.isPlaying)
                    _wisps.Play();
                else if (k <= 0.01f && _wisps.isPlaying)
                    _wisps.Stop();
            }

            if (_glow != null)
            {
                // A property block, not a material instance: this is written every frame of the
                // opening, and a per-frame material allocation is a per-frame leak.
                Color c = Energy;
                c.a = 0.55f * k;
                _glow.GetPropertyBlock(_block);
                _block.SetColor(ColorId, c);
                _block.SetColor(TintId, c);
                _glow.SetPropertyBlock(_block);
                _glow.enabled = k > 0.01f;
            }
        }

        /// <summary>
        /// One additive quad slightly behind the surface, a little larger than the opening, so
        /// the frame is lit from inside rather than the opening having a drawn-on border.
        /// </summary>
        private void BuildGlow(Vector2 openingSize)
        {
            Shader shader = CiycShaders.Find(CiycShaders.ParticlesUnlit);
            if (shader == null)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "RimGlow";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            go.transform.localScale = new Vector3(openingSize.x * 1.35f, openingSize.y * 1.2f, 1f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            _glow = go.GetComponent<Renderer>();
            var material = new Material(shader) { name = "Portal_RimGlow" };
            _glow.sharedMaterial = material;
            _glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glow.receiveShadows = false;
        }

        /// <summary>
        /// Wisps: a handful of slow motes rising out of the plane of the opening. The rate is
        /// low on purpose - this is the air being disturbed, not a smoke machine.
        /// </summary>
        private void BuildWisps(Vector2 openingSize)
        {
            var go = new GameObject("Wisps");
            go.transform.SetParent(transform, false);

            _wisps = go.AddComponent<ParticleSystem>();
            _wisps.Stop();

            ParticleSystem.MainModule main = _wisps.main;
            main.loop = true;
            main.startLifetime = 2.4f;
            main.startSpeed = 0.18f;
            main.startSize = 0.05f;
            main.startColor = Energy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            _baseRate = 9f;
            ParticleSystem.EmissionModule emission = _wisps.emission;
            emission.rateOverTime = 0f;

            // Confined to the plane of the doorway, so nothing drifts through the wall beside it.
            ParticleSystem.ShapeModule shape = _wisps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(openingSize.x, openingSize.y, 0.02f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader shader = CiycShaders.Find(CiycShaders.ParticlesUnlit);
            if (renderer != null && shader != null)
            {
                renderer.sharedMaterial = new Material(shader) { name = "Portal_Wisp" };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
