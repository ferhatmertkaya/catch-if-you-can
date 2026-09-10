using UnityEngine;

namespace CatchIfYouCan.Character
{
    /// <summary>
    /// How one character's skeleton is named, and where equipment sits in its hand.
    ///
    /// <para>
    /// The procedural body layer finds every bone it drives by matching the end of a
    /// transform's name. Those suffixes were Renderpeople's, written as literals in the
    /// middle of the motion code, which meant a second character with a different rig would
    /// not fail - it would silently lose crouch, strafe, blink, the head follow and the
    /// flashlight arm, because every lookup is allowed to return null.
    /// </para>
    ///
    /// <para>
    /// The defaults below are exactly the literals that were in the code, so the profile
    /// that ships with Nathan reproduces his current behaviour to the letter. This is a
    /// naming contract only: no angle, limit or blend lives here, because moving the maths
    /// is a different change with a different risk.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "RigProfile_", menuName = "Catch If You Can/Character Rig Profile")]
    public sealed class CharacterRigProfile : ScriptableObject
    {
        [Header("Spine and head")]
        [SerializeField] private string spine01Suffix = "_spine_01";
        [SerializeField] private string spine02Suffix = "_spine_02";
        [SerializeField] private string spine03Suffix = "_spine_03";
        [SerializeField] private string neckSuffix = "_neck";
        [SerializeField] private string headSuffix = "_head";

        [Header("Legs")]
        [SerializeField] private string upperLegLeftSuffix = "_upperleg_l";
        [SerializeField] private string upperLegRightSuffix = "_upperleg_r";
        [SerializeField] private string lowerLegLeftSuffix = "_lowerleg_l";
        [SerializeField] private string lowerLegRightSuffix = "_lowerleg_r";
        [SerializeField] private string footLeftSuffix = "_foot_l";
        [SerializeField] private string footRightSuffix = "_foot_r";

        [Header("Face")]
        [SerializeField] private string eyelidLeftSuffix = "_eyelid_l";
        [SerializeField] private string eyelidRightSuffix = "_eyelid_r";

        [Header("Right arm")]
        [Tooltip("Renderpeople calls the collarbone the shoulder and the shoulder the upper " +
                 "arm. Another rig may not, which is the whole reason this is data.")]
        [SerializeField] private string clavicleRightSuffix = "_shoulder_r";
        [SerializeField] private string upperArmRightSuffix = "_upperarm_r";
        [SerializeField] private string lowerArmRightSuffix = "_lowerarm_r";
        [SerializeField] private string handRightSuffix = "_hand_r";

        [Header("Right hand digits")]
        [Tooltip("Four fingers, in the order the pose code curls them.")]
        [SerializeField] private string[] fingerDigits = { "index", "middle", "ring", "pinky" };
        [SerializeField] private string thumbDigit = "thumb";

        [Tooltip("The three joints of a digit, appended after the digit name.")]
        [SerializeField] private string[] fingerJointSuffixes = { "_01_r", "_02_r", "_03_r" };

        [Tooltip("What sits between the bone prefix and the digit name.")]
        [SerializeField] private string digitSeparator = "_";

        [Header("Held equipment")]
        [Tooltip("Per-character correction applied to anything held in the right hand. Zero " +
                 "for Nathan, whose item offsets are currently authored on the item itself.")]
        [SerializeField] private Vector3 gripPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 gripRotationOffset = Vector3.zero;

        public string Spine01 => spine01Suffix;
        public string Spine02 => spine02Suffix;
        public string Spine03 => spine03Suffix;
        public string Neck => neckSuffix;
        public string Head => headSuffix;
        public string UpperLegLeft => upperLegLeftSuffix;
        public string UpperLegRight => upperLegRightSuffix;
        public string LowerLegLeft => lowerLegLeftSuffix;
        public string LowerLegRight => lowerLegRightSuffix;
        public string FootLeft => footLeftSuffix;
        public string FootRight => footRightSuffix;
        public string EyelidLeft => eyelidLeftSuffix;
        public string EyelidRight => eyelidRightSuffix;
        public string ClavicleRight => clavicleRightSuffix;
        public string UpperArmRight => upperArmRightSuffix;
        public string LowerArmRight => lowerArmRightSuffix;
        public string HandRight => handRightSuffix;
        public string[] FingerDigits => fingerDigits;
        public string ThumbDigit => thumbDigit;
        public Vector3 GripPositionOffset => gripPositionOffset;
        public Vector3 GripRotationOffset => gripRotationOffset;

        /// <summary>The suffix of one joint of one digit, e.g. "_index_02_r".</summary>
        public string DigitJointSuffix(string digit, int jointIndex)
        {
            if (fingerJointSuffixes == null || jointIndex < 0 || jointIndex >= fingerJointSuffixes.Length)
                return null;

            return digitSeparator + digit + fingerJointSuffixes[jointIndex];
        }

        public int JointsPerDigit => fingerJointSuffixes != null ? fingerJointSuffixes.Length : 0;

        /// <summary>
        /// The profile used when a character has none assigned.
        ///
        /// Built in code rather than loaded, so the body layer behaves identically whether
        /// or not the Nathan asset has been authored yet. Cached because the bone binding
        /// asks for it once per spawn.
        /// </summary>
        private static CharacterRigProfile _default;

        public static CharacterRigProfile Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<CharacterRigProfile>();
                    _default.name = "RigProfile_BuiltInDefault";
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }

                return _default;
            }
        }
    }
}
