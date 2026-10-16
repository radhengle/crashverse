#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>All music is streamed (mixing_unity.md): Streaming, Vorbis 40% stems / 50% full mixes, load in background.</summary>
    public class MusicImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
            var imp = (AudioImporter)assetImporter;
            bool stem = assetPath.Contains("titan_int");
            bool stinger = assetPath.Contains("stinger_");
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = stem ? 0.40f : stinger ? 0.60f : 0.50f;
            s.preloadAudioData = false;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = true;
            imp.forceToMono = false;
        }
    }
}
#endif
