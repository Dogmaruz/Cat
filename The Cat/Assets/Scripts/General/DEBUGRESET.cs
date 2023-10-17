using UnityEngine;
using Zenject;

public class DEBUGRESET : MonoBehaviour
{
    private CoinManager _coinManager;
    private SkinManager _skinManager;
    private float currentTimeScale;

    [Inject]
    public void Construct(CoinManager coinManager, SkinManager skinManager)
    {
        _coinManager = coinManager;
        _skinManager = skinManager;
    }

    private void Start()
    {
        currentTimeScale = Time.timeScale;
    }


    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.M)) _coinManager.ResetValue();
        if (Input.GetKeyUp(KeyCode.C)) _coinManager.AddCoins(99);

        if (Input.GetKeyUp(KeyCode.T))
        {
            if (Time.timeScale > 0) Time.timeScale = 0;
            else if (Time.timeScale == 0) Time.timeScale = currentTimeScale;
        }

        if (Input.GetKeyUp(KeyCode.S)) _skinManager.ResetData();
    }
}
