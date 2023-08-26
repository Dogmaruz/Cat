using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Zenject;

public class UIVirtualTouchZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [System.Serializable]
    public class Event : UnityEvent<Vector2> { }

    [Header("Rect References")]
    public RectTransform ContainerRect;

    [Header("Settings")]
    public float MagnitudeMultiplier = 1f;

    public bool InvertXOutputValue;

    public bool InvertYOutputValue;

    //Stored Pointer Values
    private Vector2 _pointerDownPosition;

    private Vector2 _currentPointerPosition;

    private Vector2 _previousPositionDelta;

    private Resolution _resolution;

    private Vector2 _positionDelta;

    [Header("Output")]
    public Event TouchZoneOutputEvent;

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
        RectTransformUtility.ScreenPointToLocalPointInRectangle(ContainerRect, eventData.position, eventData.pressEventCamera, out _pointerDownPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _previousPositionDelta = _positionDelta;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(ContainerRect, eventData.position, eventData.pressEventCamera, out _currentPointerPosition);

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
        TouchZoneOutputEvent?.Invoke(pointerPosition);

        _cat.SetMouseAxisRight(pointerPosition.x / _resolution.width * MagnitudeMultiplier);
    }

    Vector2 GetDeltaBetweenPositions(Vector2 firstPosition, Vector2 secondPosition)
    {
        return secondPosition - firstPosition;
    }
}
