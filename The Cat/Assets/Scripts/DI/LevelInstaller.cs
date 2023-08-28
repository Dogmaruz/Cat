using UnityEngine;
using Zenject;

public class LevelInstaller : MonoInstaller
{
    [SerializeField] private LevelSecuenceController LevelSecuenceController;

    public override void InstallBindings()
    {
        BindLevelController();
    }

    private void BindLevelController()
    {
        Container.
            Bind<LevelSecuenceController>().FromInstance(LevelSecuenceController).AsSingle();
    }
}
