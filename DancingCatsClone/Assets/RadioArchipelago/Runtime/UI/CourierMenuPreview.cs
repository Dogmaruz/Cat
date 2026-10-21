using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.UI
{
    /// <summary>A self-contained preview rig. Its camera and texture live with its view.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CourierMenuPreview : MonoBehaviour
    {
        public static Vector2 LandingPadAnchor => new Vector2(.534925f, .492235f);
        private const float BodyHeight = 2.1f, ViewHalfHeight = 2.6f, FootViewportY = .12f, ImageWidthFraction = .86f;
        private CourierSelection selection;
        private UnityEngine.UI.RawImage image;
        private GameObject rig, actor;
        private Camera previewCamera;
        private GameObject lighting;
        private RenderTexture texture;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool initialized;
        private RectTransform background, contactShadow;
        private Transform leftFoot, rightFoot;
        private Vector3 leftSole, rightSole;

        public RenderTexture Texture => texture;
        public Camera PreviewCamera => previewCamera;

        public void Initialize(CourierSelection courierSelection, RectTransform backgroundRect, RectTransform shadowRect)
        {
            if (initialized) throw new InvalidOperationException("The courier preview is already initialized.");
            initialized = true;
            selection = courierSelection ?? throw new ArgumentNullException(nameof(courierSelection));
            background = backgroundRect != null ? backgroundRect : throw new ArgumentNullException(nameof(backgroundRect));
            contactShadow = shadowRect;
            image = GetComponent<UnityEngine.UI.RawImage>();
            if (image == null) throw new InvalidOperationException("Courier preview requires its own RawImage.");
            rig = new GameObject("CourierPreviewRig");
            // A Canvas changes scale after layout. Its scale must not reach a world-space camera.
            SceneManager.MoveGameObjectToScene(rig, gameObject.scene);
            // World-space preview objects are isolated from gameplay by both position and layer.
            rig.transform.position = new Vector3(1000, 1000, 1000);
            var cameraObject = new GameObject("CourierPreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = ViewHalfHeight;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << 12;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 25;
            cameraObject.transform.localPosition = new Vector3(0, ViewHalfHeight * (1 - 2 * FootViewportY), 8);
            cameraObject.transform.localRotation = Quaternion.Euler(0,180,0);
            texture = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) {name="RadioCourierPreview", antiAliasing=2};
            texture.Create();
            previewCamera.targetTexture = texture;
            image.texture = texture;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = new Vector2(.5f, FootViewportY);
            UpdatePresentationRect();
            selection.Changed += Rebuild;
            Rebuild();
            previewCamera.enabled = isActiveAndEnabled;
        }

        private void Rebuild()
        {
            if (actor != null) { actor.SetActive(false); Destroy(actor); }
            var definition = selection.SelectedDefinition;
            if (definition == null || definition.prefab == null) return;
            actor = Instantiate(definition.prefab, rig.transform, false);
            actor.name = "Preview_" + definition.id;
            actor.transform.localRotation = Quaternion.Euler(0,-24,0);
            foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 12;
            foreach (var source in actor.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true; body.detectCollisions=false; }
            var renderers = actor.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var motion = actor.GetComponentInChildren<CourierMotion>();
            if (motion != null) motion.ResetMotion();
            Bounds bodyBounds = CourierVisualBounds.MeasureBody(renderers);
            actor.transform.localScale *= BodyHeight / Mathf.Max(.01f, bodyBounds.size.y);
            bodyBounds = CourierVisualBounds.MeasureBody(renderers);
            Transform footL = null, footR = null;
            foreach (var node in actor.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "foot.L") footL = node;
                else if (node.name == "foot.R") footR = node;
            }
            var feet = footL != null && footR != null ? (footL.position + footR.position) * .5f : bodyBounds.center;
            actor.transform.position += rig.transform.position - new Vector3(feet.x, bodyBounds.min.y, feet.z);
            leftFoot = footL; rightFoot = footR;
            if (leftFoot != null && rightFoot != null)
            {
                var leftContact = leftFoot.position; leftContact.y = rig.transform.position.y;
                var rightContact = rightFoot.position; rightContact.y = rig.transform.position.y;
                leftSole = leftFoot.InverseTransformPoint(leftContact);
                rightSole = rightFoot.InverseTransformPoint(rightContact);
            }
            restPosition = actor.transform.localPosition;
            restRotation = actor.transform.localRotation;
            if (lighting != null) { lighting.SetActive(false); Destroy(lighting); }
            lighting = new GameObject("Courier lighting"); lighting.transform.SetParent(rig.transform, false);
            lighting.AddComponent<CourierLightingRig>().Initialize(selection.Lighting, actor, Vector3.forward, 1 << 12);
        }

        private void Update()
        {
            if (actor == null) return;
            float t=Time.unscaledTime;
            actor.transform.localPosition = restPosition;
            actor.transform.localRotation = restRotation * Quaternion.Euler(0, Mathf.Sin(t*.55f)*5, Mathf.Sin(t*1.1f)*1.5f);
        }

        private void LateUpdate()
        {
            // Keep one foot on the landing pad while the existing idle pose shifts weight.
            // Two bone transforms replace any need to bake geometry in a gameplay frame.
            if (actor != null && leftFoot != null && rightFoot != null)
            {
                float lowest = Mathf.Min(leftFoot.TransformPoint(leftSole).y, rightFoot.TransformPoint(rightSole).y);
                var position = actor.transform.position;
                position.y += rig.transform.position.y - lowest;
                actor.transform.position = position;
            }
            UpdatePresentationRect();
        }

        private void UpdatePresentationRect()
        {
            if (background == null || image == null) return;
            var rect = background.rect;
            var anchor = LandingPadAnchor;
            var support = background.TransformPoint(new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, anchor.x), Mathf.Lerp(rect.yMin, rect.yMax, anchor.y), 0));
            float size = rect.width * ImageWidthFraction;
            var dimensions = new Vector2(size, size);
            if (image.rectTransform.sizeDelta != dimensions) image.rectTransform.sizeDelta = dimensions;
            if (image.rectTransform.position != support) image.rectTransform.position = support;
            if (contactShadow != null)
            {
                var shadowSize = new Vector2(rect.width * .125f, rect.width * .023f);
                if (contactShadow.position != support) contactShadow.position = support;
                if (contactShadow.sizeDelta != shadowSize) contactShadow.sizeDelta = shadowSize;
            }
        }

        private void OnEnable()
        {
            if(rig != null) rig.SetActive(true);
            if(previewCamera != null) previewCamera.enabled=true;
        }
        private void OnDisable()
        {
            if(rig != null) rig.SetActive(false);
            if(previewCamera != null) previewCamera.enabled=false;
        }
        private void OnDestroy()
        {
            if(selection != null) selection.Changed -= Rebuild;
            if(previewCamera != null) previewCamera.targetTexture=null;
            if(texture != null) { texture.Release(); Destroy(texture); }
            if(rig != null) Destroy(rig);
        }
    }
}
