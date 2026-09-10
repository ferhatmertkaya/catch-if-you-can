using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools.DOTS
{
    /// <summary>
    /// Regenerates the DOTS projector's cookie masks, deterministically, from code.
    ///
    /// <para>
    /// The PNGs are committed, so a fresh clone needs nothing from Photoshop or a download.
    /// This exists so they can be CHANGED - density, dot size, seed - and come back with the
    /// same pixels for the same parameters, rather than being a binary nobody can reproduce.
    /// (The same pixels, not the same bytes: two PNG encoders may compress an identical image
    /// differently, so it is the image that is reproducible, not the file.)
    /// </para>
    ///
    /// <para>
    /// <b>Every dot is a true circle, and the generator proves it.</b> One radius in both axes,
    /// area-coverage antialiased rather than a hard inside/outside test, and every centre
    /// snapped to a PIXEL CENTRE - a disc whose centre lands at an arbitrary fraction rasterises
    /// to a 3 px box or a 4 px box depending on where it fell, so the dots would differ from
    /// each other even though every one is a genuine circle. Snapped, every dot is the identical
    /// stamp, which is what lets <see cref="MeasureBlobs"/> be exact instead of approximate: it
    /// flood-fills the finished texture and refuses to write it if any blob is not as wide as it
    /// is tall. The jitter survives - a cell is about 39 px across, so whole-pixel offsets are
    /// still irregular.
    /// </para>
    ///
    /// <para>
    /// <b>On the dot count.</b> A cookie's dot count is free at runtime - it is a texture,
    /// sampled once per lit pixel whatever is drawn on it - so this number is a readability
    /// decision, not a performance one. What costs per frame is the five lights in the cluster.
    /// The count is deliberately low BECAUSE there are five: each light projects the whole
    /// cookie, so the marks a player sees is roughly five times the dots inside the cone.
    /// </para>
    /// </summary>
    public static class DotsCookieGenerator
    {
        private const string RuntimeFolder = "Assets/CatchIfYouCan/Resources/Equipment/DOTS";

        /// <summary>
        /// Dots per cookie, before the cone clips the corners off.
        ///
        /// <para>
        /// About 129 of these land inside the inscribed circle, and
        /// <c>SpectralGridProjection</c> runs five lights off one cookie, so a room shows
        /// roughly 645 marks. Raising this multiplies by five.
        /// </para>
        /// </summary>
        public const int DotCount = 160;

        public const int Seed = 20260910;

        private const int PrimarySize = 512;
        private const float PrimaryRadius = 1.5f;
        private const int LowSize = 256;
        private const float LowRadius = 0.9f;

        [MenuItem("Catch If You Can/4. SPIELINHALT/Equipment/DOTS-Cookies erzeugen [SCHREIBT DATEIEN]")]
        public static void Generate()
        {
            if (!EditorUtility.DisplayDialog(
                    "Generate DOTS cookies",
                    "Writes two PNG masks into\n" + RuntimeFolder + "\n\n" +
                    "T_DOTS_RoomCookie_512  (the one the game loads)\n" +
                    "T_DOTS_RoomCookie_256  (low-quality variant)\n\n" +
                    DotCount + " dots, radius " + PrimaryRadius + " px, seed " + Seed + ".\n\n" +
                    "Existing files are overwritten and reimported. Nothing else in the " +
                    "project is touched, and the open scene is not saved.",
                    "Write them", "Cancel"))
            {
                return;
            }

            Directory.CreateDirectory(RuntimeFolder);

            if (!WriteCookie(PrimarySize, PrimaryRadius, "T_DOTS_RoomCookie_512.png"))
                return;
            if (!WriteCookie(LowSize, LowRadius, "T_DOTS_RoomCookie_256.png"))
                return;

            AssetDatabase.Refresh();

            ApplyCookieImport("T_DOTS_RoomCookie_512.png", PrimarySize);
            ApplyCookieImport("T_DOTS_RoomCookie_256.png", LowSize);
        }

        private static bool WriteCookie(int size, float radius, string file)
        {
            // Built as a plain mask buffer and measured there, rather than written to a texture
            // and read back. Same bytes, no GetPixels round trip, and nothing depends on the
            // asset being readable.
            byte[] mask = Build(size, radius, out int inCone);

            var boxes = MeasureBlobs(mask, size);
            int notSquare = 0;
            foreach (var b in boxes)
            {
                if (b.x != b.y)
                    notSquare++;
            }

            if (notSquare > 0)
            {
                // Refused rather than written. A cookie of elliptical marks is the exact defect
                // this generator was rewritten to stop producing, and it is not visible in a
                // thumbnail - it shows up as pills on a wall, three steps and one build later.
                Debug.LogError("[CIYC][DOTS] REFUSED to write " + file + ": " + notSquare +
                               " of " + boxes.Count + " dots are not as wide as they are tall.");
                return false;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < mask.Length; i++)
            {
                // The mask goes into every channel: which one a light cookie is sampled from is
                // a per-pipeline detail this machine cannot look up, and the only configuration
                // seen working in the editor has it in all four.
                float v = mask[i] / 255f;
                pixels[i] = new Color(v, v, v, v);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(RuntimeFolder, file), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log("[CIYC][DOTS] " + file + ": " + size + " px, radius " + radius +
                      " px, " + inCone + " dots inside the cone, " + boxes.Count +
                      " blobs measured, all square.");
            return true;
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

        private static byte[] Build(int size, float radius, out int inCone)
        {
            Reseed(Seed);

            var mask = new byte[size * size];

            int side = Mathf.CeilToInt(Mathf.Sqrt(DotCount));
            float cell = size / (float)side;
            float c = size * 0.5f;

            inCone = 0;
            int placed = 0;

            for (int gy = 0; gy < side; gy++)
            for (int gx = 0; gx < side; gx++)
            {
                if (placed >= DotCount)
                    break;
                placed++;

                // Jitter capped at 0.35 of a cell each way, so two neighbours can never close
                // to touching: the gap is cell - 2*radius and a cell is far wider than that.
                float x = (gx + 0.5f) * cell + (NextFloat() - 0.5f) * cell * 0.70f;
                float y = (gy + 0.5f) * cell + (NextFloat() - 0.5f) * cell * 0.70f;

                x = Mathf.Floor(x) + 0.5f;
                y = Mathf.Floor(y) + 0.5f;

                float t = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                if (t > 1f - (radius + 1f) / c)
                    continue;                    // outside the cone; the spot clips it anyway

                Stamp(mask, size, x, y, radius);
                inCone++;
            }

            return mask;
        }

        /// <summary>One disc, area-antialiased: coverage per pixel, not a hard inside test.</summary>
        private static void Stamp(byte[] mask, int size, float cx, float cy, float r)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r + 1f));

            const int Samples = 4;
            const float Step = 1f / Samples;

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    float px = x + (sx + 0.5f) * Step;
                    float py = y + (sy + 0.5f) * Step;
                    float dx = px - cx;
                    float dy = py - cy;
                    if (dx * dx + dy * dy <= r * r)
                        hit++;
                }

                if (hit == 0)
                    continue;

                byte v = (byte)Mathf.RoundToInt(hit / (float)(Samples * Samples) * 255f);
                int idx = y * size + x;
                if (v > mask[idx])
                    mask[idx] = v;
            }
        }

        /// <summary>
        /// Flood-fills every lit blob and returns the size of its bounding box. This is the
        /// generator inspecting its own output: a dot is a circle when its box is square, and
        /// because every centre is snapped to a pixel centre, every box should be identical.
        /// </summary>
        private static List<Vector2Int> MeasureBlobs(byte[] mask, int size)
        {
            var seen = new bool[size * size];
            var boxes = new List<Vector2Int>();
            var stack = new Stack<int>();

            for (int start = 0; start < mask.Length; start++)
            {
                if (mask[start] == 0 || seen[start])
                    continue;

                stack.Clear();
                stack.Push(start);
                seen[start] = true;

                int minX = start % size, maxX = minX;
                int minY = start / size, maxY = minY;

                while (stack.Count > 0)
                {
                    int j = stack.Pop();
                    int jx = j % size, jy = j / size;

                    if (jx < minX) minX = jx;
                    if (jx > maxX) maxX = jx;
                    if (jy < minY) minY = jy;
                    if (jy > maxY) maxY = jy;

                    TryPush(mask, seen, stack, size, jx + 1, jy);
                    TryPush(mask, seen, stack, size, jx - 1, jy);
                    TryPush(mask, seen, stack, size, jx, jy + 1);
                    TryPush(mask, seen, stack, size, jx, jy - 1);
                }

                boxes.Add(new Vector2Int(maxX - minX + 1, maxY - minY + 1));
            }

            return boxes;
        }

        private static void TryPush(byte[] mask, bool[] seen, Stack<int> stack,
                                    int size, int x, int y)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
                return;

            int k = y * size + x;
            if (mask[k] == 0 || seen[k])
                return;

            seen[k] = true;
            stack.Push(k);
        }

        /// <summary>
        /// A cookie is a MASK that is only ever MAGNIFIED - 512 texels spread across a whole
        /// room - so three of Unity's defaults are actively harmful here.
        ///
        /// <para>
        /// Mipmaps are never selected at magnification and only soften the dots. Anisotropic
        /// filtering is a DIRECTIONAL filter: on a 3 px dot at a grazing angle it smears the dot
        /// along one axis, which is precisely the pill this generator was rewritten to stop
        /// drawing. And URP packs additional-light cookies into one atlas, where a repeating
        /// cookie bleeds across its neighbours. Off, 1, and Clamp on both axes.
        /// </para>
        ///
        /// <para>
        /// Filter mode and compression are deliberately NOT set from code. Both are real
        /// importer members as far as I know, and "as far as I know" is not good enough for an
        /// Editor script: a wrong member name does not fail this one tool, it fails the whole
        /// Editor assembly, and docs.unity3d.com is unreachable from here (mistake 9). The
        /// committed <c>.meta</c> files carry bilinear filtering, aniso 1 and uncompressed on
        /// every platform, and overwriting a PNG in place keeps its <c>.meta</c>.
        /// </para>
        /// </summary>
        private static void ApplyCookieImport(string file, int maxSize)
        {
            string path = RuntimeFolder + "/" + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[CIYC][DOTS] No importer at " + path);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }
    }
}
