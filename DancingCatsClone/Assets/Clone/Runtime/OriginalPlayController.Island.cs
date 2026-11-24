using System;
using RadioArchipelago.World;
using UnityEngine;

namespace DancingCats.Clone
{
    public sealed partial class OriginalPlayController
    {
        [SerializeField] private IslandStagePresentation islandStage;
        public IslandStagePresentation IslandStage => islandStage;
        /// <summary>Read-only native arc state used by the island presentation.</summary>
        public Vector3 IslandJumpStart => _jumpBegin;
        public float IslandJumpDistance => _jumpDistance;
        public bool IslandIsInFlight => _jumping;
        private bool IslandActive => islandStage != null && islandStage.IsActiveFor(Selected?.id);
        /// <summary>Chart input stays in the native lane domain; the island expands its world presentation.</summary>
        public float IslandLateralScale => IslandActive ? Mathf.Max(1, islandStage.Definition.LateralScale) : 1;
        public float IslandWorldLateralLimit => NativeRhythmRules.HorizontalLimit * IslandLateralScale;

        public void SetIslandStage(IslandStagePresentation stage) => islandStage = stage;

        private bool ContainsTileContact(OriginalTile tile, Vector3 center, float radius)
        {
            float width = IslandLateralScale;
            if (width > 1 && tile.element != null && tile.element.id != 10)
            {
                // Positions spread apart but ceramic supports retain their world
                // width. Query the original collider using world-sized X error;
                // Y/Z and the sphere radius keep their established semantics.
                center.x = tile.CenterX + (center.x - tile.CenterX) * width;
            }
            return tile.ContainsSphere(center, radius);
        }

        private float LandingError(OriginalTile tile) => (Actor.position.x - tile.CenterX) * IslandLateralScale;

        private static float ClampIslandPointerTarget(float target, float pointer, ref float origin, ref float dragOrigin)
        {
            float clamped = Mathf.Clamp(target, -NativeRhythmRules.HorizontalLimit, NativeRhythmRules.HorizontalLimit);
            if (target != clamped)
            {
                // Throw away travel past either edge so desktop reversals do
                // not need to unwind an invisible destination beyond the lane.
                origin = pointer; dragOrigin = clamped;
            }
            return clamped;
        }

        private void ApplyIslandChart()
        {
            if (IslandActive && islandStage.Definition.Chart != null)
            {
                Map = JsonUtility.FromJson<MapTimeline>(islandStage.Definition.Chart.text);
                if (Map == null || Map.id != Selected.id || Map.elements == null || Map.segments == null || Map.forwardSpeed <= 0)
                    throw new InvalidOperationException("The island chart must match its selected song and contain a valid measured map.");
            }
            else if (islandStage != null) islandStage.Deactivate();
        }

        private void BindIslandCourier()
        {
            if (_motion == null) return;
            bool onIsland = IslandActive;
            if (onIsland)
                islandStage.BindCourier(_motion.transform, Actor.Find("Courier lighting"));
            // Initialization creates the role before a song is selected. Apply
            // scale after binding too, so cold launch and song changes agree.
            _motion.transform.localScale = Vector3.one * (onIsland ? islandStage.Definition.CourierScale : .47f);
        }
    }
}
