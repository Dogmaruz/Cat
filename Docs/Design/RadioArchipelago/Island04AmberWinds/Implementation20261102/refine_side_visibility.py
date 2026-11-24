from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''            public int visibleWaterPixels;
            public Vector3[] projectedTopMidBase;''','''            public int visibleWaterPixels;
            public int[] topMidBaseVisiblePixels;
            public Vector3[] projectedTopMidBase;''')
start=s.index('''        private static void CaptureSideCascadeVisibility(''')
end=s.index('''        private static void Tick()''',start)
new='''        private static void CaptureSideCascadeVisibility(IslandStageDefinition definition, View view)
        {
            float previous = view.selectedStation;
            Renderer[] measuredWater = view.waters;
            var proofs = new List<CascadeVisibility>();
            try
            {
                // Include the explicit spring/roll/curtain together for endpoint
                // visibility; temporal fall measurements retain their own target subset.
                view.waters = view.owner.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.StartsWith(SideCascadePrefix,StringComparison.Ordinal) &&
                        renderer.sharedMaterials.Any(material => material && material.shader && material.shader.name == ShaderName)).ToArray();
                foreach (float station in new[] {155f,170f,185f})
                {
                    view.UseGameplayCamera(definition,station);
                    view.Capture(LiveFolder + "/side-cascade-route-" + station.ToString("000",System.Globalization.CultureInfo.InvariantCulture) + ".png");
                    var mask = view.VisibleWaterMask();
                    var projected = SideCascadeTargets.Select(point => view.camera.WorldToViewportPoint(point)).ToArray();
                    proofs.Add(new CascadeVisibility {station = station,visibleWaterPixels = mask.Count(pixel => pixel.r > 0),
                        projectedTopMidBase = projected,topMidBaseVisiblePixels = projected.Select(point => VisibleNear(mask,view.width,view.height,point)).ToArray(),
                        allTargetsInsidePortrait = projected.All(point => point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1)});
                }
            }
            finally { view.waters = measuredWater; view.UseGameplayCamera(definition,previous); }
            File.WriteAllText(LiveFolder + "/side-cascade-visibility.json",JsonUtility.ToJson(new CascadeVisibilityReport {stations = proofs.ToArray()},true));
            if (proofs.Any(proof => proof.visibleWaterPixels < 500 || !proof.allTargetsInsidePortrait || proof.topMidBaseVisiblePixels.Any(count => count < 5)))
                throw new InvalidOperationException("The explicit side waterfall top, middle and base must be visible through full scenery from all three representative normal route cameras.");
        }
        private static int VisibleNear(Color32[] mask, int width, int height, Vector3 viewport)
        {
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return 0;
            int x = Mathf.RoundToInt(viewport.x*(width-1)), y = Mathf.RoundToInt(viewport.y*(height-1)), count = 0;
            for (int dy = -10; dy <= 10; dy++) for (int dx = -10; dx <= 10; dx++)
            {
                int px = x+dx,py = y+dy;
                if (dx*dx+dy*dy <= 100 && px >= 0 && px < width && py >= 0 && py < height && mask[py*width+px].r > 0) count++;
            }
            return count;
        }

'''
s=s[:start]+new+s[end:]
p.write_text(s,encoding='utf8')
print('Added occlusion-aware top/mid/base mask witnesses, with original target subset restored before temporal measurement.')
