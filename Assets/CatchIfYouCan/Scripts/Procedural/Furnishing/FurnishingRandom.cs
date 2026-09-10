using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Procedural.Furnishing
{
    /// <summary>
    /// Der Zufall der Einrichtung - und ausdrücklich NICHT der der Generierung.
    ///
    /// <para>
    /// <b>Warum nicht <c>CiycRandom</c>.</b> Dessen Ströme gehören Stage A. Einen davon hier zu
    /// ziehen würde ihn weitertreiben und damit in die Grundrisserzeugung zurückgreifen: derselbe
    /// Seed ergäbe je nach Einrichtung ein anderes Haus. Genau diese Regel steht schon über der
    /// Wandvariantenwahl in <see cref="ModularRoomBuilder"/>, die ihre Auswahl deshalb aus einem
    /// Hash der Raumidentität ABLEITET statt sie zu würfeln.
    /// </para>
    ///
    /// <para>
    /// Hier wird dieselbe Ableitung gebraucht, nur öfter als einmal pro Wand - also ein eigener,
    /// winziger Strom, dessen Startwert aus (Layout-Seed, Raum-ID, Zweck) gehasht ist. Er hängt
    /// von nichts ab, was zur Laufzeit anders sein kann: keine Instanz-IDs, keine
    /// Dateisystemreihenfolge, keine Adressen. Gleicher Seed und gleicher Katalog ergeben
    /// dieselbe Einrichtung, auf jeder Maschine.
    /// </para>
    /// </summary>
    public struct FurnishingRandom
    {
        private ulong _state;

        public static FurnishingRandom ForRoom(int layoutSeed, int roomId, string purpose)
        {
            var hash = Fnv1a64.Create();
            hash.WriteInt32(layoutSeed);
            hash.WriteInt32(roomId);
            hash.WriteString(purpose ?? string.Empty);

            // Nie null: xorshift bleibt bei 0 für immer bei 0.
            ulong seed = hash.Value;
            return new FurnishingRandom { _state = seed != 0UL ? seed : 0x9E3779B97F4A7C15UL };
        }

        /// <summary>xorshift64*, weil er kurz, deterministisch und plattformgleich ist.</summary>
        public ulong NextUInt64()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return unchecked(_state * 0x2545F4914F6CDD1DUL);
        }

        /// <summary>0 bis exclusiveMax - 1. Bei exclusiveMax &lt;= 0 immer 0.</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                return 0;

            return (int)(NextUInt64() % (ulong)exclusiveMax);
        }

        /// <summary>0..1.</summary>
        public float NextFloat() => (NextUInt64() >> 11) * (1.0f / 9007199254740992.0f);

        public bool NextBool(float chance) => NextFloat() < chance;

        /// <summary>
        /// Ein gewichteter Index. <paramref name="weights"/> muss so lang sein wie die Auswahl.
        /// Sind alle Gewichte null, gewinnt der erste Eintrag - nicht ein zufälliger, denn "kein
        /// Gewicht" heisst "keine Präferenz" und nicht "wähle blind".
        /// </summary>
        public int NextWeighted(System.Collections.Generic.IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count == 0)
                return 0;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                total += weights[i] > 0f ? weights[i] : 0f;

            if (total <= 0.0001f)
                return 0;

            float roll = NextFloat() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = weights[i] > 0f ? weights[i] : 0f;
                if (roll < w)
                    return i;
                roll -= w;
            }

            return weights.Count - 1;
        }

        /// <summary>
        /// Mischt eine Liste an Ort und Stelle, deterministisch (Fisher-Yates).
        /// Benutzt, damit nicht jeder Raum dieselbe Wand zuerst probiert.
        /// </summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            if (list == null)
                return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
