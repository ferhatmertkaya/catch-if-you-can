using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// How many extra renders of the world the lobby is allowed in one frame.
    ///
    /// <para>
    /// The lobby can hold two things that each render the scene a second time: the mirror and
    /// the portal. Both already cull themselves by facing, distance and frustum, and separately
    /// that is correct - but neither knows the other exists, so a player standing where both are
    /// on screen pays for three full renders of the room on a phone. Nothing was coordinating
    /// that, and "each one is individually reasonable" is exactly how a frame budget goes.
    /// </para>
    ///
    /// <para>
    /// This is the arbiter, and deliberately nothing more. It does not decide whether a view is
    /// visible - each view still owns that, because only it knows its own geometry - it decides
    /// which of the views that already want to render this frame get to. A view that is the only
    /// one asking always renders, at every quality level: the cost this exists to bound is two
    /// secondary renders at once, not one.
    /// </para>
    ///
    /// <para>
    /// Round-robin by frame and slot, so with a budget of one and two claimants each renders on
    /// alternate frames rather than one of them winning forever. No allocation, no scene sweep,
    /// no per-frame garbage: an int per claimant and three static ints.
    /// </para>
    /// </summary>
    public static class SecondaryViewBudget
    {
        private static int _nextSlot;
        private static int _frame = -1;

        // Intent is counted for the CURRENT frame and read from the PREVIOUS one. A view has to
        // ask before it can be counted, so within one frame the count is always incomplete;
        // using last frame's total costs one frame of lag on a claimant appearing and buys a
        // decision that does not depend on which view happens to run its LateUpdate first.
        private static int _intentThisFrame;
        private static int _intentLastFrame;
        private static int _grantedThisFrame;

        /// <summary>Claims a stable slot. Called once, when a view builds itself.</summary>
        public static int Reserve() => _nextSlot++;

        /// <summary>
        /// How many secondary views may render in one frame on this device.
        ///
        /// <para>
        /// One at the lowest quality level and unbounded at the top, from the same quality
        /// fraction the buffer sizes and particle rates use. There is no separate tier enum
        /// here for the same reason there is none there: a second notion of how much machine
        /// this is can disagree with the first.
        /// </para>
        /// </summary>
        public static int MaxPerFrame =>
            PortalStyle.QualityFraction01() < 0.34f ? 1 : int.MaxValue;

        /// <summary>
        /// Whether the view in <paramref name="slot"/> may render this frame.
        ///
        /// <para>
        /// Call it once per frame, and only after the view has decided it is visible - asking
        /// while off screen would spend intent that a visible view needs.
        /// </para>
        /// </summary>
        public static bool MayRender(int slot)
        {
            int frame = Time.frameCount;
            if (frame != _frame)
            {
                _frame = frame;
                _intentLastFrame = _intentThisFrame;
                _intentThisFrame = 0;
                _grantedThisFrame = 0;
            }

            _intentThisFrame++;

            int budget = MaxPerFrame;
            int contenders = Mathf.Max(1, _intentLastFrame);

            // Nobody to share with, or room for everyone.
            if (contenders <= budget || budget == int.MaxValue)
                return true;

            // Whose turn it is. Shifting the window by the frame number rotates the grant
            // through the slots, so no view is starved and no view has to remember anything.
            bool mine = (frame + slot) % contenders < budget;
            if (!mine || _grantedThisFrame >= budget)
                return false;

            _grantedThisFrame++;
            return true;
        }

        /// <summary>Test and diagnostic seam. Resets the arbiter to its initial state.</summary>
        public static void ResetForTests()
        {
            _nextSlot = 0;
            _frame = -1;
            _intentThisFrame = 0;
            _intentLastFrame = 0;
            _grantedThisFrame = 0;
        }
    }
}
