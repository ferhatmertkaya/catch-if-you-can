using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The one place the night sky lives.
    ///
    /// <para>
    /// Two things want it - the lobby's exterior, which puts it on the player's camera, and the
    /// investigation, which puts it in <c>RenderSettings</c> - and they had the path written out
    /// separately. One string in two files is the shape of CLAUDE.md mistake 3: the day one of
    /// them is renamed, the other resolves to nothing and says so in a log nobody reads.
    /// </para>
    /// </summary>
    public static class CiycSky
    {
        /// <summary>Relative to a Resources folder, with no extension. See Resources/Sky/.</summary>
        public const string PanoramaResourcePath = "Sky/MAT_Skybox_HauntedNight";

        private static Material _cached;

        /// <summary>
        /// The night panorama, or null with the miss reported. Cached, because a skybox is asked
        /// for once per scene and <c>Resources.Load</c> is not free.
        /// </summary>
        public static Material LoadPanorama()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<Material>(PanoramaResourcePath);

            if (_cached == null)
            {
                Core.CIYCLog.Error("[CIYC][Sky] Resources.Load<Material>(\"" +
                                   PanoramaResourcePath + "\") ergab NULL. Erwartet wird " +
                                   "Assets/**/Resources/" + PanoramaResourcePath + ".mat. Ohne " +
                                   "sie zeichnet Unity ihren eingebauten Prozedur-Himmel, und " +
                                   "der ist HELLBLAU - was durch jedes Fenster und jede " +
                                   "Oeffnung im Haus als blaues Rechteck erscheint.");
            }

            return _cached;
        }

        /// <summary>Statics survive play mode; a fresh process has loaded nothing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _cached = null;
    }
}
