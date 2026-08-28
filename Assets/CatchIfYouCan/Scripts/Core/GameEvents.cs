using System;
using UnityEngine;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Missions;

namespace CatchIfYouCan.Core
{
    public static class GameEvents
    {
        public static event Action<float> OnGhostActivityChanged;
        public static event Action<EvidenceType> OnEvidenceDetected;
        public static event Action OnHuntStarted;
        public static event Action OnHuntEnded;
        public static event Action OnPlayerDied;
        public static event Action OnDoorOpened;
        public static event Action<float, Vector3> OnNoiseGenerated;
        public static event Action OnEquipmentChanged;
        public static event Action<string> OnObjectiveCompleted;
        public static event Action OnMissionComplete;
        public static event Action OnMissionFailed;
        public static event Action<float> OnFearChanged;
        public static event Action OnInvestigationStarted;
        public static event Action<GhostDefinition> OnEntityDiscovered;
        public static event Action<int> OnPhotoTaken;
        public static event Action OnBreakerChanged;
        public static event Action<string> OnTipRequested;

        public static void GhostActivityChanged(float v) => OnGhostActivityChanged?.Invoke(v);
        public static void EvidenceDetected(EvidenceType t) => OnEvidenceDetected?.Invoke(t);
        public static void HuntStarted() => OnHuntStarted?.Invoke();
        public static void HuntEnded() => OnHuntEnded?.Invoke();
        public static void PlayerDied() => OnPlayerDied?.Invoke();
        public static void DoorOpened() => OnDoorOpened?.Invoke();
        public static void NoiseGenerated(float intensity, Vector3 pos) => OnNoiseGenerated?.Invoke(intensity, pos);
        public static void EquipmentChanged() => OnEquipmentChanged?.Invoke();
        public static void ObjectiveCompleted(string id) => OnObjectiveCompleted?.Invoke(id);
        public static void MissionComplete() => OnMissionComplete?.Invoke();
        public static void MissionFailed() => OnMissionFailed?.Invoke();
        public static void FearChanged(float v) => OnFearChanged?.Invoke(v);
        public static void InvestigationStarted() => OnInvestigationStarted?.Invoke();
        public static void EntityDiscovered(GhostDefinition g) => OnEntityDiscovered?.Invoke(g);
        public static void PhotoTaken(int stars) => OnPhotoTaken?.Invoke(stars);
        public static void BreakerChanged() => OnBreakerChanged?.Invoke();
        public static void TipRequested(string tip) => OnTipRequested?.Invoke(tip);
    }
}
