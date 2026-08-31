using System;
using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Missions
{
    public class MissionManager : SingletonBehaviour<MissionManager>
    {
        [SerializeField] private MissionDefinition[] availableMissions;
        [SerializeField] private GhostDefinition[] ghostPool;
        [SerializeField] private int startingCaseNumber = 1001;

        private int _nextCaseNumber;

        public MissionRuntime ActiveMission { get; private set; }
        public MissionDefinition SelectedMission { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _nextCaseNumber = PlayerPrefs.GetInt("ciyc_next_case", startingCaseNumber);

            if (availableMissions == null || availableMissions.Length == 0)
                availableMissions = MissionDefinitionFactory.CreateAllDefaultMissions();

            if (ghostPool == null || ghostPool.Length == 0)
                ghostPool = GhostDefinitionFactory.CreateAllDefaultGhosts();
        }

        public MissionDefinition[] GetAvailableMissions()
        {
            return availableMissions ?? Array.Empty<MissionDefinition>();
        }

        public void SelectMission(MissionDefinition mission)
        {
            SelectedMission = mission;
            CIYCLog.Info($"Mission selected: {mission?.MapName ?? "None"}");
        }

        public MissionDefinition SelectRandomMission()
        {
            if (availableMissions == null || availableMissions.Length == 0)
                return null;

            int index = UnityEngine.Random.Range(0, availableMissions.Length);
            SelectedMission = availableMissions[index];
            return SelectedMission;
        }

        public MissionRuntime StartInvestigation(MissionDefinition mission = null, int? seedOverride = null)
        {
            mission = mission ?? SelectedMission ?? SelectRandomMission();
            if (mission == null)
            {
                CIYCLog.Warn("MissionManager: no mission available to start.");
                return null;
            }

            // THE authoritative session seed. This is the live path - MissionSelectUI and
            // InvestigationBootstrap both arrive here - so it is the one that has to be right.
            //
            // It previously drew from UnityEngine.Random, a process-global stream shared with
            // roughly a hundred cosmetic call sites, which made seed selection depend on how
            // many cosmetic draws happened to precede it. SessionSeedSource is the single
            // authoritative source; in multiplayer this call becomes host-only and the result
            // is replicated with MissionStart (Docs/NETWORKING.md §3).
            int seed = seedOverride ?? SessionSeedSource.Next();
            var ghost = PickGhost();
            var runtime = MissionRuntime.Create(mission, _nextCaseNumber++, seed, ghost);
            ActiveMission = runtime;
            SelectedMission = mission;

            PlayerPrefs.SetInt("ciyc_next_case", _nextCaseNumber);
            PlayerPrefs.Save();

            ApplyDifficultyModifiers(runtime);
            GameManager.Instance?.BeginMission(runtime);
            CIYCLog.Info($"Investigation started: CASE #{runtime.CaseNumber} at {runtime.LocationName} (seed {seed}).");
            return runtime;
        }

        public void MarkInvestigationEntered()
        {
            if (ActiveMission == null)
                return;

            ActiveMission.InvestigationStarted = true;
        }

        public int CalculateTotalReward(MissionRuntime runtime, int optionalObjectivesCompleted, bool mainObjectiveComplete)
        {
            if (runtime?.Definition == null)
                return 0;

            int reward = runtime.Definition.BaseReward;
            reward += optionalObjectivesCompleted * runtime.Definition.BonusRewardPerObjective;
            if (mainObjectiveComplete)
                reward += runtime.Definition.BaseReward / 2;

            float multiplier = runtime.Difficulty != null ? runtime.Difficulty.RewardMultiplier : 1f;
            return Mathf.RoundToInt(reward * multiplier);
        }

        public void CompleteActiveMission(bool mainObjectiveComplete, int optionalCompleted)
        {
            if (ActiveMission == null)
                return;

            ActiveMission.Completed = true;
            ActiveMission.OptionalObjectivesCompleted = optionalCompleted;
            int money = CalculateTotalReward(ActiveMission, optionalCompleted, mainObjectiveComplete);
            int xp = money / 2;
            ActiveMission.AddPendingReward(money, xp);
            GameManager.Instance?.CompleteMission();
        }

        public void FailActiveMission()
        {
            if (ActiveMission == null)
                return;

            ActiveMission.Failed = true;
            GameManager.Instance?.FailMission();
        }

        private GhostDefinition PickGhost()
        {
            if (ghostPool == null || ghostPool.Length == 0)
                return null;

            return ghostPool[UnityEngine.Random.Range(0, ghostPool.Length)];
        }

        private void ApplyDifficultyModifiers(MissionRuntime runtime)
        {
            if (runtime?.Difficulty == null || runtime.AssignedGhost == null)
                return;

            runtime.AssignedGhost.Speed *= runtime.Difficulty.GhostSpeedMultiplier;
        }
    }
}
