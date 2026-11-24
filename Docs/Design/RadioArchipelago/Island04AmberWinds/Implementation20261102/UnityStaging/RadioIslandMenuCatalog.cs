using System;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.UI
{
    /// <summary>Direct dependencies for public island selection; no scene loading or destructive catalog filtering.</summary>
    [CreateAssetMenu(menuName = "Radio Archipelago/Public island menu")]
    public sealed class RadioIslandMenuCatalog : ScriptableObject
    {
        public const string ResourcePath = "RadioArchipelago/IslandMenuCatalog";
        [Serializable] public sealed class Entry
        {
            public IslandStageDefinition Definition;
            public Material Skybox;
            public Color AmbientSky, AmbientEquator, AmbientGround, FogColor, SunColor;
            public float SunIntensity;
            public Quaternion SunRotation = Quaternion.identity;
        }
        public Entry[] Islands = Array.Empty<Entry>();

        public void Validate()
        {
            if (Islands == null || Islands.Length != RadioIslandMenuPolicy.IslandCount)
                throw new InvalidOperationException("The public menu requires one entry per public island.");
            for (int i = 0; i < Islands.Length; i++)
            {
                Entry entry = Islands[i];
                if (entry?.Definition == null || entry.Definition.SongId != RadioIslandMenuPolicy.SongId(i) ||
                    entry.Definition.Prefab == null || entry.Definition.RouteJson == null || entry.Definition.Chart == null ||
                    entry.Definition.ElevationJson == null || entry.Skybox == null || entry.SunIntensity <= 0)
                    throw new InvalidOperationException("Incomplete public island binding at " + i + ".");
            }
        }

        public Entry ForSong(string songId)
        {
            int index = RadioIslandMenuPolicy.IndexOf(songId);
            return index >= 0 && Islands != null && index < Islands.Length ? Islands[index] : null;
        }
    }

    /// <summary>A menu-owned binding of an existing scene stage and its lighting; disposed with the presentation.</summary>
    internal sealed class RadioIslandMenuWorld : IDisposable
    {
        private readonly OriginalPlayController game;
        private readonly RadioIslandMenuCatalog catalog;
        private readonly Light sun;
        private readonly Material previousSky;
        private readonly AmbientMode previousAmbientMode;
        private readonly Color previousAmbientSky, previousAmbientEquator, previousAmbientGround, previousFog, previousSunColor;
        private readonly float previousSunIntensity;
        private readonly Quaternion previousSunRotation;
        private GameObject ownedStageNode;

        public RadioIslandMenuWorld(OriginalPlayController controller, RadioIslandMenuCatalog content)
        {
            game = controller ?? throw new ArgumentNullException(nameof(controller));
            catalog = content ?? throw new ArgumentNullException(nameof(content));
            catalog.Validate();
            previousSky = RenderSettings.skybox; previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor; previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor; previousFog = RenderSettings.fogColor;
            sun = RenderSettings.sun;
            if (sun != null)
            {
                previousSunColor = sun.color; previousSunIntensity = sun.intensity;
                previousSunRotation = sun.transform.rotation;
            }
        }

        public bool BindDefinition(string songId)
        {
            var entry = catalog.ForSong(songId);
            if (entry == null) return false;
            IslandStagePresentation stage = game.IslandStage;
            if (stage == null)
            {
                ownedStageNode = new GameObject("Public archipelago stage");
                ownedStageNode.transform.SetParent(game.transform,false);
                stage = ownedStageNode.AddComponent<IslandStagePresentation>();
                game.SetIslandStage(stage);
            }
            if (stage.Definition != entry.Definition) stage.Configure(game, entry.Definition);
            return true;
        }

        public void ApplyLighting(string songId)
        {
            var entry = catalog.ForSong(songId);
            if (entry == null) return;
            RenderSettings.skybox = entry.Skybox; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = entry.AmbientSky; RenderSettings.ambientEquatorColor = entry.AmbientEquator;
            RenderSettings.ambientGroundColor = entry.AmbientGround; RenderSettings.fogColor = entry.FogColor;
            if (sun != null)
            {
                sun.color = entry.SunColor; sun.intensity = entry.SunIntensity; sun.transform.rotation = entry.SunRotation;
                RenderSettings.sun = sun;
            }
        }

        public void Dispose()
        {
            if(ownedStageNode!=null)
            {
                if(game!=null && game.IslandStage==ownedStageNode.GetComponent<IslandStagePresentation>()) game.SetIslandStage(null);
                ownedStageNode.SetActive(false);
                if(Application.isPlaying) UnityEngine.Object.Destroy(ownedStageNode); else UnityEngine.Object.DestroyImmediate(ownedStageNode);
                ownedStageNode=null;
            }
            RenderSettings.skybox = previousSky; RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSky; RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround; RenderSettings.fogColor = previousFog;
            if (sun != null)
            {
                sun.color = previousSunColor; sun.intensity = previousSunIntensity; sun.transform.rotation = previousSunRotation;
            }
        }
    }
}
