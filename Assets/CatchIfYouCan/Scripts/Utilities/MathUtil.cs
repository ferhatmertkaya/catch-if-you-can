using UnityEngine;

namespace CatchIfYouCan.Utilities
{
    public static class MathUtil
    {
        public static float SmoothApproach(float current, float target, float speed, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        }

        public static float Remap(float v, float a, float b, float c, float d)
        {
            if (Mathf.Approximately(a, b)) return c;
            return Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, v));
        }
    }
}
