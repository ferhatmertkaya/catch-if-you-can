using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Ghost
{
    [CreateAssetMenu(fileName = "GhostDefinition", menuName = "Catch If You Can/Ghost Definition")]
    public class GhostDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        [TextArea(2, 4)] public string BehaviorNotes;
        [TextArea(1, 3)] public string Strengths;
        [TextArea(1, 3)] public string Weaknesses;

        [Header("Evidence")]
        public EvidenceType Evidence1;
        public EvidenceType Evidence2;
        public EvidenceType Evidence3;

        [Header("Personality (0-1)")]
        [Range(0f, 1f)] public float Aggression = 0.5f;
        [Range(0f, 1f)] public float Curiosity = 0.5f;
        [Range(0f, 1f)] public float Activity = 0.5f;
        [Range(0f, 1f)] public float RoamFrequency = 0.5f;
        [Range(0f, 1f)] public float HuntThreshold = 0.65f;
        [Range(0f, 1f)] public float SoundSensitivity = 0.5f;
        [Range(0f, 1f)] public float ElectronicsSensitivity = 0.5f;

        [Header("Interaction Chances (0-1)")]
        [Range(0f, 1f)] public float DoorInteractionChance = 0.3f;
        [Range(0f, 1f)] public float LightInteractionChance = 0.25f;
        [Range(0f, 1f)] public float ObjectThrowChance = 0.2f;
        [Range(0f, 1f)] public float ManifestationChance = 0.15f;
        [Range(0f, 1f)] public float ResponseFrequency = 0.4f;

        [Header("Movement & Perception")]
        [Range(0.5f, 6f)] public float Speed = 2.5f;
        [Range(0f, 1f)] public float SearchAbility = 0.5f;
        public GhostVisualProfile VisualProfile = GhostVisualProfile.HumanSilhouette;

        [Header("Prefab")]
        public GameObject Prefab;

        public bool HasEvidence(EvidenceType type)
        {
            return Evidence1 == type || Evidence2 == type || Evidence3 == type;
        }

        public float GetEffectiveHuntThreshold(float activityNormalized)
        {
            return Mathf.Clamp01(HuntThreshold - Activity * 0.1f + (1f - activityNormalized) * 0.05f);
        }
    }
}
