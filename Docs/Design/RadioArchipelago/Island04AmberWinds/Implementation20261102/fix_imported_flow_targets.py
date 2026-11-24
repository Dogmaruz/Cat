from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''var targets = new List<(string label,Vector3 point)>();''','''var targets = new List<(string label,Vector3 point,float v)>();''',1)
old='''                Vector3 start = tag == "roll210" ? new Vector3(65,15.1f,-118) : new Vector3(47,28,-96);
                foreach (float degrees in new[] {35f,48f,65f})
                {
                    float angle = degrees*Mathf.Deg2Rad;
                    targets.Add(("roll-" + degrees.ToString("00",System.Globalization.CultureInfo.InvariantCulture) + "deg",
                        start + new Vector3(radius*Mathf.Sin(angle),-radius*(1-Mathf.Cos(angle)),0)));
                }'''
new='''                float joinV = tag == "roll210" ? 53.113525390625f : 10.356194496154785f;
                foreach (float degrees in new[] {35f,48f,65f})
                {
                    float angle = degrees*Mathf.Deg2Rad;
                    // The native imported physical V axis locates the real curved
                    // sheet; do not approximate a diagonal creek roll as world+X.
                    targets.Add(("roll-" + degrees.ToString("00",System.Globalization.CultureInfo.InvariantCulture) + "deg",
                        Vector3.zero,joinV-radius*(Mathf.PI*.5f-angle)));
                }'''
assert old in s;s=s.replace(old,new,1)
s=s.replace('''targets.Add(("curtain-upper",new Vector3(48.6f,24,-96)));''','''targets.Add(("curtain-upper",new Vector3(48.6f,24,-96),float.NaN));''',1)
s=s.replace('''targets.Add(("curtain-middle",new Vector3(49,20,-96)));''','''targets.Add(("curtain-middle",new Vector3(49,20,-96),float.NaN));''',1)
s=s.replace('''targets.Add(("curtain-lower",new Vector3(49,15,-96)));''','''targets.Add(("curtain-lower",new Vector3(49,15,-96),float.NaN));''',1)
s=s.replace('''else targets.Add(("stream-centre",view.centre));''','''else targets.Add(("stream-centre",view.centre,float.NaN));''',1)
s=s.replace('''ImportedFlowSample(view,target.label,target.point)''','''ImportedFlowSample(view,target.label,target.point,target.v)''',1)
s=s.replace('''ImportedFlowSample(View view, string label, Vector3 wanted)''','''ImportedFlowSample(View view, string label, Vector3 wanted, float wantedV)''',1)
s=s.replace('''                    float distance = (centre-wanted).sqrMagnitude;
                    if (distance >= best) continue;
                    Vector2 ab = uv[ib]-uv[ia]''','''                    Vector2 centreUv = (uv[ia]+uv[ib]+uv[ic])/3;
                    float distance = float.IsNaN(wantedV) ? (centre-wanted).sqrMagnitude :
                        (centreUv.x-.5f)*(centreUv.x-.5f)*36+(centreUv.y-wantedV)*(centreUv.y-wantedV);
                    if (distance >= best) continue;
                    Vector2 ab = uv[ib]-uv[ia]''',1)
p.write_text(s,encoding='utf8')
print('R3/R1.5 slope targets now resolve by actual imported metricUV arclength, not approximate world-axis coordinates.')
