using UnityEngine;
using Zenject;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float m_offset = 0;

    MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    void Update()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, _cat.transform.position.z + m_offset);
    }
}
