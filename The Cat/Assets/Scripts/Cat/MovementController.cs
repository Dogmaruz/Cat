using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
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

    private bool _isTouch;

    private bool _isJump;

    private bool _isLongMove;

    private bool _isLose;

    private Tile _currentTile;

    private Vector3 _targetPos;

    private float _totalTime;

    private float _currentTime;

    private Vector3 _lastPosition;


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

        enabled = false;

        _lastPosition = m_cat.transform.localPosition;

        _totalTime = m_jumpCurve.keys[m_jumpCurve.keys.Length - 1].time;
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

            UpdatePosition(_playerInputs.MouseAxisRight);

            if (_isJump == false && _isLongMove == false)
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
        }

        _playerInputs.MouseAxisRight = 0;
    }

    public void UpdatePosition(float mouseX)
    {
        float bounds = 2f;

        float newPositionX = Mathf.Clamp(m_cat.transform.localPosition.x + mouseX * m_sensitivity, -bounds, bounds);

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

        //if (currentTile.TileType == TileType.Move)
        //{
        //    _sequence.Kill();

        //    float offset = 1;

        //    float angleY = 360;

        //    MovePlatform movePlatform = currentTile.GetComponent<MovePlatform>();

        //    var distance = Vector3.Distance(currentTile.transform.position, movePlatform.EndPosition.position);

        //    var newPos = m_cat.transform.position.z + distance;

        //    currentTile.transform.SetParent(m_checkPosition);

        //    currentTile.transform.localPosition = new Vector3(0, currentTile.transform.localPosition.y, currentTile.transform.localPosition.z);

        //    _sequence = DOTween.Sequence()
        //   .Append(m_cat.transform.DOMoveZ(newPos, (distance + offset) * _platformController.SecPerBeat))
        //   .Join(m_cat.transform.DORotate(new Vector3(0, angleY, 0), (distance + offset) * _platformController.SecPerBeat, RotateMode.FastBeyond360))
        //   .SetEase(Ease.Linear)
        //   .OnComplete(() =>
        //   {
        //       currentTile.transform.SetParent(movePlatform.Parent);

        //       Jump(newPos);

        //       currentTile.FadeTile();
        //   });
        //}
    }

    private void MoveToLongTile()
    {
        //float angleY = 360;

        _isJump = false;

        var position = _lastPosition;

        var distance = _currentTile.transform.localScale.z - 1;

        //var newPos = m_cat.transform.position.z + distance;

        position.x = m_cat.transform.position.x;

        position.y = m_cat.transform.position.y;

        Debug.Log(position.y);

        position.z = m_moveCurve.Evaluate(_currentTime) * distance;

        m_cat.transform.localPosition = new Vector3(0, 0, _lastPosition.z) + position;

        if (_currentTime >= _totalTime)
        {
            _currentTime = 0f;

            _lastPosition = m_cat.transform.localPosition;

            _isLongMove = false;

            _isJump = true;

            _currentTile.FadeTile();
        }

        _currentTime += 1 / _tileController.SecPerBeat / (distance - 1) * Time.deltaTime;


        // _sequence = DOTween.Sequence()
        //.Append(m_cat.transform.DOMoveZ(newPos, (distance + 1) * _tileController.SecPerBeat))
        //.Join(m_cat.transform.DORotate(new Vector3(0, angleY, 0), (distance + 1) * _tileController.SecPerBeat, RotateMode.FastBeyond360))
        //.SetEase(Ease.Linear)
        //.OnComplete(() =>
        //{
        //    Jump(newPos);

        //    currentTile.FadeTile();
        //});
    }

    private void Jump()
    {
        var position = _lastPosition;

        var distance = _targetPos.z - _currentTile.transform.position.z;

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

        _currentTime += 1 / _tileController.SecPerBeat / (distance - 1) * Time.deltaTime;
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
