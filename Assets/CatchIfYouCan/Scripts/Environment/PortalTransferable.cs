using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// Marks one loose physics object as allowed through the portal.
    ///
    /// <para>
    /// <b>A marker, not a filter over every Rigidbody.</b> Sweeping the scene for bodies near the
    /// opening would eventually pick up a piece of lobby furniture, a particle, or the portal's
    /// own wall-aperture colliders, and the first time it did the failure would be an object
    /// silently teleported into a house. An object is transferable because something said so.
    /// </para>
    ///
    /// <para>
    /// Added by <c>HeldEquipmentBase</c> at the moment an item is thrown - which is exactly the
    /// moment it becomes a free-flying object with a body of its own - and removed when it is
    /// picked back up. Nothing else adds it today; anything that wants to may.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalTransferable : MonoBehaviour
    {
        private static readonly List<PortalTransferable> Live = new List<PortalTransferable>();

        /// <summary>Everything currently eligible. Never null, and never contains a dead entry.</summary>
        public static IReadOnlyList<PortalTransferable> All => Live;

        /// <summary>Signed distance to the portal plane on the previous check, per object.</summary>
        public float PreviousSide { get; set; }

        /// <summary>False until a first side has been sampled; a crossing needs two.</summary>
        public bool HasPreviousSide { get; set; }

        /// <summary>
        /// Where the object was on the previous step.
        ///
        /// <para>
        /// Kept because the crossing POINT is what has to be inside the opening, and that point
        /// is on the segment between the two samples. A fast throw moves a long way in one
        /// physics step; using only the current position would test a place the object was never
        /// at when it met the plane.
        /// </para>
        /// </summary>
        public Vector3 PreviousPosition { get; set; }

        /// <summary>Time of the last migration, for the anti-ping-pong cooldown.</summary>
        public float LastTransferTime { get; set; } = float.NegativeInfinity;

        /// <summary>The body that carries the momentum through. May be null on a static prop.</summary>
        public Rigidbody Body { get; private set; }

        private void Awake() => Body = GetComponent<Rigidbody>();

        private void OnEnable()
        {
            if (Body == null)
                Body = GetComponent<Rigidbody>();

            if (!Live.Contains(this))
                Live.Add(this);
        }

        private void OnDisable() => Live.Remove(this);

        /// <summary>
        /// Makes an object transferable, or returns the marker it already had.
        ///
        /// <para>
        /// Idempotent on purpose: an item thrown, picked up and thrown again must end with one
        /// marker, not three, and <see cref="DisallowMultipleComponent"/> turns a second
        /// AddComponent into a console error rather than a silent no-op.
        /// </para>
        /// </summary>
        public static PortalTransferable Mark(GameObject target)
        {
            if (target == null)
                return null;

            PortalTransferable existing = target.GetComponent<PortalTransferable>();
            if (existing != null)
                return existing;

            return target.AddComponent<PortalTransferable>();
        }

        /// <summary>Takes the mark off - an item back in a hand is not a loose object.</summary>
        public static void Unmark(GameObject target)
        {
            if (target == null)
                return;

            PortalTransferable existing = target.GetComponent<PortalTransferable>();
            if (existing != null)
                Destroy(existing);
        }
    }
}
