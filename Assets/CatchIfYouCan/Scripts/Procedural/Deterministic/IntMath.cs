namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>Exact integer math helpers. No libm, so identical on every platform.</summary>
    public static class IntMath
    {
        /// <summary>
        /// Exact integer square root (floor). Used instead of Mathf.Sqrt for distance
        /// scoring: float sqrt is IEEE-exact, but keeping the whole scoring path in
        /// integers removes any question of FMA contraction or evaluation order.
        /// </summary>
        public static long Sqrt(long value)
        {
            if (value <= 0)
                return 0;

            long x = value;
            long y = (x + 1) / 2;
            while (y < x)
            {
                x = y;
                y = (x + value / x) / 2;
            }

            return x;
        }
    }
}
