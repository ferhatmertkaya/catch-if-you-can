using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Electronics;

namespace CatchIfYouCan.Equipment
{
    public abstract class EquipmentBase : MonoBehaviour, IEquipment, IElectronicDevice
    {
        [SerializeField] protected EquipmentDefinition definition;
        [SerializeField] protected AudioSource audioSource;
        [SerializeField] protected float durabilityLossPerUse = 1f;


        protected Transform HandAnchor;
        protected bool DeviceActive;

        public EquipmentDefinition Definition => definition;
        public bool IsEquipped { get; protected set; }
        public bool IsPlaced { get; protected set; }
        public float BatteryLevel { get; protected set; }
        public float BatteryPercent => definition != null && definition.MaxBattery > 0f
            ? Mathf.Clamp01(BatteryLevel / definition.MaxBattery)
            : 0f;
        public float Durability { get; protected set; }
        public float MaxDurability => definition != null ? definition.MaxDurability : 100f;

        public bool IsPowered => BatteryLevel > 0f;
        public bool IsActive => IsPowered && DeviceActive && (IsEquipped || IsPlaced);
        public float InterferenceStrength => IsActive ? Mathf.Clamp01(BatteryPercent) * GetInterferenceMultiplier() : 0f;
        public string DeviceId => definition != null ? definition.Id : name;
        public Vector3 DevicePosition => transform.position;

        /// <summary>
        /// Whose this is, or <see cref="Procedural.Deterministic.EquipmentOwnership.Nobody"/>.
        ///
        /// <para>
        /// The project had no answer to "whose torch is that". An item knew it was equipped
        /// and knew which transform it was parented to, which is enough with one player and is
        /// exactly the shape of the mistake this repository keeps making. Set only through
        /// <see cref="TryClaim"/> and <see cref="ReleaseOwnership"/>, which is what makes the
        /// authority the only thing that can change it.
        /// </para>
        /// </summary>
        public int OwnerClientId { get; private set; } =
            Procedural.Deterministic.EquipmentOwnership.Nobody;

        /// <summary>
        /// Where this is, for ownership.
        ///
        /// <para>
        /// Read from ownership and placement rather than from <see cref="IsEquipped"/>, which
        /// answers a different question: a holstered item is not in a hand and is very much
        /// still carried. Deriving it from the wrong flag would put every stowed item back on
        /// the floor as far as a second player is concerned.
        /// </para>
        /// </summary>
        public Procedural.Deterministic.EquipmentHold Hold
        {
            get
            {
                if (IsPlaced)
                    return Procedural.Deterministic.EquipmentHold.Placed;

                return Procedural.Deterministic.EquipmentOwnership.IsOwned(OwnerClientId)
                    ? Procedural.Deterministic.EquipmentHold.Carried
                    : Procedural.Deterministic.EquipmentHold.InWorld;
            }
        }

        /// <summary>
        /// Somebody reaches for this. The authority answers.
        ///
        /// <para>
        /// Two players reaching for the same torch on the same frame is not an edge case, and
        /// the only way one of them loses is if exactly one machine decides. Offline that
        /// machine is this one and every claim is granted, so single player behaves exactly as
        /// it always has.
        /// </para>
        ///
        /// <para>
        /// Reach is not checked here. How far away the claimant is belongs with the other
        /// spatial checks in <c>Session.AuthorityRequests</c>, against positions the authority
        /// can see rather than a distance the asker computed.
        /// </para>
        /// </summary>
        public Procedural.Deterministic.EquipmentClaimVerdict TryClaim(int claimantClientId)
        {
            if (!SessionAuthority.CanChangeWorldState(this))
                return Procedural.Deterministic.EquipmentClaimVerdict.NotAuthoritative;

            var verdict = Procedural.Deterministic.EquipmentOwnership.Claim(
                Hold, OwnerClientId, claimantClientId);

            if (Procedural.Deterministic.EquipmentOwnership.ChangesOwner(verdict))
            {
                OwnerClientId = claimantClientId;

                // Taking a placed item is what un-places it. Doing this here rather than in
                // the pickup path means an item cannot end up owned and still installed on a
                // wall, which is a state nothing downstream knows how to draw.
                IsPlaced = false;
            }

            return verdict;
        }

