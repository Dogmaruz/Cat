using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUD : MonoBehaviour
{
    [SerializeField] public GameObject m_startButton;

    [SerializeField] public GameObject m_restartButton;

    [SerializeField] public GameObject m_menuButton;

    private void Start()
    {
        m_startButton.SetActive(true);
        m_restartButton.SetActive(false);
        m_menuButton.SetActive(false);
    }
}
