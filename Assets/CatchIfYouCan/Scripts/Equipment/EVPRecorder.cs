using System;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;

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

    /// <summary>
    /// The EVP recorder: ask a question, wait, and usually get nothing.
    ///
    /// <para>
    /// Getting nothing is the point, and it did not used to be possible. Every question
    /// produced a paranormal response and wrote a journal entry claiming one - with no ghost
    /// required, no range, and no check on whether the entity makes EVP at all. Since the
    /// journal registered its own evidence unchecked before AH, asking four questions into an
    /// empty room proved EVP Response.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/EVP Recorder")]
    public class EVPRecorder : HeldEquipmentBase
    {
        [Header("Questions")]
        [SerializeField] private string[] questions =
        {
            "Are you here?",
            "What do you want?",
            "How did you die?",
            "Can you speak to us?"
        };

        [Header("Timing")]
        [Tooltip("Seconds between asking and whatever comes back. The wait is the tension.")]
        [SerializeField, Min(0.1f)] private float responseDelay = 2.5f;

        [Tooltip("Seconds after a recording finishes before another can be started. Without " +
                 "it, holding the button is a way to reroll the dice as fast as the frame rate.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 3f;

        [Header("Eligibility")]
        [Tooltip("How close the ghost has to be to answer, in metres.")]
        [SerializeField, Min(1f)] private float ghostRange = 8f;

        [Tooltip("Chance of an answer when the ghost is right next to you, 0 to 1. It falls " +
                 "off to nothing at the range above.")]
        [SerializeField, Range(0f, 1f)] private float bestCaseChance = 0.55f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] whisperClips;
        [SerializeField] private AudioClip[] staticClips;
        [SerializeField] private AudioClip[] wordClips;
        [SerializeField] private AudioClip[] reverseClips;
        [SerializeField] private AudioClip[] knockClips;

        private int _questionIndex;
        private bool _awaitingResponse;
        private float _responseTimer;
        private float _cooldownTimer;
        private bool _warnedMissingClips;

        /// <summary>The question that would be asked next.</summary>
        public string CurrentQuestion =>
            questions != null && questions.Length > 0 ? questions[_questionIndex] : "Hello?";

        /// <summary>True between asking and the answer.</summary>
        public bool IsRecording => _awaitingResponse;

        /// <summary>Seconds until another question can be asked.</summary>
        public float Cooldown => Mathf.Max(0f, _cooldownTimer);

        /// <summary>What came back last, for a lab readout. Null when it was silence.</summary>
        public string LastResult { get; private set; } = "nothing yet";

        /// <summary>What the recorder is doing, and the question it would ask next.</summary>
        public override string HudReadout
        {
            get
            {
                if (_awaitingResponse)
                    return "RECORDING";
                if (_cooldownTimer > 0f)
                    return "WAIT " + _cooldownTimer.ToString("F1") + "s";
                return CurrentQuestion;
            }
        }

        /// <summary>
        /// Changing the question. It was bound to the Tab key, on a game built for phones.
        /// </summary>
        public override void CollectActions(System.Collections.Generic.List<EquipmentAction> into)
        {
            into.Add(new EquipmentAction("NEXT Q", NextQuestion,
                                         !_awaitingResponse && questions != null && questions.Length > 1));
        }

        protected override float GetInterferenceMultiplier() => 0.4f;

        /// <summary>Asking a question does not wear the recorder out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        /// <summary>Steps to the next question. Called by the HUD or the lab, not by a key.</summary>
        public void NextQuestion()
        {
            if (questions == null || questions.Length == 0)
                return;

            _questionIndex = (_questionIndex + 1) % questions.Length;
            CIYCLog.Info("EVP question selected: " + questions[_questionIndex]);
        }

        protected override void OnUse()
        {
            // One recording at a time, and not back to back. Without the cooldown, holding the
            // button rerolls the dice as fast as the frame rate.
            if (_awaitingResponse || _cooldownTimer > 0f)
                return;

            SetDeviceActive(true);
            _awaitingResponse = true;
            _responseTimer = responseDelay;
            LastResult = "recording";
            CIYCLog.Info("EVP asked: " + CurrentQuestion);
        }

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            // Stowing it abandons the recording rather than leaving one running in a bag.
            if (to == EquipmentLifecycleState.Equipped)
                return;

            _awaitingResponse = false;
            SetDeviceActive(false);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;

            if (!_awaitingResponse)
            {
                if (_cooldownTimer <= 0f)
                    SetDeviceActive(false);
                return;
            }

            _responseTimer -= deltaTime;
            if (_responseTimer > 0f)
                return;

            _awaitingResponse = false;
            _cooldownTimer = cooldownSeconds;
            Resolve();
        }

        /// <summary>
        /// Decides whether anything answered, and only then reports it.
        ///
        /// <para>
        /// Three things have to be true: there is a ghost, it is close enough to hear, and it
        /// is the kind of entity that answers. A ghost with no EVP in its profile stays silent
        /// however many times it is asked, which is what makes silence informative.
        /// </para>
        /// </summary>
        private void Resolve()
        {
            var ghost = GhostController.Active;
            if (ghost == null)
            {
                Silence("no entity present");
                return;
            }

            Vector3 probe = CarriedRoot != null ? CarriedRoot.position : transform.position;
            float distance = Vector3.Distance(probe, ghost.transform.position);
            if (distance > ghostRange)
            {
                Silence("nothing within range");
                return;
            }

            var definition = ghost.Definition;
            if (definition == null || !definition.HasEvidence(EvidenceType.EVPResponse))
            {
                Silence("no response");
                return;
            }

            float chance = bestCaseChance * (1f - distance / ghostRange);
            if (UnityEngine.Random.value > chance)
            {
                Silence("no response");
                return;
            }

            Respond(RollResponseType(), 1f - distance / ghostRange);
        }

        private void Silence(string why)
        {
            LastResult = why;
            CIYCLog.Info("EVP playback: " + why + ".");
        }

        private void Respond(EVPResponseType responseType, float strength)
        {
            AudioClip clip = PickClip(responseType);
            if (clip != null)
            {
                PlayClip(clip);
            }
            else if (!_warnedMissingClips)
            {
                // Said once, and said plainly. A recorder with no audio still reports the
                // response it detected - the finding is real - but nobody should be left
                // thinking they heard something the project does not contain.
                _warnedMissingClips = true;
                CIYCLog.Warn("EVP recorder has no clips for " + responseType +
                             ". The response is reported but there is nothing to play.");
            }

            string body = responseType switch
            {
                EVPResponseType.Whisper => "Faint whisper detected on playback.",
                EVPResponseType.RadioStatic => "Radio static burst recorded.",
                EVPResponseType.SingleWords => "Isolated words captured.",
                EVPResponseType.ReverseVoice => "Reverse voice pattern detected.",
                EVPResponseType.Knocking => "Knocking response recorded.",
                _ => "Unknown EVP response."
            };

            LastResult = body;

            // The observation is what proves it; the journal entry is the record of it. Both
            // go through the validator now, which is what closed the door this item used to
            // walk through.
            Observe(EvidenceType.EVPResponse, Mathf.Clamp01(strength));

            if (ServiceLocator.TryGet<EvidenceManager>(out var manager))
                manager.AddJournalEntry("EVP Response", body, EvidenceType.EVPResponse);
        }

        private static EVPResponseType RollResponseType()
        {
            Array values = Enum.GetValues(typeof(EVPResponseType));
            return (EVPResponseType)values.GetValue(UnityEngine.Random.Range(0, values.Length));
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
