using DG.Tweening;
using UnityEngine;
using Zenject;

public struct PlayerInputs
{
    public float MouseAxisRight;
}

public class MovementController : MonoBehaviour
{
    [SerializeField] private Transform m_cat;

    [SerializeField] private float m_sensitivity = 0.1f;

    private Tween _tween;

    private bool _isTouch;

    private BoxCollider _collider;

    TileController _platformController;

    protected PlayerInputs _playerInputs;

    private PlayerInputAction _playerInputAction;
    public PlayerInputAction PlayerInputAction { get => _playerInputAction; set => _playerInputAction = value; }

    private float _maxSpeedLookAxis = 50;

    private Vector3 _position;

    [Inject]
    public void Construct(TileController platformController)
    {
        _platformController = platformController;
    }

    private void Awake()
    {
        _collider = m_cat.GetComponent<BoxCollider>();

        _collider.enabled = false;

        _playerInputs = new PlayerInputs();

        _playerInputAction = new PlayerInputAction();

        _playerInputAction.Player.Enable();
    }

    private void OnDestroy()
    {
        _tween.Kill();
    }

    private void Update()
    {
        //ѕремещение персонажа с клавиатуры и мыши
        MouseInput();

        UpdatePosition(_playerInputs.MouseAxisRight);

        _playerInputs.MouseAxisRight = 0;
    }

    public void UpdatePosition(float mouseX)
    {
        Vector3 newPosition = transform.position + new Vector3(mouseX * m_sensitivity, 0f, 0f);

        transform.position = newPosition;
    }

    public void EnabledCollider()
    {
        _collider.enabled = true;
    }

    public void Move(Tile currentTile)
    {
        var target = _platformController.NextTile();
        var result = target.transform.position.z - currentTile.transform.position.z - 1;

        _tween.Kill();

        _tween = transform.DOJump(new Vector3(_position.x, transform.position.y, target.transform.position.z), result, 1, result * _platformController.SecPerBeat)
                          .SetEase(Ease.Linear);
    }

    protected virtual void MouseInput()
    {
        if (!_isTouch)
        {
            float xValue = _playerInputAction.Player.MouseAxisX.ReadValue<Vector2>().x;

            _playerInputs.MouseAxisRight = xValue >= 0 ? Mathf.Clamp(xValue, 0, _maxSpeedLookAxis) : Mathf.Clamp(xValue, -_maxSpeedLookAxis, 0);
        }
    }

    public void SetMouseLookAxisRight(float value)
    {
        _isTouch = true;

        float xValue = value;

        _playerInputs.MouseAxisRight = xValue >= 0 ? Mathf.Clamp(xValue, 0, _maxSpeedLookAxis) : Mathf.Clamp(xValue, -_maxSpeedLookAxis, 0);
    }
}
