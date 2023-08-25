using UnityEngine;
using Zenject;

public class BackgroundSceneClip : MonoBehaviour
{
    [SerializeField] private AudioClip m_backgroundClip;

    [SerializeField] private float m_volume = 1;

    private BackgroundSoundPlayer _backgroundSoundPlayer;

    [Inject]
    public void Construct(BackgroundSoundPlayer backgroundSoundPlayer)
    {
        _backgroundSoundPlayer = backgroundSoundPlayer;
    }

    private void Awake()
    {

    }

    public void PlayBackground()
    {
        if (m_backgroundClip != null)
        {
            _backgroundSoundPlayer.Play(m_backgroundClip, m_volume);
        }
    }
}
