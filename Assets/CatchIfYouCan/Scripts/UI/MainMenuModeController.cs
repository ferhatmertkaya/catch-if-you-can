using System.Collections;
using CatchIfYouCan.Art;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Owns the one-way handover from the cinematic main menu to the lobby.
    ///
    /// <para>
    /// The menu is a fixed camera looking at a diorama; the lobby is a walkable
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
            Lobby
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

        [Header("Lobby")]
        [Tooltip("Where the player is placed. A marker in the scene, so the spawn can be moved " +
                 "without touching code.")]
        [SerializeField] private Transform playerSpawn;

        [Tooltip("Room objects switched on at handover. The room is inactive while the menu " +
                 "plays so it costs nothing and cannot be seen from the menu camera.")]
        [SerializeField] private GameObject[] interactiveRoomRoots = new GameObject[0];

        [Tooltip("Chooses the night outside the window and puts it on the player's camera. The " +
                 "sky is per-camera on purpose, so the cinematic menu never inherits one.")]
        [SerializeField] private CatchIfYouCan.Art.LobbyExterior roomExterior;

        [Tooltip("The lobby's spatial ambience. Left empty the room is simply silent.")]
        [SerializeField] private CatchIfYouCan.Audio.LobbyAmbience roomAmbience;

        [Header("Transition")]
        [Tooltip("Seconds to fade down to black before the swap.")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

        [Tooltip("Seconds to fade the lobby up.")]
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
        public bool CanEnterLobby => Mode == MenuMode.CinematicMainMenu;

        private void Awake()
        {
            // The room is dormant until it is needed. It is also switched off in the scene
            // itself, and that is the part that matters: doing it only here left a window in
            // which everything under the room had already run its Awake and OnEnable, because
            // Unity does not define whether this runs before or after theirs. The moon light
            // claiming the scene's sun, or an emitter starting, would both have happened over
            // the menu. This call is now the belt to that braces.
            SetRoomActive(false);

            // Nothing from the room is allowed to be heard here, ever. The room being inactive
            // already guarantees it; saying so out loud costs nothing and means a future change
            // that reactivates the room early cannot quietly bring the night in with it.
            if (roomAmbience != null)
                roomAmbience.End();
        }

        /// <summary>
        /// Starts the one-way handover. Safe to call repeatedly: everything after the first call
        /// is ignored until — and after — the transition completes.
        /// </summary>
        public void EnterLobby()
        {
            if (!CanEnterLobby)
                return;

            Mode = MenuMode.TransitioningToInteractive;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            if (logTransition)
                Debug.Log("[CIYC] Menu: entering lobby", this);

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

            // 7. Once the lobby is its own scene, the hand-over is a scene load and
            //    everything below this point belongs to LobbySceneInstaller instead. The
            //    branch is on whether that scene is in the build, not on a code flag, so the
            //    cutover happens when the scene is created rather than in a second commit.
            //    Delete the legacy branch after the split lands.
            if (Core.CiycScenes.IsRegisteredInBuild(Core.CiycScenes.Lobby))
            {
                yield return HandOverToLobbyScene();
                yield break;
            }

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

            Mode = MenuMode.Lobby;

            // 10. Reveal, then arm input. Enabling movement and look before the fade finishes is
            //     what would let the tap that started all this carry through as a look delta,
            //     and showing the controls over a black screen would look like a glitch.
            yield return Fade(1f, 0f, fadeInDuration);

            PlayerSpawner.SetHudVisible(true);

            PlayerSpawner.SetInputEnabled(true);
            DestroyFadeOverlay();

            if (logTransition)
                Debug.Log("[CIYC] Menu: lobby live", this);
        }

        /// <summary>
        /// Hands over by loading the lobby scene.
        ///
        /// <para>
        /// The menu's own camera and listener are switched off first and the menu scene is
        /// then unloaded by the load itself, so there is never a frame with two listeners
        /// and never two production scenes resident at once. The fade overlay is not torn
        /// down here: it belongs to this object, which the load destroys, and the lobby
        /// raises its own HUD once its installer has run.
        /// </para>
        /// </summary>
        private System.Collections.IEnumerator HandOverToLobbyScene()
        {
            if (cinematicAudioListener != null)
                cinematicAudioListener.enabled = false;

            Mode = MenuMode.Lobby;

            if (logTransition)
                Debug.Log("[CIYC] Menu: loading the lobby scene", this);

            if (Core.SceneLoader.Instance != null)
            {
                Core.SceneLoader.Instance.LoadLobby();
            }
            else
            {
                // Only reachable when the menu was opened directly without the boot flow and
                // the services somehow did not come up. Saying so beats a silent dead tap.
                Core.CIYCLog.Error("No SceneLoader, so the lobby cannot be loaded. Enter " +
                                   "through 00_Boot, or check that CiycServices ran.");
            }

            yield break;
        }

        private void SpawnPlayer()
        {
            if (_player != null)
                return;

            // The sequence - build, hold input, snap the look - lives in PlayerSpawner now,
            // so the lobby and any later scene get it without copying it out of a menu
            // component. This still decides the two things that are the menu's business:
            // where, and that input stays off until the fade is finished.
            PlayerSpawner.LockCursorWhenEnabled = lockCursorInRoom;
            _player = PlayerSpawner.Spawn(playerSpawn, enableInput: false,
                                          contextForDiagnostics: name);
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
