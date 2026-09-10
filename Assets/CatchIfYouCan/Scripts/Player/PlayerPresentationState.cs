using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Everything another machine needs to draw this player correctly, and nothing else.
    ///
    /// <para>
    /// <b>The point of this struct is what is not in it.</b> Not the animator's state, not the
    /// hand target, not the elbow hint, not a single bone or finger transform. The remote
    /// player is the same rig running the same <see cref="PlayerBodyMotion"/>, so it can
    /// reconstruct the whole pose - the arm holding the torch, the head turn, the crouch, the
    /// walk - from these few numbers. Sending the pose instead of the inputs to the pose would
    /// be sending a hundred transforms a tick to say something eight bytes already say.
    /// </para>
    ///
    /// <para>
    /// Every field is one a local player already publishes on <see cref="PlayerController"/>
    /// and <see cref="PlayerLook"/>. That is deliberate: a remote player is driven by writing
    /// these onto the same properties the local one computes, so
    /// <see cref="PlayerBodyMotion"/> cannot tell the difference and its mathematics never had
    /// to change.
    /// </para>
    ///
    /// <para>
    /// Transport-neutral by construction - plain fields, no attributes, no serializer, no
    /// dependency on any networking package. What writes it to a wire is not this type's
    /// business, and choosing or replacing that does not invalidate it.
    /// </para>
    /// </summary>
    public struct PlayerPresentationState
    {
        /// <summary>Where the body faces, degrees. The root's yaw.</summary>
        public float Yaw;

        /// <summary>Where the head looks, degrees. Not the body's.</summary>
        public float Pitch;

        /// <summary>
        /// The movement stick in the player's own axes: x strafe, y forward. What tells a
        /// remote body that a sideways walk is sideways rather than a turn.
        /// </summary>
        public Vector2 MoveInput;

        /// <summary>Metres per second along the ground. Drives the walk/run blend.</summary>
        public float Speed;

        /// <summary>How far into the crouch, 0 standing and 1 fully down.</summary>
        public float Crouch01;

        public bool IsSprinting;
        public bool IsCrouching;
        public bool IsGrounded;

        /// <summary>Reads the state of a live local player, for sending.</summary>
        public static PlayerPresentationState Capture(PlayerController controller, PlayerLook look)
        {
            var state = new PlayerPresentationState();
            if (controller == null)
                return state;

            state.Yaw = controller.transform.eulerAngles.y;
            state.Pitch = look != null ? look.Pitch : 0f;
            state.MoveInput = controller.LocalMoveInput;
            state.Speed = controller.CurrentSpeed;
            state.Crouch01 = controller.CrouchAmount01;
            state.IsSprinting = controller.IsSprinting;
            state.IsCrouching = controller.IsCrouching;
            state.IsGrounded = controller.IsGrounded;
            return state;
        }

        /// <summary>
        /// Between two received states, for smoothing a remote body between network ticks.
        ///
        /// <para>
        /// Angles are interpolated the short way round, which is the difference between a head
        /// turning from 350 to 10 degrees and a head spinning the other way through the whole
        /// circle. The booleans take the newer value rather than blending, because there is no
        /// halfway between crouching and not.
        /// </para>
        /// </summary>
        public static PlayerPresentationState Lerp(in PlayerPresentationState a,
                                                   in PlayerPresentationState b, float t)
        {
            t = Mathf.Clamp01(t);

            return new PlayerPresentationState
            {
                Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t),
                Pitch = Mathf.LerpAngle(a.Pitch, b.Pitch, t),
                MoveInput = Vector2.Lerp(a.MoveInput, b.MoveInput, t),
                Speed = Mathf.Lerp(a.Speed, b.Speed, t),
                Crouch01 = Mathf.Lerp(a.Crouch01, b.Crouch01, t),
                IsSprinting = t < 0.5f ? a.IsSprinting : b.IsSprinting,
                IsCrouching = t < 0.5f ? a.IsCrouching : b.IsCrouching,
                IsGrounded = t < 0.5f ? a.IsGrounded : b.IsGrounded,
            };
        }
    }
}
