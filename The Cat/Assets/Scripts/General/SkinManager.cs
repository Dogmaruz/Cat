using UnityEngine;

public class SkinManager : MonoBehaviour
{
    [SerializeField] private Material m_material;

    public void ChangePlayerColor(Color color)
    {
        m_material.color = color;
    }
}
