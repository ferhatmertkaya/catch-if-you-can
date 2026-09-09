using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Where the rest of the game asks for an equipment definition.
    ///
    /// <para>
    /// It prefers the authored <see cref="EquipmentCatalog"/> reached through the content
    /// registry, and falls back to building the same eleven definitions in code when the
    /// catalog has not been authored or imported yet. The fallback is a fallback: two callers
    /// asking for "flashlight" used to get two different ScriptableObjects, so nothing could
    /// compare definitions by reference and a battery charge written onto one was invisible to
    /// the next caller. The catalog hands back the same asset every time, and the fallback is
    /// now built once and cached so that it does too.
    /// </para>
    /// </summary>
    public static class EquipmentDefinitionFactory
    {
        private static EquipmentDefinition[] _fallback;

        /// <summary>
        /// Every definition, authored if there is a catalog and code-built if there is not.
        /// This is what a shop or a content hash should read.
        /// </summary>
        public static EquipmentDefinition[] All()
        {
            var catalog = Catalog();
            if (catalog != null && catalog.Count > 0)
                return catalog.Equipment;

            return CachedFallback();
        }

        /// <summary>The authored catalog, or null while the registry has not been created.</summary>
        public static EquipmentCatalog Catalog()
        {
            return Content.CiycContentRegistry.Load()?.EquipmentCatalog;
        }

        /// <summary>
        /// The code-built definitions, made once. Rebuilt after a domain reload, because the
        /// instances behind them do not survive one.
        /// </summary>
        private static EquipmentDefinition[] CachedFallback()
        {
            if (_fallback == null || _fallback.Length == 0 || _fallback[0] == null)
                _fallback = CreateAllDefaultDefinitions();

            return _fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _fallback = null;
        }

        /// <summary>
        /// The definitions as code, one instance per call. This is the authoring source the
        /// editor tools write the assets from, and the runtime fallback when they have not
        /// been written yet - reach it through <see cref="All"/> rather than directly.
        /// </summary>
        public static EquipmentDefinition[] CreateAllDefaultDefinitions()
        {
            return new[]
            {
                Create("flashlight", "Flashlight", EquipmentCategory.Visual, 0, 1,
                    batteryUsage: 0.35f,
                    maxBattery: 100f,
                    canPlace: false,
                    description: "Essential light source. Reveals UV traces when upgraded."),
                Create("emf_detector", "EMF Detector", EquipmentCategory.Detection, 150, 1,
                    batteryUsage: 0.6f,
                    maxBattery: 100f,
                    canPlace: false,
                    description: "Detects electromagnetic surges linked to entity activity."),
                Create("uv_light", "UV Light", EquipmentCategory.Visual, 175, 1,
                    batteryUsage: 0.55f,
                    maxBattery: 90f,
                    canPlace: false,
                    description: "Reveals hidden fingerprints, salt trails, and UV traces."),
                Create("thermometer", "Thermometer", EquipmentCategory.Detection, 125, 1,
                    batteryUsage: 0.25f,
                    maxBattery: 120f,
                    canPlace: false,
                    description: "Measures ambient temperature drops near the entity."),
                Create("evp_recorder", "EVP Recorder", EquipmentCategory.Audio, 200, 1,
                    batteryUsage: 0.45f,
                    maxBattery: 100f,
                    canPlace: true,
                    description: "Ask questions and capture spirit voice responses."),
                Create("parabolic_microphone", "Parabolic Microphone", EquipmentCategory.Audio, 275, 2,
                    batteryUsage: 0.7f,
                    maxBattery: 80f,
                    canPlace: false,
                    description: "Directional audio probe for distant anomalies."),
                Create("photo_camera", "Photo Camera", EquipmentCategory.Visual, 225, 1,
                    batteryUsage: 0.5f,
                    maxBattery: 60f,
                    canPlace: false,
                    description: "Capture photographic evidence and ghost manifestations."),
                Create("spectral_grid", "Spectral Grid Projector", EquipmentCategory.Detection, 300, 2,
                    batteryUsage: 0.85f,
                    maxBattery: 75f,
                    canPlace: true,
                    description: "Projects a grid that reveals spectral silhouettes."),
                Create("video_camera", "Video Camera", EquipmentCategory.Visual, 350, 2,
                    batteryUsage: 0.9f,
                    maxBattery: 90f,
                    canPlace: true,
                    description: "Remote monitoring camera for fixed surveillance points."),
                Create("warding_relic", "Warding Relic", EquipmentCategory.Protection, 350, 1,
                    batteryUsage: 0f,
                    maxBattery: 0f,
                    canPlace: true,
                    description: "Crystal ward that can interrupt an active hunt nearby."),
                Create("salt", "Salt", EquipmentCategory.Utility, 50, 1,
                    batteryUsage: 0f,
                    maxBattery: 0f,
                    canPlace: true,
                    canDrop: true,
                    description: "Place salt piles to reveal footprints under UV light.")
            };
        }

        public static EquipmentDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var catalog = Catalog();
            if (catalog != null)
            {
                var authored = catalog.Resolve(id);
                if (authored != null)
                    return authored;
            }

            foreach (var def in CachedFallback())
            {
                if (def != null && def.Id == id)
                    return def;
            }

            return null;
        }

        /// <summary>
        /// The visual for an item built in code.
        ///
        /// <para>
        /// <b>Without this every item in the game is a placeholder capsule.</b> The authored
        /// <c>VisualProfile_*</c> assets are not under Resources, so nothing at runtime can
        /// load them, and this factory - which IS the live source of definitions - left
        /// <c>VisualProfile</c> null. <c>EquipmentVisualFactory</c> then substituted its
        /// honest-placeholder capsule, which is why the finished flashlight model never
        /// appeared in anybody's hand.
        /// </para>
        ///
        /// <para>
        /// The torch, the UV light and the spectral grid projector have finished art. The
        /// rest get a placeholder that <b>says</b> it is one, which is the honest state and not
        /// something to paper over: an item that looks finished and is not is worse than an
        /// item that looks unfinished.
        /// </para>
        /// </summary>
        private static EquipmentVisualProfile BuildVisualProfile(string id, string displayName)
        {
            var profile = ScriptableObject.CreateInstance<EquipmentVisualProfile>();
            profile.name = "VisualProfile_Runtime_" + id;
            profile.hideFlags = HideFlags.HideAndDontSave;

            if (string.Equals(id, EquipmentIds.Flashlight, System.StringComparison.Ordinal))
            {
                // -X. The mesh runs along X, so the axis was never in doubt; the SIGN was,
                // and it was decided from a radius measurement rather than from looking at the
                // torch in a hand. That measurement said the +X end is the wider one and called
                // it the bell - but wider is not the same as the emitting end on this model,
                // and with the torch finally visible it points the wrong way round: the front
                // is at the back.
                //
                // Held in a real hand it is now the other way. ModelForwardAxis is turned onto
                // the carried root's +Y, and that is where HeldFlashlight hangs the lens and
                // the spot light, so this is also which end the beam leaves from.
                profile.ApplyModel("Props/CIYC_Flashlight", "Props/MAT_Flashlight",
                                   0.24f, new Vector3(-1f, 0f, 0f));
                return profile;
            }

            if (string.Equals(id, EquipmentIds.UvLight, System.StringComparison.Ordinal))
            {
                // The tactical torch runs along X like the other one but is built the other way
                // round: the stepped bezel is the -X end (max radius 0.244, and the brightest
                // part of the base map) and the switch tail is +X.
                profile.ApplyModel("Props/CIYC_UvLight", "Props/MAT_UvLight",
                                   0.18f, new Vector3(-1f, 0f, 0f));
                return profile;
            }

            if (string.Equals(id, EquipmentIds.SpectralGrid, System.StringComparison.Ordinal))
            {
                // Not a torch: a brick with the lens in the middle of one large face rather
                // than on an end. The emissive patch in the base map sits on the face that
                // imports as -Z, so THAT is the direction the grid has to leave the device -
                // SpectralGridProjection builds its cone along the head's +Y and the head
                // inherits the carried root. Length is therefore the device's DEPTH, not its
                // long side: 0.055 m deep puts it in the hand at 0.225 x 0.111 m, which is a
                // projector you could hold. Using the long side instead would have aimed the
                // lens sideways out of the player's palm.
                // Level 1 art. The mesh is the SAME mesh as the model this replaced -
                // vertices, normals, UVs and polygon indices all hash identical - so the two
                // numbers below were measured against this exact geometry and are carried
                // across rather than re-guessed. What changed is the texturing: a 4K base
                // colour, a real normal map, and metallic/roughness packed the way URP Lit
                // reads them (metallic in RGB, SMOOTHNESS - not roughness - in alpha).
                profile.ApplyModel("Props/CIYC_DOTSProjectorLevel1",
                                   "Props/MAT_DOTSProjectorLevel1",
                                   0.055f, new Vector3(0f, 0f, -1f));
                return profile;
            }

            profile.ApplyDevPlaceholder(new Vector3(0.06f, 0.18f, 0.06f),
                                        PlaceholderColorFor(id));
            return profile;
        }

        /// <summary>
        /// A distinct colour per item, so a floor of stand-ins is readable.
        ///
        /// <para>
        /// Every placeholder used to be the same purple. That is fine for ONE unfinished item
        /// and useless for eight: laid out together they are eight identical capsules, so the
        /// only way to find out which one is the thermometer is to pick each up and read the
        /// HUD. The colour is the label until the art arrives.
        /// </para>
        ///
        /// <para>
        /// Deliberately bright and deliberately not realistic - these still have to SAY they
        /// are placeholders, and the lobby is dim, so a muted palette would read as finished
        /// art in the dark. The hues are spread rather than picked one at a time, and no two
        /// of the eleven are adjacent.
        /// </para>
        ///
        /// <para>
        /// An id with no entry gets a colour derived from the id itself rather than a shared
        /// default: a twelfth item added later should look like a twelfth item, not like
        /// whichever one it was accidentally sharing with.
        /// </para>
        /// </summary>
        private static Color PlaceholderColorFor(string id)
        {
            if (string.Equals(id, EquipmentIds.EmfDetector, System.StringComparison.Ordinal))
                return new Color(1f, 0.82f, 0.10f);        // amber
            if (string.Equals(id, EquipmentIds.Thermometer, System.StringComparison.Ordinal))
                return new Color(0.25f, 0.85f, 1f);        // ice blue
            if (string.Equals(id, EquipmentIds.EvpRecorder, System.StringComparison.Ordinal))
                return new Color(1f, 0.45f, 0.10f);        // orange
            if (string.Equals(id, EquipmentIds.ParabolicMicrophone, System.StringComparison.Ordinal))
                return new Color(0.25f, 0.45f, 1f);        // deep blue
            if (string.Equals(id, EquipmentIds.PhotoCamera, System.StringComparison.Ordinal))
                return new Color(0.92f, 0.92f, 0.95f);     // near white
            if (string.Equals(id, EquipmentIds.VideoCamera, System.StringComparison.Ordinal))
                return new Color(0.95f, 0.15f, 0.20f);     // red
            if (string.Equals(id, EquipmentIds.WardingRelic, System.StringComparison.Ordinal))
                return new Color(0.70f, 0.30f, 1f);        // violet
            if (string.Equals(id, EquipmentIds.Salt, System.StringComparison.Ordinal))
                return new Color(0.55f, 1f, 0.55f);        // pale green

            // The three with finished art never reach here; they are listed so that a model
            // that fails to load still comes out as ITSELF rather than as the unknown colour.
            if (string.Equals(id, EquipmentIds.Flashlight, System.StringComparison.Ordinal))
                return new Color(1f, 0.95f, 0.70f);        // warm white
            if (string.Equals(id, EquipmentIds.UvLight, System.StringComparison.Ordinal))
                return new Color(0.45f, 0.20f, 0.95f);     // uv purple
            if (string.Equals(id, EquipmentIds.SpectralGrid, System.StringComparison.Ordinal))
                return new Color(0.20f, 1f, 0.35f);        // grid green

            // Unknown id: a stable hue from the id's own characters. Stable so the same item
            // is the same colour every run - a placeholder that changes colour between
            // sessions is one nobody can describe in a bug report.
            int h = 17;
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < id.Length; i++)
                    h = unchecked(h * 31 + id[i]);
            }

            float hue = Mathf.Repeat(Mathf.Abs(h) * 0.6180339887f, 1f);
            return Color.HSVToRGB(hue, 0.75f, 1f);
        }

        private static EquipmentDefinition Create(
            string id,
            string displayName,
            EquipmentCategory category,
            int price,
            int tier,
            float batteryUsage,
            float maxBattery,
            bool canPlace,
            string description,
            bool canDrop = true)
        {
            var def = ScriptableObject.CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.DisplayName = displayName;
            def.Category = category;
            def.Price = price;
            def.Tier = tier;
            def.BatteryUsagePerSecond = batteryUsage;
            def.MaxBattery = maxBattery;
            def.MaxDurability = 100f;
            def.CanPlace = canPlace;
            def.CanDrop = canDrop;
            def.CanUse = true;
            def.InteractionRange = 2.5f;
            def.HandLocalPosition = new Vector3(0.08f, -0.05f, 0.22f);
            def.HandLocalRotation = new Vector3(0f, -90f, 0f);
            def.Description = description;
            def.VisualProfile = BuildVisualProfile(id, displayName);
            return def;
        }
    }
}
