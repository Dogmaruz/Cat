using System;
using System.Collections;
using UnityEngine;

public class DailyRewardManager : MonoBehaviour
{
    public Action RewardStateUpdated;
    public Action<int> RewardClaimed;

    [SerializeField] private int m_maxStreakCount = 5;

    private int _currentStreak
    {
        get => PlayerPrefs.GetInt("currentStreak", 0);
        set => PlayerPrefs.SetInt("currentStreak", value);
    }
    public int CurrentStreak => _currentStreak;

    private DateTime? _lastClaimTime //он сказал, что в префс нельзя сохранять DateTime (но в JSON можно)
    {
        get
        {
            string data = PlayerPrefs.GetString("lastClaimedTime", null);

            if (!string.IsNullOrEmpty(data))
            {
                return DateTime.Parse(data);
            }

            return null;
        }

        set
        {
            if (value != null)
            {
                PlayerPrefs.SetString("lastClaimedTime", value.ToString());
            }
            else
            {
                PlayerPrefs.DeleteKey("lastClaimedTime");
            }
        }
    }
    public DateTime LastClaimTimeValue => _lastClaimTime.Value;

    private bool _canClaimReward;
    public bool CanClaimReward => _canClaimReward;

    private float _claimCooldown = 24f;
    public float ClaimCooldown => _claimCooldown;

    private float _claimDeadline = 48f;
    public float ClaimDeadline => _claimDeadline;

    private void Start()
    {
        StartCoroutine(RewardStateUpdater());
    }

    private IEnumerator RewardStateUpdater()
    {
        while (true)
        {
            UpdateRewardState();
            yield return new WaitForSeconds(1);
        }
    }

    private void UpdateRewardState()
    {
        _canClaimReward = true;

        if (_lastClaimTime.HasValue)
        {
            var timeSpan = DateTime.UtcNow - _lastClaimTime.Value;

            if (timeSpan.TotalHours > _claimDeadline)
            {
                _lastClaimTime = null;
                _currentStreak = 0;
            }
            else if (timeSpan.TotalHours < _claimCooldown)
            {
                _canClaimReward = false;
            }
        }

        RewardStateUpdated?.Invoke();
    }

    public void ClaimReward()
    {
        if (!_canClaimReward) return;

        RewardClaimed?.Invoke(_currentStreak);

        _lastClaimTime = DateTime.UtcNow;
        _currentStreak = (_currentStreak + 1) % m_maxStreakCount;

        RewardStateUpdated?.Invoke();
    }
}
