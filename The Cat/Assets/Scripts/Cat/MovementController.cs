using DG.Tweening;
using UnityEngine;
using Zenject;
using static UnityEngine.GraphicsBuffer;

public struct PlayerInputs
{
    public float MouseAxisRight;
}

public class MovementController : MonoBehaviour
{
    [SerializeField] private Transform m_cat;
    public Transform Cat => m_cat;

    [SerializeField] private Transform m_checkPosition;

    [SerializeField] private float m_sensitivity = 0.1f;

    [SerializeField] private LayerMask m_layerMask;

    [SerializeField] private GameObject m_restartButton;

    private Tween _tween;

    private Tween _tweenMoveX;

    private Sequence _sequence;

    private bool _isTouch;

    private BoxCollider _collider;

    private TileController _platformController;

    private PlayerInputs _playerInputs;

    private PlayerInputAction _playerInputAction;

    private float _maxSpeedAxisRight = 50f;

    private BackgroundSoundPlayer _backgroundSoundPlayer;

    private SoundPlayer _soundPlayer;

    private bool _isJump;

    private Tile _currentTile;

    private Vector3 _targetPos;

    //private Rigidbody rigidBody;

    [Inject]
    public void Construct(TileController platformController, BackgroundSoundPlayer backgroundSoundPlayer, SoundPlayer soundPlayer)
    {
        _platformController = platformController;

        _backgroundSoundPlayer = backgroundSoundPlayer;

        _soundPlayer = soundPlayer;
    }

    private void Awake()
    {
        //rigidBody = GetComponentInChildren<Rigidbody>();

        _collider = m_cat.GetComponent<BoxCollider>();

        _collider.enabled = false;

        _playerInputs = new PlayerInputs();

        _playerInputAction = new PlayerInputAction();

        _playerInputAction.Player.Enable();

        enabled = false;
    }

    private void OnDestroy()
    {
        _tween.Kill();

        _tweenMoveX.Kill();

        _sequence.Kill();
    }

    private void Update()
    {
        MouseInput();

        UpdatePosition(_playerInputs.MouseAxisRight);

        if (_isJump == false)
        {
            CheckCollisions();
        }

        _playerInputs.MouseAxisRight = 0;
    }

    public void UpdatePosition(float mouseX)
    {
        float bounds = 2f;

        Vector3 newPosition = m_cat.transform.position + new Vector3(mouseX * m_sensitivity, 0f, 0f);

        //rigidBody.velocity = new Vector3(mouseX - transform.position.x, rigidBody.velocity.y, 0);

        _tweenMoveX = m_cat.transform.DOMoveX(newPosition.x, 0.2f);

        if (m_cat.transform.position.x >= bounds)
        {
            _tweenMoveX = m_cat.transform.DOMoveX(bounds, 0.01f);
        }

        if (m_cat.transform.position.x <= -bounds)
        {
            _tweenMoveX = m_cat.transform.DOMoveX(-bounds, 0.01f);
        }
    }

    public void ActivateMovement()
    {
        _collider.enabled = true;

        enabled = true;
    }

