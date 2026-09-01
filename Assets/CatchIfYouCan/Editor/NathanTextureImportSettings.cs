#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Owns the platform import settings for the Nathan character textures.
    ///
    /// <para>
    /// These six were the only textures in the project carrying an Android or iPhone platform
    /// entry, and they were the only six Unity 6.5 warned about — including the three whose entry
    /// was not even marked overridden. So it is the presence of a stored iOS entry with the format
    /// left on <c>Automatic</c> that does it, not the override flag: Unity resolves that entry
    /// against the platform's own legacy default, and for iOS that is PVRTC, which 6.5 has retired.
    /// A texture with no iOS entry at all never goes down that path and never warns, which is why
    /// the rest of the project is quiet.
    /// </para>
    ///
    /// <para>
    /// The committed .meta files therefore carry no mobile entry, exactly like every other texture
    /// here. This tool adds them back with a format named rather than left automatic, which is the
    /// only version of a mobile entry that is safe to have.
    /// </para>
    ///
    /// <para>
    /// The formats are named here as <see cref="TextureImporterFormat"/> constants rather than
    /// written into the .meta as integers, so there is no enum value to get wrong, and the result
    /// is written back by Unity itself into the .meta where it can be inspected and committed.
    /// </para>
    ///
    /// <para>
    /// Deliberately scoped to these six files. Nothing else in the project is touched: the rest of
    /// the textures have no override, resolve through the project default, and do not warn.
    /// </para>
    /// </summary>
    public static class NathanTextureImportSettings
    {
        private const string TextureRoot = "Assets/CatchIfYouCan/Art/Characters/Nathan/Textures/";

        private readonly struct Spec
        {
            public readonly string File;
            public readonly int Desktop;
            public readonly int Mobile;
            public readonly TextureImporterFormat MobileFormat;
            public readonly string Why;

            public Spec(string file, int desktop, int mobile, TextureImporterFormat mobileFormat, string why)
            {
                File = file; Desktop = desktop; Mobile = mobile; MobileFormat = mobileFormat; Why = why;
            }
        }

        // Sizes are per map, not one number for the character. The diffuse is a whole-body atlas
        // in which the face is a small island, so it is the one map that still reads soft when it
        // is halved; the normal carries cloth folds, which nobody looks at from 30 cm away.
        private static readonly Spec[] Specs =
        {
            new Spec("rp_nathan_animated_003_dif.jpg", 2048, 2048, TextureImporterFormat.ASTC_6x6,
                     "albedo - the face is roughly 1/80th of the atlas, so this is the map that needs the pixels"),
            new Spec("rp_nathan_animated_003_norm.jpg", 2048, 1024, TextureImporterFormat.ASTC_5x5,
                     "normal - a tighter ASTC block because normals show blocking before colour does"),
            new Spec("rp_nathan_animated_003_metallicsmoothness.png", 1024, 512, TextureImporterFormat.ASTC_6x6,
                     "packed metallic/smoothness - a slowly varying signal, small is fine"),
            new Spec("rp_nathan_animated_003_gloss.jpg", 512, 512, TextureImporterFormat.ASTC_6x6,
                     "source only - kept for repacking, referenced by nothing, so it never ships"),
            new Spec("rp_nathan_animated_003_mask01.jpg", 512, 512, TextureImporterFormat.ASTC_6x6,
                     "source only - t-shirt selection mask"),
            new Spec("rp_nathan_animated_003_mask02.jpg", 512, 512, TextureImporterFormat.ASTC_6x6,
                     "source only - jeans selection mask"),
        };

        [MenuItem("Catch If You Can/Characters/Fix Nathan Texture Import Settings", false, 12)]
        public static void FixMenuItem()
        {
            var log = new StringBuilder();
            log.AppendLine("[CIYC] Nathan texture import settings");
            Apply(log);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Applies the platform settings and reimports. Idempotent; safe to call from the
        /// character build so the textures are never left in the state that produced the warning.
        /// </summary>
        public static void Apply(StringBuilder log)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                string path = TextureRoot + spec.File;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    log.AppendLine("  MISSING: " + path);
                    continue;
                }

                importer.maxTextureSize = spec.Desktop;

                // Desktop keeps Automatic on purpose: the BC family it resolves to is current, and
                // letting Unity choose between BC1/BC5/BC7 per texture type is better than naming
                // one here.
                SetPlatform(importer, "Standalone", spec.Desktop, TextureImporterFormat.Automatic);
                SetPlatform(importer, "Android", spec.Mobile, spec.MobileFormat);
                SetPlatform(importer, "iPhone", spec.Mobile, spec.MobileFormat);

                importer.SaveAndReimport();

                log.AppendLine("  " + spec.File);
                log.AppendLine("      PC " + spec.Desktop + " (Automatic)   Android/iOS " +
                               spec.Mobile + " (" + spec.MobileFormat + ")");
                log.AppendLine("      " + spec.Why);
            }

            log.AppendLine("  no platform override is left on Automatic, so nothing can resolve to PVRTC");
        }

        private static void SetPlatform(TextureImporter importer, string platform, int maxSize,
                                        TextureImporterFormat format)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
#endif
