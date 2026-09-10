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

        /// <summary>This screen's name in <see cref="MenuInputGate"/>. One name, one hold.</summary>
        private const string GateOwner = "MissionSelectUI";

        private readonly List<Button> _missionButtons = new List<Button>();

        /// <summary>
        /// The mission each button stands for, in the same order. Kept alongside rather than
        /// derived from the index into <c>missions</c>, because the build loop skips null
        /// entries and the two lists would then quietly disagree about which row is selected.
        /// </summary>
        private readonly List<MissionDefinition> _buttonMissions = new List<MissionDefinition>();

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
            // Taken here rather than by whoever opened this screen. A fullscreen menu is
            // responsible for its own suppression of the touch HUD, wherever it was opened
            // from - the lobby board used to do it on this screen's behalf and hand the
            // controls back before showing it, which is how the joystick, sprint, crouch and
            // interact buttons ended up sitting on top of the mission list.
            MenuInputGate.Push(GateOwner);

            if (missions.Count == 0)
                LoadMissions();
            BuildMissionList();
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionSelect, false);
        }

        private void OnDisable()
        {
            MenuInputGate.Pop(GateOwner);
        }

        /// <summary>
        /// Back, on the same three routes as the lobby board: the on-screen button, Escape, and
        /// the gamepad cancel. Unity reports Android's system back key as Escape, so the
        /// hardware button reaches the same single path rather than a second one of its own.
        /// </summary>
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) ||
                UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                RequestBack();
            }
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
                backButton.onClick.AddListener(RequestBack);
            }
        }

        private static bool _authoredCatalogMissingReported;

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

            // The authored assets are the intended source; the code factory is the safety
            // net. Which one is live must not be invisible. Reported once per session.
            if (!_authoredCatalogMissingReported)
            {
                _authoredCatalogMissingReported = true;
                Core.CIYCLog.Warn("No MissionDefinition assets under Resources/Missions. " +
                                  "Falling back to MissionDefinitionFactory defaults.");
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
            _buttonMissions.Clear();

            for (int i = 0; i < missions.Count; i++)
            {
                var mission = missions[i];
                if (mission == null) continue;
                var captured = mission;

                // Every row is built the same. The chosen one is marked afterwards, by its
                // accent and its text, not by being built as a different kind of button - which
                // is what "the first one is primary" used to mean and why the first mission
                // looked selected whatever the player had actually picked.
                var btn = RuntimeUIFactory.CreateButton(missionListParent, mission.MapName,
                    () => SelectMission(captured), false, 64f);
                _missionButtons.Add(btn);
                _buttonMissions.Add(captured);
            }

            if (_selected == null && missions.Count > 0)
                SelectMission(missions[0]);
            else if (_selected != null)
                RefreshDetail();

            RefreshSelectionVisuals();
        }

        private void SelectMission(MissionDefinition mission)
        {
            _selected = mission;
            MissionManager.Instance?.SelectMission(mission);
            RefreshDetail();
            RefreshSelectionVisuals();
        }

        /// <summary>
        /// Marks the chosen row: white text and a thin green edge. The others stay dark with a
        /// neutral hairline.
        ///
        /// <para>
        /// Deliberately not a fill. A selected row painted solid brand green is unreadable at
        /// arm's length on a phone and turns the list into the loudest thing on the screen,
        /// which is the opposite of what a selection should do.
        /// </para>
        /// </summary>
        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < _missionButtons.Count && i < _buttonMissions.Count; i++)
            {
                Button button = _missionButtons[i];
                if (button == null)
                    continue;

                bool chosen = _buttonMissions[i] == _selected;

                var feedback = button.GetComponent<UIButtonFeedback>();
                if (feedback != null)
                    feedback.SetSelected(chosen);

                UITheme.ApplyBorder(button.gameObject,
                    chosen ? UITheme.AccentBorder : UITheme.Border,
                    UITheme.PanelBorderWidth);

                Component label = RuntimeUIFactory.FindLabel(button);
                UITheme.SetTextColor(label, chosen ? UITheme.TextPrimary : UITheme.TextMuted);
            }
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

        /// <summary>
        /// Returns the mission authority, creating it if the menu scene has none.
        /// InvestigationBootstrap.EnsureManagers already does exactly this for the
        /// investigation scene; the menu needs it too so that starting a mission from here
        /// goes through the same authority rather than around it.
        /// </summary>
        private static MissionManager EnsureMissionManager()
        {
            if (MissionManager.Instance != null)
                return MissionManager.Instance;

            var go = new GameObject("MissionManager");
            go.AddComponent<MissionManager>();
            return MissionManager.Instance;
        }

        /// <summary>
        /// Accepts the mission and opens the lobby doorway. It does <b>not</b> load a scene.
        ///
        /// <para>
        /// <b>This is the fix for "nothing happens when I press START INVESTIGATION".</b> The
        /// method used to call <c>SceneLoader.LoadInvestigation()</c> here, which cuts straight
        /// to the investigation behind a loading screen - so the doorway the player was meant
        /// to walk through never had a chance to become anything, and no amount of portal code
        /// would have shown one, because nothing on this path ever asked for a portal.
        /// </para>
        ///
        /// <para>
        /// Accepting the mission now opens the door; the player starts the investigation by
        /// walking through it. The two are separate moments and the second one is theirs.
        /// </para>
        ///
        /// <para>
        /// <b>The direct load survives as a named alternative, not as a silent fallback.</b> A
        /// scene with no lobby doorway - training, or a build that opens mission select without
        /// ever passing through the lobby - has nothing to open, and the mission still has to
        /// start. That path says so at warning level every time it is taken.
        /// </para>
        /// </summary>
        private void StartInvestigation()
        {
            if (_selected == null)
            {
                CIYCLog.Warn("[CIYC][Portal] START INVESTIGATION pressed with no mission " +
                             "selected. Nothing to open the doorway onto.");
                return;
            }

            // The session seed is host-authoritative and must be rolled in exactly one place
            // (Docs/NETWORKING.md §3). This branch used to call SessionSeedSource.Next()
            // itself, which made the menu a second, client-side seed source — and since
            // SceneAutoSetup does not put a MissionManager in the menu scene, it was usually
            // the live one. Ensuring the manager routes the start through the single
            // authoritative path, which also picks the ghost that the old fallback left null.
            var missions = EnsureMissionManager();
            MissionRuntime runtime = missions != null
                ? missions.StartInvestigation(_selected)
                : null;

            if (runtime == null)
                CIYCLog.Warn("MissionSelectUI: could not start an investigation - no MissionManager.");

            LoadRecommendedEquipment();

            if (CatchIfYouCan.Environment.LobbyPortal.Instance != null)
            {
                if (!CatchIfYouCan.Environment.LobbyPortal.TryOpenForMission(_selected.MapName))
                {
                    // TryOpenForMission has already said why, at error level. The screen stays
                    // up rather than closing onto an unchanged wall, so the player is not left
                    // wondering whether their press registered.
                    return;
                }

                // Closing this screen releases the input gate, which gives the player back
                // their controls and their HUD - in the lobby, facing an open door.
                if (UIManager.Instance != null)
                    UIManager.Instance.Hide(UIScreen.MissionSelect);
                return;
            }

            CIYCLog.Warn("[CIYC][Portal] No lobby portal in this scene, so the investigation " +
                         "starts with a direct scene load and the player does not walk through " +
                         "a doorway. This is expected outside the lobby and nowhere else.");

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadInvestigation();
            else
                CIYCLog.Error("[CIYC][Portal] No lobby portal AND no SceneLoader: the mission " +
                              "was accepted and there is no way to reach it.");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.Hide(UIScreen.MissionSelect);
                UIManager.Instance.Show(UIScreen.HUD);
            }
        }

        /// <summary>
        /// Back to where the player came from: the lobby board if there is one, the main menu
        /// otherwise. One method, reached by the on-screen button, Escape and the gamepad
        /// cancel - there is no second back path to drift out of step with this one.
        /// </summary>
        private void RequestBack()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Hide(UIScreen.MissionSelect);

            if (LobbyBoardUI.Exists)
            {
                LobbyBoardUI.Open();
                return;
            }

            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MainMenu);
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
