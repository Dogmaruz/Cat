using TMPro;
using UnityEngine;
using Zenject;

public class UI_CoinInfo : MonoBehaviour
{
    [SerializeField] private TMP_Text m_coinCountText;

    private CoinManager _coinManager;

    [Inject]
    public void Construct(CoinManager coinManager)
    {
        _coinManager = coinManager;
    }

    private void Start()
    {
        _coinManager.CoinsCountChanged += OnCoinsCountChanged;

        m_coinCountText.text = _coinManager.CoinsCount.ToString();
    }

    private void OnDestroy()
    {
        _coinManager.CoinsCountChanged -= OnCoinsCountChanged;
    }

    private void OnCoinsCountChanged(int count)
    {
        m_coinCountText.text = _coinManager.CoinsCount.ToString();
    }
}