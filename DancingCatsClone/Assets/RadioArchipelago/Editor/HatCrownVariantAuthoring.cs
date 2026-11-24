using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Copy-on-write, editor-only crown tailoring. Source meshes and PBR references stay intact.</summary>
    internal static class HatCrownVariantAuthoring
    {
        const string Root = "Assets/RadioArchipelago/Content/GeneratedHatFits";
        static readonly string Evidence = Path.GetFullPath("../Docs/Design/RadioArchipelago/HatsAndMotion20261105/GeneratedHatFits");
        [Serializable] internal sealed class Recipe
        {
            public string hero, hat, sourcePrefab, generatedPrefab;
            public float heightScale, radialScale, anchorY;
            public Vector3 openingCenter;
            public int meshes, vertices;
            public Control[] controls;
            public float minimumJacobianDeterminant=float.MaxValue,maximumDisplacement;
            public int nonfiniteVertices,nonfiniteNormals,nonfiniteTangents;
        }
        [Serializable] internal sealed class Control
        {
            public Vector3 center, displacement;
            public float radius=.18f, verticalRadius=.13f;
            public int pass;
        }

        public static Vector3 Deform(Vector3 point, ModularHatFitRepair.OpeningDescriptor opening, float heightScale, float radialScale,
            Control[] controls=null,float anchorY=float.NaN)
        {
            float height = point.y - opening.referenceHeight;
            // The lower opening ring remains unchanged. A smooth transition
            // avoids a kink or an artificial seam at the anchored edge.
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(height / .16f));
            float radial = 1 + (radialScale - 1) * blend;
            Vector3 result=height<=0?point:new Vector3(opening.center.x + (point.x-opening.center.x)*radial,
                opening.referenceHeight + height*(1+(heightScale-1)*blend),
                opening.center.z + (point.z-opening.center.z)*radial);
            if(controls==null||controls.Length==0)return result;
            if(float.IsNaN(anchorY))anchorY=opening.referenceHeight;
            float anchored=Mathf.SmoothStep(0,1,Mathf.Clamp01((point.y-anchorY)/.075f));
            // Each local sculpt pass is a normalized smooth field. Controls in
            // one patch never add hundreds of duplicate displacements together.
            for(int pass=0;pass<6;pass++)
            {
                Vector3 displacement=Vector3.zero;float weight=0;
                foreach(var control in controls)
                {
                    if(control.pass!=pass)continue;
                    Vector3 delta=point-control.center;
                    float distance=(delta.x*delta.x+delta.z*delta.z)/(control.radius*control.radius)+
                        delta.y*delta.y/(control.verticalRadius*control.verticalRadius);
                    if(distance>9)continue;
                    float influence=Mathf.Exp(-distance*1.5f);
                    displacement+=control.displacement*influence;weight+=influence;
                }
                result+=displacement/Mathf.Max(1,weight)*anchored;
            }
            return result;
        }

        public static GameObject Save(string hero, CourierEquipmentDefinition hat,
            ModularHatFitRepair.OpeningDescriptor opening, float heightScale, float radialScale,Control[] controls=null,float anchorY=float.NaN)
        {
            string folder = Root + "/" + hero;
            if(!Finite(heightScale)||!Finite(radialScale)||heightScale<=0||radialScale<=0)
                throw new InvalidOperationException("Hat tailoring scale is invalid.");
            if(controls!=null)
                foreach(var control in controls)
                    if(!Finite(control.center)||!Finite(control.displacement)||!Finite(control.radius)||!Finite(control.verticalRadius)||
                        control.radius<=.005f||control.verticalRadius<=.005f)
                        throw new InvalidOperationException("Hat tailoring contains an invalid sculpt control.");
            EnsureFolder(folder);
            string prefabPath = folder + "/" + hat.id + ".prefab";
            var actor = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(hat.prefab));
            actor.name = hero + "__" + hat.id + "__TailoredCrown";
            var recipe = new Recipe { hero=hero,hat=hat.id,sourcePrefab=AssetDatabase.GetAssetPath(hat.prefab),
                generatedPrefab=prefabPath,heightScale=heightScale,radialScale=radialScale,
                anchorY=float.IsNaN(anchorY)?opening.referenceHeight:anchorY,openingCenter=opening.center,controls=controls };
            try
            {
                var filters=actor.GetComponentsInChildren<MeshFilter>(true);
                for(int index=0;index<filters.Length;index++)
                {
                    var filter=filters[index];var source=filter.sharedMesh;
                    if(source==null)continue;
                    // Instantiate preserves UVs, material submeshes, vertex
                    // colors and all imported channels. Never mutate sharedMesh.
                    var copy=UnityEngine.Object.Instantiate(source);
                    copy.name=hero+"_"+hat.id+"_Crown_"+index;
                    var matrix=actor.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    Warp(copy,matrix,opening,heightScale,radialScale,controls,recipe.anchorY,recipe);
                    string meshPath=folder+"/"+hat.id+"_Mesh"+index+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing==null){AssetDatabase.CreateAsset(copy,meshPath);existing=copy;}
                    else
                    {
                        EditorUtility.CopySerialized(copy,existing);
                        // Serialized Mesh updates alone can leave a renderer's
                        // native vertex buffers resident. Assign the changed
                        // streams explicitly and upload before reusing the GUID.
                        existing.vertices=copy.vertices;
                        if(copy.normals.Length==copy.vertexCount)existing.normals=copy.normals;
                        if(copy.tangents.Length==copy.vertexCount)existing.tangents=copy.tangents;
                        EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(copy);
                    }
                    RefreshMesh(existing,meshPath);
                    existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    filter.sharedMesh=existing;
                    recipe.meshes++;recipe.vertices+=existing.vertexCount;
                }
                var prefab=PrefabUtility.SaveAsPrefabAsset(actor,prefabPath);
                Directory.CreateDirectory(Path.Combine(Evidence,hero));
                File.WriteAllText(Path.Combine(Evidence,hero,hat.id+".json"),JsonUtility.ToJson(recipe,true));
                return prefab;
            }
            finally{PrefabUtility.UnloadPrefabContents(actor);}
        }

        public static Recipe Read(string hero,string hat)
        {
            string path=Path.Combine(Evidence,hero,hat+".json");
            return File.Exists(path)?JsonUtility.FromJson<Recipe>(File.ReadAllText(path)):null;
        }

        public static void ReloadAll()
        {
            int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{Root}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)continue;RefreshMesh(mesh,path);count++;
            }
            Debug.Log("GENERATED_HAT_MESH_RELOAD: "+count+" generated meshes uploaded and force-imported.");
        }

        static void RefreshMesh(Mesh mesh,string path)
        {
            mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var loaded=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(loaded!=null){loaded.RecalculateBounds();loaded.UploadMeshData(false);}
        }

        static void Warp(Mesh mesh,Matrix4x4 toRoot,ModularHatFitRepair.OpeningDescriptor opening,float heightScale,float radialScale,
            Control[] controls,float anchorY,Recipe evidence)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
            var fromRoot=toRoot.inverse;var normalToRoot=fromRoot.transpose;var normalFromRoot=toRoot.transpose;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 point=toRoot.MultiplyPoint3x4(vertices[i]);
                var jacobian=Jacobian(point,opening,heightScale,radialScale,controls,anchorY);
                float determinant=jacobian.determinant;
                if(!Finite(determinant)||determinant<.12f)throw new InvalidOperationException("Hat tailoring would fold a mesh; reduce local sculpt displacement.");
                evidence.minimumJacobianDeterminant=Mathf.Min(evidence.minimumJacobianDeterminant,determinant);
                Vector3 deformed=Deform(point,opening,heightScale,radialScale,controls,anchorY);
                vertices[i]=fromRoot.MultiplyPoint3x4(deformed);
                if(!Finite(vertices[i])){evidence.nonfiniteVertices++;throw new InvalidOperationException("Hat tailoring produced a nonfinite vertex.");}
                evidence.maximumDisplacement=Mathf.Max(evidence.maximumDisplacement,(deformed-point).magnitude);
                if(normals.Length==vertices.Length)
                {
                    Vector3 normal=normalToRoot.MultiplyVector(normals[i]);
                    normals[i]=normalFromRoot.MultiplyVector(jacobian.inverse.transpose.MultiplyVector(normal)).normalized;
                    if(!Finite(normals[i])){evidence.nonfiniteNormals++;throw new InvalidOperationException("Hat tailoring produced a nonfinite normal.");}
                }
                if(tangents.Length==vertices.Length)
                {
                    Vector4 tangent=tangents[i];
                    Vector3 transformed=fromRoot.MultiplyVector(jacobian.MultiplyVector(toRoot.MultiplyVector(new Vector3(tangent.x,tangent.y,tangent.z))));
                    if(normals.Length==vertices.Length)transformed-=normals[i]*Vector3.Dot(transformed,normals[i]);
                    transformed.Normalize();tangents[i]=new Vector4(transformed.x,transformed.y,transformed.z,tangent.w);
                    if(!Finite(transformed)||!Finite(tangent.w))
                    {evidence.nonfiniteTangents++;throw new InvalidOperationException("Hat tailoring produced a nonfinite tangent.");}
                }
            }
            mesh.vertices=vertices;
            if(normals.Length==vertices.Length)mesh.normals=normals;
            if(tangents.Length==vertices.Length)mesh.tangents=tangents;
            mesh.RecalculateBounds();
        }

        static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x)&&Finite(value.y)&&Finite(value.z);

        internal static Matrix4x4 Jacobian(Vector3 point,ModularHatFitRepair.OpeningDescriptor opening,float heightScale,float radialScale,
            Control[] controls,float anchorY)
        {
            const float step=.0005f;
            Vector3 x=(Deform(point+Vector3.right*step,opening,heightScale,radialScale,controls,anchorY)-Deform(point-Vector3.right*step,opening,heightScale,radialScale,controls,anchorY))/(2*step);
            Vector3 y=(Deform(point+Vector3.up*step,opening,heightScale,radialScale,controls,anchorY)-Deform(point-Vector3.up*step,opening,heightScale,radialScale,controls,anchorY))/(2*step);
            Vector3 z=(Deform(point+Vector3.forward*step,opening,heightScale,radialScale,controls,anchorY)-Deform(point-Vector3.forward*step,opening,heightScale,radialScale,controls,anchorY))/(2*step);
            var matrix=Matrix4x4.identity;
            matrix.SetColumn(0,new Vector4(x.x,x.y,x.z,0));matrix.SetColumn(1,new Vector4(y.x,y.y,y.z,0));matrix.SetColumn(2,new Vector4(z.x,z.y,z.z,0));
            return matrix;
        }

        static void EnsureFolder(string path)
        {
            string[] parts=path.Split('/');string current=parts[0];
            for(int i=1;i<parts.Length;i++)
            {string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
    }
}