    public void Move(Tile currentTile)
    {
        var target = _platformController.NextTile();

        if (target.TileType == TileType.Static)
        {
            _targetPos = target.transform.position;
        }

        if (target.TileType == TileType.Long)
        {
            _targetPos = target.GetComponent<LongPlaform>().JumpPosition.position;
        }

        if (target.TileType == TileType.Move)
        {
            _targetPos = target.transform.position;
        }

        if (currentTile.TileType == TileType.Static)
        {
            Jump(currentTile.transform.position.z);
        }

        if (currentTile.TileType == TileType.Long)
        {
            _sequence.Kill();

            float offset = 1;

            float angleY = 360;

            var distance = currentTile.transform.localScale.z - offset;

            var newPos = m_cat.transform.position.z + distance;

            _sequence = DOTween.Sequence()
           .Append(m_cat.transform.DOMoveZ(newPos, (distance + offset) * _platformController.SecPerBeat))
           .Join(m_cat.transform.DORotate(new Vector3(0, angleY, 0), (distance + offset) * _platformController.SecPerBeat, RotateMode.FastBeyond360))
           .SetEase(Ease.Linear)
           .OnComplete(() =>
           {
               Jump(newPos);

               currentTile.FadeTile();
           });
        }

        if (currentTile.TileType == TileType.Move)
        {
            _sequence.Kill();

            float offset = 1;

            float angleY = 360;

            MovePlatform movePlatform = currentTile.GetComponent<MovePlatform>();

            var distance = Vector3.Distance(currentTile.transform.position, movePlatform.EndPosition.position);

            var newPos = m_cat.transform.position.z + distance;

            currentTile.transform.SetParent(m_checkPosition);

            currentTile.transform.localPosition = new Vector3(0, currentTile.transform.localPosition.y, currentTile.transform.localPosition.z);

            _sequence = DOTween.Sequence()
           .Append(m_cat.transform.DOMoveZ(newPos, (distance + offset) * _platformController.SecPerBeat))
           .Join(m_cat.transform.DORotate(new Vector3(0, angleY, 0), (distance + offset) * _platformController.SecPerBeat, RotateMode.FastBeyond360))
           .SetEase(Ease.Linear)
           .OnComplete(() =>
           {
               currentTile.transform.SetParent(movePlatform.Parent);

               Jump(newPos);

               currentTile.FadeTile();
           });
        }
    }


    private void Jump(float currentTilePositionZ)
    {
        float maxHight = 6f;

        float _maxPower = 3.2f;

        float offset = 1f;

        int jumpCount = 1;

        _isJump = true;

        var result = _targetPos.z - currentTilePositionZ - offset;

        var power = result;

        _tween.Kill();

        if (result >= maxHight)
        {
            power = _maxPower;
        }

        var newPos = new Vector3(m_cat.transform.position.x, m_cat.transform.position.y, _targetPos.z);

        _tween = m_cat.transform.DOJump(newPos, power, jumpCount, result * _platformController.SecPerBeat)
                                .SetEase(Ease.Linear)
                                .OnComplete(OnComplete);
    }

    private void OnComplete()
    {
        _tween.Kill();

        _isJump = false;
    }

    private void CheckCollisions()
    {
        float maxDistance = 5f;

        float halfIndex = 0.5f;

        float fallYPosition = -5f;

        Vector3 origin = m_checkPosition.position;

        Vector3 size = GetComponentInChildren<Rigidbody>().GetComponent<Transform>().localScale * halfIndex;

        Vector3 direction = Vector3.down;

        RaycastHit hitInfo;

        if (Physics.BoxCast(origin, size* halfIndex, direction, out hitInfo, transform.rotation, maxDistance, m_layerMask))
        {
            Collider hitCollider = hitInfo.collider;

            if (hitCollider != null)
            {
                var killZone = hitCollider.gameObject.GetComponent<KillZone>();

                if (killZone != null)
                {
                    if (_currentTile.TileType == TileType.Move)
                    {
                        _currentTile.transform.SetParent(_currentTile.GetComponent<MovePlatform>().Parent);
                    }

                    enabled = false;

                    //Здесь реклама.

                    _tween.Kill();

                    _tweenMoveX.Kill();

                    _sequence.Kill();

                    _tween = m_cat.transform.DOMoveY(fallYPosition, 0.5f);

                    _backgroundSoundPlayer.Stop();

                    _soundPlayer.Play(Sound.Fall, 1f);

                    m_restartButton.SetActive(true);

                    Cursor.visible = true;

                    Cursor.lockState = CursorLockMode.None;
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
    }

    private void MouseInput()
    {
        if (!_isTouch)
        {
            float xValue = _playerInputAction.Player.MouseAxisX.ReadValue<Vector2>().x;

            _playerInputs.MouseAxisRight = xValue >= 0 ? Mathf.Clamp(xValue, 0, _maxSpeedAxisRight) : Mathf.Clamp(xValue, -_maxSpeedAxisRight, 0);
        }
    }

    public void SetMouseAxisRight(float value)
    {
        _isTouch = true;

        float xValue = value;

        _playerInputs.MouseAxisRight = xValue >= 0 ? Mathf.Clamp(xValue, 0, _maxSpeedAxisRight) : Mathf.Clamp(xValue, -_maxSpeedAxisRight, 0);
    }
}
