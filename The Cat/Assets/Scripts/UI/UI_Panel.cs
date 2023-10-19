using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI_Panel : MonoBehaviour
{
    [SerializeField] protected Button m_openPanelButton;
    [SerializeField] protected Button m_closePanelButton;

    [SerializeField] protected GameObject m_mainMenuCanvas;

    protected virtual void Awake()
    {
        m_openPanelButton.onClick.AddListener(OpenPanel);

        m_closePanelButton.onClick.AddListener(ClosePanel);
    }

    protected virtual void Start()
    {
        gameObject.SetActive(false);
    }

    protected virtual void OpenPanel()
    {
        m_mainMenuCanvas.SetActive(false);

        gameObject.SetActive(true);
    }

    public virtual void ClosePanel()
    {
        gameObject.SetActive(false);

        m_mainMenuCanvas.SetActive(true);
    }
}
