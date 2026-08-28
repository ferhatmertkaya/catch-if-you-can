using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class AudioClipShuffleBag
    {
        private const int RecentWindow = 3;
        private const float RecentWeightMultiplier = 0.05f;

        private readonly List<AudioClip> _recent = new List<AudioClip>(RecentWindow);
        private readonly System.Random _rng;

        public AudioClipShuffleBag(int seed = 0)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public AudioClip Pick(IReadOnlyList<AudioClip> variants)
        {
            if (variants == null || variants.Count == 0)
                return null;

            if (variants.Count == 1)
            {
                Remember(variants[0]);
                return variants[0];
            }

            var last = _recent.Count > 0 ? _recent[_recent.Count - 1] : null;
            float totalWeight = 0f;
            var weights = new float[variants.Count];

            for (int i = 0; i < variants.Count; i++)
            {
                var clip = variants[i];
                if (clip == null)
                {
                    weights[i] = 0f;
                    continue;
                }

                float weight = 1f;
                if (clip == last)
                    weight = 0f;
                else if (_recent.Contains(clip))
                    weight *= RecentWeightMultiplier;

                weights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                var fallback = variants[RandomIndex(variants.Count)];
                Remember(fallback);
                return fallback;
            }

            float roll = (float)_rng.NextDouble() * totalWeight;
            for (int i = 0; i < variants.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                {
                    Remember(variants[i]);
                    return variants[i];
                }
            }

            var picked = variants[variants.Count - 1];
            Remember(picked);
            return picked;
        }

        public void Reset()
        {
            _recent.Clear();
        }

        private void Remember(AudioClip clip)
        {
            if (clip == null) return;
            _recent.Remove(clip);
            _recent.Add(clip);
            while (_recent.Count > RecentWindow)
                _recent.RemoveAt(0);
        }

        private int RandomIndex(int count) => _rng.Next(0, count);
    }
}
