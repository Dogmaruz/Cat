using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class VisualModel : MonoBehaviour
{
    [SerializeField] private PlayerVisualModel m_visualModel;

    [SerializeField] private Image m_image;
    [SerializeField] private TMP_Text m_skinCostText;

    [SerializeField] private GameObject m_choose;
    [SerializeField] private GameObject m_buy;
    [SerializeField] private GameObject m_ADOffer;

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
        _buyButton.onClick.AddListener(Click);

        // TODO Add images;
        //m_image.sprite = m_visualModel.Sprite;
        //m_image.color = Color.white;

        m_skinCostText.text = m_visualModel.Cost.ToString();

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
            _charactersPanel.CloseCharactersPanel();
        }
        else if (_canChoose)
        {
            _skinManager.ChangePlayerVisualModel(m_visualModel);

            UpdatePanel();

            _charactersPanel.CloseCharactersPanel();
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
        bool isCoinsEnough = _coinManager.CoinsCount >= m_visualModel.Cost;

        foreach (var model in _skinManager.SkinsData)
        {
            if (model.m_VisualModel == m_visualModel)
            {
                if (m_visualModel == _skinManager.CurrentVisualModel)
                {
                    SetAvailabilityToAllButtons(false);
                    _isCurrent = true;
                }
                else if (model.isPurchased == true)
                {
                    SetAvailabilityToAllButtons(false);
                    m_choose.SetActive(true);

                    _canChoose = true;
                    _canBuy = false;
                }
                else if (model.isPurchased == false && isCoinsEnough)
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
    }

    private void BuySkin()
    {
        if (_coinManager.RemoveCoins(m_visualModel.Cost) == true)
        {
            foreach (var model in _skinManager.SkinsData)
            {
                if (model.m_VisualModel == m_visualModel)
                {
                    model.isPurchased = true;

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