using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DancingCat.EditorTools
{
    public sealed class CatChartTools : EditorWindow
    {
        private AudioClip clip;
        private float bpm = 120;
        private float offset;
        private float onsetThreshold = 1.35f;
        private int difficulty = 2;
        private int seed = 7;

        [MenuItem("Dancing Cat/Analyze selected audio")]
        private static void Open()
        {
            var window = GetWindow<CatChartTools>("Cat chart analyzer");
            window.clip = Selection.activeObject as AudioClip;
            window.minSize = new Vector2(360, 280);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create a playable chart from local audio", EditorStyles.boldLabel);
            clip = (AudioClip)EditorGUILayout.ObjectField("Audio", clip, typeof(AudioClip), false);
            bpm = EditorGUILayout.FloatField("Tempo (BPM)", bpm);
            offset = EditorGUILayout.FloatField("First beat (seconds)", offset);
            onsetThreshold = EditorGUILayout.Slider("Onset contrast", onsetThreshold, 1.05f, 3f);
            difficulty = EditorGUILayout.IntSlider("Difficulty", difficulty, 1, 4);
            seed = EditorGUILayout.IntField("Lane seed", seed);
            EditorGUILayout.HelpBox("This creates a chart suggestion with short tiles, sustained holds and occasional " +
                "moving holds. Listen and review the result before using it as a finished level. " +
                "The audio needs Decompress On Load.", MessageType.Info);

            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (GUILayout.Button("Analyze and create song asset", GUILayout.Height(30))) CreateSong();
            }
        }

        private void CreateSong()
        {
            var song = CreateInstance<CatSong>();
            try
            {
                song.id = Guid.NewGuid().ToString("N");
                song.title = clip.name;
                song.artist = "Local audio";
                song.clip = clip;
                song.bpm = bpm;
                song.offset = offset;
                song.duration = clip.length;
                song.difficulty = difficulty;
                CatChartAnalyzer.Analyze(song, seed, onsetThreshold);

                const string folder = "Assets/DancingCat/Songs";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/DancingCat", "Songs");
                var filename = clip.name;
                foreach (var character in Path.GetInvalidFileNameChars()) filename = filename.Replace(character, '_');
                if (string.IsNullOrWhiteSpace(filename)) filename = "Song";
                var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + filename + ".asset");
                AssetDatabase.CreateAsset(song, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = song;
                EditorGUIUtility.PingObject(song);
                Debug.Log("Created " + path + " with " + song.notes.Count + " chart notes.", song);
            }
            catch (Exception exception)
            {
                if (!AssetDatabase.Contains(song)) DestroyImmediate(song);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Cannot analyze audio", exception.Message, "OK");
            }
        }
    }
}
