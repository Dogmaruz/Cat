from pathlib import Path
p=Path('DancingCatsClone/Assets/RadioArchipelago/Editor/ModularSourceHarnessFitter.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('public static void BakeAll(CourierCatalog catalog)\n        {','''public static void BakeAll(CourierCatalog catalog) { Bake(catalog,null,null); }

        /// <summary>Fast targeted native inspection after a source-fitting change.</summary>
        public static void Probe()
        {
            Bake(AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset"),"Rumi",
                new[]{"backpack_Tiko","backpack_Nori","backpack_Rumi","backpack_Miro"});
        }

        static void Bake(CourierCatalog catalog,string heroFilter,string[] itemFilter)
        {''',1)
s=s.replace('foreach(var hero in catalog.entries)','foreach(var hero in catalog.entries.Where(h=>heroFilter==null||h.assetId==heroFilter))',1)
s=s.replace('catalog.equipment.Where(x=>x.kind==CourierEquipmentKind.Backpack)','catalog.equipment.Where(x=>x.kind==CourierEquipmentKind.Backpack&&(itemFilter==null||itemFilter.Contains(x.id)))',1)
s=s.replace('string evidencePath=Path.GetFullPath("../Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair/source-tailoring.json");','int expected=heroFilter==null?200:itemFilter.Length;\n            string evidencePath=Path.GetFullPath("../Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair/"+(heroFilter==null?"source-tailoring.json":"source-tailoring-probe.json"));',1)
s=s.replace('passed=report.Count==200','passed=report.Count==expected',1).replace('if(report.Count!=200)throw new InvalidDataException("Expected 200 source harness fits, got "+report.Count);','if(report.Count!=expected)throw new InvalidDataException("Expected "+expected+" source harness fits, got "+report.Count);',1)
s=s.replace('Debug.Log("MODULAR_SOURCE_HARNESS PASS:200 authored fits, "+meshes','Debug.Log("MODULAR_SOURCE_HARNESS PASS:"+report.Count+" authored fits, "+meshes',1)
a=s.index('            for(int i=0;i<Samples;i++)\n            {\n                float angle=i*Mathf.PI*2/Samples;var tangent=(centers')
b=s.index('            float sourceSeam=',a)
s=s[:a]+'''            var radialAxes=new Vector3[Samples];var normalAxes=new Vector3[Samples];
            var radialVariances=new List<float>();var normalVariances=new List<float>();
            for(int i=0;i<Samples;i++)
            {
                float angle=i*Mathf.PI*2/Samples;var tangent=(centers[(i+1)%Samples]-centers[(i-1+Samples)%Samples]).normalized;
                var radial=(horizontal*Mathf.Sin(angle)+Vector3.up*Mathf.Cos(angle));radial=Vector3.ProjectOnPlane(radial,tangent).normalized;
                if(radial.sqrMagnitude<.01f)radial=Vector3.ProjectOnPlane(planeNormal,tangent).normalized;
                var normal=Vector3.Cross(tangent,radial).normalized;
                if(Vector3.Dot(normal,planeNormal)<0)normal=-normal;
                radialAxes[i]=radial;normalAxes[i]=normal;
                double aa=0,bb=0;int count=0;
                for(int k=0;k<points.Length;k++)
                {
                    if(Mathf.Abs(Mathf.DeltaAngle(angles[k]*Mathf.Rad2Deg,angle*Mathf.Rad2Deg))>10)continue;
                    var delta=points[k]-centers[i];double x=Vector3.Dot(delta,radial),y=Vector3.Dot(delta,normal);aa+=x*x;bb+=y*y;count++;
                }
                if(count>12){radialVariances.Add((float)(aa/count));normalVariances.Add((float)(bb/count));}
            }
            // Source strips can have their broad face radial OR normal to the oval.
            // Choose that physical orientation once per supplied strip. Per-bin PCA
            // eigenvectors are unoriented lines and can introduce a half-turn at 0/360.
            // This continuous frame has the same orientation on both sides of the seam.
            bool radialIsWidth=Median(radialVariances)>Median(normalVariances);
            for(int i=0;i<Samples;i++)
            {
                broad[i]=radialIsWidth?radialAxes[i]:normalAxes[i];
                thin[i]=radialIsWidth?normalAxes[i]:radialAxes[i];
            }
''' +s[b:]
out=Path('ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/ModularSourceHarnessFitter-stable.cs.txt');out.write_text(s,encoding='utf-8')
print(out)
