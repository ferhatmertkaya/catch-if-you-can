using UnityEngine;

namespace CatchIfYouCan.Missions
{
    public enum MissionTheme
    {
        SuburbanHouse,
        OldFarmHouse,
        ForestCabin
    }

    [CreateAssetMenu(fileName = "MissionDefinition", menuName = "Catch If You Can/Missions/Mission Definition")]
    public class MissionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string MapName = "Suburban Home";
        public MissionTheme Theme = MissionTheme.SuburbanHouse;

        [Header("Difficulty")]
        public DifficultyDefinition Difficulty;

        [Header("Estimate")]
        [Range(6, 14)] public int EstimatedRoomCount = 8;

        [Header("Rewards")]
        public int BaseReward = 250;
        public int BonusRewardPerObjective = 75;

        [Header("Loadout")]
        public string[] RecommendedEquipmentIds;

        [TextArea(2, 4)]
        public string Briefing;
    }
}
