from pathlib import Path
p=Path('DancingCatsClone/Assets/RadioArchipelago/Editor/ModularSourceHarnessFitter.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('var wardrobe=instance.GetComponent<CourierWardrobeRig>();\n                    if(wardrobe==null)', 'var wardrobe=instance.GetComponent<CourierWardrobeRig>();\n                    if(wardrobe==null)')
s=s.replace('foreach(var equipment in catalog.equipment.Where(x=>x.kind==CourierEquipmentKind.Backpack))','var surface=CreateSurface(instance,wardrobe.bodyRig.visualPivot);\n                    try\n                    {\n                    foreach(var equipment in catalog.equipment.Where(x=>x.kind==CourierEquipmentKind.Backpack))',1)
s=s.replace('Fit(filters[i],rig,wardrobe.bodyRig.visualPivot,wardrobe.profile)','Fit(filters[i],rig,wardrobe.bodyRig.visualPivot,wardrobe.profile,surface)',1)
s=s.replace('PrefabUtility.SaveAsPrefabAsset(instance,heroPath);','}\n                    finally{var mesh=surface.sharedMesh;UnityEngine.Object.DestroyImmediate(surface.gameObject);UnityEngine.Object.DestroyImmediate(mesh);}\n                    PrefabUtility.SaveAsPrefabAsset(instance,heroPath);',1)
s=s.replace('CourierMountProfile profile)\n        {','CourierMountProfile profile,MeshCollider surface)\n        {',1)
a=s.index('            // Remove one-vertex scan noise')
b=s.index('            var mesh=UnityEngine.Object.Instantiate(original)',a)
s=s[:a]+'''            // A local source centreline removes the loop's natural out-of-plane bend.
            // PCA of its transverse section identifies the actual broad leather face;
            // generated packs do not all orient that face in the same global plane.
            var centers=new Vector3[Samples];var broad=new Vector3[Samples];var thin=new Vector3[Samples];
            for(int i=0;i<Samples;i++)
            {
                var nearby=new List<Vector3>();float angle=i*Mathf.PI*2/Samples;
                for(int k=0;k<points.Length;k++)if(Mathf.Abs(Mathf.DeltaAngle(angles[k]*Mathf.Rad2Deg,angle*Mathf.Rad2Deg))<7.5f)nearby.Add(points[k]);
                if(nearby.Count<4)
                {
                    int[] nearest=Enumerable.Range(0,points.Length).OrderBy(k=>Mathf.Abs(Mathf.DeltaAngle(angles[k]*Mathf.Rad2Deg,angle*Mathf.Rad2Deg))).Take(12).ToArray();
                    nearby=nearest.Select(k=>points[k]).ToList();
                }
                centers[i]=new Vector3(Median(nearby.Select(p=>p.x)),Median(nearby.Select(p=>p.y)),Median(nearby.Select(p=>p.z)));
            }
            for(int pass=0;pass<4;pass++){var copy=centers.ToArray();for(int i=0;i<Samples;i++)centers[i]=(copy[(i-1+Samples)%Samples]+copy[i]*2+copy[(i+1)%Samples])*.25f;}
            for(int i=0;i<Samples;i++)
            {
                float angle=i*Mathf.PI*2/Samples;var tangent=(centers[(i+1)%Samples]-centers[(i-1+Samples)%Samples]).normalized;
                var radial=(horizontal*Mathf.Sin(angle)+Vector3.up*Mathf.Cos(angle));radial=Vector3.ProjectOnPlane(radial,tangent).normalized;
                if(radial.sqrMagnitude<.01f)radial=Vector3.ProjectOnPlane(planeNormal,tangent).normalized;
                var normal=Vector3.Cross(tangent,radial).normalized;double aa=0,bb=0,ab=0;int count=0;
                for(int k=0;k<points.Length;k++)
                {
                    if(Mathf.Abs(Mathf.DeltaAngle(angles[k]*Mathf.Rad2Deg,angle*Mathf.Rad2Deg))>10)continue;
                    var delta=points[k]-centers[i];double x=Vector3.Dot(delta,radial),y=Vector3.Dot(delta,normal);aa+=x*x;bb+=y*y;ab+=x*y;count++;
                }
                float turn=count>3?.5f*Mathf.Atan2((float)(2*ab),(float)(aa-bb)):0;
                broad[i]=(radial*Mathf.Cos(turn)+normal*Mathf.Sin(turn)).normalized;
                if(i>0&&Vector3.Dot(broad[i],broad[i-1])<0)broad[i]=-broad[i];
                thin[i]=Vector3.Cross(tangent,broad[i]).normalized;
                if(Vector3.Dot(thin[i],radial)<0)thin[i]=-thin[i];
            }
            for(int pass=0;pass<6;pass++)
            {
                var copy=broad.ToArray();for(int i=0;i<Samples;i++)
                {
                    var previous=copy[(i-1+Samples)%Samples];var next=copy[(i+1)%Samples];if(Vector3.Dot(previous,copy[i])<0)previous=-previous;if(Vector3.Dot(next,copy[i])<0)next=-next;
                    broad[i]=(previous+copy[i]*2+next).normalized;
                }
            }
            float sourceSeam=points.Min(p=>p.z),seamBlend=.04f;
            float scale=visual.InverseTransformVector(equipment.transform.TransformVector(Vector3.up)).magnitude;
            var result=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                float a=angles[i],sample=a/(Mathf.PI*2)*Samples;int lo=Mathf.FloorToInt(sample)%Samples,hi=(lo+1)%Samples;float blend=sample-Mathf.Floor(sample);
                Vector3 center=Vector3.Lerp(centers[lo],centers[hi],blend);
                Vector3 sourceWide=Vector3.Lerp(broad[lo],broad[hi],blend).normalized;
                Vector3 sourceThin=Vector3.Lerp(thin[lo],thin[hi],blend).normalized;
                float widthOffset=Vector3.Dot(points[i]-center,sourceWide)*scale;
                float thicknessOffset=Vector3.Dot(points[i]-center,sourceThin)*scale;
                Vector3 target=Vector3.Lerp(path[lo],path[hi],blend);
                Vector3 outward=Vector3.Slerp(normals[lo],normals[hi],blend).normalized;
                Vector3 tangent=(path[(lo+1)%Samples]-path[(lo-1+Samples)%Samples]).normalized;
                Vector3 across=Vector3.Cross(tangent,outward).normalized;
                if(across.sqrMagnitude<.1f)across=Vector3.right;
                target+=across*Mathf.Clamp(widthOffset,-.12f,.12f);
                float outerOffset=.025f+Mathf.Clamp(thicknessOffset,-.012f,.06f);
                // Conform the complete supplied leather face, not just its centre.
                // Otherwise the inner half of a broad strip disappears inside a curved torso.
                var ray=new Ray(visual.TransformPoint(target+outward*2),visual.TransformDirection(-outward));
                float front=Mathf.Clamp01(Mathf.Sin(a)*4+.25f);
                if(front>0&&surface.Raycast(ray,out RaycastHit hit,4))
                {
                    Vector3 onBody=visual.InverseTransformPoint(hit.point);Vector3 normal=visual.InverseTransformDirection(hit.normal).normalized;
                    target=Vector3.Lerp(target,onBody,front);outward=Vector3.Slerp(outward,normal,front).normalized;
                }
                target+=outward*outerOffset;
                // Only actual rear cuts retain the exact attachment seam. Holding a
                // forward buckle merely because its depth matches the cut folds faces.
                float hold=(1-Mathf.SmoothStep(0,1,(points[i].z-sourceSeam)/seamBlend))*Mathf.Clamp01(-Mathf.Sin(a)*4+.25f);
                Vector3 originalInVisual=visual.InverseTransformPoint(equipment.transform.TransformPoint(points[i]));
                target=Vector3.Lerp(target,originalInVisual,hold);
                result[i]=source.transform.InverseTransformPoint(visual.TransformPoint(target));
            }
''' +s[b:]
pos=s.index('        static void AttachRear')
s=s[:pos]+'''        static float Median(IEnumerable<float> values){var sorted=values.OrderBy(x=>x).ToArray();return sorted[sorted.Length/2];}

        static MeshCollider CreateSurface(GameObject hero,Transform visual)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.sharedMesh==null||skin.bones.Length==0)continue;
                var scratch=new Mesh();
                try
                {
                    skin.BakeMesh(scratch,true);var points=scratch.vertices;var counts=skin.sharedMesh.GetBonesPerVertex();var weights=skin.sharedMesh.GetAllBoneWeights();int cursor=0;
                    var eligible=new bool[points.Length];int offset=vertices.Count;
                    for(int i=0;i<points.Length;i++)
                    {
                        float weight=0;for(int j=0;j<counts[i];j++){var w=weights[cursor++];string name=skin.bones[w.boneIndex].name;if(name=="body"||name=="head"||name=="root")weight+=w.weight;}
                        eligible[i]=weight>.7f;vertices.Add(visual.InverseTransformPoint(skin.transform.TransformPoint(points[i])));
                    }
                    var indices=scratch.triangles;for(int i=0;i<indices.Length;i+=3)if(eligible[indices[i]]&&eligible[indices[i+1]]&&eligible[indices[i+2]])
                    {triangles.Add(offset+indices[i]);triangles.Add(offset+indices[i+1]);triangles.Add(offset+indices[i+2]);}
                }
                finally{UnityEngine.Object.DestroyImmediate(scratch);}
            }
            var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var node=new GameObject("Disposable source strap tailoring surface");node.transform.SetParent(visual,false);var collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();return collider;
        }

''' +s[pos:]
p.write_text(s,encoding='utf-8')
