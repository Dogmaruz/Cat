from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''            public float u,positionError,normalDot,normalAngle;''','''            public string joint;
            public float u,positionError,normalDot,normalAngle,maximumPredictedAnimatedGap,colorError;''',1)
s=s.replace('''Actual imported R3 join at UV V53.113525390625, U0/.5/1.''','''Actual imported three joins at five U samples; matched normals/colors and frozen e5595 Vert displacement at ten real-valued sample times. R3 cloned-alpha comparison at UV V53.113525390625.''',1)
start=s.index('''                report.seam = new[] {0f,.5f,1f}.Select(u =>''')
end=s.index('''                view.Capture(folder + "/roll210-current-alpha.png");''',start)
new='''                var pairs = new List<SeamPair>();
                var joints = new[] {
                    (name:"lower",v:36.018428802490234f,stream:"Amber_DownstreamCreek_0",fall:"Amber_VoluminousFallingWater_0_0"),
                    (name:"R3",v:53.113525390625f,stream:"Amber_DownstreamCreek_1",fall:"Amber_VoluminousFallingWater_1_0"),
                    (name:"side",v:10.356194496154785f,stream:"RA_Amber_SideCascade_RaisedStreamAndRoll",fall:"RA_Amber_SideCascade_RaisedFall_Layer0") };
                foreach (var joint in joints) foreach (float u in new[] {0f,.25f,.5f,.75f,1f})
                {
                    var stream = FindImportedSeamVertex(view,"Stream",u,joint.v,null,joint.stream);
                    var fall = FindImportedSeamVertex(view,"Waterfall",u,joint.v,stream.position,joint.fall);
                    var pair = new SeamPair {joint = joint.name,u = u,stream = stream,fall = fall,positionError = Vector3.Distance(stream.position,fall.position),
                        normalDot = Vector3.Dot(stream.normal,fall.normal),normalAngle = Vector3.Angle(stream.normal,fall.normal),
                        colorError = Mathf.Max(Mathf.Abs(stream.color.r-fall.color.r),Mathf.Max(Mathf.Abs(stream.color.g-fall.color.g),Mathf.Abs(stream.color.b-fall.color.b)))};
                    foreach (float time in new[] {0f,.1f,.25f,.5f,.75f,1f,1.5f,2f,3f,4.5f})
                        pair.maximumPredictedAnimatedGap = Mathf.Max(pair.maximumPredictedAnimatedGap,
                            Vector3.Distance(EvaluateFrozenVert(stream,time,0),EvaluateFrozenVert(fall,time,1)));
                    pairs.Add(pair);
                }
                report.seam = pairs.ToArray();
'''
s=s[:start]+new+s[end:]
s=s.replace('''FindImportedSeamVertex(View view, string kind, float u, float v, Vector3? matchPosition)''','''FindImportedSeamVertex(View view, string kind, float u, float v, Vector3? matchPosition, string nodeName)''',1)
s=s.replace('''view.waters.Where(renderer => renderer.sharedMaterials.Any(material => material.name.Contains(kind)))''','''view.waters.Where(renderer => renderer.name == nodeName && renderer.sharedMaterials.Any(material => material.name.Contains(kind)))''',1)
needle='''        [MenuItem("Tools/Radio Archipelago/Verify native water clock")]'''
method='''        private static float HlslSmooth(float low, float high, float value)
        { float t = Mathf.Clamp01((value-low)/(high-low)); return t*t*(3-2*t); }
        private static Vector3 EvaluateFrozenVert(SeamVertex sample, float seconds, float fallFlag)
        {
            float time = seconds*.65f;
            Vector3 position = sample.position,normal = sample.normal;
            float wave = Mathf.Sin(position.x*.48f+position.z*.31f-time*1.5f)*.045f +
                Mathf.Sin(position.x*-.28f+position.z*.71f+time*1.1f)*.025f;
            float horizontal = HlslSmooth(.45f,.9f,Mathf.Abs(normal.y))*(1-fallFlag);
            position.y += wave*horizontal;
            float falling = Mathf.Max(fallFlag,1-HlslSmooth(.35f,.85f,Mathf.Abs(normal.y)));
            position += normal*Mathf.Sin(position.y*.8f+time*2.1f+sample.uv.x*5)*.065f*falling;
            return position;
        }

'''
assert needle in s;s=s.replace(needle,method+needle,1)
p.write_text(s,encoding='utf8')
print('Imported seam proof now covers all3 joints×5U×10time frozen vertex-displacement samples, actual normals/colors.')
