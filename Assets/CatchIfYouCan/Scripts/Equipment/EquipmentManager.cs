using System;
using System.Collections.Generic;
using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Equipment
{
    public class EquipmentManager : SingletonBehaviour<EquipmentManager>
    {
        [SerializeField] private Transform handAnchor;
        [SerializeField] private KeyCode useKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode placeKey = KeyCode.G;
        [SerializeField] private KeyCode dropKey = KeyCode.Q;
        [SerializeField] private LayerMask placementMask = ~0;
        [SerializeField] private float placementSurfaceOffset = 0.02f;

        private readonly List<EquipmentDefinition> _loadout = new List<EquipmentDefinition>();
        private readonly Dictionary<string, EquipmentBase> _spawned = new Dictionary<string, EquipmentBase>();
        private int _activeIndex = -1;

        public IEquipment ActiveEquipment => ActiveInstance;
        public EquipmentBase ActiveInstance { get; private set; }
        public Transform HandAnchor => handAnchor;
        public IReadOnlyList<EquipmentDefinition> Loadout => _loadout;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                Core.ServiceLocator.Unregister<EquipmentManager>();
            base.OnDestroy();
        }

        private void Update()
        {
            if (ActiveInstance == null || !ActiveInstance.IsEquipped)
                return;

            if (UnityEngine.Input.GetKeyDown(useKey))
                ActiveInstance.Use();

            if (UnityEngine.Input.GetKeyDown(placeKey))
                TryPlaceActive();

            if (UnityEngine.Input.GetKeyDown(dropKey))
                DropActive();
        }

        public void SetHandAnchor(Transform anchor)
        {
            handAnchor = anchor;
            if (ActiveInstance != null && ActiveInstance.IsEquipped && handAnchor != null)
                ActiveInstance.Equip(handAnchor);
        }

        public void SetLoadout(IEnumerable<EquipmentDefinition> definitions)
        {
            _loadout.Clear();
            if (definitions == null)
                return;

            foreach (var def in definitions)
            {
                if (def != null)
                    _loadout.Add(def);
            }
        }

        public bool EquipByIndex(int index)
        {
            if (index < 0 || index >= _loadout.Count || handAnchor == null)
                return false;

            var definition = _loadout[index];
            if (definition == null)
                return false;

            EquipmentRuntimeFactory.EnsureRuntimePrefab(definition);
            if (definition.Prefab == null)
                return false;

            UnequipActive();

            if (!_spawned.TryGetValue(definition.Id, out var instance) || instance == null)
            {
                var go = Instantiate(definition.Prefab);
                instance = go.GetComponent<EquipmentBase>();
                if (instance == null)
                {
                    CIYCLog.Warn($"Equipment prefab missing EquipmentBase component: {definition.Id}");
                    Destroy(go);
                    return false;
                }

                instance.name = definition.DisplayName;
                _spawned[definition.Id] = instance;
            }

            ActiveInstance = instance;
            _activeIndex = index;
            ActiveInstance.Equip(handAnchor);
            GameEvents.EquipmentChanged();
            return true;
        }

        public bool EquipById(string equipmentId)
        {
            for (int i = 0; i < _loadout.Count; i++)
            {
                if (_loadout[i] != null && _loadout[i].Id == equipmentId)
                    return EquipByIndex(i);
            }

            return false;
        }

        public void UnequipActive()
        {
            if (ActiveInstance == null)
                return;

            ActiveInstance.Unequip();
            ActiveInstance = null;
            _activeIndex = -1;
            GameEvents.EquipmentChanged();
        }

        public void CycleNext()
        {
            if (_loadout.Count == 0)
                return;

            int next = _activeIndex < 0 ? 0 : (_activeIndex + 1) % _loadout.Count;
            EquipByIndex(next);
        }

        public void CyclePrevious()
        {
            if (_loadout.Count == 0)
                return;

            int prev = _activeIndex < 0 ? _loadout.Count - 1 : (_activeIndex - 1 + _loadout.Count) % _loadout.Count;
            EquipByIndex(prev);
        }

        public bool TryPlaceActive()
        {
            if (ActiveInstance == null || ActiveInstance.Definition == null || !ActiveInstance.Definition.CanPlace)
                return false;

            if (!TryGetPlacementPose(out var position, out var rotation))
                return false;

            var placed = ActiveInstance.TryPlace(position, rotation);
            if (placed)
            {
                ActiveInstance = null;
                _activeIndex = -1;
            }

            return placed;
        }

        public void DropActive()
        {
            if (ActiveInstance == null || ActiveInstance.Definition == null || !ActiveInstance.Definition.CanDrop)
                return;

            var dropPos = handAnchor != null ? handAnchor.position + handAnchor.forward * 0.75f : transform.position;
            var dropRot = handAnchor != null ? handAnchor.rotation : Quaternion.identity;
            ActiveInstance.Drop(dropPos, dropRot);
            ActiveInstance = null;
            _activeIndex = -1;
        }

        private bool TryGetPlacementPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (handAnchor == null)
                return false;

            float range = ActiveInstance.Definition.InteractionRange;
            var ray = new Ray(hAnchorPosition(), handAnchor.forward);
            if (Physics.Raycast(ray, out var hit, range, placementMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point + hit.normal * placementSurfaceOffset;
                rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                return true;
            }

            position = handAnchor.position + handAnchor.forward * Mathf.Min(range, 1.25f);
            rotation = Quaternion.LookRotation(handAnchor.forward, Vector3.up);
            return true;
        }

        private Vector3 hAnchorPosition() => handAnchor != null ? handAnchor.position : transform.position;

        public void GiveStarterLoadout()
        {
            var flashlight = EquipmentDefinitionFactory.GetById("flashlight");
            var emf = EquipmentDefinitionFactory.GetById("emf_detector");
            var uv = EquipmentDefinitionFactory.GetById("uv_light");

            EquipmentRuntimeFactory.EnsureRuntimePrefab(flashlight);
            EquipmentRuntimeFactory.EnsureRuntimePrefab(emf);
            EquipmentRuntimeFactory.EnsureRuntimePrefab(uv);

            SetLoadout(new[] { flashlight, emf, uv });
            EquipByIndex(0);
        }
    }
}
