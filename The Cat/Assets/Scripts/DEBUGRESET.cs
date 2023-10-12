using UnityEngine;
using Zenject;

public class DEBUGRESET : MonoBehaviour
{
    private CoinManager _coinManager;

    [Inject]
    public void Construct(CoinManager coinManager)
    {
        _coinManager = coinManager;
    }

    private void Update()
    {
        if(Input.GetKeyUp(KeyCode.R)) { _coinManager.ResetValue(); }
    }
}
