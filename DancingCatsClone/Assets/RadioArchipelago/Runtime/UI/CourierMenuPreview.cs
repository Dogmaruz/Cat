using System;
using UnityEngine;

namespace RadioArchipelago.UI
{
    /// <summary>A self-contained preview rig. Its camera and texture live with its view.</summary>
    [DisallowMultipleComponent]
    public sealed class CourierMenuPreview : MonoBehaviour
    {
        private CourierSelection selection;
        private UnityEngine.UI.RawImage image;
        private GameObject rig, actor;
        private Camera previewCamera;
        private Light keyLight, fillLight;
        private RenderTexture texture;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool initialized;

        public RenderTexture Texture => texture;
        public Camera PreviewCamera => previewCamera;

        public void Initialize(CourierSelection courierSelection)
        {
            if (initialized) throw new InvalidOperationException("The courier preview is already initialized.");
            initialized = true;
            selection = courierSelection ?? throw new ArgumentNullException(nameof(courierSelection));
            image = GetComponent<UnityEngine.UI.RawImage>();
            if (image == null) throw new InvalidOperationException("Courier preview requires its own RawImage.");
            rig = new GameObject("CourierPreviewRig");
            rig.transform.SetParent(transform, false);
            // World-space preview objects are isolated from gameplay by both position and layer.
            rig.transform.position = new Vector3(1000, 1000, 1000);
            var cameraObject = new GameObject("CourierPreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.9f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << 12;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 25;
            cameraObject.transform.localPosition = new Vector3(0,.1f,8);
            cameraObject.transform.localRotation = Quaternion.Euler(0,180,0);
            texture = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) {name="RadioCourierPreview", antiAliasing=2};
            texture.Create();
            previewCamera.targetTexture = texture;
            image.texture = texture;
            image.raycastTarget = false;
            keyLight = CreateLight("CourierPreviewKey",new Vector3(2.5f,3,4.5f),22,new Color(1,.95f,.85f));
            fillLight = CreateLight("CourierPreviewFill",new Vector3(-3,0,3),8,new Color(.72f,.88f,1));
            selection.Changed += Rebuild;
            Rebuild();
            previewCamera.enabled = isActiveAndEnabled;
            keyLight.enabled = fillLight.enabled = isActiveAndEnabled;
        }

        private Light CreateLight(string name,Vector3 position,float intensity,Color color)
        {
            var node = new GameObject(name,typeof(Light));
            node.transform.SetParent(rig.transform,false);
            node.transform.localPosition = position;
            var light = node.GetComponent<Light>();
            light.type=LightType.Point;light.intensity=intensity;light.range=12;
            light.color=color;light.cullingMask=1<<12;light.shadows=LightShadows.None;
            return light;
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
            Bounds bounds = CourierVisualBounds.Measure(renderers);
            actor.transform.localScale *= 3.05f / Mathf.Max(.01f,bounds.size.y);
            bounds = CourierVisualBounds.Measure(renderers);
            actor.transform.position += rig.transform.position - bounds.center;
            restPosition = actor.transform.localPosition;
            restRotation = actor.transform.localRotation;
        }

        private void Update()
        {
            if (actor == null) return;
            float t=Time.unscaledTime;
            actor.transform.localPosition = restPosition + Vector3.up*(Mathf.Sin(t*1.45f)*.065f);
            actor.transform.localRotation = restRotation * Quaternion.Euler(0, Mathf.Sin(t*.55f)*5, Mathf.Sin(t*1.1f)*1.5f);
        }

        private void OnEnable()
        {
            if(previewCamera != null) previewCamera.enabled=true;
            if(keyLight != null) keyLight.enabled=true;
            if(fillLight != null) fillLight.enabled=true;
        }
        private void OnDisable()
        {
            if(previewCamera != null) previewCamera.enabled=false;
            if(keyLight != null) keyLight.enabled=false;
            if(fillLight != null) fillLight.enabled=false;
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