        /// <summary>
        /// Puts this back to belonging to nobody - dropped, or taken out of an inventory.
        ///
        /// <para>
        /// Deliberately not part of <see cref="Unequip"/>. Unequipping is also how an item is
        /// stowed, and a stowed item is still its owner's; clearing ownership there would hand
        /// everybody's spare equipment to the first person who walked past.
        /// </para>
        /// </summary>
        public void ReleaseOwnership()
        {
            OwnerClientId = Procedural.Deterministic.EquipmentOwnership.Nobody;
        }

        /// <summary>Whether this player may press the button on it.</summary>
        public bool MayBeUsedBy(int clientId) =>
            Procedural.Deterministic.EquipmentOwnership.MayUse(Hold, OwnerClientId, clientId);

        protected virtual float GetInterferenceMultiplier() => 0.35f;

        /// <summary>
        /// How much condition one use costs. Virtual because "a use" is not the same act for
        /// every item: firing a camera wears it, and flicking a torch switch does not.
        /// </summary>
        protected virtual float DurabilityLossPerUse => durabilityLossPerUse;

        /// <summary>
        /// Every piece of equipment that exists. Systems that need to walk them - the audio
        /// wiring, the lab, the validator - ask this instead of sweeping the scene.
        ///
        /// <para>
        /// The audio controller called <c>FindObjectsByType&lt;EquipmentBase&gt;</c> on every
        /// equipment change, which walks every object in the house to find at most a handful.
        /// It is the same cost the EMF reader and the thermometer were each paying before
        /// phases W and Y, in a different place.
        /// </para>
        /// </summary>
        private static readonly System.Collections.Generic.List<EquipmentBase> AliveEquipment =
            new System.Collections.Generic.List<EquipmentBase>();

        /// <summary>Read-only view. Do not hold onto it across frames.</summary>
        public static System.Collections.Generic.IReadOnlyList<EquipmentBase> Alive => AliveEquipment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => AliveEquipment.Clear();

        protected virtual void Awake()
        {
            ApplyDefinitionStats();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            ElectronicDeviceRegistry.Register(this);

            if (!AliveEquipment.Contains(this))
                AliveEquipment.Add(this);
        }

        protected virtual void OnDestroy()
        {
            ElectronicDeviceRegistry.Unregister(this);
            AliveEquipment.Remove(this);
        }

        public virtual void BindDefinition(EquipmentDefinition def)
        {
            definition = def;
            ApplyDefinitionStats();
        }

        private void ApplyDefinitionStats()
        {
            if (definition == null)
                return;

            BatteryLevel = definition.MaxBattery;
            Durability = definition.MaxDurability;
        }

        protected virtual void Update()
        {
            if (!ShouldTick)
                return;

            DrainBattery();
            TickEquipped(Time.deltaTime);
        }

        /// <summary>
        /// Whether this device is doing anything worth spending a frame on. Held or placed by
        /// default; a subclass that can be left running somewhere else says so.
        /// </summary>
        protected virtual bool ShouldTick => IsEquipped || IsPlaced;

        protected virtual void DrainBattery()
        {
            if (definition == null || definition.BatteryUsagePerSecond <= 0f || !DeviceActive)
                return;

            if (BatteryLevel <= 0f)
            {
                BatteryLevel = 0f;
                OnBatteryDepleted();
                return;
            }

            BatteryLevel = Mathf.Max(0f, BatteryLevel - definition.BatteryUsagePerSecond * Time.deltaTime);
            if (BatteryLevel <= 0f)
                OnBatteryDepleted();
        }

        protected virtual void OnBatteryDepleted()
        {
            SetDeviceActive(false);
        }

        public virtual void Equip(Transform handAnchor)
        {
            HandAnchor = handAnchor;
            IsEquipped = true;
            IsPlaced = false;

            transform.SetParent(handAnchor, false);

            // The deprecated HandLocalPosition/Rotation are applied only for items that have
            // not been converted to the held-equipment path. A HeldEquipmentBase gets its pose
            // from its grip profile through EquipmentPresentation, which is the one owner;
            // applying a second offset here as well is how there came to be three.
            if (definition != null && !(this is HeldEquipmentBase))
            {
                transform.localPosition = definition.HandLocalPosition;
                transform.localRotation = Quaternion.Euler(definition.HandLocalRotation);
            }

            OnEquipped();
            GameEvents.EquipmentChanged();
        }

