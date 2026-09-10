namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// The canonical ghost roster, declared once.
    ///
    /// <para>
    /// The ids existed only as string literals inside
    /// <see cref="GhostDefinitionFactory"/>, which is the same shape the equipment ids had
    /// before V3 gave them <c>EquipmentIds</c>. A literal is fine until something else needs to
    /// name a ghost - a save file, a mission's assigned entity, a compatibility hash, a network
    /// handshake - and then the id exists in several places and one of them is a typo that
    /// resolves to nothing at runtime.
    /// </para>
    ///
    /// <para>
    /// The order is the roster order and is part of the contract: <see cref="IndexOf"/> is a
    /// stable ordinal, which is what lets a ghost be named on a wire in one byte later without
    /// the meaning of that byte depending on which build sent it.
    /// </para>
    /// </summary>
    public static class GhostIds
    {
        public const string Wanderer = "the_wanderer";
        public const string Whisper = "the_whisper";
        public const string Watcher = "the_watcher";
        public const string Mimicer = "the_mimicer";
        public const string Knocker = "the_knocker";
        public const string Crawler = "the_crawler";
        public const string Hollow = "the_hollow";
        public const string Static = "the_static";
        public const string Shadeborn = "the_shadeborn";
        public const string WeepingOne = "the_weeping_one";

        /// <summary>Every ghost, in roster order. The order is part of the contract.</summary>
        public static readonly string[] All =
        {
            Wanderer,
            Whisper,
            Watcher,
            Mimicer,
            Knocker,
            Crawler,
            Hollow,
            Static,
            Shadeborn,
            WeepingOne,
        };

        public static bool IsCanonical(string id) => IndexOf(id) >= 0;

        /// <summary>Stable ordinal, or -1. Not a hash code: this number is a contract.</summary>
        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;

            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], id, System.StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}
