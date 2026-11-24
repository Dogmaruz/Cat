from pathlib import Path
stage=Path(__file__).with_name('UnityStaging'); p=stage/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
needle='''        [MenuItem("Tools/Radio Archipelago/Verify native water clock")]'''
methods='''        [Serializable] private sealed class SeamVertex
        {
            public string sourceNode;
            public Vector3 position,normal;
            public Vector2 uv;
            public Color color;
        }
        [Serializable] private sealed class SeamPair
        {
            public float u,positionError,normalDot,normalAngle;
            public SeamVertex stream,fall;
        }
        [Serializable] private sealed class AlphaComparison
        {
            public string scope = "Actual imported R3 join at UV V53.113525390625, U0/.5/1. Identical fixed210m portrait camera and cloned-material preview clock2s. B changes only cloned Fall SrcBlendOne/DstBlendZero; saved ZWrite0/queue3000 and all other properties stay unchanged. No source or real material writes.";
            public bool savedMaterialsUnchanged;
            public SeamPair[] seam;
        }
        public static void CaptureAmberAlphaComparison()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = AmberWindsIslandJobs.LoadDefinition();
            if (definition == null) throw new InvalidOperationException("The real fourth-island definition is required for this imported seam diagnosis.");
            string folder = AmberWindsIslandJobs.ReviewFolder + "/SeamDiagnosis"; Directory.CreateDirectory(folder);
            var saved = definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material && material.shader && material.shader.name == ShaderName).Distinct().ToArray();
            string[] before = saved.Select(EditorJsonUtility.ToJson).ToArray();
            bool[] dirty = saved.Select(EditorUtility.IsDirty).ToArray();
            var report = new AlphaComparison();
            using (var view = new View(definition,false,true))
            {
                view.UseGameplayCamera(definition,210);
                view.camera.clearFlags = CameraClearFlags.Skybox;
                view.camera.gameObject.AddComponent<Skybox>().material = AssetDatabase.LoadAssetAtPath<Material>(AmberWindsIslandJobs.ContentPath + "/AmberWindsSky.mat");
                var sun = view.owner.GetComponentInChildren<Light>(); sun.color = new Color(1,.90f,.73f); sun.intensity = 1.65f;
                foreach (var material in view.clones) material.SetFloat(PreviewTime,2);
                report.seam = new[] {0f,.5f,1f}.Select(u =>
                {
                    var stream = FindImportedSeamVertex(view,"Stream",u,53.113525390625f,null);
                    var fall = FindImportedSeamVertex(view,"Waterfall",u,53.113525390625f,stream.position);
                    return new SeamPair {u = u,stream = stream,fall = fall,positionError = Vector3.Distance(stream.position,fall.position),
                        normalDot = Vector3.Dot(stream.normal,fall.normal),normalAngle = Vector3.Angle(stream.normal,fall.normal)};
                }).ToArray();
                view.Capture(folder + "/roll210-current-alpha.png");
                foreach (var material in view.clones.Where(material => material.name.Contains("Waterfall")))
                { material.SetFloat("_SrcBlend",1); material.SetFloat("_DstBlend",0); }
                view.Capture(folder + "/roll210-cloned-fall-opaque.png");
            }
            report.savedMaterialsUnchanged = before.SequenceEqual(saved.Select(EditorJsonUtility.ToJson)) && dirty.SequenceEqual(saved.Select(EditorUtility.IsDirty));
            File.WriteAllText(folder + "/comparison.json",JsonUtility.ToJson(report,true));
            if (!report.savedMaterialsUnchanged) throw new InvalidOperationException("The diagnostic must leave all saved material data and dirty flags unchanged.");
            Debug.Log("AMBER_ALPHA_SEAM_DIAGNOSIS READY: native imported endpoints and clone-only A/B; no real material/source writes.");
        }
        private static SeamVertex FindImportedSeamVertex(View view, string kind, float u, float v, Vector3? matchPosition)
        {
            float score = float.MaxValue; SeamVertex best = null;
            foreach (var renderer in view.waters.Where(renderer => renderer.sharedMaterials.Any(material => material.name.Contains(kind))))
            {
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (Mathf.Abs(uv[i].y-v) > .003f || Mathf.Abs(uv[i].x-u) > .02f) continue;
                    Vector3 position = filter.transform.TransformPoint(vertices[i]);
                    float candidate = Mathf.Abs(uv[i].y-v)*100 + Mathf.Abs(uv[i].x-u)*10 + (matchPosition.HasValue ? Vector3.Distance(position,matchPosition.Value) : 0);
                    if (candidate >= score) continue;
                    score = candidate; best = new SeamVertex {sourceNode = renderer.name,position = position,uv = uv[i],
                        normal = filter.transform.worldToLocalMatrix.transpose.MultiplyVector(normals[i]).normalized,
                        color = colors.Length == vertices.Length ? colors[i] : Color.white};
                }
            }
            if (best == null) throw new InvalidOperationException("The imported seam endpoint is missing: " + kind + " U=" + u + " V=" + v);
            return best;
        }

'''
assert needle in s;s=s.replace(needle,methods+needle,1);p.write_text(s,encoding='utf8')
bridge=Path.cwd()/'DancingCatsClone/Assets/Clone/Editor/MusicalChartEditorSession.cs'; s=bridge.read_text(encoding='utf8'); needle='                    case "amber-scenery-water-live":'
s=s.replace(needle,'''                    case "amber-roll-alpha-compare":
                        RadioArchipelago.Editor.LivingWaterReview.CaptureAmberAlphaComparison();
                        break;
'''+needle,1);bridge.write_text(s,encoding='utf8')
print('Staged imported U0/.5/1 normal/color seam readout and clone-only fixed-clock alpha A/B.')
