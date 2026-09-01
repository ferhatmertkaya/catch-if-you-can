using CatchIfYouCan.Core;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class SanityAudioController : MonoBehaviour
    {
        [SerializeField] private FearSystem fearSystem;
        [SerializeField] private RoomToneController roomTone;
        [SerializeField] private float dropFearThreshold = 55f;
        [SerializeField] private float dropCooldown = 18f;

        private float _cooldownTimer;
        private bool _subscribed;

        private void Awake()
        {
            if (fearSystem == null)
                fearSystem = GetComponent<FearSystem>();
            if (roomTone == null)
                roomTone = FindAnyObjectByType<RoomToneController>();
        }

        private void OnEnable()
        {
            GameEvents.OnFearChanged += OnFearChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
                GameEvents.OnFearChanged -= OnFearChanged;
        }

        private void OnFearChanged(float fear)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f) return;
            if (fear < dropFearThreshold) return;
            if (Random.value > Mathf.InverseLerp(dropFearThreshold, 95f, fear) * 0.08f)
                return;

            _cooldownTimer = dropCooldown;
            roomTone?.DropRoomTone(0.35f);
            AudioManager.Instance?.PlayEvent("Player.Sanity.RoomToneDrop", null, 0.5f);
            AudioManager.Instance?.PlayEvent("Player.Breath.Catch", null, 0.3f);
        }

        public void TriggerRoomToneDrop(float scale = 0.4f)
        {
            roomTone?.DropRoomTone(scale);
            AudioManager.Instance?.PlayEvent("Player.Sanity.RoomToneDrop", null, scale);
        }
    }
}
