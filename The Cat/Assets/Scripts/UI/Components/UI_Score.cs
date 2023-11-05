using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class UI_Score : MonoBehaviour
{
    [SerializeField] private TMP_Text m_scoreText;

    [SerializeField] private Image m_filledImage;

    [SerializeField] private Image[] m_stars;

    [Range(0, 255)]
    [SerializeField] private float m_unfilledStarAlpha;

    private TileController _tileController;

    private LevelScore _levelScore;

    private MovementController _movementController;

    private float _fieldDistance;

    [Inject]
    public void Construct(TileController TileController, LevelScore levelScore, MovementController movementController)
    {
        _tileController = TileController;

        _levelScore = levelScore;

        _movementController = movementController;
    }

    private void Start()
    {
        _levelScore.ScoreChanged += OnScoreChanged;

        _fieldDistance = _tileController.FieldDistance;

        foreach (var star in m_stars)
        {
            star.color = new Color(1, 1, 1, m_unfilledStarAlpha / 255);
        }
    }

    private void OnDestroy()
    {
        _levelScore.ScoreChanged -= OnScoreChanged;
    }

    private void Update()
    {
        float currentStep = _movementController.transform.position.z / _fieldDistance;

        m_filledImage.fillAmount = currentStep;

        for (int i = 0; i < m_stars.Length; i++)
        {
            if (currentStep >= (float)(i + 1) / m_stars.Length)
            {
                m_stars[i].DOColor(new Color(1, 1, 1, 1), 0.3f);
            }
        }
    }

    private void OnScoreChanged(int score)
    {
        m_scoreText.text = score.ToString();
    }
}
