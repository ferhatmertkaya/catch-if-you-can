using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// What a room in the reference apartment is, for anything that needs to ask.
    ///
    /// <para>
    /// It carries the same <see cref="RoomCategory"/> a generated house carries, which is the
    /// whole point: ghost behaviour, prop spawning and objectives can read a hand-built
    /// apartment and a generated one through the same question, so none of them has to know
    /// which they are standing in.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApartmentRoom : MonoBehaviour
    {
        [SerializeField] private RoomCategory category;
        [SerializeField] private Rect footprint;
        [SerializeField] private float floorY;

        public RoomCategory Category => category;

        /// <summary>The room's floor rectangle in the apartment's own space, metres.</summary>
        public Rect Footprint => footprint;

        /// <summary>Which storey this is, 0 for the ground floor.</summary>
        public int Storey => Mathf.RoundToInt(floorY / ApartmentShell.StoreyPitch);

        /// <summary>The middle of the floor, in world space.</summary>
        public Vector3 Centre =>
            transform.TransformPoint(new Vector3(footprint.center.x, floorY, footprint.center.y));

        public void Configure(RoomCategory roomCategory, Rect rect, float y)
        {
            category = roomCategory;
            footprint = rect;
            floorY = y;
        }
    }
}
