using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// A held item that can be installed in the room: aimed, previewed, placed, and picked back
    /// up as the same object.
    ///
    /// <para>
    /// Three items place things - the grid projector, the video camera, the salt - and this is
    /// the part they share. Each of them inventing its own aim ray, its own preview and its own
    /// idea of what a wall is would be three answers to one question, which is the mistake this
    /// migration keeps finding.
    /// </para>
    ///
    /// <para>
    /// <b>Placing does not create a new object.</b> The item reparents itself into the room and
    /// changes lifecycle state; its battery, durability and settings come with it because it is
    /// the same instance. That is what makes picking it back up restore what you put down
    /// rather than a fresh copy of it.
    /// </para>
    /// </summary>
    public abstract class PlaceableEquipmentBase : HeldEquipmentBase
    {
        [Header("Placement")]
        [Tooltip("Which surfaces this item will sit on.")]
        [SerializeField] private PlacementSurface allowedSurfaces = PlacementSurface.FloorAndWall;

        [Tooltip("How far the player can reach to place it, in metres.")]
        [SerializeField, Min(0.5f)] private float placementRange = 3.5f;

        [Tooltip("What counts as a surface, and what counts as being in the way.")]
        [SerializeField] private LayerMask placementMask = ~0;

        [Tooltip("Half-size of the clearance box, in metres. Zero skips the check, which means " +
                 "the item can be placed inside a wall.")]
        [SerializeField] private Vector3 clearanceHalfExtents = new Vector3(0.09f, 0.09f, 0.09f);

        [Tooltip("Steepest surface still treated as floor rather than wall, degrees from level.")]
        [SerializeField, Range(5f, 70f)] private float maxFloorAngle = 40f;

        private EquipmentPlacementPreview _preview;
        private PlacementResult _candidate;

        /// <summary>The last placement evaluated, for the HUD and the lab.</summary>
        public PlacementResult Candidate => _candidate;

        /// <summary>Whether the current aim would be accepted.</summary>
        public bool HasValidCandidate => _candidate.IsValid;

        /// <summary>Which surfaces this item accepts. Read by the lab and the validator.</summary>
        public PlacementSurface AllowedSurfaces => allowedSurfaces;

        public override EquipmentActionResult TryBeginPlacement()
        {
            var started = base.TryBeginPlacement();
            if (!started.Ok)
                return started;

            EnsurePreview();
            return started;
        }

        public override EquipmentActionResult TryCancelPlacement()
        {
            var cancelled = base.TryCancelPlacement();
            _preview?.SetVisible(false);
            return cancelled;
        }

        protected override void CancelPlacementInternal()
        {
            _preview?.SetVisible(false);
        }

        /// <summary>
        /// Commits the current candidate. Refuses with the candidate's own reason, so "there is
        /// a table in the way" and "that is a wall and this only goes on floors" reach the
        /// player as different answers.
        /// </summary>
        public override EquipmentActionResult TryPlace()
        {
            if (definition == null || !definition.CanPlace)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NotAllowedByDefinition);

            if (LifecycleState != EquipmentLifecycleState.PlacementPreview)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "no placement in progress");

            if (!_candidate.IsValid)
                return EquipmentActionResult.Fail(_candidate.Status, _candidate.Detail);

            _preview?.SetVisible(false);

            // The same object moves into the room. Nothing is instantiated, so nothing is left
            // behind and nothing is duplicated.
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(_candidate.Position,
                                             OrientForPlacement(_candidate));

            PlayClip(definition.PlaceAudio);
            EnterPlacedState();
            OnPlacedInWorld(_candidate);
            return EquipmentActionResult.Success;
        }

        /// <summary>
        /// Turns the shared query's answer into this item's own orientation.
        ///
        /// <para>
        /// The carried-transform convention is that an item's local +Y is its length and the
        /// direction it works along. The placement query hands back a rotation whose +Z is a
        /// wall's normal and whose +Y is a floor's, so a wall placement is turned a quarter
        /// circle to put the item's working axis along the normal - which is what makes a
        /// wall-mounted projector throw its field into the room rather than into the plaster.
        /// </para>
        /// </summary>
        protected virtual Quaternion OrientForPlacement(in PlacementResult result)
        {
            return result.Surface == PlacementSurface.Floor
                ? result.Rotation
                : result.Rotation * Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>Called once the item is installed. Start whatever being placed means.</summary>
        protected virtual void OnPlacedInWorld(in PlacementResult result) { }

        protected override void OnPickedUpFromPlacement()
        {
            _preview?.SetVisible(false);
        }

        /// <summary>
        /// Re-evaluates the aim while a preview is up. Runs from the equipment tick, so it stops
        /// the moment the item is stowed or placed.
        /// </summary>
        protected override void TickEquipped(float deltaTime)
        {
            if (LifecycleState != EquipmentLifecycleState.PlacementPreview)
                return;

            Transform view = ViewTransform;
            if (view == null)
                return;

            var query = new PlacementQuery
            {
                Origin = view.position,
                Direction = view.forward,
                MaxRange = placementRange,
                Allowed = allowedSurfaces,
                SurfaceMask = placementMask,
                HalfExtents = clearanceHalfExtents,
                SurfaceSkin = 0.01f,
                PlayerForward = view.forward,
                MaxFloorAngle = maxFloorAngle,
            };

            _candidate = EquipmentPlacement.Evaluate(query);

            EnsurePreview();
            if (_preview == null)
                return;

            if (_candidate.IsValid)
            {
                _preview.Show(_candidate.Position, OrientForPlacement(_candidate), true);
                return;
            }

            // An invalid candidate still shows where it would have gone when there is a surface
            // to show it against; with nothing in range at all there is nothing to draw.
            if (_candidate.Status == EquipmentActionStatus.Blocked)
                _preview.Show(_candidate.Position, OrientForPlacement(_candidate), false);
            else
                _preview.SetVisible(false);
        }

        private void EnsurePreview()
        {
            if (_preview != null || CarriedRoot == null)
                return;

            _preview = EquipmentPlacementPreview.Build(
                CarriedRoot, "DEV_PlacementPreview_" + DeviceId);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_preview != null)
                Destroy(_preview.gameObject);
        }
    }
}
