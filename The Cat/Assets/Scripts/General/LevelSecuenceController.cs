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

    private MovementController _cat;

    private SceneLoader _sceneLoader;

    [Inject]
    public void Construct(BackgroundSoundPlayer backgroundSoundPlayer, SoundPlayer soundPlayer, MovementController cat)
    {
        _backgroundSoundPlayer = backgroundSoundPlayer;

        _soundPlayer = soundPlayer;

        _cat = cat;
    }

    private void Awake()
    {
        _sceneLoader = GetComponent<SceneLoader>();

        ChangeCursorVisible(true);

        m_startButton.GetComponent<Button>().onClick.AddListener(StartGame);

        m_restartButton.GetComponent<Button>().onClick.AddListener(_sceneLoader.Restart);
    }

    public void StartGame()
    {
        m_backgroundSceneClip.PlayBackground();

        _cat.ActivateMovement();

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
