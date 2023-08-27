using UnityEngine;
using Zenject;

public class UIButton : MonoBehaviour
{
    [SerializeField] BackgroundSceneClip m_backgroundSceneClip;

    [SerializeField] GameObject m_button;

    private MovementController _cat;

    private void Awake()
    {
        Cursor.visible = true;

        Cursor.lockState = CursorLockMode.None;
    }

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    public void StartGame()
    {
        m_backgroundSceneClip.PlayBackground();

        _cat.ActivateMovement();

        m_button.SetActive(false);

        Cursor.visible = false;

        Cursor.lockState = CursorLockMode.Locked;
    }
}
