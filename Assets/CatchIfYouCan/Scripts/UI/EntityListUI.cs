using System.Collections.Generic;
using System.Linq;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.UI
{
    public class EntityListUI : MonoBehaviour
    {
        [SerializeField] private List<GhostDefinition> ghostCatalog = new List<GhostDefinition>();
        [SerializeField] private Transform listParent;
        [SerializeField] private Component headerText;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private HashSet<EvidenceType> _filter = new HashSet<EvidenceType>();
        private GameObject _root;
        private GhostDefinition _selectedGhost;

        public void BuildRuntime(Transform parent)
        {
            _root = new GameObject("EntityList");
            _root.transform.SetParent(parent, false);
            var rect = _root.AddComponent<RectTransform>();
            RuntimeUIFactory.Stretch(_root);

            headerText = RuntimeUIFactory.CreateText(_root.transform, "Header", "MATCHING ENTITIES — TAP TO IDENTIFY", 24,
                TextAnchor.UpperLeft, true);
            var headerRect = headerText.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.85f);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.offsetMin = headerRect.offsetMax = Vector2.zero;

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(_root.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = new Vector2(1, 0.84f);
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;

            var inner = RuntimeUIFactory.CreatePanel(scrollGo.transform, "Inner", true);
            var layout = inner.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            listParent = inner.transform;

            LoadCatalog();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        public void ApplyEvidenceFilter(HashSet<EvidenceType> selected)
        {
            _filter = selected != null ? new HashSet<EvidenceType>(selected) : new HashSet<EvidenceType>();
            Refresh();
        }

        public void Refresh()
        {
            ClearRows();
            LoadCatalog();

            IEnumerable<GhostDefinition> matches = ghostCatalog.Where(g => g != null);
            if (_filter != null && _filter.Count > 0)
                matches = matches.Where(g => _filter.All(e => g.HasEvidence(e)));

            var list = matches.ToList();
            if (list.Count == 0)
            {
                AddInfoRow("No entities match selected evidence.");
                return;
            }

            foreach (var ghost in list)
                AddEntityRow(ghost);
        }

        private void LoadCatalog()
        {
            if (ghostCatalog != null && ghostCatalog.Count > 0) return;

            ghostCatalog = new List<GhostDefinition>();
            var resources = Resources.LoadAll<GhostDefinition>("Ghosts");
            if (resources != null && resources.Length > 0)
            {
                ghostCatalog.AddRange(resources);
                return;
            }

            ghostCatalog.AddRange(GhostDefinitionFactory.CreateAllDefaultGhosts());
        }

        private void AddInfoRow(string text)
        {
            if (listParent == null) return;
            var row = RuntimeUIFactory.CreateText(listParent, "EntityRow", text, 18, TextAnchor.UpperLeft);
            var rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 36);
            UITheme.StyleBody(row);
            _rows.Add(row.gameObject);
        }

        private void AddEntityRow(GhostDefinition ghost)
        {
            if (listParent == null || ghost == null)
                return;

            string label = $"{ghost.DisplayName} — {ghost.Evidence1}, {ghost.Evidence2}, {ghost.Evidence3}";
            var btn = RuntimeUIFactory.CreateButton(listParent, label, () => SelectEntity(ghost), false, 44);
            _rows.Add(btn.gameObject);
        }

        private void SelectEntity(GhostDefinition ghost)
        {
            if (ghost == null)
                return;

            _selectedGhost = ghost;
            GameEvents.EntityDiscovered(ghost);
            UITheme.SetText(headerText, $"IDENTIFIED: {ghost.DisplayName.ToUpperInvariant()}");
            UITheme.SetTextColor(headerText, UITheme.Primary);
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row);
            }
            _rows.Clear();
        }
    }
}
