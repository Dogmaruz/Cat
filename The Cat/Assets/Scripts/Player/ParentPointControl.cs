using UnityEngine;

public class ParentPointControl : MonoBehaviour
{
    [SerializeField] private Transform _playerTransform;

    void LateUpdate()
    {
        transform.position = _playerTransform.position;
    }
}
