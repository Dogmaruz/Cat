using UnityEngine;

public class ParentPointControl : MonoBehaviour
{
    [SerializeField] private Transform _playerTransform;

    void Update()
    {
        transform.position = _playerTransform.position;
    }
}
