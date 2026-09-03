using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Where a carried item sits and which way it points, as arithmetic and nothing else.
    ///
    /// <para>
    /// This is the half of "holding something" that is the same for every item: lay it on the
    /// fist the arm pose measured, or - when there is no character to measure - hang it off an
    /// anchor and aim it down the player's look with a lag and a walk bob. The torch worked this
    /// out first and it was written into the torch; every item after it would have needed the
    /// same twenty lines, and twenty lines copied four times is four places for the grip to
    /// drift apart.
    /// </para>
    ///
    /// <para>
    /// <b>It does not decide where the hand is.</b> That is
    /// <see cref="Player.PlayerBodyMotion"/>'s, which poses the arm and hands out the palm, the
    /// barrel axis and the palm normal it arrived at. Nothing here touches the pose; it only
    /// reads the result. Keeping the two apart is what lets the arm be re-tuned without every
    /// item in the game needing to be re-tuned with it.
    /// </para>
    ///
    /// <para>
    /// The convention for a carried transform is that its <b>local +Y is its long axis</b>, its
    /// origin is the grip rather than the middle, and it points away from the hand. That is why
    /// every rotation below ends in a quarter turn about X: <see cref="Quaternion.LookRotation"/>
    /// points local +Z along the direction it is given, and the extra turn puts +Y there instead.
    /// </para>
    /// </summary>
    public static class EquipmentPresentation
    {
        /// <summary>
        /// The measured path: the fist's own knuckles say which axis a cylinder held in it lies
        /// along and where the middle of the palm is, and the item is laid on that.
        ///
        /// <para>
        /// The offset is applied in the hand's own frame rather than the player's, so "towards
        /// the fingertips" keeps meaning that however the wrist is turned.
        /// </para>
        /// </summary>
        public static void SolveMeasuredHand(
            Vector3 palm, Vector3 barrel, Vector3 palmNormal,
            Vector3 handPositionOffset, Vector3 handRotationOffset, Vector3 gripRotationOffset,
            float backset,
            out Vector3 position, out Quaternion rotation)
        {
            SolveMeasuredHand(palm, barrel, palmNormal,
                              handPositionOffset, handRotationOffset, gripRotationOffset,
                              Vector3.zero, Vector3.zero, backset,
                              out position, out rotation);
        }

        /// <summary>
        /// The same solve, composing the character's own correction with the item's.
        ///
        /// <para>
        /// Two offsets, two owners. The character correction is a fact about whose hand this is
        /// - Nathan's fist is Nathan's fist whatever he is holding - and the item offset is a
        /// fact about the item, the same in anyone's hand. This is the only place the two are
        /// composed, which is what stops either of them turning into a database of the other.
        /// </para>
        ///
        /// <para>
        /// The character's correction is applied first, moving where "the palm" effectively is,
        /// and the item is then laid on that. Both being zero - which is what they are until a
        /// character is authored with a correction - produces exactly the arithmetic that was
        /// here before.
        /// </para>
        /// </summary>
        public static void SolveMeasuredHand(
            Vector3 palm, Vector3 barrel, Vector3 palmNormal,
            Vector3 handPositionOffset, Vector3 handRotationOffset, Vector3 gripRotationOffset,
            Vector3 characterPositionOffset, Vector3 characterRotationOffset,
            float backset,
            out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.LookRotation(barrel, palmNormal) *
                       Quaternion.Euler(90f, 0f, 0f) *
                       Quaternion.Euler(characterRotationOffset) *
                       Quaternion.Euler(gripRotationOffset) *
                       Quaternion.Euler(handRotationOffset);

            Vector3 towardsFingers = Vector3.Cross(palmNormal, barrel);
            Vector3 offset = characterPositionOffset + handPositionOffset;

            position = palm
                       - barrel * backset
                       + barrel * offset.x
                       + palmNormal * offset.y
                       + towardsFingers * offset.z;
        }

        /// <summary>
        /// Advances the lagged aim by one frame and returns where it now points.
        ///
        /// <para>
        /// Smoothing the direction rather than the angle keeps the swing even when the player
        /// spins right past 180 degrees, where an angle would unwind the long way round.
        /// </para>
        /// </summary>
        public static Vector3 AdvanceAim(Vector3 aim, ref Vector3 aimVelocity,
                                         Vector3 look, Vector3 right, float aimPitch, float aimLag)
        {
            Vector3 target = Quaternion.AngleAxis(aimPitch, right) * look;
            Vector3 next = Vector3.SmoothDamp(aim, target, ref aimVelocity, aimLag);
            return next.sqrMagnitude < 0.0001f ? target : next;
        }

        /// <summary>Advances the walk bob's phase. Speed, not time, drives it.</summary>
        public static float AdvanceBobPhase(float phase, float speed, float bobRate, float deltaTime)
        {
            return phase + deltaTime * speed * bobRate * Mathf.PI * 2f;
        }

        /// <summary>How far off the aim the bob is this frame, in degrees. Zero when standing.</summary>
        public static float BobDegrees(float phase, float speed, float bobDegrees)
        {
            return Mathf.Sin(phase) * bobDegrees * Mathf.Clamp01(speed * 0.5f);
        }

        /// <summary>
        /// The fallback path, for a player with no character visual to measure: hung off
        /// whatever anchor the inventory equipped the item to, and aimed down the given
        /// direction rather than down the wrist.
        ///
        /// <para>
        /// The item is slid back down its own long axis by <paramref name="backset"/> so the
        /// hand closes around the handle rather than around the very end of it. The origin is
        /// the tail, so this is the one number that decides how much of it sticks out of the
        /// front of the fist.
        /// </para>
        /// </summary>
        public static void SolveAimed(
            Vector3 anchorPosition, Vector3 aim,
            Vector3 right, Vector3 up, Vector3 forward,
            Vector3 anchorOffset, Vector3 gripRotationOffset, float backset,
            out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.LookRotation(aim, up) *
                       Quaternion.Euler(90f, 0f, 0f) *
                       Quaternion.Euler(gripRotationOffset);

            position = anchorPosition +
                       right * anchorOffset.x +
                       up * anchorOffset.y +
                       forward * anchorOffset.z -
                       aim * backset;
        }
    }
}
