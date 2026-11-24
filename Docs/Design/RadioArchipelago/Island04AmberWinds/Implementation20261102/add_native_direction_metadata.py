from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''        private static double started;''','''        private static double started;
        private static readonly List<float> LiveFrameTimes = new List<float>(16);''',1)
s=s.replace('''public FlowProperties[] materialProperties; }''','''public FlowProperties[] materialProperties; public float[] frameTimes; public DirectionSample[] flowSamples; }''',1)
s=s.replace('''                    firstLive = live.Capture(LiveFolder''','''                    LiveFrameTimes.Clear();
                    firstLive = live.Capture(LiveFolder''',1)
s=s.replace('''liveFrame.ToString("00") + ".png"); liveFrame++;''','''liveFrame.ToString("00") + ".png"); LiveFrameTimes.Add(Time.time); liveFrame++;''',1)
s=s.replace('''                    materialProperties = live.waters''','''                    frameTimes = LiveFrameTimes.ToArray(),flowSamples = AmberLive ? DirectionSamples(live,LiveTag) : Array.Empty<DirectionSample>(),
                    materialProperties = live.waters''',1)
needle='''                File.WriteAllText(LiveFolder + "/" + (AmberLive ? LiveTag + "-clock.json" : "live-clock.json"),JsonUtility.ToJson(report,true));'''
replacement='''                if (AmberLive) SaveVisibleMask(live,mask,LiveFolder + "/" + LiveTag + "-visible-mask.png");
'''+needle
assert needle in s;s=s.replace(needle,replacement,1)
needle='''        private static void Tick()
'''
methods='''        [Serializable] private sealed class DirectionSample
        {
            public string label,sourceNode;
            public float x,y,tangentX,tangentY,expectedPixelsPerSecond;
            public Vector3 worldPosition,worldDirection;
        }
        private static DirectionSample[] DirectionSamples(View view, string tag)
        {
            var targets = new List<(string label,Vector3 point)>();
            if (tag == "roll210" || tag == "side-roll")
            {
                float radius = tag == "roll210" ? 3 : 1.5f;
                Vector3 start = tag == "roll210" ? new Vector3(65,15.1f,-118) : new Vector3(47,28,-96);
                foreach (float degrees in new[] {35f,48f,65f})
                {
                    float angle = degrees*Mathf.Deg2Rad;
                    targets.Add(("roll-" + degrees.ToString("00",System.Globalization.CultureInfo.InvariantCulture) + "deg",
                        start + new Vector3(radius*Mathf.Sin(angle),-radius*(1-Mathf.Cos(angle)),0)));
                }
            }
            else if (tag == "waterfall")
            {
                targets.Add(("curtain-upper",new Vector3(48.6f,24,-96)));
                targets.Add(("curtain-middle",new Vector3(49,20,-96)));
                targets.Add(("curtain-lower",new Vector3(49,15,-96)));
            }
            else targets.Add(("stream-centre",view.centre));
            return targets.Select(target => ImportedFlowSample(view,target.label,target.point)).ToArray();
        }
        private static DirectionSample ImportedFlowSample(View view, string label, Vector3 wanted)
        {
            float best = float.MaxValue; Vector3 position = Vector3.zero,direction = Vector3.zero; string node = "";
            foreach (var renderer in view.waters)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles; var uv = mesh.uv;
                if (uv.Length != vertices.Length) continue;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i],ib = triangles[i+1],ic = triangles[i+2];
                    Vector3 a = filter.transform.TransformPoint(vertices[ia]),b = filter.transform.TransformPoint(vertices[ib]),c = filter.transform.TransformPoint(vertices[ic]);
                    Vector3 centre = (a+b+c)/3;
                    float distance = (centre-wanted).sqrMagnitude;
                    if (distance >= best) continue;
                    Vector2 ab = uv[ib]-uv[ia],ac = uv[ic]-uv[ia]; float determinant = ab.x*ac.y-ab.y*ac.x;
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    Vector3 along = ((c-a)*ab.x-(b-a)*ac.x)/determinant;
                    if (along.sqrMagnitude < .000001f) continue;
                    best = distance; position = centre; direction = along.normalized; node = renderer.name;
                }
            }
            if (best == float.MaxValue) throw new InvalidOperationException("The native water direction sample lacks imported metric UV triangles: " + label);
            Vector3 point = view.camera.WorldToViewportPoint(position);
            Vector3 before = view.camera.WorldToViewportPoint(position-direction*.1f),after = view.camera.WorldToViewportPoint(position+direction*.1f);
            Vector2 tangent = new Vector2((after.x-before.x)*(view.width-1),-(after.y-before.y)*(view.height-1))/.2f;
            float speed = view.waters.SelectMany(renderer => renderer.sharedMaterials).First().GetFloat("_CurrentSpeed") *
                view.waters.SelectMany(renderer => renderer.sharedMaterials).First().GetFloat("_FlowSpeed");
            return new DirectionSample {label = label,sourceNode = node,x = point.x*(view.width-1),y = (1-point.y)*(view.height-1),
                tangentX = tangent.normalized.x,tangentY = tangent.normalized.y,expectedPixelsPerSecond = tangent.magnitude*speed,
                worldPosition = position,worldDirection = direction};
        }
        private static void SaveVisibleMask(View view, Color32[] mask, string path)
        {
            var texture = new Texture2D(view.width,view.height,TextureFormat.RGBA32,false);
            try { texture.SetPixels32(mask); texture.Apply(false,false); File.WriteAllBytes(path,texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }

'''
assert needle in s;s=s.replace(needle,methods+needle,1)
p.write_text(s,encoding='utf8')
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'; s=p.read_text(encoding='utf8').replace('new Vector4(-64,-61.5f,0,0)','new Vector4(-90,-80,0,0)'); p.write_text(s,encoding='utf8')
print('Native direction metadata now uses actual imported UV triangles, exact sampled times and category-visible masks; Amber fade below-80.')
