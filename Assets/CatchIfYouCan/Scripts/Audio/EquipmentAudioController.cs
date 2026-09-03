using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    /// <summary>
    /// Gives each piece of equipment the sound it makes.
    ///
    /// <para>
    /// Most of what was here could not make a sound. <c>PlayShutter</c>, <c>PlayFocus</c>,
    /// <c>PlayNightVisionHum</c>, <c>PlayQuestion</c>, <c>PlayResponse</c>,
    /// <c>PlayStaticBurst</c>, <c>PlayPour</c> and <c>PlayCrackle</c> were public methods with
    /// no caller anywhere in the project - eight sounds, fully written, that nothing could
    /// trigger. The relic's ambience was gated on <c>IsActive</c>, which for an item with no
    /// battery is <c>IsPowered</c> and therefore permanently false. And the thermometer's
    /// helper did a <c>GetComponent</c> every frame to find a component on its own object.
    /// </para>
    ///
    /// <para>
    /// <b>The helpers watch; the items do not call them.</b> Every device already publishes the
    /// state its sound follows - a shutter cooldown, a charge count, a recording flag - so the
    /// helpers read that and fire on the edges. Gameplay classes stay free of audio, which is
    /// what lets the sound design change without touching an item.
    /// </para>
    /// </summary>
    public class EquipmentAudioController : MonoBehaviour
    {
        public static EquipmentAudioController Instance { get; private set; }

        private static readonly HashSet<string> ReportedFallbacks =
            new HashSet<string>(System.StringComparer.Ordinal);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnEquipmentChanged += HandleEquipmentChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        /// <summary>
        /// Plays one equipment sound, and says so the first time an id has no authored event.
        ///
        /// <para>
        /// The event library synthesises a generic beep for any id it does not know and
        /// reports success, so a project with no equipment audio bank sounds like a project
        /// with one. Naming the ids that fell through is the difference between placeholder
        /// audio and audio somebody thinks is finished.
        /// </para>
        /// </summary>
        public void RouteEquipEvent(string eventId, Vector3? pos = null, float scale = 1f)
        {
            var manager = AudioManager.Instance;
            if (manager == null)
                return;

            var library = manager.EventLibrary;
            if (library != null && library.Find(eventId) == null &&
                ReportedFallbacks.Add(eventId))
            {
                CIYCLog.Warn("No authored audio event '" + eventId +
                             "'. A synthesised placeholder is playing in its place.");
            }

            manager.PlayEvent(eventId, pos, scale);
        }

        /// <summary>
        /// Gives one item its feedback component, if it has one. A single switch on the item's
        /// own type: this used to be nine calls each running a ten-branch type comparison, so
        /// ninety <c>typeof</c> tests to add at most one component.
        /// </summary>
        public void AttachFeedbackToEquipment(EquipmentBase equipment)
        {
            switch (equipment)
            {
                case EMFDetector: Ensure<EmfAudioFeedback>(equipment); break;
                case EVPRecorder: Ensure<EvpDeviceAudio>(equipment); break;
                case PhotoCameraEquipment: Ensure<PhotoCameraAudio>(equipment); break;
                case VideoCameraEquipment: Ensure<CameraDeviceAudio>(equipment); break;
                case UVLight: Ensure<UvDeviceAudio>(equipment); break;
                case ThermometerEquipment: Ensure<ThermometerDeviceAudio>(equipment); break;
                case ParabolicMicrophone: Ensure<ParabolicAudioProcessor>(equipment); break;
                case SpectralGridProjector: Ensure<SpectralGridAudio>(equipment); break;
                case WardingRelic: Ensure<RelicAudio>(equipment); break;
                case SaltEquipment: Ensure<SaltAudio>(equipment); break;
            }
        }

        private static void Ensure<T>(EquipmentBase equipment) where T : Component
        {
            if (equipment != null && equipment.GetComponent<T>() == null)
                equipment.gameObject.AddComponent<T>();
        }

        private void HandleEquipmentChanged() => WireAllEquipment();

        /// <summary>From the registry, not a scene sweep.</summary>
        public void WireAllEquipment()
        {
            var all = EquipmentBase.Alive;
            for (int i = 0; i < all.Count; i++)
                AttachFeedbackToEquipment(all[i]);
        }
    }

    /// <summary>
    /// What every equipment sound helper shares: the item it belongs to, and the rule that a
    /// device in a bag is silent.
    /// </summary>
    public abstract class EquipmentAudioFeedback<T> : MonoBehaviour where T : EquipmentBase
    {
        protected T Device { get; private set; }

        /// <summary>
        /// True while the item is somewhere it can be heard: in a hand or set down in the room.
        /// Deliberately not <c>IsActive</c>, which also requires a battery - and would make
        /// every battery-less item, the relic and the salt among them, permanently silent.
        /// </summary>
        protected bool Audible => Device != null && (Device.IsEquipped || Device.IsPlaced);

        protected virtual void Awake() => Device = GetComponent<T>();

        protected void Cue(string eventId, float scale) =>
            EquipmentAudioController.Instance?.RouteEquipEvent(eventId, transform.position, scale);
    }

    /// <summary>The EMF reader's beeping, which quickens with the level.</summary>
    public class EmfAudioFeedback : EquipmentAudioFeedback<EMFDetector>
    {
        private float _beepTimer;

        private void Update()
        {
            if (Device == null || !Device.IsActive)
                return;

            int level = Device.CurrentLevel;
            if (level <= 0)
                return;

            _beepTimer -= Time.deltaTime;
            if (_beepTimer > 0f)
                return;

            _beepTimer = Mathf.Lerp(1.1f, 0.08f, level / 5f);
            Cue("Equip.EMF.Beep.L" + Mathf.Clamp(level, 1, 5), 0.35f + level * 0.08f);
        }
    }

    /// <summary>
    /// The recorder: the question going out, and what did or did not come back.
    ///
    /// <para>
    /// Watched from the recorder's own state rather than called by it. The three methods this
    /// replaces were public, correct and unreachable.
    /// </para>
    /// </summary>
    public class EvpDeviceAudio : EquipmentAudioFeedback<EVPRecorder>
    {
        private bool _wasRecording;
        private string _lastResult;

        private void Update()
        {
            if (Device == null || !Audible)
                return;

            bool recording = Device.IsRecording;

            if (recording && !_wasRecording)
                Cue("Equip.EVP.Question", 0.5f);

            _wasRecording = recording;

            string result = Device.LastResult;
            if (result == _lastResult)
                return;

            _lastResult = result;

            if (recording || string.IsNullOrEmpty(result) || result == "recording")
                return;

            // Something came back, or nothing did. Silence is the recorder's most common
            // answer and it should sound like one: static, not a response.
            bool answered = result.Contains("detected") || result.Contains("recorded") ||
                            result.Contains("captured");

            Cue(answered ? "Equip.EVP.Response" : "Equip.EVP.Static", answered ? 0.45f : 0.4f);
        }
    }

    /// <summary>The photo camera: the shutter, the lens, and the night-vision hum.</summary>
    public class PhotoCameraAudio : EquipmentAudioFeedback<PhotoCameraEquipment>
    {
        private float _lastCooldown;
        private float _lastZoom;
        private bool _wasNightVision;

        private void Update()
        {
            if (Device == null || !Audible)
                return;

            // The cooldown jumping up is the shutter having fired. It is the one piece of
            // state a photograph leaves behind.
            float cooldown = Device.ShutterCooldown;
            if (cooldown > _lastCooldown + 0.01f)
                Cue("Equip.Camera.Shutter", 0.65f);
            _lastCooldown = cooldown;

            float zoom = Device.Zoom;
            if (!Mathf.Approximately(zoom, _lastZoom))
            {
                if (_lastZoom > 0f)
                    Cue("Equip.Camera.Focus", 0.35f);
                _lastZoom = zoom;
            }

            bool nightVision = Device.NightVisionOn;
            if (nightVision && !_wasNightVision)
                Cue("Equip.Camera.NV.Hum", 0.25f);
            _wasNightVision = nightVision;
        }
    }

    /// <summary>The installed video camera: the hum of the feed being watched.</summary>
    public class CameraDeviceAudio : EquipmentAudioFeedback<VideoCameraEquipment>
    {
        private bool _wasSelected;

        private void Update()
        {
            if (Device == null)
                return;

            bool selected = Device.IsSelectedFeed;
            if (selected && !_wasSelected)
                Cue("Equip.Camera.NV.Hum", 0.25f);

            _wasSelected = selected;
        }
    }

    public class UvDeviceAudio : EquipmentAudioFeedback<UVLight>
    {
        private bool _wasActive;

        private void Update()
        {
            if (Device == null)
                return;

            bool active = Device.IsActive;
            if (active && !_wasActive)
                Cue("Equip.UV.Activate", 0.4f);

            _wasActive = active;
        }
    }

    public class ThermometerDeviceAudio : EquipmentAudioFeedback<ThermometerEquipment>
    {
        private float _timer;

        private void Update()
        {
            // Cached in Awake. This used to GetComponent every frame, to find a component on
            // its own GameObject.
            if (Device == null || !Device.IsActive)
                return;

            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer = 1.2f;
            Cue("Equip.Thermo.Beep", 0.3f);
        }
    }

    /// <summary>
    /// The dish narrowing what the player can hear.
    ///
    /// <para>
    /// This was doing real damage. It compared the camera's forward with the item's forward -
    /// two vectors that are all but identical for something held in the hand - so its cone
    /// test was always true; it used <c>.forward</c> on a transform whose working axis is +Y
    /// by this project's carried convention; and it wrote the player camera's low-pass cutoff
    /// and never put it back, so switching the microphone off in the wrong frame left the
    /// entire game muffled at six kilohertz for the rest of the session.
    /// </para>
    ///
    /// <para>
    /// What it does now is the thing the item is for: while the dish is live, the world outside
    /// it closes down, in proportion to how strong the signal in the headphones is. Switched
    /// off, stowed, or destroyed, the cutoff goes back to open - in every path, including
    /// OnDisable.
    /// </para>
    /// </summary>
    public class ParabolicAudioProcessor : EquipmentAudioFeedback<ParabolicMicrophone>
    {
        private const float OpenCutoff = 22000f;

        [Tooltip("How far the world closes down when the dish is at full signal, in hertz.")]
        [SerializeField, Min(500f)] private float focusedCutoff = 900f;

        private AudioLowPassFilter _listenerFilter;
        private bool _narrowed;

        private void LateUpdate()
        {
            if (Device == null || !Device.IsActive || !Audible)
            {
                Restore();
                return;
            }

            var filter = ResolveFilter();
            if (filter == null)
                return;

            // The stronger the thing in the dish, the less of the room the player hears
            // around it. That is the whole instrument: it trades awareness for direction.
            filter.cutoffFrequency =
                Mathf.Lerp(OpenCutoff, focusedCutoff, Mathf.Clamp01(Device.SignalStrength));
            _narrowed = true;
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!_narrowed)
                return;

            _narrowed = false;

            var filter = ResolveFilter();
            if (filter != null)
                filter.cutoffFrequency = OpenCutoff;
        }

        /// <summary>
        /// Resolved lazily, because this component can be added before a player camera exists,
        /// and re-resolved when the cached one has gone with a destroyed camera.
        /// </summary>
        private AudioLowPassFilter ResolveFilter()
        {
            if (_listenerFilter != null)
                return _listenerFilter;

            var cam = LocalPlayerService.ResolveViewCamera();
            if (cam == null)
                return null;

            _listenerFilter = cam.GetComponent<AudioLowPassFilter>();
            return _listenerFilter;
        }
    }

    public class SpectralGridAudio : EquipmentAudioFeedback<SpectralGridProjector>
    {
        private float _pulse;

        private void Update()
        {
            // IsProjecting rather than IsActive: the projector's own word for whether it is
            // throwing a field, which is the thing that should be making a noise.
            if (Device == null || !Device.IsProjecting)
                return;

            _pulse -= Time.deltaTime;
            if (_pulse > 0f)
                return;

            _pulse = 0.6f;
            Cue("Equip.SpectralGrid.Pulse", 0.35f);
        }
    }

    /// <summary>
    /// The relic humming while it has charges, and cracking when it spends one.
    ///
    /// <para>
    /// It used to be gated on <c>IsActive</c>, which requires a battery. The relic has none, so
    /// the condition was false forever and the relic has never made a sound.
    /// </para>
    /// </summary>
    public class RelicAudio : EquipmentAudioFeedback<WardingRelic>
    {
        private int _lastCharges = -1;

        private void Update()
        {
            if (Device == null || !Audible)
                return;

            int charges = Device.RemainingCharges;

            if (_lastCharges >= 0 && charges < _lastCharges)
                Cue("Equip.Relic.Break", 0.7f);

            _lastCharges = charges;

            if (charges > 0 && Random.value < 0.002f)
                Cue("Equip.Relic.Resonate", 0.4f);
        }
    }

    /// <summary>Salt being poured, watched from the container emptying.</summary>
    public class SaltAudio : EquipmentAudioFeedback<SaltEquipment>
    {
        private int _lastRemaining = -1;

        private void Update()
        {
            if (Device == null || !Audible)
                return;

            int remaining = Device.RemainingPiles;

            if (_lastRemaining >= 0 && remaining < _lastRemaining)
                Cue("Equip.Salt.Pour", 0.55f);

            _lastRemaining = remaining;
        }
    }
}
