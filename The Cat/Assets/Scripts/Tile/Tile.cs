using DG.Tweening;
using System.Collections;
using UnityEngine;

public enum TileType
{
    Static,
    Long,
    Move,
}

public abstract class Tile : MonoBehaviour
{
    [SerializeField] protected TileType m_tileType;
    public TileType TileType => m_tileType;

    protected BoxCollider _collider;
    public Vector3 JumpPosition { get; protected set; }

    protected Vector3 _startPosition;

    protected MeshRenderer[] _meshes;

    private Tween _tween;

    [HideInInspector]
    public bool IsMoved = false;

    protected void Awake()
    {
        _startPosition = transform.position;

        _collider = GetComponent<BoxCollider>();

        _meshes = GetComponentsInChildren<MeshRenderer>();

        SetJumpPosition();
    }

    private void OnDestroy()
    {
        _tween.Kill();
    }

    public abstract void SetJumpPosition();

    public void FadeTile()
    {
        _collider.enabled = false;

        StartCoroutine(ChangeColorAlpha(1, 0));

        _tween = transform.DOPunchPosition(new Vector3(0f, 0.2f, 0f), 0.3f)
                 .SetEase(Ease.InOutQuad);
    }

    public void Hide()
    {
        IsMoved = true;

        Vector3 hidePosition = SetTileHidePosition();

        StartCoroutine(MoveToHidePosition(hidePosition, 0f));
    }

    public void Show()
    {
        IsMoved = false;

        StartCoroutine(SmoothMove(_startPosition, 1f, 1f));
    }

    private IEnumerator MoveToHidePosition(Vector3 position, float alpha)
    {
        foreach (var mesh in _meshes)
        {
            mesh.material.color = new Color(mesh.material.color.r, mesh.material.color.g, mesh.material.color.b, alpha);

            yield return null;
        }

        transform.position = position;
    }

    private IEnumerator SmoothMove(Vector3 position, float alpha, float duration)
    {
        float elapsedTime = 0f;

        float moveDuration = duration;

        while (elapsedTime < moveDuration)
        {
            float t = Mathf.Clamp01(elapsedTime / moveDuration);

            transform.position = Vector3.Lerp(transform.position, position, t);

            StartCoroutine(ChangeColorAlpha(duration, alpha));

            elapsedTime += Time.deltaTime;

            yield return null;
        }

        transform.position = position;
    }

    private IEnumerator ChangeColorAlpha(float duration, float alpha)
    {
        float elapsedTime = 0f;

        float moveDuration = duration;

        while (elapsedTime < moveDuration)
        {
            float t = Mathf.Clamp01(elapsedTime / moveDuration);

            foreach (var mesh in _meshes)
            {
                mesh.material.color = Color.Lerp(mesh.material.color, new Color(mesh.material.color.r, mesh.material.color.g, mesh.material.color.b, alpha), t);
            }

            elapsedTime += Time.deltaTime;

            yield return null;
        }
    }

    private Vector3 SetTileHidePosition()
    {
        Vector2 newPos = new Vector2(Random.Range(-2, 2), 4f);

        var hidePosition = new Vector3(newPos.x, newPos.y, transform.position.z);

        return hidePosition;
    }
}
