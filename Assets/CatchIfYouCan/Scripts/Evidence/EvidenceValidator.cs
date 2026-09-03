using System.Collections.Generic;
using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Evidence
{
    /// <summary>
    /// Decides whether what a device measured is evidence.
    ///
    /// <para>
    /// <b>Equipment observes. This decides truth.</b> The two were the same thing: an item
    /// called RegisterEvidence and the journal believed it. Nothing checked the ghost, so a
    /// thermometer could prove Freezing Temperatures against a ghost that does not have it, and
    /// nothing checked duration, so one noisy frame was as good as a sustained reading. Pressing
    /// Use was, in effect, a way to grant evidence.
    /// </para>
    ///
    /// <para>
    /// It is also where host authority goes later. A future networked build validates
    /// observations on the host and replicates the confirmations; the eleven items keep
    /// submitting observations and never learn the difference. That is the whole reason this is
    /// a boundary rather than a rule inside each item.
    /// </para>
    ///
    /// <para>
    /// Ghost AI is untouched. This only reads <see cref="Ghost.GhostDefinition.HasEvidence"/>,
    /// which already existed and was already the ghost's own statement of what it exhibits.
    /// </para>
    /// </summary>
    public static class EvidenceValidator
    {
        /// <summary>
        /// How long a device must keep observing before it counts, per evidence type, in
        /// seconds. Longer for the ones a player can stumble into, shorter for the ones that
        /// take a deliberate act.
        /// </summary>
        private static readonly Dictionary<EvidenceType, float> DwellSeconds =
            new Dictionary<EvidenceType, float>
            {
                { EvidenceType.EMFSurge, 1.5f },
                { EvidenceType.FreezingTemperature, 3f },
                { EvidenceType.UVTraces, 1f },
                { EvidenceType.SpectralGrid, 0.75f },
                { EvidenceType.ParabolicAnomaly, 2f },
                { EvidenceType.ElectronicDistortion, 2f },
                // An EVP response and a photograph are single deliberate events, not readings
                // held steady, so they confirm the moment they are valid.
                { EvidenceType.EVPResponse, 0f },
                { EvidenceType.GhostOrb, 0f },
                { EvidenceType.PhysicalDisturbance, 0f },
            };

        /// <summary>Minimum strength an observation needs before its dwell even starts.</summary>
        private const float MinimumStrength = 0.15f;

        /// <summary>
        /// How long an interrupted observation keeps its accumulated dwell before it is
        /// forgotten. A reading that flickers for a frame should not restart the clock.
        /// </summary>
        private const float DwellGraceSeconds = 0.5f;

        private struct Progress
        {
            public float Dwell;
            public float LastSeenTime;
        }

        private static readonly Dictionary<EvidenceType, Progress> InFlight =
            new Dictionary<EvidenceType, Progress>();

        /// <summary>The last decision, for the equipment lab's readout.</summary>
        public static EvidenceObservation LastObservation { get; private set; }
        public static EvidenceConfirmation LastConfirmation { get; private set; }

        /// <summary>
        /// Submits what a device measured. Returns what was decided, and registers the evidence
        /// only when it is confirmed.
        /// </summary>
        public static EvidenceConfirmation Submit(in EvidenceObservation observation)
        {
            LastObservation = observation;
            return LastConfirmation = Decide(observation);
        }

        private static EvidenceConfirmation Decide(in EvidenceObservation observation)
        {
            if (!ServiceLocator.TryGet<EvidenceManager>(out var manager))
                return EvidenceConfirmation.NoActiveGhost;

            if (manager.HasEvidence(observation.Type))
                return EvidenceConfirmation.AlreadyFound;

            var ghost = ActiveGhost();
            if (ghost == null)
                return EvidenceConfirmation.NoActiveGhost;

            // The ghost's own statement of what it exhibits. This is the check whose absence
            // let a device prove something the entity does not do.
            if (!ghost.HasEvidence(observation.Type))
            {
                Forget(observation.Type);
                return EvidenceConfirmation.NotInGhostProfile;
            }

            if (observation.Strength < MinimumStrength)
            {
                Forget(observation.Type);
                return EvidenceConfirmation.TooWeak;
            }

            float required = DwellSeconds.TryGetValue(observation.Type, out float seconds)
                ? seconds
                : 1f;

            if (required > 0f && !HasDwelled(observation.Type, required))
                return EvidenceConfirmation.Dwelling;

            Forget(observation.Type);
            manager.RegisterEvidence(observation.Type);
            return EvidenceConfirmation.Confirmed;
        }

        /// <summary>
        /// Accumulates time for one evidence type, forgiving a brief gap so a reading that
        /// flickers for a frame does not restart the clock.
        ///
        /// <para>
        /// The accumulated figure is elapsed time and nothing else. It used to be elapsed time
        /// <i>plus</i> one <c>Time.deltaTime</c> per submission, which is not a quantity: a
        /// device submitting every frame counted each frame twice and dwelled in half the
        /// seconds asked of it, so Freezing Temperatures confirmed in a second and a half
        /// against a three second requirement, while a device submitting on a throttle counted
        /// only a little fast. A dwell that means a different number of seconds depending on how
        /// often the caller happens to ask is not a dwell.
        /// </para>
        /// </summary>
        private static bool HasDwelled(EvidenceType type, float required)
        {
            float now = Time.time;

            if (!InFlight.TryGetValue(type, out var progress) ||
                now - progress.LastSeenTime > DwellGraceSeconds)
            {
                // First sight, or gone long enough to have been forgotten. The clock starts
                // here and this sample is worth no time at all - a device cannot have been
                // observing for longer than it has been observing.
                InFlight[type] = new Progress { Dwell = 0f, LastSeenTime = now };
                return required <= 0f;
            }

            progress.Dwell += now - progress.LastSeenTime;
            progress.LastSeenTime = now;
            InFlight[type] = progress;

            return progress.Dwell >= required;
        }

        private static void Forget(EvidenceType type) => InFlight.Remove(type);

        /// <summary>
        /// How far through its dwell an evidence type is, 0 to 1. For the lab, so "nothing is
        /// happening" and "nearly there" look different.
        /// </summary>
        public static float DwellProgress(EvidenceType type)
        {
            if (!InFlight.TryGetValue(type, out var progress))
                return 0f;

            float required = DwellSeconds.TryGetValue(type, out float seconds) ? seconds : 1f;
            return required <= 0f ? 1f : Mathf.Clamp01(progress.Dwell / required);
        }

        /// <summary>
        /// The ghost this house has, or null. Read from the mission first, because that is the
        /// assignment; the ghost in the scene is the fallback for a lab with a ghost and no
        /// mission.
        ///
        /// <para>
        /// The fallback was <c>FindAnyObjectByType&lt;GhostController&gt;</c>, and this runs on
        /// every observation - so a held thermometer, which submits on every one of its ticks
        /// while it reads cold, walked every object in the scene to find the one ghost, over
        /// and over, for as long as the player stood in the cold spot. The registry added for
        /// exactly this in phase Y already has it.
        /// </para>
        /// </summary>
        private static Ghost.GhostDefinition ActiveGhost()
        {
            var mission = Missions.MissionManager.Instance != null
                ? Missions.MissionManager.Instance.ActiveMission
                : null;

            if (mission != null && mission.AssignedGhost != null)
                return mission.AssignedGhost;

            var controller = Ghost.GhostController.Active;
            return controller != null ? controller.Definition : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            InFlight.Clear();
            LastConfirmation = EvidenceConfirmation.NoActiveGhost;
            LastObservation = default;
        }
    }
}
