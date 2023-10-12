using UnityEngine;
using Zenject;

public class Coin : MonoBehaviour
{
    private SoundPlayer _soundPlayer;

    [Inject]
    public void Construct(SoundPlayer soundPlayer)
    {
        _soundPlayer = soundPlayer;
    }

    private void OnTriggerEnter(Collider other)
    {
        _soundPlayer.Play(Sound.Coin, 1f);

        Destroy(gameObject);
    }
}
