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

    private Resolution _resolution;

    private Vector2 _positionDelta;

    private MovementController _cat;

    private float _directionIndex;

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
    }

    public void OnDrag(PointerEventData eventData)
    {
        _previousPositionDelta = _positionDelta;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_containerRect, eventData.position, eventData.pressEventCamera, out _currentPointerPosition);

        _positionDelta = GetDeltaBetweenPositions(_pointerDownPosition, _currentPointerPosition);

        float oldDirectionIndex = _directionIndex;

        if (Mathf.Abs(_previousPositionDelta.x) > Mathf.Abs(_positionDelta.x))
        {
            _directionIndex = -1;
        }
        else
        {
            _directionIndex = 1;
        }

        if (oldDirectionIndex != _directionIndex)
        {
            _pointerDownPosition = _currentPointerPosition;

            _positionDelta *= _directionIndex;
        }

        OutputPointerEventValue(new Vector2(_positionDelta.x, _positionDelta.y));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDownPosition = Vector2.zero;

        _positionDelta = Vector2.zero;

        _pointerDownPosition = Vector2.zero;

        _currentPointerPosition = Vector2.zero;

        OutputPointerEventValue(Vector2.zero);
    }

    void OutputPointerEventValue(Vector2 pointerPosition)
    {
        _cat.SetMouseAxisRight(pointerPosition.x / _resolution.width * m_magnitudeMultiplier);
    }

    Vector2 GetDeltaBetweenPositions(Vector2 firstPosition, Vector2 secondPosition)
    {
        return secondPosition - firstPosition;
    }
}
