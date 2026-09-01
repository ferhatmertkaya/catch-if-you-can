using System.Collections;
using CatchIfYouCan.Art;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Owns the one-way handover from the cinematic main menu to the interactive room.
    ///
    /// <para>
    /// The menu is a fixed camera looking at a diorama; the interactive room is a walkable
    /// space. This component is the only thing that knows both exist. It does not run either of
    /// them — the horror director still owns its events, the phone still owns its audio, the
    /// player controller still owns movement. It only tells them that cinematic mode is over,
    /// which is what keeps the "did the menu end yet" question in one place instead of spread
    /// through every system as a flag.
    /// </para>
    ///
    /// <para>
    /// The handover happens exactly once. The guard is the state itself rather than a bool per
    /// step, so a second tap during the fade cannot spawn a second player, switch the camera
    /// twice, or shut the audio down twice.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Mode Controller")]
    public sealed class MainMenuModeController : MonoBehaviour
    {
        public enum MenuMode
        {
            CinematicMainMenu,
            TransitioningToInteractive,
            InteractiveRoom
        }

        [Header("Cinematic systems handed over")]
        [Tooltip("The horror event director. Told once that cinematic mode has ended; it cancels " +
                 "whatever is mid-flight, restores baselines and never schedules again.")]
        [SerializeField] private MainMenuHorrorEventDirector horrorDirector;

        [Tooltip("The phone's ring player. Any ring in progress is cut when the handover starts.")]
        [SerializeField] private RotaryPhoneRandomRing phoneRing;

        [Tooltip("Scene AudioSources that belong to the cinematic menu and must fall silent. " +
                 "The phone source belongs here. Global audio is not touched.")]
        [SerializeField] private AudioSource[] cinematicAudioSources = new AudioSource[0];

        [Tooltip("Cinematic-only UI roots hidden at handover — the branding canvas that carries " +
                 "the logo and the TAP TO START label.")]
        [SerializeField] private GameObject[] cinematicUiRoots = new GameObject[0];

        [Header("Cinematic camera")]
        [Tooltip("The menu camera. Its Camera and AudioListener are disabled at handover; the " +
                 "GameObject is left alone so the handover stays reversible later.")]
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private AudioListener cinematicAudioListener;

        [Header("Interactive room")]
        [Tooltip("Where the player is placed. A marker in the scene, so the spawn can be moved " +
                 "without touching code.")]
        [SerializeField] private Transform playerSpawn;

        [Tooltip("Room objects switched on at handover. The room is inactive while the menu " +
                 "plays so it costs nothing and cannot be seen from the menu camera.")]
        [SerializeField] private GameObject[] interactiveRoomRoots = new GameObject[0];

        [Tooltip("Chooses the night outside the window and puts it on the player's camera. The " +
                 "sky is per-camera on purpose, so the cinematic menu never inherits one.")]
        [SerializeField] private CatchIfYouCan.Art.InteractiveRoomExterior roomExterior;

        [Tooltip("The interactive room's spatial ambience. Left empty the room is simply silent.")]
        [SerializeField] private CatchIfYouCan.Audio.InteractiveRoomAmbience roomAmbience;

        [Header("Transition")]
        [Tooltip("Seconds to fade down to black before the swap.")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

        [Tooltip("Seconds to fade the interactive room up.")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.3f;

        [Tooltip("How long the menu music takes to reach silence. Long enough to read as the " +
                 "cinematic letting go rather than someone pulling the plug; the visual fade is " +
                 "shorter, so the last of the music plays under a black screen.")]
        [SerializeField, Min(0f)] private float musicFadeDuration = 1.2f;

        [Tooltip("The phone gets a shorter fade than the music. It is a diegetic sound tied to " +
                 "an object the player is leaving behind, so it should go first, but cutting it " +
                 "on the frame of the tap is audible as a click.")]
        [SerializeField, Min(0f)] private float phoneFadeDuration = 0.35f;

        [Tooltip("Decibels the fade travels before the source is stopped. -60 dB is inaudible " +
                 "under anything; fading amplitude linearly instead sounds like a sudden drop " +
                 "followed by a long tail.")]
        [SerializeField] private float fadeFloorDecibels = -60f;

        [Tooltip("On PC, lock and hide the cursor once the room is live.")]
        [SerializeField] private bool lockCursorInRoom = true;

        [Header("Debug")]
        [SerializeField] private bool logTransition = true;

        private Canvas _fadeCanvas;
        private Image _fadeImage;
        private PlayerBuildResult _player;

        /// <summary>Which half of the menu is live. Read by the tap handler.</summary>
        public MenuMode Mode { get; private set; } = MenuMode.CinematicMainMenu;

        /// <summary>True only while the cinematic menu is up and a tap would do something.</summary>
        public bool CanEnterInteractiveRoom => Mode == MenuMode.CinematicMainMenu;

        private void Awake()
        {
            // The room is dormant until it is needed. Doing this here rather than in the scene
            // means the menu is never rendering or simulating a room nobody can see.
            SetRoomActive(false);
        }

        /// <summary>
        /// Starts the one-way handover. Safe to call repeatedly: everything after the first call
        /// is ignored until — and after — the transition completes.
        /// </summary>
        public void EnterInteractiveRoom()
        {
            if (!CanEnterInteractiveRoom)
                return;

            Mode = MenuMode.TransitioningToInteractive;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            if (logTransition)
                Debug.Log("[CIYC] Menu: entering interactive room", this);

            // 1. The music starts leaving on the frame of the tap, running alongside everything
            //    below rather than after it. Waiting until the screen is already black to begin
            //    fading is what made the old transition sound like a cut.
            var musicFade = StartCoroutine(FadeOutMenuMusic());
            var sourceFades = StartCoroutine(FadeOutCinematicSources());

            // 2. The label goes next, so a player hammering the screen sees it stop responding
            //    immediately rather than during the fade.
            SetCinematicUiActive(false);

            // 3. Black before anything moves, so the camera swap and the spawn are never seen.
            BuildFadeOverlay();
            yield return Fade(0f, 1f, fadeOutDuration);

            // 4. Cinematic mode ends while the screen is covered. The director cancels a
            //    mid-flight event and restores its baselines, so the room is never entered with
            //    red lights up, the ghost displaced or the fog still agitated. This is visual
            //    state only; the audio is already on its way out above.
            if (horrorDirector != null)
                horrorDirector.StopCinematicMode();

            // 5. Wait for silence before handing over. The room is quiet, so arriving in it while
            //    the menu is still audible would be the one thing that gives the seam away.
            yield return musicFade;
            yield return sourceFades;

            // 6. Only now is the phone told to stop for good. Its source has already faded, so
            //    this is bookkeeping rather than a cut, and it is what guarantees a ring that was
            //    mid-flight cannot survive into the room.
            if (phoneRing != null)
                phoneRing.StopRinging();

            for (int i = 0; i < cinematicAudioSources.Length; i++)
            {
                if (cinematicAudioSources[i] == null)
                    continue;

                cinematicAudioSources[i].Stop();
                // Put the authored volume back now the source is silent, so the scene asset is
                // left as it was serialized rather than permanently at zero.
                if (_cinematicSourceVolumes != null && i < _cinematicSourceVolumes.Length)
                    cinematicAudioSources[i].volume = _cinematicSourceVolumes[i];
            }

            if (UIManager.Instance != null)
                UIManager.Instance.HideAll();

            // 7. The room exists before the player is put in it.
            SetRoomActive(true);

            // 8. Listener first, then the player. The player builds its own AudioListener, so
            //    silencing the menu's before it exists is what guarantees there is never a frame
            //    with two enabled listeners.
            if (cinematicAudioListener != null)
                cinematicAudioListener.enabled = false;

            SpawnPlayer();

            // The sky goes onto the player's camera, never onto RenderSettings: a global one
            // would start feeding ambient into a room lit without it, and would still be there
            // if the player ever came back to the menu. Applied before the reveal, so the first
            // frame seen through the window is already the right night.
            if (roomExterior != null && _player != null)
                roomExterior.ApplyTo(_player.ViewCamera);

            // The room's own soundscape, started with the player it follows. It holds no sources
            // and schedules nothing until this call, so the cinematic menu never hears it.
            if (roomAmbience != null && _player != null && _player.Root != null)
                roomAmbience.Begin(_player.Root.transform);

            // 8. Only now does the menu camera stop rendering — the player camera already exists,
            //    so no frame is drawn with no camera at all.
            if (cinematicCamera != null)
                cinematicCamera.enabled = false;

            Mode = MenuMode.InteractiveRoom;

            // 10. Reveal, then arm input. Enabling movement and look before the fade finishes is
            //     what would let the tap that started all this carry through as a look delta,
            //     and showing the controls over a black screen would look like a glitch.
            yield return Fade(1f, 0f, fadeInDuration);

            if (_player != null && _player.TouchHud != null)
                _player.TouchHud.SetActive(true);

            EnablePlayerInput(true);
            DestroyFadeOverlay();

            if (logTransition)
                Debug.Log("[CIYC] Menu: interactive room live", this);
        }

        private void SpawnPlayer()
        {
            if (_player != null)
                return;

            Vector3 position = playerSpawn != null ? playerSpawn.position : transform.position;
            Quaternion rotation = playerSpawn != null ? playerSpawn.rotation : Quaternion.identity;

            _player = PlayerFactory.Create(position, rotation);

            // Placed by the factory at construction, so there is no teleport of an already-live
            // CharacterController to work around and no accumulated fall velocity to clear.
            // Movement and look stay off until the fade is done.
            EnablePlayerInput(false);

            var look = _player.CameraRoot != null ? _player.CameraRoot.GetComponent<PlayerLook>() : null;
            if (look != null)
                look.SnapTo(rotation, 0f);
        }

        private void EnablePlayerInput(bool enabled)
        {
            if (_player == null || _player.Root == null)
                return;

            var controller = _player.Root.GetComponent<PlayerController>();
            if (controller != null)
                controller.MovementEnabled = enabled;

            var look = _player.CameraRoot != null ? _player.CameraRoot.GetComponent<PlayerLook>() : null;
            if (look != null)
                look.AllowLook = enabled;

            if (lockCursorInRoom && !Application.isMobilePlatform)
            {
                // Reversible: a future return to the menu sets these back rather than being
                // stuck with a locked cursor.
                Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !enabled;
            }
        }

        private void SetRoomActive(bool active)
        {
            for (int i = 0; i < interactiveRoomRoots.Length; i++)
                if (interactiveRoomRoots[i] != null)
                    interactiveRoomRoots[i].SetActive(active);
        }

        private void SetCinematicUiActive(bool active)
        {
            for (int i = 0; i < cinematicUiRoots.Length; i++)
                if (cinematicUiRoots[i] != null)
                    cinematicUiRoots[i].SetActive(active);
        }

        /// <summary>
        /// Converts a normalised fade position into a volume multiplier.
        ///
        /// <para>
        /// The travel happens in decibels, not in amplitude. Halving amplitude only drops
        /// loudness by 6 dB, so a straight lerp to zero sounds like the music dives and then
        /// hangs around barely audible for most of the fade. Sweeping the gain down a dB scale
        /// instead is what makes the level appear to fall evenly, and the SmoothStep takes the
        /// corners off both ends so neither the start nor the arrival at silence is a step.
        /// </para>
        /// </summary>
        private float FadeCurve(float t)
        {
            float decibels = Mathf.Lerp(0f, fadeFloorDecibels, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            return Mathf.Pow(10f, decibels / 20f);
        }

        private IEnumerator FadeOutMenuMusic()
        {
            var audio = AudioManager.Instance;
            if (audio == null)
                yield break;

            float start = audio.GetMusicVolume();
            for (float e = 0f; e < musicFadeDuration; e += Time.unscaledDeltaTime)
            {
                audio.SetMusicVolume(start * FadeCurve(e / Mathf.Max(0.0001f, musicFadeDuration)));
                yield return null;
            }

            // A null clip is this manager's documented way of stopping the music source. Nothing
            // else on the manager is touched, so UI sounds and anything the room needs later
            // still work, and the stored volume is put back so a future return to the menu is
            // not silent.
            audio.SetMusicVolume(0f);
            audio.PlayMusic(null);
            audio.SetMusicVolume(start);
        }

        /// <summary>
        /// Fades the scene's cinematic sources — in practice the phone — then leaves them at
        /// zero. Stopping them is done by the caller once the handover is committed, so a ring
        /// that was sounding at the moment of the tap dies away instead of clicking off.
        /// </summary>
        private IEnumerator FadeOutCinematicSources()
        {
            if (cinematicAudioSources.Length == 0 || phoneFadeDuration <= 0f)
                yield break;

            var starts = new float[cinematicAudioSources.Length];
            for (int i = 0; i < cinematicAudioSources.Length; i++)
                starts[i] = cinematicAudioSources[i] != null ? cinematicAudioSources[i].volume : 0f;

            for (float e = 0f; e < phoneFadeDuration; e += Time.unscaledDeltaTime)
            {
                float k = FadeCurve(e / phoneFadeDuration);
                for (int i = 0; i < cinematicAudioSources.Length; i++)
                    if (cinematicAudioSources[i] != null)
                        cinematicAudioSources[i].volume = starts[i] * k;
                yield return null;
            }

            for (int i = 0; i < cinematicAudioSources.Length; i++)
                if (cinematicAudioSources[i] != null)
                    cinematicAudioSources[i].volume = 0f;

            // The authored volumes are restored after the stop, so the sources are left exactly
            // as the scene serialized them and a later menu is not silent.
            _cinematicSourceVolumes = starts;
        }

        private float[] _cinematicSourceVolumes;

        // ---- fade overlay ----------------------------------------------------------------

        private void BuildFadeOverlay()
        {
            if (_fadeCanvas != null)
                return;

            var go = new GameObject("MainMenu_TransitionFade");
            go.transform.SetParent(transform, false);

            _fadeCanvas = go.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the menu canvases so nothing shows through mid-swap.
            _fadeCanvas.sortingOrder = 500;

            var imageGo = new GameObject("Fade", typeof(RectTransform));
            imageGo.transform.SetParent(go.transform, false);
            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _fadeImage = imageGo.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            _fadeImage.raycastTarget = true;   // swallows further taps while transitioning
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_fadeImage == null)
                yield break;

            if (duration <= 0f)
            {
                SetFadeAlpha(to);
                yield break;
            }

            for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
            {
                SetFadeAlpha(Mathf.Lerp(from, to, e / duration));
                yield return null;
            }
            SetFadeAlpha(to);
        }

        private void SetFadeAlpha(float a)
        {
            var c = _fadeImage.color;
            c.a = Mathf.Clamp01(a);
            _fadeImage.color = c;
        }

        private void DestroyFadeOverlay()
        {
            if (_fadeCanvas != null)
                Destroy(_fadeCanvas.gameObject);
            _fadeCanvas = null;
            _fadeImage = null;
        }
    }
}
