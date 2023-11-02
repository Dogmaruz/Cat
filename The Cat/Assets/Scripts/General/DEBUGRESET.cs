using UnityEngine;
using Zenject;

public class DEBUGRESET : MonoBehaviour
{
    private CoinManager _coinManager;
    private SkinManager _skinManager;
    private DailyRewardManager _dailyRewardManager;
    private float currentTimeScale;

    [Inject]
    public void Construct(CoinManager coinManager, SkinManager skinManager, DailyRewardManager dailyRewardManager)
    {
        _coinManager = coinManager;
        _skinManager = skinManager;
        _dailyRewardManager = dailyRewardManager;
    }

    private void Start()
    {
        currentTimeScale = Time.timeScale;
    }


    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.M)) _coinManager.ResetValue();
        if (Input.GetKeyUp(KeyCode.C)) _coinManager.AddCoins(99);

        if (Input.GetKeyUp(KeyCode.S)) _skinManager.ResetData();
        if (Input.GetKeyUp(KeyCode.D)) _dailyRewardManager.DeleteAllData();

        if (Input.GetKeyUp(KeyCode.T))
        {
            if (Time.timeScale > 0) Time.timeScale = 0;
            else if (Time.timeScale == 0) Time.timeScale = currentTimeScale;
        }
    }
}
