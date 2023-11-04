using System;
using System.Collections;
using UnityEngine;

public class CoinsShop : MonoBehaviour
{
    public Action PeriodicRewardStateUpdated;

    [SerializeField] private float _claimCooldownInHours = 4f;
    public float ClaimCooldown => _claimCooldownInHours;

    private string _lastClaimTimeFilename = "coinsShop.dat";

    private DateTime _lastClaimTime = DateTime.MinValue;
    public DateTime LastClaimTime => _lastClaimTime;

    private bool _canClaimReward;
    public bool CanClaimReward => _canClaimReward;

    private float _updatingVelocity = 1f;

    private void Awake()
    {
        LoadLastClaimTimeData();
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

        PeriodicRewardStateUpdated?.Invoke();
    }

    public bool TryClaimReward()
    {
        if (!_canClaimReward) return false;

        _lastClaimTime = DateTime.UtcNow;

        SaveLastClaimTimeData();

        return true;
    }

    #region DataSaving
    private void SaveLastClaimTimeData()
    {
        string data = _lastClaimTime.ToString();

        Saver<string>.Save(_lastClaimTimeFilename, data);
    }

    private void LoadLastClaimTimeData()
    {
        string data = null;

        Saver<string>.TryLoad(_lastClaimTimeFilename, ref data);

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
        FileHandler.Reset(_lastClaimTimeFilename);

        _lastClaimTime = DateTime.MinValue;
    }
    #endregion
}
