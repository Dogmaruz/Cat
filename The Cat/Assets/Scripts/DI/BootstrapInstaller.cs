using UnityEngine;
using Zenject;

public class BootstrapInstaller : MonoInstaller
{
    [SerializeField] private CoinManager m_coinManager;

    [SerializeField] private SkinManager m_skinManager;

    [SerializeField] private BackgroundSoundPlayer m_backgroundSoundPlayer;

    [SerializeField] private SoundPlayer m_soundPlayer;

    public override void InstallBindings()
    {
        Container.
            Bind<CoinManager>()
            .FromComponentInNewPrefab(m_coinManager)
            .AsSingle();

        Container.
            Bind<SkinManager>()
            .FromComponentInNewPrefab(m_skinManager)
            .AsSingle();

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