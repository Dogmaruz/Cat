using UnityEngine;

public class MoveTile : Tile
{
    [SerializeField] private Transform m_parentTransform;
    public Transform ParentTransform => m_parentTransform;

    [SerializeField] private Transform m_endPosition;

    public override void SetJumpPosition()
    {
        JumpPosition = transform.position;
    }

    public override void SetFinishPosition()
    {
        FinishPosition = m_endPosition.position;
    }
}
