using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class UI_CharactersPanel : MonoBehaviour
{
    [SerializeField] private Button m_openPanelButton;
    [SerializeField] private Button m_closePanelButton;

    [SerializeField] private GameObject m_mainMenuCanvas;

    private MovementController _movementController; 
    
    [Inject]
    public void Construct(MovementController movementController)
    {
        _movementController = movementController;
    }

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

    public void ChangePlayerColor(Color color)
    {
        _movementController.GetComponentInChildren<MeshRenderer>().material.color = color;
    }
}
