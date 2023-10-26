using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyReward : MonoBehaviour
{
    [SerializeField] private int m_value;

    [SerializeField] private TMP_Text m_valueText;

    [SerializeField] private Image m_background;
    [SerializeField] private Color m_defaultColor;
    [SerializeField] private Color m_currentColor;

    private bool _isCurrent;

    private void Start()
    {
        m_valueText.text = m_value.ToString();

        m_background.color = _isCurrent ? m_currentColor : m_defaultColor;
    }
}