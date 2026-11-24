using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RadioArchipelago.UI
{
    /// <summary>Rotates only the workshop portrait through the owning uGUI pointer gesture.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CourierPreviewOrbitGesture : MonoBehaviour, IPointerDownHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerUpHandler, ICancelHandler
    {
        private const float DegreesAcrossPreview = 360;
        private CourierMenuPreview preview;
        private RectTransform viewport;
        private Camera gestureCamera;
        private Vector2 previousScreenPosition;
        private int activePointerId;
        private bool ownsPointer, dragging;

        public void Initialize(CourierMenuPreview target)
        {
            preview = target != null ? target : throw new ArgumentNullException(nameof(target));
            viewport = (RectTransform)transform;
            ResetGesture();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (ownsPointer || eventData.button != PointerEventData.InputButton.Left ||
                preview == null || !preview.isActiveAndEnabled) return;
            ownsPointer = true;
            activePointerId = eventData.pointerId;
            previousScreenPosition = eventData.position;
            gestureCamera = eventData.pressEventCamera;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (Owns(eventData)) eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Owns(eventData)) return;
            dragging = true;
            // The EventSystem has now crossed its drag threshold; clicks never rotate.
            previousScreenPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || !Owns(eventData) || preview == null || !preview.isActiveAndEnabled) return;
            if (viewport.rect.width > .01f &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, previousScreenPosition,
                    gestureCamera, out var previous) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                    gestureCamera, out var current))
            {
                // Both points are in the same local UI units, so CanvasScaler and
                // display resolution cannot alter the angle for an equal-width drag.
                preview.RotatePreview((current.x - previous.x) / viewport.rect.width * DegreesAcrossPreview);
            }
            previousScreenPosition = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Owns(eventData)) ResetGesture();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Owns(eventData)) ResetGesture();
        }

        public void OnCancel(BaseEventData eventData) => ResetGesture();
        private void OnDisable() => ResetGesture();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetGesture(); }

        private bool Owns(PointerEventData eventData) => ownsPointer &&
            eventData.pointerId == activePointerId && eventData.button == PointerEventData.InputButton.Left;

        private void ResetGesture()
        {
            ownsPointer = false;
            dragging = false;
            activePointerId = 0;
            gestureCamera = null;
            previousScreenPosition = Vector2.zero;
        }
    }
}
