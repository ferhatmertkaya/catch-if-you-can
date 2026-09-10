using System.Collections.Generic;
using CatchIfYouCan.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// Carries thrown objects through the portal, the same way the player is carried.
    ///
    /// <para>
    /// A thrown EMF reader already flies: <c>HeldEquipmentBase</c> gives a dropped item a
    /// Rigidbody and throws it with an impulse. What it could not do was ARRIVE. The portal is a
    /// hole in one scene showing a second scene loaded beside it, so an item thrown through the
    /// opening simply landed on the lobby floor behind where the picture was - and was then
    /// destroyed with the lobby, because <c>Unequip</c> unparents to world space and a dropped
    /// item is a scene ROOT.
    /// </para>
    ///
    /// <para>
    /// <b>The same object, moved.</b> Nothing is destroyed and rebuilt, so battery, tier, on/off
    /// state, ownership and every other runtime value survive by never being touched: they live
    /// on components of a GameObject that changes scene and nothing else. That is also why this
    /// does not re-add anything to an inventory - a dropped item is world equipment, and
    /// changing scene is not picking it up.
    /// </para>
    ///
    /// <para>
    /// <b>Symmetric by construction.</b> The mapping is a matrix pair, so running it the other
    /// way is the same code with the two transforms swapped. Reverse travel is only meaningful
    /// while BOTH scenes are loaded, which today is the window between the portal opening and
    /// the lobby unloading; nothing here assumes a direction.
    /// </para>
    /// </summary>
    public sealed class PortalObjectTransfer : MonoBehaviour
    {
        private const string LogTag = "[CIYC][Portal][ObjectTransfer] ";

        /// <summary>Stops a body that straddles the plane from bouncing between scenes.</summary>
        private const float CooldownSeconds = 0.35f;

        private Transform _sourcePlane;
        private Transform _destinationAnchor;
        private Vector2 _aperture;
        private bool _armed;

        /// <summary>Everything this portal has already sent through, for the unload check.</summary>
        private readonly List<GameObject> _transferred = new List<GameObject>();

        /// <summary>What has crossed, so the lobby can refuse to unload on top of it.</summary>
        public IReadOnlyList<GameObject> Transferred => _transferred;

        /// <summary>
        /// Hands over the pair. Until this is called nothing is moved, so an unprepared or
        /// closed portal is inert rather than a hole objects fall through into nowhere.
        /// </summary>
        public void Arm(Transform sourcePlane, Transform destinationAnchor, Vector2 aperture)
        {
            _sourcePlane = sourcePlane;
            _destinationAnchor = destinationAnchor;
            _aperture = aperture;
            _armed = sourcePlane != null && destinationAnchor != null;
        }

        /// <summary>Stops carrying anything. The objects already through stay through.</summary>
        public void Disarm() => _armed = false;

        // FixedUpdate, not Update: these are physics bodies, and sampling their position on the
        // render frame would miss a fast throw between two physics steps.
        private void FixedUpdate()
        {
            if (!_armed || _sourcePlane == null || _destinationAnchor == null)
                return;

            Vector3 planePoint = _sourcePlane.position;
            Vector3 planeNormal = _sourcePlane.forward;

            // This portal only carries what is on ITS side of the pair. The registry is global,
            // so without this an object that has already gone through would keep being measured
            // against the plane it left - and its position in the destination world has nothing
            // to do with that plane, so sooner or later it reads as a second crossing and is
            // carried again, from a place it is not, to a place it already is.
            //
            // It is also what makes the system symmetric: an instance armed the other way round
            // picks up the objects on the other side by the same test, with no direction encoded
            // anywhere.
            Scene sourceScene = _sourcePlane.gameObject.scene;

            IReadOnlyList<PortalTransferable> all = PortalTransferable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                PortalTransferable item = all[i];
                if (item == null || item.gameObject.scene != sourceScene)
                    continue;

                float side = Vector3.Dot(item.transform.position - planePoint, planeNormal);

                if (!item.HasPreviousSide)
                {
                    item.PreviousSide = side;
                    item.PreviousPosition = item.transform.position;
                    item.HasPreviousSide = true;
                    continue;
                }

                float previous = item.PreviousSide;
                Vector3 previousPosition = item.PreviousPosition;
                item.PreviousSide = side;
                item.PreviousPosition = item.transform.position;

                // A CROSSING, not an overlap: the sign has to actually change. An object resting
                // in the opening reads the same side every step and is never carried.
                if (previous <= 0f || side > 0f)
                    continue;

                if (Time.time - item.LastTransferTime < CooldownSeconds)
                {
                    CIYCLog.Info(LogTag + "duplicate-cross suppressed object=" + item.name);
                    continue;
                }

                if (!InsideAperture(previousPosition, item.transform.position, previous, side))
                {
                    CIYCLog.Info(LogTag + "rejected object=" + item.name +
                                 " reason=crossed the plane outside the opening");
                    continue;
                }

                Transfer(item);
            }
        }

        /// <summary>
        /// Whether the point where the object actually crossed lies inside the hole.
        ///
        /// <para>
        /// The plane is infinite; the opening is not. Without this an item thrown at the wall two
        /// metres to the left of the portal would cross the same plane and be carried through
        /// solid plaster.
        /// </para>
        /// </summary>
        private bool InsideAperture(Vector3 from, Vector3 to, float previous, float side)
        {
            // Where the segment between the two samples actually met the plane. The signed
            // distances are linear along it, so the crossing fraction is exact rather than
            // approximated by projecting the end point back onto the plane.
            float travel = previous - side;
            float t = Mathf.Abs(travel) < 1e-6f ? 0f : Mathf.Clamp01(previous / travel);
            Vector3 hit = Vector3.Lerp(from, to, t);

            Vector3 local = _sourcePlane.InverseTransformPoint(hit);
            return Mathf.Abs(local.x) <= _aperture.x * 0.5f &&
                   Mathf.Abs(local.y) <= _aperture.y * 0.5f;
        }

        /// <summary>
        /// Moves one object into the destination world, pose and momentum intact.
        /// </summary>
        private void Transfer(PortalTransferable item)
        {
            GameObject go = item.gameObject;
            Scene from = go.scene;
            Scene to = _destinationAnchor.gameObject.scene;

            if (go.transform.parent != null)
            {
                CIYCLog.Warn(LogTag + "rejected object=" + go.name +
                             " reason=not a scene root, so it cannot change scene");
                return;
            }

            if (!to.IsValid() || !to.isLoaded)
            {
                CIYCLog.Warn(LogTag + "rejected object=" + go.name +
                             " reason=destination scene is not loaded");
                return;
            }

            // The SAME matrix the player and the portal camera are mapped by, so a thrown object
            // and the person who threw it arrive in the same place by the same rule.
            Matrix4x4 flip = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            Matrix4x4 through = _destinationAnchor.localToWorldMatrix *
                                flip *
                                _sourcePlane.worldToLocalMatrix;

            Vector3 velocity = Vector3.zero;
            Vector3 spin = Vector3.zero;
            Rigidbody body = item.Body;
            if (body != null)
            {
                velocity = body.linearVelocity;
                spin = body.angularVelocity;
            }

            Vector3 position = through.MultiplyPoint3x4(go.transform.position);

            // The mapping's own rotation, read off its basis columns - the same idiom
            // PortalSurface uses to pose the camera, rather than a second way of asking.
            Vector3 mappedForward = through.GetColumn(2);
            Vector3 mappedUp = through.GetColumn(1);
            Quaternion rotation = mappedForward.sqrMagnitude > 1e-8f && mappedUp.sqrMagnitude > 1e-8f
                ? Quaternion.LookRotation(mappedForward, mappedUp) * go.transform.rotation
                : go.transform.rotation;

            SceneManager.MoveGameObjectToScene(go, to);
            go.transform.SetPositionAndRotation(position, rotation);

            if (body != null)
            {
                // Rotated, not zeroed. A thrown object has to keep going on the far side, and
                // the direction it was travelling in is expressed in the source portal's frame.
                body.linearVelocity = through.MultiplyVector(velocity);
                body.angularVelocity = through.MultiplyVector(spin);
            }

            Physics.SyncTransforms();

            item.LastTransferTime = Time.time;
            item.HasPreviousSide = false;
            _transferred.Add(go);

            bool arrived = go.scene == to;
            string line = LogTag + "object=" + go.name + " id=" + go.GetEntityId() +
                          " from=" + from.name + " to=" + go.scene.name +
                          " pos=" + position.ToString("F2") +
                          " velocity=" + (body != null ? body.linearVelocity.ToString("F2") : "<none>") +
                          " result=" + (arrived ? "SUCCESS" : "FAILED");

            if (arrived)
                CIYCLog.Info(line);
            else
                CIYCLog.Error(line + " - the object is still owned by the source scene and will " +
                              "be destroyed with it.");
        }

        /// <summary>
        /// Counts what has crossed and how much of it is still owned by the scene about to be
        /// unloaded. Called before the lobby goes, because a transferred object still living in
        /// the lobby is one that is about to silently cease to exist.
        /// </summary>
        public int CountStillOwnedBy(Scene scene)
        {
            int stranded = 0;
            for (int i = 0; i < _transferred.Count; i++)
            {
                GameObject go = _transferred[i];
                if (go != null && go.scene == scene)
                    stranded++;
            }

            string line = LogTag + "preLobbyUnload transferred=" + _transferred.Count +
                          " stillOwnedByLobby=" + stranded;
            if (stranded > 0)
                CIYCLog.Error(line + " - those objects are about to be destroyed with the scene.");
            else
                CIYCLog.Info(line);

            return stranded;
        }
    }
}
