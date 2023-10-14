using UnityEngine;
using UnityEngine.UI;

public class UI_CharactersPanel : MonoBehaviour
{
    [SerializeField] private Button m_openPanelButton;
    [SerializeField] private Button m_closePanelButton;

    [SerializeField] private GameObject m_mainMenuCanvas;
    
    private void Awake()
    {
        m_openPanelButton.onClick.AddListener(OpenCharactersPanel);

        m_closePanelButton.onClick.AddListener(CloseCharactersPanel);
    }

    private void Start()
    {
        gameObject.SetActive(false);
    }

    private void OpenCharactersPanel()
    {
        m_mainMenuCanvas.SetActive(false);
        gameObject.SetActive(true);
    }

    public void CloseCharactersPanel()
    {
        m_mainMenuCanvas.SetActive(true);
        gameObject.SetActive(false);
    }
}