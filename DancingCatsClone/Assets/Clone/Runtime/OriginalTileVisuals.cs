using System;
using UnityEngine;

namespace DancingCats.Clone
{
    public static class OriginalTileVisuals
    {
        public static void RegisterHoldPointTemplate(GameObject hold, GameObject prefab)
        {
            if (prefab == null || prefab.transform.Find("Point") == null)
                throw new ArgumentException("The recovered Hold_Light checkpoint prefab is required.", nameof(prefab));
            State(hold).HoldPointTemplate = prefab;
        }

        public static GameObject GetHoldPointTemplate(GameObject hold)
        {
            var template = State(hold).HoldPointTemplate;
            if (template == null) throw new InvalidOperationException("Register the original external Hold_Light prefab before spawning checkpoints.");
            return template;
        }

        public static GameObject SpawnHoldPoint(GameObject hold, Vector3 worldPosition)
        {
            var point = UnityEngine.Object.Instantiate(GetHoldPointTemplate(hold), hold.transform);
            point.transform.position = worldPosition;
            point.transform.localScale = Vector3.one;
            point.SetActive(true);
            var state = State(point);
            state.IsPoint = true;
            state.PointModel = point.transform.Find("Point");
            state.PointModel.gameObject.SetActive(true);
            StopParticles(point);
            return point;
        }

        public static Collider GetHoldExitCollider(GameObject hold)
        {
            var exit = hold.transform.Find("Jump");
            return exit == null ? null : exit.GetComponent<Collider>();
        }

        public static void Configure(GameObject tile, MapElement element, MapSegment segment)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            var state = State(tile);
            state.Animator = tile.GetComponent<Animator>();
            state.IsHold = element.id == 2 || element.id == 3;
            state.IsFollow = element.id == 4;
            state.IsFollowMid = element.id == 5;
            state.IsFollowEnd = element.id == 6;
            StopParticles(tile);
            Play(state.Animator, state.IsFollowEnd ? "FollowEnd_Normal" : "Normal");

            if (state.IsHold)
            {
                state.Start = Require(tile.transform, "Hold/Dummy002/Start");
                state.End = Require(tile.transform, "Hold/Dummy002/End");
                var start = state.Start.localPosition;
                start.y = element.se.x;
                state.Start.localPosition = start;
                var end = state.End.localPosition;
                end.y = element.se.y;
                state.End.localPosition = end;
                // Environment 6's authored child is "Hold_Light " (a trailing space).
                state.HoldLight = FindDirectTrimmed(tile.transform, "Hold_Light");
                if (state.HoldLight == null) throw new InvalidOperationException("Original hold light root is missing.");
                state.Follow = Require(state.HoldLight, "Dummy001/Follow");
                state.Follow.position = state.Start.position;
                var exit = Require(tile.transform, "Jump");
                exit.position = state.End.position;
                // The authored mesh is skinned between the two relocated end bones.
                // Recovered fixed bounds must include the new authored span for reliable culling.
                foreach (var renderer in tile.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var bounds = renderer.localBounds;
                    bounds.Encapsulate(renderer.transform.InverseTransformPoint(state.Start.position));
                    bounds.Encapsulate(renderer.transform.InverseTransformPoint(state.End.position));
                    renderer.localBounds = bounds;
                }
                state.Holding = false;
            }
            else if (state.IsFollow)
            {
                Require(tile.transform, "Normal").gameObject.SetActive(true);
                Require(tile.transform, "Down").gameObject.SetActive(false);
            }
            else if (state.IsFollowMid)
                Require(tile.transform, "Sphere").gameObject.SetActive(true);

            if (!state.IsHold && !state.IsFollowMid)
            {
                var ring = FindNamed(tile.transform, "Ring");
                if (ring != null) ring.gameObject.SetActive(false);
            }
        }

