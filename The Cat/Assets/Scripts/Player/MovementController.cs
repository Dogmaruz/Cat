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

    [SerializeField] private float m_sensitivity = 0.1f;

    [SerializeField] private LayerMask m_layerMask;

    [SerializeField] private AnimationCurve m_moveCurve;

    [SerializeField] private AnimationCurve m_jumpCurve;

    private LevelSecuenceController _levelSecuenceController;

    private TileController _tileController;

    private PlayerInputAction _playerInputAction;

    private PlayerInputs _playerInputs;

    private BoxCollider _collider;

    private Tile _currentTile;

    private Vector3 _targetPos;

    private Vector3 _lastPosition;

    private float _totalTime;

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

        _collider = m_playerTransform.GetComponent<BoxCollider>();

        _playerInputAction.Player.Enable();

        //_collider.enabled = false;

        _lastPosition = m_playerTransform.transform.localPosition;

        _totalTime = m_jumpCurve.keys[m_jumpCurve.keys.Length - 1].time;

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

            if (_isJump == false && _isMove == false)
            {
                CheckCollisions();
            }

            if (_isJump == true)
            {
                Jump();
            }

            if (_isLongMove == true)
            {
                MoveToLongTile();
            }

            if (_isMove == true)
            {
                MoveTile();
            }
        }

        _playerInputs.MouseAxisRight = 0;
    }

    public void UpdatePosition()
    {
        float bounds = 2.5f;

        float newPositionX = Mathf.Clamp(m_playerTransform.transform.localPosition.x + _playerInputs.MouseAxisRight * m_sensitivity, -bounds, bounds);

        m_playerTransform.transform.localPosition = new Vector3(newPositionX, m_playerTransform.transform.localPosition.y, m_playerTransform.transform.localPosition.z);
    }

    public void ActivateMovement()
    {
        //_collider.enabled = true;

        enabled = true;
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

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        float rotationAngle = 360f * (_currentTime / _totalTime);

        m_playerTransform.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        m_playerTransform.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);

        if (_currentTime >= _totalTime)
        {
            _currentTime = 0f;

            _lastPosition = m_playerTransform.transform.localPosition;

            _isMove = false;

            _isJump = true;

            _currentTile.FadeTile();

            _currentTile.transform.SetParent((_currentTile as MoveTile).ParentTransform);
        }

        _currentTime += 1 / _tileController.SecPerBeat / (distance / 2) * Time.deltaTime;
    }

    private void MoveToLongTile()
    {
        var position = _lastPosition;

        var distance = _currentTile.transform.localScale.z - 1;

        position.x = m_playerTransform.transform.position.x;

        position.y = 0;

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        float rotationAngle = 360f * (_currentTime / _totalTime);

        m_playerTransform.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        m_playerTransform.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);

        if (_currentTime >= _totalTime)
        {
            _currentTime = 0f;

            _lastPosition = m_playerTransform.transform.localPosition;

            _isLongMove = false;

            _isJump = true;

            _currentTile.FadeTile();
        }

        _currentTime += 1 / _tileController.SecPerBeat / (distance) * Time.deltaTime;
    }

    private void Jump()
    {
        var position = _lastPosition;

        var distance = _targetPos.z - transform.TransformPoint(_lastPosition).z;

        position.x = m_playerTransform.transform.position.x;

        position.y = m_jumpCurve.Evaluate(_currentTime) * distance;

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        m_playerTransform.transform.localPosition = new Vector3(0, _lastPosition.y, _lastPosition.z) + position;

        if (_currentTime >= _totalTime)
        {
            _isJump = false;

            _currentTime = 0f;

            _lastPosition = m_playerTransform.transform.localPosition;

            return;
        }

        _currentTime += 1 / _tileController.SecPerBeat / (distance - 1f) * Time.deltaTime;
    }

    private void CheckCollisions()
    {
        float maxDistance = 4f;

        Vector3 origin = m_playerTransform.transform.position + Vector3.up;

        RaycastHit hitInfo;

        if (Physics.BoxCast(origin, new Vector3(0.3f, 0.3f, 0.3f), Vector3.down, out hitInfo, transform.rotation, maxDistance, m_layerMask))
        {
            Collider hitCollider = hitInfo.collider;

            var killZone = hitCollider.gameObject.GetComponent<KillZone>();

            if (killZone != null)
            {
                _isLose = true;
            }
            else
            {
                var tile = hitCollider.GetComponent<Tile>();

                if (_currentTile == tile) return;

                _currentTile = tile;

                Move(tile);

                if (tile.TileType == TileType.Static)
                {
                    tile.FadeTile();
                }
            }
        }
    }

    private void MouseInput()
    {
        float xValue = _playerInputAction.Player.MouseAxisX.ReadValue<Vector2>().x;

        _playerInputs.MouseAxisRight = xValue;
    }

    public void StopMovement()
    {
        enabled = false;
    }
}
