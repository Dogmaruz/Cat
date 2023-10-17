using UnityEngine;
using Zenject;

public class DEBUGRESET : MonoBehaviour
{
    private CoinManager _coinManager;

    private MovementController _movementController;

    [Inject]
    public void Construct(CoinManager coinManager, LevelSecuenceController levelSecuenceController)
    {
        _coinManager = coinManager;
    }

    private void Awake()
    {
        _movementController = GetComponent<MovementController>();
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.R))
        {
            _coinManager.ResetValue();

            _movementController._isLose = true;
        }
    }
}
