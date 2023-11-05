using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class CoinsPack : MonoBehaviour
{
    public enum PackType
    {
        Payment,
        Periodic,
        Video
    }

    [SerializeField] private PackType m_type;
    public PackType Type => m_type;

    [SerializeField] private int m_coinsValue;

    [SerializeField] private TMP_Text m_coinsCountText;

    private Button _claimButton;
    private TMP_Text _claimButtonText;

    private CoinManager _coinManager;
    private CoinsShop _coinsShop;

    [Inject]
    public void Construct(CoinManager coinManager, CoinsShop coinsShop)
    {
        _coinManager = coinManager;
        _coinsShop = coinsShop;
    }

    private void Start()
    {
        _claimButton = GetComponentInChildren<Button>();
        _claimButtonText = _claimButton.GetComponentInChildren<TMP_Text>();

        _claimButton.onClick.AddListener(OnButtonClick);

        if (m_type == PackType.Video)
        {
            ModifyButton(false, "Смотреть видео"); // TODO change to true
        }
        else if (m_type == PackType.Payment)
        {
            ModifyButton(false, "Купить"); // TODO change to true
        }
    }

    // TODO Add logic to purchase and advertise

    private void OnButtonClick()
    {
        // TODO add purchase and advertise
        switch (m_type)
        {
            case PackType.Payment:
                break;

            case PackType.Periodic:
                if (_coinsShop.TryClaimReward() == true)
                {
                    _coinManager.AddCoins(m_coinsValue);
                }
                break;

            case PackType.Video:
                break;

        }
    }

    public void ModifyButton(bool interactible, string text)
    {
        _claimButton.interactable = interactible;

        _claimButtonText.text = text;
    }

    public void ModifyButton(bool interactible, string text, int fontSize, bool autoSizing)
    {
        _claimButton.interactable = interactible;

        _claimButtonText.text = text;

        _claimButtonText.fontSize = fontSize;

        _claimButtonText.enableAutoSizing = autoSizing;
    }
}
