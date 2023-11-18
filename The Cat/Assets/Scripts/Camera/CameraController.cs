using UnityEngine;
using Zenject;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Vector3 m_offset;

    [SerializeField] private float m_followSpeed = 10f;

    private MovementController _movementController;

    private Vector3 _targetPosition;

    private Vector3 _currentPosition;

    [Inject]
    public void Construct(MovementController movementController)
    {
        _movementController = movementController;
    }

    void LateUpdate()
    {
        _currentPosition = transform.position;

        _targetPosition = _movementController.PlayerTransform.transform.position + m_offset;

        _targetPosition.y = m_offset.y;

        transform.position = Vector3.Lerp(_currentPosition, _targetPosition, m_followSpeed * Time.deltaTime);
    }
}
