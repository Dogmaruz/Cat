using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using DG.Tweening;

public class UI_CoinFlight : MonoBehaviour
{
    [SerializeField] private GameObject m_coinImagePrefab;
    [SerializeField] private Transform m_targetIcon;

    [SerializeField] private Vector3 startScale;

    [SerializeField] float m_duration;

    [SerializeField] private GameObject[] m_randomMiddlePoints;

    private CoinManager _coinManager;
    private MovementController _movementController;
    private TileController _tileController;

    private Vector2 _firstPos;
    private Vector2 _middlePos;
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

        _middlePos = m_randomMiddlePoints[rnd].transform.position;

        _path = new Vector3[] { _firstPos, _middlePos, _lastPos };

        var currentImage = _coinsImages[_currentImageNumber];

        currentImage.transform.position = _firstPos;
        currentImage.transform.localScale = startScale;

        currentImage.transform.DOPath(_path, m_duration, PathType.CatmullRom);
        currentImage.transform.DOScale(_endScale, m_duration);

        _currentImageNumber++;
        if (_currentImageNumber == _coinsImages.Length)
        {
            _currentImageNumber = 0;
        }
    }
}