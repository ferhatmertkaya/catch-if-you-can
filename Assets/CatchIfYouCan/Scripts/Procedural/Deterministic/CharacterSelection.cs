namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>What the host decided about a character somebody claimed.</summary>
    public enum CharacterVerdict
    {
        /// <summary>The catalog has that character. Use it.</summary>
        Accepted = 0,

        /// <summary>Nothing was chosen. The host substitutes its default rather than refusing.</summary>
        Unset,

        /// <summary>There is no catalog, or it is empty. Nobody can be anybody.</summary>
        EmptyCatalog,

        /// <summary>An index the catalog does not have. Substituted, never trusted.</summary>
        OutOfRange,

        /// <summary>
        /// More characters than the compact encoding can name. A content problem, not a peer
        /// problem: the catalog was allowed to grow past what a byte can index.
        /// </summary>
        CatalogTooLarge,
    }

    /// <summary>
    /// Which character a player is, as one small number, and what a host does with the one it
    /// is handed.
    ///
    /// <para>
    /// The index is the catalog's order, which is why that order is documented as meaningful
    /// and why the catalog is an explicit list rather than a folder scan - a scan answers
    /// "whatever happened to be imported", which is a different order on two machines and
    /// therefore a different character on two clients.
    /// </para>
    ///
    /// <para>
    /// <b>An index that arrives from another machine is a claim, not a fact.</b> Every entry
    /// point here either reports what is wrong with it or substitutes something valid; none
    /// of them hands back a number that would index outside the catalog. A client that sends
    /// 4000 gets the default character and a line in the host's log, not an exception in the
    /// middle of spawning somebody.
    /// </para>
    ///
    /// <para>
    /// Pure, and in the deterministic assembly, so the rules are exercised by the offline
    /// harness rather than only by a running game with two machines in it.
    /// </para>
    /// </summary>
    public static class CharacterSelection
    {
        /// <summary>No choice has been made. Distinct from character zero, which is a choice.</summary>
        public const int Unset = -1;

        /// <summary>
        /// The most characters the compact encoding can name.
        ///
        /// <para>
        /// The index travels as one byte and <see cref="UnsetWire"/> takes the top value, so
        /// the usable indices are 0 to 254. This is a real limit on the catalog rather than a
        /// buffer size: a 256th character would be unnameable over the wire and silently
        /// become somebody else.
        /// </para>
        /// </summary>
        public const int MaxCharacters = 255;

        /// <summary>The byte that means "nothing chosen".</summary>
        public const byte UnsetWire = 255;

        /// <summary>What a host substitutes for a claim it will not honour.</summary>
        public const int Fallback = 0;

        /// <summary>
        /// What is wrong with this index against a catalog of this size, or nothing.
        ///
        /// <para>
        /// Kept separate from <see cref="Resolve"/> so the host can log the precise cause -
        /// an unset choice and a hostile index both end up as character zero, and they are
        /// not the same event.
        /// </para>
        /// </summary>
        public static CharacterVerdict Check(int index, int catalogCount)
        {
            if (catalogCount <= 0)
                return CharacterVerdict.EmptyCatalog;

            if (catalogCount > MaxCharacters)
                return CharacterVerdict.CatalogTooLarge;

            if (index == Unset)
                return CharacterVerdict.Unset;

            if (index < 0 || index >= catalogCount)
                return CharacterVerdict.OutOfRange;

            return CharacterVerdict.Accepted;
        }

        /// <summary>Whether the claim was honoured as sent.</summary>
        public static bool IsAccepted(CharacterVerdict verdict) =>
            verdict == CharacterVerdict.Accepted;

        /// <summary>
        /// The index the host will actually use: the claim when it is valid, the default when
        /// it is not, and <see cref="Unset"/> only when there is nothing to choose from.
        ///
        /// <para>
        /// Never returns a number that would index outside a catalog of
        /// <paramref name="catalogCount"/> entries. A catalog larger than the encoding can
        /// name is clamped rather than trusted, so the indices past the limit resolve to the
        /// default instead of to a character no peer could have meant.
        /// </para>
        /// </summary>
        public static int Resolve(int index, int catalogCount)
        {
            if (catalogCount <= 0)
                return Unset;

            int nameable = catalogCount > MaxCharacters ? MaxCharacters : catalogCount;

            if (index < 0 || index >= nameable)
                return Fallback;

            return index;
        }

        /// <summary>The index as it travels. Anything unusable becomes <see cref="UnsetWire"/>.</summary>
        public static byte Encode(int index)
        {
            if (index < 0 || index >= MaxCharacters)
                return UnsetWire;

            return (byte)index;
        }

        /// <summary>
        /// What arrived, as an index. Still a claim - <see cref="Resolve"/> decides whether
        /// the catalog has it.
        /// </summary>
        public static int Decode(byte wire) => wire == UnsetWire ? Unset : wire;

        /// <summary>Why, for a log. Not shown to a player: which index they sent is not news.</summary>
        public static string Describe(CharacterVerdict verdict)
        {
            switch (verdict)
            {
                case CharacterVerdict.Accepted:
                    return "the chosen character";
                case CharacterVerdict.Unset:
                    return "no character was chosen; the default was used";
                case CharacterVerdict.EmptyCatalog:
                    return "there is no character catalog to choose from";
                case CharacterVerdict.OutOfRange:
                    return "a character index outside the catalog; the default was used";
                case CharacterVerdict.CatalogTooLarge:
                    return "the character catalog is larger than " + MaxCharacters +
                           " and cannot be indexed over the wire";
                default:
                    return "unknown character verdict";
            }
        }
    }
}
