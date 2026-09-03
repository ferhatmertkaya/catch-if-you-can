using UnityEngine;

namespace CatchIfYouCan.Missions
{
    public static class MissionDefinitionFactory
    {
        public static MissionDefinition[] CreateAllDefaultMissions()
        {
            return new[]
            {
                Create(
                    MissionTheme.SuburbanHouse,
                    "SUBURBAN HOUSE",
                    estimatedRooms: 8,
                    baseReward: 600,
                    bonusPerObjective: 75,
                    difficulty: DifficultyTier.Investigator,
                    briefing: "A quiet suburban home with a recent history of unexplained disturbances. "
                              + "Identify the entity before dawn.",
                    recommended: new[]
                    {
                        Equipment.EquipmentIds.Flashlight,
                        Equipment.EquipmentIds.EmfDetector,
                        Equipment.EquipmentIds.UvLight,
                        Equipment.EquipmentIds.Thermometer,
                    },
                    // Three entities, each told apart by which of EMF, UV traces and freezing
                    // temperatures the standard kit can actually find here: the Wanderer leaves
                    // EMF and cold, the Mimicer EMF and traces, the Static only EMF. Their other
                    // evidence needs tools this location is not equipped for, which is what
                    // makes those signatures distinct rather than incomplete.
                    eligibleGhosts: new[]
                    {
                        Ghost.GhostIds.Wanderer,
                        Ghost.GhostIds.Mimicer,
                        Ghost.GhostIds.Static,
                    }),
                Create(
                    MissionTheme.OldFarmHouse,
                    "OLD FARM HOUSE",
                    estimatedRooms: 11,
                    baseReward: 850,
                    bonusPerObjective: 90,
                    difficulty: DifficultyTier.Investigator,
                    briefing: "Decades of isolation have left this farmhouse saturated with activity. "
                              + "Expect aggressive hunts and layered evidence.",
                    recommended: new[] { "thermometer", "evp_recorder", "warding_relic" }),
                Create(
                    MissionTheme.ForestCabin,
                    "FOREST CABIN",
                    estimatedRooms: 6,
                    baseReward: 450,
                    bonusPerObjective: 65,
                    difficulty: DifficultyTier.Casual,
                    briefing: "Remote cabin surrounded by forest. Limited rooms but dense paranormal signatures.",
                    recommended: new[] { "flashlight", "uv_light", "salt" })
            };
        }

        public static MissionDefinition GetByTheme(MissionTheme theme)
        {
            foreach (var mission in CreateAllDefaultMissions())
            {
                if (mission != null && mission.Theme == theme)
                    return mission;
            }

            return null;
        }

        private static MissionDefinition Create(
            MissionTheme theme,
            string mapName,
            int estimatedRooms,
            int baseReward,
            int bonusPerObjective,
            DifficultyTier difficulty,
            string briefing,
            string[] recommended,
            string[] eligibleGhosts = null)
        {
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            mission.Theme = theme;
            mission.MapName = mapName;
            mission.EstimatedRoomCount = estimatedRooms;
            mission.BaseReward = baseReward;
            mission.BonusRewardPerObjective = bonusPerObjective;
            mission.Difficulty = DifficultyDefinition.CreatePreset(difficulty);
            mission.Briefing = briefing;
            mission.RecommendedEquipmentIds = recommended;
            mission.EligibleGhostIds = eligibleGhosts ?? System.Array.Empty<string>();
            return mission;
        }
    }
}
