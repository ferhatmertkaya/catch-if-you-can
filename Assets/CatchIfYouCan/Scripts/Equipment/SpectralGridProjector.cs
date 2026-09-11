using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
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

        [Tooltip("Seconds between checks for something standing in the field. A body does not " +
                 "cross a room between ticks, and the reveal holds itself up for longer than " +
                 "this so a ghost still in the cone stays lit between scans.")]
        [SerializeField, Min(0.05f)] private float scanInterval = 0.1f;

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

        /// <summary>Whether it is throwing a field.</summary>
        public override string HudReadout => IsProjecting ? "PROJECTING" : "OFF";

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

        /// <summary>
        /// Aiming is the device's RESTING state in the hand, not a mode the player switches on.
        ///
        /// <para>
        /// The AIM action existed and was reachable only from a HUD button that is not built
        /// on this screen, and the F key behind it raises a signal nothing in the project
        /// reads - so placement could not be started at all. Rather than adding a key for a
        /// step that carries no decision, the step is removed: a projector in the hand is a
        /// projector looking for a wall, and the preview appears when one is in front of it.
        /// </para>
        ///
        /// <para>
        /// Driven from the lifecycle rather than from a per-frame check, so holstering,
        /// dropping and placing all end the aim through the transitions they already make.
        /// </para>
        /// </summary>
        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            ApplyPower();

            if (to == EquipmentLifecycleState.Equipped)
            {
                var began = TryBeginPlacement();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Core.CIYCLog.Info(began.Ok
                    ? "[CIYC][DOTS] HELD_AUTO_PLACEMENT_ACTIVE"
                    : "[CIYC][DOTS] [BLOCKED] AUTO_PLACEMENT reason=" + began.Status);
#endif
            }
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

            // The lens rides the SAME signal, deliberately. `running` already folds in the
            // battery, the power switch and the lifecycle - stowed in a bag, packed up off a
            // wall, or run flat all arrive here as false - so a second state variable for the
            // glow could only ever disagree with this one, and the disagreement would show as a
            // device that is off and still lit.
            _lens?.SetPowered(running);
        }

        /// <summary>
        /// The field is a child of the device head, so it inherits the device's orientation.
        /// That is what makes a wall-mounted projector throw into the room without anything
        /// having to work out which way "away from the wall" is.
        /// </summary>
        /// <summary>
        /// A floor device as well as a wall device - and on the floor it LIES DOWN.
        ///
        /// <para>
        /// It was wall-only, and the reason written down for that was true at the time: the
        /// field was a 70-degree cone along the device's +Y, so a projector standing on a floor
        /// pointed its lens at the ceiling and lit nothing the player walks through. The field
        /// is a SPHERE now. Which way the device points no longer changes what is lit, so the
        /// restriction was protecting against a shape the effect no longer has - a rule that
        /// outlives its reason is just a rule.
        /// </para>
        ///
        /// <para>
        /// What still matters is that it lies rather than stands: see
        /// <see cref="OrientForPlacement"/>. Declared here on the DEVICE rather than left to an
        /// Inspector value somebody has to set on every instance, and the placement query reads
        /// this - an override nothing reads changes nothing.
        /// </para>
        /// </summary>
        protected override PlacementSurface? SurfaceOverride => PlacementSurface.FloorAndWall;

        /// <summary>
        /// Lays it down on a floor instead of standing it on end.
        ///
        /// <para>
        /// The base puts a floor item's working axis along the floor's normal, which stands a
        /// 25 cm tube upright on its end - correct for a camera on a tripod and wrong for this.
        /// The SAME quarter turn the wall case already uses does the job: the placement query
        /// builds a floor rotation whose +Z is the player's own facing flattened into the floor
        /// plane and whose +Y is the normal, so turning it about X maps the device's +Y onto
        /// that flattened forward. It ends up horizontal, pointing away from whoever set it
        /// down.
        /// </para>
        ///
        /// <para>
        /// The lit field does not care - it is a sphere about the lens. The EVIDENCE cone does:
        /// <see cref="FieldStrengthAt"/> tests 70 degrees along this same +Y, so lying down
        /// sweeps it horizontally across the room at about ankle height instead of at the
        /// ceiling, which is the same kind of coverage a wall mounting gives and a good deal
        /// more than standing on end gave.
        /// </para>
        /// </summary>
        protected override Quaternion OrientForPlacement(in PlacementResult result)
        {
            return result.Rotation * Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>
        /// The head that carries the lens, and the projection hanging off it.
        ///
        /// <para>
        /// <b>Adopt before building.</b> <see cref="HeldEquipmentBase.BuildCarried"/> takes over
        /// the visual a clone already carries rather than making a second one, and everything
        /// below the visual has to follow the same rule or the fix is only half done. This
        /// method did not: it ran <c>new GameObject("ProjectorHead")</c> unconditionally, so
        /// every clone - and every projector in the game is a clone of one live template -
        /// arrived with the template's ProjectorHead, projection and volume ALREADY under its
        /// adopted visual, and then got a second set built beside them. The stale pair is inert
        /// (a projection that was never switched on carries a disabled renderer across the copy)
        /// which is precisely why it never announced itself: nothing was drawn twice, nothing
        /// errored, and the device simply carried a dead twin of its own effect. Mistake 30, one
        /// level below where it was fixed.
        /// </para>
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            // Found by COMPONENT, which is the one thing Instantiate brings across - a name
            // survives too, but a name is not identity and this project has been bitten by
            // trusting one before.
            _projection = GetComponentInChildren<SpectralGridProjection>(true);
            if (_projection != null)
            {
                Transform host = _projection.transform;
                _head = host.parent != null ? host.parent : host;
            }
            else
            {
                var head = new GameObject("ProjectorHead");
                head.transform.SetParent(CarriedRoot, false);
                head.transform.localPosition = new Vector3(0f, CarriedLength, 0f);

                _head = head.transform;
                _projection = SpectralGridProjection.Attach(_head);
            }

            _projection?.Configure(projectionRange, projectionAngle);
            _projection?.SetRunning(false);

            // On the visual rather than on the head: the emission mask lives in the model's own
            // material, so the thing that lights up has to be the thing that wears it.
            _lens = SpectralGridLens.Attach(CarriedRoot);
            _lens?.SetPowered(false);
        }

        [Header("Development")]
        [Tooltip("Suspends this device's battery drain so a visual can be looked at for longer " +
                 "than it lasts. The definition gives it 75 units at 0.85 per second, which is " +
                 "88 seconds of running - long enough to play with and far too short to step " +
                 "through six diagnostic stages, which is how a bisect ran out of power halfway. " +
                 "Read only in the editor and in a development build; a shipping build drains " +
                 "normally whatever this says, and the battery architecture is untouched.")]
        [SerializeField] private bool developmentInfiniteBattery;

        /// <summary>
        /// The battery, minus the development escape hatch.
        ///
        /// <para>
        /// Fenced rather than removed, and fenced on THIS device rather than in
        /// <see cref="EquipmentBase"/>: the drain is a gameplay decision that belongs to every
        /// other item as much as this one, and 88 seconds may well be the right number for a
        /// haunting. What it is not is enough time to look at a projection, change a setting and
        /// look again. Whether the gameplay duration should change is a design call and is left
        /// alone here.
        /// </para>
        /// </summary>
        protected override void DrainBattery()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentInfiniteBattery)
                return;
