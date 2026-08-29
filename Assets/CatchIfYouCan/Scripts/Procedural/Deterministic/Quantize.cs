namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// THE single quantization contract for the project. Do not duplicate these
    /// conversions anywhere else.
    ///
    /// Raw floats are never hashed. IEEE-754 add/multiply are bit-exact given a fixed
    /// evaluation order, but transcendental functions route to platform libm and
    /// compilers may contract a*b+c into an FMA with different rounding, so a raw float
    /// is not a safe network or persistence contract. Quantizing to integers makes the
    /// hash robust against last-bit noise while still catching every difference a player
    /// could perceive.
    ///
    /// Stage A works in integer millimetres end to end; these helpers exist for the
    /// boundaries where authored content (which is float) enters generation, and for the
    /// bridge back to Unity world space.
    ///
    /// These scales are frozen and part of GenerationVersion.
    /// </summary>
    public static class Quantize
    {
        /// <summary>Positions and sizes: metres -> millimetres.</summary>
        public const int PositionScale = 1000;

        /// <summary>Selection weights: fixed point with 3 decimal places.</summary>
        public const int WeightScale = 1000;

        /// <summary>Rotations are stored as cardinal indices (0=N, 1=E, 2=S, 3=W).</summary>
        public const int RotationSteps = 4;

        /// <summary>Metres -> millimetres, half-away-from-zero so the result is symmetric about 0.</summary>
        public static int Millimetres(float metres)
        {
            float scaled = metres * PositionScale;
            return scaled >= 0f ? (int)(scaled + 0.5f) : -(int)(-scaled + 0.5f);
        }

        /// <summary>Millimetres -> metres. Used only when crossing back into Unity world space.</summary>
        public static float Metres(int millimetres) => millimetres / (float)PositionScale;

        /// <summary>Weight -> fixed point, for hashing content identity.</summary>
        public static int Weight(float weight)
        {
            float scaled = weight * WeightScale;
            return scaled >= 0f ? (int)(scaled + 0.5f) : -(int)(-scaled + 0.5f);
        }

        /// <summary>Normalises any cardinal rotation index into [0, RotationSteps).</summary>
        public static int RotationIndex(int index)
        {
            int r = index % RotationSteps;
            return r < 0 ? r + RotationSteps : r;
        }
    }
}
