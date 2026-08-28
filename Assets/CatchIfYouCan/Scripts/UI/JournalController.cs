using System.Collections.Generic;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public enum JournalTab
    {
        Case,
        Evidence,
        Entities,
        Photos,
        Objectives
    }

    public class JournalController : MonoBehaviour
    {
        [SerializeField] private RectTransform slidePanel;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private Transform contentParent;
        [SerializeField] private Button closeButton;
        [SerializeField] private float slideDuration = 0.25f;
        [SerializeField] private float slideHiddenX = 600f;

        [SerializeField] private EvidenceUIController evidencePanel;
        [SerializeField] private EntityListUI entityListPanel;
        [SerializeField] private JournalAudio journalAudio;

        private JournalTab _activeTab = JournalTab.Case;
        private readonly List<GameObject> _contentItems = new List<GameObject>();
        private Vector2 _shownPosition;
        private bool _open;

        public void BindRuntime(RectTransform slidePanel, Button[] tabButtons, Transform contentParent, Button closeButton)
        {
            this.slidePanel = slidePanel;
            this.tabButtons = tabButtons;
            this.contentParent = contentParent;
            this.closeButton = closeButton;
            EnsureSubControllers();
            WireButtons();
        }

        private void OnEnable()
        {
            Open(true);
            SelectTab(JournalTab.Case);
            GameEvents.OnEvidenceDetected += OnEvidenceDetected;
            GameEvents.OnObjectiveCompleted += OnObjectiveCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnEvidenceDetected -= OnEvidenceDetected;
            GameEvents.OnObjectiveCompleted -= OnObjectiveCompleted;
            Close(true);
        }

        private void Start()
        {
            EnsureSubControllers();
            WireButtons();
            if (slidePanel != null)
                _shownPosition = slidePanel.anchoredPosition;
        }

        private void EnsureSubControllers()
        {
            if (contentParent == null) return;
            if (journalAudio == null)
                journalAudio = GetComponent<JournalAudio>();
            if (evidencePanel == null)
            {
                var go = new GameObject("EvidencePanel");
                go.transform.SetParent(contentParent, false);
                evidencePanel = go.AddComponent<EvidenceUIController>();
                evidencePanel.BuildRuntime(contentParent);
            }
            if (entityListPanel == null)
            {
                var go = new GameObject("EntityListPanel");
                go.transform.SetParent(contentParent, false);
                entityListPanel = go.AddComponent<EntityListUI>();
                entityListPanel.BuildRuntime(contentParent);
            }
            evidencePanel.OnFilterChanged += types => entityListPanel?.ApplyEvidenceFilter(types);
        }

        private void WireButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() =>
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.Hide(UIScreen.Journal);
                });
            }

            if (tabButtons == null) return;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int index = i;
                tabButtons[i].onClick.RemoveAllListeners();
                tabButtons[i].onClick.AddListener(() => SelectTab((JournalTab)index));
            }
        }

        public void Open(bool instant = false)
        {
            _open = true;
            gameObject.SetActive(true);
            journalAudio?.PlayOpen();
            if (slidePanel == null) return;
            StopAllCoroutines();
            if (instant)
                slidePanel.anchoredPosition = _shownPosition;
            else
                StartCoroutine(SlideTo(_shownPosition));
        }

        public void Close(bool instant = false)
        {
            _open = false;
            journalAudio?.PlayClose();
            if (slidePanel == null)
            {
                gameObject.SetActive(false);
                return;
            }
            StopAllCoroutines();
            var hidden = _shownPosition + new Vector2(slideHiddenX, 0);
            if (instant)
            {
                slidePanel.anchoredPosition = hidden;
                gameObject.SetActive(false);
            }
            else
                StartCoroutine(SlideTo(hidden, deactivateAfter: true));
        }

        public void SelectTab(JournalTab tab)
        {
            _activeTab = tab;
            ClearContent();
            evidencePanel?.SetVisible(tab == JournalTab.Evidence || tab == JournalTab.Entities);
            entityListPanel?.SetVisible(tab == JournalTab.Entities);

            switch (tab)
            {
                case JournalTab.Case:
                    PopulateCaseTab();
                    break;
                case JournalTab.Evidence:
                    evidencePanel?.Refresh();
                    break;
                case JournalTab.Entities:
                    evidencePanel?.Refresh();
                    entityListPanel?.Refresh();
                    break;
                case JournalTab.Photos:
                    PopulatePhotosTab();
                    break;
                case JournalTab.Objectives:
                    PopulateObjectivesTab();
                    break;
            }
        }

        private void PopulateCaseTab()
        {
            string caseName = GameManager.Instance?.CurrentMission?.LocationName ?? "Unknown Case";
            AddContentLine($"CASE FILE: {caseName}");
            AddContentLine("Status: Active Investigation");
            if (GameManager.Instance?.CurrentMission?.AssignedGhost != null)
                AddContentLine($"Suspected Entity: {GameManager.Instance.CurrentMission.AssignedGhost.DisplayName}");
        }

        private void PopulatePhotosTab()
        {
            var evidence = EvidenceManager.Instance;
            if (evidence == null || evidence.Photos.Count == 0)
            {
                AddContentLine("No photos captured yet.");
                return;
            }

            foreach (var photo in evidence.Photos)
            {
                if (photo == null) continue;
                AddContentLine($"[{photo.Stars}★] {photo.Caption}");
            }
        }

        private void PopulateObjectivesTab()
        {
            var mission = GameManager.Instance?.CurrentMission;
            if (mission == null || mission.OptionalObjectivesCompleted <= 0)
            {
                AddContentLine("Locate evidence and identify the entity.");
                AddContentLine("Capture photographic proof.");
                AddContentLine("Survive until extraction.");
                return;
            }

            AddContentLine($"Optional objectives completed: {mission.OptionalObjectivesCompleted}");
        }

        private void AddContentLine(string text)
        {
            if (contentParent == null) return;
            var label = RuntimeUIFactory.CreateText(contentParent, "Line", text, 20, TextAnchor.UpperLeft);
            var rect = label.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 32);
            _contentItems.Add(label.gameObject);
        }

        private void ClearContent()
        {
            foreach (var item in _contentItems)
            {
                if (item != null)
                    Destroy(item);
            }
            _contentItems.Clear();
        }

        private void OnEvidenceDetected(EvidenceType type)
        {
            if (_activeTab == JournalTab.Evidence)
                evidencePanel?.Refresh();
        }

        private void OnObjectiveCompleted(string id)
        {
            if (_activeTab == JournalTab.Objectives)
                SelectTab(JournalTab.Objectives);
        }

        private System.Collections.IEnumerator SlideTo(Vector2 target, bool deactivateAfter = false)
        {
            if (slidePanel == null) yield break;
            Vector2 start = slidePanel.anchoredPosition;
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / slideDuration);
                slidePanel.anchoredPosition = Vector2.Lerp(start, target, p);
                yield return null;
            }
            slidePanel.anchoredPosition = target;
            if (deactivateAfter)
                gameObject.SetActive(false);
        }
    }
}
