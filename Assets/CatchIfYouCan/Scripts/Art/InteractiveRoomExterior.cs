using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Picks what the night outside the window looks like, once, when the room comes up.
    ///
    /// <para>
    /// The sky is applied to the player's camera rather than to <see cref="RenderSettings.skybox"/>.
    /// That matters twice over: the scene's ambient mode is Skybox with no skybox assigned, so a
    /// global one would start feeding ambient light into a room whose lighting was tuned without
    /// it, and the cinematic menu would inherit a sky it was never composed against. A
    /// <see cref="Skybox"/> component overrides the sky for one camera and nothing else, and the
    /// player camera only exists after the handover.
    /// </para>
    ///
    /// <para>
    /// A skybox is also the only way to get a sky that is not eaten by fog. The scene's fog is
    /// exponential-squared at 0.025, which removes almost everything past 40 m; geometry that far
    /// out would arrive as a flat dark green wash. Skyboxes are not fogged, so the sky stays clean
    /// while the silhouettes closer in still pick up haze — which is the right way round.
    /// </para>
    ///
    /// <para>
    /// Variation is cosmetic and deliberately shallow: a few degrees of sky rotation, a little
    /// exposure, and the moonlight that follows it. Nothing is generated, nothing moves after
    /// setup, and no scenery has a collider, a script or a shadow. The rotation jitter is
    /// deliberately narrow rather than a full circle, because the sky is a painting with a moon
    /// in it and spinning it freely would as often as not point the window at empty horizon.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Interactive Room Exterior")]
    public sealed class InteractiveRoomExterior : MonoBehaviour
    {
        [Header("Sky")]
        [Tooltip("Skybox material, loaded from Resources so the built-in panoramic shader is " +
                 "always included in a player build.")]
        [SerializeField] private string skyResourcePath = "Sky/MAT_Skybox_HauntedNight";

        [Tooltip("Searched if the named material is missing, so a renamed sky still shows up " +
                 "rather than leaving the window black.")]
        [SerializeField] private string skyResourceFolder = "Sky";

        [Tooltip("Brightness range applied to the sky. The panorama is already authored for " +
                 "night at a mean luminance of 16/255, so this sits around Unity's neutral 1 " +
                 "rather than pulling it down further.")]
        [SerializeField] private Vector2 exposureRange = new Vector2(0.92f, 1.06f);

        [Tooltip("Degrees of sky rotation that put the moon in the window. The room's window " +
                 "faces +X; the moon sits at u=0.311 in the panorama, so this brings it round " +
                 "to roughly 15 degrees left of straight out, which frames it against the " +
                 "ridge rather than dead centre.")]
        [SerializeField, Range(0f, 360f)] private float skyRotation = 143f;

        [Tooltip("Cosmetic wobble either side of that rotation. Small on purpose: enough that " +
                 "two sessions are not pixel-identical, far too small to lose the moon.")]
        [SerializeField, Range(0f, 30f)] private float skyRotationJitter = 6f;

        [Header("Scenery")]
        [Tooltip("Foreground silhouettes outside the window. Off by default: the Haunted Night " +
                 "panorama already contains its own forest, ridge line, valley and distant " +
                 "house, and these boxes were massed against the flatter placeholder sky they " +
                 "were built for. Switch this on to get near-field parallax back, and expect to " +
                 "re-tune their heights against the new horizon, which sits 18 degrees below eye " +
                 "level rather than on it.")]
        [SerializeField] private bool useForegroundSilhouettes;

        [Tooltip("Silhouette groups outside the window. One is chosen per session and the rest " +
                 "are switched off; they are only geometry, so switching them off costs nothing.")]
        [SerializeField] private GameObject[] sceneryVariants = new GameObject[0];

        [Tooltip("Always-on scenery, such as the far ridge line.")]
        [SerializeField] private GameObject[] sceneryAlways = new GameObject[0];

        [Header("Moonlight")]
        [Tooltip("Optional cold light near the window. Its intensity is nudged with the variant " +
                 "so a cloudy night is dimmer than a clear one.")]
        [SerializeField] private Light windowMoonlight;

        [SerializeField] private Vector2 moonlightRange = new Vector2(0.25f, 0.75f);

        private Material _skyInstance;
        private bool _chosen;

        /// <summary>The sky picked for this session, or null if none could be loaded.</summary>
        public Material SkyMaterial => _skyInstance;

        private void OnEnable()
        {
            Choose();
        }

        private void OnDestroy()
        {
            // The instance is ours; the asset in Resources must not be left modified.
            if (_skyInstance != null)
                Destroy(_skyInstance);
        }

        /// <summary>
        /// Chooses the night once. Repeated calls do nothing, so the view cannot change while
        /// the player is looking at it.
        /// </summary>
        public void Choose()
        {
            if (_chosen)
                return;

            _chosen = true;

            // Its own stream. Nothing here may reach the mission seed, the layout generator or
            // anything the network agrees on — this decides which clouds are in the sky.
            var rng = new System.Random(
                unchecked((int)System.DateTime.UtcNow.Ticks) ^ (GetHashCode() * 397));

            var source = LoadSky();
            if (source != null)
            {
                // Instanced before anything is written to it: _Rotation and _Exposure are
                // material state, and writing them on the Resources asset would persist into the
                // next session and dirty the file in the editor.
                _skyInstance = new Material(source);

                if (_skyInstance.HasProperty("_Rotation"))
                {
                    float jitter = ((float)rng.NextDouble() * 2f - 1f) * skyRotationJitter;
                    _skyInstance.SetFloat("_Rotation", Mathf.Repeat(skyRotation + jitter, 360f));
                }

                float exposure = Mathf.Lerp(exposureRange.x, exposureRange.y, (float)rng.NextDouble());
                if (_skyInstance.HasProperty("_Exposure"))
                    _skyInstance.SetFloat("_Exposure", exposure);

                if (windowMoonlight != null)
                {
                    // Brighter sky, brighter shaft. One light, no shadows, no volumetrics.
                    float t = Mathf.InverseLerp(exposureRange.x, exposureRange.y, exposure);
                    windowMoonlight.intensity = Mathf.Lerp(moonlightRange.x, moonlightRange.y, t);
                }
            }

            for (int i = 0; i < sceneryAlways.Length; i++)
                if (sceneryAlways[i] != null)
                    sceneryAlways[i].SetActive(useForegroundSilhouettes);

            if (sceneryVariants.Length > 0)
            {
                int keep = useForegroundSilhouettes ? rng.Next(sceneryVariants.Length) : -1;
                for (int i = 0; i < sceneryVariants.Length; i++)
                    if (sceneryVariants[i] != null)
                        sceneryVariants[i].SetActive(i == keep);
            }

            // Says on the device which exterior actually came up. The silhouette boxes sit 6-14 m
            // beyond the wall and stand 9-15 m tall, so with them on they fill the window and the
            // view reads as a hillside immediately outside; this line is how you tell that state
            // apart from the sky itself at a glance, without attaching a profiler.
            Debug.Log("[CIYC] Exterior: sky=" +
                      (_skyInstance != null ? _skyInstance.name : "NONE") +
                      " rotation=" + (_skyInstance != null && _skyInstance.HasProperty("_Rotation")
                          ? _skyInstance.GetFloat("_Rotation").ToString("0.0")
                          : "n/a") +
                      " foregroundSilhouettes=" + (useForegroundSilhouettes ? "ON" : "off"));
        }

        /// <summary>
        /// The named sky, or any sky in the folder if the name has moved. Returning null here is
        /// what leaves the window showing the camera's clear colour, so it is worth saying so.
        /// </summary>
        private Material LoadSky()
        {
            if (!string.IsNullOrEmpty(skyResourcePath))
            {
                var named = Resources.Load<Material>(skyResourcePath);
                if (named != null)
                    return named;
            }

            var found = Resources.LoadAll<Material>(skyResourceFolder);
            if (found != null && found.Length > 0)
                return found[0];

            Debug.LogError("[CIYC] No skybox material at Resources/" + skyResourcePath +
                           " and none in Resources/" + skyResourceFolder + ", so the window " +
                           "has nothing behind it. Run Catch If You Can > Environment > " +
                           "Build Interactive Room Sky.");
            return null;
        }

        /// <summary>
        /// Points a camera at this session's sky. Called once, with the player camera, after the
        /// handover builds it.
        /// </summary>
        public void ApplyTo(Camera camera)
        {
            if (camera == null)
                return;

            Choose();

            if (_skyInstance == null)
                return;

            camera.clearFlags = CameraClearFlags.Skybox;

            var skybox = camera.GetComponent<Skybox>();
            if (skybox == null)
                skybox = camera.gameObject.AddComponent<Skybox>();

            skybox.material = _skyInstance;
        }
    }
}
