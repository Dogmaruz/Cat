using UnityEngine;
using Zenject;

public class Coin : Emerging
{
    private SoundPlayer _soundPlayer;

    private CoinManager _coinManager;

    [Inject]
    public void Construct(SoundPlayer soundPlayer, CoinManager coinManager)
    {
        _soundPlayer = soundPlayer;

        _coinManager = coinManager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.GetComponent<MovementController>() == null) return;

        _soundPlayer.Play(Sound.Coin, 1f);

        _coinManager.AddCoins(1);

        GetComponentInChildren<MeshRenderer>().enabled = false;
    }
}
