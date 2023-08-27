using UnityEngine;
using Zenject;

public class SoundHook : MonoBehaviour
{
    public Sound m_sound;

    private SoundPlayer _soundPlayer;

    [Inject]
    public void Construct(SoundPlayer soundPlayer)
    {
        _soundPlayer = soundPlayer;
    }
    public void Play()
    {
        _soundPlayer.Play(m_sound, 1);
    }
}
