using DG.Tweening;
using UnityEngine;

public enum TileType
{
    Static,
    Long,
    Move,
}

public abstract class Tile : Emerging
{
    [SerializeField] protected TileType m_tileType;
    public TileType TileType => m_tileType;

    protected BoxCollider _collider;
    public BoxCollider Collider => _collider;
    public Vector3 JumpPosition { get; protected set; }
    public Vector3 FinishPosition { get; protected set; }

    private Tween _tween;

    public float Distance { get; set; }

    protected override void Awake()
    {
        base.Awake();

        _collider = GetComponentInChildren<BoxCollider>();

        SetJumpPosition();

        SetFinishPosition();

        Distance = FinishPosition.z - JumpPosition.z;
    }

    public abstract void SetJumpPosition();
    public abstract void SetFinishPosition();

    public void FadeTile()
    {
        _collider.enabled = false;

        StartCoroutine(ChangeColorAlpha(1, 0));

        ShakeTile();
    }

    private void ShakeTile()
    {
        _tween = transform.DOPunchPosition(new Vector3(0f, 0.2f, 0f), 0.3f)
                         .SetEase(Ease.InOutQuad);
    }

    private void OnDestroy()
    {
        _tween.Kill();
    }
}
