using UnityEngine;

public class LongTile : Tile
{
    [SerializeField] private Transform m_jumpPosition;

    public override void SetJumpPosition()
    {
        JumpPosition = m_jumpPosition.position;
    }
}
