public class ShortTile : Tile
{
    public override void SetFinishPosition()
    {
        FinishPosition = transform.position;
    }

    public override void SetJumpPosition()
    {
        JumpPosition = transform.position;
    }
}
