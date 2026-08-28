using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class MissionResultBreakdown
    {
        public int EntityReward;
        public int EvidenceReward;
        public int ObjectivesReward;
        public int PhotosReward;
        public float DifficultyMultiplier = 1f;
        public int TotalMoney;
        public int XpEarned;
    }

    public class MissionResultUI : MonoBehaviour
    {
        [SerializeField] private Component titleText;
        [SerializeField] private Component breakdownText;
        [SerializeField] private Button continueButton;

        public Component BreakdownText => breakdownText;
        public Button ContinueButton => continueButton;

        public void BindRuntime(Component titleText, Component breakdownText, Button continueButton)
        {
            this.titleText = titleText;
            this.breakdownText = breakdownText;
            this.continueButton = continueButton;
            WireButtons();
        }

        private void OnEnable()
        {
            WireButtons();
            GameEvents.OnMissionComplete += ShowSuccess;
            GameEvents.OnMissionFailed += ShowFailure;
        }

        private void OnDisable()
        {
            GameEvents.OnMissionComplete -= ShowSuccess;
            GameEvents.OnMissionFailed -= ShowFailure;
        }

        private void Start() => WireButtons();

        private void WireButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinue);
            }
        }

        public void ShowSuccess()
        {
            gameObject.SetActive(true);
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionComplete, false);

            UITheme.SetText(titleText, "MISSION COMPLETE");
            UITheme.SetTextColor(titleText, UITheme.Primary);

            var mission = GameManager.Instance?.CurrentMission
                          ?? MissionManager.Instance?.ActiveMission;
            var breakdown = BuildBreakdown(mission, success: true);

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.AddMoney(breakdown.TotalMoney);
                SaveManager.Instance.AddXp(breakdown.XpEarned);
            }
            if (StatisticsTracker.Instance != null)
            {
                StatisticsTracker.Instance.RecordSuccessfulCase();
                if (mission != null && mission.AssignedGhost != null)
                    StatisticsTracker.Instance.RecordCorrectIdentification();
            }

            UITheme.SetText(breakdownText, FormatBreakdown(breakdown));
        }

        public void ShowFailure()
        {
            gameObject.SetActive(true);
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionFailed, false);

            UITheme.SetText(titleText, "MISSION FAILED");
            UITheme.SetTextColor(titleText, UITheme.Warning);
            UITheme.SetText(breakdownText,
                "The entity claimed another investigator.\n\nReward: $0\nXP: +0\n\nReturn to base and prepare better.");
        }

        private static MissionResultBreakdown BuildBreakdown(MissionRuntime mission, bool success)
        {
            var breakdown = new MissionResultBreakdown();
            if (mission == null || !success)
                return breakdown;

            int evidenceCount = EvidenceManager.Instance != null
                ? EvidenceManager.Instance.FoundEvidence.Count
                : mission.EvidenceFound?.Count ?? 0;
            int photos = mission.PhotosTaken;
            int objectives = mission.OptionalObjectivesCompleted;
            bool entityId = mission.AssignedGhost != null;

            breakdown.EntityReward = entityId ? mission.Definition.BaseReward / 2 : 0;
            breakdown.EvidenceReward = evidenceCount * 75;
            breakdown.ObjectivesReward = objectives * (mission.Definition?.BonusRewardPerObjective ?? 75);
            breakdown.PhotosReward = photos * 25;
            breakdown.DifficultyMultiplier = mission.Difficulty != null ? mission.Difficulty.RewardMultiplier : 1f;

            int subtotal = breakdown.EntityReward + breakdown.EvidenceReward +
                           breakdown.ObjectivesReward + breakdown.PhotosReward;
            breakdown.TotalMoney = Mathf.RoundToInt(subtotal * breakdown.DifficultyMultiplier);
            breakdown.XpEarned = breakdown.TotalMoney / 2;

            mission.AddPendingReward(breakdown.TotalMoney, breakdown.XpEarned);
            return breakdown;
        }

        private static string FormatBreakdown(MissionResultBreakdown b)
        {
            return
                $"Entity Identified:     ${b.EntityReward:N0}\n" +
                $"Evidence Collected:    ${b.EvidenceReward:N0}\n" +
                $"Objectives Complete:   ${b.ObjectivesReward:N0}\n" +
                $"Photo Bonus:           ${b.PhotosReward:N0}\n" +
                $"Difficulty Multiplier: x{b.DifficultyMultiplier:0.00}\n" +
                $"------------------------------\n" +
                $"TOTAL MONEY:           ${b.TotalMoney:N0}\n" +
                $"XP EARNED:             +{b.XpEarned}";
        }

        private void OnContinue()
        {
            Time.timeScale = 1f;
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadMainMenu();
            else if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MainMenu);
        }
    }
}
