using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>One island-owned clock for the authored rigid rooftop heads.</summary>
    public sealed class IslandRooftopMotion : IDisposable
    {
        [Serializable] public sealed class MaterialSource { public string nodeName; public int slot; }
        [Serializable] public sealed class Mechanism
        {
            public string nodeName, kind;
            public Vector3 pivot, axis;
            public float degreesPerSecond;
            public MaterialSource[] materialSources;
        }
        [Serializable] public sealed class Document { public int schema; public string island; public Mechanism[] mechanisms; }
        private sealed class Rotor
        {
            public Transform node;
            public Quaternion rest;
            public Vector3 axisInParent;
            public float speed, angle;
        }
        private Rotor[] rotors = Array.Empty<Rotor>();
        public int Count => rotors.Length;

        public void Configure(Transform model, Transform coordinateSpace, TextAsset manifest)
        {
            Dispose();
            if (manifest == null) return;
            if (model == null || coordinateSpace == null) throw new ArgumentNullException(nameof(model));
            var document = JsonUtility.FromJson<Document>(manifest.text);
            if (document == null || document.schema != 1 || document.mechanisms == null || document.mechanisms.Length > 32)
                throw new InvalidOperationException("Invalid rooftop motion manifest: " + manifest.name);
            var hierarchy = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
                if (!hierarchy.TryAdd(node.name, node) && node.name.StartsWith("RA_Rooftop_", StringComparison.Ordinal))
                    throw new InvalidOperationException("Duplicate rooftop binding: " + node.name);
            var bindings = new Rotor[document.mechanisms.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bindings.Length; i++)
            {
                Mechanism entry = document.mechanisms[i];
                if (entry == null || string.IsNullOrEmpty(entry.nodeName) ||
                    !entry.nodeName.StartsWith("RA_Rooftop_", StringComparison.Ordinal) || !names.Add(entry.nodeName) ||
                    !hierarchy.TryGetValue(entry.nodeName, out Transform node))
                    throw new InvalidOperationException("Missing rooftop node: " + entry?.nodeName);
                if (!IsFinite(entry.axis) || !IsFinite(entry.pivot) || entry.axis.sqrMagnitude < .99f ||
                    !IsFinite(entry.degreesPerSecond) || Mathf.Abs(entry.degreesPerSecond) > 180)
                    throw new InvalidOperationException("Invalid rooftop rotation: " + entry.nodeName);
                Vector3 actualPivot = coordinateSpace.InverseTransformPoint(node.position);
                if (Vector3.Distance(actualPivot, entry.pivot) > .01f)
                    throw new InvalidOperationException("Imported rooftop pivot is misaligned: " + entry.nodeName + " " + actualPivot + " / " + entry.pivot);
                bindings[i] = new Rotor { node = node, rest = node.localRotation,
                    axisInParent = node.parent.InverseTransformDirection(coordinateSpace.TransformDirection(entry.axis.normalized)).normalized,
                    speed = entry.degreesPerSecond };
            }
            rotors = bindings;
        }

        public void Tick(float deltaTime)
        {
            float dt = Mathf.Clamp(deltaTime, 0, .1f);
            if (dt <= 0) return;
            for (int i = 0; i < rotors.Length; i++)
            {
                Rotor rotor = rotors[i];
                if (rotor.node == null) continue;
                rotor.angle = Mathf.Repeat(rotor.angle + rotor.speed * dt, 360);
                rotor.node.localRotation = Quaternion.AngleAxis(rotor.angle, rotor.axisInParent) * rotor.rest;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < rotors.Length; i++)
                if (rotors[i].node != null) rotors[i].node.localRotation = rotors[i].rest;
            rotors = Array.Empty<Rotor>();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}

