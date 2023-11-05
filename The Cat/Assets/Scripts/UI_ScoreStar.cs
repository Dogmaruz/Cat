using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UI_ScoreStar : MonoBehaviour
{
    [Range(0, 255)]
    [SerializeField] private float m_unreachedStarAlpha;

    [SerializeField] private ImpactEffect m_reachStarEffect;

    private Image _image;

    private bool _activated;

    private void Start()
    {
        _image = GetComponent<Image>();

        _image.color = new Color(1, 1, 1, m_unreachedStarAlpha / 255);

        _activated = false;
    }

    public void Activate()
    {
        if (_activated == true) return;

        _image.DOColor(new Color(1, 1, 1, 1), 0.3f);

        if (m_reachStarEffect != null)
        {
            var effect = Instantiate(m_reachStarEffect, transform);
        }

        _activated = true;
    }
}
