using UnityEngine;
using Zenject;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float m_offsetZ = 0;

    [SerializeField] private float m_followSpeedX = 5f;

    [SerializeField] private float m_followSpeedZ = 10f;

    private MovementController _cat;

    private Vector3 _targetPosition;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    void Update()
    {
        Vector3 currentPosition = transform.position;

        float targetX = _cat.Cat.transform.position.x;

        float targetZ = _cat.Cat.transform.position.z + m_offsetZ;

        _targetPosition.x = Mathf.Lerp(_targetPosition.x, targetX, m_followSpeedX * Time.deltaTime);

        _targetPosition.y = currentPosition.y;

        _targetPosition.z = targetZ;

        transform.position = Vector3.Lerp(currentPosition, _targetPosition, m_followSpeedZ * Time.deltaTime);
    }
}
