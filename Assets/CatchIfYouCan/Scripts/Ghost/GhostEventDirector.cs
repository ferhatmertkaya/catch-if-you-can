using UnityEngine;
using CatchIfYouCan.Core;

namespace CatchIfYouCan.Ghost
{
    public enum HorrorEventType
    {
        LightFlicker,
        DoorMove,
        DoorSlam,
        Footsteps,
        WhisperBehind,
        ObjectThrow,
        PhoneRing,
        RadioStatic,
        ShadowCrossing,
        Breathing,
        ColdBreath,
        MirrorWriting,
        ChairMove,
        TVActivation,
        ToyActivation,
        CabinetOpening,
        DistantCry,
        TempManifestation
    }

    [System.Serializable]
    public struct HorrorEventWeight
    {
        public HorrorEventType Type;
        [Range(0f, 10f)] public float Weight;
        [Range(0f, 120f)] public float Cooldown;
    }

    public class GhostEventDirector : MonoBehaviour
    {
        [SerializeField] private GhostController ghost;
        [SerializeField] private HorrorEventWeight[] eventWeights;
        [SerializeField] private float minQuietDuration = 8f;
        [SerializeField] private float maxQuietDuration = 20f;
        [SerializeField] private float tensionRampRate = 0.15f;

        private float _tension;
        private float _nextEventTime;
        private float _quietUntil;
        private readonly System.Collections.Generic.Dictionary<HorrorEventType, float> _cooldowns =
            new System.Collections.Generic.Dictionary<HorrorEventType, float>();

        private enum PacingPhase { Quiet, SmallEvent, Activity, BiggerEvent, HuntWindow }
        private PacingPhase _phase = PacingPhase.Quiet;

        private void Start()
        {
            if (ghost == null)
                ghost = FindFirstObjectByType<GhostController>();

            if (eventWeights == null || eventWeights.Length == 0)
                eventWeights = GetDefaultWeights();

            ScheduleQuiet();
        }

        private void Update()
        {
            if (ghost == null || ghost.CurrentState == GhostState.Hunting) return;

            _tension = Mathf.Clamp01(_tension + tensionRampRate * Time.deltaTime * GetPhaseMultiplier());

            if (Time.time < _quietUntil) return;
            if (Time.time < _nextEventTime) return;

            if (TryFireWeightedEvent())
                AdvancePhase();
        }

        private float GetPhaseMultiplier()
        {
            switch (_phase)
            {
                case PacingPhase.Quiet: return 0.3f;
                case PacingPhase.SmallEvent: return 0.6f;
                case PacingPhase.Activity: return 1f;
                case PacingPhase.BiggerEvent: return 1.4f;
                case PacingPhase.HuntWindow: return 1.8f;
                default: return 1f;
            }
        }

        private void AdvancePhase()
        {
            switch (_phase)
            {
                case PacingPhase.Quiet:
                    _phase = PacingPhase.SmallEvent;
                    _quietUntil = Time.time + Random.Range(4f, 10f);
                    break;
                case PacingPhase.SmallEvent:
                    _phase = PacingPhase.Quiet;
                    ScheduleQuiet();
                    break;
                case PacingPhase.Activity:
                    _phase = Random.value < 0.4f + _tension * 0.3f
                        ? PacingPhase.HuntWindow
                        : PacingPhase.BiggerEvent;
                    break;
                case PacingPhase.BiggerEvent:
                    _phase = PacingPhase.Quiet;
                    ScheduleQuiet();
                    break;
                case PacingPhase.HuntWindow:
                    TryTriggerHunt();
                    _phase = PacingPhase.Quiet;
                    ScheduleQuiet();
                    _tension *= 0.5f;
                    break;
            }

            _nextEventTime = Time.time + Random.Range(3f, 8f);
        }

        private void ScheduleQuiet()
        {
            _quietUntil = Time.time + Random.Range(minQuietDuration, maxQuietDuration);
            if (_tension > 0.5f && Random.value < 0.35f)
            {
                _phase = PacingPhase.Activity;
                _quietUntil = Time.time + Random.Range(2f, 5f);
            }
        }

        private bool TryFireWeightedEvent()
        {
            float total = 0f;
            for (int i = 0; i < eventWeights.Length; i++)
            {
                if (IsOnCooldown(eventWeights[i].Type)) continue;
                total += eventWeights[i].Weight * GetEventScale(eventWeights[i].Type);
            }

            if (total <= 0f) return false;

            float roll = Random.value * total;
            for (int i = 0; i < eventWeights.Length; i++)
            {
                var ew = eventWeights[i];
                if (IsOnCooldown(ew.Type)) continue;

                float w = ew.Weight * GetEventScale(ew.Type);
                roll -= w;
                if (roll <= 0f)
                {
                    ExecuteEvent(ew.Type, ew.Cooldown);
                    return true;
                }
            }

            return false;
        }

