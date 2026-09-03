using System.Collections.Generic;
using System.Linq;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class EntityListUI : MonoBehaviour
    {
        [SerializeField] private List<GhostDefinition> ghostCatalog = new List<GhostDefinition>();
        [SerializeField] private Transform listParent;
        [SerializeField] private Component headerText;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<Button> _rowButtons = new List<Button>();
        private readonly List<GhostDefinition> _rowGhosts = new List<GhostDefinition>();
        private HashSet<EvidenceType> _filter = new HashSet<EvidenceType>();
        private GameObject _root;
        private GhostDefinition _selectedGhost;
        private Component _checklistText;
        private Button _confirmButton;
        private Component _confirmLabel;

        public void BuildRuntime(Transform parent)
        {
            _root = new GameObject("EntityList");
            _root.transform.SetParent(parent, false);
            var rect = _root.AddComponent<RectTransform>();
            RuntimeUIFactory.Stretch(_root);

            headerText = RuntimeUIFactory.CreateText(_root.transform, "Header",
                "SELECT AN ENTITY", 24, TextAnchor.UpperLeft, true, UITheme.FontRole.Header);
            var headerRect = headerText.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.90f);
            headerRect.anchorMax = new Vector2(1, 1f);
            headerRect.offsetMin = headerRect.offsetMax = Vector2.zero;

            // What the player has actually found, spelled out. Deducing an entity from evidence
            // is the game; asking them to hold three findings in their head while reading nine
            // rows is not.
            _checklistText = RuntimeUIFactory.CreateText(_root.transform, "Checklist",
                string.Empty, 17, TextAnchor.UpperLeft, false, UITheme.FontRole.Body);
            var checklistRect = _checklistText.GetComponent<RectTransform>();
            checklistRect.anchorMin = new Vector2(0, 0.80f);
            checklistRect.anchorMax = new Vector2(1, 0.90f);
            checklistRect.offsetMin = checklistRect.offsetMax = Vector2.zero;
            UITheme.StyleMuted(_checklistText);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(_root.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0.14f);
            scrollRect.anchorMax = new Vector2(1, 0.79f);
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;

            var inner = RuntimeUIFactory.CreatePanel(scrollGo.transform, "Inner", true);
            var layout = inner.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            listParent = inner.transform;

            BuildConfirmButton();
            LoadCatalog();
        }

        /// <summary>
        /// The commitment.
        ///
        /// <para>
        /// Tapping a row used to <em>be</em> the identification: it raised
        /// <c>EntityDiscovered</c> immediately, which completed the objective when it happened
        /// to be right and did nothing whatever when it was wrong. A wrong answer therefore
        /// looked exactly like no answer, and working down the list until something happened
        /// was a guaranteed win. Selecting is now free and reversible; confirming is neither.
        /// </para>
        /// </summary>
        private void BuildConfirmButton()
        {
            _confirmButton = RuntimeUIFactory.CreateButton(_root.transform,
                "CONFIRM IDENTIFICATION", Confirm, true, 56f);
            var rect = _confirmButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.01f);
            rect.anchorMax = new Vector2(1f, 0.12f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _confirmLabel = RuntimeUIFactory.FindLabel(_confirmButton);
            RefreshConfirmState();
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
            RefreshChecklist();

            if (list.Count == 0)
            {
                AddInfoRow("No entities match the evidence found so far.");
                RefreshConfirmState();
                return;
            }

            foreach (var ghost in list)
                AddEntityRow(ghost);

            // A selection that is no longer on the list cannot be confirmed from it.
            if (_selectedGhost != null && !list.Contains(_selectedGhost))
                _selectedGhost = null;

            RefreshSelectionVisuals();
            RefreshConfirmState();
        }

        /// <summary>The evidence found so far, and what is still missing.</summary>
        private void RefreshChecklist()
        {
            if (_checklistText == null)
                return;

            var manager = EvidenceManager.Instance;
            var found = manager != null
                ? new HashSet<EvidenceType>(manager.FoundEvidence)
                : new HashSet<EvidenceType>();

            if (found.Count == 0)
            {
                UITheme.SetText(_checklistText, "EVIDENCE: none confirmed yet.");
                return;
            }

            UITheme.SetText(_checklistText,
                "EVIDENCE: " + string.Join(", ", found.Select(Readable)));
        }

        private static string Readable(EvidenceType type)
        {
            switch (type)
            {
                case EvidenceType.EMFSurge: return "EMF surge";
                case EvidenceType.UVTraces: return "UV traces";
                case EvidenceType.FreezingTemperature: return "freezing temperature";
                case EvidenceType.SpectralGrid: return "spectral grid";
                case EvidenceType.EVPResponse: return "EVP response";
                case EvidenceType.GhostOrb: return "ghost orb";
                case EvidenceType.ParabolicAnomaly: return "parabolic anomaly";
                case EvidenceType.ElectronicDistortion: return "electronic distortion";
                case EvidenceType.PhysicalDisturbance: return "physical disturbance";
                default: return type.ToString();
            }
        }

        private static bool _authoredCatalogMissingReported;

        private void LoadCatalog()
        {
            if (ghostCatalog != null && ghostCatalog.Count > 0)
            {
                RestrictToMissionRoster();
                return;
            }

            ghostCatalog = new List<GhostDefinition>();
            var resources = Resources.LoadAll<GhostDefinition>("Ghosts");
            if (resources != null && resources.Length > 0)
            {
                ghostCatalog.AddRange(resources);
                RestrictToMissionRoster();
                return;
            }

            // The authored assets are the intended source; the code factory is the safety
            // net. Which one is live must not be invisible. Reported once per session.
            if (!_authoredCatalogMissingReported)
            {
                _authoredCatalogMissingReported = true;
                Core.CIYCLog.Warn("No GhostDefinition assets under Resources/Ghosts. " +
                                  "Falling back to GhostDefinitionFactory defaults.");
            }

            ghostCatalog.AddRange(GhostDefinitionFactory.CreateAllDefaultGhosts());
            RestrictToMissionRoster();
        }

        /// <summary>
        /// Narrows the list to the entities that can actually haunt this location.
        ///
        /// <para>
        /// A mission names its own roster so the evidence its kit can gather is enough to tell
        /// the candidates apart. Listing the whole bestiary anyway would undo that: the player
        /// would be choosing between nine entities on evidence chosen to separate three.
        /// </para>
        /// </summary>
        private void RestrictToMissionRoster()
        {
            MissionRuntime mission = MissionManager.Instance != null
                ? MissionManager.Instance.ActiveMission
                : null;

            string[] allowed = mission?.Definition != null ? mission.Definition.EligibleGhostIds : null;
            if (allowed == null || allowed.Length == 0)
                return;

            var set = new HashSet<string>(allowed, System.StringComparer.Ordinal);
            ghostCatalog = ghostCatalog.Where(g => g != null && set.Contains(g.Id)).ToList();
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

            string label = $"{ghost.DisplayName} — {Readable(ghost.Evidence1)}, " +
                           $"{Readable(ghost.Evidence2)}, {Readable(ghost.Evidence3)}";
            var btn = RuntimeUIFactory.CreateButton(listParent, label, () => SelectEntity(ghost), false, 44);
            _rows.Add(btn.gameObject);
            _rowButtons.Add(btn);
            _rowGhosts.Add(ghost);
        }

        /// <summary>Chooses a candidate. Free, reversible, and not an answer.</summary>
        private void SelectEntity(GhostDefinition ghost)
        {
            if (ghost == null || Submitted)
                return;

            _selectedGhost = ghost;
            RefreshSelectionVisuals();
            RefreshConfirmState();
        }

        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < _rowButtons.Count && i < _rowGhosts.Count; i++)
            {
                Button row = _rowButtons[i];
                if (row == null)
                    continue;

                bool chosen = _rowGhosts[i] == _selectedGhost;

                var feedback = row.GetComponent<UIButtonFeedback>();
                if (feedback != null)
                    feedback.SetSelected(chosen);

                UITheme.ApplyBorder(row.gameObject,
                    chosen ? UITheme.AccentBorder : UITheme.Border, UITheme.PanelBorderWidth);
                UITheme.SetTextColor(RuntimeUIFactory.FindLabel(row),
                    chosen ? UITheme.TextPrimary : UITheme.TextMuted);
            }
        }

        private static bool Submitted =>
            MissionManager.Instance?.ActiveMission?.IdentificationSubmitted == true;

        private void RefreshConfirmState()
        {
            if (_confirmButton == null)
                return;

            if (Submitted)
            {
                _confirmButton.interactable = false;
                UITheme.SetText(_confirmLabel, "IDENTIFICATION SUBMITTED");
                return;
            }

            bool ready = _selectedGhost != null;
            _confirmButton.interactable = ready;
            UITheme.SetText(_confirmLabel, ready
                ? "CONFIRM: " + _selectedGhost.DisplayName
                : "SELECT AN ENTITY FIRST");
        }

        /// <summary>
        /// Commits. One answer per investigation, right or wrong, and the player is told which.
        /// </summary>
        private void Confirm()
        {
            if (_selectedGhost == null)
                return;

            var manager = MissionManager.Instance;
            if (manager == null)
            {
                SetHeader("No active investigation to identify.", UITheme.Warning);
                return;
            }

            MissionManager.IdentificationResult result = manager.SubmitIdentification(_selectedGhost);

            switch (result)
            {
                case MissionManager.IdentificationResult.Correct:
                    SetHeader("CONFIRMED: " + _selectedGhost.DisplayName.ToUpperInvariant(),
                              UITheme.Primary);
                    break;
                case MissionManager.IdentificationResult.Incorrect:
                    // Said plainly. A wrong answer that looks like nothing happening is the
                    // failure this whole screen was rebuilt to end.
                    SetHeader("THAT IS NOT THE ENTITY. THE CASE IS FILED.", UITheme.Warning);
                    break;
                case MissionManager.IdentificationResult.AlreadySubmitted:
                    SetHeader("You have already filed an identification.", UITheme.TextMuted);
                    break;
                default:
                    SetHeader("No entity to identify.", UITheme.Warning);
                    break;
            }

            RefreshConfirmState();
        }

        private void SetHeader(string message, Color colour)
        {
            UITheme.SetText(headerText, message);
            UITheme.SetTextColor(headerText, colour);
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row);
            }
            _rows.Clear();
            _rowButtons.Clear();
            _rowGhosts.Clear();
        }
    }
}
