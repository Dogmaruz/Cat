using UnityEngine;

public class MoveTile : Tile
{
    [SerializeField] private Transform m_parent;
    public Transform Parent => m_parent;

    [SerializeField] private Transform m_endPosition;
    public Transform EndPosition => m_endPosition;

    public override void SetBasePosition()
    {
        _basePosition = transform.position;
    }
}
