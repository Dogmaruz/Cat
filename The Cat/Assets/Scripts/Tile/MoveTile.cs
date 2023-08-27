using DG.Tweening;
using UnityEngine;

public class MoveTile : MonoBehaviour
{
    [SerializeField] private float offset;

    private Vector3 startPoint;

    private Vector3 endPoint;

    public float duration = 2f;

    private bool isMovingForward = true;

    private Tween _tween;

    private void Start()
    {
        startPoint = transform.position;

        endPoint = new Vector3 (startPoint.x - offset, startPoint.y, startPoint.z);

        MoveToNextWaypoint();
    }

    private void MoveToNextWaypoint()
    {
        Vector3 targetPosition = isMovingForward ? endPoint : startPoint;

        _tween = transform.DOMove(targetPosition, duration).OnComplete(OnWaypointReached);
    }

    private void OnWaypointReached()
    {
        isMovingForward = !isMovingForward;

        MoveToNextWaypoint();
    }

    private void OnDestroy()
    {
        _tween.Kill();
    }
}