#endif
            base.DrainBattery();
        }

        private SpectralGridProjection _projection;
        private SpectralGridLens _lens;
        private Transform _head;
        private float _scanTimer;

        /// <summary>
        /// Looks for the ghost in the field, and shows its shape when one is standing in it.
        ///
        /// <para>
        /// Throttled rather than per frame: a body does not cross a room between ticks, and the
        /// reveal holds itself up for longer than one interval so a ghost still in the cone
        /// stays lit between scans.
        /// </para>
        /// </summary>
        protected override void TickEquipped(float deltaTime)
        {
            // The placement preview, when one is up.
            base.TickEquipped(deltaTime);

            if (!IsActive || _head == null)
                return;

            _scanTimer -= deltaTime;
            if (_scanTimer > 0f)
                return;

            _scanTimer = scanInterval;
            ScanForGhost();
        }

        private void ScanForGhost()
        {
            // From the registry, not a scene sweep.
            var ghost = GhostController.Active;
            if (ghost == null)
                return;

            float strength = FieldStrengthAt(ghost.transform.position);
            if (strength <= 0f)
                return;

            // Only an entity that actually leaves a spectral signature can be shown by one.
            // A ghost without SpectralGrid in its profile walks through the field and nothing
            // happens, which is what makes an empty field informative.
            var definition = ghost.Definition;
            if (definition == null || !definition.HasEvidence(EvidenceType.SpectralGrid))
                return;

            var reveal = GhostSpectralReveal.Ensure(ghost);
            reveal?.Illuminate(_head, projectionRange, projectionAngle * 0.5f * Mathf.Deg2Rad);

            OnGhostRevealed(ghost, strength);
        }

        /// <summary>
        /// How strongly the field falls on a world point, 0 to 1, where 0 means outside it.
        ///
        /// <para>
        /// <b>This is NOT the shape the shader draws, and the difference is deliberate.</b> The
        /// projection is a full sphere about the lens; this test is a 70-degree cone along the
        /// device head's +Y. So the EVIDENCE volume is narrower than the LIT volume, and a ghost
        /// standing in dots on a side wall is visible to the player without being counted. That
        /// is an evidence-contract decision rather than a rendering one - widening it changes
        /// what this device can prove - so it is written down here rather than quietly matched
        /// to whatever the shader happens to light. See <see cref="Configure"/>.
        /// </para>
        ///
        /// <para>
        /// The falloff is linear in distance and towards the rim. The shader's is neither: it
        /// holds full strength across most of the range and then rolls off over the last
        /// stretch. A measurement should not inherit a rendering curve, so these do not track
        /// each other and are not meant to.
        /// </para>
        /// </summary>
        public float FieldStrengthAt(Vector3 worldPoint)
        {
            if (_head == null)
                return 0f;

            Vector3 local = _head.InverseTransformPoint(worldPoint);
            float axial = local.y;
            if (axial <= 0f || axial >= projectionRange)
                return 0f;

            float coneRadius = axial * Mathf.Tan(projectionAngle * 0.5f * Mathf.Deg2Rad);
            if (coneRadius <= 0f)
                return 0f;

            float radial = new Vector2(local.x, local.z).magnitude;
            if (radial >= coneRadius)
                return 0f;

            float reach = 1f - axial / projectionRange;
            float centring = 1f - radial / coneRadius;
            return Mathf.Clamp01(reach * centring);
        }

        /// <summary>
        /// Whether a world point is inside the cone at all. One cone test, asked two ways -
        /// two tests would be two chances to disagree with the shader.
        /// </summary>
        public bool IsInsideField(Vector3 worldPoint) => FieldStrengthAt(worldPoint) > 0f;

        /// <summary>
        /// Called each scan that a qualifying ghost is standing in the field, with how strongly
        /// the field is falling on it.
        ///
        /// <para>
        /// This reports a measurement. It does not decide that Spectral Grid has been proved -
        /// the validator does, against the ghost's own profile and against a dwell, which is
        /// why a projector left running in a doorway is not a way to grant yourself evidence.
        /// </para>
        /// </summary>
        protected virtual void OnGhostRevealed(GhostController ghost, float fieldStrength)
        {
            Observe(EvidenceType.SpectralGrid, fieldStrength);
        }

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
            SeatOnSurface(result);

            // Placing does not switch it on, and does not switch it off. Whatever the player
            // set in the hand is what is installed - the power is the item's, and the item is
            // the same object it was a moment ago.
            ApplyPower();
        }

        /// <summary>
        /// Rests a floor-placed device ON the floor rather than half inside it.
        ///
        /// <para>
        /// The placement puts the item's PIVOT on the contact point, which is right for
        /// something standing on its base and wrong the moment it lies on its side: the pivot
        /// is then somewhere in the middle of the model and half of it is under the boards.
        /// How far under is a fact about this model's pivot, and this project has been wrong
        /// about that twice - the purchased pack puts pivots tens of metres from their own
        /// mesh, and the flashlight's length was set from a comment nobody checked against the
        /// file (mistakes 12 and 29). So it is MEASURED here instead of written down: whatever
        /// the model turns out to be, its lowest drawn point ends up on the surface.
        /// </para>
        ///
        /// <para>
        /// Measured off what is DRAWN, with the projection volume left out - that box is
        /// eleven metres across and is the effect's screen footprint rather than the device, and
        /// measuring it once already lifted this projector through the lobby ceiling (mistake
        /// 42). Only ever lifts: a device that already clears the floor is left where it is.
        /// </para>
        /// </summary>
        private void SeatOnSurface(in PlacementResult result)
        {
            if (result.Surface != PlacementSurface.Floor)
                return;

            if (!TryMeasureDrawnBounds(out Bounds drawn))
                return;

            float sink = result.Position.y - drawn.min.y;
            if (sink > 0.0001f)
                transform.position += Vector3.up * sink;
        }

        /// <summary>
        /// The world box of everything this device DRAWS, effect volumes excluded.
        /// </summary>
        private bool TryMeasureDrawnBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || EffectVolume.Encloses(r.transform))
                    continue;

                Bounds b = r.bounds;
                if (b.size == Vector3.zero)
                    continue;

                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }

            return any;
        }
    }
}
