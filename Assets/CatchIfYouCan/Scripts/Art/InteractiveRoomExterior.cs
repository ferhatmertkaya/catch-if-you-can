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
    /// Variation is cosmetic and deliberately shallow: which of two panoramas, how it is rotated,
    /// how bright it is, and which silhouette groups are switched on. Nothing is generated, nothing
    /// moves after setup, and no scenery has a collider, a script or a shadow.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Interactive Room Exterior")]
    public sealed class InteractiveRoomExterior : MonoBehaviour
    {
        [Header("Sky")]
        [Tooltip("Skybox materials, loaded from Resources so the built-in panoramic shader is " +
                 "always included in a player build.")]
        [SerializeField] private string skyResourceFolder = "Sky";

        [Tooltip("Brightness range applied to the chosen sky. Kept low: this is a night the " +
                 "player looks out into, not a light source for the room.")]
        [SerializeField] private Vector2 exposureRange = new Vector2(0.45f, 0.7f);

        [Header("Scenery")]
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

            var skies = Resources.LoadAll<Material>(skyResourceFolder);
            if (skies != null && skies.Length > 0)
            {
                var source = skies[rng.Next(skies.Length)];
                _skyInstance = new Material(source);

                if (_skyInstance.HasProperty("_Rotation"))
                    _skyInstance.SetFloat("_Rotation", (float)rng.NextDouble() * 360f);

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
                    sceneryAlways[i].SetActive(true);

            if (sceneryVariants.Length > 0)
            {
                int keep = rng.Next(sceneryVariants.Length);
                for (int i = 0; i < sceneryVariants.Length; i++)
                    if (sceneryVariants[i] != null)
                        sceneryVariants[i].SetActive(i == keep);
            }
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
