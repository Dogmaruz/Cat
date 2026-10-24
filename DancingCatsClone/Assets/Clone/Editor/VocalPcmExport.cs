using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Exports the actual imported playback PCM without changing the AudioImporter.</summary>
    public static class VocalPcmExport
    {
        [Serializable] private sealed class Metadata
        {
            public string sourceFileSha256, sourcePcmSha256;
            public int sampleRate, channels, sampleFrames;
        }

        [MenuItem("Assets/Dancing Cats/Export selected audio PCM for vocal analysis")]
        public static void ExportSelected()
        {
            var clip = Selection.activeObject as AudioClip;
            if (clip == null) throw new InvalidOperationException("Select an imported AudioClip first.");
            Export(clip);
        }

        public static string Export(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            string source = Path.GetFullPath(AssetDatabase.GetAssetPath(clip));
            if (!File.Exists(source)) throw new FileNotFoundException("The selected clip has no source audio file.", source);
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var pcm = new float[checked(clip.samples * clip.channels)];
            if (clip.loadState != AudioDataLoadState.Loaded || !clip.GetData(pcm, 0))
                throw new InvalidOperationException("The imported clip must expose its decoded PCM for local analysis.");
            foreach (float value in pcm)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Imported PCM contains a non-finite sample.");
            string sourceHash = Hash(source);
            string folder = Path.GetFullPath(Path.Combine("Library/VocalAnalysis", sourceHash));
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder, "source.f32");
            using (var stream = File.Create(output))
            {
                var buffer = new byte[32768];
                for (int first = 0; first < pcm.Length; first += buffer.Length / sizeof(float))
                {
                    int bytes = Math.Min(buffer.Length / sizeof(float), pcm.Length - first) * sizeof(float);
                    Buffer.BlockCopy(pcm, first * sizeof(float), buffer, 0, bytes);
                    if (!BitConverter.IsLittleEndian) for (int index = 0; index < bytes; index += 4) Array.Reverse(buffer, index, 4);
                    stream.Write(buffer, 0, bytes);
                }
            }
            var metadata = new Metadata { sourceFileSha256 = sourceHash, sourcePcmSha256 = Hash(output),
                sampleRate = clip.frequency, channels = clip.channels, sampleFrames = clip.samples };
            File.WriteAllText(Path.Combine(folder, "source.json"), JsonUtility.ToJson(metadata, true) + "\n");
            Debug.Log("VOCAL_PCM_EXPORT PASS: actual imported float32 little-endian PCM SHA-256 " + metadata.sourcePcmSha256 + "; " + folder);
            return folder;
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
