using UnityEngine;
using Zenject;
using DG.Tweening;
using TMPro;

public class UI_CoinFlight : MonoBehaviour
{
    [SerializeField] private GameObject m_coinImagePrefab;
    [SerializeField] private Transform m_targetIcon;

    [SerializeField] private Vector3 m_startScale;

    [SerializeField] private float m_duration;

    [SerializeField] private GameObject[] m_randomMiddlePoints;

    [SerializeField] private TMP_Text m_coinsText;

    private CoinManager _coinManager;
    private MovementController _movementController;
    private TileController _tileController;

    private Vector2 _firstPos;
    private Vector2 _lastPos;
    private Vector3[] _path;

    private int _coinsImageCount;
    private GameObject[] _coinsImages;
    private int _currentImageNumber;

    private Vector3 _endScale;

    [Inject]
    public void Construct(CoinManager coinManager, MovementController movementController, TileController tileController)
    {
        _coinManager = coinManager;
        _movementController = movementController;
        _tileController = tileController;
    }

    private void Start()
    {
        _coinManager.CoinsCountChanged += ShowAnimation;

        _lastPos = m_targetIcon.position;
        _endScale = m_targetIcon.localScale;

        float movementSpeed = 1f / (_tileController.Period / _movementController.Step);
        _coinsImageCount = (int)(movementSpeed * m_duration) + 1;

        _coinsImages = new GameObject[_coinsImageCount];

        for (int i = 0; i < _coinsImages.Length; i++)
        {
            _coinsImages[i] = Instantiate(m_coinImagePrefab, transform);
            _coinsImages[i].transform.localScale = Vector3.zero;
        }
    }

    private void OnDestroy()
    {
        _coinManager.CoinsCountChanged -= ShowAnimation;
    }

    private void ShowAnimation(int _)
    {
        _firstPos = RectTransformUtility.WorldToScreenPoint(Camera.main, _movementController.transform.position);

        int rnd = Random.Range(0, m_randomMiddlePoints.Length);

        Vector2 middlePos = m_randomMiddlePoints[rnd].transform.position;

        _path = new Vector3[] { _firstPos, middlePos, _lastPos };

        var currentImage = _coinsImages[_currentImageNumber];

        currentImage.transform.position = _firstPos;
        currentImage.transform.localScale = m_startScale;

        currentImage.transform.DOPath(_path, m_duration, PathType.CatmullRom);
        currentImage.transform.DOScale(_endScale, m_duration).OnComplete(TextScaling);

        _currentImageNumber++;

        if (_currentImageNumber == _coinsImages.Length)
        {
            _currentImageNumber = 0;
        }
    }

    private void TextScaling()
    {
        /* 
         * TODO Visual effect of text scaling
         * 
        Vector3 defaultScale = m_coinsText.transform.localScale;
        
        var sequence = DOTween.Sequence()
           .Append(m_coinsText.transform.DOScale(defaultScale * 1.5f, 0.2f)).SetEase(Ease.OutBack)
           .Append(m_coinsText.transform.DOScale(defaultScale, 0.2f)).SetEase(Ease.InBack);

        sequence.Kill();
        */
    }
}