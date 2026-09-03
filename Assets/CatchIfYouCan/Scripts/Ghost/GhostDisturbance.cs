using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// A record that the ghost physically moved this object, and when.
    ///
    /// <para>
    /// PhysicalDisturbance was the one evidence type with no producer at all. It could not be
    /// added by giving some device a new button, because the evidence is not a reading - it is
    /// the claim that <em>something moved on its own</em>, and that claim is only true if
    /// something actually did. This is the thing that makes it true: the ghost's own throw
    /// leaves a mark on the object it threw, and the mark is what a camera can photograph.
    /// </para>
    ///
    /// <para>
    /// <b>Not every physical event.</b> A door the ghost swung is a noise and a scare; it is
    /// not a photograph of an object out of place. Only a throw marks, because a thrown object
    /// is the case where the evidence is still lying there afterwards for somebody to find.
    /// </para>
    ///
    /// <para>
    /// The mark expires. An object the ghost knocked over an hour ago is furniture, and a house
    /// where every disturbed object stays evidence forever is a house where a player who
    /// photographs the floor eventually wins.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Ghost Disturbance")]
    public sealed class GhostDisturbance : MonoBehaviour
    {
        [Tooltip("Seconds the disturbance stays photographable. After this the object is just " +
                 "an object that happens to be lying somewhere.")]
        [SerializeField, Min(1f)] private float witnessSeconds = 90f;

        private float _disturbedAt = float.NegativeInfinity;

        /// <summary>
        /// Every object the ghost has disturbed and that is still worth photographing. Asked by
        /// the camera when its shutter fires - a discrete act, not a per-frame one - and kept
        /// as a list so that act is not a scene sweep.
        /// </summary>
        private static readonly List<GhostDisturbance> Alive = new List<GhostDisturbance>();

        /// <summary>Read-only view. Do not hold onto it across frames.</summary>
        public static IReadOnlyList<GhostDisturbance> All => Alive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Alive.Clear();

        private void OnEnable()
        {
            if (!Alive.Contains(this))
                Alive.Add(this);
        }

        private void OnDisable() => Alive.Remove(this);

        /// <summary>Whether this is still recent enough to be evidence.</summary>
        public bool IsFresh => Time.time - _disturbedAt <= witnessSeconds;

        /// <summary>
        /// How strong a finding this is, 1 at the moment it happened and falling to nothing as
        /// the window closes. A photograph taken as it lands is a better photograph than one
        /// taken a minute later, and the validator is allowed to care.
        /// </summary>
        public float Freshness
        {
            get
            {
                float elapsed = Time.time - _disturbedAt;
                if (elapsed < 0f || elapsed > witnessSeconds)
                    return 0f;

                return 1f - elapsed / Mathf.Max(0.0001f, witnessSeconds);
            }
        }

        /// <summary>Marks this object as just moved by the ghost. Re-marking restarts the window.</summary>
        public void Mark()
        {
            _disturbedAt = Time.time;
        }

        /// <summary>
        /// Marks an object, adding the component if it does not have one. Called by the ghost's
        /// interaction brain at the moment it throws something.
        /// </summary>
        public static GhostDisturbance MarkObject(GameObject target)
        {
            if (target == null)
                return null;

            var mark = target.GetComponent<GhostDisturbance>();
            if (mark == null)
                mark = target.AddComponent<GhostDisturbance>();

            mark.Mark();
            return mark;
        }
    }
}
