from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'
s=p.read_text(encoding='utf8')
s=s.replace('''                AmberWindsTerrainChecks.RunSceneryChecks(definition);''','''                var renderers = definition.Prefab.GetComponentsInChildren<MeshRenderer>(true);
                report.world.renderers = renderers.Length;
                report.world.materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().Count();
                foreach (var filter in definition.Prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        report.world.triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                AmberWindsTerrainChecks.RunSceneryChecks(definition);''',1)
s=s.replace('''            IslandLivingNature nature = null; IslandRooftopMotion motion = null;
            try''','''            IslandLivingNature nature = null; IslandRooftopMotion motion = null;
            var captureMilliseconds = new System.Collections.Generic.List<double>();
            try''',1)
old='''                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                    RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(false, false);
                    File.WriteAllBytes(folder + "/" + (i == 0 ? "overview" : "route-" + i) + ".png", pixels.EncodeToPNG());'''
new='''                    captureMilliseconds.Add(CaptureSceneryFrame(camera, texture, pixels,
                        folder + "/" + (i == 0 ? "overview" : "route-" + i) + ".png"));'''
assert old in s;s=s.replace(old,new,1)
old='''                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                    RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(false, false);
                    File.WriteAllBytes(folder + "/portrait-route-" + station.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".png", pixels.EncodeToPNG());'''
new='''                    captureMilliseconds.Add(CaptureSceneryFrame(camera, texture, pixels,
                        folder + "/portrait-route-" + station.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".png"));'''
assert old in s;s=s.replace(old,new,1)
needle='''                }
            }
            finally
            {
                motion?.Dispose(); nature?.Dispose();'''
replacement='''                }
                File.WriteAllText(folder + "/preview-timings.json", JsonUtility.ToJson(new PreviewTimingReport {
                    frames = captureMilliseconds.Count, minimumMilliseconds = captureMilliseconds.Min(),
                    meanMilliseconds = captureMilliseconds.Average(), maximumMilliseconds = captureMilliseconds.Max() }, true));
            }
            finally
            {
                motion?.Dispose(); nature?.Dispose();'''
assert needle in s;s=s.replace(needle,replacement,1)
needle='''        private static void RestoreQuality(byte[] bytes)
'''
methods='''        [Serializable] private sealed class PreviewTimingReport
        {
            public string scope = "Synchronous Editor offscreen scenery captures, mixed1536x1024/720x1280 resolutions. Includes render submission and GPU readback; excludes PNG encoding/file IO. No actor, chart, gameplay VFX or native frame pacing; this is an art-preview cost observation, not a runtime FPS or phone benchmark.";
            public int frames;
            public double minimumMilliseconds, meanMilliseconds, maximumMilliseconds;
        }

        private static double CaptureSceneryFrame(Camera camera, RenderTexture texture, Texture2D pixels, string path)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(false, false);
            elapsed.Stop();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return elapsed.Elapsed.TotalMilliseconds;
        }

'''
assert needle in s;s=s.replace(needle,methods+needle,1)
p.write_text(s,encoding='utf8')
print('Added source renderer/material/triangle counts and explicitly scoped Editor capture cost observations.')
