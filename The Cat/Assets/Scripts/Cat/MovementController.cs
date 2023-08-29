using UnityEngine;
using Zenject;

public struct PlayerInputs
{
    public float MouseAxisRight;
}

public class MovementController : MonoBehaviour
{
    [SerializeField] private Transform m_cat;
    public Transform Cat => m_cat;

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

    private bool _isTouch;

    private bool _isJump;

    private bool _isLongMove;

    private bool _isMove;

    private bool _isLose;


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

        _collider = m_cat.GetComponent<BoxCollider>();

        _playerInputAction.Player.Enable();

        _collider.enabled = false;

        _lastPosition = m_cat.transform.localPosition;

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
        float bounds = 2f;

        float newPositionX = Mathf.Clamp(m_cat.transform.localPosition.x + _playerInputs.MouseAxisRight * m_sensitivity, -bounds, bounds);

        m_cat.transform.localPosition = new Vector3(newPositionX, m_cat.transform.localPosition.y, m_cat.transform.localPosition.z);
    }

    public void ActivateMovement()
    {
        _collider.enabled = true;

        enabled = true;
    }

    public void Move(Tile currentTile)
    {
        var target = _tileController.NextTile();

        _targetPos = target.BasePosition;

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

        var distance =  (_currentTile as MoveTile).EndPosition.position.z - transform.TransformPoint(_lastPosition).z;

        _currentTile.transform.SetParent(m_cat.transform);

        _currentTile.transform.localPosition = new Vector3(0, _currentTile.transform.localPosition.y, _currentTile.transform.localPosition.z);

        position.x = m_cat.transform.position.x;

        position.y = 0;

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        float rotationAngle = 360f * (_currentTime / _totalTime);

        m_cat.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        m_cat.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);

        if (_currentTime >= _totalTime)
        {
            _currentTime = 0f;

            _lastPosition = m_cat.transform.localPosition;

            _isMove = false;

            _isJump = true;

            _currentTile.FadeTile();

            _currentTile.transform.SetParent((_currentTile as MoveTile).Parent);
        }

        _currentTime += 1 / _tileController.SecPerBeat / (distance / 2) * Time.deltaTime;
    }

    private void MoveToLongTile()
    {
        var position = _lastPosition;

        var distance = _currentTile.transform.localScale.z - 1;

        position.x = m_cat.transform.position.x;

        position.y = 0;

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        float rotationAngle = 360f * (_currentTime / _totalTime);

        m_cat.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        m_cat.transform.localRotation = Quaternion.Euler(0, rotationAngle, 0);

        if (_currentTime >= _totalTime)
        {
            _currentTime = 0f;

            _lastPosition = m_cat.transform.localPosition;

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

        position.x = m_cat.transform.position.x;

        position.y = m_jumpCurve.Evaluate(_currentTime) * distance;

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        m_cat.transform.localPosition = new Vector3(0, _lastPosition.y, _lastPosition.z) + position;


        if (_currentTime >= _totalTime)
        {
            _isJump = false;

            _currentTime = 0f;

            _lastPosition = m_cat.transform.localPosition;

            return;
        }

        _currentTime += 1 / _tileController.SecPerBeat / (distance - 1f) * Time.deltaTime;
    }


    private void CheckCollisions()
    {
        float maxDistance = 4f;

        Vector3 origin = m_cat.transform.position + Vector3.up;

        RaycastHit hitInfo;

        if (Physics.BoxCast(origin, new Vector3(0.15f, 0.15f, 0.15f), Vector3.down, out hitInfo, transform.rotation, maxDistance, m_layerMask))
        {
            Collider hitCollider = hitInfo.collider;

            var killZone = hitCollider.gameObject.GetComponent<KillZone>();

            if (killZone != null)
            {
                if (_currentTile.TileType == TileType.Move)
                {
                    _currentTile.transform.SetParent(_currentTile.GetComponent<MoveTile>().Parent);
                }

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
        if (!_isTouch)
        {
            float xValue = _playerInputAction.Player.MouseAxisX.ReadValue<Vector2>().x;

            _playerInputs.MouseAxisRight = xValue;
        }
    }

    public void SetMouseAxisRight(float xValue)
    {
        _isTouch = true;

        _playerInputs.MouseAxisRight = xValue;
    }
}
