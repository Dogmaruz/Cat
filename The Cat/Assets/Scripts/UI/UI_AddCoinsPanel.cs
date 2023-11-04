using System;
using Zenject;

public class UI_AddCoinsPanel : UI_Panel
{
    private CoinsPack[] _coinsPacks;

    private CoinsShop _coinsShop;

    [Inject]
    public void Construct(CoinsShop coinsShop)
    {
        _coinsShop = coinsShop;
    }

    protected override void Start()
    {
        base.Start();

        _coinsPacks = GetComponentsInChildren<CoinsPack>();

        _coinsShop.PeriodicRewardStateUpdated += UpdateUI;

        UpdateUI();
    }

    private void OnDestroy()
    {
        _coinsShop.PeriodicRewardStateUpdated -= UpdateUI;
    }

    private void UpdateUI()
    {
        foreach(var item in _smthNew)
        {
            item.SetAvailability(_coinsShop.CanClaimReward);
        }

        var nextClaimTime = _coinsShop.LastClaimTime.AddHours(_coinsShop.ClaimCooldown);
        var currentClaimCooldown = nextClaimTime - DateTime.UtcNow;

        string cd = $"{currentClaimCooldown.Hours:D2}:{currentClaimCooldown.Minutes:D2}:{currentClaimCooldown.Seconds:D2}";

        foreach (var pack in _coinsPacks)
        {
            if (pack.Type == CoinsPack.PackType.Periodic)
            {
                bool canClaimReward = _coinsShop.CanClaimReward;

                string text = canClaimReward ? "Забрать" : cd;

                int size = canClaimReward ? 0 : 34;

                pack.ModifyButton(canClaimReward, text, size, canClaimReward);
            }
        }
    }
}
