using System.Collections;
using UnityEngine;

public class Emerging : MonoBehaviour, IEmerging
{
    [HideInInspector]
    public bool IsMoved { get; set; }

    protected MeshRenderer[] _meshes;

    protected Vector3 _startPosition;
    public Vector3 StartPosition => _startPosition;

    protected virtual void Awake()
    {
        _meshes = GetComponentsInChildren<MeshRenderer>();

        SetStartPosition(transform.position);
    }

    public void SetStartPosition(Vector3 position)
    {
        _startPosition = position;
    }

    public void Hide()
    {
        IsMoved = true;

        Vector3 hidePosition = SetTileHidePosition();

        MoveToHidePosition(hidePosition, 0f);
    }

    public void Show()
    {
        IsMoved = false;

        StartCoroutine(SmoothMove(_startPosition, 1f, 1f));

        StartCoroutine(ChangeColorAlpha(1, 1));
    }

    private Vector3 SetTileHidePosition()
    {
        Vector2 newPos = new Vector2(Random.Range(-2, 2), 4f);

        var hidePosition = new Vector3(newPos.x, newPos.y, transform.position.z);

        return hidePosition;
    }

    private void MoveToHidePosition(Vector3 position, float alpha)
    {
        foreach (var mesh in _meshes)
        {
            mesh.material.color = new Color(mesh.material.color.r, mesh.material.color.g, mesh.material.color.b, alpha);
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

            elapsedTime += Time.deltaTime;

            yield return null;
        }

        transform.position = position;
    }

    public IEnumerator ChangeColorAlpha(float duration, float alpha)
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

    public Transform GetTransform()
    {
        return transform;
    }
}
