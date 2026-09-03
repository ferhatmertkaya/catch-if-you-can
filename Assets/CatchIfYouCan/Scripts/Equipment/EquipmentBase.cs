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

        protected virtual float GetInterferenceMultiplier() => 0.35f;

        /// <summary>
        /// How much condition one use costs. Virtual because "a use" is not the same act for
        /// every item: firing a camera wears it, and flicking a torch switch does not.
        /// </summary>
        protected virtual float DurabilityLossPerUse => durabilityLossPerUse;

        protected virtual void Awake()
        {
            ApplyDefinitionStats();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            ElectronicDeviceRegistry.Register(this);
        }

        protected virtual void OnDestroy()
        {
            ElectronicDeviceRegistry.Unregister(this);
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
        protected virtual void OnDeviceActiveChanged(bool active) { }
        protected virtual void OnDurabilityDepleted() { }
        protected virtual void TickEquipped(float deltaTime) { }
    }
}
