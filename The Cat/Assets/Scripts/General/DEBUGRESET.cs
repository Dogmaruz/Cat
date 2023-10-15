using UnityEngine;
using Zenject;

public class DEBUGRESET : MonoBehaviour
{
    private CoinManager _coinManager;
    private float currentTimeScale;

    [Inject]
    public void Construct(CoinManager coinManager)
    {
        _coinManager = coinManager;
    }

    private void Start()
    {
        currentTimeScale = Time.timeScale;
    }


    private void Update()
    {
        if(Input.GetKeyUp(KeyCode.R)) _coinManager.ResetValue();

        if(Input.GetKeyUp(KeyCode.P)) 
        {
            if (Time.timeScale > 0) Time.timeScale = 0; 
            else if (Time.timeScale == 0) Time.timeScale = currentTimeScale; 
        }
    }
}
