using CatchIfYouCan.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button returnVanButton;
        [SerializeField] private Button exitButton;

        public void BindRuntime(
            Button continueButton,
            Button settingsButton,
            Button restartButton,
            Button returnVanButton,
            Button exitButton)
        {
            this.continueButton = continueButton;
            this.settingsButton = settingsButton;
            this.restartButton = restartButton;
            this.returnVanButton = returnVanButton;
            this.exitButton = exitButton;
            WireButtons();
        }

        private void OnEnable()
        {
            WireButtons();
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.Pause, false);
        }

        private void Start() => WireButtons();

        private void WireButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinue);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(() =>
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.Show(UIScreen.Settings, false);
                });
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestart);
            }
            if (returnVanButton != null)
            {
                returnVanButton.onClick.RemoveAllListeners();
                returnVanButton.onClick.AddListener(OnReturnToVan);
            }
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OnExitMission);
            }
        }

        private void OnContinue()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ResumeGame();
            if (UIManager.Instance != null)
            {
                UIManager.Instance.Hide(UIScreen.Pause);
                UIManager.Instance.Show(UIScreen.HUD, false);
            }
        }

        private void OnRestart()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }

        private void OnReturnToVan()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.InVan);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.Hide(UIScreen.Pause);
                UIManager.Instance.Show(UIScreen.CameraMonitor, false);
            }
        }

        private void OnExitMission()
        {
            Time.timeScale = 1f;
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadMainMenu();
            else
                SceneManager.LoadScene("01_MainMenu");
        }
    }
}
