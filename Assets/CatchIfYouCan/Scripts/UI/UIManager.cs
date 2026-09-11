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
        LobbyBoard,
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
            LocalPlayerService.PlayerRegistered += HandleLocalPlayerRegistered;
        }

        private void OnDisable()
        {
            GameEvents.OnMissionComplete -= HandleMissionComplete;
            GameEvents.OnMissionFailed -= HandleMissionFailed;
            GameEvents.OnInvestigationStarted -= HandleInvestigationStarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            LocalPlayerService.PlayerRegistered -= HandleLocalPlayerRegistered;
        }

        private void HandleMissionComplete() => Show(UIScreen.MissionComplete, false);
        private void HandleMissionFailed() => Show(UIScreen.MissionFailed, false);
        private void HandleInvestigationStarted() => Show(UIScreen.HUD);

        /// <summary>
        /// The HUD belongs to the PLAYER, so it comes up when there is one.
        ///
        /// <para>
        /// <b>It used to come up when a mission started.</b> That is one line - the handler
        /// above - and it is why the inventory row was invisible in the lobby: the HUD root is
        /// built at boot and registered inactive, <c>MobileHUDController.OnEnable</c> cannot run
        /// while it is inactive, and the lobby installer's own request is refused because the UI
        /// root already existed. So the player could pick the DOTS projector up, and had nowhere
        /// to see that they had. The lobby is the preparation area; a bag the player cannot look
        /// into there is a bag they will pack blind.
        /// </para>
        ///
        /// <para>
        /// Only from <see cref="UIScreen.None"/>, which is exactly the state both routes into
        /// the lobby leave behind - each calls <see cref="HideAll"/> before spawning the player.
        /// Anything else means a screen already owns the display, and a HUD drawn over the
        /// cinematic menu or over the board would be this method deciding something that is not
        /// its business.
        /// </para>
        /// </summary>
        private void HandleLocalPlayerRegistered()
        {
            if (_current != UIScreen.None)
                return;

            Show(UIScreen.HUD, false);
        }
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
            if (screen == _current)
            {
                if (_screens.TryGetValue(screen, out var currentRoot) && currentRoot != null)
                    currentRoot.SetActive(true);
                return;
            }

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
