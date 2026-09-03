using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The warding relic: a crystal that breaks to end a hunt, three times, and then is glass.
    ///
    /// <para>
    /// It had no runtime path - no case for <c>warding_relic</c> in the factory, so the id fell
    /// through to the unknown-id branch and a player would have been handed a DEV_PLACEHOLDER
    /// box - and it derived from <see cref="EquipmentBase"/>, so it could not have been carried
    /// if it had one.
    /// </para>
    ///
    /// <para>
    /// <b>Its ward radius did nothing.</b> It found the ghost with
    /// <c>GameObject.Find("Ghost")</c>, falling back to <c>GameObject.Find("GhostEntity")</c> -
    /// names nothing in this project uses; the ghost carries the <c>Ghost</c> <i>tag</i> and is
    /// named from its definition. And when the lookup failed the method returned
    /// <c>IsEquipped || IsPlaced</c>, which is to say <b>yes</b>. So a relic anywhere in the
    /// house, in a bag, at any distance, ended every hunt on the frame it began. The item was
    /// not weak or strong; it was the end of the hunt mechanic.
    /// </para>
    ///
    /// <para>
    /// It has no battery and reports no evidence. A ward is not an instrument, and giving it an
    /// evidence type to make the matrix look full would be inventing gameplay to fill a table.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Warding Relic")]
    public class WardingRelic : PlaceableEquipmentBase
    {
        [Header("Ward")]
        [Tooltip("How close the ghost has to come before the crystal answers, in metres.")]
        [SerializeField, Min(0.5f)] private float wardRadius = 5f;

        [Tooltip("How many times it can do this before it is glass.")]
        [SerializeField, Min(1)] private int maxCharges = 3;

        [Tooltip("Seconds the ghost has to be inside the radius before the crystal breaks. " +
                 "Not zero: a ward that fires on the first frame of a hunt means the player " +
                 "never sees a hunt, and a mechanic nobody experiences is not a mechanic.")]
        [SerializeField, Min(0f)] private float reactionDelay = 1.25f;

        [Header("Presentation")]
        [SerializeField] private Color chargedGlow = new Color(0.55f, 0.75f, 1f);
        [SerializeField, Min(0f)] private float glowRange = 2.4f;
        [SerializeField, Min(0f)] private float glowIntensity = 1.1f;

        [Header("Audio")]
        [SerializeField] private AudioClip breakClip;

        private Light _glow;
        private int _charges;
        private bool _huntActive;
        private float _wardTimer;

        /// <summary>How many breaks it has left. For the HUD and the lab.</summary>
        public int RemainingCharges => _charges;

        /// <summary>Whether it can still do anything at all.</summary>
        public bool IsSpent => _charges <= 0;

        /// <summary>How close the ghost must come. Read by the lab and the HUD.</summary>
        public float WardRadius => wardRadius;

        /// <summary>How many breaks are left. There was no way at all to see this.</summary>
        public override string HudReadout =>
            IsSpent ? "SPENT" : _charges + " / " + maxCharges;

        protected override float GetInterferenceMultiplier() => 0f;

        /// <summary>A crystal is spent by warding, not by being switched on.</summary>
        protected override float DurabilityLossPerUse => 0f;

        protected override void Awake()
        {
            base.Awake();
            _charges = maxCharges;

            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameEvents.OnHuntStarted -= HandleHuntStarted;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
        }

        /// <summary>
        /// The glow. A mesh cannot light a room, and the <c>crystalIntact</c> object this used
        /// to hide on the last charge was a serialized field nothing ever assigned - so there
        /// was no way to tell a live relic from a spent one.
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            var glowGo = new GameObject("WardGlow");
            glowGo.transform.SetParent(CarriedRoot, false);
            glowGo.transform.localPosition = new Vector3(0f, CarriedLength * 0.5f, 0f);

            _glow = glowGo.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = glowRange;
            _glow.color = chargedGlow;
            _glow.shadows = LightShadows.None;

            ApplyGlow();
        }

        /// <summary>
        /// Nothing. A relic is not switched on: it is carried or set down, and it answers a
        /// hunt or it does not. Pressing Use on one used to toggle a DeviceActive flag that
        /// nothing read.
        /// </summary>
        protected override void OnUse()
        {
        }

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            _wardTimer = 0f;
            ApplyGlow();
        }

        protected override void OnPlacedInWorld(in PlacementResult result)
        {
            ApplyGlow();
        }

        protected override void TickEquipped(float deltaTime)
        {
            // The placement preview, when one is up.
            base.TickEquipped(deltaTime);

            if (!_huntActive || IsSpent || !IsWarding)
            {
                _wardTimer = 0f;
                return;
            }

            var ghost = GhostController.Active;
            if (ghost == null || !ghost.IsHuntImminentOrActive)
            {
                _wardTimer = 0f;
                return;
            }

            Vector3 here = CarriedRoot != null ? CarriedRoot.position : transform.position;
            if (Vector3.Distance(here, ghost.transform.position) > wardRadius)
            {
                // Out of range resets the clock rather than pausing it. A ward that
                // accumulates across several separate approaches is a ward with no radius.
                _wardTimer = 0f;
                return;
            }

            _wardTimer += deltaTime;
            if (_wardTimer < reactionDelay)
                return;

            _wardTimer = 0f;
            Break(ghost);
        }

        /// <summary>
        /// Whether the relic is somewhere it can work: in a hand or set down in the room. In a
        /// bag it is not warding anything, which is the difference the old version could not
        /// tell.
        /// </summary>
        private bool IsWarding =>
            LifecycleState == EquipmentLifecycleState.Equipped ||
            LifecycleState == EquipmentLifecycleState.Placed;

        private void HandleHuntStarted()
        {
            _huntActive = true;
            _wardTimer = 0f;
        }

        private void HandleHuntEnded()
        {
            _huntActive = false;
            _wardTimer = 0f;
        }

        /// <summary>
        /// One charge, one hunt. Asked of the ghost rather than taken from a HuntController
        /// found by sweeping the scene - and only spent if there was actually a hunt to end.
        /// </summary>
        private void Break(GhostController ghost)
        {
            if (!ghost.TryEndHunt())
                return;

            _charges--;
            _huntActive = false;

            PlayClip(breakClip);
            ApplyGlow();

            CIYCLog.Info(_charges > 0
                ? "Warding relic broke a charge. " + _charges + " left."
                : "Warding relic is spent.");
        }

        /// <summary>
        /// The glow says what the relic has left: full on three charges, dimmer on each break,
        /// dark when it is glass. That is the readout the item never had.
        /// </summary>
        private void ApplyGlow()
        {
            if (_glow == null)
                return;

            bool lit = !IsSpent && IsWarding;
            _glow.enabled = lit;

            if (!lit)
                return;

            float fraction = maxCharges > 0 ? _charges / (float)maxCharges : 0f;
            _glow.intensity = glowIntensity * Mathf.Max(0.25f, fraction);
        }
    }
}
