using DG.Tweening;
using UnityEngine;

public enum TileType
{
    Static,
    Long,
    Move,
}

public class Tile : MonoBehaviour
{
    [SerializeField] private TileType m_tileType;
    public TileType TileType => m_tileType;

    private Sequence _sequence;

    private BoxCollider _collider;

    private Vector3 _basePosition;
    public Vector3 BasePosition => _basePosition;

    [HideInInspector]
    public bool IsMoved = false; //‘лаг указывающий на изменени€ позиции при старте.

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();

        _basePosition = transform.position;
    }

    private void OnDestroy()
    {
        _sequence.Kill();
    }

    public void FadeTile()
    {
        _collider.enabled = false;

        _sequence.Kill();

        var meshRenders = GetComponentsInChildren<MeshRenderer>();

        foreach (var mesh in meshRenders)
        {
            _sequence = DOTween.Sequence()
           .Append(transform.DOPunchPosition(new Vector3(0f, 0.2f, 0f), 0.3f))
           .Append(mesh.material.DOFade(0f, 0.3f))
           .SetEase(Ease.InOutQuad)
           .OnComplete(OnDissolveComplete);
        }
    }

    private void OnDissolveComplete()
    {
        _sequence.Kill();

        //Destroy(gameObject);
    }
}
