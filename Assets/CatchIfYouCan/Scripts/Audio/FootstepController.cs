using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [RequireComponent(typeof(PlayerController))]
    public class FootstepController : MonoBehaviour
    {
        [SerializeField] private SurfaceAudioProfile profile;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float rayHeight = 0.2f;
        [SerializeField] private float sphereRadius = 0.15f;
        [SerializeField] private float walkStride = 0.45f;
        [SerializeField] private float runStride = 0.32f;
        [SerializeField] private float crouchStride = 0.55f;
        [SerializeField] private float indoorVolumeScale = 0.85f;
        [SerializeField] private float outdoorVolumeScale = 1f;
        [SerializeField] private float shuffleBagChance = 0.12f;

        private PlayerController _player;
        private float _strideDistance;
        private bool _indoor = true;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            if (profile == null)
                profile = ScriptableObject.CreateInstance<SurfaceAudioProfile>();
        }

        private void Update()
        {
            if (_player == null || !_player.IsGrounded || _player.CurrentSpeed < 0.05f)
                return;

            _strideDistance += _player.CurrentSpeed * Time.deltaTime;
            float threshold = GetStrideThreshold();
            if (_strideDistance < threshold) return;
            _strideDistance = 0f;

            var surface = SampleSurface(out _indoor);
            var gait = GetGait();
            string eventId = profile.GetEventId(surface, gait);
            float scale = (_indoor ? indoorVolumeScale : outdoorVolumeScale) * GetGaitVolume(gait);

            if (Random.value < shuffleBagChance)
                AudioManager.Instance?.PlayEvent("Player.Gear.Shuffle", transform.position, scale * 0.35f);

            AudioManager.Instance?.PlayEvent(eventId, transform.position, scale);
        }

        private FootstepGait GetGait()
        {
            if (_player.IsCrouching) return FootstepGait.Crouch;
            if (_player.IsSprinting) return FootstepGait.Run;
            return FootstepGait.Walk;
        }

        private float GetStrideThreshold()
        {
            if (_player.IsCrouching) return crouchStride;
            if (_player.IsSprinting) return runStride;
            return walkStride;
        }

        private static float GetGaitVolume(FootstepGait gait)
        {
            return gait switch
            {
                FootstepGait.Run => 1.05f,
                FootstepGait.Crouch => 0.55f,
                _ => 0.85f
            };
        }

        private SurfaceType SampleSurface(out bool indoor)
        {
            indoor = true;
            Vector3 origin = transform.position + Vector3.up * rayHeight;
            if (!Physics.SphereCast(origin, sphereRadius, Vector3.down, out RaycastHit hit, 1.5f, groundMask, QueryTriggerInteraction.Ignore))
                return SurfaceType.Concrete;

            string tag = hit.collider.tag;
            string name = hit.collider.name.ToLowerInvariant();
            if (tag == "Outdoor" || name.Contains("grass") || name.Contains("yard"))
                indoor = false;

            if (name.Contains("carpet") || name.Contains("rug")) return SurfaceType.Carpet;
            if (name.Contains("tile")) return SurfaceType.Tile;
            if (name.Contains("metal") || name.Contains("garage")) return SurfaceType.Metal;
            if (name.Contains("gravel")) return SurfaceType.Gravel;
            if (name.Contains("mud")) return SurfaceType.Mud;
            if (name.Contains("grass")) return SurfaceType.Grass;
            if (name.Contains("concrete") || name.Contains("basement")) return SurfaceType.Concrete;
            if (name.Contains("old") || name.Contains("attic")) return SurfaceType.OldWood;
            return SurfaceType.Wood;
        }
    }
}
