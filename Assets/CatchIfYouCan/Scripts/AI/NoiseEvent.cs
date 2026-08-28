using UnityEngine;

namespace CatchIfYouCan.AI
{
    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Intensity;
        public float Timestamp;
        public string SourceTag;

        public NoiseEvent(Vector3 position, float intensity, float timestamp, string sourceTag = "")
        {
            Position = position;
            Intensity = intensity;
            Timestamp = timestamp;
            SourceTag = sourceTag;
        }

        public float Age(float now) => now - Timestamp;

        public float DecayedIntensity(float now, float halfLifeSeconds)
        {
            if (halfLifeSeconds <= 0f) return Intensity;
            float age = Age(now);
            return Intensity * Mathf.Pow(0.5f, age / halfLifeSeconds);
        }
    }
}
