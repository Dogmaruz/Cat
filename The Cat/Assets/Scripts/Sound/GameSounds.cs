using UnityEngine;
using System;
# if UNITY_EDITOR
using UnityEditor;
#endif


[CreateAssetMenu(fileName = "GameSounds", menuName = "Sounds/GameSounds")]
public class GameSounds : ScriptableObject
{
    public AudioClip[] m_sounds;

    public AudioClip this[Sound sound] => m_sounds[(int)sound];


#if UNITY_EDITOR
    [CustomEditor(typeof(GameSounds))]
    public class SoundsInspector : Editor
    {
        private static readonly int soundCount = Enum.GetValues(typeof(Sound)).Length;
        private new GameSounds target => base.target as GameSounds;

        public override void OnInspectorGUI()
        {
            if (target.m_sounds.Length < soundCount)
            {
                Array.Resize(ref target.m_sounds, soundCount);
            }

            for (int i = 0; i < target.m_sounds.Length; i++)
            {
                target.m_sounds[i] = EditorGUILayout.ObjectField($"{(Sound)i} : ", target.m_sounds[i], typeof(AudioClip), false) as AudioClip;
            }

            EditorUtility.SetDirty(target);
        }
    }
#endif
}
