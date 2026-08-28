using System;
using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.UI
{
    public enum UIScreen
    {
        None,
        MainMenu,
        MissionSelect,
        HUD,
        Journal,
        Pause,
        Settings,
        MissionComplete,
        MissionFailed,
        EntityDiscovered,
        EquipmentShop,
        CameraMonitor,
        Loading,
        InteractionPrompt,
        Debug
    }

    public class UIManager : SingletonBehaviour<UIManager>
    {

        private readonly Dictionary<UIScreen, GameObject> _screens = new Dictionary<UIScreen, GameObject>();
        private UIScreen _current = UIScreen.None;
        private UIScreen _previous = UIScreen.None;

        public UIScreen CurrentScreen => _current;
        public event Action<UIScreen, UIScreen> OnScreenChanged;

        protected override void Awake()
        {
            persist = true;
            base.Awake();
        }

        private void OnEnable()
        {
            GameEvents.OnMissionComplete += HandleMissionComplete;
            GameEvents.OnMissionFailed += HandleMissionFailed;
            GameEvents.OnInvestigationStarted += HandleInvestigationStarted;
            GameEvents.OnPlayerDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            GameEvents.OnMissionComplete -= HandleMissionComplete;
            GameEvents.OnMissionFailed -= HandleMissionFailed;
            GameEvents.OnInvestigationStarted -= HandleInvestigationStarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        private void HandleMissionComplete() => Show(UIScreen.MissionComplete, false);
        private void HandleMissionFailed() => Show(UIScreen.MissionFailed, false);
        private void HandleInvestigationStarted() => Show(UIScreen.HUD);
        private void HandlePlayerDied()
        {
            if (GameManager.Instance != null && !GameManager.Instance.Invincible)
                GameManager.Instance.FailMission();
        }

        public void RegisterScreen(UIScreen screen, GameObject root)
        {
            if (root == null) return;
            _screens[screen] = root;
            root.SetActive(screen == _current);
        }

        public void UnregisterScreen(UIScreen screen)
        {
            _screens.Remove(screen);
        }

        public bool TryGetScreen(UIScreen screen, out GameObject root)
        {
            return _screens.TryGetValue(screen, out root);
        }

        public void Show(UIScreen screen, bool hideOthers = true)
        {
            if (screen == _current) return;

            _previous = _current;
            if (hideOthers)
            {
                foreach (var pair in _screens)
                {
                    if (pair.Value != null)
                        pair.Value.SetActive(pair.Key == screen);
                }
            }
            else if (_screens.TryGetValue(screen, out var target) && target != null)
            {
                target.SetActive(true);
            }

            _current = screen;
            OnScreenChanged?.Invoke(_previous, _current);
        }

        public void Hide(UIScreen screen)
        {
            if (_screens.TryGetValue(screen, out var root) && root != null)
                root.SetActive(false);
            if (_current == screen)
                _current = UIScreen.None;
        }

        public void HideAll()
        {
            foreach (var pair in _screens)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(false);
            }
            _previous = _current;
            _current = UIScreen.None;
        }

        public void ShowPrevious()
        {
            if (_previous != UIScreen.None)
                Show(_previous);
        }

        public void Toggle(UIScreen screen)
        {
            if (_current == screen)
                Hide(screen);
            else
                Show(screen);
        }

        public bool IsVisible(UIScreen screen) =>
            _screens.TryGetValue(screen, out var root) && root != null && root.activeSelf;
    }
}
