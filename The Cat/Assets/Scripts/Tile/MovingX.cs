using DG.Tweening;
using UnityEngine;

public class MovingX : MonoBehaviour
{
    [SerializeField] private float offset;

    public float duration = 2f;

    private Tween _tween;

    private void Start()
    {
        _tween = transform.DOMoveX(offset, duration)
                          .SetLoops(-1, LoopType.Yoyo)
                          .SetEase(Ease.Linear);
    }

    private void OnDestroy()
    {
        _tween.Kill();
    }
}