        public static void OnHit(GameObject tile, bool perfect, bool slider, int streak)
        {
            var state = State(tile);
            if (state.IsPoint || tile.transform.Find("Point") != null)
            {
                state.PointModel = state.PointModel != null ? state.PointModel : tile.transform.Find("Point");
                if (state.PointModel != null) state.PointModel.gameObject.SetActive(false);
                PlayEffect(tile.transform, perfect ? "Effect_Point_P" : "Effect_Point_N");
                return;
            }
            if (state.IsFollowMid)
            {
                Require(tile.transform, "Sphere").gameObject.SetActive(false);
                PlayEffect(tile.transform, "Effect");
                return;
            }
            if (state.IsFollow)
            {
                Require(tile.transform, "Normal").gameObject.SetActive(false);
                Require(tile.transform, "Down").gameObject.SetActive(true);
            }
            if (slider)
            {
                Play(state.Animator, "Loop");
                if (state.IsHold)
                {
                    state.HoldLight.gameObject.SetActive(true);
                    state.Holding = true;
                    PlayEffect(tile.transform, "Effect_Point_P");
                }
            }
            else
            {
                // Native Perfect() scores every centered hit but returns true only after the fourth.
                bool enhancedPerfect = perfect && streak > 4;
                Play(state.Animator, state.IsFollowEnd ? "FollowEnd_Out" : (enhancedPerfect ? "Perfect" : "Great"));
                PlayEffect(tile.transform, enhancedPerfect ? "Effect_P" : "Effect_N");
            }
        }

        public static void RestartHold(GameObject tile, float playerZ)
        {
            var state = State(tile);
            if (!state.IsHold) return;
            Play(state.Animator, "Loop");
            state.HoldLight.gameObject.SetActive(true);
            state.Holding = true;
            PlayEffect(tile.transform, "Effect_Point_P");
            AdvanceHold(tile, playerZ);
        }

        public static void AdvanceHold(GameObject tile, float playerZ)
        {
            var state = State(tile);
            if (!state.IsHold || !state.Holding) return;
            var position = state.Follow.position;
            position.z = Mathf.Min(playerZ, state.End.position.z);
            state.Follow.position = position;
            var local = state.Follow.localPosition;
            local.x = local.z = 0f;
            state.Follow.localPosition = local;
            if (playerZ < state.End.position.z) return;
            state.Holding = false;
            Play(state.Animator, "End");
        }

        private static OriginalTileVisualState State(GameObject tile)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            var state = tile.GetComponent<OriginalTileVisualState>();
            return state != null ? state : tile.AddComponent<OriginalTileVisualState>();
        }

        private static void Play(Animator animator, string name)
        {
            if (animator == null) return;
            int hash = Animator.StringToHash(name);
            if (animator.HasState(0, hash)) animator.Play(hash, 0, 0f);
        }

        private static void StopParticles(GameObject tile)
        {
            foreach (var particle in tile.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void PlayEffect(Transform root, string name)
        {
            var effect = FindNamed(root, name);
            if (effect == null) return; // Some original tile types intentionally have no hit particle field.
            effect.gameObject.SetActive(true);
            var main = effect.GetComponent<ParticleSystem>();
            if (main != null) main.Play(true);
            else foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true)) particle.Play(false);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var found = FindNamed(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDirectTrimmed(Transform root, string name)
        {
            foreach (Transform child in root)
                if (child.name.TrimEnd() == name) return child;
            return null;
        }

        private static Transform Require(Transform root, string path)
        {
            var value = root.Find(path);
            if (value == null) throw new InvalidOperationException("Original tile hierarchy is missing: " + path);
            return value;
        }
    }

    internal sealed class OriginalTileVisualState : MonoBehaviour
    {
        internal GameObject HoldPointTemplate;
        internal Animator Animator;
        internal Transform Start, End, Follow, PointModel, HoldLight;
        internal bool IsHold, IsFollow, IsFollowMid, IsFollowEnd, IsPoint, Holding;
    }
}
