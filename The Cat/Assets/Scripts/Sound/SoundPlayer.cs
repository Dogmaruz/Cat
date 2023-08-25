using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class SoundPlayer : MonoBehaviour
{
    [SerializeField] private GameSounds m_Sounds;

    [SerializeField] private AudioSource m_AudioSource;
    public AudioSource AudioSource { get => m_AudioSource; set => m_AudioSource = value; }

    public void Play(Sound sound, float pitch)
    {
        m_AudioSource.pitch = pitch;

        m_AudioSource.PlayOneShot(m_Sounds[sound]);
    }
}
