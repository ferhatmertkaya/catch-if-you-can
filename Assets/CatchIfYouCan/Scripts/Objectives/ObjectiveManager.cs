using System;
using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Objectives
{
    public class ObjectiveManager : SingletonBehaviour<ObjectiveManager>
    {
        [SerializeField] private ObjectiveDefinition[] objectiveLibrary;

        private readonly List<ObjectiveBase> _activeObjectives = new List<ObjectiveBase>();
        private readonly HashSet<string> _reportedCompletions = new HashSet<string>();
        private ObjectiveBase _mainObjective;
        private bool _missionCompletionTriggered;

        public IReadOnlyList<ObjectiveBase> ActiveObjectives => _activeObjectives;
        public ObjectiveBase MainObjective => _mainObjective;

        public void AssignMissionObjectives(MissionRuntime mission)
        {
            ClearObjectives();
            if (mission == null)
                return;

            _mainObjective = CreateObjective(FindMainDefinition() ?? BuildFallbackMain(mission), mission, false);
            if (_mainObjective != null)
            {
                _mainObjective.Activate();
                _activeObjectives.Add(_mainObjective);
            }

            var optionalDefs = PickOptionalDefinitions(3);
            for (int i = 0; i < optionalDefs.Count; i++)
            {
                var objective = CreateObjective(optionalDefs[i], mission, true);
                if (objective == null)
                    continue;

                objective.Activate();
                _activeObjectives.Add(objective);
            }
        }

        private ObjectiveDefinition FindMainDefinition()
        {
            if (objectiveLibrary == null)
                return null;

            for (int i = 0; i < objectiveLibrary.Length; i++)
            {
                if (objectiveLibrary[i] != null && objectiveLibrary[i].IsMainObjective)
                    return objectiveLibrary[i];
            }

            return null;
        }

        private List<ObjectiveDefinition> PickOptionalDefinitions(int count)
        {
            var result = new List<ObjectiveDefinition>();
            if (objectiveLibrary == null || objectiveLibrary.Length == 0)
                return result;

            var pool = new List<ObjectiveDefinition>();
            for (int i = 0; i < objectiveLibrary.Length; i++)
            {
                if (objectiveLibrary[i] != null && !objectiveLibrary[i].IsMainObjective)
                    pool.Add(objectiveLibrary[i]);
            }

            while (result.Count < count && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        private ObjectiveDefinition BuildFallbackMain(MissionRuntime mission)
        {
            var def = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            def.Id = "identify_entity";
            def.Title = "Identify Entity";
            def.Description = "Gather evidence and identify the haunting entity.";
            def.Kind = ObjectiveKind.IdentifyEntity;
            def.IsMainObjective = true;
            return def;
        }

        private ObjectiveBase CreateObjective(ObjectiveDefinition definition, MissionRuntime mission, bool optional)
        {
            if (definition == null)
                return null;

            string id = string.IsNullOrEmpty(definition.Id) ? definition.name : definition.Id;
            string description = string.IsNullOrEmpty(definition.Description) ? definition.Title : definition.Description;

            switch (definition.Kind)
            {
                case ObjectiveKind.IdentifyEntity:
                    return new IdentifyEntityObjective(id, description, mission.AssignedGhost, optional);
                case ObjectiveKind.CapturePhoto:
                    return new CapturePhotoObjective(id, description, 2, optional);
                case ObjectiveKind.DetectEMF:
                    return new DetectEMFObjective(id, description, optional);
                case ObjectiveKind.SurviveHunt:
                    return new SurviveHuntObjective(id, description, optional);
                case ObjectiveKind.FindEvidence:
                    return new FindEvidenceObjective(id, description, ParseEvidence(definition.TargetEvidence), definition.TargetCount, optional);
                case ObjectiveKind.SaltGhost:
                    return new SaltGhostObjective(id, description, optional);
                case ObjectiveKind.RecordEVP:
                    return new RecordEVPObjective(id, description, optional);
                default:
                    return new FindEvidenceObjective(id, description, EvidenceType.GhostOrb, 1, optional);
            }
        }

        private static EvidenceType ParseEvidence(string value)
        {
            if (string.IsNullOrEmpty(value))
                return EvidenceType.GhostOrb;

            return Enum.TryParse(value, true, out EvidenceType parsed) ? parsed : EvidenceType.GhostOrb;
        }

        private void Update()
        {
            for (int i = 0; i < _activeObjectives.Count; i++)
            {
                var objective = _activeObjectives[i];
                if (objective == null || !objective.IsComplete)
                    continue;

                if (_reportedCompletions.Contains(objective.Id))
                    continue;

                _reportedCompletions.Add(objective.Id);
                GameEvents.ObjectiveCompleted(objective.Id);

                if (!_missionCompletionTriggered && objective == _mainObjective && objective.IsComplete)
                {
                    _missionCompletionTriggered = true;
                    int optional = CountCompletedOptional();
                    MissionManager.Instance?.CompleteActiveMission(true, optional);
                }
            }
        }

        public int CountCompletedOptional()
        {
            int count = 0;
            for (int i = 0; i < _activeObjectives.Count; i++)
            {
                var obj = _activeObjectives[i];
                if (obj != null && obj.IsOptional && obj.IsComplete)
                    count++;
            }

            return count;
        }

        public void ClearObjectives()
        {
            for (int i = 0; i < _activeObjectives.Count; i++)
                _activeObjectives[i]?.Deactivate();

            _activeObjectives.Clear();
            _reportedCompletions.Clear();
            _mainObjective = null;
            _missionCompletionTriggered = false;
        }
    }
}
