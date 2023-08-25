using UnityEngine;

public class BackgroundSoundPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource m_AudioSource;
    public AudioSource AudioSource { get => m_AudioSource; set => m_AudioSource = value; }

    public void Play(AudioClip bckgroundSound, float volume)
    {
        if (m_AudioSource.isPlaying)
        {
            m_AudioSource?.Stop();
        }

        m_AudioSource.clip = bckgroundSound;

        m_AudioSource.volume = volume;

        m_AudioSource.Play();
    }
}
