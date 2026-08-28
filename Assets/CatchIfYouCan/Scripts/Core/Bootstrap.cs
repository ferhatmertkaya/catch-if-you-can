using UnityEngine;
using CatchIfYouCan.Save;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Graphics;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.UI;

namespace CatchIfYouCan.Core
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private float splashDuration = 1.8f;
        [SerializeField] private CanvasGroup splashGroup;

        private void Start()
        {
            EnsureManagers();
            StartCoroutine(SplashThenMenu());
        }

        private void EnsureManagers()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
            if (SceneLoader.Instance == null)
            {
                var go = new GameObject("SceneLoader");
                go.AddComponent<SceneLoader>();
            }
            if (SaveManager.Instance == null)
            {
                var go = new GameObject("SaveManager");
                go.AddComponent<SaveManager>();
            }
            if (SettingsManager.Instance == null)
            {
                var go = new GameObject("SettingsManager");
                go.AddComponent<SettingsManager>();
            }
            AudioBootstrap.Initialize();
            if (AudioManager.Instance == null)
            {
                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();
            }
            if (GraphicsManager.Instance == null)
            {
                var go = new GameObject("GraphicsManager");
                go.AddComponent<GraphicsManager>();
            }
            if (HapticManager.Instance == null)
            {
                var go = new GameObject("HapticManager");
                go.AddComponent<HapticManager>();
            }
            if (EquipmentManager.Instance == null)
            {
                var go = new GameObject("EquipmentManager");
                go.AddComponent<EquipmentManager>();
            }
            if (StatisticsTracker.Instance == null)
            {
                var go = new GameObject("StatisticsTracker");
                go.AddComponent<StatisticsTracker>();
            }
            if (UIManager.Instance == null)
            {
                var go = new GameObject("UIManager");
                go.AddComponent<UIManager>();
            }
            if (GameObject.Find("RuntimeUI") == null)
                RuntimeUIFactory.BuildCompleteUI();
        }

        private System.Collections.IEnumerator SplashThenMenu()
        {
            if (splashGroup != null) splashGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(splashDuration);
            if (splashGroup != null)
            {
                float t = 1f;
                while (t > 0f)
                {
                    t -= Time.unscaledDeltaTime;
                    splashGroup.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
            }
            bool headphonesShown = PlayerPrefs.GetInt("ciyc_headphones_tip", 0) == 1;
            if (!headphonesShown)
            {
                PlayerPrefs.SetInt("ciyc_headphones_tip", 1);
                GameEvents.TipRequested("BEST EXPERIENCED WITH HEADPHONES");
            }
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadMainMenu();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("01_MainMenu");
        }
    }
}
