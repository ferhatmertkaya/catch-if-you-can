using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The spectral grid projector: a box that throws a field of green points across a room, and
    /// shows you the shape of something standing in it.
    ///
    /// <para>
    /// It had no runtime path. There was no case for spectral_grid in the runtime factory, so
    /// the id fell through to the unknown-id branch and the item a player would have been handed
    /// was a DEV_PLACEHOLDER box. It also derived from <see cref="EquipmentBase"/>, so even if
    /// it had been buildable it could not have been carried properly.
    /// </para>
    ///
    /// <para>
    /// This commit is the item, not the effect: held, stowed, dropped, picked up, switched on
    /// and off, with its power surviving every transition. Placement, the projection itself and
    /// the reveal arrive in their own commits on top of this.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Spectral Grid Projector")]
    public class SpectralGridProjector : PlaceableEquipmentBase
    {
        [Header("Projection")]
        [Tooltip("How far the point field reaches, in metres. A room, not a building.")]
        [SerializeField, Range(1f, 12f)] private float projectionRange = 6f;

        [Tooltip("Full angle of the cone it throws, in degrees.")]
        [SerializeField, Range(15f, 120f)] private float projectionAngle = 70f;

        private bool _powered;

        /// <summary>Whether the projector is switched on. Not whether it is being carried.</summary>
        public bool IsProjecting => _powered && IsActive;

        /// <summary>How far the field reaches, for the placement preview and the renderer.</summary>
        public float ProjectionRange => projectionRange;

        /// <summary>Full cone angle in degrees.</summary>
        public float ProjectionAngle => projectionAngle;

        /// <summary>
        /// Where the field comes out and which way it points. The carried transform's +Y is the
        /// item's length by the shared convention, so the head is at the far end of it.
        /// </summary>
        public Transform ProjectionOrigin => CarriedRoot != null ? CarriedRoot : transform;

        protected override float GetInterferenceMultiplier() => 0.45f;

        /// <summary>Flicking the switch does not wear the projector out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        protected override void OnUse() => SetPowered(!_powered);

        protected override void OnBatteryDepleted()
        {
            base.OnBatteryDepleted();
            SetPowered(false);
        }

        /// <summary>
        /// Switches the projector. The power state belongs to the item, so it survives being
        /// stowed, dropped, picked up and - once placement lands - being put on a wall.
        /// </summary>
        public void SetPowered(bool on)
        {
            bool next = on && (IsPowered || definition == null || definition.MaxBattery <= 0f);
            if (_powered == next)
                return;

            _powered = next;
            ApplyPower();
        }

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            ApplyPower();
        }

        /// <summary>
        /// The device is running when it is switched on and either in the hand or installed in
        /// the room. Stowed in a bag it stops, which is what keeps a projector from draining a
        /// battery and paying for a projection nobody can see.
        /// </summary>
        protected void ApplyPower()
        {
            bool running = _powered &&
                           (LifecycleState == EquipmentLifecycleState.Equipped ||
                            LifecycleState == EquipmentLifecycleState.Placed);

            SetDeviceActive(running);
            OnProjectionStateChanged(running);
        }

        /// <summary>
        /// Called whenever the projection starts or stops. Everything expensive hangs off this,
        /// so a projector that is off, stowed or in a bag renders nothing at all.
        /// </summary>
        protected virtual void OnProjectionStateChanged(bool running)
        {
            _projection?.SetRunning(running);
        }

        /// <summary>
        /// The field is a child of the device head, so it inherits the device's orientation.
        /// That is what makes a wall-mounted projector throw into the room without anything
        /// having to work out which way "away from the wall" is.
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            var head = new GameObject("ProjectorHead");
            head.transform.SetParent(CarriedRoot, false);
            head.transform.localPosition = new Vector3(0f, CarriedLength, 0f);

            _projection = SpectralGridProjection.Attach(head.transform);
            _projection?.Configure(projectionRange, projectionAngle);
            _projection?.SetRunning(false);
        }

        private SpectralGridProjection _projection;

        /// <summary>
        /// Installed on a wall or a floor, and left running if it was running.
        ///
        /// <para>
        /// Deliberately no ghost silhouette spawning any more. The old implementation
        /// instantiated a prefab at a random point inside a radius and called it a spectral
        /// reveal - a second object pretending to be the ghost, in a place the ghost was not. A
        /// reveal has to be the real ghost or it is a lie told to the player. The prefab field
        /// is gone rather than left dangling; nothing ever assigned it, so the path had never
        /// once run.
        /// </para>
        /// </summary>
        protected override void OnPlacedInWorld(in PlacementResult result)
        {
            // Placing does not switch it on, and does not switch it off. Whatever the player
            // set in the hand is what is installed - the power is the item's, and the item is
            // the same object it was a moment ago.
            ApplyPower();
        }
    }
}
