using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class UiAudioService : MonoBehaviour
    {
        public static UiAudioService Instance { get; private set; }

        [SerializeField] private string buttonId = "UI.Button.Press";
        [SerializeField] private string tabId = "UI.Tab.Switch";
        [SerializeField] private string evidenceId = "UI.Evidence.Found";
        [SerializeField] private string objectiveId = "UI.Objective.Complete";
        [SerializeField] private string entityId = "UI.Entity.Discovered";
        [SerializeField] private string successId = "UI.Mission.Success";
        [SerializeField] private string failId = "UI.Mission.Fail";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnEvidenceDetected += OnEvidence;
            GameEvents.OnObjectiveCompleted += OnObjective;
            GameEvents.OnEntityDiscovered += OnEntity;
            GameEvents.OnMissionComplete += OnSuccess;
            GameEvents.OnMissionFailed += OnFail;
        }

        private void OnDisable()
        {
            GameEvents.OnEvidenceDetected -= OnEvidence;
            GameEvents.OnObjectiveCompleted -= OnObjective;
            GameEvents.OnEntityDiscovered -= OnEntity;
            GameEvents.OnMissionComplete -= OnSuccess;
            GameEvents.OnMissionFailed -= OnFail;
        }

        public void PlayButton() => Play(buttonId);
        public void PlayTab() => Play(tabId);
        public void PlayEvidence() => Play(evidenceId);
        public void PlayObjective() => Play(objectiveId);
        public void PlayEntityDiscovered() => Play(entityId);
        public void PlayMissionSuccess() => Play(successId);
        public void PlayMissionFail() => Play(failId);

        private void Play(string id) => AudioManager.Instance?.PlayEvent(id, null, 0.65f);

        private void OnEvidence(EvidenceType t) => PlayEvidence();
        private void OnObjective(string id) => PlayObjective();
        private void OnEntity(GhostDefinition g) => PlayEntityDiscovered();
        private void OnSuccess() => PlayMissionSuccess();
        private void OnFail() => PlayMissionFail();
    }
}
