using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class UI_Score : MonoBehaviour
{
    [SerializeField] private TMP_Text m_scoreText;

    [SerializeField] private Image m_filledImage;

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

    }

    private void OnDestroy()
    {
        _levelScore.ScoreChanged -= OnScoreChanged;
    }

    private void Update()
    {
        m_filledImage.fillAmount = _movementController.transform.position.z / _fieldDistance;
    }

    private void OnScoreChanged(int score)
    {
        m_scoreText.text = score.ToString();
    }
}
