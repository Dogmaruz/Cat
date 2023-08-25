using UnityEngine;
using Zenject;

public class BootstrapInstaller : MonoInstaller
{
    [SerializeField] private BackgroundSoundPlayer m_backgroundSoundPlayer;

    [SerializeField] private SoundPlayer m_soundPlayer;

    public override void InstallBindings()
    {
        Container.
            Bind<BackgroundSoundPlayer>()
            .FromComponentInNewPrefab(m_backgroundSoundPlayer)
            .AsSingle();

        Container.
            Bind<SoundPlayer>()
            .FromComponentInNewPrefab(m_soundPlayer)
            .AsSingle();
    }
}