        private float GetEventScale(HorrorEventType type)
        {
            bool big = type == HorrorEventType.DoorSlam || type == HorrorEventType.TempManifestation ||
                       type == HorrorEventType.ShadowCrossing || type == HorrorEventType.DistantCry;
            bool small = type == HorrorEventType.Breathing || type == HorrorEventType.LightFlicker ||
                         type == HorrorEventType.RadioStatic;

            if (_phase == PacingPhase.SmallEvent && big) return 0.2f;
            if (_phase == PacingPhase.BiggerEvent && small) return 0.3f;
            if (_phase == PacingPhase.HuntWindow && type == HorrorEventType.TempManifestation) return 2f;
            return 1f;
        }

        private bool IsOnCooldown(HorrorEventType type)
        {
            return _cooldowns.TryGetValue(type, out float until) && Time.time < until;
        }

        private void ExecuteEvent(HorrorEventType type, float cooldown)
        {
            _cooldowns[type] = Time.time + cooldown;
            Vector3 pos = ghost != null ? ghost.transform.position : transform.position;

            if (GhostActivitySystem.Instance != null)
                GhostActivitySystem.Instance.RegisterGhostEvent(GetIntensity(type));

            switch (type)
            {
                case HorrorEventType.LightFlicker:
                    ghost?.RequestLightFlicker();
                    break;
                case HorrorEventType.DoorMove:
                case HorrorEventType.DoorSlam:
                    ghost?.RequestDoorInteraction(type == HorrorEventType.DoorSlam);
                    break;
                case HorrorEventType.Footsteps:
                    GameEvents.NoiseGenerated(0.4f, pos + Random.insideUnitSphere * 3f);
                    break;
                case HorrorEventType.WhisperBehind:
                    GameEvents.NoiseGenerated(0.25f, GetBehindPlayer(pos));
                    break;
                case HorrorEventType.ObjectThrow:
                    ghost?.RequestObjectThrow();
                    break;
                case HorrorEventType.PhoneRing:
                case HorrorEventType.RadioStatic:
                case HorrorEventType.TVActivation:
                    GameEvents.BreakerChanged();
                    break;
                case HorrorEventType.ShadowCrossing:
                case HorrorEventType.TempManifestation:
                    ghost?.RequestManifestation(2f, true);
                    break;
                case HorrorEventType.Breathing:
                case HorrorEventType.ColdBreath:
                case HorrorEventType.DistantCry:
                    GameEvents.FearChanged(Mathf.Min(1f, _tension + 0.15f));
                    break;
                case HorrorEventType.MirrorWriting:
                case HorrorEventType.ChairMove:
                case HorrorEventType.ToyActivation:
                case HorrorEventType.CabinetOpening:
                    ghost?.RequestInteraction(type);
                    break;
            }

            ghost?.NotifyDirectorEvent(type);
            CIYCLog.Detail($"Horror event: {type}");
        }

        private void TryTriggerHunt()
        {
            if (ghost == null) return;
            var activity = GhostActivitySystem.Instance;
            if (activity == null) return;

            float threshold = ghost.Definition != null
                ? ghost.Definition.GetEffectiveHuntThreshold(activity.Normalized)
                : 0.65f;

            if (activity.ShouldConsiderHunt(threshold))
            {
                float aggression = ghost.Definition != null ? ghost.Definition.Aggression : 0.5f;
                if (Random.value < aggression + _tension * 0.3f)
                    ghost.TryBeginHunt();
            }
        }

        private static float GetIntensity(HorrorEventType type)
        {
            switch (type)
            {
                case HorrorEventType.DoorSlam:
                case HorrorEventType.TempManifestation:
                    return 1f;
                case HorrorEventType.ObjectThrow:
                case HorrorEventType.ShadowCrossing:
                    return 0.75f;
                default:
                    return 0.35f;
            }
        }

        private static Vector3 GetBehindPlayer(Vector3 fallback)
        {
            var cam = Camera.main;
            if (cam == null) return fallback;
            return cam.transform.position - cam.transform.forward * 2f;
        }

        private static HorrorEventWeight[] GetDefaultWeights()
        {
            return new[]
            {
                new HorrorEventWeight { Type = HorrorEventType.LightFlicker, Weight = 6f, Cooldown = 15f },
                new HorrorEventWeight { Type = HorrorEventType.DoorMove, Weight = 4f, Cooldown = 25f },
                new HorrorEventWeight { Type = HorrorEventType.Footsteps, Weight = 5f, Cooldown = 20f },
                new HorrorEventWeight { Type = HorrorEventType.Breathing, Weight = 7f, Cooldown = 12f },
                new HorrorEventWeight { Type = HorrorEventType.ObjectThrow, Weight = 3f, Cooldown = 30f },
                new HorrorEventWeight { Type = HorrorEventType.WhisperBehind, Weight = 4f, Cooldown = 22f },
                new HorrorEventWeight { Type = HorrorEventType.ShadowCrossing, Weight = 2f, Cooldown = 45f },
                new HorrorEventWeight { Type = HorrorEventType.TempManifestation, Weight = 1.5f, Cooldown = 60f },
            };
        }
    }
}
