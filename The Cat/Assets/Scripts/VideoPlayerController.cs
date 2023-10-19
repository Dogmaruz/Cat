using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPlayerController : MonoBehaviour
{
    public RawImage rawImage;

    public VideoPlayer videoPlayer;

    public static VideoPlayerController Instance;

    private bool isPaused = false;

    void Start()
    {
       Instance = this;

        videoPlayer.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);

        rawImage.texture = videoPlayer.targetTexture;
    }

    public void Play()
    {
        videoPlayer.Play();
    }

    private void Update()
    {
        if (isPaused)
        {
            Time.timeScale = 0f;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            if (isPaused)
            {
                StepFrame();
            }
        }
    }

    public void PauseGame()
    {
        Time.timeScale = 0; // Замораживаем время
        isPaused = true;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1; // Возобновляем время
        isPaused = false;
    }

    public void StepFrame()
    {
        if (isPaused)
        {
            Time.timeScale = 1; // Установите время на 1
        }
    }
}

