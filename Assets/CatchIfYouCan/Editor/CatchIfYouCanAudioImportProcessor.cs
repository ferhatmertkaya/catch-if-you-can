using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public class CatchIfYouCanAudioImportProcessor : AssetPostprocessor
    {
        private const string AudioRoot = "Assets/CatchIfYouCan/Audio/";
        private const float VorbisQuality = 0.7f;
        private const long WarnUncompressedBytes = 5L * 1024L * 1024L;

        private static readonly List<string> BatchWarnings = new List<string>();
        private static bool BatchHasCiycAudio;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot))
                return;

            BatchHasCiycAudio = true;

            var importer = (AudioImporter)assetImporter;

            ApplyCategorySettings(importer, assetPath);
            ValidateImport(importer, assetPath);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!BatchHasCiycAudio)
                return;

            BatchHasCiycAudio = false;

            if (BatchWarnings.Count == 0)
                return;

            var report = new StringBuilder();
            report.AppendLine("[CIYC Audio Import] Batch warnings:");

            for (int i = 0; i < BatchWarnings.Count; i++)
                report.AppendLine("  • " + BatchWarnings[i]);

            Debug.LogWarning(report.ToString());
            BatchWarnings.Clear();
        }

        private static void ApplyCategorySettings(AudioImporter importer, string path)
        {
            string normalized = path.Replace('\\', '/');

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            if (normalized.Contains("/Ambience/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;

                importer.defaultSampleSettings = settings;

                bool isExterior = normalized.Contains(
                    "/Exterior/",
                    System.StringComparison.OrdinalIgnoreCase);

                importer.forceToMono = !isExterior;
                return;
            }

            if (normalized.Contains("/Music/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;

                importer.defaultSampleSettings = settings;
                return;
            }

            if (normalized.Contains("/Ghost/"))
            {
                importer.forceToMono = true;

                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;
                settings.loadType = IsLikelyShortClip(importer)
                    ? AudioClipLoadType.DecompressOnLoad
                    : AudioClipLoadType.CompressedInMemory;

                importer.defaultSampleSettings = settings;
                return;
            }

            if (normalized.Contains("/Foley/Footsteps/"))
            {
                importer.forceToMono = true;

                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;

                importer.defaultSampleSettings = settings;
                return;
            }

            if (normalized.Contains("/UI/"))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;

                importer.defaultSampleSettings = settings;
                return;
            }

            if (normalized.Contains("/Equipment/"))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;

                importer.defaultSampleSettings = settings;
                return;
            }

            if (normalized.Contains("/Generated/"))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;

                importer.defaultSampleSettings = settings;
            }
        }

        private static bool IsLikelyShortClip(AudioImporter importer)
        {
            AudioImporterSampleSettings defaultSettings =
                importer.defaultSampleSettings;

            if (defaultSettings.loadType == AudioClipLoadType.DecompressOnLoad)
                return true;

            long fileBytes = 0;

            try
            {
                if (File.Exists(importer.assetPath))
                    fileBytes = new FileInfo(importer.assetPath).Length;
            }
            catch
            {
                return true;
            }

            return fileBytes > 0 && fileBytes < 256 * 1024;
        }

        private static void ValidateImport(AudioImporter importer, string path)
        {
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;

            uint sampleRate =
                settings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate
                    ? settings.sampleRateOverride
                    : 0u;

            if (sampleRate > 0 &&
                sampleRate != 22050u &&
                sampleRate != 44100u &&
                sampleRate != 48000u)
            {
                BatchWarnings.Add(
                    $"{Path.GetFileName(path)}: sample rate {sampleRate} Hz " +
                    "(prefer 22050/44100/48000)");
            }

            long uncompressed = EstimateUncompressedBytes(importer);

            if (uncompressed > WarnUncompressedBytes)
            {
                float mb = uncompressed / (1024f * 1024f);

                BatchWarnings.Add(
                    $"{Path.GetFileName(path)}: ~{mb:F1} MB " +
                    "uncompressed estimate (> 5 MB)");
            }
        }

        private static long EstimateUncompressedBytes(AudioImporter importer)
        {
            long fileBytes = 0;

            try
            {
                if (File.Exists(importer.assetPath))
                    fileBytes = new FileInfo(importer.assetPath).Length;
            }
            catch
            {
                return 0;
            }

            if (fileBytes <= 0)
                return 0;

            string ext =
                Path.GetExtension(importer.assetPath).ToLowerInvariant();

            if (ext == ".wav")
                return fileBytes;

            return fileBytes * 4;
        }
    }
}