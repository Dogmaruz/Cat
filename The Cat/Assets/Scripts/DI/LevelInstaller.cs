using UnityEngine;
using Zenject;

public class LevelInstaller : MonoInstaller
{
    [SerializeField] private LevelSecuenceController m_levelSecuenceController;
    [SerializeField] private LevelScore m_levelScore;

    public override void InstallBindings()
    {
        BindLevelController();
    }

    private void BindLevelController()
    {
        Container.
            Bind<LevelSecuenceController>().FromInstance(m_levelSecuenceController).AsSingle();

        Container.
            Bind<LevelScore>().FromInstance(m_levelScore).AsSingle();
    }
}
