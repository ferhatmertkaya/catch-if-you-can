using System.Collections;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Core;
using CatchIfYouCan.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Runtime References")]
        [SerializeField] private Component levelText;
        [SerializeField] private Component moneyText;
        [SerializeField] private Component versionText;
        [SerializeField] private Image flickerOverlay;
        [SerializeField] private Button playButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button journalButton;
        [SerializeField] private Button trainingButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;

        [Header("Music")]
        [SerializeField] private AudioClip menuMusic;

        [Header("Ambience")]
        [SerializeField] private float flickerMinInterval = 4f;
        [SerializeField] private float flickerMaxInterval = 12f;
        [SerializeField] private float flickerDuration = 0.08f;

        public Component LevelText => levelText;
        public Component MoneyText => moneyText;
        public Component VersionText => versionText;
        public Image FlickerOverlay => flickerOverlay;

        private Coroutine _flickerRoutine;

        public void BindRuntime(
            Component levelText,
            Component moneyText,
            Component versionText,
            Image flickerOverlay,
            Button playButton,
            Button equipmentButton,
            Button journalButton,
            Button trainingButton,
            Button settingsButton,
            Button creditsButton)
        {
            this.levelText = levelText;
            this.moneyText = moneyText;
            this.versionText = versionText;
            this.flickerOverlay = flickerOverlay;
            this.playButton = playButton;
            this.equipmentButton = equipmentButton;
            this.journalButton = journalButton;
            this.trainingButton = trainingButton;
            this.settingsButton = settingsButton;
            this.creditsButton = creditsButton;
            WireButtons();
        }

        private void OnEnable()
        {
            RefreshHeader();
            PlayMenuMusic();
            if (_flickerRoutine == null)
                _flickerRoutine = StartCoroutine(FlickerLoop());
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MainMenu, false);
        }

        private void OnDisable()
        {
            if (_flickerRoutine != null)
            {
                StopCoroutine(_flickerRoutine);
                _flickerRoutine = null;
            }
        }

        private void Start()
        {
            WireButtons();
            RefreshHeader();
        }

        private void WireButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlay);
            }
            if (equipmentButton != null)
            {
                equipmentButton.onClick.RemoveAllListeners();
                equipmentButton.onClick.AddListener(OnEquipment);
            }
            if (journalButton != null)
            {
                journalButton.onClick.RemoveAllListeners();
                journalButton.onClick.AddListener(OnJournal);
            }
            if (trainingButton != null)
            {
                trainingButton.onClick.RemoveAllListeners();
                trainingButton.onClick.AddListener(OnTraining);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettings);
            }
            if (creditsButton != null)
            {
                creditsButton.onClick.RemoveAllListeners();
                creditsButton.onClick.AddListener(OnCredits);
            }
        }

        public void RefreshHeader()
        {
            int level = 1;
            int money = 500;
            if (SaveManager.Instance != null)
            {
                level = SaveManager.Instance.Data.Level;
                money = SaveManager.Instance.Data.Money;
            }

            UITheme.SetText(levelText, $"LV {level}");
            UITheme.SetText(moneyText, $"${money:N0}");
            UITheme.SetText(versionText, $"v{Application.version}");
            UITheme.StyleTitle(levelText);
            UITheme.StyleTitle(moneyText);
        }

        private void PlayMenuMusic()
        {
            if (menuMusic == null)
                menuMusic = Resources.Load<AudioClip>("Audio/Music/Menu/ciyc_menu_main_theme");

            if (AudioManager.Instance == null || menuMusic == null)
                return;

            float volume = SettingsManager.Instance != null
                ? SettingsManager.Instance.MusicVolume
                : 0.5f;

            AudioManager.Instance.SetMusicVolume(volume);
            AudioManager.Instance.PlayMusic(menuMusic);
        }

        private void OnPlay()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(null);
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionSelect);
            else if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadInvestigation();
        }

        private void OnEquipment()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.EquipmentShop);
        }

        private void OnJournal()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.Journal);
        }

        private void OnTraining()
        {
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadTraining();
        }

        private void OnSettings()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.Settings);
        }

        private void OnCredits()
        {
            GameEvents.TipRequested("CATCH IF YOU CAN — A paranormal investigation horror experience.");
        }

        private IEnumerator FlickerLoop()
        {
            while (enabled)
            {
                float wait = Random.Range(flickerMinInterval, flickerMaxInterval);
                yield return new WaitForSecondsRealtime(wait);
                if (flickerOverlay == null || (SettingsManager.Instance != null && SettingsManager.Instance.ReduceFlicker))
                    continue;

                flickerOverlay.color = new Color(0.02f, 0.08f, 0.04f, 0.35f);
                yield return new WaitForSecondsRealtime(flickerDuration);
                flickerOverlay.color = new Color(0, 0, 0, 0);
            }
        }
    }
}
