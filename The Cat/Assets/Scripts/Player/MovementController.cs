using UnityEngine;
using UnityEngine.UI;
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

    [SerializeField] private float _step = 1f;

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

    private float posZ;

    private float _startTimeNewAction;

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
        _speed = 1f / (_tileController.Period / _step);
    }

    private void Update()
    {
        if (_startTime == 0)
        {
            _startTime = Time.time;
        }

        if (_isLose)
        {
            _levelSecuenceController.Lose();

            enabled = false;
        }
        else
        {
            MouseInput();

            UpdatePosition();

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

            if (!_isJump && !_isMove)
            {
                CheckCollisions();
            }
        }
    }

    public void UpdatePosition()
    {
        float bounds = 1.5f;

        float posX = transform.position.x + _playerInputs.MouseAxisRight * m_sensitivity;

        posX = Mathf.Clamp(posX, -bounds, bounds);

        posZ = (Time.time - _startTime) * _speed;

        transform.position = new Vector3(posX, posY, posZ);
    }

    public void Move()
    {
        _targetPos = _tileController.NextTile().JumpPosition;

        _startTimeNewAction = Time.time;

        if (_currentTile.TileType == TileType.Static)
        {
            _isJump = true;
        }

        if (_currentTile.TileType == TileType.Long)
        {
            _isLongMove = true;
        }

        if (_currentTile.TileType == TileType.Move)
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

        if (posZ >= _currentTile.FinishPosition.z)
        {
            _isMove = false;

            _isJump = true;

            _startTimeNewAction = Time.time;

            _currentTile.FadeTile();

            m_parentPointTransform.GetComponentInChildren<BoxCollider>().transform.SetParent((_currentTile as MoveTile).ParentTransform);
        }
    }

    private void MoveToLongTile()
    {
        _distance = _currentTile.Distance;

        PlayerRotation();

        if (posZ >= _currentTile.FinishPosition.z)
        {
            _isJump = true;

            _isLongMove = false;

            _startTimeNewAction = Time.time;

            _currentTile.FadeTile();

            posY = 0;
        }
    }

    private void Jump()
    {
        _distance = _targetPos.z - _currentTile.FinishPosition.z;

        if (posZ >= _targetPos.z)
        {
            _isJump = false;

            posY = 0;
        }
        else
        {
            posY = m_jumpCurve.Evaluate(((Time.time - _startTimeNewAction) * _speed / _distance) % 1) * Mathf.Clamp(_distance, 0, m_maxHightToJump);

            if (posY == float.NaN)
            {
                posZ = 0;
            }
        }
    }

    private void CheckCollisions()
    {
        RaycastHit[] result = new RaycastHit[2];

        if (Physics.BoxCastNonAlloc(transform.position, Vector3.one * 0.3f, Vector3.down, result, Quaternion.identity, 2, m_layerMask) == 1)
        {
            var tile = result[0].transform.parent.GetComponent<Tile>();

            if (_currentTile == tile) return;

            _currentTile = tile;

            Move();

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
        float rotationAngle = 360f * (((Time.time - _startTimeNewAction) * _speed / _distance) % 1);

        m_playerTransform.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);
    }

    public void SetMovementState(bool state)
    {
        enabled = state;
    }
}
