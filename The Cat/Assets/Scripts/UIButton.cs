using UnityEngine;
using Zenject;

public class UIButton : MonoBehaviour
{
    [SerializeField] BackgroundSceneClip m_backgroundSceneClip;

    [SerializeField] GameObject m_button;

    MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    public void StartGame()
    {
        m_backgroundSceneClip.PlayBackground();

        _cat.EnabledCollider();

        m_button.SetActive(false);
    }
}
