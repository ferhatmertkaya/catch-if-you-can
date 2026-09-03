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

        [Header("Entities")]
        [Tooltip("Which entities can haunt this location. Empty means the whole roster.\n\n" +
                 "This is what makes a mission solvable rather than a guess. A player deduces " +
                 "the entity from the evidence they can actually gather with the kit they were " +
                 "sent in with, so a location that can host an entity whose evidence needs a " +
                 "tool nobody brought is a location where the answer cannot be worked out. " +
                 "Restricting the roster is content, not a change to what counts as evidence.")]
        public string[] EligibleGhostIds;

        [TextArea(2, 4)]
        public string Briefing;
    }
}
