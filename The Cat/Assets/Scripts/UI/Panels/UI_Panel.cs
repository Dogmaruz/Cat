using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Panel : MonoBehaviour
{
    public Action PanelOpened;

    [SerializeField] protected Button[] m_openPanelButton;
    [SerializeField] protected Button m_closePanelButton;

    [SerializeField] protected GameObject m_mainMenuCanvas;

    protected List<SmthNewIcon> _smthNew = new List<SmthNewIcon>();

    protected virtual void Awake()
    {
        foreach (var button in m_openPanelButton)
        {
            button.onClick.AddListener(OpenPanel);
        }

        m_closePanelButton.onClick.AddListener(ClosePanel);

        foreach (var button in m_openPanelButton)
        {
            var item = button.gameObject.GetComponentInChildren<SmthNewIcon>();

            if (item != null)
            {
                _smthNew.Add(item);
                item.SetAvailability(false);
            }
        }
    }

    protected virtual void Start()
    {
        gameObject.SetActive(false);
    }

    protected virtual void OpenPanel()
    {
        m_mainMenuCanvas.SetActive(false);

        gameObject.SetActive(true);

        PanelOpened?.Invoke();
    }

    public virtual void ClosePanel()
    {
        gameObject.SetActive(false);

        m_mainMenuCanvas.SetActive(true);
    }
}
