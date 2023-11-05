using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class UI_Score : MonoBehaviour
{
    [SerializeField] private TMP_Text m_scoreText;

    [SerializeField] private Image m_filledImage;

    private UI_ScoreStar[] _stars;

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

        _stars = GetComponentsInChildren<UI_ScoreStar>();
    }

    private void OnDestroy()
    {
        _levelScore.ScoreChanged -= OnScoreChanged;
    }

    private void Update()
    {
        float currentStep = _movementController.transform.position.z / _fieldDistance;

        m_filledImage.fillAmount = currentStep;

        for (int i = 0; i < _stars.Length; i++)
        {
            if (currentStep >= (float)(i + 1) / _stars.Length)
            {
                _stars[i].Activate();
            }
        }
    }

    private void OnScoreChanged(int score)
    {
        m_scoreText.text = score.ToString();
    }
}
