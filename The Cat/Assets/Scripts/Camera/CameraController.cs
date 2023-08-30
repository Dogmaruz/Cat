using UnityEngine;
using Zenject;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float m_offsetZ = 0;

    [SerializeField] private float m_followSpeedX = 5f;

    [SerializeField] private float m_followSpeedZ = 10f;

    private MovementController _movementController;

    private Vector3 _targetPosition;

    [Inject]
    public void Construct(MovementController movementController)
    {
        _movementController = movementController;
    }

    void FixedUpdate()
    {
        Vector3 currentPosition = transform.position;

        float targetX = _movementController.PlayerTransform.transform.position.x;

        float targetZ = _movementController.PlayerTransform.transform.position.z + m_offsetZ;

        _targetPosition.x = Mathf.Lerp(_targetPosition.x, targetX, m_followSpeedX * Time.fixedDeltaTime);

        _targetPosition.y = currentPosition.y;

        _targetPosition.z = targetZ;

        transform.position = Vector3.Lerp(currentPosition, _targetPosition, m_followSpeedZ);
    }
}
