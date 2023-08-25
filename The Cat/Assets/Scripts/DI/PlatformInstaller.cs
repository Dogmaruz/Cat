using UnityEngine;
using Zenject;

public class PlatformInstaller : MonoInstaller
{
    [SerializeField] private TileController m_platfornController;

    public override void InstallBindings()
    {
        BindPlatform();
    }

    private void BindPlatform()
    {
        Container.
            Bind<TileController>().FromInstance(m_platfornController).AsSingle();
    }
}
