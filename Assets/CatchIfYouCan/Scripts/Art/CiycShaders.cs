using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Every shader this project asks for by name, and the one place that asks.
    ///
    /// <para>
    /// This exists because of the magenta mirror. <see cref="Shader.Find"/> returns what is in
    /// the build, and a player build contains only the shaders its materials ask for - so a
    /// URP shader nothing references is absent on the device while being present in the
    /// editor. Code all over the project reacted to that by falling back to a Built-in Render
    /// Pipeline shader (<c>Standard</c>, <c>Particles/Standard Unlit</c>), which is in Always
    /// Included Shaders and therefore always resolves. Under URP a built-in shader draws solid
    /// magenta. Three of those fallbacks asked for the built-in shader <b>first</b>, so they
    /// were magenta in the editor too.
    /// </para>
    ///
    /// <para>
    /// There is no fallback here on purpose. A missing object is better than a magenta one,
    /// and an error naming the shader is better than either.
    /// </para>
    /// </summary>
    public static class CiycShaders
    {
        public const string Lit = "Universal Render Pipeline/Lit";
        public const string Unlit = "Universal Render Pipeline/Unlit";
        public const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        public const string GhostDissolve = "CatchIfYouCan/GhostDissolve";
        public const string UVEvidence = "CatchIfYouCan/UVEvidence";
        public const string SpectralGrid = "CatchIfYouCan/SpectralGrid";
        public const string ElectronicGlitch = "CatchIfYouCan/ElectronicGlitch";
        public const string UISlime = "CatchIfYouCan/UI/Slime";
        public const string PlanarMirror = "CatchIfYouCan/PlanarMirror";

        // One complaint per shader per session. A shader that is missing is missing every
        // frame something tries to build with it, and a log line per frame buries the rest.
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>
        /// A shader by name, or null - never a shader from the wrong render pipeline. Anything
        /// not in the build or not supported on this device would be a magenta surface.
        /// </summary>
        public static Shader Find(string name)
        {
            var shader = Shader.Find(name);

            if (shader == null)
            {
                Report(name, "is not in this build. Nothing in the project references it, so " +
                             "it was stripped. Put it on a material under Resources, or in " +
                             "Always Included Shaders.");
                return null;
            }

            if (!shader.isSupported)
            {
                Report(name, "is not supported on this device.");
                return null;
            }

            return shader;
        }

        /// <summary>The lit shader everything opaque in this project is built from.</summary>
        public static Shader FindLit() => Find(Lit);

        private static void Report(string name, string why)
        {
            if (!Reported.Add(name))
                return;

            Debug.LogError("[CIYC] Shader '" + name + "' " + why);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Reported.Clear();
    }
}
