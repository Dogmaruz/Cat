using UnityEngine;

namespace DancingCats.Clone
{
    public sealed class OriginalTile : MonoBehaviour
    {
        public OriginalPlayController owner;
        public MapElement element;
        public MapSegment segment;
        public bool checkpoint, consumed;
        public float initialZ, initialX;
        private Collider[] _colliders;

        public void Initialize(OriginalPlayController game, MapElement source, MapSegment support = null, bool point = false)
        {
            owner = game; element = source; segment = support; checkpoint = point;
            initialZ = source.pos.z; initialX = source.pos.x;
            _colliders = GetComponents<Collider>();
            if (_colliders.Length == 0) _colliders = GetComponentsInChildren<Collider>(true);
            if (source.id != 7)
                foreach (var particle in GetComponentsInChildren<ParticleSystem>(true)) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.speed = 1;
        }

        public void Tick(float playerZ)
        {
            if (element == null) return;
            float ahead = initialZ - playerZ;
            var position = transform.position;
            if (element.updateType == 1)
            {
                float progress = NativeRhythmRules.MovePercent(ahead, element.begin, element.end);
                position.x = Mathf.Lerp(element.mstartPos.x, initialX, progress);
                position.y = Mathf.Lerp(element.mstartPos.y, element.pos.y, progress);
            }
            else if (element.updateType == 2 && owner.Support == this && ahead <= 0)
            {
                position.x = owner.Actor.position.x;
                position.z = initialZ + Mathf.Min(-ahead, element.mfendPos);
            }
            else if (element.updateType == 3)
                position.x = NativeRhythmRules.MoveCycleX(initialX, ahead, element.msLeft != 0);
            transform.position = position;
        }

        public float CenterX => transform.position.x;
        public float TopY => transform.position.y;
        public void MarkConsumed() { consumed = true; }

        public bool ContainsSphere(Vector3 center, float radius)
        {
            foreach (var collider in _colliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if ((collider.ClosestPoint(center) - center).sqrMagnitude <= radius * radius + .00001f) return true;
            }
            return false;
        }
    }

    public sealed class OriginalActorTrigger : MonoBehaviour
    {
        public OriginalPlayController owner;
        private void OnTriggerEnter(Collider other)
        {
            var tile = other.GetComponentInParent<OriginalTile>();
            if (tile != null) owner.TouchTile(tile);
        }
        private void OnTriggerExit(Collider other)
        {
            var tile = other.GetComponentInParent<OriginalTile>();
            if (tile != null) owner.LeaveTile(tile);
        }
    }
}
