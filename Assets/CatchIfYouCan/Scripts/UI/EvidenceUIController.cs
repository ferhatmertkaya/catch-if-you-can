using System;
using System.Collections.Generic;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class EvidenceUIController : MonoBehaviour
    {
        [SerializeField] private Transform toggleParent;
        [SerializeField] private bool highContrast;

        private readonly Dictionary<EvidenceType, Toggle> _toggles = new Dictionary<EvidenceType, Toggle>();
        private readonly HashSet<EvidenceType> _selected = new HashSet<EvidenceType>();
        private GameObject _root;

        public event Action<HashSet<EvidenceType>> OnFilterChanged;

        public void BuildRuntime(Transform parent)
        {
            _root = new GameObject("EvidenceToggles");
            _root.transform.SetParent(parent, false);
            var rect = _root.AddComponent<RectTransform>();
            RuntimeUIFactory.Stretch(_root);
            var layout = _root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            toggleParent = _root.transform;
            BuildToggles();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        private void Start()
        {
            if (SettingsManager.Instance != null)
                highContrast = SettingsManager.Instance.HighContrastEvidence;
            if (_toggles.Count == 0)
                BuildToggles();
        }

        private void BuildToggles()
        {
            if (toggleParent == null) return;
            _toggles.Clear();

            foreach (EvidenceType type in Enum.GetValues(typeof(EvidenceType)))
            {
                bool found = EvidenceManager.Instance != null && EvidenceManager.Instance.HasEvidence(type);
                var toggle = RuntimeUIFactory.CreateToggle(toggleParent, FormatLabel(type), found);
                toggle.interactable = found;
                if (highContrast && found)
                    toggle.graphic.color = UITheme.Primary;

                var captured = type;
                toggle.onValueChanged.AddListener(on =>
                {
                    if (on) _selected.Add(captured);
                    else _selected.Remove(captured);
                    OnFilterChanged?.Invoke(new HashSet<EvidenceType>(_selected));
                });

                if (found)
                {
                    toggle.isOn = true;
                    _selected.Add(type);
                }

                _toggles[type] = toggle;
            }

            OnFilterChanged?.Invoke(new HashSet<EvidenceType>(_selected));
        }

        public void Refresh()
        {
            foreach (var pair in _toggles)
            {
                bool found = EvidenceManager.Instance != null && EvidenceManager.Instance.HasEvidence(pair.Key);
                pair.Value.interactable = found;
                if (!found)
                {
                    pair.Value.isOn = false;
                    _selected.Remove(pair.Key);
                }
            }
            OnFilterChanged?.Invoke(new HashSet<EvidenceType>(_selected));
        }

        public HashSet<EvidenceType> GetSelectedEvidence() => new HashSet<EvidenceType>(_selected);

        private static string FormatLabel(EvidenceType type)
        {
            return type switch
            {
                EvidenceType.EMFSurge => "EMF SURGE",
                EvidenceType.UVTraces => "UV TRACES",
                EvidenceType.SpectralGrid => "SPECTRAL GRID",
                EvidenceType.EVPResponse => "EVP RESPONSE",
                EvidenceType.GhostOrb => "GHOST ORB",
                EvidenceType.FreezingTemperature => "FREEZING TEMP",
                EvidenceType.ParabolicAnomaly => "PARABOLIC ANOMALY",
                EvidenceType.ElectronicDistortion => "ELECTRONIC DISTORTION",
                EvidenceType.PhysicalDisturbance => "PHYSICAL DISTURBANCE",
                _ => type.ToString().ToUpperInvariant()
            };
        }
    }
}
