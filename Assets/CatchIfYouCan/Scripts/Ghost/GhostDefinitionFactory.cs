using CatchIfYouCan.Evidence;
using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    public static class GhostDefinitionFactory
    {
        public static GhostDefinition[] CreateAllDefaultGhosts()
        {
            return new[]
            {
                Create(
                    "the_wanderer",
                    "THE WANDERER",
                    EvidenceType.EMFSurge,
                    EvidenceType.FreezingTemperature,
                    EvidenceType.SpectralGrid,
                    aggression: 0.35f,
                    curiosity: 0.7f,
                    activity: 0.75f,
                    roamFrequency: 0.85f,
                    huntThreshold: 0.7f,
                    speed: 3.2f,
                    visual: GhostVisualProfile.HumanSilhouette,
                    description: "Restless entity that roams every room it can reach.",
                    behavior: "Frequent roaming and orb manifestations.",
                    strengths: "Hard to pin to one room.",
                    weaknesses: "Predictable movement patterns."),
                Create(
                    "the_whisper",
                    "THE WHISPER",
                    EvidenceType.EVPResponse,
                    EvidenceType.ParabolicAnomaly,
                    EvidenceType.FreezingTemperature,
                    aggression: 0.4f,
                    curiosity: 0.55f,
                    activity: 0.6f,
                    roamFrequency: 0.35f,
                    huntThreshold: 0.62f,
                    speed: 2.2f,
                    visual: GhostVisualProfile.TallShadow,
                    description: "Voice-driven presence that answers through static and whispers.",
                    behavior: "Responds to EVP sessions and distorts electronics.",
                    strengths: "Strong audio deception.",
                    weaknesses: "Low physical aggression."),
                Create(
                    "the_watcher",
                    "THE WATCHER",
                    EvidenceType.GhostOrb,
                    EvidenceType.UVTraces,
                    EvidenceType.SpectralGrid,
                    aggression: 0.55f,
                    curiosity: 0.8f,
                    activity: 0.5f,
                    roamFrequency: 0.25f,
                    huntThreshold: 0.68f,
                    speed: 2.0f,
                    visual: GhostVisualProfile.HumanSilhouette,
                    description: "Patient observer that marks surfaces and watches from afar.",
                    behavior: "UV traces near windows and doorways.",
                    strengths: "Excellent awareness of player position.",
                    weaknesses: "Slow to hunt."),
                Create(
                    "the_mimicer",
                    "THE MIMICER",
                    EvidenceType.EVPResponse,
                    EvidenceType.EMFSurge,
                    EvidenceType.UVTraces,
                    aggression: 0.65f,
                    curiosity: 0.45f,
                    activity: 0.7f,
                    roamFrequency: 0.4f,
                    huntThreshold: 0.6f,
                    speed: 2.8f,
                    visual: GhostVisualProfile.DistortedWoman,
                    description: "Copies footsteps, voices, and household sounds.",
                    behavior: "Parabolic anomalies and thrown objects.",
                    strengths: "Creates false leads.",
                    weaknesses: "Reveals itself under sustained recording."),
                Create(
                    "the_hollow",
                    "THE HOLLOW",
                    EvidenceType.FreezingTemperature,
                    EvidenceType.GhostOrb,
                    EvidenceType.EMFSurge,
                    aggression: 0.5f,
                    curiosity: 0.3f,
                    activity: 0.55f,
                    roamFrequency: 0.3f,
                    huntThreshold: 0.66f,
                    speed: 2.4f,
                    visual: GhostVisualProfile.FacelessFigure,
                    description: "Cold void entity that drains warmth from rooms.",
                    behavior: "Freezing zones and spectral silhouettes.",
                    strengths: "Strong temperature evidence.",
                    weaknesses: "Slow manifestation."),
                Create(
                    "the_knocker",
                    "THE KNOCKER",
                    EvidenceType.ParabolicAnomaly,
                    EvidenceType.UVTraces,
                    EvidenceType.EMFSurge,
                    aggression: 0.75f,
                    curiosity: 0.35f,
                    activity: 0.8f,
                    roamFrequency: 0.45f,
                    huntThreshold: 0.58f,
                    speed: 3.0f,
                    visual: GhostVisualProfile.TallShadow,
                    description: "Aggressive poltergeist that slams doors and knocks walls.",
                    behavior: "Physical disturbances and hunt triggers.",
                    strengths: "High hunt frequency.",
                    weaknesses: "Loud and easy to track."),
                Create(
                    "the_shadeborn",
                    "THE SHADEBORN",
                    EvidenceType.SpectralGrid,
                    EvidenceType.GhostOrb,
                    EvidenceType.FreezingTemperature,
                    aggression: 0.45f,
                    curiosity: 0.5f,
                    activity: 0.65f,
                    roamFrequency: 0.5f,
                    huntThreshold: 0.64f,
                    speed: 2.6f,
                    visual: GhostVisualProfile.TallShadow,
                    description: "Born from darkness; leaves grid projections in doorways.",
                    behavior: "Spectral grid silhouettes and cold spots.",
                    strengths: "Visual evidence in low light.",
                    weaknesses: "Weakened in well-lit areas."),
                Create(
                    "the_static",
                    "THE STATIC",
                    EvidenceType.EMFSurge,
                    EvidenceType.EVPResponse,
                    EvidenceType.ElectronicDistortion,
                    aggression: 0.6f,
                    curiosity: 0.6f,
                    activity: 0.72f,
                    roamFrequency: 0.35f,
                    huntThreshold: 0.63f,
                    speed: 2.5f,
                    visual: GhostVisualProfile.FacelessFigure,
                    description: "Electromagnetic entity that scrambles devices and radios.",
                    behavior: "Electronic distortion spikes during hunts.",
                    strengths: "Disables equipment quickly.",
                    weaknesses: "EMF spikes reveal location."),
                Create(
                    "the_crawler",
                    "THE CRAWLER",
                    EvidenceType.UVTraces,
                    EvidenceType.SpectralGrid,
                    EvidenceType.ParabolicAnomaly,
                    aggression: 0.8f,
                    curiosity: 0.25f,
                    activity: 0.85f,
                    roamFrequency: 0.55f,
                    huntThreshold: 0.55f,
                    speed: 3.5f,
                    visual: GhostVisualProfile.CrawlingEntity,
                    description: "Low, fast predator that hunts from floor vents and crawlspaces.",
                    behavior: "Physical marks and sudden temperature drops.",
                    strengths: "Fast hunts.",
                    weaknesses: "Limited vertical reach."),
                Create(
                    "the_weeping_one",
                    "THE WEEPING ONE",
                    EvidenceType.ParabolicAnomaly,
                    EvidenceType.EVPResponse,
                    EvidenceType.GhostOrb,
                    aggression: 0.7f,
                    curiosity: 0.4f,
                    activity: 0.6f,
                    roamFrequency: 0.2f,
                    huntThreshold: 0.72f,
                    speed: 2.1f,
                    visual: GhostVisualProfile.HumanSilhouette,
                    description: "Mourning spirit anchored to a single room.",
                    behavior: "Orbs and grid figures near its anchor point.",
                    strengths: "Extremely territorial.",
                    weaknesses: "Rarely leaves ghost room.")
            };
        }

        private static GhostDefinition Create(
            string id,
            string displayName,
            EvidenceType evidence1,
            EvidenceType evidence2,
            EvidenceType evidence3,
            float aggression,
            float curiosity,
            float activity,
            float roamFrequency,
            float huntThreshold,
            float speed,
            GhostVisualProfile visual,
            string description,
            string behavior,
            string strengths,
            string weaknesses)
        {
            var ghost = ScriptableObject.CreateInstance<GhostDefinition>();
            ghost.Id = id;
            ghost.DisplayName = displayName;
            ghost.Evidence1 = evidence1;
            ghost.Evidence2 = evidence2;
            ghost.Evidence3 = evidence3;
            ghost.Aggression = aggression;
            ghost.Curiosity = curiosity;
            ghost.Activity = activity;
            ghost.RoamFrequency = roamFrequency;
            ghost.HuntThreshold = huntThreshold;
            ghost.Speed = speed;
            ghost.VisualProfile = visual;
            ghost.Description = description;
            ghost.BehaviorNotes = behavior;
            ghost.Strengths = strengths;
            ghost.Weaknesses = weaknesses;
            ghost.DoorInteractionChance = 0.2f + aggression * 0.25f;
            ghost.LightInteractionChance = 0.15f + activity * 0.2f;
            ghost.ObjectThrowChance = 0.1f + aggression * 0.2f;
            ghost.ManifestationChance = 0.1f + curiosity * 0.15f;
            ghost.ResponseFrequency = 0.3f + curiosity * 0.3f;
            ghost.SoundSensitivity = 0.4f + aggression * 0.2f;
            ghost.ElectronicsSensitivity = 0.35f + activity * 0.25f;
            ghost.SearchAbility = 0.35f + aggression * 0.35f;
            return ghost;
        }
    }
}
