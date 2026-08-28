using UnityEngine;

namespace CatchIfYouCan.Missions
{
    public enum DifficultyTier
    {
        Casual,
        Investigator,
        Nightmare
    }

    [CreateAssetMenu(fileName = "DifficultyDefinition", menuName = "Catch If You Can/Missions/Difficulty Definition")]
    public class DifficultyDefinition : ScriptableObject
    {
        public DifficultyTier Tier = DifficultyTier.Investigator;
        public string DisplayName = "Investigator";

        [Header("Hunt")]
        [Range(0f, 1f)] public float HuntWarningLeadTime = 0.35f;
        [Range(0.5f, 2f)] public float GhostSpeedMultiplier = 1f;
        [Range(0f, 1f)] public float HuntFrequencyMultiplier = 1f;

        [Header("Investigation")]
        [Range(0, 8)] public int ExtraHideSpots = 0;
        [Range(0f, 1f)] public float HintAvailability = 0.5f;
        [Range(0.5f, 2f)] public float EvidenceRateMultiplier = 1f;
        [Range(0.5f, 2f)] public float RewardMultiplier = 1f;

        public static DifficultyDefinition CreatePreset(DifficultyTier tier)
        {
            var def = CreateInstance<DifficultyDefinition>();
            def.Tier = tier;
            switch (tier)
            {
                case DifficultyTier.Casual:
                    def.DisplayName = "Casual";
                    def.HuntWarningLeadTime = 0.55f;
                    def.GhostSpeedMultiplier = 0.85f;
                    def.HuntFrequencyMultiplier = 0.7f;
                    def.ExtraHideSpots = 2;
                    def.HintAvailability = 0.85f;
                    def.EvidenceRateMultiplier = 1.25f;
                    def.RewardMultiplier = 0.85f;
                    break;
                case DifficultyTier.Nightmare:
                    def.DisplayName = "Nightmare";
                    def.HuntWarningLeadTime = 0.15f;
                    def.GhostSpeedMultiplier = 1.35f;
                    def.HuntFrequencyMultiplier = 1.45f;
                    def.ExtraHideSpots = 0;
                    def.HintAvailability = 0.15f;
                    def.EvidenceRateMultiplier = 0.85f;
                    def.RewardMultiplier = 1.35f;
                    break;
                default:
                    def.DisplayName = "Investigator";
                    def.HuntWarningLeadTime = 0.35f;
                    def.GhostSpeedMultiplier = 1f;
                    def.HuntFrequencyMultiplier = 1f;
                    def.ExtraHideSpots = 1;
                    def.HintAvailability = 0.5f;
                    def.EvidenceRateMultiplier = 1f;
                    def.RewardMultiplier = 1f;
                    break;
            }

            return def;
        }
    }
}
