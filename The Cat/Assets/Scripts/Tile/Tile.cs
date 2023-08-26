using DG.Tweening;
using UnityEngine;

public class Tile : MonoBehaviour
{
    private Sequence _sequence;

    private BoxCollider _collider;

    private void Start()
    {
        _collider = GetComponent<BoxCollider>();
    }

    private void OnDestroy()
    {
        _sequence.Kill();
    }

    private void OnTriggerEnter(Collider other)
    {
        var cat = other.transform.root.GetComponent<MovementController>();

        if (cat != null)
        {
            cat.Move(this);

            _collider.enabled = false;

            _sequence.Kill();

            Material material = GetComponent<MeshRenderer>().material;

            _sequence = DOTween.Sequence()
            .Append(transform.DOPunchPosition(new Vector3(0f, 0.2f, 0f), 0.3f))
            .Append(material.DOFade(0f, 0.5f))
            .SetEase(Ease.InOutQuad)
            .OnComplete(OnDissolveComplete); 
        }
    }

    private void OnDissolveComplete()
    {
        //Destroy(gameObject);
    }
}
