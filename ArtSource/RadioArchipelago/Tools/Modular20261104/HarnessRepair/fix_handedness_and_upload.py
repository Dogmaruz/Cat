from pathlib import Path
p=Path('DancingCatsClone/Assets/RadioArchipelago/Editor/ModularSourceHarnessFitter.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('else{EditorUtility.CopySerialized(mesh,asset);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(asset);}', '''else
                                {
                                    EditorUtility.CopySerialized(mesh,asset);
                                    // CopySerialized updates serialized mesh data but can leave an
                                    // already rendered native GPU buffer stale in this Editor process.
                                    asset.vertices=mesh.vertices;asset.normals=mesh.normals;asset.tangents=mesh.tangents;
                                    asset.bounds=mesh.bounds;asset.UploadMeshData(false);
                                    UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(asset);
                                }''',1)
s=s.replace('thin[i]=radialIsWidth?normalAxes[i]:radialAxes[i];','var sourceTangent=(centers[(i+1)%Samples]-centers[(i-1+Samples)%Samples]).normalized;\n                thin[i]=Vector3.Cross(sourceTangent,broad[i]).normalized;',1)
s=s.replace('Vector3 across=Vector3.Cross(tangent,outward).normalized;','Vector3 across=Vector3.Cross(outward,tangent).normalized;',1)
s=s.replace('float sourceSeam=points.Min(p=>p.z),seamBlend=.04f;', 'var anchors=CaseVertexGrid(equipment);',1)
s=s.replace('float hold=(1-Mathf.SmoothStep(0,1,(points[i].z-sourceSeam)/seamBlend))*Mathf.Clamp01(-Mathf.Sin(a)*4+.25f);', '''float rootDistance=DistanceToCase(points[i],anchors);
                float hold=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.012f,.075f,rootDistance));''',1)
pos=s.index('        static float Median')
s=s[:pos]+'''        const float AnchorCell=.04f;
        static Vector3Int Cell(Vector3 point)=>new Vector3Int(Mathf.FloorToInt(point.x/AnchorCell),Mathf.FloorToInt(point.y/AnchorCell),Mathf.FloorToInt(point.z/AnchorCell));
        static Dictionary<Vector3Int,List<Vector3>> CaseVertexGrid(CourierEquipmentRig equipment)
        {
            var grid=new Dictionary<Vector3Int,List<Vector3>>();
            foreach(var filter in equipment.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null||filter.name!="Case")continue;
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point=equipment.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));var key=Cell(point);
                    if(!grid.TryGetValue(key,out var bucket)){bucket=new List<Vector3>();grid.Add(key,bucket);}bucket.Add(point);
                }
            }
            return grid;
        }
        static float DistanceToCase(Vector3 point,Dictionary<Vector3Int,List<Vector3>> grid)
        {
            Vector3Int cell=Cell(point);float nearest=.15f*.15f;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++)
                if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var bucket))
                    foreach(var sample in bucket)nearest=Mathf.Min(nearest,(point-sample).sqrMagnitude);
            return Mathf.Sqrt(nearest);
        }

''' +s[pos:]
p.write_text(s,encoding='utf-8')
