using System.Collections.Generic;
using CatchIfYouCan.Equipment;

namespace CatchIfYouCan.Evidence
{
    /// <summary>Whether an evidence type has a real player-facing path, and if not, why not.</summary>
    public enum EvidenceSupport
    {
        /// <summary>A device observes it and the validator can confirm it.</summary>
        Supported = 0,

        /// <summary>
        /// Deliberately has no confirmation path. Not a gap to be filled by inventing a device
        /// - an entry here is a design decision with a reason attached.
        /// </summary>
        IntentionallyUnsupported,
    }

    /// <summary>One evidence type's authoritative path, as data rather than as folklore.</summary>
    public readonly struct EvidenceSource
    {
        public readonly EvidenceType Type;

        /// <summary>
        /// The equipment id whose <c>Observe</c> call is the only legal way this evidence type
        /// enters the system, or null when unsupported. One id, not a list: two devices able to
        /// prove the same thing is two places for the rule to drift.
        /// </summary>
        public readonly string ObserverEquipmentId;

        /// <summary>What the device is actually measuring, for the journal and the lab.</summary>
        public readonly string Phenomenon;

        public readonly EvidenceSupport Support;

        public EvidenceSource(EvidenceType type, string observerEquipmentId,
                              string phenomenon, EvidenceSupport support)
        {
            Type = type;
            ObserverEquipmentId = observerEquipmentId;
            Phenomenon = phenomenon;
            Support = support;
        }

        public bool IsSupported => Support == EvidenceSupport.Supported;
    }

    /// <summary>
    /// The one table that says how each kind of evidence can be proved, and who proves it.
    ///
    /// <para>
    /// <b>Observation and confirmation are different acts with different owners.</b> A device
    /// observes: it says "I measured this, this strongly, here". Confirmation - turning that
    /// into a fact in the journal that completes objectives - belongs to the host, through
    /// <see cref="EvidenceValidator"/>. V3 built that boundary; this states the contract it
    /// enforces, so "which device can prove Ghost Orbs" has a written answer rather than being
    /// whatever the code happens to do this week.
    /// </para>
    ///
    /// <para>
    /// It matters most for the thing that keeps going wrong here. Three separate paths used to
    /// announce evidence with nothing found, and each was found by reading unrelated code. A
    /// table CI can compare against the actual <c>Observe</c> call sites turns that from a
    /// discovery into a build failure.
    /// </para>
    ///
    /// <para>
    /// <b>Being detectable is not being proved.</b> Every entry below still passes through the
    /// validator's ghost-profile check, strength floor and dwell. A device that fires is a
    /// device that has an opinion.
    /// </para>
    /// </summary>
    public static class EvidenceAuthority
    {
        /// <summary>
        /// Every evidence type, exactly once. Adding a value to <see cref="EvidenceType"/>
        /// without adding it here is caught by <see cref="Validate"/> and by CI.
        /// </summary>
        private static readonly EvidenceSource[] Sources =
        {
            new EvidenceSource(EvidenceType.EMFSurge, EquipmentIds.EmfDetector,
                "a field left where the ghost has been", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.UVTraces, EquipmentIds.UvLight,
                "prints and disturbed salt, brought out under ultraviolet", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.SpectralGrid, EquipmentIds.SpectralGrid,
                "a body standing in the projected point field", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.EVPResponse, EquipmentIds.EvpRecorder,
                "an answer on playback", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.GhostOrb, EquipmentIds.VideoCamera,
                "a mote visible only down a camera feed", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.FreezingTemperature, EquipmentIds.Thermometer,
                "air the ghost has taken the heat out of", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.ParabolicAnomaly, EquipmentIds.ParabolicMicrophone,
                "a sound with a direction and no source", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.ElectronicDistortion, EquipmentIds.Flashlight,
                "current pulled out of a torch near the entity", EvidenceSupport.Supported),

            new EvidenceSource(EvidenceType.PhysicalDisturbance, EquipmentIds.PhotoCamera,
                "a photograph of something the ghost actually moved", EvidenceSupport.Supported),
        };

        /// <summary>The authoritative path for one evidence type.</summary>
        public static EvidenceSource For(EvidenceType type)
        {
            for (int i = 0; i < Sources.Length; i++)
            {
                if (Sources[i].Type == type)
                    return Sources[i];
            }

            return new EvidenceSource(type, null, "undeclared",
                                      EvidenceSupport.IntentionallyUnsupported);
        }

        /// <summary>Every declared path, in table order.</summary>
        public static IReadOnlyList<EvidenceSource> All => Sources;

        /// <summary>Whether this type can be proved at all.</summary>
        public static bool IsSupported(EvidenceType type) => For(type).IsSupported;

        /// <summary>
        /// Whether this equipment id is the declared observer for this evidence type.
        ///
        /// <para>
        /// Asked by <see cref="EvidenceValidator"/> on every submission. It is what makes the
        /// table binding rather than descriptive: a device observing something it is not the
        /// declared source of is refused, however correct its measurement was.
        /// </para>
        /// </summary>
        public static bool IsDeclaredObserver(EvidenceType type, string equipmentId)
        {
            var source = For(type);
            return source.IsSupported &&
                   !string.IsNullOrEmpty(equipmentId) &&
                   string.Equals(source.ObserverEquipmentId, equipmentId,
                                 System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Everything wrong with the table, as text. Empty means coherent.
        ///
        /// <para>
        /// Checks the two things that can silently rot: an evidence type with no entry, and an
        /// entry naming an equipment id that is not in the canonical roster. Both compile
        /// clean, and both mean an evidence type nobody can ever prove.
        /// </para>
        /// </summary>
        public static List<string> Validate()
        {
            var issues = new List<string>();
            var seen = new HashSet<EvidenceType>();

            for (int i = 0; i < Sources.Length; i++)
            {
                var source = Sources[i];

                if (!seen.Add(source.Type))
                    issues.Add(source.Type + " is declared more than once");

                if (!source.IsSupported)
                    continue;

                if (string.IsNullOrEmpty(source.ObserverEquipmentId))
                {
                    issues.Add(source.Type + " is supported but names no observer");
                    continue;
                }

                if (EquipmentIds.IndexOf(source.ObserverEquipmentId) < 0)
                {
                    issues.Add(source.Type + " names '" + source.ObserverEquipmentId +
                               "', which is not a canonical equipment id");
                }
            }

            foreach (EvidenceType type in System.Enum.GetValues(typeof(EvidenceType)))
            {
                if (!seen.Contains(type))
                    issues.Add(type + " has no declared confirmation path");
            }

            return issues;
        }
    }
}
