using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools.DOTS
{
    /// <summary>
    /// Regenerates the DOTS projector's cookie textures, deterministically, from code.
    ///
    /// <para>
    /// The four PNGs are committed, so a fresh clone needs nothing from Photoshop, Substance
    /// or a download. This exists so they can be CHANGED - density, dot size, seed - and come
    /// back with the same PIXELS for the same parameters, rather than being a binary nobody
    /// can reproduce. (The same pixels, not the same bytes: two PNG encoders may compress the
    /// identical image differently, so it is the image that is reproducible, not the file.)
    /// </para>
    ///
    /// <para>
    /// <b>The mask is written into every channel.</b> Which channel a light cookie is sampled
    /// from is a per-pipeline detail, and this machine cannot reach docs.unity3d.com to settle
    /// it. Rather than guess one and ship a black cookie if the guess is wrong, R, G, B and A
    /// all carry the same value - correct whichever is read, at no cost, because the importer
    /// keeps the source alpha (alphaUsage 1) and is told the texture is not transparency.
    /// </para>
    ///
    /// <para>
    /// The spherical cookie is the interesting one. Dots are distributed evenly on the SPHERE
    /// by a Fibonacci lattice, not on the square, and each is drawn with its horizontal radius
    /// divided by cos(latitude) - which is exactly the stretch an equirectangular map applies,
    /// so the dot comes back round on the sphere instead of smearing into an ellipse near the
    /// poles. A dot crossing u=0 is drawn a second time at the far edge, or the seam cuts it in
    /// half. That is the "Cartesian to polar" step, done as geometry rather than as a filter.
    /// </para>
    ///
    /// <para>
    /// Only the spot cookie lives under <c>Resources</c>. Everything in that folder is pulled
    /// into every build whether or not anything references it, so the source pattern, the
    /// equirectangular map and the checker - all authoring and diagnostic assets - sit under
    /// <c>Textures/DOTS</c> instead and cost a shipped player nothing.
    /// </para>
    /// </summary>
    public static class DotsCookieGenerator
    {
        /// <summary>The one cookie the game loads at runtime, so the one that must ship.</summary>
        private const string RuntimeFolder = "Assets/CatchIfYouCan/Resources/Equipment/DOTS";

        /// <summary>Source art and diagnostics. Deliberately NOT under Resources.</summary>
        private const string AuthoringFolder = "Assets/CatchIfYouCan/Art/Equipment/DOTS";

        /// <summary>Size of the authoring masters.</summary>
        public const int Resolution = 2048;

        /// <summary>
        /// Size of the cookie the game loads, authored at the size it is USED at.
        /// CIYC_URP.asset sets m_AdditionalLightsCookieResolution to 2048, so a 2048 cookie is
        /// the whole atlas and leaves room for no other cookied light; and a mask the importer
        /// has to downscale loses its dots to the filter. 1024 with a 2 px radius keeps a 4 px
        /// dot in a 16 px cell.
        /// </summary>
        public const int SpotResolution = 1024;

        public const int DotCount = 4096;
        public const float DotRadius = 3.0f;
        public const float SpotDotRadius = 2.0f;
        public const int Seed = 20260910;

        [MenuItem("Catch If You Can/4. SPIELINHALT/Equipment/DOTS-Cookies erzeugen [SCHREIBT DATEIEN]")]
        public static void Generate()
        {
            if (!EditorUtility.DisplayDialog(
                    "Generate DOTS cookies",
                    "Writes four PNG files.\n\n" +
                    RuntimeFolder + "\n" +
                    "   T_DOTS_SpotCookie_1024  (the one the game loads)\n\n" +
                    AuthoringFolder + "\n" +
                    "   T_DOTS_SourcePattern_2048  (flat pattern)\n" +
                    "   T_DOTS_SphericalCookie_2048  (point-light, equirectangular)\n" +
                    "   T_DOTS_TestCookie  (checker, to prove cookies apply at all)\n\n" +
                    "Existing files are overwritten and reimported. Nothing else in the " +
                    "project is touched, and the open scene is not saved.",
                    "Write them", "Cancel"))
            {
                return;
            }

            Directory.CreateDirectory(RuntimeFolder);
            Directory.CreateDirectory(AuthoringFolder);

            WritePng(BuildSpotCookie(), RuntimeFolder, "T_DOTS_SpotCookie_1024.png");
            WritePng(BuildFlatPattern(), AuthoringFolder, "T_DOTS_SourcePattern_2048.png");
            WritePng(BuildSphericalCookie(), AuthoringFolder, "T_DOTS_SphericalCookie_2048.png");
            WritePng(BuildTestCookie(), AuthoringFolder, "T_DOTS_TestCookie.png");

            AssetDatabase.Refresh();

            ApplyCookieImport(RuntimeFolder, "T_DOTS_SpotCookie_1024.png", SpotResolution, false);
            ApplyCookieImport(AuthoringFolder, "T_DOTS_SourcePattern_2048.png", Resolution, false);
            ApplyCookieImport(AuthoringFolder, "T_DOTS_SphericalCookie_2048.png", Resolution, true);
            ApplyCookieImport(AuthoringFolder, "T_DOTS_TestCookie.png", 512, false);

            Debug.Log("[CIYC][DOTS] Wrote 4 cookies, seed " + Seed + ". Runtime cookie: " +
                      SpotResolution + " px, radius " + SpotDotRadius + " px, in " +
                      RuntimeFolder + ". Authoring masters: " + Resolution + " px, radius " +
                      DotRadius + " px, in " + AuthoringFolder + ".");
        }

        private static uint _state;

        private static void Reseed(int seed) => _state = (uint)seed;

        /// <summary>Xorshift: the same numbers on every machine, unlike Random's shared state.</summary>
        private static float NextFloat()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (_state & 0xFFFFFF) / (float)0xFFFFFF;
        }

        private static Texture2D BuildFlatPattern()
        {
            Reseed(Seed);
            var tex = Blank(Resolution);
            int side = Mathf.CeilToInt(Mathf.Sqrt(DotCount));
            float cell = Resolution / (float)side;

            int placed = 0;
            for (int gy = 0; gy < side && placed < DotCount; gy++)
            for (int gx = 0; gx < side && placed < DotCount; gx++, placed++)
            {
                float x = (gx + 0.5f) * cell + (NextFloat() - 0.5f) * cell * 0.55f;
                float y = (gy + 0.5f) * cell + (NextFloat() - 0.5f) * cell * 0.55f;
                Stamp(tex, x, y, DotRadius, DotRadius);
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D BuildSphericalCookie()
        {
            var tex = Blank(Resolution);
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < DotCount; i++)
            {
                float z = 1f - (2f * i + 1f) / DotCount;        // even in solid angle
                float lat = Mathf.Asin(Mathf.Clamp(z, -1f, 1f));
                float lon = (i * golden) % (2f * Mathf.PI);

                float u = lon / (2f * Mathf.PI) * Resolution;
                float v = (0.5f - lat / Mathf.PI) * Resolution;
                float cosLat = Mathf.Max(0.15f, Mathf.Cos(lat));
                float rx = DotRadius / cosLat;

                Stamp(tex, u, v, rx, DotRadius);
                if (u - rx < 0f) Stamp(tex, u + Resolution, v, rx, DotRadius);
                else if (u + rx > Resolution) Stamp(tex, u - Resolution, v, rx, DotRadius);
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D BuildSpotCookie()
        {
            Reseed(Seed ^ 0x5A5A);
            var tex = Blank(SpotResolution);
            int side = Mathf.CeilToInt(Mathf.Sqrt(DotCount));
            float cell = SpotResolution / (float)side;
            float c = SpotResolution * 0.5f;

            for (int gy = 0; gy < side; gy++)
            for (int gx = 0; gx < side; gx++)
            {
                float x = (gx + 0.5f) * cell + (NextFloat() - 0.5f) * cell * 0.55f;
                float y = (gy + 0.5f) * cell + (NextFloat() - 0.5f) * cell * 0.55f;

                float t = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                if (t > 1f - SpotDotRadius / c)
                    continue;

                // Faded to black at the rim, or the cookie's square edge shows as a square.
                Stamp(tex, x, y, SpotDotRadius, SpotDotRadius, Mathf.Clamp01((1f - t) / 0.25f));
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D BuildTestCookie()
        {
            var tex = Blank(512);
            for (int y = 0; y < 512; y++)
            for (int x = 0; x < 512; x++)
                if (((x / 64) + (y / 64)) % 2 == 0)
                    tex.SetPixel(x, y, Color.white);

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Fully transparent black: alpha 0 rather than <c>Color.black</c>, whose alpha is 1.
        /// A background of alpha 1 would make the whole mask solid in the alpha channel, which
        /// is precisely the channel a spot cookie may be read from.
        /// </summary>
        private static Texture2D Blank(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0f, 0f, 0f, 0f);
            tex.SetPixels(clear);
            return tex;
        }

        private static void Stamp(Texture2D tex, float cx, float cy, float rx, float ry,
                                  float brightness = 1f)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
            int x1 = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + rx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry));
            int y1 = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + ry));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                if (dx * dx + dy * dy > 1f)
                    continue;

                float v = Mathf.Max(tex.GetPixel(x, y).r, brightness);
                tex.SetPixel(x, y, new Color(v, v, v, v));
            }
        }

        private static void WritePng(Texture2D tex, string folder, string file)
        {
            File.WriteAllBytes(Path.Combine(folder, file), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// A cookie is a MASK: linear rather than sRGB, mipmapped so the dots do not alias into
        /// a shimmer at range, and clamped vertically. The equirectangular map repeats
        /// horizontally so its seam is continuous; a spot cookie clamps both ways or the
        /// pattern tiles across the cone.
        ///
        /// <para>
        /// Compression is set through the per-platform settings that
        /// <c>InteractiveRoomSkySetup</c> already uses here, rather than through the importer's
        /// own compression property: the platform path is the one this project has run, and the
        /// distinction matters because desktop and mobile want opposite answers. Desktop keeps
        /// the mask uncompressed. Mobile cannot: 2048 RGBA32 is 16 MiB before mipmaps and this
        /// game targets phones, so it gets ASTC 6x6 (about 1.9 MiB) - the dots survive it
        /// because one texel of this cookie lands on roughly 3 mm of wall at working range.
        /// </para>
        ///
        /// <para>
        /// Filter mode and alpha source are deliberately NOT set from code. Both are real
        /// importer members as far as I know, and "as far as I know" is not good enough for an
        /// Editor script: a wrong member name does not fail this one tool, it fails the whole
        /// Editor assembly, and docs.unity3d.com is unreachable from here (mistake 9). The
        /// committed <c>.meta</c> files carry trilinear filtering and alphaUsage 1, and
        /// overwriting a PNG in place keeps its <c>.meta</c>, so regenerating preserves them.
        /// </para>
        /// </summary>
        private static void ApplyCookieImport(string folder, string file, int maxSize,
                                              bool wrapHorizontally)
        {
            string path = folder + "/" + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[CIYC][DOTS] No importer at " + path);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapModeU = wrapHorizontally ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;

            SetPlatform(importer, "Standalone", maxSize,
                        TextureImporterFormat.Automatic, TextureImporterCompression.Uncompressed);
            SetPlatform(importer, "Android", maxSize,
                        TextureImporterFormat.ASTC_6x6, TextureImporterCompression.Compressed);
            SetPlatform(importer, "iPhone", maxSize,
                        TextureImporterFormat.ASTC_6x6, TextureImporterCompression.Compressed);

            importer.SaveAndReimport();
        }

        private static void SetPlatform(TextureImporter importer, string platform, int maxSize,
                                        TextureImporterFormat format,
                                        TextureImporterCompression compression)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = format;
            settings.textureCompression = compression;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
