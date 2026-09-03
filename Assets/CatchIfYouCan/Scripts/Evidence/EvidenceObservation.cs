using UnityEngine;

namespace CatchIfYouCan.Evidence
{
    /// <summary>
    /// A device saying what it just measured. Not a claim that the evidence is real.
    ///
    /// <para>
    /// Equipment used to call <c>RegisterEvidence</c> directly, which meant a device that fired
    /// once had proved something. Nothing checked whether the ghost in the house even exhibits
    /// that evidence type, so a thermometer reading a cold draught could prove Freezing
    /// Temperatures against a ghost that has none of it, and the journal would say so.
    /// </para>
    /// </summary>
    public readonly struct EvidenceObservation
    {
        /// <summary>What the device thinks it saw.</summary>
        public readonly EvidenceType Type;

        /// <summary>Which device said so, for the log and for a lab readout.</summary>
        public readonly string SourceDeviceId;

        /// <summary>
        /// How strongly, 0 to 1. A reading barely over a threshold and one that pinned the
        /// needle are not the same observation, and a validator is allowed to care.
        /// </summary>
        public readonly float Strength;

        /// <summary>Where it was measured.</summary>
        public readonly Vector3 Position;

        public EvidenceObservation(EvidenceType type, string sourceDeviceId,
                                   float strength, Vector3 position)
        {
            Type = type;
            SourceDeviceId = sourceDeviceId;
            Strength = Mathf.Clamp01(strength);
            Position = position;
        }

        public override string ToString() =>
            Type + " from " + (SourceDeviceId ?? "?") + " at " + Strength.ToString("F2");
    }

    /// <summary>What the validator decided about an observation, and why.</summary>
    public enum EvidenceConfirmation
    {
        /// <summary>Accepted and registered.</summary>
        Confirmed = 0,

        /// <summary>Already found. Not a failure; the device was right, just late.</summary>
        AlreadyFound,

        /// <summary>No ghost is assigned, so nothing can be proved about it yet.</summary>
        NoActiveGhost,

        /// <summary>The ghost in this house does not exhibit this evidence type.</summary>
        NotInGhostProfile,

        /// <summary>Seen, but not for long enough yet. One noisy frame is not evidence.</summary>
        Dwelling,

        /// <summary>Seen too soon after the last observation of this type.</summary>
        CoolingDown,

        /// <summary>Below the strength this evidence type needs.</summary>
        TooWeak,
    }
}
