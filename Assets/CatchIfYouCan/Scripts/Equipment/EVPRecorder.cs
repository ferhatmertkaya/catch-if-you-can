using System;
using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public enum EVPResponseType
    {
        Whisper,
        RadioStatic,
        SingleWords,
        ReverseVoice,
        Knocking
    }

    public class EVPRecorder : EquipmentBase
    {
        [SerializeField] private string[] questions =
        {
            "Are you here?",
            "What do you want?",
            "How did you die?",
            "Can you speak to us?"
        };
        [SerializeField] private AudioClip[] whisperClips;
        [SerializeField] private AudioClip[] staticClips;
        [SerializeField] private AudioClip[] wordClips;
        [SerializeField] private AudioClip[] reverseClips;
        [SerializeField] private AudioClip[] knockClips;
        [SerializeField] private float responseDelay = 2.5f;

        private int _questionIndex;
        private bool _awaitingResponse;
        private float _responseTimer;

        protected override float GetInterferenceMultiplier() => 0.4f;

        protected override void OnEquipped()
        {
            SetDeviceActive(true);
        }

        protected override void OnUse()
        {
            if (_awaitingResponse)
                return;

            AskCurrentQuestion();
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!_awaitingResponse)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                    CycleQuestion();
                return;
            }

            _responseTimer -= deltaTime;
            if (_responseTimer > 0f)
                return;

            _awaitingResponse = false;
            PlayGhostResponse(RollResponseType());
        }

        private void CycleQuestion()
        {
            if (questions == null || questions.Length == 0)
                return;

            _questionIndex = (_questionIndex + 1) % questions.Length;
            CIYCLog.Info($"EVP question selected: {questions[_questionIndex]}");
        }

        private void AskCurrentQuestion()
        {
            string question = questions != null && questions.Length > 0
                ? questions[_questionIndex]
                : "Hello?";

            _awaitingResponse = true;
            _responseTimer = responseDelay;
            CIYCLog.Info($"EVP asked: {question}");
        }

        private EVPResponseType RollResponseType()
        {
            Array values = Enum.GetValues(typeof(EVPResponseType));
            return (EVPResponseType)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        }

        private void PlayGhostResponse(EVPResponseType responseType)
        {
            AudioClip clip = PickClip(responseType);
            if (clip != null)
                PlayClip(clip);

            string body = responseType switch
            {
                EVPResponseType.Whisper => "Faint whisper detected on playback.",
                EVPResponseType.RadioStatic => "Radio static burst recorded.",
                EVPResponseType.SingleWords => "Isolated words captured.",
                EVPResponseType.ReverseVoice => "Reverse voice pattern detected.",
                EVPResponseType.Knocking => "Knocking response recorded.",
                _ => "Unknown EVP response."
            };

            if (Core.ServiceLocator.TryGet<EvidenceManager>(out var manager))
            {
                manager.AddJournalEntry("EVP Response", body, EvidenceType.EVPResponse);
            }
        }

        private AudioClip PickClip(EVPResponseType type)
        {
            AudioClip[] pool = type switch
            {
                EVPResponseType.Whisper => whisperClips,
                EVPResponseType.RadioStatic => staticClips,
                EVPResponseType.SingleWords => wordClips,
                EVPResponseType.ReverseVoice => reverseClips,
                EVPResponseType.Knocking => knockClips,
                _ => null
            };

            if (pool == null || pool.Length == 0)
                return null;

            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
    }
}
