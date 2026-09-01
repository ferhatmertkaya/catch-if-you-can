using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class TensionAudioDirector : MonoBehaviour
    {
        [SerializeField] private AudioSnapshotController snapshots;
        [SerializeField] private string bedLowId = "Tension.Bed.Low";
        [SerializeField] private string bedMidId = "Tension.Bed.Mid";
        [SerializeField] private string bedHighId = "Tension.Bed.High";
        [SerializeField] private string bedExtremeId = "Tension.Bed.Extreme";

        private float _tension;
        private float _lastEventTime;
        private FearSystem _fear;
        private Transform _player;
        private GhostController _ghost;
        private AudioSource _bed;
        private TensionBand _band = TensionBand.Low;

        private enum TensionBand { Low, Mid, High, Extreme }

        private void Awake()
        {
            _bed = gameObject.AddComponent<AudioSource>();
            _bed.loop = true;
            _bed.spatialBlend = 0f;
            _bed.volume = 0f;
            if (snapshots == null)
                snapshots = AudioManager.Instance?.SnapshotController;
        }

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                _fear = playerGo.GetComponent<FearSystem>();
            _player = playerGo != null ? playerGo.transform : null;
            _ghost = FindAnyObjectByType<GhostController>();
            _lastEventTime = Time.time;
        }

        private void OnEnable()
        {
            GameEvents.OnGhostActivityChanged += OnActivity;
            GameEvents.OnFearChanged += OnFear;
            GameEvents.OnHuntStarted += OnHunt;
            GameEvents.OnHuntEnded += OnHuntEnd;
            GameEvents.OnEvidenceDetected += OnEvidence;
        }

        private void OnDisable()
        {
            GameEvents.OnGhostActivityChanged -= OnActivity;
            GameEvents.OnFearChanged -= OnFear;
            GameEvents.OnHuntStarted -= OnHunt;
            GameEvents.OnHuntEnded -= OnHuntEnd;
            GameEvents.OnEvidenceDetected -= OnEvidence;
        }

        private void Update()
        {
            RecomputeTension();
            ApplyBand();
        }

        private void RecomputeTension()
        {
            float activity = GhostActivitySystem.Instance != null ? GhostActivitySystem.Instance.Normalized * 25f : 0f;
            float fear = _fear != null ? _fear.Fear * 0.35f : 0f;
            float isolation = ComputeIsolation() * 20f;
            float sinceEvent = Mathf.Clamp((Time.time - _lastEventTime) / 60f, 0f, 1f) * 15f;
            float proximity = ComputeProximity() * 25f;
            float darkness = RenderSettings.ambientIntensity < 0.3f ? 10f : 0f;
            float evidence = Evidence.EvidenceManager.Instance != null && Evidence.EvidenceManager.Instance.FoundEvidence.Count > 0 ? 5f : 0f;

            _tension = Mathf.Clamp(activity + fear + isolation + sinceEvent + proximity + darkness + evidence, 0f, 100f);
        }

        private float ComputeIsolation()
        {
            if (_player == null || _ghost == null) return 0.5f;
            float dist = Vector3.Distance(_player.position, _ghost.transform.position);
            return Mathf.Clamp01(dist / 30f);
        }

        private float ComputeProximity()
        {
            if (_player == null || _ghost == null) return 0f;
            float dist = Vector3.Distance(_player.position, _ghost.transform.position);
            return 1f - Mathf.Clamp01(dist / 15f);
        }

        private void ApplyBand()
        {
            TensionBand next = _tension switch
            {
                < 25f => TensionBand.Low,
                < 50f => TensionBand.Mid,
                < 75f => TensionBand.High,
                _ => TensionBand.Extreme
            };
            if (next != _band)
            {
                _band = next;
                SwitchBed(next);
                snapshots?.TransitionTo(BandToSnapshot(next));
            }

            _bed.volume = Mathf.Lerp(_bed.volume, BandVolume(next), Time.deltaTime * 1.2f);
        }

        private void SwitchBed(TensionBand band)
        {
            string id = band switch
            {
                TensionBand.Mid => bedMidId,
                TensionBand.High => bedHighId,
                TensionBand.Extreme => bedExtremeId,
                _ => bedLowId
            };
            var clip = AudioEventResolve.ResolveClip(id);
            if (clip == null) return;
            _bed.clip = clip;
            _bed.Play();
        }

        private static float BandVolume(TensionBand band)
        {
            return band switch
            {
                TensionBand.Low => 0.06f,
                TensionBand.Mid => 0.12f,
                TensionBand.High => 0.18f,
                _ => 0.24f
            };
        }

        private static AudioSnapshotId BandToSnapshot(TensionBand band)
        {
            return band switch
            {
                TensionBand.Mid => AudioSnapshotId.HouseInterior,
                TensionBand.High => AudioSnapshotId.HighTension,
                TensionBand.Extreme => AudioSnapshotId.GhostEvent,
                _ => AudioSnapshotId.Normal
            };
        }

        private void OnActivity(float v) => _lastEventTime = Time.time;
        private void OnFear(float v) => _lastEventTime = Time.time;
        private void OnHunt() { _tension = 85f; snapshots?.TransitionTo(AudioSnapshotId.Hunt); }
        private void OnHuntEnd() => snapshots?.TransitionTo(AudioSnapshotId.Normal);
        private void OnEvidence(Evidence.EvidenceType t) => _lastEventTime = Time.time;

        public float Tension => _tension;
    }
}
