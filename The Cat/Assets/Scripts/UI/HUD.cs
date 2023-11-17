using UnityEngine;
using Zenject;

public class HUD : MonoBehaviour
{
    [SerializeField] public GameObject m_startButton;

    [SerializeField] public GameObject m_restartButton;

    [SerializeField] public GameObject m_menuButton;

    [SerializeField] private GameObject[] m_switchablePanelOpeningButtons;

    private LevelSecuenceController _levelSecuenceController;

    [Inject]
    public void Construct(LevelSecuenceController levelSecuenceController)
    {
        _levelSecuenceController = levelSecuenceController;
    }

    private void Start()
    {
        _levelSecuenceController.PlayModeChange += ChangeButtonsAvailability;

        m_startButton.SetActive(true);
        m_restartButton.SetActive(false);
        m_menuButton.SetActive(false);
    }

    private void OnDestroy()
    {
        _levelSecuenceController.PlayModeChange -= ChangeButtonsAvailability;
    }

    private void ChangeButtonsAvailability(bool state)
    {
        foreach (var button in m_switchablePanelOpeningButtons)
        {
            button.SetActive(!state);
        }
    }
}
