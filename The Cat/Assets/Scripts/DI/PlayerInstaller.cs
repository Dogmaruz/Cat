using UnityEngine;
using Zenject;

public class PlayerInstaller : MonoInstaller
{
    [SerializeField] private MovementController m_cat;

    public override void InstallBindings()
    {
        BindPlayer();
    }

    private void BindPlayer()
    {
        Container.
            Bind<MovementController>().FromInstance(m_cat).AsSingle();
    }
}
