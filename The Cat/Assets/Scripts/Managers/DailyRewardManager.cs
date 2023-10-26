using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DailyRewardManager : MonoBehaviour
{ 







    /*
    private bool _isYesterdayRewardReached;
    private bool _isTodayRewardReached;
    public bool IsRewardAvailable => !_isTodayRewardReached;

    private DateTime _dateTime;

    private DailyReward[] _allRewards;

    private HUD _hud;

    private void Start()
    {
        _hud = FindObjectOfType<HUD>();

        _allRewards = _hud.GetComponentsInChildren<DailyReward>();

        _dateTime = DateTime.Now;

        int currentDay = _dateTime.Day;

        if (_isTodayRewardReached == false ) 
        {
            
        }
    }

    /*
    private void ResetRewards()
    {
        foreach (var reward in _allRewards)
        {
            reward.IsRewardAvailable = false;
        }

        _allRewards[0].IsRewardAvailable = true;
    }
    */
}
