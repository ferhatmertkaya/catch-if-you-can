using System;
using UnityEngine;
using Random = System.Random;

namespace CatchIfYouCan.Procedural
{
    public static class SeedManager
    {
        public const int KnownGoodSeed = 424242;

        private static int _currentSeed = KnownGoodSeed;
        private static Random _systemRandom = new Random(_currentSeed);

        public static int CurrentSeed => _currentSeed;

        public static void SetSeed(int seed)
        {
            _currentSeed = seed;
            _systemRandom = new Random(seed);
            UnityEngine.Random.InitState(seed);
        }

        public static int GetSeed() => _currentSeed;

        public static Random CreateRandom(int seed)
        {
            return new Random(seed);
        }

        public static Random SystemRandom => _systemRandom;

        public static float NextFloat(Random rng, float min, float max)
        {
            return (float)(min + rng.NextDouble() * (max - min));
        }

        public static T PickWeighted<T>(Random rng, T[] items, float[] weights)
        {
            if (items == null || items.Length == 0)
                return default;

            if (weights == null || weights.Length != items.Length)
            {
                int index = rng.Next(0, items.Length);
                return items[index];
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += Mathf.Max(0.01f, weights[i]);

            float roll = NextFloat(rng, 0f, total);
            float cumulative = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                cumulative += Mathf.Max(0.01f, weights[i]);
                if (roll <= cumulative)
                    return items[i];
            }

            return items[items.Length - 1];
        }
    }
}
