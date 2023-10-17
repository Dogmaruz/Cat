using UnityEngine;

public interface IEmerging
{
    public bool IsMoved { get; set; }

    public void Hide();

    public void Show();

    public Transform GetTransform();
}
