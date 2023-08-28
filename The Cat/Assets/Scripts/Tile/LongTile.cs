using UnityEngine;

public class LongTile : Tile
{
    [SerializeField] private Transform m_jumpPosition;
    public Transform JumpPosition => m_jumpPosition;

    public override void SetBasePosition()
    {
        _basePosition = m_jumpPosition.position;
    }
}
