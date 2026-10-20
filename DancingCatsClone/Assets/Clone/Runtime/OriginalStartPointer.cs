using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DancingCats.Clone
{
    /// <summary>Recovered StartBtn invoked PlayButtonClick from TouchDown.OnPointerDown.</summary>
    public sealed class OriginalStartPointer : MonoBehaviour, IPointerDownHandler
    {
        private Action pressed;
        public void Bind(Action action) { pressed = action; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed?.Invoke();
        }
    }
}
