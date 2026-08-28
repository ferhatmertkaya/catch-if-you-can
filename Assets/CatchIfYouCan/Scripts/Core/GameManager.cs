using UnityEngine;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Save;
using CatchIfYouCan.Graphics;
using CatchIfYouCan.Audio;

namespace CatchIfYouCan.Core
{
    public enum GameState
    {
        Boot,
        MainMenu,
        Loading,
        InVan,
        Investigating,
        Hunt,
        Paused,
        MissionComplete,
        MissionFailed
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private float targetFramerate = 60f;
        public GameState State { get; private set; } = GameState.Boot;
        public MissionRuntime CurrentMission { get; private set; }
        public bool IsPaused { get; private set; }
        public bool Invincible { get; set; }
        public string BuildVersion => Application.version;

        private float _prePauseTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = Mathf.RoundToInt(targetFramerate);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;
        }

        private void Start()
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.Load();
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.ApplyAll();
            SetState(GameState.MainMenu);
        }

        public void SetState(GameState state)
        {
            State = state;
            if (CIYCLog.Enabled)
                CIYCLog.Info($"GameState -> {state}");
        }

        public void BeginMission(MissionRuntime runtime)
        {
            CurrentMission = runtime;
            SetState(GameState.InVan);
            GameEvents.InvestigationStarted();
        }

        public void SetInvestigating() => SetState(GameState.Investigating);

        public void PauseGame()
        {
            if (IsPaused) return;
            IsPaused = true;
            _prePauseTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
            AudioBootstrap.HandlePauseChanged(true);
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = _prePauseTimeScale <= 0f ? 1f : _prePauseTimeScale;
            SetState(GameState.Investigating);
            AudioBootstrap.HandlePauseChanged(false);
        }

        public void CompleteMission()
        {
            SetState(GameState.MissionComplete);
            GameEvents.MissionComplete();
            if (CurrentMission != null && SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.Statistics.SuccessfulCases++;
                SaveManager.Instance.Data.Statistics.Investigations++;
                SaveManager.Instance.Save();
            }
        }

        public void FailMission()
        {
            if (Invincible) return;
            SetState(GameState.MissionFailed);
            GameEvents.MissionFailed();
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.Statistics.Deaths++;
                SaveManager.Instance.Data.Statistics.Investigations++;
                SaveManager.Instance.Save();
            }
        }

        public void SetTargetFramerate(int fps)
        {
            targetFramerate = fps;
            Application.targetFrameRate = fps;
        }
    }
}
