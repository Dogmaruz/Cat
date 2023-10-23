using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class LevelSecuenceController : MonoBehaviour
{
    [SerializeField] private HUD m_HUD;

    [SerializeField] private BackgroundSceneClip m_backgroundSceneClip;

    private GameObject m_startButton;

    private GameObject m_restartButton;

    private GameObject m_menuButton;

    private BackgroundSoundPlayer _backgroundSoundPlayer;

    private SoundPlayer _soundPlayer;

    private MovementController _movementController;

    private TileController _tileController;

    private SceneLoader _sceneLoader;

    [Inject]
    public void Construct(BackgroundSoundPlayer backgroundSoundPlayer, SoundPlayer soundPlayer, MovementController movementController, TileController tileController)
    {
        _backgroundSoundPlayer = backgroundSoundPlayer;

        _soundPlayer = soundPlayer;

        _movementController = movementController;

        _tileController = tileController;
    }

    private void Awake()
    {
        Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;

        _sceneLoader = GetComponent<SceneLoader>();

        ChangeCursorVisible(true);

        m_startButton = m_HUD.m_startButton;
        m_restartButton = m_HUD.m_restartButton;
        m_menuButton = m_HUD.m_menuButton;

        m_startButton.GetComponent<Button>().onClick.AddListener(StartGame);

        m_restartButton.GetComponent<Button>().onClick.AddListener(_sceneLoader.Restart);

        m_menuButton.GetComponent<Button>().onClick.AddListener(_sceneLoader.Restart);

        _tileController.LastTileReached += OnLastTileReached;
    }

    private void OnDestroy()
    {
        _tileController.LastTileReached -= OnLastTileReached;
    }

    public void StartGame()
    {
        m_backgroundSceneClip.PlayBackground();

        //VideoPlayerController.Instance.Play();

        _movementController.SetMovementState(true);

        m_startButton.SetActive(false);

        ChangeCursorVisible(false);
    }

    public void Win()
    {
        _backgroundSoundPlayer.Stop();

        _soundPlayer.Play(Sound.Fall, 1f);

        m_menuButton.SetActive(true);

        ChangeCursorVisible(true);
    }

    public void Lose()
    {
        _backgroundSoundPlayer.Stop();

        _soundPlayer.Play(Sound.Fall, 1f);

        m_restartButton.SetActive(true);

        ChangeCursorVisible(true);
    }

    public void ChangeCursorVisible(bool visible)
    {
        Cursor.visible = visible;

        Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void OnLastTileReached()
    {
        StartCoroutine(GetTimeToCollectCoins());
    }

    private IEnumerator GetTimeToCollectCoins()
    {
        while (_movementController.transform.position.z < _tileController.FieldDistance)
        {
            yield return null;
        }

        _movementController.SetMovementState(false);

        Win();
    }
}
