using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [RequireComponent(typeof(GhostController))]
    public class GhostVoiceController : MonoBehaviour
    {
        [SerializeField] private float whisperIntervalMin = 14f;
        [SerializeField] private float whisperIntervalMax = 40f;
        [SerializeField] private float evpResponseChance = 0.35f;
        [SerializeField] private float maxWhisperDistance = 18f;

        private GhostController _ghost;
        private GhostAudioIdentity _identity;
        private FearSystem _playerFear;
        private float _whisperTimer;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
        }

        private void Start()
        {
            _identity = GhostIdentityAudio.Resolve(_ghost?.Definition?.DisplayName);
            _whisperTimer = Random.Range(whisperIntervalMin, whisperIntervalMax);
            _playerFear = Core.LocalPlayerService.GetPlayerComponent<FearSystem>();
        }

        private void OnEnable()
        {
            GameEvents.OnNoiseGenerated += HandleNoise;
        }

        private void OnDisable()
        {
            GameEvents.OnNoiseGenerated -= HandleNoise;
        }

        private void Update()
        {
            if (_ghost == null) return;
            _whisperTimer -= Time.deltaTime;
            if (_whisperTimer > 0f) return;
            _whisperTimer = Random.Range(whisperIntervalMin, whisperIntervalMax);

            var player = Core.LocalPlayerService.RootTransform;
            if (player == null) return;

            // Resolved late as well: this controller lives on the ghost, which is spawned
            // before the player in the investigation bootstrap.
            if (_playerFear == null)
                _playerFear = Core.LocalPlayerService.GetPlayerComponent<FearSystem>();

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > maxWhisperDistance) return;

            float fearBoost = _playerFear != null ? _playerFear.NormalizedFear * 0.3f : 0f;
            AudioManager.Instance?.PlayEvent(_identity.WhisperEventId, transform.position, 0.22f + fearBoost);
            _playerFear?.SetWhisperActive(true);
            Invoke(nameof(ClearWhisper), 2.5f);
        }

        private void ClearWhisper()
        {
            _playerFear?.SetWhisperActive(false);
        }

        private void HandleNoise(float intensity, Vector3 pos)
        {
            if (intensity < 0.4f) return;
            if (Random.value > evpResponseChance * intensity) return;
            if (Vector3.Distance(transform.position, pos) > 12f) return;

            AudioManager.Instance?.PlayEvent("Ghost.EVP.Response", transform.position, 0.35f);
        }
    }
}
