using UnityEngine;

namespace CatchIfYouCan.Missions
{
    public enum ObjectiveKind
    {
        IdentifyEntity,
        CapturePhoto,
        DetectEMF,
        SurviveHunt,
        FindEvidence,
        SaltGhost,
        RecordEVP
    }

    [CreateAssetMenu(fileName = "ObjectiveDefinition", menuName = "Catch If You Can/Missions/Objective Definition")]
    public class ObjectiveDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(2, 4)] public string Description;
        public ObjectiveKind Kind = ObjectiveKind.FindEvidence;
        public bool IsMainObjective;
        public int TargetCount = 1;
        public string TargetEvidence;
        public int RewardBonus = 50;
    }
}
