using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// Everything another machine needs to draw the ghost correctly, and nothing else.
    ///
    /// <para>
    /// <b>The point of this struct is what is not in it.</b> Not the path, not the
    /// destination, not the roam target, not what it heard, not who it is hunting, not how
    /// long the hunt has left, not the agent's velocity or its remaining distance. Those are
    /// the ghost's reasoning, and reasoning is exactly what <see cref="GhostState"/> already
    /// summarises. A client that knew the ghost's target would be a client that could draw an
    /// arrow to it.
    /// </para>
    ///
    /// <para>
    /// The animation is rebuilt rather than sent. <see cref="GhostRigController"/> chooses a
    /// clip from <see cref="GhostState"/> and nothing else, so one enum reproduces the whole
    /// performance on every machine - the same argument that keeps a hundred bone transforms
    /// off the wire for a player.
    /// </para>
    ///
    /// <para>
    /// The spectral reveal is deliberately absent. How lit the ghost is by a grid projector
    /// is a fact about the viewer's own equipment, not about the ghost: two players pointing
    /// two projectors should each see what their own projector reveals, and replicating one
    /// answer would show both of them somebody else's.
    /// </para>
    ///
    /// <para>
    /// Transport-neutral by construction - plain fields, no attributes, no serializer, no
    /// dependency on any networking package.
    /// </para>
    /// </summary>
    public struct GhostPresentationState
    {
        /// <summary>Where it is. The host's answer; a client never computes its own.</summary>
        public Vector3 Position;

        /// <summary>Which way it faces, degrees. Yaw only: a ghost does not pitch or roll.</summary>
        public float Yaw;

        /// <summary>
        /// What it is doing, as the one enum the whole presentation is built from.
        /// </summary>
        public GhostState State;

        /// <summary>
        /// Whether it is manifested. Separate from <see cref="State"/> because a manifestation
        /// can be refused - the roll happens on the host, and a client that inferred visibility
        /// from Manifesting would show a ghost that the host decided not to show.
        /// </summary>
        public bool IsVisible;

        /// <summary>Reads a live ghost, for sending. Host side.</summary>
        public static GhostPresentationState Capture(GhostController ghost)
        {
            var state = new GhostPresentationState();
            if (ghost == null)
                return state;

            state.Position = ghost.transform.position;
            state.Yaw = ghost.transform.eulerAngles.y;
            state.State = ghost.CurrentState;
            state.IsVisible = ghost.IsManifestationVisible;
            return state;
        }

        /// <summary>
        /// Between two received frames, for smoothing a ghost between network ticks.
        ///
        /// <para>
        /// Yaw goes the short way round, which is the difference between a ghost turning from
        /// 350 degrees to 10 and one spinning the other way through the whole circle. The
        /// state and the visibility take the newer value rather than blending: there is no
        /// halfway between hunting and roaming, and a half-manifested ghost is not a thing the
        /// host ever decided.
        /// </para>
        /// </summary>
        public static GhostPresentationState Lerp(in GhostPresentationState a,
                                                  in GhostPresentationState b, float t)
        {
            t = Mathf.Clamp01(t);

            return new GhostPresentationState
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t),
                State = t < 0.5f ? a.State : b.State,
                IsVisible = t < 0.5f ? a.IsVisible : b.IsVisible,
            };
        }
    }
}
