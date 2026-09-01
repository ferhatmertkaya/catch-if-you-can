using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    /// <summary>
    /// Plays a footstep every time the player has actually covered a stride.
    ///
    /// <para>
    /// Cadence comes from distance travelled, not from a timer and not from input. That single
    /// choice settles most of the behaviour for free: walking is slower than running because a
    /// stride takes longer to cover, easing off the stick thins the steps out rather than
    /// switching them off, and pushing into a wall stops them dead — the controller reports the
    /// movement it achieved, which against a wall is nothing, so the stride never completes.
    /// </para>
    ///
    /// <para>
    /// The previous version read <see cref="PlayerController.CurrentSpeed"/>, which is
    /// <c>input * speed</c> and stays at full walking pace while you lean on a wall. That is why
    /// this reads <see cref="CharacterController.velocity"/> instead, with the vertical component
    /// removed so falling is not mistaken for walking.
    /// </para>
    ///
    /// <para>
    /// Sound goes out through one reused AudioSource. No source is created per step and no clip
    /// is loaded at play time. If the project's audio event database has entries for footsteps
    /// this defers to it; otherwise it plays from the clip list directly, which is what makes it
    /// audible today, before that database has been filled in.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class FootstepController : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField] private SurfaceAudioProfile profile;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float rayHeight = 0.35f;
        [SerializeField] private float sphereRadius = 0.15f;

        [Header("Stride length (metres of travel per step)")]
        [Tooltip("A walking human covers roughly this much ground between footfalls. At the " +
                 "player's 1.9 m/s that lands a step about every 0.43 s.")]
        [SerializeField, Min(0.1f)] private float walkStride = 0.82f;

        [Tooltip("Running strides are longer. At 3.8 m/s this gives a step about every 0.30 s.")]
        [SerializeField, Min(0.1f)] private float runStride = 1.15f;

        [SerializeField, Min(0.1f)] private float crouchStride = 0.62f;

        [Header("Gate")]
        [Tooltip("Planar speed below which the player counts as standing still. Large enough to " +
                 "ignore the millimetre of drift from settling onto the floor.")]
        [SerializeField, Min(0f)] private float moveThreshold = 0.12f;

        [Header("Test clips")]
        [Tooltip("Played directly when the audio event database has nothing for this surface. " +
                 "Drop your own recordings in here to replace the generated placeholders.")]
        [SerializeField] private AudioClip[] woodClips = new AudioClip[0];

        [SerializeField] private AudioSource stepSource;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.42f;
        [SerializeField, Range(0f, 1f)] private float runVolume = 0.58f;
        [SerializeField, Range(0f, 1f)] private float crouchVolume = 0.2f;
        [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.04f;
        [SerializeField, Range(0f, 0.3f)] private float volumeVariation = 0.08f;
        [SerializeField] private float indoorVolumeScale = 0.85f;
        [SerializeField] private float outdoorVolumeScale = 1f;

        private PlayerController _player;
        private CharacterController _controller;
        private float _strideDistance;
        private int _lastClipIndex = -1;

        // Repeat steps land on the same collider, so the surface lookup is worth remembering.
        private Collider _lastGroundCollider;
        private FootstepSurface _lastGroundSurface;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _controller = GetComponent<CharacterController>();

            if (profile == null)
                profile = ScriptableObject.CreateInstance<SurfaceAudioProfile>();

            if (stepSource == null)
                stepSource = GetComponent<AudioSource>();
        }

        /// <summary>Points this at the source the player factory built. One source, reused.</summary>
        public void BindSource(AudioSource source) => stepSource = source;

        /// <summary>Supplies the placeholder wood set without touching the serialized field.</summary>
        public void SetWoodClips(AudioClip[] clips) => woodClips = clips ?? new AudioClip[0];

        private void Update()
        {
            if (_player == null || _controller == null)
                return;

            // Airborne is not walking. Neither is sliding down a slope with no input.
            if (!_controller.isGrounded)
            {
                _strideDistance = 0f;
                return;
            }

            Vector3 v = _controller.velocity;
            v.y = 0f;
            float planar = v.magnitude;

            if (planar < moveThreshold)
            {
                // Hold the accumulated distance near a full stride rather than resetting, so
                // stopping and starting again does not always cost a step.
                _strideDistance = Mathf.Min(_strideDistance, GetStride() * 0.5f);
                return;
            }

            _strideDistance += planar * Time.deltaTime;
            if (_strideDistance < GetStride())
                return;

            _strideDistance = 0f;
            PlayStep();
        }

        private void PlayStep()
        {
            var gait = GetGait();
            var surface = SampleSurface(out bool indoor);
            float volume = GetGaitVolume(gait) * (indoor ? indoorVolumeScale : outdoorVolumeScale);
            volume *= 1f + Random.Range(-volumeVariation, volumeVariation);

            // The project's event database wins when it knows this surface; it carries mixing,
            // occlusion and distance behaviour this component has no business duplicating.
            string eventId = profile != null ? profile.GetEventId(surface, gait) : null;
            if (!string.IsNullOrEmpty(eventId) && AudioManager.Instance != null &&
                AudioManager.Instance.PlayEvent(eventId, transform.position, volume))
                return;

            PlayDirect(volume);
        }

        private void PlayDirect(float volume)
        {
            if (stepSource == null || woodClips == null || woodClips.Length == 0)
                return;

            AudioClip clip = PickClip();
            if (clip == null)
                return;

            stepSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            stepSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Picks a clip that is not the one just played, so steps do not stutter.</summary>
        private AudioClip PickClip()
        {
            if (woodClips.Length == 1)
                return woodClips[0];

            int index;
            int guard = 0;
            do
            {
                index = Random.Range(0, woodClips.Length);
            } while (index == _lastClipIndex && ++guard < 4);

            _lastClipIndex = index;
            return woodClips[index];
        }

        private FootstepGait GetGait()
        {
            if (_player.IsCrouching) return FootstepGait.Crouch;
            if (_player.IsSprinting) return FootstepGait.Run;
            return FootstepGait.Walk;
        }

        private float GetStride()
        {
            if (_player.IsCrouching) return crouchStride;
            if (_player.IsSprinting) return runStride;
            return walkStride;
        }

        private float GetGaitVolume(FootstepGait gait)
        {
            return gait switch
            {
                FootstepGait.Run => runVolume,
                FootstepGait.Crouch => crouchVolume,
                _ => walkVolume
            };
        }

        /// <summary>
        /// Finds what is underfoot. Reads a <see cref="FootstepSurface"/> component rather than
        /// the collider's name, and remembers the answer for as long as the player stays on the
        /// same collider.
        /// </summary>
        private SurfaceType SampleSurface(out bool indoor)
        {
            indoor = true;

            Vector3 origin = transform.position + Vector3.up * rayHeight;
            if (!Physics.SphereCast(origin, sphereRadius, Vector3.down, out RaycastHit hit,
                                    rayHeight + 0.6f, groundMask, QueryTriggerInteraction.Ignore))
                return SurfaceType.Wood;

            if (hit.collider != _lastGroundCollider)
            {
                _lastGroundCollider = hit.collider;
                _lastGroundSurface = hit.collider.GetComponentInParent<FootstepSurface>();
            }

            if (_lastGroundSurface == null)
                return SurfaceType.Wood;

            indoor = _lastGroundSurface.Indoor;
            return _lastGroundSurface.Surface;
        }
    }
}
