using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Salt: pour a pile on a floor and see whether anything walks through it.
    ///
    /// <para>
    /// It had never poured one. <c>saltPilePrefab</c> was a serialized field nothing anywhere
    /// assigned, and it was the first thing the pour tested, so every press of Use returned on
    /// the same line. The item was also on <see cref="EquipmentBase"/>, so it could not be
    /// carried, and the pile went wherever the hand anchor happened to be pointing with no
    /// check that there was a floor there - salt in mid-air, or inside a wall.
    /// </para>
    ///
    /// <para>
    /// It uses the shared placement query rather than inheriting
    /// <see cref="PlaceableEquipmentBase"/>, because salt is the one placeable item that does
    /// not place <i>itself</i>: the container stays in the hand and a pile is left behind. What
    /// it wants from that system is the part that decides whether the spot is a floor, in
    /// reach, and clear - so it asks for exactly that.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Salt")]
    public class SaltEquipment : HeldEquipmentBase
    {
        [Header("Pouring")]
        [Tooltip("How many piles one container holds.")]
        [SerializeField, Min(1)] private int maxPiles = 5;

        [Tooltip("How close two piles may be, in metres. Without it a player can empty the " +
                 "whole container onto one square of floor.")]
        [SerializeField, Min(0.1f)] private float pileSpacing = 1.5f;

        [Tooltip("How far the player can reach to pour, in metres.")]
        [SerializeField, Min(0.5f)] private float pourRange = 3f;

        [Tooltip("What counts as a floor, and what counts as being in the way.")]
        [SerializeField] private LayerMask pourMask = ~0;

        [Tooltip("Steepest surface salt will still sit on, degrees from level. Salt does not " +
                 "stay on a wall.")]
        [SerializeField, Range(5f, 45f)] private float maxFloorAngle = 25f;

        [Header("Visuals")]
        [Tooltip("What a poured pile looks like. Left empty it is a marked DEV placeholder; " +
                 "assigning a profile here swaps the art with no code change.")]
        [SerializeField] private EquipmentVisualProfile pileVisual;

        [Tooltip("What a disturbed print looks like under UV. Same contract as the pile.")]
        [SerializeField] private EquipmentVisualProfile footprintVisual;

        private int _poured;

        private static EquipmentVisualProfile _pileFallback;
        private static EquipmentVisualProfile _footprintFallback;

        /// <summary>How many piles are left in the container.</summary>
        public int RemainingPiles => Mathf.Max(0, maxPiles - _poured);

        /// <summary>Why the last pour did or did not happen. For the HUD and the lab.</summary>
        public EquipmentActionResult LastPour { get; private set; } = EquipmentActionResult.Success;

        /// <summary>How much salt is left in the container.</summary>
        public override string HudReadout => RemainingPiles + " / " + maxPiles;

        protected override float GetInterferenceMultiplier() => 0f;

        /// <summary>Pouring uses the container up. This is what the old version charged.</summary>
        protected override float DurabilityLossPerUse => 2f;

        protected override void OnUse()
        {
            LastPour = Pour();

            if (!LastPour.Ok)
                CIYCLog.Info("Salt not poured: " + LastPour.Status + " " + LastPour.Detail);
        }

        /// <summary>
        /// Puts one pile where the player is looking, if that is somewhere salt can sit.
        ///
        /// <para>
        /// Every refusal has its own reason, so "there is no floor there", "you are too far
        /// away", "there is already a pile there" and "the container is empty" are four
        /// different answers rather than one silent nothing.
        /// </para>
        /// </summary>
        public EquipmentActionResult Pour()
        {
            if (LifecycleState != EquipmentLifecycleState.Equipped)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "salt has to be in your hand");

            if (RemainingPiles <= 0)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.Broken, "the container is empty");

            Transform view = ViewTransform;
            if (view == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.MissingContent, "no view to pour along");

            var query = new PlacementQuery
            {
                Origin = view.position,
                Direction = view.forward,
                MaxRange = pourRange,
                // Floor only, and the angle limit is tighter than an installed device's. Salt
                // does not stay on a wall and it does not stay on a staircase.
                Allowed = PlacementSurface.Floor,
                SurfaceMask = pourMask,
                HalfExtents = new Vector3(0.16f, 0.02f, 0.16f),
                SurfaceSkin = 0.005f,
                PlayerForward = view.forward,
                MaxFloorAngle = maxFloorAngle,
            };

            var spot = EquipmentPlacement.Evaluate(query);
            if (!spot.IsValid)
                return EquipmentActionResult.Fail(spot.Status, spot.Detail);

            if (IsTooCloseToExisting(spot.Position))
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.Blocked, "there is already salt there");

            SaltPile.Create(spot.Position, spot.Rotation, PileVisual, FootprintVisual);
            _poured++;

            PlayClip(definition != null ? definition.PlaceAudio : null);
            return EquipmentActionResult.Success;
        }

        /// <summary>From the registry, not a scene sweep.</summary>
        private bool IsTooCloseToExisting(Vector3 position)
        {
            var piles = SaltPile.All;
            for (int i = 0; i < piles.Count; i++)
            {
                var pile = piles[i];
                if (pile != null &&
                    Vector3.Distance(pile.transform.position, position) < pileSpacing)
                    return true;
            }

            return false;
        }

        private EquipmentVisualProfile PileVisual =>
            pileVisual != null ? pileVisual : (_pileFallback ??= BuildFallback(
                "VisualProfile_SaltPile_DEV",
                new Vector3(0.34f, 0.05f, 0.34f),
                new Color(0.92f, 0.92f, 0.88f)));

        private EquipmentVisualProfile FootprintVisual =>
            footprintVisual != null ? footprintVisual : (_footprintFallback ??= BuildFallback(
                "VisualProfile_SaltFootprint_DEV",
                new Vector3(0.13f, 0.02f, 0.3f),
                new Color(0.75f, 0.85f, 0.95f)));

        /// <summary>
        /// A DEV placeholder profile, built as data rather than as geometry in here.
        ///
        /// <para>
        /// It goes through <see cref="EquipmentVisualFactory"/> like every other unfinished
        /// item, so it is a marked placeholder in the same shape as the other ten and swapping
        /// it for real art is assigning a prefab. Building a mesh in this class would be the
        /// hard-coded construction the art policy exists to prevent.
        /// </para>
        /// </summary>
        private static EquipmentVisualProfile BuildFallback(string name, Vector3 size, Color tint)
        {
            var profile = ScriptableObject.CreateInstance<EquipmentVisualProfile>();
            profile.name = name;
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.ApplyDevPlaceholder(size, tint);
            return profile;
        }
    }
}
