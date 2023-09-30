using UnityEngine;
using Zenject;

public struct PlayerInputs
{
    public float MouseAxisRight;
}

public class MovementController : MonoBehaviour
{
    [SerializeField] private Transform m_parentPointTransform;

    [SerializeField] private Transform m_playerTransform;
    public Transform PlayerTransform => m_playerTransform;

    [SerializeField] private float m_maxHightToJump = 3f;

    [SerializeField] private float m_sensitivity = 0.1f;

    [SerializeField] private LayerMask m_layerMask;

    [SerializeField] private AnimationCurve m_moveCurve;

    [SerializeField] private AnimationCurve m_jumpCurve;

    private LevelSecuenceController _levelSecuenceController;

    private TileController _tileController;

    private PlayerInputAction _playerInputAction;

    private PlayerInputs _playerInputs;

    private Tile _currentTile;

    private Vector3 _targetPos;

    private Vector3 _lastPosition;

    private float _currentTime;

    private bool _isJump;

    private bool _isLongMove;

    private bool _isMove;
    public bool _isLose { get; set; }

    [Inject]
    public void Construct(TileController TileController, LevelSecuenceController levelSecuenceController)
    {
        _tileController = TileController;

        _levelSecuenceController = levelSecuenceController;
    }

    private void Awake()
    {
        _playerInputs = new PlayerInputs();

        _playerInputAction = new PlayerInputAction();

        _playerInputAction.Player.Enable();

        _lastPosition = m_playerTransform.transform.localPosition;

        enabled = false;
    }

    private void Update()
    {
        if (_isLose)
        {
            _levelSecuenceController.Lose();

            enabled = false;
        }
        else
        {
            MouseInput();

            UpdatePosition();

            if (!_isJump && !_isMove)
            {
                CheckCollisions();
            }

            if (_isJump)
            {
                Jump();
            }

            if (_isLongMove)
            {
                MoveToLongTile();
            }

            if (_isMove)
            {
                MoveTile();
            }
        }

        _playerInputs.MouseAxisRight = 0;
    }

    public void UpdatePosition()
    {
        float bounds = 2.5f;

        float newPositionX = Mathf.Clamp(m_playerTransform.transform.localPosition.x + _playerInputs.MouseAxisRight * m_sensitivity * Time.deltaTime, -bounds, bounds);

        m_playerTransform.transform.localPosition = new Vector3(newPositionX, m_playerTransform.transform.localPosition.y, m_playerTransform.transform.localPosition.z);
    }

    public void Move(Tile currentTile)
    {
        var target = _tileController.NextTile();

        _targetPos = target.JumpPosition;

        if (currentTile.TileType == TileType.Static)
        {
            _isJump = true;
        }

        if (currentTile.TileType == TileType.Long)
        {
            _isLongMove = true;
        }

        if (currentTile.TileType == TileType.Move)
        {

            _isMove = true;
        }
    }

    private void MoveTile()
    {
        var position = _lastPosition;

        var distance = (_currentTile as MoveTile).EndPosition.position.z - transform.TransformPoint(_lastPosition).z;

        _currentTile.transform.SetParent(m_parentPointTransform.transform);

        _currentTile.transform.localPosition = new Vector3(0, _currentTile.transform.localPosition.y, _currentTile.transform.localPosition.z);

        position.x = m_playerTransform.transform.position.x;

        position.y = 0;

        position.z = m_moveCurve.Evaluate(_currentTime / (_tileController.Period * distance)) * distance;

        float rotationAngle = 360f * (_currentTime / (_tileController.Period * distance));

        m_playerTransform.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        m_playerTransform.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);

        if (_currentTime / (_tileController.Period * distance) >= 1)
        {
            _currentTime = 0f;

            _lastPosition = m_playerTransform.transform.localPosition;

            _isMove = false;

            _isJump = true;

            _currentTile.FadeTile();

            _currentTile.transform.SetParent((_currentTile as MoveTile).ParentTransform);
        }

        _currentTime += Time.deltaTime;
    }

    private void MoveToLongTile()
    {
        var position = _lastPosition;

        var distance = (_currentTile as LongTile).EndJumpPosition.transform.position.z - _currentTile.JumpPosition.z;

        position.x = m_playerTransform.transform.position.x;

        position.y = 0;

        position.z = m_moveCurve.Evaluate(_currentTime / (_tileController.Period * distance)) * distance;

        float rotationAngle = 360f * (_currentTime / (_tileController.Period * distance));

        m_playerTransform.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        m_playerTransform.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);

        if (_currentTime / (_tileController.Period * distance) >= 1)
        {
            _currentTime = 0f;

            _lastPosition = m_playerTransform.transform.localPosition;

            _isLongMove = false;

            _isJump = true;

            _currentTile.FadeTile();
        }

        _currentTime += Time.deltaTime;
    }

    private void Jump()
    {
        var position = _lastPosition;

        var distance = _targetPos.z - transform.TransformPoint(_lastPosition).z;

        position.x = m_playerTransform.transform.position.x;

        position.y = m_jumpCurve.Evaluate(_currentTime / (_tileController.Period * distance)) * Mathf.Clamp(distance, 0, m_maxHightToJump);

        position.z = m_moveCurve.Evaluate(_currentTime / (_tileController.Period * distance)) * distance;

        m_playerTransform.transform.localPosition = new Vector3(0, _lastPosition.y, _lastPosition.z) + position;

        if (_currentTime / (_tileController.Period * distance) >= 1)
        {
            _isJump = false;

            _currentTime = 0f;

            _lastPosition = new Vector3(m_playerTransform.transform.localPosition.x, 0, m_playerTransform.transform.localPosition.z);

            return;
        }

        _currentTime += Time.deltaTime;
    }

    private void CheckCollisions()
    {
        Collider[] hitColliders = new Collider[1];

        if (Physics.OverlapBoxNonAlloc(m_playerTransform.transform.position, Vector3.one * 0.4f, hitColliders, transform.rotation, m_layerMask) == 1)
        {
            var tile = hitColliders[0].GetComponent<Tile>();

            if (_currentTile == tile) return;

            _currentTile = tile;

            Move(tile);

            if (tile.TileType == TileType.Static)
            {
                tile.FadeTile();
            }
        }
        else
        {
            _isLose = true;
        }
    }

    private void MouseInput()
    {
        float xValue = _playerInputAction.Player.MouseAxisX.ReadValue<Vector2>().x;

        _playerInputs.MouseAxisRight = xValue;
    }

    public void ActivateMovement()
    {
        enabled = true;
    }

    public void StopMovement()
    {
        enabled = false;
    }
}
