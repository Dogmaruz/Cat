using System;
using System.Collections;
using UnityEngine;

public class DailyRewardManager : MonoBehaviour
{
    public Action RewardStateUpdated;
    public Action<int> RewardClaimed;

    [SerializeField] private int m_dailyRewardsCount = 5;

    [SerializeField] private float _claimCooldownInHours = 24f;
    public float ClaimCooldown => _claimCooldownInHours;

    private string _dailyRewardDataFilename = "dailyReward.dat";
    private string _currentStreakFilename = "currentStreak.dat";

    private int _currentStreak = 0;
    public int CurrentStreak => _currentStreak;

    private DateTime _lastClaimTime = DateTime.MinValue;
    public DateTime LastClaimTime => _lastClaimTime;

    private bool _canClaimReward;
    public bool CanClaimReward => _canClaimReward;

    

    private float _updatingVelocity = 1f;

    private void Awake()
    {
        LoadAllData();
    }

    private void Start()
    {
        StartCoroutine(RewardStateUpdater());
    }

    private IEnumerator RewardStateUpdater()
    {
        while (true)
        {
            UpdateRewardState();
            yield return new WaitForSeconds(_updatingVelocity);
        }
    }

    private void UpdateRewardState()
    {
        _canClaimReward = true;

        if (_lastClaimTime != DateTime.MinValue)
        {
            var timeSpan = DateTime.UtcNow - _lastClaimTime;

            if (timeSpan.TotalHours < _claimCooldownInHours)
            {
                _canClaimReward = false;
            }
        }

        _updatingVelocity = _canClaimReward ? 0.2f : 1f;


        RewardStateUpdated?.Invoke();
    }

    public void ClaimReward()
    {
        if (!_canClaimReward) return;

        RewardClaimed?.Invoke(_currentStreak);

        _lastClaimTime = DateTime.UtcNow;
        _currentStreak = (_currentStreak + 1) % m_dailyRewardsCount;

        SaveAllData();

        RewardStateUpdated?.Invoke();

        _canClaimReward = false;
    }

    #region DataSaving
    private void SaveLastClaimTimeData()
    {
        string data = _lastClaimTime.ToString();

        Saver<string>.Save(_dailyRewardDataFilename, data);
    }

    private void LoadLastClaimTimeData()
    {
        string data = null;

        Saver<string>.TryLoad(_dailyRewardDataFilename, ref data);

        if (data == null)
        {
            _lastClaimTime = DateTime.MinValue;
        }
        else
        {
            _lastClaimTime = DateTime.Parse(data);
        }
    }

    private void DeleteLastClaimTimeData()
    {
        FileHandler.Reset(_dailyRewardDataFilename);

        _lastClaimTime = DateTime.MinValue;
    }

    private void SaveCurrentStreakData()
    {
        Saver<int>.Save(_currentStreakFilename, _currentStreak);
    }

    private void LoadCurrentStreakData()
    {
        Saver<int>.TryLoad(_currentStreakFilename, ref _currentStreak);
    }

    private void DeleteCurrentStreakData()
    {
        FileHandler.Reset(_currentStreakFilename);

        _currentStreak = 0;
    }

    private void SaveAllData()
    {
        SaveLastClaimTimeData();
        SaveCurrentStreakData();
    }

    private void LoadAllData()
    {
        LoadLastClaimTimeData();
        LoadCurrentStreakData();
    }

    public void DeleteAllData()
    {
        DeleteLastClaimTimeData();
        DeleteCurrentStreakData();
    }
    #endregion
}
