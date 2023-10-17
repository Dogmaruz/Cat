using UnityEngine;
using DG.Tweening;
using Zenject;
using System.Collections.Generic;
using System.Linq;

public class BonusCoinsAnimation : MonoBehaviour
{
    [SerializeField] private float m_activationDistance;

    [SerializeField] private float m_animationSpeed;

    private Coin[] _coins;

    private float _alpha;

    private MovementController _movementController;

    private List<Coin> _coinsList;

    [Inject]
    public void Construct(MovementController movementController)
    {
        _movementController = movementController;
    }

    private void Start()
    {
        _coins = GetComponentsInChildren<Coin>();

        _coinsList = new List<Coin>();

        foreach (Coin coin in _coins)
        {
            _coinsList.Add(coin);

            SetCoinVisibility(coin, false);
        }
    }

    private void Update()
    {
        TryShowCoinByDistance();
    }

    private void TryShowCoinByDistance()
    {
        foreach (Coin coin in _coinsList.ToList())
        {
            var dist = Vector3.Distance(_movementController.PlayerTransform.transform.position, coin.transform.position);

            if (dist < m_activationDistance)
            {
                SetCoinVisibility(coin, true);

                _coinsList.Remove(coin);
            }
        }
    }

    private void SetCoinVisibility(Coin coin, bool state)
    {
        _alpha = state ? 1 : 0;

        Material mat = coin.GetComponentInChildren<MeshRenderer>().material;

        mat.DOColor(new Color(mat.color.r, mat.color.g, mat.color.b, _alpha), m_animationSpeed);
    }
}
