using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class MissionSelectUI : MonoBehaviour
    {
        [SerializeField] private List<MissionDefinition> missions = new List<MissionDefinition>();
        [SerializeField] private Transform missionListParent;
        [SerializeField] private Component detailTitle;
        [SerializeField] private Component detailBody;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        private readonly List<Button> _missionButtons = new List<Button>();
        private MissionDefinition _selected;

        public Button StartButton => startButton;
        public Button BackButton => backButton;

        public void BindRuntime(
            Transform missionListParent,
            Component detailTitle,
            Component detailBody,
            Button startButton,
            Button backButton)
        {
            this.missionListParent = missionListParent;
            this.detailTitle = detailTitle;
            this.detailBody = detailBody;
            this.startButton = startButton;
            this.backButton = backButton;
            WireButtons();
            BuildMissionList();
        }

        private void OnEnable()
        {
            if (missions.Count == 0)
                LoadMissions();
            BuildMissionList();
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionSelect, false);
        }

        private void Start()
        {
            WireButtons();
            if (missions.Count == 0)
                LoadMissions();
            BuildMissionList();
        }

        private void WireButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(StartInvestigation);
            }
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() =>
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.Show(UIScreen.MainMenu);
                });
            }
        }

        private void LoadMissions()
        {
            missions.Clear();

            if (MissionManager.Instance != null)
            {
                var available = MissionManager.Instance.GetAvailableMissions();
                if (available != null && available.Length > 0)
                {
                    missions.AddRange(available);
                    return;
                }
            }

            var loaded = Resources.LoadAll<MissionDefinition>("Missions");
            if (loaded != null && loaded.Length > 0)
            {
                missions.AddRange(loaded);
                return;
            }

            missions.AddRange(MissionDefinitionFactory.CreateAllDefaultMissions());
        }

        private static MissionDefinition CreateRuntimeMission(MissionTheme theme, string name, int rooms, int reward)
        {
            var m = ScriptableObject.CreateInstance<MissionDefinition>();
            m.Theme = theme;
            m.MapName = name;
            m.EstimatedRoomCount = rooms;
            m.BaseReward = reward;
            m.Difficulty = DifficultyDefinition.CreatePreset(DifficultyTier.Investigator);
            m.RecommendedEquipmentIds = new[] { "emf_detector", "flashlight", "photo_camera" };
            m.Briefing = $"Investigate the {name.ToLowerInvariant()} and identify the entity.";
            return m;
        }

        private void BuildMissionList()
        {
            if (missionListParent == null) return;

            foreach (var btn in _missionButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _missionButtons.Clear();

            for (int i = 0; i < missions.Count; i++)
            {
                var mission = missions[i];
                if (mission == null) continue;
                var captured = mission;
                var btn = RuntimeUIFactory.CreateButton(missionListParent, mission.MapName,
                    () => SelectMission(captured), i == 0);
                _missionButtons.Add(btn);
            }

            if (_selected == null && missions.Count > 0)
                SelectMission(missions[0]);
            else if (_selected != null)
                RefreshDetail();
        }

        private void SelectMission(MissionDefinition mission)
        {
            _selected = mission;
            MissionManager.Instance?.SelectMission(mission);
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            if (_selected == null) return;

            UITheme.SetText(detailTitle, _selected.MapName);
            UITheme.StyleTitle(detailTitle);

            string diff = _selected.Difficulty != null
                ? _selected.Difficulty.DisplayName
                : DifficultyTier.Investigator.ToString();
            float multiplier = _selected.Difficulty != null ? _selected.Difficulty.RewardMultiplier : 1f;

            string equipment = "Recommended: ";
            if (_selected.RecommendedEquipmentIds != null && _selected.RecommendedEquipmentIds.Length > 0)
                equipment += string.Join(", ", _selected.RecommendedEquipmentIds);
            else
                equipment += "EMF, Flashlight, Photo Camera";

            string body = $"Theme: {_selected.Theme}\n" +
                          $"Difficulty: {diff}\n" +
                          $"Size: {_selected.EstimatedRoomCount} rooms\n" +
                          $"Reward: ${_selected.BaseReward:N0}\n" +
                          $"Multiplier: x{multiplier:0.00}\n\n" +
                          equipment;

            if (!string.IsNullOrEmpty(_selected.Briefing))
                body += "\n\n" + _selected.Briefing;

            UITheme.SetText(detailBody, body);
            UITheme.StyleBody(detailBody);
        }

        private void StartInvestigation()
        {
            if (_selected == null) return;

            MissionRuntime runtime = null;
            if (MissionManager.Instance != null)
                runtime = MissionManager.Instance.StartInvestigation(_selected);
            else if (GameManager.Instance != null)
                GameManager.Instance.BeginMission(MissionRuntime.Create(_selected, 1001, SessionSeedSource.Next(), null));

            LoadRecommendedEquipment();

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadInvestigation();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.Hide(UIScreen.MissionSelect);
                UIManager.Instance.Show(UIScreen.HUD);
            }
        }

        private void LoadRecommendedEquipment()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null || _selected?.RecommendedEquipmentIds == null) return;

            var defs = new List<EquipmentDefinition>();
            foreach (var id in _selected.RecommendedEquipmentIds)
            {
                var def = Resources.Load<EquipmentDefinition>($"Equipment/{id}")
                          ?? EquipmentDefinitionFactory.GetById(id);
                if (def != null)
                    defs.Add(def);
            }
            if (defs.Count > 0)
                mgr.SetLoadout(defs);
        }
    }
}
