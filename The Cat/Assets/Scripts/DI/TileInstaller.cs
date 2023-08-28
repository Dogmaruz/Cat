using UnityEngine;
using Zenject;

public class TileInstaller : MonoInstaller
{
    [SerializeField] private TileController m_platfornController;

    public override void InstallBindings()
    {
        BindTileController();
    }

    private void BindTileController()
    {
        Container.
            Bind<TileController>().FromInstance(m_platfornController).AsSingle();
    }
}
