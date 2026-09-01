using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    /// <summary>
    /// Everything the interactive room is supposed to sound like, driven from one place.
    ///
    /// <para>
    /// Four layers, deliberately ranked. A looping night outside the window sits underneath
    /// everything and never stops. Above it, three kinds of one-shot compete for a single slot:
    /// an exterior animal or a distant bell, a noise from somewhere else in the building, or the
    /// thing that walks behind you. Only one of those three may sound at a time, which is what
    /// keeps the room from turning into a haunted house sound effects reel — and long stretches
    /// where none of them fire are not a bug, they are most of the effect.
    /// </para>
    ///
    /// <para>
    /// It is asleep until <see cref="Begin"/> is called with the player, which happens at the
    /// handover into the room. Before that it holds no sources and schedules nothing, so the
    /// cinematic menu is untouched by it.
    /// </para>
    ///
    /// <para>
    /// Every AudioSource is built once in <see cref="Begin"/> and reused: one for the night, one
    /// for the thing behind you, and two small pools for exterior and building one-shots. Update
    /// walks no hierarchy, allocates nothing and calls no Find. The only per-frame work while
    /// something is playing is one distance check for the window filter, throttled to ten times a
    /// second, and one transform write for the moving emitter.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Interactive Room Ambience")]
    public sealed class InteractiveRoomAmbience : MonoBehaviour
    {
        // ---- authored data ------------------------------------------------------------------

        /// <summary>
        /// One kind of thing that can happen outside: its clips, how far away it is when it
        /// does, and how loud. Distance is per kind rather than per emitter because an owl and a
        /// church bell are not the same distance away — a bell that sounds like it is at the
        /// front door is not a distant bell, it is a doorbell.
        /// </summary>
        [System.Serializable]
        public sealed class ExteriorKind
        {
            public string label;
            public AudioClip[] clips = new AudioClip[0];

            [Tooltip("Relative likelihood of this kind when an exterior event fires.")]
            [Min(0f)] public float weight = 1f;

            [Tooltip("How far outside it happens, in metres from the window. The lower bound is " +
                     "a real minimum: nothing is ever allowed closer than this.")]
            public Vector2 distanceRange = new Vector2(14f, 30f);

            [Tooltip("Loudness band, rolled per event. A wolf carries; a bell at four hundred " +
                     "metres does not.")]
            public Vector2 volumeRange = new Vector2(0.5f, 0.8f);

            [Tooltip("Height relative to the window, rolled per event.")]
            public Vector2 heightRange = new Vector2(-3f, 8f);
        }

        [System.Serializable]
        public sealed class BuildingZone
        {
            public string label;
            public Vector3 position;

            [Tooltip("Low-pass cutoff in Hz. Lower reads as more material between you and it: a " +
                     "floor above or below is darker than a hallway with an open door.")]
            public float cutoffHz = 1200f;

            [Range(0.1f, 1.5f)] public float volumeScale = 1f;
        }

        // ---- night ------------------------------------------------------------------------

        [Header("Night outside the window")]
        [SerializeField] private AudioClip nightAmbience;

        [Tooltip("Where the night is. Outside the glass, not on the player: the whole point is " +
                 "that it arrives through the window from one direction.")]
        [SerializeField] private Vector3 nightPosition = new Vector3(26.5f, 1.9f, 0f);

        [SerializeField, Range(0f, 1f)] private float nightVolume = 0.34f;
        [SerializeField] private float nightMinDistance = 1.5f;
        [SerializeField] private float nightMaxDistance = 16f;

        [Header("Closed glass")]
        [Tooltip("Cutoff when the player is across the room, and when they are at the glass. The " +
                 "window is shut, so the night is always a little filtered; standing at it just " +
                 "opens the filter up rather than removing it.")]
        [SerializeField] private float glassCutoffFar = 900f;
        [SerializeField] private float glassCutoffNear = 4200f;

        [Tooltip("Distance from the window over which that filter opens.")]
        [SerializeField] private float glassNearDistance = 1.5f;
        [SerializeField] private float glassFarDistance = 9f;

        [SerializeField] private Vector3 windowPosition = new Vector3(25.06f, 1.75f, 0f);

        // ---- exterior one-shots ------------------------------------------------------------

        [Header("Exterior one-shots")]
        [Tooltip("What can happen outside, each with its own distance band and loudness.")]
        [SerializeField] private ExteriorKind[] exteriorKinds = new ExteriorKind[0];

        [SerializeField] private Vector2 exteriorInterval = new Vector2(16f, 46f);

        [Tooltip("Half-angle of the arc outside the window that events are placed in, measured " +
                 "from straight out. Wide enough that direction is readable on headphones, " +
                 "narrow enough that nothing is ever placed back through the room.")]
        [SerializeField, Range(20f, 85f)] private float exteriorSpreadDegrees = 72f;

        [Tooltip("Two events in a row from nearly the same bearing read as one repeated sound. " +
                 "This is the minimum turn between them.")]
        [SerializeField, Range(0f, 120f)] private float exteriorMinBearingChange = 45f;

        [Tooltip("Rolloff of the exterior voices. The far end has to clear the furthest kind's " +
                 "distance or the most distant events would be silent rather than distant.")]
        [SerializeField] private float exteriorRolloffNear = 8f;
        [SerializeField] private float exteriorRolloffFar = 75f;

        // ---- building ----------------------------------------------------------------------

        [Header("Building noises")]
        [SerializeField] private AudioClip[] buildingClips = new AudioClip[0];

        [SerializeField] private Vector2 buildingInterval = new Vector2(16f, 52f);
        [SerializeField, Range(0f, 1f)] private float buildingVolume = 0.62f;
        [SerializeField] private float buildingMinDistance = 2.5f;
        [SerializeField] private float buildingMaxDistance = 26f;

        [Tooltip("Acoustic positions elsewhere in the house. No rooms are modelled there; these " +
                 "are just believable places for a noise to have come from.")]
        [SerializeField] private BuildingZone[] buildingZones = new BuildingZone[0];

        // ---- behind the player ---------------------------------------------------------------

        [Header("Behind the player")]
        [SerializeField] private AudioClip behindPlayerClip;

        [Tooltip("How long after entering the room before this can happen at all. It must never " +
                 "be the first thing the room does.")]
        [SerializeField] private Vector2 behindFirstDelay = new Vector2(50f, 110f);

        [SerializeField] private Vector2 behindInterval = new Vector2(60f, 150f);
        [SerializeField, Range(0f, 1f)] private float behindVolume = 0.5f;

        [Tooltip("How far from the player's head it passes. Close enough to be personal, far " +
                 "enough not to sound like it is inside their skull.")]
        [SerializeField] private float behindRadius = 1.1f;

        [Tooltip("Where the arc starts and ends, in degrees from straight ahead. 180 is directly " +
                 "behind. Staying inside 100..260 is what stops it ever appearing in front.")]
        [SerializeField] private Vector2 behindArc = new Vector2(118f, 242f);

        [SerializeField] private float behindHeightOffset = -0.08f;

        // ---- runtime -------------------------------------------------------------------------

        private Transform _player;
        private AudioSource _night;
        private AudioLowPassFilter _nightFilter;
        private AudioSource _behind;
        private Transform _behindTransform;
        private AudioSource[] _exteriorVoices;
        private AudioSource[] _buildingVoices;
        private AudioLowPassFilter[] _buildingFilters;

        private float _exteriorTimer;
        private float _buildingTimer;
        private float _behindTimer;
        private float _filterTimer;

        private AudioClip _lastExteriorClip;
        private int _lastExteriorKind = -1;
        private float _lastExteriorBearing = 999f;
        private AudioClip _lastBuildingClip;
        private int _lastBuildingZone = -1;
        private int _exteriorCursor;
        private int _buildingCursor;

        private bool _running;
        private float _behindEndTime;
        private float _behindStartTime;
        private float _behindFrom;
        private float _behindTo;

        private System.Random _rng;
        private readonly List<AudioClip> _pick = new List<AudioClip>(4);

        /// <summary>True while one of the three one-shot layers is sounding.</summary>
        private bool EventBusy =>
            Time.time < _behindEndTime ||
            AnyPlaying(_exteriorVoices) ||
            AnyPlaying(_buildingVoices);

        private static bool AnyPlaying(AudioSource[] voices)
        {
            if (voices == null)
                return false;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null && voices[i].isPlaying)
                    return true;
            return false;
        }

        /// <summary>
        /// Wakes the room up. Called at the handover, with the player that was just built.
        /// Safe to call twice; the second call only re-points the player.
        /// </summary>
        public void Begin(Transform player)
        {
            _player = player;

            if (_running)
                return;

            // Re-entry after End(): the sources are still there, so restart rather than rebuild.
            if (_night != null)
            {
                if (nightAmbience != null && !_night.isPlaying)
                    _night.Play();
                _exteriorTimer = Range(4f, 10f);
                _buildingTimer = NextInterval(buildingInterval);
                _behindTimer = NextInterval(behindFirstDelay);
                _running = true;
                return;
            }

            // Cosmetic only, and its own stream: nothing here may reach the mission seed or
            // anything the network agrees on. Which owl hoots is not shared state.
            _rng = new System.Random(unchecked((int)System.DateTime.UtcNow.Ticks) ^ (GetHashCode() * 31));

            BuildNight();
            BuildBehind();
            _exteriorVoices = BuildPool("Exterior", 2, exteriorRolloffNear, exteriorRolloffFar, false, out _);
            _buildingVoices = BuildPool("Building", 2, buildingMinDistance, buildingMaxDistance, true, out _buildingFilters);

            // Deliberately soon. A layer whose first event is half a minute away is
            // indistinguishable from a layer that does not work.
            _exteriorTimer = Range(4f, 10f);
            _buildingTimer = NextInterval(buildingInterval);
            _behindTimer = NextInterval(behindFirstDelay);

            _running = true;
        }

        /// <summary>Silences everything. The room going away is enough to trigger this.</summary>
        public void End()
        {
            if (_night != null) _night.Stop();
            if (_behind != null) _behind.Stop();
            StopAll(_exteriorVoices);
            StopAll(_buildingVoices);
            _behindEndTime = 0f;

            // Not just silence: the room is no longer running. Without this the sources stayed
            // built and _running stayed true, so a second entry would take Begin's early return
            // and the night would never start again.
            _running = false;
        }

        private static void StopAll(AudioSource[] voices)
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null) voices[i].Stop();
        }

        private void OnDisable()
        {
            End();
        }

        // ---- construction ---------------------------------------------------------------------

        private AudioSource MakeSource(string label, Vector3 position, float min, float max)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            // Fully positional. A 2D source would arrive in both ears equally and read as music.
            src.spatialBlend = 1f;
            src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = min;
            src.maxDistance = max;
            src.spread = 25f;
            return src;
        }

        private void BuildNight()
        {
            _night = MakeSource("Ambience_NightExterior", nightPosition, nightMinDistance, nightMaxDistance);
            _night.clip = nightAmbience;
            _night.loop = true;
            _night.volume = nightVolume;
            _night.priority = 200;
            _night.spread = 55f;   // wide: the night is everywhere out there, not a point

            _nightFilter = _night.gameObject.AddComponent<AudioLowPassFilter>();
            _nightFilter.cutoffFrequency = glassCutoffFar;

            if (nightAmbience != null)
                _night.Play();
        }

        private void BuildBehind()
        {
            _behind = MakeSource("Ambience_BehindPlayer", Vector3.zero, 0.6f, 12f);
            _behind.volume = behindVolume;
            _behind.spread = 15f;
            _behindTransform = _behind.transform;
        }

        private AudioSource[] BuildPool(string label, int count, float min, float max,
                                        bool withFilter, out AudioLowPassFilter[] filters)
        {
            var pool = new AudioSource[count];
            filters = withFilter ? new AudioLowPassFilter[count] : null;
            for (int i = 0; i < count; i++)
            {
                pool[i] = MakeSource(label + "_Voice_" + i, Vector3.zero, min, max);
                if (withFilter)
                {
                    filters[i] = pool[i].gameObject.AddComponent<AudioLowPassFilter>();
                    filters[i].cutoffFrequency = 22000f;
                }
            }
            return pool;
        }

        // ---- per frame ---------------------------------------------------------------------

        private void Update()
        {
            if (!_running)
                return;

            float dt = Time.deltaTime;

            UpdateGlassFilter(dt);
            UpdateBehind();

            // One slot for the three loud layers. Timers keep counting while something else is
            // sounding, so a blocked event fires shortly after rather than being skipped, but
            // nothing ever lands on top of anything else.
            _exteriorTimer -= dt;
            _buildingTimer -= dt;
            _behindTimer -= dt;

            if (EventBusy)
                return;

            if (_behindTimer <= 0f && behindPlayerClip != null && _player != null)
            {
                StartBehind();
                _behindTimer = NextInterval(behindInterval);
                return;
            }

            if (_exteriorTimer <= 0f)
            {
                PlayExterior();
                _exteriorTimer = NextInterval(exteriorInterval);
                return;
            }

            if (_buildingTimer <= 0f)
            {
                PlayBuilding();
                _buildingTimer = NextInterval(buildingInterval);
            }
        }

        /// <summary>
        /// Opens the window filter as the player walks up to the glass. Ten times a second is
        /// plenty for something the ear reads as a slow change, and it keeps a distance check out
        /// of every frame.
        /// </summary>
        private void UpdateGlassFilter(float dt)
        {
            if (_nightFilter == null || _player == null)
                return;

            _filterTimer -= dt;
            if (_filterTimer > 0f)
                return;
            _filterTimer = 0.1f;

            float d = Vector3.Distance(_player.position, windowPosition);
            float k = Mathf.InverseLerp(glassFarDistance, glassNearDistance, d);
            _nightFilter.cutoffFrequency =
                Mathf.Lerp(_nightFilter.cutoffFrequency, Mathf.Lerp(glassCutoffFar, glassCutoffNear, k), 0.35f);
        }

        // ---- behind the player ----------------------------------------------------------------

        private void StartBehind()
        {
            float length = behindPlayerClip.length;
            _behindStartTime = Time.time;
            _behindEndTime = Time.time + length;

            // Left to right or right to left, never the same way twice running by luck alone.
            bool leftToRight = _rng.Next(2) == 0;
            _behindFrom = leftToRight ? behindArc.x : behindArc.y;
            _behindTo = leftToRight ? behindArc.y : behindArc.x;

            PlaceBehind(0f);
            _behind.clip = behindPlayerClip;
            _behind.volume = behindVolume;
            _behind.Play();
        }

        private void UpdateBehind()
        {
            if (Time.time >= _behindEndTime || _player == null)
                return;

            float total = Mathf.Max(0.01f, _behindEndTime - _behindStartTime);
            PlaceBehind(Mathf.Clamp01((Time.time - _behindStartTime) / total));
        }

        /// <summary>
        /// Puts the emitter on an arc through the rear hemisphere, measured from the player's
        /// own facing so it stays behind them even while they turn. Smoothstepped rather than
        /// linear so it eases in and out instead of starting and stopping dead.
        /// </summary>
        private void PlaceBehind(float t)
        {
            float eased = t * t * (3f - 2f * t);
            float angle = Mathf.Lerp(_behindFrom, _behindTo, eased);

            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * _player.forward;
            Vector3 head = _player.position + Vector3.up * (1.6f + behindHeightOffset);
            _behindTransform.position = head + dir * behindRadius;
        }

        // ---- one-shots -------------------------------------------------------------------------

        /// <summary>
        /// Places one exterior event somewhere outside and plays it.
        ///
        /// <para>
        /// Nothing is placed at a fixed point any more. A kind is drawn by weight, then a bearing,
        /// a distance inside that kind's band and a height are all rolled independently, so the
        /// same owl is never twice in the same place. The distance band is what stops everything
        /// sounding like it is at the front door: an owl is allowed close, a bell never is.
        /// </para>
        ///
        /// <para>
        /// The bearing is constrained to an arc facing out of the window, so a placement can
        /// never land behind the player through the room, and it has to differ from the last one
        /// by <see cref="exteriorMinBearingChange"/> — two events from the same direction in a row
        /// read as one sound repeating rather than as a world with things in it.
        /// </para>
        /// </summary>
        private void PlayExterior()
        {
            if (exteriorKinds.Length == 0 || _exteriorVoices == null)
                return;

            int k = PickExteriorKind();
            if (k < 0)
                return;

            var kind = exteriorKinds[k];
            var clip = PickFrom(kind.clips, kind.clips.Length > 1 ? _lastExteriorClip : null);
            if (clip == null)
                return;

            _lastExteriorKind = k;
            _lastExteriorClip = clip;

            float bearing = PickBearing();
            float distance = Range(Mathf.Min(kind.distanceRange.x, kind.distanceRange.y),
                                   Mathf.Max(kind.distanceRange.x, kind.distanceRange.y));

            // Out through the window, then turned by the bearing. Window faces +X.
            Vector3 dir = Quaternion.AngleAxis(bearing, Vector3.up) * Vector3.right;
            Vector3 pos = windowPosition + dir * distance;
            pos.y = windowPosition.y + Range(Mathf.Min(kind.heightRange.x, kind.heightRange.y),
                                             Mathf.Max(kind.heightRange.x, kind.heightRange.y));

            var voice = _exteriorVoices[_exteriorCursor];
            _exteriorCursor = (_exteriorCursor + 1) % _exteriorVoices.Length;

            voice.transform.position = pos;
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(Range(Mathf.Min(kind.volumeRange.x, kind.volumeRange.y),
                                               Mathf.Max(kind.volumeRange.x, kind.volumeRange.y)));
            voice.pitch = Range(0.94f, 1.06f);
            voice.Play();
        }

        /// <summary>A bearing inside the outward arc, far enough from the last one to read as new.</summary>
        private float PickBearing()
        {
            float bearing = Range(-exteriorSpreadDegrees, exteriorSpreadDegrees);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(bearing, _lastExteriorBearing)) >= exteriorMinBearingChange)
                    break;
                bearing = Range(-exteriorSpreadDegrees, exteriorSpreadDegrees);
            }
            _lastExteriorBearing = bearing;
            return bearing;
        }

        /// <summary>Weighted draw over the kinds, avoiding the one that just happened.</summary>
        private int PickExteriorKind()
        {
            float total = 0f;
            for (int i = 0; i < exteriorKinds.Length; i++)
            {
                if (exteriorKinds[i] == null || exteriorKinds[i].clips.Length == 0) continue;
                if (i == _lastExteriorKind && exteriorKinds.Length > 1) continue;
                total += Mathf.Max(0f, exteriorKinds[i].weight);
            }
            if (total <= 0f)
            {
                // Everything else was excluded; fall back to anything playable at all.
                for (int i = 0; i < exteriorKinds.Length; i++)
                    if (exteriorKinds[i] != null && exteriorKinds[i].clips.Length > 0) return i;
                return -1;
            }

            float roll = Range(0f, total);
            for (int i = 0; i < exteriorKinds.Length; i++)
            {
                if (exteriorKinds[i] == null || exteriorKinds[i].clips.Length == 0) continue;
                if (i == _lastExteriorKind && exteriorKinds.Length > 1) continue;
                roll -= Mathf.Max(0f, exteriorKinds[i].weight);
                if (roll <= 0f) return i;
            }
            return -1;
        }

        private void PlayBuilding()
        {
            if (buildingClips.Length == 0 || buildingZones.Length == 0 || _buildingVoices == null)
                return;

            var clip = PickFrom(buildingClips, _lastBuildingClip);
            _lastBuildingClip = clip;

            int z = PickIndex(buildingZones.Length, _lastBuildingZone);
            _lastBuildingZone = z;
            var zone = buildingZones[z];

            int slot = _buildingCursor;
            _buildingCursor = (_buildingCursor + 1) % _buildingVoices.Length;
            var voice = _buildingVoices[slot];

            voice.transform.position = zone.position;
            voice.clip = clip;
            voice.volume = buildingVolume * zone.volumeScale * Range(0.85f, 1f);
            voice.pitch = Range(0.95f, 1.05f);

            // The filter is the whole illusion: a knock through a ceiling has no top end left.
            if (_buildingFilters != null && _buildingFilters[slot] != null)
                _buildingFilters[slot].cutoffFrequency = zone.cutoffHz;

            voice.Play();
        }

        // ---- helpers ----------------------------------------------------------------------------

        private float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        private float NextInterval(Vector2 range) => Range(Mathf.Min(range.x, range.y),
                                                           Mathf.Max(range.x, range.y));

        /// <summary>An index that is not the one used last, when there is more than one to choose from.</summary>
        private int PickIndex(int count, int avoid)
        {
            if (count <= 1) return 0;
            int i = _rng.Next(count);
            if (i == avoid) i = (i + 1 + _rng.Next(count - 1)) % count;
            return i;
        }

        private AudioClip PickFrom(AudioClip[] clips) => PickFrom(clips, null);

        private AudioClip PickFrom(AudioClip[] clips, AudioClip avoid)
        {
            _pick.Clear();
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && clips[i] != avoid)
                    _pick.Add(clips[i]);

            if (_pick.Count == 0)
            {
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) return clips[i];
                return null;
            }
            return _pick[_rng.Next(_pick.Count)];
        }
    }
}
