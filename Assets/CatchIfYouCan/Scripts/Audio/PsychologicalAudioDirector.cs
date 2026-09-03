using CatchIfYouCan.Core;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class PsychologicalAudioDirector : MonoBehaviour
    {
        [SerializeField] private float minInterval = 35f;
        [SerializeField] private float maxInterval = 120f;
        [SerializeField] private HorrorSilenceSystem silenceSystem;

        private Transform _player;
        private FearSystem _fear;
        private float _timer;

        private void Start()
        {
            _player = Core.LocalPlayerService.RootTransform;
            _fear = Core.LocalPlayerService.GetPlayerComponent<FearSystem>();
            _timer = Random.Range(minInterval, maxInterval);
            if (silenceSystem == null)
                silenceSystem = FindAnyObjectByType<HorrorSilenceSystem>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Random.Range(minInterval, maxInterval);
            if (_fear != null && _fear.Fear < 25f && Random.value > 0.35f) return;
            TriggerRandomEvent();
        }

        private void TriggerRandomEvent()
        {
            if (silenceSystem != null && Random.value < 0.35f)
                silenceSystem.TrySilenceBeforeMajorEvent();

            AudioManager.Instance?.TransitionSnapshot(AudioSnapshotId.PsychoEvent, 0.6f);

            int roll = Random.Range(0, 6);
            switch (roll)
            {
                case 0: PlayFalseFootstep(); break;
                case 1: PlayBehindYouBreath(); break;
                case 2: PlayMovingKnock(); break;
                case 3: PlayFalseDoor(); break;
                case 4: PlayFootstepEcho(); break;
                case 5: PlayRoomToneDrop(); break;
            }

            Invoke(nameof(ResetPsychoSnapshot), 3f);
        }

        private void ResetPsychoSnapshot()
        {
            AudioManager.Instance?.TransitionSnapshot(AudioSnapshotId.Normal, 1.5f);
        }

        private void PlayFalseFootstep()
        {
            Vector3 pos = OffsetBehind(3f, 6f);
            AudioManager.Instance?.PlayEvent("Psycho.FalseFootstep", pos, 0.45f);
        }

        private void PlayBehindYouBreath()
        {
            Vector3 pos = OffsetBehind(1f, 2.5f);
            AudioManager.Instance?.PlayEvent("Psycho.BehindYouBreath", pos, 0.35f);
        }

        private void PlayMovingKnock()
        {
            Vector3 pos = RandomNearby(4f, 10f);
            AudioManager.Instance?.PlayEvent("Psycho.MovingKnock", pos, 0.5f);
        }

        private void PlayFalseDoor()
        {
            var doors = FindObjectsByType<DoorAudioController>();
            if (doors.Length == 0)
            {
                AudioManager.Instance?.PlayEvent("Env.Door.Creak", RandomNearby(2f, 8f), 0.4f);
                return;
            }
            var door = doors[Random.Range(0, doors.Length)];
            AudioManager.Instance?.PlayEvent("Psycho.FalseDoor", door.transform.position, 0.42f);
        }

        private void PlayFootstepEcho()
        {
            if (_player == null) return;
            AudioManager.Instance?.PlayEvent("Psycho.PlayerFootstepEcho", _player.position, 0.38f);
        }

        private void PlayRoomToneDrop()
        {
            var sanity = _player != null ? _player.GetComponent<SanityAudioController>() : null;
            if (sanity != null)
                sanity.TriggerRoomToneDrop(0.3f);
            else
                AudioManager.Instance?.PlayEvent("Player.Sanity.RoomToneDrop", null, 0.4f);
        }

        private Vector3 OffsetBehind(float min, float max)
        {
            if (_player == null) return RandomNearby(min, max);
            Vector3 back = -_player.forward;
            return _player.position + back * Random.Range(min, max);
        }

        private Vector3 RandomNearby(float min, float max)
        {
            if (_player == null) return Vector3.zero;
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(min, max);
            return _player.position + new Vector3(c.x, 0f, c.y);
        }
    }
}