        public virtual void Unequip()
        {
            OnUnequipped();
            IsEquipped = false;
            HandAnchor = null;
            transform.SetParent(null, true);
            SetDeviceActive(false);
            GameEvents.EquipmentChanged();
        }

        public virtual void Use()
        {
            if (!CanPerformUse())
                return;

            PlayClip(definition != null ? definition.UseAudio : null);
            ApplyDurabilityLoss(DurabilityLossPerUse);
            OnUse();
        }

        public virtual bool TryPlace(Vector3 position, Quaternion rotation)
        {
            if (definition == null || !definition.CanPlace)
                return false;

            if (IsEquipped)
                Unequip();

            transform.SetPositionAndRotation(position, rotation);
            IsPlaced = true;
            PlayClip(definition.PlaceAudio);
            OnPlaced();
            GameEvents.EquipmentChanged();
            return true;
        }

        public virtual void Drop(Vector3 position, Quaternion rotation)
        {
            if (definition == null || !definition.CanDrop)
                return;

            if (IsEquipped)
                Unequip();

            transform.SetPositionAndRotation(position, rotation);
            IsPlaced = false;
            SetDeviceActive(false);
            GameEvents.EquipmentChanged();
        }

        protected bool CanPerformUse()
        {
            if (definition == null || !definition.CanUse)
                return false;
            if (Durability <= 0f)
                return false;
            if (definition.BatteryUsagePerSecond > 0f && BatteryLevel <= 0f)
                return false;
            return IsEquipped || IsPlaced;
        }

        protected void SetDeviceActive(bool active)
        {
            if (DeviceActive == active)
                return;

            DeviceActive = active;
            OnDeviceActiveChanged(active);
        }

        protected void ApplyDurabilityLoss(float amount)
        {
            if (amount <= 0f)
                return;

            Durability = Mathf.Max(0f, Durability - amount);
            if (Durability <= 0f)
                OnDurabilityDepleted();
        }

        /// <summary>
        /// Reports what this device just measured. It is an observation, not a finding.
        ///
        /// <para>
        /// Every item used to call <c>EvidenceManager.RegisterEvidence</c>, which meant a
        /// device that fired once had proved something - with nothing checking whether the
        /// ghost in the house exhibits that evidence at all, and nothing checking that the
        /// reading lasted longer than a frame. <see cref="Evidence.EvidenceValidator"/> decides;
        /// equipment only reports.
        /// </para>
        /// </summary>
        protected Evidence.EvidenceConfirmation Observe(Evidence.EvidenceType type, float strength)
        {
            return Evidence.EvidenceValidator.Submit(
                new Evidence.EvidenceObservation(type, DeviceId, strength, transform.position));
        }

        protected void PlayClip(AudioClip clip)
        {
            if (clip == null)
                return;

            if (audioSource != null)
                audioSource.PlayOneShot(clip);
            else
                AudioSource.PlayClipAtPoint(clip, transform.position);
        }

        protected virtual void OnEquipped() { }
        protected virtual void OnUnequipped() { }
        protected virtual void OnPlaced() { }
        protected virtual void OnUse() { }
        /// <summary>
        /// One line of state for the HUD: what this device is showing right now.
        ///
        /// <para>
        /// Battery by default, because most of them run on one. An item overrides this with
        /// the number that actually matters to it - a temperature, an EMF level, how many
        /// charges are left - so the player can read the instrument without looking at the
        /// instrument, which on a small screen is the difference between usable and not.
        /// </para>
        /// </summary>
        public virtual string HudReadout =>
            definition != null && definition.MaxBattery > 0f
                ? Mathf.RoundToInt(BatteryPercent * 100f) + "%"
                : string.Empty;

        /// <summary>
        /// The item's own controls, beyond Use. Empty by default.
        ///
        /// <para>
        /// The list is filled rather than returned so that showing the HUD costs no
        /// allocation: it is rebuilt every refresh, and one reused list serves every item.
        /// </para>
        /// </summary>
        public virtual void CollectActions(System.Collections.Generic.List<EquipmentAction> into)
        {
        }

        protected virtual void OnDeviceActiveChanged(bool active) { }
        protected virtual void OnDurabilityDepleted() { }
        protected virtual void TickEquipped(float deltaTime) { }
    }
}
