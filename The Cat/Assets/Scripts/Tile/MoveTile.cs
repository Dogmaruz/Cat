using DG.Tweening;
using UnityEngine;

public class MoveTile : MonoBehaviour
{
    [SerializeField] private float offset;

    private Sequence _sequence;

    private void Start()
    {
        var startPos = transform.position;

        _sequence = DOTween.Sequence()
                .Append(transform.DOMoveX(startPos.x - offset, 3f))
                .Append(transform.DOMoveX(startPos.x, 3f))
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .SetLoops(-1);
    }

    private void OnDestroy()
    {
        _sequence.Kill();
    }
}
