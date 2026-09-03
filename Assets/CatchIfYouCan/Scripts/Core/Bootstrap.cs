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
        private void Start()
        {
            // Created first and before anything else so the screen is black from the very first
            // frame: EnsureManagers builds the runtime UI canvas, and none of that may be
            // visible even for a frame before the intro.
            var intro = StartupIntroVideo.Create();

            EnsureManagers();

            // Started on the intro object, not on this one. Bootstrap is destroyed with 00_Boot
            // when the menu loads, which would strand the coroutine and leave the screen black.
            intro.StartCoroutine(intro.Sequence(CiycScenes.MainMenu, ShowHeadphonesTip));
        }

        private static void ShowHeadphonesTip()
        {
            if (PlayerPrefs.GetInt("ciyc_headphones_tip", 0) == 1)
                return;

            PlayerPrefs.SetInt("ciyc_headphones_tip", 1);
            GameEvents.TipRequested("BEST EXPERIENCED WITH HEADPHONES");
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
    }
}
