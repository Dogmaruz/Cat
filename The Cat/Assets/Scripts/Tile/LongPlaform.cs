using UnityEngine;

public class LongPlaform : MonoBehaviour
{
    [SerializeField] private Transform m_jumpPosition;
    public Transform JumpPosition => m_jumpPosition;
}
