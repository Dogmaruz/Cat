from pathlib import Path
stage=Path(__file__).with_name('UnityStaging'); p=stage/'AmberWindsIslandJobs.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''                material.SetFloat("_UseFlowUV", name.Contains("Stream") ? 1 : 0);
                material.SetFloat("_CurrentSpeed", name.Contains("Stream") ? 4.2f : name.Contains("Waterfall") ? 3.2f : 2.1f);''','''                bool authoredFlow = name.Contains("Stream") || name.Contains("Waterfall");
                material.SetFloat("_UseFlowUV", authoredFlow ? 1 : 0);
                material.SetFloat("_CurrentSpeed", authoredFlow ? 4.2f : 2.1f);''')
s=s.replace('''                    material.SetFloat("_ZWrite", 0);''','''                    material.SetVector("_FallFadeRange", new Vector4(-64,-61.5f,0,0));
                    material.SetFloat("_ZWrite", 0);''',1)
s=s.replace('''                material.SetFloat("_FlowSpeed", name.Contains("Waterfall") ? 1.3f : name.Contains("Stream") ? .65f : .55f);''','''                material.SetFloat("_FlowSpeed", authoredFlow ? .65f : .55f);''')
p.write_text(s,encoding='utf8')
p=stage/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''                                if ((!fall && (Mathf.Abs(material.GetFloat("_UseFlowUV")-1) > .001f || Mathf.Abs(material.GetFloat("_CurrentSpeed")-4.2f) > .001f)) ||
                                    (fall && material.GetFloat("_Fall") < .5f))''','''                                if (Mathf.Abs(material.GetFloat("_UseFlowUV")-1) > .001f || Mathf.Abs(material.GetFloat("_CurrentSpeed")-4.2f) > .001f ||
                                    Mathf.Abs(material.GetFloat("_FlowSpeed")-.65f) > .001f || (fall && material.GetFloat("_Fall") < .5f))''')
p.write_text(s,encoding='utf8')
print('Staged coherent downstream UV1/current4.2/flow.65 for every new Amber Stream/Fall; legacy old materials unchanged.')
