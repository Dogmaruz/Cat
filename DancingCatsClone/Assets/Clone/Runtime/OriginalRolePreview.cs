using System;
using UnityEngine;

namespace DancingCats.Clone
{
    /// <summary>Renders an independent original rig through the recovered popup preview camera.</summary>
    [DisallowMultipleComponent]
    public sealed class OriginalRolePreview : MonoBehaviour
    {
        private UnityEngine.UI.RawImage image;
        private Camera previewCamera;
        private RenderTexture target, originalTarget;
        private GameObject actor;
        private GameObject model;
        private OriginalRoleMotion motion;
        private bool needsFrame;
        public string PrefabName { get; private set; }
        public Camera PreviewCamera => previewCamera;

        public void Bind(OriginalAssets assets, OriginalRoleDefinition role, OriginalRoleDefinition trail,
            Camera nativeCamera, Transform nativeParent)
        {
            if (assets == null || role == null || string.IsNullOrEmpty(role.prefabName) || nativeCamera == null || nativeParent == null)
                throw new InvalidOperationException("The original role preview needs its real prefab, camera and parent.");
            image = GetComponent<UnityEngine.UI.RawImage>();
            if (image == null) throw new InvalidOperationException("The original preview RawImage is missing.");
            if (previewCamera == null)
            {
                previewCamera = nativeCamera; originalTarget = nativeCamera.targetTexture;
                int width = originalTarget != null ? originalTarget.width : 512;
                int height = originalTarget != null ? originalTarget.height : 512;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Original role preview" };
                target.Create();
                previewCamera.targetTexture = target;
                previewCamera.cullingMask = 1 << 12;
            }
            if (actor != null) { actor.SetActive(false); Destroy(actor); }
            actor = new GameObject("Original preview actor");
            actor.transform.SetParent(nativeParent, false);
            model = Instantiate(assets.Prefab(role.prefabName), actor.transform, false);
            model.name = role.prefabName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            motion = actor.AddComponent<OriginalRoleMotion>();
            if (trail != null && !string.IsNullOrEmpty(trail.prefabName))
                Instantiate(assets.Prefab(trail.prefabName), actor.transform, false);
            foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 12;
            foreach (var source in actor.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
            PrefabName = role.prefabName;
            image.enabled = true; image.texture = target; image.uvRect = new Rect(0, 0, 1, 1);
            image.color = Color.white;
            previewCamera.enabled = gameObject.activeInHierarchy;
            needsFrame = true;
        }

        private void OnEnable()
        {
            if (previewCamera != null) previewCamera.enabled = true;
            needsFrame = true;
        }
        private void OnDisable() { if (previewCamera != null) previewCamera.enabled = false; }

        private void LateUpdate()
        {
            if (!needsFrame || actor == null || previewCamera == null) return;
            Canvas.ForceUpdateCanvases();
            // Popup wiring runs before its hierarchy is active. Animator.HasState is
            // valid after activation; initialize the real rig before measuring its bounds.
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            {
                if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) continue;
                animator.Rebind(); animator.Update(0);
            }
            var meshes = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (meshes.Length == 0) throw new InvalidOperationException("The original preview role has no skinned mesh.");
            // Preserve the authored camera's angle/projection. Normalize the independent model
            // to its viewport after Canvas scaling, without touching the live gameplay actor.
            var bounds = meshes[0].bounds;
            foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
            float visibleHeight = previewCamera.orthographic ? previewCamera.orthographicSize * 2 :
                2 * Mathf.Max(.1f, Vector3.Dot(bounds.center - previewCamera.transform.position, previewCamera.transform.forward)) * Mathf.Tan(previewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float scale = visibleHeight * .8f / Mathf.Max(.001f, bounds.size.y);
            actor.transform.localScale *= scale;
            bounds = meshes[0].bounds;
            foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
            Vector3 center = bounds.center - previewCamera.transform.position;
            actor.transform.position -= previewCamera.transform.right * Vector3.Dot(center, previewCamera.transform.right)
                + previewCamera.transform.up * Vector3.Dot(center, previewCamera.transform.up);
            // Spring lengths are world distances, so bind again after the independent preview scales.
            motion.Bind(model, PrefabName); motion.PlayIdle();
            needsFrame = false;
        }

        private void OnDestroy()
        {
            if (actor != null) Destroy(actor);
            if (previewCamera != null) { previewCamera.enabled = false; previewCamera.targetTexture = originalTarget; }
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
