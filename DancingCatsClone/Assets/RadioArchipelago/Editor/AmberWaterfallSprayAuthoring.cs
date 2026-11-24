using System;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Bounded ordinary particles at the raised cascade's real receiving basin.</summary>
    internal static class AmberWaterfallSprayAuthoring
    {
        internal static void Attach(Transform parent)
        {
            var shader = Shader.Find("RadioArchipelago/Amber Water Spray");
            if (shader == null) throw new InvalidOperationException("Amber spray shader is missing.");
            string path = AmberWindsIslandJobs.ContentPath + "/RA_Amber_Spray.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader;
            var root = new GameObject("Amber cascade impact spray").transform;
            root.SetParent(parent,false); root.localPosition = new Vector3(49.21f,12.25f,-96);
            Create(root,material,false);
            Create(root,material,true);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void Create(Transform parent,Material material,bool mist)
        {
            var node = new GameObject(mist ? "Soft basin mist" : "Falling water impact droplets");
            node.transform.SetParent(parent,false); node.transform.localRotation = Quaternion.Euler(-90,0,0);
            var particles = node.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false; particles.randomSeed = mist ? 7303u : 7301u;
            var main = particles.main;
            main.loop = true; main.prewarm = true; main.duration = 3;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(mist ? 1.1f : .55f,mist ? 1.8f : 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(mist ? .35f : 1.8f,mist ? .9f : 3.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(mist ? 1.25f : .09f,mist ? 2.8f : .22f);
            main.startColor = mist ? new Color(.87f,.97f,.96f,.18f) : new Color(.88f,.98f,.97f,.78f);
            main.gravityModifier = mist ? 0 : .65f; main.maxParticles = mist ? 24 : 48;
            var emission = particles.emission; emission.rateOverTime = mist ? 9 : 28;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = mist ? 45 : 36; shape.radius = mist ? 1.4f : 1.05f;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.6f,.55f),new GradientAlphaKey(0,1)});
            fade.color = gradient;
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mist ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = mist ? 1 : 1.5f;
            renderer.velocityScale = mist ? 0 : .08f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
