using UnityEngine;

public class MovePlatform : MonoBehaviour
{
    [SerializeField] private Transform m_parent;
    public Transform Parent => m_parent;

    [SerializeField] private Transform m_endPosition;
    public Transform EndPosition => m_endPosition;
}
