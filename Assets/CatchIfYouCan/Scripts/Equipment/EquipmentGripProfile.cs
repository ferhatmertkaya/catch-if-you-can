using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// How one item sits in a hand. Not which hand, and not whose.
    ///
    /// <para>
    /// There were three places describing where a held object goes, and none of them agreed on
    /// what they were describing. <see cref="EquipmentDefinition"/> had HandLocalPosition and
    /// HandLocalRotation, applied as a local transform on the anchor.
    /// <see cref="HeldEquipmentBase"/> had its own offsets, applied in the hand's measured
    /// axes. <see cref="Character.CharacterRigProfile"/> had GripPositionOffset and
    /// GripRotationOffset, which nothing read at all. Three stores, one of them dead, and the
    /// live two used by different items in different spaces.
    /// </para>
    ///
    /// <para>
    /// The split is by <b>what the offset is a property of</b>:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>CharacterRigProfile</b> owns the character. Its grip offsets are a correction for
    /// <i>this character's</i> hand - the same correction whatever they are holding. Nathan's
    /// fist is Nathan's fist. It must never grow per-item entries; that is what made it a
    /// second equipment database waiting to happen.
    /// </description></item>
    /// <item><description>
    /// <b>EquipmentGripProfile</b> (this) owns the item. Where the torch sits relative to a
    /// fist is a fact about the torch, and it is the same fact in anyone's hand.
    /// </description></item>
    /// <item><description>
    /// <b>EquipmentPresentation</b> composes the two. It is the only place that does.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// A ScriptableObject rather than fields on the definition, because several items share a
    /// grip - anything cylindrical held in a fist is the torch's grip with a different backset
    /// - and because a grip is tuned by dragging numbers while the game runs, which wants an
    /// asset you can leave selected.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "GripProfile", menuName = "Catch If You Can/Equipment Grip Profile")]
    public sealed class EquipmentGripProfile : ScriptableObject
    {
        [Header("Placement in the fist")]
        [Tooltip("Where the item sits in the fist, in the hand's own measured axes and in " +
                 "metres: X along the item towards its far end, Y out of the back of the hand, " +
                 "Z towards the fingertips.")]
        [SerializeField] private Vector3 positionOffset;

        [Tooltip("Turn added after the item has been laid on the fist, in its own axes.")]
        [SerializeField] private Vector3 rotationOffset;

        [Tooltip("How far back along the item the fist closes, in metres. The origin is the " +
                 "tail, so this decides how much sticks out of the front of the hand.")]
        [SerializeField, Min(0f)] private float backset = 0.085f;

        [Header("Shape")]
        [Tooltip("Which way along the item its length runs, in the item's own axes. The " +
                 "convention is +Y, and an item that does not follow it says so here rather " +
                 "than having the presentation special-case it.")]
        [SerializeField] private Vector3 forwardAxis = Vector3.up;

        [Tooltip("How long the item is, in metres. Used to centre it when dropped and to size " +
                 "the collider it lands on.")]
        [SerializeField, Min(0.01f)] private float length = 0.24f;

        [Header("Fallback aim (no character to measure)")]
        [Tooltip("Offset from the hand in the player's own axes - right, up, forward - used " +
                 "only when there is no character whose knuckles can be measured.")]
        [SerializeField] private Vector3 anchorOffset = new Vector3(0.02f, 0.01f, 0.06f);

        [Tooltip("Downward tilt from level, degrees.")]
        [SerializeField] private float aimPitch = 10f;

        [Tooltip("Seconds the aim lags the body. This is the swing.")]
        [SerializeField, Min(0.01f)] private float aimLag = 0.16f;

        [SerializeField] private float walkBobDegrees = 4.5f;
        [SerializeField] private float walkBobRate = 1.15f;

        [Header("Rig hints (optional)")]
        [Tooltip("How strongly this item pulls the elbow out from the body, 0 to 1. Zero " +
                 "leaves the arm pose exactly as PlayerBodyMotion computed it, which is the " +
                 "default and what every item does today.")]
        [SerializeField, Range(0f, 1f)] private float elbowHintWeight;

        [Tooltip("Extra wrist turn this item wants, degrees. Applied by the presentation on " +
                 "top of the pose; it does not change how the pose was computed.")]
        [SerializeField] private Vector3 wristHint = Vector3.zero;

        [Tooltip("How tightly the fingers close, 0 to 1, for a rig that can be told. Nathan's " +
                 "cannot yet, so nothing reads this - it is here so an item can describe " +
                 "itself before the rig can listen.")]
        [SerializeField, Range(0f, 1f)] private float fingerCurl = 1f;

        [Header("Two-handed (not implemented)")]
        [Tooltip("Whether this item is meant to be held in two hands. Nothing implements " +
                 "two-hand IK yet; this is the extension point, and an item that sets it will " +
                 "still be held one-handed until that exists.")]
        [SerializeField] private bool twoHanded;

        [Tooltip("Where the second hand would go, in the item's own axes. See twoHanded.")]
        [SerializeField] private Vector3 secondaryGripPoint = Vector3.zero;

        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
        public float Backset => backset;
        public Vector3 ForwardAxis => forwardAxis;
        public float Length => length;

        public Vector3 AnchorOffset => anchorOffset;
        public float AimPitch => aimPitch;
        public float AimLag => aimLag;
        public float WalkBobDegrees => walkBobDegrees;
        public float WalkBobRate => walkBobRate;

        public float ElbowHintWeight => elbowHintWeight;
        public Vector3 WristHint => wristHint;
        public float FingerCurl => fingerCurl;

        public bool TwoHanded => twoHanded;
        public Vector3 SecondaryGripPoint => secondaryGripPoint;

        private static EquipmentGripProfile _default;

        /// <summary>
        /// The grip everything falls back to: the flashlight's, unchanged.
        ///
        /// <para>
        /// It is the flashlight's because it is the only grip in this project that has been
        /// tuned against a real character in a real hand. Every other item starts from a grip
        /// that is known to work and is adjusted from there, rather than from zero.
        /// </para>
        /// </summary>
        public static EquipmentGripProfile Default
        {
            get
            {
                if (_default != null)
                    return _default;

                _default = CreateInstance<EquipmentGripProfile>();
                _default.name = "GripProfile_Default";
                _default.hideFlags = HideFlags.HideAndDontSave;
                return _default;
            }
        }

        /// <summary>
        /// Builds a profile from a definition's deprecated HandLocalPosition/Rotation, so an
        /// item authored before grip profiles existed keeps the pose it was given.
        ///
        /// <para>
        /// The old values were a local transform on the hand anchor and these are offsets in
        /// the hand's measured axes, so this is a migration and not a conversion: it preserves
        /// intent, not arithmetic. Anything tuned this way should be re-checked in the
        /// equipment lab once, and then the deprecated fields on the definition zeroed.
        /// </para>
        /// </summary>
        public static EquipmentGripProfile FromLegacyHandPose(EquipmentDefinition definition)
        {
            var profile = CreateInstance<EquipmentGripProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;

            if (definition == null)
                return profile;

            profile.name = "GripProfile_Legacy_" + definition.Id;
            profile.positionOffset = definition.HandLocalPosition;
            profile.rotationOffset = definition.HandLocalRotation;
            return profile;
        }
    }
}
