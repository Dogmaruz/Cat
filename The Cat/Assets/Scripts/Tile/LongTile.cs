using UnityEngine;

public class LongTile : Tile
{
    [SerializeField] private Transform m_jumpPosition;

    [SerializeField] private Transform m_endJumpPosition;

    public override void SetJumpPosition()
    {
        JumpPosition = m_jumpPosition.position;
    }

    public override void SetFinishPosition()
    {
        FinishPosition = m_endJumpPosition.position;
    }
}
