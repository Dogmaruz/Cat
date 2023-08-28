using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

public class UIVirtualTouchZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Rect References")]
    [SerializeField] private RectTransform m_containerRect;

    [Header("Settings")]
    [SerializeField] private float m_magnitudeMultiplier = 50f;

    private Vector2 _pointerDownPosition;

    private Vector2 _currentPointerPosition;

    private Vector2 _previousPositionDelta;

    private Vector2 _startPosition;

    private bool _isMovingRight;

    private Resolution _resolution;

    private Vector2 _positionDelta;

    private MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    private void Awake()
    {
        _resolution = Screen.currentResolution;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_containerRect, eventData.position, eventData.pressEventCamera, out _pointerDownPosition);
        _startPosition = _pointerDownPosition;
        _isMovingRight = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _previousPositionDelta = _currentPointerPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_containerRect, eventData.position, eventData.pressEventCamera, out _currentPointerPosition);

        if (!_isMovingRight && _currentPointerPosition.x - _startPosition.x > 0)
        {
            _isMovingRight = true;
            _startPosition = _currentPointerPosition;
        }
        else if (_isMovingRight && _currentPointerPosition.x - _startPosition.x < 0)
        {
            _isMovingRight = false;
            _startPosition = _currentPointerPosition;
        }

        _previousPositionDelta = _positionDelta;
        _positionDelta = GetDeltaBetweenPositions(_startPosition, _currentPointerPosition);

        OutputPointerEventValue(new Vector2(_positionDelta.x, _positionDelta.y));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDownPosition = Vector2.zero;
        _currentPointerPosition = Vector2.zero;
        _previousPositionDelta = Vector2.zero;
        _positionDelta = Vector2.zero;
        _startPosition = Vector2.zero;
        _isMovingRight = false;

        OutputPointerEventValue(Vector2.zero);
    }

    void OutputPointerEventValue(Vector2 pointerPosition)
    {
        //_cat.SetMouseAxisRight(pointerPosition.x * m_magnitudeMultiplier);
    }

    Vector2 GetDeltaBetweenPositions(Vector2 firstPosition, Vector2 secondPosition)
    {
        return secondPosition - firstPosition;
    }
}
