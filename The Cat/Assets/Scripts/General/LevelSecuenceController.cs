using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class LevelSecuenceController : MonoBehaviour
{
    [SerializeField] GameObject m_startButton;

    [SerializeField] private GameObject m_restartButton;

    [SerializeField] BackgroundSceneClip m_backgroundSceneClip;

    private BackgroundSoundPlayer _backgroundSoundPlayer;

    private SoundPlayer _soundPlayer;

    private MovementController _movementController;

    private SceneLoader _sceneLoader;

    [Inject]
    public void Construct(BackgroundSoundPlayer backgroundSoundPlayer, SoundPlayer soundPlayer, MovementController movementController)
    {
        _backgroundSoundPlayer = backgroundSoundPlayer;

        _soundPlayer = soundPlayer;

        _movementController = movementController;
    }

    private void Awake()
    {
        //Application.targetFrameRate = 60;

        _sceneLoader = GetComponent<SceneLoader>();

        ChangeCursorVisible(true);

        m_startButton.GetComponent<Button>().onClick.AddListener(StartGame);

        m_restartButton.GetComponent<Button>().onClick.AddListener(_sceneLoader.Restart);
    }

    public void StartGame()
    {
        m_backgroundSceneClip.PlayBackground();

        _movementController.SetMovementState(true);

        m_startButton.SetActive(false);

        ChangeCursorVisible(false);
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
}
