using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class VisualModel : MonoBehaviour
{
    [SerializeField] private int m_index;
    [SerializeField] private int m_skinCost;

    [SerializeField] private Image m_image;
    [SerializeField] private TMP_Text m_skinCostText;

    [SerializeField] private GameObject m_choose;
    [SerializeField] private GameObject m_buy;
    [SerializeField] private GameObject m_ADOffer;

    [SerializeField] private Sprite m_ifCurrentModelSprite;

    private Sprite _defaultBtnSprite;
    private Color _defaultBtnColor;

    //[SerializeField] private GameObject m_Adv; // TODO stub for Adv

    private Button _buyButton;

    private UI_CharactersPanel _charactersPanel;

    private SkinManager _skinManager;

    private CoinManager _coinManager;

    private bool _isCurrent = false;
    private bool _canChoose = false;
    private bool _canBuy = false;

    [Inject]
    public void Construct(SkinManager skinManager, CoinManager coinManager)
    {
        _skinManager = skinManager;

        _coinManager = coinManager;
    }

    private void Start()
    {
        _charactersPanel = transform.root.GetComponentInChildren<UI_CharactersPanel>();
        _charactersPanel.PanelOpened += UpdatePanel;

        _buyButton = GetComponentInChildren<Button>();
        _defaultBtnSprite = _buyButton.GetComponent<Image>().sprite;
        _defaultBtnColor = _buyButton.GetComponent<Image>().color;
        _buyButton.onClick.AddListener(Click);

        // TODO Add images;
        //m_image.sprite = m_visualModel.Sprite;
        //m_image.color = Color.white;

        m_skinCostText.text = m_skinCost.ToString();

        SetAvailabilityToAllButtons(false);
    }

    private void OnDestroy()
    {
        if (_charactersPanel)
        {
            _charactersPanel.PanelOpened -= UpdatePanel;
        }
    }

    private void Click()
    {
        if (_isCurrent)
        {
            _charactersPanel.ClosePanel();
        }
        else if (_canChoose)
        {
            _skinManager.ChangePlayerVisualModel(m_index);

            UpdatePanel();

            _charactersPanel.ClosePanel();
        }
        else if (_canBuy)
        {
            BuySkin();

            UpdatePanel();
        }/*
        else if (m_Adv == null)
        {
            Instantiate(_charactersPanel.NotEnoughCoinsEffect.gameObject);
        }*/
        else
        {
            // TODO Watch Adv;
        }
    }

    private void UpdatePanel()
    {
        bool isCoinsEnough = _coinManager.CoinsCount >= m_skinCost;

        foreach (var model in _skinManager.SkinsData)
        {
            if (model.SkinIndex == m_index)
            {
                if (m_index == _skinManager.CurrentSkinIndex)
                {
                    SetAvailabilityToAllButtons(false);
                    _isCurrent = true;
                }
                else if (model.IsPurchased == true)
                {
                    SetAvailabilityToAllButtons(false);
                    m_choose.SetActive(true);

                    _canChoose = true;
                    _canBuy = false;
                    _isCurrent = false;
                }
                else if (model.IsPurchased == false && isCoinsEnough)
                {
                    SetAvailabilityToAllButtons(false);
                    m_buy.SetActive(true);

                    _canBuy = true;
                }
                else
                {
                    SetAvailabilityToAllButtons(false);
                    m_ADOffer.SetActive(true);
                }
            }
        }

        bool isCurrent = m_index == _skinManager.CurrentSkinIndex;
        _buyButton.enabled = !isCurrent;
        _buyButton.GetComponent<Image>().sprite = isCurrent ? m_ifCurrentModelSprite : _defaultBtnSprite;
        _buyButton.GetComponent<Image>().color = isCurrent ? Color.white : _defaultBtnColor;
    }

    private void BuySkin()
    {
        if (_coinManager.RemoveCoins(m_skinCost) == true)
        {
            foreach (var model in _skinManager.SkinsData)
            {
                if (model.SkinIndex == m_index)
                {
                    model.IsPurchased = true;

                    UpdatePanel();

                    _skinManager.SaveSkinsData();
                }
            }
        }
    }

    private void SetAvailabilityToAllButtons(bool state)
    {
        m_choose.SetActive(state);
        m_buy.SetActive(state);
        m_ADOffer.SetActive(state);
    }
}