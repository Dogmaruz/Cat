using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class DailyReward : MonoBehaviour
{
    [SerializeField] private int m_dayIndex;
    public int DayIndex => m_dayIndex;

    [SerializeField] private int m_value;

    [SerializeField] private TMP_Text m_dayText;
    [SerializeField] private TMP_Text m_valueText;

    [SerializeField] public Image BackgroundImage;
    

    private DailyRewardManager _dailyRewardManager;
    private CoinManager _coinManager;

    [Inject]
    public void Construct(DailyRewardManager dailyRewardManager, CoinManager coinManager)
    {
        _dailyRewardManager = dailyRewardManager;
        _coinManager = coinManager;
    }

    private void Start()
    {
        _dailyRewardManager.RewardClaimed += ClaimReward;

        m_dayText.text = "Day " + (m_dayIndex + 1);
        m_valueText.text = m_value.ToString();
    }

    private void OnDestroy()
    {
        _dailyRewardManager.RewardClaimed -= ClaimReward;
    }

    public void ClaimReward(int currentStreak)
    {
        if(currentStreak == m_dayIndex)
        {
            _coinManager.AddCoins(m_value);
        }
    }
}