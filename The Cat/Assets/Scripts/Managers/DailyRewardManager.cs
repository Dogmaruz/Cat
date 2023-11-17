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

    private string _lastClaimTimeFilename = "lastClaimTime.dat";
    private string _previousClaimTimeFilename = "previousClaimTime.dat";
    private string _currentStreakFilename = "currentStreak.dat";

    private int _currentStreak = 0;
    public int CurrentStreak => _currentStreak;

    private DateTime _lastClaimTime = DateTime.UtcNow.AddDays(-1f);

    private DateTime _previousClaimTime = DateTime.MinValue;

    private bool _canClaimReward;
    public bool CanClaimReward => _canClaimReward;

    private float _updatingVelocity = 1f;

    private void Awake()
    {
        LoadData();
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

        if (_lastClaimTime.Date == DateTime.UtcNow.Date)
        {
            _canClaimReward = false;
        }

        _updatingVelocity = _canClaimReward ? 0.2f : 1f;


        RewardStateUpdated?.Invoke();
    }

    public void ClaimReward()
    {
        if (!_canClaimReward) return;

        RewardClaimed?.Invoke(_currentStreak);

        _previousClaimTime = _lastClaimTime;

        _lastClaimTime = DateTime.UtcNow;

        var timeSpan = _lastClaimTime.Date - _previousClaimTime.Date;

        if (timeSpan.Days != 1)
        {
            _currentStreak = 0;
        }
        else
        {
            _currentStreak = (_currentStreak + 1) % m_dailyRewardsCount;
        }

        SaveData();

        RewardStateUpdated?.Invoke();

        _canClaimReward = false;
    }

    #region DataSaving
    private void SaveData()
    {
        string data;

        data = _lastClaimTime.ToString();
        Saver<string>.Save(_lastClaimTimeFilename, data);

        data = _previousClaimTime.ToString();
        Saver<string>.Save(_previousClaimTimeFilename, data);

        Saver<int>.Save(_currentStreakFilename, _currentStreak);
    }

    private void LoadData()
    {
        string data; 

        data = null;
        Saver<string>.TryLoad(_lastClaimTimeFilename, ref data);
        if (string.IsNullOrEmpty(data))
        {
            _lastClaimTime = DateTime.UtcNow.AddDays(-1f);
        }
        else
        {
            _lastClaimTime = DateTime.Parse(data);
        }

        data = null;
        Saver<string>.TryLoad(_previousClaimTimeFilename, ref data);
        if (string.IsNullOrEmpty(data))
        {
            _previousClaimTime = DateTime.MinValue;
        }
        else
        {
            _previousClaimTime = DateTime.Parse(data);
        }

        Saver<int>.TryLoad(_currentStreakFilename, ref _currentStreak);
    }

    public void DeleteData()
    {
        FileHandler.Reset(_lastClaimTimeFilename);
        _lastClaimTime = DateTime.UtcNow.AddDays(-1f);

        FileHandler.Reset(_previousClaimTimeFilename);
        _lastClaimTime = DateTime.MinValue;

        FileHandler.Reset(_currentStreakFilename);
        _currentStreak = 0;
    }
    #endregion
}
