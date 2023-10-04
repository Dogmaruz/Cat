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

    [SerializeField] private float m_sensitivity = 1f;

    [SerializeField] private LayerMask m_layerMask;

    [SerializeField] private AnimationCurve m_jumpCurve;

    private LevelSecuenceController _levelSecuenceController;

    private TileController _tileController;

    private PlayerInputAction _playerInputAction;

    private PlayerInputs _playerInputs;

    private Tile _currentTile;

    private Vector3 _targetPos;

    private bool _isJump;

    private bool _isLongMove;

    private bool _isMove;
    public bool _isLose { get; set; }

    private float _speed;

    private float _startTime;

    private float _distance;

    private float posY;

    private float _startTimeY;

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

        enabled = false;
    }

    private void Start()
    {
        _speed = 1f / _tileController.Period;
    }

    private void Update()
    {
        if (_startTime == 0)
        {
            _startTime = Time.time;

            _startTimeY = Time.time;
        }

        if (_isLose)
        {
            _levelSecuenceController.Lose();

            enabled = false;
        }
        else
        {
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

            MouseInput();

            UpdatePosition();
        }
    }

    public void UpdatePosition()
    {
        float bounds = 4.5f;

        float posX = Mathf.Clamp(transform.position.x + _playerInputs.MouseAxisRight * m_sensitivity * Time.deltaTime, -bounds, bounds);

        float posZ = (Time.time - _startTime) * _speed;

        transform.position = new Vector3(posX, posY, posZ);
    }

    public void Move(Tile currentTile)
    {
        _targetPos = _tileController.NextTile().JumpPosition;

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
        _distance = _currentTile.Distance;

        var platforn = _currentTile.Collider;

        if (platforn != null)
        {
            platforn.transform.SetParent(m_parentPointTransform.transform);

            platforn.transform.localPosition = new Vector3(0, platforn.transform.localPosition.y, platforn.transform.localPosition.z);
        }

        PlayerRotation();

        if (Mathf.FloorToInt((Time.time - _startTime) * _speed) == _currentTile.FinishPosition.z)
        {
            _isMove = false;

            _isJump = true;

            _startTimeY = Time.time;

            _currentTile.FadeTile();

            m_parentPointTransform.GetComponentInChildren<BoxCollider>().transform.SetParent((_currentTile as MoveTile).ParentTransform);
        }
    }

    private void MoveToLongTile()
    {
        _distance = _currentTile.Distance;

        PlayerRotation();

        if (Mathf.FloorToInt((Time.time - _startTime) * _speed) == _currentTile.FinishPosition.z)
        {
            _isJump = true;

            _isLongMove = false;

            _startTimeY = Time.time;

            _currentTile.FadeTile();

            posY = 0;
        }
    }

    private void Jump()
    {
        _distance = _targetPos.z - _currentTile.FinishPosition.z;

        posY = m_jumpCurve.Evaluate(((Time.time - _startTimeY) * _speed / _distance) % 1) * Mathf.Clamp(_distance, 0, m_maxHightToJump);

        if (Mathf.FloorToInt((Time.time - _startTime) * _speed) == _targetPos.z)
        {
            _isJump = false;

            _startTimeY = Time.time;

            posY = 0;
        }
    }

    private void CheckCollisions()
    {
        RaycastHit[] result = new RaycastHit[2];

        if (Physics.BoxCastNonAlloc(transform.position, Vector3.one * 0.3f, Vector3.down, result, Quaternion.identity, 1, m_layerMask) == 1)
        {
            var tile = result[0].transform.parent.GetComponent<Tile>();

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

    private void PlayerRotation()
    {
        float rotationAngle = 360f * (((Time.time - _startTimeY) * _speed / _distance) % 1);

        m_playerTransform.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);
    }

    public void SetMovementState(bool state)
    {
        enabled = state;
    }
}
