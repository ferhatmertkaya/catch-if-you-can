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

            var mission = GameManager.Instance?.CurrentMission
                          ?? MissionManager.Instance?.ActiveMission;

            // "Complete" is not the same as "solved". The case is filed either way; whether the
            // entity was named correctly is what this screen has to say plainly, because the
            // reward depends on it and the player is owed the answer.
            bool identified = mission != null && mission.IdentificationCorrect;

            UITheme.SetText(titleText, identified ? "ENTITY IDENTIFIED" : "CASE CLOSED");
            UITheme.SetTextColor(titleText, identified ? UITheme.Primary : UITheme.TextPrimary);

            var breakdown = BuildBreakdown(mission, success: true);

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.AddMoney(breakdown.TotalMoney);
                SaveManager.Instance.AddXp(breakdown.XpEarned);
            }
            if (StatisticsTracker.Instance != null)
            {
                StatisticsTracker.Instance.RecordSuccessfulCase();

                // Recorded when the player was right, not when an entity existed. The old test
                // was "mission.AssignedGhost != null", which is true of every mission ever run,
                // so the correct-identification statistic counted attempts.
                if (identified)
                    StatisticsTracker.Instance.RecordCorrectIdentification();
            }

            UITheme.SetText(breakdownText,
                DescribeIdentification(mission) + FormatBreakdown(breakdown));
        }

        public void ShowFailure()
        {
            gameObject.SetActive(true);
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionFailed, false);

            UITheme.SetText(titleText, "MISSION FAILED");
            UITheme.SetTextColor(titleText, UITheme.Warning);

            var mission = GameManager.Instance?.CurrentMission
                          ?? MissionManager.Instance?.ActiveMission;

            int evidenceCount = EvidenceManager.Instance != null
                ? EvidenceManager.Instance.FoundEvidence.Count
                : mission?.EvidenceFound?.Count ?? 0;

            // Says what happened and what it cost, rather than one fixed sentence. A player who
            // loses an investigation should be able to see how far they got.
            UITheme.SetText(breakdownText,
                "The entity claimed another investigator.\n\n" +
                $"Evidence confirmed:    {evidenceCount}\n" +
                $"Entity identified:     no\n\n" +
                "Reward:                $0\n" +
                "XP:                    +0\n\n" +
                "Evidence is not paid for on a failed case. Return to base and prepare better.");
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
            // The identification bonus is paid for identifying the entity. This used to test
            // whether the mission HAD an entity, which is true of every mission, so the bonus
            // was paid for turning up.
            bool entityId = mission.IdentificationCorrect;

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

        /// <summary>
        /// What the player named, and what was actually there when they got it wrong.
        /// </summary>
        private static string DescribeIdentification(MissionRuntime mission)
        {
            if (mission == null)
                return string.Empty;

            string actual = mission.AssignedGhost != null
                ? mission.AssignedGhost.DisplayName
                : "unknown";

            if (!mission.IdentificationSubmitted)
                return "NO IDENTIFICATION FILED\nThe entity was " + actual + ".\n\n";

            if (mission.IdentificationCorrect)
                return "IDENTIFIED: " + actual + "\n\n";

            return "FILED: " + (mission.IdentifiedGhostId ?? "unknown") +
                   "\nThe entity was " + actual + ".\n\n";
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

        /// <summary>
        /// Back to base, and back to the lobby itself rather than to the title screen in front
        /// of it.
        ///
        /// <para>
        /// The lobby lives inside the menu scene, so this is still a load of that scene - but
        /// the intent is stated before the load rather than inferred after it, and the menu
        /// comes up already in the room. Replaying the cinematic, the phone and the tap to
        /// start after every investigation turns the gameplay loop into a series of title
        /// screens. A cold boot is unaffected: the mode defaults to Cinematic and resets itself
        /// once read.
        /// </para>
        /// </summary>
        private void OnContinue()
        {
            Time.timeScale = 1f;

            MainMenuModeController.PendingEntryMode = MainMenuEntryMode.DirectLobby;

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadMainMenu();
            }
            else if (UIManager.Instance != null)
            {
                // No loader means no scene change, so the intent would sit unread until some
                // later load picked it up and skipped an intro nobody asked to skip.
                MainMenuModeController.PendingEntryMode = MainMenuEntryMode.Cinematic;
                UIManager.Instance.Show(UIScreen.MainMenu);
            }
        }
    }
}
