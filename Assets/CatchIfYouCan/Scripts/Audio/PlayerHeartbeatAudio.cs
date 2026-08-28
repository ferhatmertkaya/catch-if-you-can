using CatchIfYouCan.Core;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class PlayerHeartbeatAudio : MonoBehaviour
    {
        [SerializeField] private FearSystem fearSystem;
        [SerializeField] private string lightBreathId = "Player.Breath.Light";
        [SerializeField] private string heavyBreathId = "Player.Breath.Heavy";
        [SerializeField] private string subtleHeartbeatId = "Player.Heartbeat.Subtle";
        [SerializeField] private string strongHeartbeatId = "Player.Heartbeat.Strong";
        [SerializeField] private string tinnitusId = "Player.Tinnitus.Subtle";

        private float _breathTimer;
        private float _heartbeatTimer;
        private FearBand _band = FearBand.Calm;

        private enum FearBand { Calm, Light, Elevated, Extreme }

        private void Awake()
        {
            if (fearSystem == null)
                fearSystem = GetComponent<FearSystem>();
        }

        private void OnEnable()
        {
            GameEvents.OnFearChanged += HandleFear;
            GameEvents.OnHuntEnded += HandleHuntEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnFearChanged -= HandleFear;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
            StopAllLayers();
        }

        private void Update()
        {
            float fear = fearSystem != null ? fearSystem.Fear : 0f;
            UpdateBand(fear);
            TickLayers(fear);
        }

        private void HandleFear(float fear) => UpdateBand(fear);

        private void HandleHuntEnded()
        {
            StopAllLayers();
            _band = FearBand.Calm;
        }

        private void UpdateBand(float fear)
        {
            FearBand next = fear switch
            {
                < 30f => FearBand.Calm,
                < 60f => FearBand.Light,
                < 80f => FearBand.Elevated,
                _ => FearBand.Extreme
            };
            if (next == _band) return;
            _band = next;
            if (_band == FearBand.Calm)
                StopAllLayers();
        }

        private void TickLayers(float fear)
        {
            switch (_band)
            {
                case FearBand.Calm:
                    return;
                case FearBand.Light:
                    TickBreath(lightBreathId, ref _breathTimer, 2.8f, 0.35f);
                    break;
                case FearBand.Elevated:
                    TickBreath(heavyBreathId, ref _breathTimer, 2.2f, 0.45f);
                    TickHeartbeat(subtleHeartbeatId, ref _heartbeatTimer, fear, 0.4f);
                    break;
                case FearBand.Extreme:
                    TickBreath(heavyBreathId, ref _breathTimer, 1.6f, 0.55f);
                    TickHeartbeat(strongHeartbeatId, ref _heartbeatTimer, fear, 0.65f);
                    if (Random.value < 0.015f)
                        AudioManager.Instance?.PlayEvent(tinnitusId, null, 0.12f);
                    break;
            }
        }

        private void TickBreath(string id, ref float timer, float interval, float scale)
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval + Random.Range(-0.3f, 0.3f);
            AudioManager.Instance?.PlayEvent(id, null, scale);
        }

        private void TickHeartbeat(string id, ref float timer, float fear, float scale)
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            float bpm = Mathf.Lerp(72f, 118f, fear / 100f);
            timer = 60f / bpm;
            AudioManager.Instance?.PlayEvent(id, null, scale);
        }

        private void StopAllLayers()
        {
            AudioManager.Instance?.StopLoopingEvent(AudioChannel.Player);
        }
    }
}
