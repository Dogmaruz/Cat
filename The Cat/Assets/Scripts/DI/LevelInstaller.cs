using UnityEngine;
using Zenject;

public class LevelInstaller : MonoInstaller
{
    [SerializeField] private LevelSecuenceController m_levelSecuenceController;

    public override void InstallBindings()
    {
        BindLevelController();
    }

    private void BindLevelController()
    {
        Container.
            Bind<LevelSecuenceController>().FromInstance(m_levelSecuenceController).AsSingle();
    }
}
