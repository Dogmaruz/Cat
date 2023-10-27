using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class UI_DailyRewardsPanel : UI_Panel
{
    public Action PanelOpened;

    [SerializeField] private TMP_Text m_status;
    [SerializeField] private Button m_claimButton;

    [SerializeField] private Color m_defaultColor;
    [SerializeField] private Color m_currentColor;

    private DailyReward[] rewards;

    private DailyRewardManager _dailyRewardManager;

    [Inject]
    public void Construct(DailyRewardManager dailyRewardManager)
    {
        _dailyRewardManager = dailyRewardManager;
    }

    protected override void Start()
    {
        base.Start();

        _dailyRewardManager.RewardStateUpdated += UpdateRewardsUI;

        rewards = GetComponentsInChildren<DailyReward>();

        m_claimButton.onClick.AddListener(_dailyRewardManager.ClaimReward);

        UpdateRewardsUI();
    }

    private void OnDestroy()
    {
        _dailyRewardManager.RewardStateUpdated -= UpdateRewardsUI;
    }

    protected override void OpenPanel()
    {
        base.OpenPanel();

        PanelOpened?.Invoke();
    }

    private void UpdateRewardsUI()
    {
        foreach (var reward in rewards)
        {
            reward.BackgroundImage.color = reward.DayIndex == _dailyRewardManager.CurrentStreak ? m_currentColor : m_defaultColor;
        }

        m_claimButton.interactable = _dailyRewardManager.CanClaimReward;

        if (_dailyRewardManager.CanClaimReward)
        {
            m_status.text = "Claim your reward!";
        }
        else
        {
            var nextClaimTime = _dailyRewardManager.LastClaimTimeValue.AddHours(_dailyRewardManager.ClaimDeadline);
            var currentClaimCooldown = nextClaimTime - DateTime.UtcNow;

            string cd = $"{currentClaimCooldown.Hours:D2}:{currentClaimCooldown.Minutes:D2}:{currentClaimCooldown.Seconds:D2}";

            m_status.text = $"Come back in {cd} for your next reward";
        }
    }
}