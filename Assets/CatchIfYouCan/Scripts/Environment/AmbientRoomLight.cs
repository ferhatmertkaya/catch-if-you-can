using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// Marks a light as the room's RESIDUAL warmth rather than as one of its practicals.
    ///
    /// <para>
    /// Every non-directional light in a generated house used to mean the same thing: a bulb with
    /// a switch, which <see cref="HouseLightingDirector"/> dresses and then turns on or off from
    /// the mission seed, and which the generator hands to a light switch on the wall. That is
    /// right for a ceiling lamp and wrong for everything else a room can glow with - a grate, a
    /// nightlight, the spill under a door, the last warmth of a room somebody left an hour ago.
    /// </para>
    ///
    /// <para>
    /// The difference is not decoration. A room whose only light is a rolled practical is either
    /// lit or completely flat: with the practical off, nothing is left but the scene's ambient
    /// term, which is the same value in every corner of every room, and a room lit by one
    /// constant everywhere reads as an unlit blockout rather than as a dark room. What separates
    /// those two on screen is having any source at all with a position - a falloff, a side, a
    /// shadow the ambient does not have.
    /// </para>
    ///
    /// <para>
    /// So a light with this component is left alone: not dressed, not rolled, not owned by a
    /// switch. Whatever built it decided its colour, its reach and how it moves, and it stays
    /// that way. It is deliberately weak - it must not be a substitute for the torch, only for
    /// the difference between a dark room and a dead one.
    /// </para>
    ///
    /// <para>
    /// It carries no state and no behaviour. It exists to be asked about.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Catch If You Can/Ambient Room Light")]
    public sealed class AmbientRoomLight : MonoBehaviour
    {
        /// <summary>
        /// True when this light is scenery rather than a practical.
        ///
        /// <para>
        /// Asked instead of read directly so both callers - the lighting director and the
        /// generator's switch installer - decide the same way. Two <c>GetComponent</c> calls
        /// with two different fallbacks is how one of them ends up wiring the ember to the
        /// light switch on the wall.
        /// </para>
        /// </summary>
        public static bool Is(Light light)
        {
            return light != null && light.GetComponent<AmbientRoomLight>() != null;
        }
    }
}
