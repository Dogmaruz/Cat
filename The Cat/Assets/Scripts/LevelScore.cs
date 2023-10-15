using System;
using UnityEngine;

public class LevelScore : MonoBehaviour
{
    public Action<int> ScoreChanged;

    private int _score;
    public int Score => _score;

    public void AddScore(int value)
    {
        _score = Mathf.Clamp(_score + value, 0, int.MaxValue);

        ScoreChanged?.Invoke(_score);
    }
}
