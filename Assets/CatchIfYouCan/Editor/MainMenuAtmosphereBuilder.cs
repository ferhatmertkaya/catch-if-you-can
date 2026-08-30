using CatchIfYouCan.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Builds and configures the 01_MainMenu doorway atmosphere: the escaping fog layers, the
    /// doorway lights and the portal recess.
    /// <para>
    /// The doorway measurements below were taken from the corridor mesh itself
    /// (CIYC_MainMenu_Corridor.fbx as placed in 01_MainMenu), not estimated from the camera.
    /// The aperture is small — roughly 30 cm wide by 55 cm tall — so every distance here is
    /// scaled to that opening rather than to a human-sized door.
    /// </para>
    /// <para>
    /// The tool is idempotent: it looks objects up by name, reuses what already exists and
    /// rewrites their configuration, so running it twice produces the same scene as running it
    /// once. It never deletes user geometry.
    /// </para>
    /// </summary>
    public static class MainMenuAtmosphereBuilder
    {
        // ---- Measured doorway geometry, world space, from the corridor mesh ----------------
        private const float DoorCenterX = 2.074f;
        private const float DoorCenterY = -0.131f;
        private const float DoorPlaneZ = 1.122f;   // outer face of the far wall
        private const float DoorWidth = 0.299f;
        private const float DoorHeight = 0.555f;
        private const float FloorY = -0.44f;

        // The corridor runs along Z and the camera sits at +Z looking back down it, so fog
        // escaping the doorway travels along +Z. Derived from the mesh bounds, not assumed.
        private static readonly Vector3 Outward = Vector3.forward;

        private const string FogRootName = "DoorFog_Root";
        private const string AtmosphereRootName = "MainMenu_Atmosphere";

        private static readonly Color SpectralGreen = new Color32(0x20, 0xBF, 0x5B, 0xFF);

        [MenuItem("Catch If You Can/Main Menu/Rebuild Door Atmosphere", false, 20)]
        public static void RebuildDoorAtmosphere()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "01_MainMenu")
            {
                EditorUtility.DisplayDialog(
                    "Rebuild Door Atmosphere",
                    "Open Assets/CatchIfYouCan/Scenes/01_MainMenu.unity first.\n\n" +
                    $"Active scene is '{scene.name}'.",
                    "OK");
                return;
            }

            var fogRoot = FindOrCreateRoot(FogRootName);
            fogRoot.transform.position = new Vector3(DoorCenterX, DoorCenterY, DoorPlaneZ);
            fogRoot.transform.rotation = Quaternion.identity;
            fogRoot.transform.localScale = Vector3.one;

            var layers = new[]
            {
                BuildBase(fogRoot.transform),
                BuildStretch(fogRoot.transform),
                BuildTendrils(fogRoot.transform),
                BuildWisps(fogRoot.transform),
            };

            var lights = ConfigureDoorLights();
            ConfigurePortal();
            RetireLegacyFog();

            WireController(layers, lights);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[CIYC] Door atmosphere rebuilt: {layers.Length} fog layers, " +
                $"{lights.Length} active doorway lights. Save the scene to keep it.");
        }

        // ---------------------------------------------------------------- fog layers -------

        private static ParticleSystem BuildBase(Transform parent)
        {
            // Primary soft mass: most of the visible fog.
            var ps = FindOrCreateSystem(parent, "DoorFog_Base", new Vector3(0f, -0.06f, -0.04f));
            ConfigureCommon(ps, lifetimeMin: 0.9f, lifetimeMax: 1.6f,
                speedMin: 0.06f, speedMax: 0.16f,
                sizeMin: 0.12f, sizeMax: 0.30f,
                rate: 9f, maxParticles: 30,
                tint: new Color(0.55f, 0.62f, 0.57f));

            var shape = ps.shape;
            shape.scale = new Vector3(DoorWidth * 0.74f, DoorHeight * 0.54f, 0.04f);

            SetAlphaEnvelope(ps, peak: 0.26f);
            SetSizeGrowth(ps, birth: 0.80f, death: 1.22f);
            SetDrift(ps, outward: 0.10f, rise: 0.03f, lateral: 0.030f);
            SetNoise(ps, strength: 0.035f, frequency: 0.45f);
            AssignMaterial(ps, "Assets/CatchIfYouCan/Art/Particles/MAT_Fog_Soft.mat",
                ParticleSystemRenderMode.Billboard, sortOffset: 0);
            return ps;
        }

        private static ParticleSystem BuildStretch(Transform parent)
        {
            // Elongated shapes that break up the round silhouettes of the base layer.
            var ps = FindOrCreateSystem(parent, "DoorFog_Stretch", new Vector3(0f, -0.03f, 0.04f));
            ConfigureCommon(ps, lifetimeMin: 0.8f, lifetimeMax: 1.4f,
                speedMin: 0.10f, speedMax: 0.24f,
                sizeMin: 0.14f, sizeMax: 0.34f,
                rate: 4f, maxParticles: 14,
                tint: new Color(0.52f, 0.60f, 0.55f));

            var shape = ps.shape;
            shape.scale = new Vector3(DoorWidth * 0.66f, DoorHeight * 0.40f, 0.03f);

            SetAlphaEnvelope(ps, peak: 0.20f);
            SetSizeGrowth(ps, birth: 0.85f, death: 1.28f);
            SetDrift(ps, outward: 0.15f, rise: 0.02f, lateral: 0.040f);
            SetNoise(ps, strength: 0.045f, frequency: 0.55f);
            AssignMaterial(ps, "Assets/CatchIfYouCan/Art/Particles/MAT_Fog_Stretch.mat",
                ParticleSystemRenderMode.Billboard, sortOffset: 1);
            return ps;
        }

        private static ParticleSystem BuildTendrils(Transform parent)
        {
            // Rare smoke fingers. Sparse on purpose: the viewer should catch them occasionally.
            var ps = FindOrCreateSystem(parent, "DoorFog_Tendrils", new Vector3(0f, -0.05f, -0.01f));
            ConfigureCommon(ps, lifetimeMin: 0.8f, lifetimeMax: 1.6f,
                speedMin: 0.08f, speedMax: 0.20f,
                sizeMin: 0.08f, sizeMax: 0.20f,
                rate: 1.5f, maxParticles: 8,
                tint: new Color(0.48f, 0.62f, 0.52f));

            var shape = ps.shape;
            shape.scale = new Vector3(DoorWidth * 0.52f, DoorHeight * 0.46f, 0.03f);

            SetAlphaEnvelope(ps, peak: 0.16f);
            SetSizeGrowth(ps, birth: 0.75f, death: 1.30f);
            SetDrift(ps, outward: 0.12f, rise: 0.05f, lateral: 0.050f);
            SetNoise(ps, strength: 0.070f, frequency: 0.75f);
            AssignMaterial(ps, "Assets/CatchIfYouCan/Art/Particles/MAT_Fog_Tendrils.mat",
                ParticleSystemRenderMode.Billboard, sortOffset: 2);
            return ps;
        }

        private static ParticleSystem BuildWisps(Transform parent)
        {
            // Small floating detail: life without noise.
            var ps = FindOrCreateSystem(parent, "DoorFog_Wisps", new Vector3(0f, -0.02f, 0.06f));
            ConfigureCommon(ps, lifetimeMin: 0.7f, lifetimeMax: 1.2f,
                speedMin: 0.07f, speedMax: 0.18f,
                sizeMin: 0.05f, sizeMax: 0.12f,
                rate: 3f, maxParticles: 12,
                tint: new Color(0.54f, 0.64f, 0.58f));

            var shape = ps.shape;
            shape.scale = new Vector3(DoorWidth * 0.74f, DoorHeight * 0.54f, 0.03f);

            SetAlphaEnvelope(ps, peak: 0.18f);
            SetSizeGrowth(ps, birth: 0.70f, death: 1.15f);
            SetDrift(ps, outward: 0.09f, rise: 0.06f, lateral: 0.055f);
            SetNoise(ps, strength: 0.060f, frequency: 0.85f);
            AssignMaterial(ps, "Assets/CatchIfYouCan/Art/Particles/MAT_Fog_Wisp.mat",
                ParticleSystemRenderMode.Billboard, sortOffset: 3);
            return ps;
        }

        // ---------------------------------------------------------------- module setup -----

        private static void ConfigureCommon(ParticleSystem ps,
            float lifetimeMin, float lifetimeMax,
            float speedMin, float speedMax,
            float sizeMin, float sizeMax,
            float rate, int maxParticles,
            Color tint)
        {
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;                     // no empty doorway on the first frame
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            main.gravityModifier = 0f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.cullingMode = ParticleSystemCullingMode.Automatic;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.randomDirectionAmount = 0f;

            // Modules the menu does not need. Collisions and trails are pure overhead on a
            // static title screen, and the fog is designed never to reach a wall.
            var collision = ps.collision; collision.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            var sub = ps.subEmitters; sub.enabled = false;
            var lightsModule = ps.lights; lightsModule.enabled = false;

            // Individual billboard roll varies, but nothing rotates the cloud as a group.
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
        }

        private static void SetAlphaEnvelope(ParticleSystem ps, float peak)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peak * 0.62f, 0.13f),
                    new GradientAlphaKey(peak, 0.45f),
                    new GradientAlphaKey(peak * 0.45f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });

            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetSizeGrowth(ParticleSystem ps, float birth, float death)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;

            var curve = new AnimationCurve(
                new Keyframe(0f, birth),
                new Keyframe(0.45f, 1f),
                new Keyframe(1f, death));

            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>
        /// Short outward drift through the opening plus a little rise and sideways wander.
        /// Velocities are metres per second against a 30 cm doorway, so the visible travel is
        /// on the order of 15–45 cm before the alpha envelope has faded the particle out.
        /// </summary>
        private static void SetDrift(ParticleSystem ps, float outward, float rise, float lateral)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-lateral, lateral);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.25f, rise);
            // Outward is +Z for this doorway (see field comment), so the outward component is
            // the Z axis; signed by Outward.z so the intent survives if the set is ever flipped.
            velocity.z = new ParticleSystem.MinMaxCurve(
                outward * 0.55f * Outward.z, outward * Outward.z);

            // Bleed off speed so particles settle instead of sailing down the corridor.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.limit = new ParticleSystem.MinMaxCurve(Mathf.Max(outward, 0.12f));
            limit.dampen = 0.35f;
        }

        private static void SetNoise(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.12f;
            noise.damping = true;
            noise.octaveCount = 1;
        }

        private static void AssignMaterial(ParticleSystem ps, string materialPath,
            ParticleSystemRenderMode mode, int sortOffset)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Debug.LogError($"[CIYC] Fog material missing: {materialPath}");
                return;
            }

            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sortingFudge = sortOffset;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 0.5f;
            renderer.minParticleSize = 0f;
        }

        // ---------------------------------------------------------------- lighting ---------

        /// <summary>
        /// Corrects the existing doorway lights instead of adding more.
        /// Two stay live — a back glow inside the room and a short outward spill — which is
        /// what the mobile quality tier budgets for additional per-pixel lights, so the Editor
        /// and a device light the doorway the same way. The other two are kept in the scene
        /// but switched off rather than deleted.
        /// </summary>
        private static Light[] ConfigureDoorLights()
        {
            var backGlow = FindLight("Door_BackGlow");
            if (backGlow != null)
            {
                backGlow.transform.position = new Vector3(DoorCenterX, DoorCenterY + 0.02f, DoorPlaneZ - 0.10f);
                backGlow.type = LightType.Point;
                backGlow.color = SpectralGreen;
                backGlow.intensity = 1.35f;
                backGlow.range = 0.48f;
                backGlow.shadows = LightShadows.None;
                backGlow.renderMode = LightRenderMode.ForcePixel;
                backGlow.enabled = true;
            }

            var spill = FindLight("Door_Green_Spot");
            if (spill != null)
            {
                // Just inside the opening, aimed outward and slightly down so the pool lands on
                // the floor immediately outside the doorway rather than up the corridor walls.
                spill.transform.position = new Vector3(DoorCenterX, DoorCenterY + 0.16f, DoorPlaneZ - 0.02f);
                spill.transform.rotation = Quaternion.LookRotation(
                    new Vector3(0f, -(DoorCenterY + 0.16f - FloorY), Outward.z * 0.55f).normalized,
                    Vector3.up);
                spill.type = LightType.Spot;
                spill.color = SpectralGreen;
                spill.intensity = 2.1f;
                spill.range = 0.85f;
                spill.spotAngle = 58f;
                spill.innerSpotAngle = 26f;
                spill.shadows = LightShadows.None;
                spill.renderMode = LightRenderMode.ForcePixel;
                spill.enabled = true;
            }

            // Previously a range-12 spot and a point light parked above the ceiling; both threw
            // the wide cyan patches across the corridor. Off, not deleted.
            DisableLight("Door_Green_Spot (1)");
            DisableLight("Door_Green_Fill");

            var live = new System.Collections.Generic.List<Light>();
            if (backGlow != null) live.Add(backGlow);
            if (spill != null) live.Add(spill);
            return live.ToArray();
        }

        private static Light FindLight(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<Light>() : null;
        }

        private static void DisableLight(string name)
        {
            var light = FindLight(name);
            if (light == null)
                return;

            light.enabled = false;
            EditorUtility.SetDirty(light);
        }

        // ---------------------------------------------------------------- portal -----------

        /// <summary>
        /// The portal was a 1 m opaque emissive cube behind a 30 cm doorway, which read as a
        /// lit slab wedged in the wall. Shrunk to sit inside the aperture, pushed back into the
        /// room so the doorframe occludes its edges, and stripped of shadow casting so it reads
        /// as depth rather than as a surface.
        /// </summary>
        private static void ConfigurePortal()
        {
            var portal = GameObject.Find("Door_Green_Portal_Cube");
            if (portal == null)
                return;

            portal.transform.position = new Vector3(DoorCenterX, DoorCenterY, DoorPlaneZ - 0.16f);
            portal.transform.rotation = Quaternion.identity;
            portal.transform.localScale = new Vector3(DoorWidth * 0.94f, DoorHeight * 0.94f, 0.02f);

            var renderer = portal.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            // A collider on a menu backdrop only costs physics work.
            var collider = portal.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            EditorUtility.SetDirty(portal);
        }

        /// <summary>
        /// The original single-emitter Door_Green_Fog pointed at a material GUID that no longer
        /// exists in the project, so it rendered with Unity's missing-material magenta. Its
        /// job now belongs to the four DoorFog_* layers. Switched off and left in place so
        /// nothing the user authored is destroyed.
        /// </summary>
        private static void RetireLegacyFog()
        {
            var legacy = GameObject.Find("Door_Green_Fog");
            if (legacy == null)
                return;

            var ps = legacy.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            legacy.SetActive(false);
            EditorUtility.SetDirty(legacy);
        }

        // ---------------------------------------------------------------- wiring -----------

        private static void WireController(ParticleSystem[] layers, Light[] lights)
        {
            var root = FindOrCreateRoot(AtmosphereRootName);
            var controller = root.GetComponent<MainMenuAtmosphereController>();
            if (controller == null)
                controller = root.AddComponent<MainMenuAtmosphereController>();

            var so = new SerializedObject(controller);
            AssignArray(so.FindProperty("fogLayers"), layers);
            AssignArray(so.FindProperty("doorLights"), lights);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
        }

        private static void AssignArray(SerializedProperty property, Object[] values)
        {
            if (property == null)
                return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------------------------------------------------------------- helpers ----------

        private static GameObject FindOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        private static ParticleSystem FindOrCreateSystem(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = parent.Find(name);
            GameObject go;
            if (child != null)
            {
                go = child.gameObject;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            }

            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.SetActive(true);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                ps = go.AddComponent<ParticleSystem>();

            return ps;
        }
    }
}
