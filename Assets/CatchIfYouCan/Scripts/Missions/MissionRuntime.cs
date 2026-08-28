using System;
using System.Collections.Generic;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Missions
{
    [Serializable]
    public class MissionRuntime
    {
        public string CaseId;
        public int CaseNumber;
        public int Seed;
        public MissionDefinition Definition;
        public DifficultyDefinition Difficulty;
        public GhostDefinition AssignedGhost;
        public MissionTheme Theme;
        public DateTime StartTimeUtc;
        public bool InvestigationStarted;
        public bool Completed;
        public bool Failed;

        public readonly HashSet<EvidenceType> EvidenceFound = new HashSet<EvidenceType>();
        public int PhotosTaken;
        public int OptionalObjectivesCompleted;
        public int PendingMoney;
        public int PendingXp;

        public string LocationName => Definition != null ? Definition.MapName : "Unknown Location";

        public static MissionRuntime Create(MissionDefinition definition, int caseNumber, int seed, GhostDefinition ghost)
        {
            var difficulty = definition != null && definition.Difficulty != null
                ? definition.Difficulty
                : DifficultyDefinition.CreatePreset(DifficultyTier.Investigator);

            return new MissionRuntime
            {
                CaseId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                CaseNumber = caseNumber,
                Seed = seed,
                Definition = definition,
                Difficulty = difficulty,
                AssignedGhost = ghost,
                Theme = definition != null ? definition.Theme : MissionTheme.SuburbanHouse,
                StartTimeUtc = DateTime.UtcNow
            };
        }

        public void RegisterEvidence(EvidenceType type)
        {
            EvidenceFound.Add(type);
        }

        public void AddPendingReward(int money, int xp)
        {
            PendingMoney += money;
            PendingXp += xp;
        }
    }
}
