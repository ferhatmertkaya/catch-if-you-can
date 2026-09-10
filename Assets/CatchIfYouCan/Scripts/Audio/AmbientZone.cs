using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public enum AmbientZoneMode
    {
        Trigger,
        Bounds
    }

    [RequireComponent(typeof(Collider))]
    public class AmbientZone : MonoBehaviour
    {
        [SerializeField] private AmbientZoneMode mode = AmbientZoneMode.Trigger;
        [SerializeField] private string ambientEventId = "Env.Ambient.Exterior";
        [SerializeField] private string randomEventId = "Env.Ambient.Random";
        [SerializeField] private float volume = 0.65f;
        [SerializeField] private float randomIntervalMin = 12f;
        [SerializeField] private float randomIntervalMax = 28f;
        [SerializeField] private int priority;

        private int _occupants;
        private float _randomTimer;
        private bool _active;

        public string AmbientEventId => ambientEventId;
        public int Priority => priority;
        public bool IsActive => _active;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (mode != AmbientZoneMode.Trigger || !other.CompareTag("Player")) return;
            _occupants++;
            if (_occupants == 1)
                Activate();
        }

        private void OnTriggerExit(Collider other)
        {
            if (mode != AmbientZoneMode.Trigger || !other.CompareTag("Player")) return;
            _occupants = Mathf.Max(0, _occupants - 1);
            if (_occupants == 0)
                Deactivate();
        }

        private void Update()
        {
            if (mode == AmbientZoneMode.Bounds)
                UpdateBoundsMode();
            if (!_active) return;
            TickRandomEvents();
        }

        private void UpdateBoundsMode()
        {
            // The local player, not an arbitrary tagged one: this zone decides what the
            // person at this machine hears.
            var player = Core.LocalPlayerService.RootTransform;
            if (player == null) return;
            var col = GetComponent<Collider>();
            bool inside = col != null && col.bounds.Contains(player.position);
            if (inside && !_active) Activate();
            else if (!inside && _active) Deactivate();
        }

        public void Activate()
        {
            _active = true;
            _randomTimer = Random.Range(randomIntervalMin, randomIntervalMax);
            AudioManager.Instance?.PlayEvent(ambientEventId, transform.position, volume);
        }

        public void Deactivate()
        {
            _active = false;
        }

        private void TickRandomEvents()
        {
            if (string.IsNullOrEmpty(randomEventId)) return;
            _randomTimer -= Time.deltaTime;
            if (_randomTimer > 0f) return;
            _randomTimer = Random.Range(randomIntervalMin, randomIntervalMax);
            AudioManager.Instance?.PlayEvent(randomEventId, transform.position, volume * 0.55f);
        }
    }
}
