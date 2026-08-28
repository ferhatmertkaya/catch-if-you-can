using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public static class AudioEventResolve
    {
        public static AudioClip ResolveClip(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return null;

            var library = AudioManager.Instance?.EventLibrary;
            var def = library?.Find(eventId);
            var clip = def?.PickClip();
            if (clip != null)
                return clip;

            return ProceduralAudioSynth.CreateForEventId(NormalizeId(eventId));
        }

        public static bool Play(string eventId, Vector3? position = null, float scale = 1f)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.PlayEvent(eventId, position, scale))
                return true;

            var clip = ResolveClip(eventId);
            if (clip == null)
                return false;

            if (position.HasValue)
                AudioManager.Instance?.PlayAtPosition(clip, position.Value, scale);
            else
                AudioManager.Instance?.PlayOneShot(clip, scale);
            return true;
        }

        private static string NormalizeId(string eventId)
        {
            return eventId.Replace('/', '.').ToLowerInvariant();
        }
    }
}
