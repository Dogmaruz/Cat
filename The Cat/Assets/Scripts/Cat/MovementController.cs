using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

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

    private Tween _tweenMove;

    private bool _isTouch;

    private BoxCollider _collider;

    TileController _platformController;

    protected PlayerInputs _playerInputs;

    private PlayerInputAction _playerInputAction;
    public PlayerInputAction PlayerInputAction { get => _playerInputAction; set => _playerInputAction = value; }

    private float _maxSpeedLookAxis = 50;

    private float _maxPower = 3.2f;

    private BackgroundSoundPlayer _backgroundSoundPlayer;

    private SoundPlayer _soundPlayer;

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

        _tweenMove.Kill();
    }

    private void Update()
    {
        //Премещение персонажа с клавиатуры и мыши
        MouseInput();

        UpdatePosition(_playerInputs.MouseAxisRight);

        _playerInputs.MouseAxisRight = 0;
    }

    public void UpdatePosition(float mouseX)
    {
        Vector3 newPosition = m_cat.transform.position + new Vector3(mouseX * m_sensitivity, 0f, 0f);

        //rigidBody.velocity = new Vector3(mouseX - transform.position.x, rigidBody.velocity.y, 0);

        _tweenMove = m_cat.transform.DOMoveX(newPosition.x, 0.2f);

        if (m_cat.transform.position.x >= 2)
        {
            _tweenMove = m_cat.transform.DOMoveX(2, 0.01f);
        }

        if (m_cat.transform.position.x <= -2)
        {
            _tweenMove = m_cat.transform.DOMoveX(-2, 0.01f);
        }
    }

    public void EnabledCollider()
    {
        _collider.enabled = true;

        enabled = true;
    }

    public void Move(Tile currentTile)
    {
        var target = _platformController.NextTile();
        var result = target.transform.position.z - currentTile.transform.position.z - 1;

        var power = result;

        _tween.Kill();

        if (result >= 6) 
        { 
            power = _maxPower;
        }
         

        var newPos = new Vector3(m_cat.transform.position.x, m_cat.transform.position.y, target.transform.position.z);

        _tween = m_cat.transform.DOJump(newPos, power, 1, result * _platformController.SecPerBeat)
                          .SetEase(Ease.Linear)
                          .OnComplete(OnComplete);
    }



    private void OnComplete()
    {
        Vector3 origin = m_checkPosition.position;

        Vector3 size = GetComponentInChildren<BoxCollider>().size * 0.5f;

        Vector3 direction = Vector3.down;

        float maxDistance = 4f; 

        RaycastHit hitInfo;

        if (Physics.BoxCast(origin, size * 0.5f, direction, out hitInfo, transform.rotation, maxDistance, m_layerMask))
        {
            Collider hitCollider = hitInfo.collider;

            if (hitCollider != null)
            {
                var killZone = hitCollider.gameObject.GetComponent<KillZone>();

                if (killZone != null)
                {
                    enabled = false;

                    //Здесь реклама.

                    _tween.Kill();

                    _tweenMove.Kill();

                    _tweenMove = m_cat.transform.DOMoveY(-5, 0.5f);

                    _backgroundSoundPlayer.Stop();

                    _soundPlayer.Play(Sound.Fall, 1f);

                    m_restartButton.SetActive(true);
                }
            }
        }
    }

    private void MouseInput()
    {
        if (!_isTouch)
        {
            float xValue = _playerInputAction.Player.MouseAxisX.ReadValue<Vector2>().x;

            _playerInputs.MouseAxisRight = xValue >= 0 ? Mathf.Clamp(xValue, 0, _maxSpeedLookAxis) : Mathf.Clamp(xValue, -_maxSpeedLookAxis, 0);
        }
    }

    public void SetMouseAxisRight(float value)
    {
        _isTouch = true;

        float xValue = value;

        _playerInputs.MouseAxisRight = xValue >= 0 ? Mathf.Clamp(xValue, 0, _maxSpeedLookAxis) : Mathf.Clamp(xValue, -_maxSpeedLookAxis, 0);
    }
}
