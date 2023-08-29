using UnityEngine;
using Zenject;

public class TileInstaller : MonoInstaller
{
    [SerializeField] private TileController m_tileController;

    public override void InstallBindings()
    {
        BindTileController();
    }

    private void BindTileController()
    {
        Container.
            Bind<TileController>().FromInstance(m_tileController).AsSingle();
    }
}
